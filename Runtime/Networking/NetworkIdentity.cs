using System;
using System.Collections.Generic;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class NetworkIdentity : MonoBehaviour, INetworkIdentity
    {
        [SerializeField]
        private NetworkContext _networkContext;
        [SerializeField]
        private string _prefabName;
        [SerializeField]
        private bool _distanceBasedAuthority;

        private readonly List<INetworkState> _networkStates = new();

        public GameObject GameObject => gameObject;
        public string PrefabName => _prefabName;
        public string Channel { get; set; }
        public Guid NetworkIdentifier { get; set; }
        public Guid NetworkOwner { get; set; }
        public bool NetworkAuthority => _networkContext.LocalClientIdentity == NetworkOwner;

        public bool DistanceBasedAuthority => _distanceBasedAuthority;
        public NetworkContext NetworkContext => _networkContext;

        public string NetworkStateName => _prefabName;

        private void Awake()
        {
            foreach (INetworkState networkState in GetComponents<INetworkState>())
            {
                if (ReferenceEquals(networkState, this))
                    continue;

                _networkStates.Add(networkState);
            }
        }

        public void WriteNetworkState(IStateOutput stateOutput)
        {
            stateOutput.WriteString(nameof(PrefabName), PrefabName);
            stateOutput.WriteGuid(nameof(NetworkOwner), NetworkOwner);

            stateOutput.BeginArrayProperty("NetworkStates");
            foreach (INetworkState networkState in _networkStates)
            {
                stateOutput.BeginObjectElement();
                networkState.WriteNetworkState(stateOutput);
                stateOutput.EndObject();
            }
            stateOutput.EndArray();
        }

        public void ReadNetworkState(IStateInput stateInput)
        {
            ReadOwnership(stateInput);
            ReadComponentStates(stateInput);
        }

        public bool ReadNetworkState(IStateInput stateInput, Guid senderGuid)
        {
            // Ownership is applied first so a client that missed a TakeAuthority RPC
            // still converges on the owner carried by the owner's state broadcasts.
            ReadOwnership(stateInput);

            // Ignore stale component state from a client that is no longer the owner,
            // e.g. right after a distance-based authority transfer.
            // The state input is per-identity, so abandoning it mid-object is safe.
            if (senderGuid != NetworkOwner)
                return false;

            ReadComponentStates(stateInput);
            return true;
        }

        internal void ReadComponentStates(IStateInput stateInput)
        {
            stateInput.BeginArrayProperty("NetworkStates");
            foreach (INetworkState networkState in _networkStates)
            {
                stateInput.BeginObjectElement();
                networkState.ReadNetworkState(stateInput);
                stateInput.EndObject();
            }
            stateInput.EndArray();
        }

        private void ReadOwnership(IStateInput stateInput)
        {
            string prefabName = stateInput.ReadString(nameof(PrefabName));
            if (prefabName != PrefabName)
                throw new InvalidOperationException($"Prefab name mismatch. Expected: {PrefabName}, Received: {prefabName}");

            NetworkOwner = stateInput.ReadGuid(nameof(NetworkOwner));
        }

        public void SendRpc(string rpcSubjectName, IStateOutput parametersStateOutput, bool invokeLocally = true)
        {
            _networkContext.SendRpc(NetworkIdentifier, rpcSubjectName, parametersStateOutput, Channel, invokeLocally);
        }

        private void OnValidate()
        {
            _prefabName = transform.name;
        }
    }
}
