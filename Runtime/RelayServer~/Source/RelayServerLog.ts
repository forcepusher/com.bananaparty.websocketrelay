const verboseDebug = process.env.RELAY_DEBUG === "1";

export class RelayServerLog {
    static info(message: string): void {
        console.log(`[WebSocketRelay] ${message}`);
    }

    static debug(message: string): void {
        if (verboseDebug) {
            console.log(`[WebSocketRelay] ${message}`);
        }
    }

    static warn(message: string): void {
        console.warn(`[WebSocketRelay] ${message}`);
    }
}
