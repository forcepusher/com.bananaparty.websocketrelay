using System;
using System.Buffers.Binary;
using System.Text;

namespace BananaParty.WebSocketRelay.Transport
{
    public static class RelayMessageCodec
    {
        public const int GuidSize = 16;

        public const int ChannelLengthOffset = 1;
        public const int ChannelOffset = 3;

        public const int ChannelMessageGuidOffset = 1;
        public const int ChannelMessageChannelLengthOffset = ChannelMessageGuidOffset + GuidSize;
        public const int ChannelMessageChannelOffset = ChannelMessageChannelLengthOffset + 2;

        public static byte[] CreateProtocolMessage(byte type, string channel, ReadOnlySpan<byte> payload = default)
        {
            byte[] channelBytes = Encoding.UTF8.GetBytes(channel);
            int payloadOffset = ChannelOffset + channelBytes.Length;
            byte[] message = new byte[payloadOffset + payload.Length];
            message[0] = type;
            BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(ChannelLengthOffset), (ushort)channelBytes.Length);
            channelBytes.CopyTo(message.AsSpan(ChannelOffset));
            payload.CopyTo(message.AsSpan(payloadOffset));
            return message;
        }

        public static byte[] CreateChannelMessage(Guid clientId, string channel, ReadOnlySpan<byte> payload = default)
        {
            byte[] channelBytes = Encoding.UTF8.GetBytes(channel);
            int payloadOffset = ChannelMessageChannelOffset + channelBytes.Length;
            byte[] message = new byte[payloadOffset + payload.Length];
            message[0] = RelayMessageType.ChannelMessage;
            WriteGuid(message.AsSpan(ChannelMessageGuidOffset), clientId);
            BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(ChannelMessageChannelLengthOffset), (ushort)channelBytes.Length);
            channelBytes.CopyTo(message.AsSpan(ChannelMessageChannelOffset));
            payload.CopyTo(message.AsSpan(payloadOffset));
            return message;
        }

        public static int ReadChannelLength(ReadOnlySpan<byte> message, int channelLengthOffset = ChannelLengthOffset)
        {
            if (message.Length < channelLengthOffset + 2)
                return -1;

            return BinaryPrimitives.ReadUInt16LittleEndian(message.Slice(channelLengthOffset, 2));
        }

        public static int GetChannelMessagePayloadOffset(int channelLength) => ChannelMessageChannelOffset + channelLength;

        public static string ReadChannel(ReadOnlySpan<byte> message, int channelLengthOffset = ChannelLengthOffset)
        {
            int channelLength = ReadChannelLength(message, channelLengthOffset);
            if (channelLength < 0)
                return string.Empty;

            int channelOffset = channelLengthOffset + 2;
            if (message.Length < channelOffset + channelLength)
                return string.Empty;

            return Encoding.UTF8.GetString(message.Slice(channelOffset, channelLength));
        }

        public static Guid ReadGuid(ReadOnlySpan<byte> message, int offset = 1)
        {
            Span<byte> bytes = stackalloc byte[GuidSize];
            message.Slice(offset, GuidSize).CopyTo(bytes);
            SwapGuidEndianness(bytes);
            return new Guid(bytes);
        }

        public static void WriteGuid(Span<byte> destination, Guid guid)
        {
            guid.TryWriteBytes(destination);
            SwapGuidEndianness(destination);
        }

        // The wire format is big-endian, while Guid stores its first three fields little-endian.
        private static void SwapGuidEndianness(Span<byte> bytes)
        {
            bytes.Slice(0, 4).Reverse();
            bytes.Slice(4, 2).Reverse();
            bytes.Slice(6, 2).Reverse();
        }
    }
}
