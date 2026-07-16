using System;
using BananaParty.WebSocketRelay.Transport;

namespace BananaParty.WebSocketRelay.Tests
{
    public class TestRelayListener : IRelayListener
    {
        public event Action Disconnected;
        public event Action<Guid, string, byte[]> ChannelMessageReceived;

        public void OnDisconnectedFromRelay()
            => Disconnected?.Invoke();

        public void OnChannelMessage(Guid senderGuid, string channel, byte[] data)
            => ChannelMessageReceived?.Invoke(senderGuid, channel, data);
    }
}
