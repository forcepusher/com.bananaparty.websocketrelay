using System;

namespace BananaParty.WebSocketRelay
{
    public interface ISocket : IDisposable
    {
        bool IsConnected { get; }

        bool HasUnreadPayloadQueue { get; }

        byte[] ReadPayloadQueue();

        /// <summary>
        /// Operation is not immediate. Check <see cref="IsConnected"/> for connection status.
        /// </summary>
        void Connect();

        void Send(byte[] payloadBytes);

        /// <summary>
        /// Operation is not immediate. Check <see cref="IsConnected"/> for connection status.
        /// </summary>
        void Disconnect();
    }
}
