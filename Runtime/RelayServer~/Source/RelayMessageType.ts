export const RelayMessageType = {
    Subscribe: 0x01,
    Unsubscribe: 0x02,
    ChannelMessage: 0x03,
} as const;

export const RelayMessageGuidSize = 16;

export const RelayMessageChannelLengthOffset = 1;
export const RelayMessageChannelOffset = 3;

export const RelayMessageChannelMessageGuidOffset = 1;
export const RelayMessageChannelMessageChannelLengthOffset = RelayMessageChannelMessageGuidOffset + RelayMessageGuidSize;
export const RelayMessageChannelMessageChannelOffset = RelayMessageChannelMessageChannelLengthOffset + 2;

const relayMessageTypeNames: Record<number, string> = {
    [RelayMessageType.Subscribe]: "Subscribe",
    [RelayMessageType.Unsubscribe]: "Unsubscribe",
    [RelayMessageType.ChannelMessage]: "ChannelMessage",
};

export function relayMessageTypeName(type: number): string {
    return relayMessageTypeNames[type] ?? `Unknown(0x${type.toString(16)})`;
}

export function relayGuidToBytes(guid: string): Uint8Array {
    const hex = guid.replaceAll("-", "");
    const bytes = new Uint8Array(RelayMessageGuidSize);
    for (let i = 0; i < RelayMessageGuidSize; i++) {
        bytes[i] = Number.parseInt(hex.slice(i * 2, i * 2 + 2), 16);
    }
    return bytes;
}

export function relayReadGuid(message: Uint8Array, offset: number = 1): string {
    const hex = Array.from(message.subarray(offset, offset + RelayMessageGuidSize), (byte) =>
        byte.toString(16).padStart(2, "0"),
    ).join("");
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export function relayReadChannelLength(message: Uint8Array, channelLengthOffset: number = RelayMessageChannelLengthOffset): number {
    if (message.byteLength < channelLengthOffset + 2) return -1;
    return new DataView(message.buffer, message.byteOffset, message.byteLength).getUint16(
        channelLengthOffset,
        true,
    );
}

export function relayPayloadOffset(channelLength: number): number {
    return RelayMessageChannelOffset + channelLength;
}

export function relayChannelMessagePayloadOffset(channelLength: number): number {
    return RelayMessageChannelMessageChannelOffset + channelLength;
}

export function relayReadChannel(message: Uint8Array, channelLengthOffset: number = RelayMessageChannelLengthOffset): string {
    const channelLength = relayReadChannelLength(message, channelLengthOffset);
    if (channelLength < 0) return "";
    const channelOffset = channelLengthOffset + 2;
    return new TextDecoder().decode(
        message.subarray(channelOffset, channelOffset + channelLength),
    );
}

export function relayWriteProtocolMessage(type: number, channel: string, payload?: Uint8Array): Uint8Array {
    const channelBytes = new TextEncoder().encode(channel);
    const payloadLength = payload?.byteLength ?? 0;
    const message = new Uint8Array(relayPayloadOffset(channelBytes.byteLength) + payloadLength);
    const view = new DataView(message.buffer);
    view.setUint8(0, type);
    view.setUint16(RelayMessageChannelLengthOffset, channelBytes.byteLength, true);
    message.set(channelBytes, RelayMessageChannelOffset);
    if (payload) message.set(payload, relayPayloadOffset(channelBytes.byteLength));
    return message;
}

export function relayWriteChannelMessage(senderGuid: string, channel: string, payload?: Uint8Array): Uint8Array {
    const channelBytes = new TextEncoder().encode(channel);
    const payloadLength = payload?.byteLength ?? 0;
    const message = new Uint8Array(relayChannelMessagePayloadOffset(channelBytes.byteLength) + payloadLength);
    const view = new DataView(message.buffer);
    view.setUint8(0, RelayMessageType.ChannelMessage);
    message.set(relayGuidToBytes(senderGuid), RelayMessageChannelMessageGuidOffset);
    view.setUint16(RelayMessageChannelMessageChannelLengthOffset, channelBytes.byteLength, true);
    message.set(channelBytes, RelayMessageChannelMessageChannelOffset);
    if (payload) message.set(payload, relayChannelMessagePayloadOffset(channelBytes.byteLength));
    return message;
}
