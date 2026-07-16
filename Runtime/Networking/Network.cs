using System;
using BananaParty.WebSocketRelay.Transport;
using UnityEngine;

namespace BananaParty.WebSocketRelay
{
    public class Network : IRelayListener, IDisposable
    {
        private readonly NetworkContext _networkContext;
        private readonly string _serverAddress;

        private RelayServerProcess _relayServerProcess;
        private RelayClient _relayClient;

        public bool IsConnected => _relayClient != null && _relayClient.IsConnected;
        public bool HasRelayClient => _relayClient != null;

        public Network(string address, NetworkContext context)
        {
            _serverAddress = address;
            _networkContext = context;
        }

        public void StartServer()
        {
            if (_relayServerProcess != null)
                throw new InvalidOperationException("Server already running");

            _relayServerProcess = new RelayServerProcess();
            _relayServerProcess.Start();
        }

        public void StopServer()
        {
            if (_relayServerProcess == null)
                throw new InvalidOperationException("Server not started to stop it");

            _relayServerProcess.Stop();
            _relayServerProcess = null;
            Debug.Log("Relay server stopped.");
        }

        public void Connect(Guid clientGuid)
        {
            if (_relayClient != null)
                throw new InvalidOperationException("Already connected");

            _networkContext.LocalClientIdentity = clientGuid;

            _relayClient = new RelayClient(_serverAddress, this, clientGuid);
            _relayClient.Connect();
            Debug.Log($"Connecting to relay server at {_serverAddress}");
        }

        public void Disconnect()
        {
            if (_relayClient == null)
                throw new InvalidOperationException("Not connected to disconnect");

            _networkContext.ClearNetworkSession();
            _relayClient.Dispose();
            _relayClient = null;
        }

        public void ManualUpdate(float unscaledDeltaTime)
        {
            _relayClient?.ProcessIncomingMessages();
            _networkContext.ManualUpdate(unscaledDeltaTime);
            SendQueuedRpcMessages();
        }

        public void SendSyncIdentities()
        {
            if (!IsConnected)
                return;

            foreach (string channel in _relayClient.SubscribedChannels)
            {
                byte[] payload = _networkContext.GetOwnedNetworkIdentitiesPayload(channel);
                byte[] message = new byte[payload.Length + 1];
                message[0] = NetworkMessage.SyncIdentities;
                payload.CopyTo(message, 1);
                _relayClient.Send(channel, message);
            }
        }

        public void SubscribeToChannel(string channel)
        {
            if (_relayClient == null)
                throw new InvalidOperationException("Not connected to subscribe to a channel");

            _relayClient.SubscribeToChannel(channel);
        }

        public void UnsubscribeFromChannel(string channel)
        {
            if (_relayClient == null)
                throw new InvalidOperationException("Not connected to unsubscribe from a channel");

            _relayClient.UnsubscribeFromChannel(channel);
        }

        public void Dispose()
        {
            _relayServerProcess?.Stop();

            if (_relayClient != null)
                Disconnect();
        }

        public void OnDisconnectedFromRelay()
        {
            Disconnect();
        }

        public void OnChannelMessage(Guid senderGuid, string channel, byte[] data)
        {
            _networkContext.ProcessChannelMessage(senderGuid, channel, data);
        }

        private void SendQueuedRpcMessages()
        {
            if (!IsConnected)
                return;

            while (_networkContext.TryDequeueOutgoingRpcMessage(out string channel, out byte[] message))
                _relayClient.Send(channel, message);
        }
    }
}
