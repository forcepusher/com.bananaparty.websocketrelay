namespace BananaParty.WebSocketRelay
{
    /// <summary>
    /// <see cref="ISocket"/> implementation for offline mode that connects instantly
    /// and discards outgoing payloads, so multiplayer code runs without a relay server.
    /// </summary>
    public class OfflineSocket : ISocket
    {
        public bool IsConnected { get; private set; }

        public bool HasUnreadPayloadQueue => false;

        public byte[] ReadPayloadQueue()
        {
            throw new System.InvalidOperationException($"Trying to use {nameof(ReadPayloadQueue)} while {nameof(HasUnreadPayloadQueue)} is false.");
        }

        public void Connect()
        {
            IsConnected = true;
        }

        public void Send(byte[] payloadBytes)
        {
            if (!IsConnected)
                throw new System.InvalidOperationException($"Trying to use {nameof(Send)} while not {nameof(IsConnected)}.");
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
