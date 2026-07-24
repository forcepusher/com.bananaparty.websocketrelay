using System;
using System.Collections.Generic;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class NetworkIdentity : MonoBehaviour, INetworkIdentity, IRpcTarget
    {
        private const string ClaimAuthorityRequesterGuidKey = nameof(ClaimAuthorityRequesterGuidKey);
        private const string ClaimAuthorityVersionKey = nameof(ClaimAuthorityVersionKey);

        [SerializeField]
        private NetworkContext _networkContext;
        [SerializeField]
        private string _prefabName;
        [SerializeField]
        private bool _distanceBasedAuthority;
        [SerializeField]
        private bool _destroyWhenAuthorityOwnerLeaves = true;

        private readonly List<INetworkState> _networkStates = new();

        public GameObject GameObject => gameObject;
        public string PrefabName => _prefabName;
        public string Channel { get; set; }
        public Guid NetworkIdentifier { get; set; }
        public Guid NetworkAuthorityOwner { get; set; }

        // Incremented on every authority claim. Lets receivers discard in-flight ownership
        // and component state written by a previous owner after an authority transfer.
        public long NetworkAuthorityVersion { get; set; }
        public bool NetworkAuthority => _networkContext.LocalClientIdentity == NetworkAuthorityOwner;
        public bool HasAuthorityOwner => NetworkAuthorityOwner != Guid.Empty;

        public bool DistanceBasedAuthority => _distanceBasedAuthority;
        public bool DestroyWhenAuthorityOwnerLeaves => _destroyWhenAuthorityOwnerLeaves;
        public NetworkContext NetworkContext => _networkContext;

        public string NetworkStateName => _prefabName;

        public string RpcSubjectName => nameof(ClaimAuthority);

        INetworkIdentity IRpcTarget.NetworkIdentity => this;

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
            stateOutput.WriteGuid(nameof(NetworkAuthorityOwner), NetworkAuthorityOwner);
            stateOutput.WriteLong(nameof(NetworkAuthorityVersion), NetworkAuthorityVersion);

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
            if (!ReadNetworkAuthorityOwner(stateInput))
                return;

            ReadComponentStates(stateInput);
        }

        public bool ReadNetworkState(IStateInput stateInput, Guid senderGuid)
        {
            // Authority owner is applied first so a client that missed a ClaimAuthority RPC
            // still converges on the owner carried by the authority owner's state broadcasts.
            // Returns false for state written before the latest known authority claim,
            // e.g. the previous owner's in-flight broadcasts right after a transfer.
            if (!ReadNetworkAuthorityOwner(stateInput))
                return false;

            // Ignore stale component state from a client that is no longer the authority owner.
            // The state input is per-identity, so abandoning it mid-object is safe.
            if (senderGuid != NetworkAuthorityOwner)
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

        private bool ReadNetworkAuthorityOwner(IStateInput stateInput)
        {
            string prefabName = stateInput.ReadString(nameof(PrefabName));
            if (prefabName != PrefabName)
                throw new InvalidOperationException($"Prefab name mismatch. Expected: {PrefabName}, Received: {prefabName}");

            Guid networkAuthorityOwner = stateInput.ReadGuid(nameof(NetworkAuthorityOwner));
            long networkAuthorityVersion = stateInput.ReadLong(nameof(NetworkAuthorityVersion));

            return TryApplyAuthority(networkAuthorityOwner, networkAuthorityVersion);
        }

        private bool TryApplyAuthority(Guid networkAuthorityOwner, long networkAuthorityVersion)
        {
            if (networkAuthorityVersion < NetworkAuthorityVersion)
                return false;

            // Concurrent claims can produce the same version with different owners.
            // The smaller owner guid wins so every client converges on the same owner.
            if (networkAuthorityVersion == NetworkAuthorityVersion
                && networkAuthorityOwner != NetworkAuthorityOwner
                && networkAuthorityOwner.CompareTo(NetworkAuthorityOwner) > 0)
                return false;

            NetworkAuthorityOwner = networkAuthorityOwner;
            NetworkAuthorityVersion = networkAuthorityVersion;
            return true;
        }

        public void SendRpc(string rpcSubjectName, IStateOutput parametersStateOutput, bool invokeLocally = true)
        {
            _networkContext.SendRpc(NetworkIdentifier, rpcSubjectName, parametersStateOutput, Channel, invokeLocally);
        }

        public void ClaimAuthority()
        {
            IStateOutput parametersStateOutput = _networkContext.StateFormat.CreateOutput();
            parametersStateOutput.WriteGuid(ClaimAuthorityRequesterGuidKey, _networkContext.LocalClientIdentity);
            parametersStateOutput.WriteLong(ClaimAuthorityVersionKey, NetworkAuthorityVersion + 1);
            SendRpc(RpcSubjectName, parametersStateOutput);
        }

        public void ReceiveRpc(IStateInput parametersStateInput)
        {
            Guid requesterGuid = parametersStateInput.ReadGuid(ClaimAuthorityRequesterGuidKey);
            long claimVersion = parametersStateInput.ReadLong(ClaimAuthorityVersionKey);
            TryApplyAuthority(requesterGuid, claimVersion);
        }

        private void OnValidate()
        {
            _prefabName = transform.name;
        }
    }
}
