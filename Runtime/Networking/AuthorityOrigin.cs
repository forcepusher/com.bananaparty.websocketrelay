using System;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class AuthorityOrigin : MonoBehaviour, IAuthorityOrigin, IRpcTarget
    {
        private const float AuthorityInterceptionThreshold = 0.5f;
        private const string TakeAuthorityGuidKey = nameof(TakeAuthorityGuidKey);
        private const string TakeAuthorityRequesterGuidKey = nameof(TakeAuthorityRequesterGuidKey);

        [SerializeField]
        private NetworkContext _networkContext;

        public NetworkIdentity NetworkIdentity { get; private set; }

        INetworkIdentity IRpcTarget.NetworkIdentity => NetworkIdentity;

        public Vector3 Position => transform.position;

        public string RpcSubjectName => nameof(AuthorityOrigin);

        private void Awake()
        {
            NetworkIdentity = GetComponent<NetworkIdentity>();
        }

        private void OnEnable()
        {
            _networkContext.RegisterAuthorityOrigin(this);
            _networkContext.RegisterRpcTarget(this);
        }

        private void OnDisable()
        {
            _networkContext.UnregisterAuthorityOrigin(this);
            _networkContext.UnregisterRpcTarget(this);
        }

        private void Update()
        {
            if (NetworkIdentity.NetworkOwner != _networkContext.LocalClientIdentity)
                return;

            foreach (INetworkIdentity networkIdentity in _networkContext.NetworkIdentities)
            {
                if (!networkIdentity.DistanceBasedAuthority)
                    continue;

                if (networkIdentity.NetworkOwner == _networkContext.LocalClientIdentity)
                    continue;

                AuthorityOrigin currentOwnerAuthorityOrigin = GetAuthorityOriginForNetworkOwner(networkIdentity.NetworkOwner);
                if (currentOwnerAuthorityOrigin == null)
                {
                    if (GetClosestAuthorityOrigin(networkIdentity.GameObject.transform.position) == this)
                        TakeAuthority((NetworkIdentity)networkIdentity);

                    continue;
                }

                float currentOwnerDistance = Vector3.Distance(networkIdentity.GameObject.transform.position, currentOwnerAuthorityOrigin.Position);
                float localDistance = Vector3.Distance(networkIdentity.GameObject.transform.position, Position);

                if (localDistance > currentOwnerDistance * AuthorityInterceptionThreshold)
                    continue;

                TakeAuthority((NetworkIdentity)networkIdentity);
            }
        }

        public void ReceiveRpc(IStateInput parametersStateInput)
        {
            Guid networkIdentityToControl = parametersStateInput.ReadGuid(TakeAuthorityGuidKey);
            Guid requesterGuid = parametersStateInput.ReadGuid(TakeAuthorityRequesterGuidKey);

            foreach (INetworkIdentity networkIdentity in _networkContext.NetworkIdentities)
            {
                if (networkIdentity.NetworkIdentifier != networkIdentityToControl)
                    continue;

                networkIdentity.NetworkOwner = requesterGuid;
                return;
            }
        }

        private AuthorityOrigin GetAuthorityOriginForNetworkOwner(Guid networkOwner)
        {
            foreach (IAuthorityOrigin authorityOrigin in _networkContext.AuthorityOrigins)
            {
                if (authorityOrigin.NetworkIdentity.NetworkOwner != networkOwner)
                    continue;

                return (AuthorityOrigin)authorityOrigin;
            }

            return null;
        }

        private AuthorityOrigin GetClosestAuthorityOrigin(Vector3 targetPosition)
        {
            AuthorityOrigin closestAuthorityOrigin = null;
            float closestDistance = float.MaxValue;

            foreach (IAuthorityOrigin authorityOrigin in _networkContext.AuthorityOrigins)
            {
                float distance = Vector3.Distance(targetPosition, authorityOrigin.Position);
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                closestAuthorityOrigin = (AuthorityOrigin)authorityOrigin;
            }

            return closestAuthorityOrigin;
        }

        private void TakeAuthority(NetworkIdentity networkIdentity)
        {
            IStateOutput parametersStateOutput = _networkContext.UseBinary ? new BinaryStateOutput() : new JsonStateOutput();
            parametersStateOutput.WriteGuid(TakeAuthorityGuidKey, networkIdentity.NetworkIdentifier);
            parametersStateOutput.WriteGuid(TakeAuthorityRequesterGuidKey, _networkContext.LocalClientIdentity);
            NetworkIdentity.SendRpc(RpcSubjectName, parametersStateOutput);
        }
    }
}
