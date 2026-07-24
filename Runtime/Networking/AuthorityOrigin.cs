using System;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class AuthorityOrigin : MonoBehaviour, IAuthorityOrigin
    {
        private const float AuthorityInterceptionThreshold = 0.5f;

        [SerializeField]
        private NetworkContext _networkContext;

        public NetworkIdentity NetworkIdentity { get; private set; }

        public Vector3 Position => transform.position;

        private void Awake()
        {
            NetworkIdentity = GetComponent<NetworkIdentity>();
        }

        private void OnEnable()
        {
            _networkContext.RegisterAuthorityOrigin(this);
        }

        private void OnDisable()
        {
            _networkContext.UnregisterAuthorityOrigin(this);
        }

        private void Update()
        {
            if (NetworkIdentity.NetworkAuthorityOwner != _networkContext.LocalClientIdentity)
                return;

            foreach (INetworkIdentity networkIdentity in _networkContext.NetworkIdentities)
            {
                if (!networkIdentity.DistanceBasedAuthority)
                    continue;

                if (networkIdentity.NetworkAuthorityOwner == _networkContext.LocalClientIdentity)
                    continue;

                AuthorityOrigin currentAuthorityOwnerOrigin = GetAuthorityOriginForNetworkAuthorityOwner(networkIdentity.NetworkAuthorityOwner);
                if (currentAuthorityOwnerOrigin == null)
                {
                    if (GetClosestAuthorityOrigin(networkIdentity.GameObject.transform.position) == this)
                        networkIdentity.ClaimAuthority();

                    continue;
                }

                float currentAuthorityOwnerDistance = Vector3.Distance(networkIdentity.GameObject.transform.position, currentAuthorityOwnerOrigin.Position);
                float localDistance = Vector3.Distance(networkIdentity.GameObject.transform.position, Position);

                if (localDistance > currentAuthorityOwnerDistance * AuthorityInterceptionThreshold)
                    continue;

                networkIdentity.ClaimAuthority();
            }
        }

        private AuthorityOrigin GetAuthorityOriginForNetworkAuthorityOwner(Guid networkAuthorityOwner)
        {
            foreach (IAuthorityOrigin authorityOrigin in _networkContext.AuthorityOrigins)
            {
                if (authorityOrigin.NetworkIdentity.NetworkAuthorityOwner != networkAuthorityOwner)
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
    }
}
