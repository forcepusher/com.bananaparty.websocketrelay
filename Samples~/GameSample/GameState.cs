using System;
using System.Collections;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    public class GameState : MonoBehaviour
    {
        private const float SyncInterval = 0.1f;

        private Network _network;

        private string _networkChannelName = "game-room";

        private float _timeSinceLastFullSync = 0f;

        [SerializeField]
        private NetworkContext _networkContext;

        [SerializeField]
        private NetworkChannel _networkChannel;

        [SerializeField]
        private NetworkIdentity _playerCharacterPrefab;

        private void Start()
        {
            _network = new Network("ws://127.0.0.1:80", _networkContext);

            //var jsonStateOutput = new JsonStateOutput();
            //WriteState(jsonStateOutput);
            //Debug.Log(jsonStateOutput.ToString());
        }

        private void Update()
        {
            if (_network == null)
                return;

            _network.ManualUpdate(Time.unscaledDeltaTime);

            if (!_network.IsConnected)
                return;

            _timeSinceLastFullSync += Time.unscaledDeltaTime;
            if (_timeSinceLastFullSync >= SyncInterval)
            {
                _timeSinceLastFullSync = 0f;
                _network.SendSyncIdentities();
            }
        }

        public void OnStartServerButtonClick()
        {
            _network.StartServer();
        }

        public void OnStopServerButtonClick()
        {
            _network.StopServer();
        }

        public void OnConnectButtonClick()
        {
            StartCoroutine(ConnectCoroutine(5f));
        }

        public void OnDisconnectButtonClick()
        {
            _network.Disconnect();
        }

        private IEnumerator ConnectCoroutine(float connectionTimeout)
        {
            float elapsed = 0;
            _network.Connect(Guid.NewGuid());

            while (!_network.IsConnected)
            {
                _network.ManualUpdate(Time.unscaledDeltaTime);
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > connectionTimeout)
                {
                    Debug.LogError($"Connection timed out after {connectionTimeout}s");
                    if (_network.HasRelayClient)
                        _network.Disconnect();
                    yield break;
                }
                yield return null;
            }

            Debug.Log("Connected to relay");

            _network.SubscribeToChannel(_networkChannelName);

            _networkChannel.SetChannel(_networkChannelName);

            _networkContext.Instantiate(_playerCharacterPrefab, _networkChannelName);
        }
    }
}
