using System;

namespace BananaParty.WebSocketRelay
{
    /// <summary>
    /// Universal default implementation of <see cref="ISocket"/> that picks
    /// the platform-specific implementation on <see cref="Connect"/>.
    /// </summary>
    public class Socket : ISocket
    {
        private readonly string _serverAddress;

        private ISocket _platformSocket;

        public Socket(string serverAddress)
        {
            _serverAddress = serverAddress;
        }

        public bool IsConnected => _platformSocket != null && _platformSocket.IsConnected;

        public bool HasUnreadPayloadQueue => _platformSocket != null && _platformSocket.HasUnreadPayloadQueue;

        public byte[] ReadPayloadQueue()
        {
            if (_platformSocket == null)
                throw new InvalidOperationException($"Trying to use {nameof(ReadPayloadQueue)} before calling {nameof(Connect)}.");

            return _platformSocket.ReadPayloadQueue();
        }

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _platformSocket = new BrowserSocket(_serverAddress);
#else
            _platformSocket = new StandaloneSocket(_serverAddress);
#endif

            _platformSocket.Connect();
        }

        public void Send(byte[] payloadBytes)
        {
            if (!IsConnected)
                throw new InvalidOperationException($"Trying to use {nameof(Send)} while not {nameof(IsConnected)}.");

            _platformSocket.Send(payloadBytes);
        }

        public void Disconnect()
        {
            if (_platformSocket == null)
                throw new InvalidOperationException($"Trying to use {nameof(Disconnect)} before calling {nameof(Connect)}.");

            _platformSocket.Disconnect();
        }

        public void Dispose()
        {
            _platformSocket?.Dispose();
        }
    }
}
