using System;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class NetworkBinding : MonoBehaviour
    {
        [SerializeField]
        private NetworkChannel _networkChannel;
        [SerializeField]
        private string _guid;

        private NetworkIdentity _networkIdentity;

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _networkIdentity.NetworkIdentifier = Guid.Parse(_guid);
        }

        private void OnEnable()
        {
            _networkChannel.AddBinding(this);
            _networkIdentity.NetworkContext.RegisterNetworkIdentity(_networkIdentity);
        }

        private void OnDisable()
        {
            _networkChannel.RemoveBinding(this);
            _networkIdentity.NetworkContext.UnregisterNetworkIdentity(_networkIdentity);
        }

        public void SetBinding(string channel)
        {
            _networkIdentity.Channel = channel;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_guid))
            {
                _guid = Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
