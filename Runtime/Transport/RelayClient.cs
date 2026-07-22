using System;
using System.Collections.Generic;
using System.IO;

namespace BananaParty.WebSocketRelay.Transport
{
    public class RelayClient : IDisposable
    {
        private readonly ISocket _socket;
        private readonly IRelayListener _relayListener;

        private bool _wasConnected;

        public Guid ClientGuid { get; }

        public HashSet<string> SubscribedChannels { get; } = new();

        public bool IsConnected => _socket.IsConnected;

        public RelayClient(string serverAddress, IRelayListener relayListener, Guid clientGuid, bool offlineMode = false)
        {
            ClientGuid = clientGuid;
            _socket = offlineMode ? new OfflineSocket() : new Socket(serverAddress);
            _relayListener = relayListener;
        }

        public void Connect()
        {
            _socket.Connect();
        }

        /// <summary>
        /// Drains queued WebSocket frames, dispatches channel messages, and reports disconnection.
        /// Call this periodically (e.g. in Update).
        /// </summary>
        public void ProcessIncomingMessages()
        {
            while (_socket.HasUnreadPayloadQueue)
                ProcessPayload(_socket.ReadPayloadQueue());

            NotifyIfDisconnected();
        }

        public void SubscribeToChannel(string channel)
        {
            if (!SubscribedChannels.Add(channel))
                return;

            _socket.Send(RelayMessageCodec.CreateProtocolMessage(RelayMessageType.Subscribe, channel));
        }

        public void UnsubscribeFromChannel(string channel)
        {
            if (!SubscribedChannels.Remove(channel))
                throw new KeyNotFoundException($"Not subscribed to channel '{channel}'.");

            _socket.Send(RelayMessageCodec.CreateProtocolMessage(RelayMessageType.Unsubscribe, channel));
        }

        public void Send(string channel, byte[] data)
        {
            if (!SubscribedChannels.Contains(channel))
                throw new KeyNotFoundException($"Not subscribed to channel '{channel}'.");

            _socket.Send(RelayMessageCodec.CreateChannelMessage(ClientGuid, channel, data));
        }

        public void Dispose()
        {
            _socket.Dispose();
        }

        private void NotifyIfDisconnected()
        {
            if (_socket.IsConnected)
            {
                _wasConnected = true;
                return;
            }

            if (!_wasConnected)
                return;

            _wasConnected = false;
            _relayListener.OnDisconnectedFromRelay();
        }

        internal void ProcessPayload(byte[] payloadBytes)
        {
            if (payloadBytes.Length == 0 || payloadBytes[0] != RelayMessageType.ChannelMessage)
                return;

            int channelLength = RelayMessageCodec.ReadChannelLength(payloadBytes, RelayMessageCodec.ChannelMessageChannelLengthOffset);
            if (channelLength < 0)
                throw new InvalidDataException("Incomplete channel message.");

            int payloadOffset = RelayMessageCodec.GetChannelMessagePayloadOffset(channelLength);
            if (payloadBytes.Length < payloadOffset)
                throw new InvalidDataException("Incomplete channel message.");

            string channel = RelayMessageCodec.ReadChannel(payloadBytes, RelayMessageCodec.ChannelMessageChannelLengthOffset);
            if (!SubscribedChannels.Contains(channel))
                return;

            Guid senderGuid = RelayMessageCodec.ReadGuid(payloadBytes, RelayMessageCodec.ChannelMessageGuidOffset);
            byte[] messageData = new byte[payloadBytes.Length - payloadOffset];
            Array.Copy(payloadBytes, payloadOffset, messageData, 0, messageData.Length);
            _relayListener.OnChannelMessage(senderGuid, channel, messageData);
        }
    }
}
