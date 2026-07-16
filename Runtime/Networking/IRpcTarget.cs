using System;

namespace BananaParty.WebSocketRelay
{
    public interface IRpcTarget
    {
        INetworkIdentity NetworkIdentity { get; }

        string RpcSubjectName { get; }

        void ReceiveRpc(IStateInput parametersStateInput);
    }
}
