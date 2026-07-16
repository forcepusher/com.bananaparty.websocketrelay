using System;
using System.Collections;
using BananaParty.WebSocketRelay.Transport;

namespace BananaParty.WebSocketRelay.Tests
{
    public static class TestParameters
    {
        public const int RelayServerPort = 23144; // Leet for RELAY

        public const float ConnectTimeoutThreshold = 3f;
        public const float ReceiveTimeoutThreshold = 5f;
        public const float DisconnectTimeoutThreshold = 3f;

        public static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds, Action poll)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                poll?.Invoke();
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;
            }
        }

        public static IEnumerator WaitForDuration(float durationSeconds, Action poll)
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                poll?.Invoke();
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;
            }
        }

        public static IEnumerator WaitUntilRelayConnected(RelayClient relay, float timeoutSeconds = ConnectTimeoutThreshold)
        {
            yield return WaitForCondition(
                () => relay.IsConnected,
                timeoutSeconds,
                () => relay.ProcessIncomingMessages());
        }

        public static IEnumerator WaitUntilRelayConnected(
            RelayClient relayA,
            RelayClient relayB,
            float timeoutSeconds = ConnectTimeoutThreshold)
        {
            yield return WaitForCondition(
                () => relayA.IsConnected && relayB.IsConnected,
                timeoutSeconds,
                () =>
                {
                    relayA.ProcessIncomingMessages();
                    relayB.ProcessIncomingMessages();
                });
        }
    }
}
