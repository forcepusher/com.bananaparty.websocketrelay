using System;
using System.Collections.Generic;

namespace BananaParty.WebSocketRelay
{
    public class NetworkIdentityRegistry
    {
        private readonly List<INetworkIdentity> _identities = new();
        private readonly Dictionary<Guid, INetworkIdentity> _identitiesByGuid = new();

        public IReadOnlyList<INetworkIdentity> Identities => _identities;

        public void Register(INetworkIdentity networkIdentity)
        {
            _identities.Add(networkIdentity);
            _identitiesByGuid[networkIdentity.NetworkIdentifier] = networkIdentity;
        }

        public void Unregister(INetworkIdentity networkIdentity)
        {
            _identities.Remove(networkIdentity);
            _identitiesByGuid.Remove(networkIdentity.NetworkIdentifier);
        }

        public bool TryGet(Guid networkIdentifier, out INetworkIdentity networkIdentity)
        {
            return _identitiesByGuid.TryGetValue(networkIdentifier, out networkIdentity);
        }
    }
}
