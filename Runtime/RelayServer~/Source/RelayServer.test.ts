import { afterAll, beforeAll, describe, expect, test } from "bun:test";
import { RelayServer } from "./RelayServer";
import {
    RelayMessageType,
    RelayMessageChannelMessageChannelLengthOffset,
    relayReadGuid,
    relayReadChannel,
    relayReadChannelLength,
    relayChannelMessagePayloadOffset,
    relayWriteProtocolMessage,
    relayWriteChannelMessage,
} from "./RelayMessageType";

const testPort = 23145;

function subscribe(ws: WebSocket, channel: string): void {
    ws.send(relayWriteProtocolMessage(RelayMessageType.Subscribe, channel));
}

async function subscribeAndSettle(ws: WebSocket, channel: string): Promise<void> {
    subscribe(ws, channel);
    await new Promise((resolve) => setTimeout(resolve, 10));
}

function unsubscribe(ws: WebSocket, channel: string): void {
    ws.send(relayWriteProtocolMessage(RelayMessageType.Unsubscribe, channel));
}

function sendChannelMessage(ws: WebSocket, senderGuid: string, channel: string, payload: Uint8Array): void {
    ws.send(relayWriteChannelMessage(senderGuid, channel, payload));
}

async function toUint8Array(data: unknown): Promise<Uint8Array> {
    if (data instanceof ArrayBuffer) return new Uint8Array(data);
    if (data instanceof Uint8Array) return data;
    if (data instanceof Blob) return new Uint8Array(await data.arrayBuffer());
    throw new Error(`Unexpected binary frame type: ${typeof data}`);
}

async function receiveBinary(ws: WebSocket, timeoutMs = 2000): Promise<Uint8Array> {
    return await new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error("Timed out waiting for message")), timeoutMs);

        ws.onmessage = async (event) => {
            clearTimeout(timer);
            resolve(await toUint8Array(event.data));
        };
    });
}

async function openSocket(clientGuid = crypto.randomUUID()): Promise<{ ws: WebSocket; clientGuid: string }> {
    const ws = new WebSocket(`ws://127.0.0.1:${testPort}`);
    ws.binaryType = "arraybuffer";
    await new Promise<void>((resolve, reject) => {
        ws.onopen = () => resolve();
        ws.onerror = () => reject(new Error("WebSocket connection failed"));
    });

    return { ws, clientGuid };
}

async function expectNoMessage(ws: WebSocket, timeoutMs = 100): Promise<void> {
    let unexpectedMessage = false;
    ws.onmessage = () => {
        unexpectedMessage = true;
    };

    await new Promise((resolve) => setTimeout(resolve, timeoutMs));
    expect(unexpectedMessage).toBe(false);
}

describe("RelayServer", () => {
    const server = new RelayServer(testPort);

    beforeAll(() => {
        server.start();
    });

    afterAll(() => {
        server.stop();
    });

    test("connection does not send messages on open", async () => {
        const { ws } = await openSocket();
        await expectNoMessage(ws);
        ws.close();
    });

    test("subscribe does not send confirmation", async () => {
        const { ws } = await openSocket();
        subscribe(ws, "lobby");
        await expectNoMessage(ws);
        ws.close();
    });

    test("duplicate subscribe does not send a message", async () => {
        const { ws } = await openSocket();
        subscribe(ws, "events");
        await expectNoMessage(ws);

        subscribe(ws, "events");
        await expectNoMessage(ws);

        ws.close();
    });

    test("relays channel messages with client-provided sender guid", async () => {
        const sender = await openSocket();
        const receiver = await openSocket();

        await subscribeAndSettle(sender.ws, "chat");
        await subscribeAndSettle(receiver.ws, "chat");

        sendChannelMessage(sender.ws, sender.clientGuid, "chat", new Uint8Array([0xaa, 0xbb]));

        const response = await receiveBinary(receiver.ws);
        expect(response[0]).toBe(RelayMessageType.ChannelMessage);
        expect(relayReadGuid(response, 1)).toBe(sender.clientGuid);
        expect(relayReadChannel(response, RelayMessageChannelMessageChannelLengthOffset)).toBe("chat");
        expect(
            Array.from(
                response.subarray(
                    relayChannelMessagePayloadOffset(
                        relayReadChannelLength(response, RelayMessageChannelMessageChannelLengthOffset),
                    ),
                ),
            ),
        ).toEqual([0xaa, 0xbb]);

        sender.ws.close();
        receiver.ws.close();
    });

    test("does not relay to clients on other channels", async () => {
        const sender = await openSocket();
        const otherChannelClient = await openSocket();

        await subscribeAndSettle(sender.ws, "alpha");
        await subscribeAndSettle(otherChannelClient.ws, "beta");

        sendChannelMessage(sender.ws, sender.clientGuid, "alpha", new Uint8Array([0x01]));

        await expectNoMessage(otherChannelClient.ws);

        sender.ws.close();
        otherChannelClient.ws.close();
    });

    test("relays channel message even when sender is not subscribed to channel", async () => {
        const sender = await openSocket();
        const receiver = await openSocket();

        await subscribeAndSettle(receiver.ws, "game");

        sendChannelMessage(sender.ws, sender.clientGuid, "game", new Uint8Array([0x99]));

        const response = await receiveBinary(receiver.ws);
        expect(response[0]).toBe(RelayMessageType.ChannelMessage);
        expect(relayReadGuid(response, 1)).toBe(sender.clientGuid);

        sender.ws.close();
        receiver.ws.close();
    });

    test("unsubscribe does not send confirmation", async () => {
        const { ws } = await openSocket();

        unsubscribe(ws, "missing");
        await expectNoMessage(ws);

        ws.close();
    });
});
