async function httpRequestImpl(method: string, uri: string, payload?: object) {
    const headers = {
        Accept: "application/json",
        "Content-Type": "application/json"
    };

    const response = await fetch(uri, {
        method,
        headers,
        body: payload ? JSON.stringify(payload) : undefined
    });

    if (!response.ok) {
        throw new HttpError(uri, response.status, response.statusText);
    }

    return response;
}

export function httpGet(uri: string) {
    return httpRequestImpl("GET", uri, undefined);
}

export function httpPost(uri: string, payload: object) {
    return httpRequestImpl("POST", uri, payload);
}

export function httpDelete(uri: string, payload: object) {
    return httpRequestImpl("DELETE", uri, payload);
}

export class HttpError extends Error {
    public readonly status: number;

    constructor(uri: string, status: number, statusText?: string) {
        let description = `${uri}: ${status}`;

        if (typeof statusText === "string") {
            description += ` (${statusText})`;
        }

        super(description);
        this.status = status;
    }
}
