using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using BananaParty.WebSocketRelay.Transport;

#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.Sockets;
#endif

namespace BananaParty.WebSocketRelay.Tests
{
    public static class RelayServerLauncher
    {
        private const int ServerStartupTimeoutMs = 15000;

#if !UNITY_WEBGL || UNITY_EDITOR
        private static RelayServerProcess _serverProcess;
        private static bool _weOwnProcess;
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly object EnsureTaskLock = new();
        private static Task<bool> _ensureRunningTask;
#endif

        public static Task StartAsync() => EnsureRunningAsync();

        public static Task<bool> EnsureRunningAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Task.FromResult(true);
#else
            lock (EnsureTaskLock)
            {
                if (_ensureRunningTask is { IsCompleted: false })
                    return _ensureRunningTask;

                _ensureRunningTask = EnsureRunningInternalAsync();
                return _ensureRunningTask;
            }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static async Task<bool> EnsureRunningInternalAsync()
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_serverProcess != null && _serverProcess.IsRunning)
                    return await WaitForPortAsync(TestParameters.RelayServerPort, ServerStartupTimeoutMs).ConfigureAwait(false);

                RelayServerProcess.KillAll();

                try
                {
                    _serverProcess = new RelayServerProcess();
                    _serverProcess.Start(
                        verboseDebug: true,
                        createNoWindow: true,
                        relayPort: TestParameters.RelayServerPort);
                    _weOwnProcess = true;
                    UnityEngine.Debug.Log($"Started local relay server. Waiting for port {TestParameters.RelayServerPort}...");

                    if (await WaitForPortAsync(TestParameters.RelayServerPort, ServerStartupTimeoutMs).ConfigureAwait(false))
                        return true;

                    UnityEngine.Debug.LogError($"Relay Server failed to open port {TestParameters.RelayServerPort} within timeout.");
                    return false;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to start Relay Server: {e.Message}");
                    return false;
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        private static async Task<bool> IsPortOpenAsync(int port)
        {
            try
            {
                using TcpClient client = new TcpClient();
                Task connectTask = client.ConnectAsync("127.0.0.1", port);
                if (await Task.WhenAny(connectTask, Task.Delay(100)).ConfigureAwait(false) == connectTask && !connectTask.IsFaulted)
                    return client.Connected;
            }
            catch { }

            return false;
        }

        private static async Task<bool> WaitForPortAsync(int port, int timeoutMs)
        {
            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (await IsPortOpenAsync(port).ConfigureAwait(false))
                    return true;

                await Task.Delay(100).ConfigureAwait(false);
            }

            return false;
        }
#endif

        public static IEnumerator StartCoroutine()
        {
            Task<bool> task = EnsureRunningAsync();
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                throw task.Exception;

            if (!task.Result)
                throw new InvalidOperationException("Relay server is not reachable.");
        }

        public static IEnumerator StopCoroutine()
        {
            Task task = StopAsync();
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted)
                throw task.Exception;
        }

        public static Task StopAsync()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Task.CompletedTask;
#else
            return StopInternalAsync();
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private static async Task StopInternalAsync()
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_weOwnProcess || _serverProcess == null || !_serverProcess.IsRunning)
                {
                    _weOwnProcess = false;
                    return;
                }

                try
                {
                    _serverProcess.Stop();
                    UnityEngine.Debug.Log("Stopped local relay server.");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Failed to stop Relay Server: {e.Message}");
                }
                finally
                {
                    _serverProcess = null;
                    _weOwnProcess = false;
                    _ensureRunningTask = null;
                }
            }
            finally
            {
                Gate.Release();
            }
        }
#endif
    }
}
