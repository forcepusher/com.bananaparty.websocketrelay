using System;

namespace BananaParty.WebSocketRelay
{
    public class NetworkPlayer
    {
        public Guid Guid { get; private set; }

        public float TimeSinceLastMessage { get; set; } = 0f;

        public NetworkPlayer(Guid playerGuid)
        {
            Guid = playerGuid;
        }
    }
}
