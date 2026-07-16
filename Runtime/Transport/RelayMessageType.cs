namespace BananaParty.WebSocketRelay.Transport
{
    public static class RelayMessageType
    {
        public const byte Subscribe = 0x01;
        public const byte Unsubscribe = 0x02;
        public const byte ChannelMessage = 0x03;
    }
}
