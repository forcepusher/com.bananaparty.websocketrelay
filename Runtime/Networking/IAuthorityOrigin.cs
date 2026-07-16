using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public interface IAuthorityOrigin
    {
        Vector3 Position { get; }
        NetworkIdentity NetworkIdentity { get; }
    }
}
