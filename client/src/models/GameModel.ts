import { Model, defineModel, watch } from "react-class-model";
import { JuiceboxDisplay } from "./JuiceboxInterfaces";
import { HttpError, httpPost } from "./util/Http";
import { connectWebSocket } from "./util/WebSocket";

// Empty by default so the API is called on the same origin that served the site.
// Set REACT_APP_API_URL (see .env.development) to use a server on another origin.
const apiBaseUrl = (process.env.REACT_APP_API_URL ?? "").replace(/\/+$/, "");
const apiEndpoint = `${apiBaseUrl}/api/members`;

const reconnectDelayMs = 5000;

export const [GameModelProvider, useGameModel, watchGameModel] =
    defineModel<GameModel>();

export class GameModel extends Model {
    @watch public sessionId: number = 0;
    @watch public name: string = "";
    @watch public secretKey: string = "";
    @watch public webSocketUri: string = "";
    @watch public webSocket: WebSocket | null = null;
    @watch public isConnected: boolean = false;
    @watch public display: JuiceboxDisplay | null = null;
    @watch public submittedResponse: boolean = true;

    private connecting: Promise<void> | null = null;
    private reconnectTimer: ReturnType<typeof setTimeout> | null = null;

    /**
     * True once joined, until the session or member is gone. The socket may be reconnecting.
     */
    public get isPlaying() {
        return !!this.sessionId && !!this.name && !!this.secretKey;
    }

    constructor() {
        super();
        this.reset();

        setInterval(() => this.ping(), 10000);
    }

    public reset() {
        if (this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
            this.reconnectTimer = null;
        }

        this.sessionId = 0;
        this.name = "";
        this.secretKey = "";
        this.webSocketUri = "";
        const webSocket = this.webSocket;
        this.webSocket = null;
        webSocket?.close();
        this.isConnected = false;
        this.display = null;
    }

    public async join(password: string, name: string) {
        this.reset();
        try {
            const joinResponse = await httpPost(`${apiEndpoint}/join`, {
                JoinPassword: password,
                MemberName: name
            });
            const joinData = await joinResponse.json();
            this.sessionId = joinData.SessionId;
            this.name = joinData.MemberName;
            this.secretKey = joinData.MemberSecretKey;
        } catch (e) {
            this.reset();
            if (e instanceof HttpError) {
                if (e.status === 404) {
                    return "not_found";
                } else if (e.status === 423) {
                    return "session_locked";
                } else if (e.status === 409) {
                    return "username_taken";
                }
            }

            return "unknown_error";
        }

        // We're a member now. If the socket can't connect yet, keep the identity and keep retrying
        // rather than giving up, which would leave the name taken until it expires.
        await this.connect();
        return "success";
    }

    /**
     * Connects the socket if it isn't already. Only one attempt runs at a time, and it never throws:
     * failures schedule another attempt until the session or member is gone.
     */
    private connect(): Promise<void> {
        if (!this.connecting) {
            this.connecting = this.connectOnce().finally(() => {
                this.connecting = null;
            });
        }

        return this.connecting;
    }

    private async connectOnce() {
        const secretKey = this.secretKey;
        const stillJoined = () => this.isPlaying && this.secretKey === secretKey;
        if (!stillJoined()) {
            return; // left the session, don't reconnect
        }

        if (this.webSocket && this.webSocket.readyState === WebSocket.OPEN) {
            return;
        }

        let endpoint: string;
        try {
            const negotiateResponse = await httpPost(
                `${apiEndpoint}/negotiate`,
                {
                    SessionId: this.sessionId,
                    MemberName: this.name,
                    MemberSecretKey: this.secretKey
                }
            );
            endpoint = (await negotiateResponse.json()).Endpoint;
        } catch (e) {
            if (e instanceof HttpError && e.status === 404) {
                console.log("Session or member no longer exists");
                if (stillJoined()) {
                    this.reset();
                }
            } else {
                console.log("Failed to negotiate connection, will retry", e);
                this.scheduleReconnect();
            }

            return;
        }

        let webSocket: WebSocket;
        try {
            webSocket = await connectWebSocket(endpoint);
        } catch (e) {
            console.log("Failed to connect websocket, will retry", e);
            this.scheduleReconnect();
            return;
        }

        if (!stillJoined()) {
            webSocket.close(); // left the session while connecting
            return;
        }

        const previous = this.webSocket;
        this.webSocketUri = endpoint;
        this.webSocket = webSocket;
        previous?.close();

        webSocket.addEventListener("message", (e) => {
            if (typeof e.data === "string") {
                this.handleMessage(e.data);
            }
        });
        webSocket.addEventListener("close", (e) => {
            if (this.webSocket !== webSocket) {
                return; // replaced or closed by reset()
            }

            console.warn("WebSocket closed", e);
            this.isConnected = false;

            // An attempt already in flight may have seen this socket as open, so retry after it.
            const reconnect = () => this.connect();
            if (this.connecting) {
                this.connecting.then(reconnect);
            } else {
                reconnect();
            }
        });

        this.isConnected = true;
    }

    private scheduleReconnect() {
        if (this.reconnectTimer || !this.isPlaying) {
            return;
        }

        this.reconnectTimer = setTimeout(() => {
            this.reconnectTimer = null;
            this.connect();
        }, reconnectDelayMs);
    }

    private trySend(message: object) {
        if (this.webSocket?.readyState !== WebSocket.OPEN) {
            console.warn("Not connected, message not sent", message);
            return false;
        }

        this.webSocket.send(JSON.stringify(message));
        return true;
    }

    public sendResponse(fields: Map<string, string>) {
        // Leave the form enabled if we couldn't send, so the player can try again once reconnected.
        if (
            this.trySend({
                Type: "Response",
                Fields: Object.fromEntries(fields.entries())
            })
        ) {
            this.submittedResponse = true;
        }
    }

    public sendAction(key: string) {
        this.trySend({
            Type: "Action",
            Key: key
        });
    }

    private handleMessage(json: string) {
        try {
            if (json === "ack" || json.startsWith("fail:")) {
                return;
            }

            const message = JSON.parse(json) as JuiceboxMessage;
            console.log(message);

            if (message.Type === "Display") {
                this.display = message.Display;
                this.submittedResponse = false;
            }
        } catch (e) {
            console.error(e);
        }
    }

    private async ping() {
        if (!this.isPlaying) {
            return;
        }

        const secretKey = this.secretKey;
        try {
            await httpPost(`${apiEndpoint}/ping`, {
                SessionId: this.sessionId,
                MemberName: this.name,
                MemberSecretKey: this.secretKey
            });
        } catch (e) {
            console.error("Failed to ping", e);

            if (
                e instanceof HttpError &&
                e.status === 404 &&
                this.secretKey === secretKey
            ) {
                this.reset(); // session/member destroyed
            }
        }
    }
}

type JuiceboxMessage = DisplayMessage;

interface DisplayMessage {
    readonly Type: "Display";
    readonly Display: JuiceboxDisplay;
}
