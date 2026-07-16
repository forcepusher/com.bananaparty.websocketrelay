using System;
using System.Collections.Generic;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    [CreateAssetMenu]
    public class NetworkContext : ScriptableObject
    {
        [SerializeField]
        private float _playerTimeoutSeconds = 5f;

        [SerializeField]
        private bool _useBinary = false;

        [SerializeField]
        private List<NetworkIdentity> _networkPrefabs;

        private readonly NetworkIdentityRegistry _identityRegistry = new();
        private readonly NetworkPlayerRoster _playerRoster = new();
        private readonly List<IAuthorityOrigin> _authorityOrigins = new();

        private IStateFormat _stateFormat;
        private RpcRouter _rpcRouter;

        public bool UseBinary => _useBinary;

        public Guid LocalClientIdentity { get; set; }

        public IReadOnlyList<INetworkIdentity> NetworkIdentities => _identityRegistry.Identities;

        public IReadOnlyList<NetworkPlayer> NetworkPlayers => _playerRoster.Players;

        public IReadOnlyList<IAuthorityOrigin> AuthorityOrigins => _authorityOrigins;

        // Created lazily because serialized fields are not assigned yet during field initialization.
        public IStateFormat StateFormat => _stateFormat ??= _useBinary ? new BinaryStateFormat() : new JsonStateFormat();

        private RpcRouter RpcRouter => _rpcRouter ??= new RpcRouter(StateFormat);

        public NetworkIdentity Instantiate(NetworkIdentity networkIdentityPrefab, string channel)
        {
            return Instantiate(networkIdentityPrefab.PrefabName, channel, Guid.NewGuid(), LocalClientIdentity);
        }

        private NetworkIdentity Instantiate(string prefabName, string channel, Guid networkIdentifier, Guid networkOwner)
        {
            NetworkIdentity prefab = _networkPrefabs.Find(networkPrefab => networkPrefab.PrefabName == prefabName);
            if (prefab == null)
                throw new InvalidOperationException($"No network prefab registered with name {prefabName}");

            // Instantiate deactivated so Awake/OnEnable run after identity fields are assigned,
            // otherwise components register themselves using an empty NetworkIdentifier.
            bool prefabWasActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            NetworkIdentity networkIdentity = GameObject.Instantiate(prefab);
            prefab.gameObject.SetActive(prefabWasActive);

            networkIdentity.NetworkIdentifier = networkIdentifier;
            networkIdentity.NetworkOwner = networkOwner;
            networkIdentity.Channel = channel;
            networkIdentity.gameObject.SetActive(prefabWasActive);

            RegisterNetworkIdentity(networkIdentity);

            Debug.Log($"Spawned network identity '{prefabName}' ({networkIdentifier}) owned by {networkOwner}");

            return networkIdentity;
        }

        public void RegisterNetworkIdentity(INetworkIdentity networkIdentity) => _identityRegistry.Register(networkIdentity);

        public void UnregisterNetworkIdentity(INetworkIdentity networkIdentity) => _identityRegistry.Unregister(networkIdentity);

        public void RegisterRpcTarget(IRpcTarget rpcTarget) => RpcRouter.RegisterTarget(rpcTarget);

        public void UnregisterRpcTarget(IRpcTarget rpcTarget) => RpcRouter.UnregisterTarget(rpcTarget);

        public void RegisterAuthorityOrigin(IAuthorityOrigin authorityOrigin) => _authorityOrigins.Add(authorityOrigin);

        public void UnregisterAuthorityOrigin(IAuthorityOrigin authorityOrigin) => _authorityOrigins.Remove(authorityOrigin);

        public void ClearNetworkSession()
        {
            for (int identityIndex = NetworkIdentities.Count - 1; identityIndex >= 0; identityIndex--)
            {
                INetworkIdentity networkIdentity = NetworkIdentities[identityIndex];
                UnregisterNetworkIdentity(networkIdentity);

                if (networkIdentity.GameObject != null)
                    Destroy(networkIdentity.GameObject);
            }

            _playerRoster.Clear();
            RpcRouter.ClearOutgoingMessages();
            LocalClientIdentity = Guid.Empty;
        }

        public void ManualUpdate(float unscaledDeltaTime)
        {
            foreach (Guid playerGuid in _playerRoster.RemoveTimedOut(unscaledDeltaTime, _playerTimeoutSeconds))
            {
                DestroyIdentitiesOwnedBy(playerGuid);
                Debug.Log($"Removed timed out player {playerGuid}");
            }
        }

        private void DestroyIdentitiesOwnedBy(Guid networkOwner)
        {
            for (int identityIndex = NetworkIdentities.Count - 1; identityIndex >= 0; identityIndex--)
            {
                INetworkIdentity networkIdentity = NetworkIdentities[identityIndex];
                if (networkIdentity.NetworkOwner != networkOwner)
                    continue;

                UnregisterNetworkIdentity(networkIdentity);
                Destroy(networkIdentity.GameObject);
            }
        }

        public void ProcessChannelMessage(Guid senderGuid, string channel, byte[] data)
        {
            if (senderGuid == LocalClientIdentity)
                return;

            if (data == null || data.Length == 0)
                throw new InvalidOperationException("Channel message data is null or empty");

            _playerRoster.RecordMessage(senderGuid);

            switch (data[0])
            {
                case NetworkMessage.Rpc:
                    RpcRouter.ProcessIncomingMessage(data);
                    break;
                case NetworkMessage.SyncIdentities:
                    ApplyIncomingChannelState(senderGuid, channel, data.AsMemory(1));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown network message type {data[0]}");
            }
        }

        public void SendRpc(Guid networkIdentifier, string rpcSubjectName, IStateOutput parametersStateOutput, string channel, bool invokeLocally = true)
        {
            RpcRouter.Send(networkIdentifier, rpcSubjectName, parametersStateOutput, channel, invokeLocally);
        }

        public bool TryDequeueOutgoingRpcMessage(out string channel, out byte[] message)
        {
            return RpcRouter.TryDequeueOutgoingMessage(out channel, out message);
        }

        public byte[] GetOwnedNetworkIdentitiesPayload(string channel)
        {
            IStateOutput stateOutput = StateFormat.CreateOutput();
            WriteOwnedNetworkStates(stateOutput, channel);
            return StateFormat.ToPayload(stateOutput);
        }

        private void WriteOwnedNetworkStates(IStateOutput stateOutput, string channel)
        {
            stateOutput.BeginObjectElement();
            foreach (INetworkIdentity networkIdentity in NetworkIdentities)
            {
                if (!networkIdentity.NetworkAuthority || networkIdentity.Channel != channel)
                    continue;

                stateOutput.BeginObjectProperty(networkIdentity.NetworkIdentifier.ToString());
                networkIdentity.WriteNetworkState(stateOutput);
                stateOutput.EndObject();
            }
            stateOutput.EndObject();
        }

        private void ApplyIncomingChannelState(Guid senderGuid, string channel, ReadOnlyMemory<byte> payload)
        {
            foreach (Guid networkIdentifier in StateFormat.GetRootIdentityIds(payload))
                ApplyIncomingNetworkIdentity(senderGuid, channel, networkIdentifier, StateFormat.CreateInput(payload));
        }

        private void ApplyIncomingNetworkIdentity(Guid senderGuid, string channel, Guid networkIdentifier, IStateInput stateInput)
        {
            stateInput.BeginObjectElement();
            stateInput.BeginObjectProperty(networkIdentifier.ToString());

            if (_identityRegistry.TryGet(networkIdentifier, out INetworkIdentity networkIdentity))
            {
                if (!networkIdentity.ReadNetworkState(stateInput, senderGuid))
                    return;
            }
            else
            {
                // The prefab name and owner are consumed here because the identity
                // cannot read its own state before the prefab to spawn is known.
                string prefabName = stateInput.ReadString(nameof(NetworkIdentity.PrefabName));
                Guid networkOwner = stateInput.ReadGuid(nameof(NetworkIdentity.NetworkOwner));

                NetworkIdentity spawnedNetworkIdentity = Instantiate(prefabName, channel, networkIdentifier, networkOwner);
                spawnedNetworkIdentity.ReadComponentStates(stateInput);
            }

            stateInput.EndObject();
        }
    }
}
