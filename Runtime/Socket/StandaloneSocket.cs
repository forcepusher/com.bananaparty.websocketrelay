using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace BananaParty.WebSocketRelay
{
    public class StandaloneSocket : ISocket
    {
        private const int ReceiveChunkSize = 65536;

        private readonly Uri _serverUri;

        private readonly ClientWebSocket _clientWebSocket = new();
        private readonly CancellationTokenSource _disconnectTokenSource = new();

        private readonly Queue<byte[]> _payloadQueue = new();

        private Task _lastSend = Task.CompletedTask;

        public StandaloneSocket(string serverAddress)
        {
            _serverUri = new Uri(serverAddress);
        }

        public bool IsConnected => _clientWebSocket.State == WebSocketState.Open;

        public bool HasUnreadPayloadQueue => _payloadQueue.Count > 0;

        public byte[] ReadPayloadQueue() => _payloadQueue.Dequeue();

        public void Connect()
        {
            ConnectAndReceiveLoopAsync();
        }

        public void Send(byte[] payloadBytes)
        {
            if (!IsConnected)
                throw new InvalidOperationException($"Connection is not open. State = {_clientWebSocket.State}");

            // Sends are chained because ClientWebSocket forbids concurrent SendAsync calls.
            // The token is captured now because the token source is disposed on disconnect.
            _lastSend = SendAsync(_lastSend, payloadBytes, _disconnectTokenSource.Token);
            ObserveSend(_lastSend);
        }

        public void Disconnect()
        {
            try
            {
                _disconnectTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        private async Task SendAsync(Task previousSend, byte[] payloadBytes, CancellationToken cancellationToken)
        {
            // A failed previous send is observed by its own ObserveSend call.
            await previousSend.ContinueWith(_ => { });
            await _clientWebSocket.SendAsync(
                new ArraySegment<byte>(payloadBytes),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken);
        }

        /// <summary>
        /// Surfaces send failures on the main thread like a fire-and-forget async void would.
        /// Sends interrupted by a disconnect are expected and not reported.
        /// </summary>
        private async void ObserveSend(Task sendTask)
        {
            try
            {
                await sendTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async void ConnectAndReceiveLoopAsync()
        {
            try
            {
                if (await TryConnectAsync())
                    await ReceiveUntilClosedAsync();
            }
            finally
            {
                _disconnectTokenSource.Dispose();
                _clientWebSocket.Dispose();
            }
        }

        private async Task<bool> TryConnectAsync()
        {
            Task connectTask = _clientWebSocket.ConnectAsync(_serverUri, _disconnectTokenSource.Token);

            // Polled instead of awaited so a disconnect request during the handshake
            // does not surface as "Cannot access a disposed object".
            while (!connectTask.IsCompleted)
            {
                await Task.Yield();

                if (_disconnectTokenSource.IsCancellationRequested)
                    return false;
            }

            return connectTask.IsCompletedSuccessfully;
        }

        private async Task ReceiveUntilClosedAsync()
        {
            byte[] chunkBuffer = new byte[ReceiveChunkSize];
            var payloadWriter = new ArrayBufferWriter<byte>();

            while (true)
            {
                Task<WebSocketReceiveResult> receiveTask = _clientWebSocket.ReceiveAsync(chunkBuffer, _disconnectTokenSource.Token);

                // Polled instead of awaited because ReceiveAsync can hang forever when the server is gone.
                while (!receiveTask.IsCompleted)
                {
                    await Task.Yield();

                    if (_clientWebSocket.State == WebSocketState.Aborted)
                        return;
                }

                if (_disconnectTokenSource.IsCancellationRequested)
                    break;

                WebSocketReceiveResult result;
                try
                {
                    result = await receiveTask;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    return;
                }
                catch (IOException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                payloadWriter.Write(new ArraySegment<byte>(chunkBuffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    _payloadQueue.Enqueue(payloadWriter.WrittenSpan.ToArray());
                    payloadWriter = new ArrayBufferWriter<byte>();
                }
            }

            await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
    }
}
