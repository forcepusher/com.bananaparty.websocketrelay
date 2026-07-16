using System;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public interface INetworkIdentity : INetworkState
    {
        string PrefabName { get; }
        GameObject GameObject { get; }
        string Channel { get; set; }
        Guid NetworkIdentifier { get; set; }
        Guid NetworkOwner { get; set; }
        bool NetworkAuthority { get; }
        bool DistanceBasedAuthority { get; }
        bool ReadNetworkState(IStateInput stateInput, Guid senderGuid);
        void SendRpc(string rpcSubjectName, IStateOutput parametersStateOutput, bool invokeLocally = true);
        NetworkContext NetworkContext { get; }
    }
}
