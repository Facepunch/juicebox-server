export function connectWebSocket(address: string, timeoutMs = 10000) {
    return new Promise<WebSocket>((resolve, reject) => {
        const ws = new WebSocket(address);

        const opened = () => {
            resolve(ws);
            cleanup();
        };

        const closed = (e: CloseEvent) => {
            reject(new WebSocketError("WebSocket connection failed", e.code));
            cleanup();
        };

        const timeout = setTimeout(() => {
            ws.close();
            cleanup();
            reject(new WebSocketError("WebSocket connection timed out"));
        }, timeoutMs);

        const cleanup = () => {
            clearTimeout(timeout);
            ws.removeEventListener("open", opened);
            ws.removeEventListener("close", closed);
        };

        ws.addEventListener("open", opened);
        ws.addEventListener("close", closed);
    });
}

export class WebSocketError extends Error {
    public readonly code: number;

    constructor(message: string, code?: number) {
        if (typeof code === "number") {
            message += ` (error code ${code})`;
        }

        super(message);
        this.code = code ?? -1;
    }
}
