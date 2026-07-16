using System;

namespace BananaParty.WebSocketRelay.Transport
{
    public interface IRelayListener
    {
        void OnDisconnectedFromRelay();

        void OnChannelMessage(Guid senderGuid, string channel, byte[] data);
    }
}
