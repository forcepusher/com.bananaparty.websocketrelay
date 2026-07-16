using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BananaParty.WebSocketRelay.Tests
{
    public class NetworkTests
    {
        private static string ServerAddress => $"ws://localhost:{TestParameters.RelayServerPort}";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return RelayServerLauncher.StartCoroutine();
        }

        [Test]
        public void SubscribeWhenNotConnected_ThrowsInvalidOperationException()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            Network network = new Network(ServerAddress, context);

            Assert.Throws<InvalidOperationException>(() => network.SubscribeToChannel("room"));
            Assert.Throws<InvalidOperationException>(() => network.UnsubscribeFromChannel("room"));

            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator ConnectTimeout_DisconnectAllowsReconnect()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            Network network = new Network("ws://127.0.0.1:1", context);

            network.Connect(Guid.NewGuid());

            yield return TestParameters.WaitForDuration(1f, () => network.ManualUpdate(Time.deltaTime));

            Assert.IsFalse(network.IsConnected);
            Assert.IsTrue(network.HasRelayClient);

            network.Disconnect();

            Assert.IsFalse(network.HasRelayClient);

            Network connectedNetwork = new Network(ServerAddress, context);
            connectedNetwork.Connect(Guid.NewGuid());
            yield return TestParameters.WaitForCondition(
                () => connectedNetwork.IsConnected,
                TestParameters.ConnectTimeoutThreshold,
                () => connectedNetwork.ManualUpdate(Time.deltaTime));

            Assert.IsTrue(connectedNetwork.IsConnected);

            connectedNetwork.Disconnect();
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator ServerStop_ManualUpdateClearsSessionAndAllowsReconnect()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            Guid clientGuid = Guid.NewGuid();
            Network network = new Network(ServerAddress, context);

            network.Connect(clientGuid);
            yield return TestParameters.WaitForCondition(
                () => network.IsConnected,
                TestParameters.ConnectTimeoutThreshold,
                () => network.ManualUpdate(Time.deltaTime));
            network.ManualUpdate(Time.deltaTime);

            GameObject localObject = new("LocalOwnedObject");
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                localObject,
                "LocalPrefab",
                clientGuid,
                Guid.NewGuid()));

            yield return RelayServerLauncher.StopCoroutine();

            yield return TestParameters.WaitForCondition(
                () => !network.HasRelayClient,
                TestParameters.DisconnectTimeoutThreshold,
                () => network.ManualUpdate(Time.deltaTime));

            Assert.IsFalse(network.HasRelayClient);
            Assert.AreEqual(Guid.Empty, context.LocalClientIdentity);
            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsTrue(localObject == null);

            yield return RelayServerLauncher.StartCoroutine();

            network.Connect(Guid.NewGuid());
            yield return TestParameters.WaitForCondition(
                () => network.IsConnected,
                TestParameters.ConnectTimeoutThreshold,
                () => network.ManualUpdate(Time.deltaTime));

            Assert.IsTrue(network.IsConnected);

            network.Disconnect();
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator ManualUpdateWhileDisconnected_DetectsDroppedConnection()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            Guid clientGuid = Guid.NewGuid();
            Network network = new Network(ServerAddress, context);

            network.Connect(clientGuid);
            yield return TestParameters.WaitForCondition(
                () => network.IsConnected,
                TestParameters.ConnectTimeoutThreshold,
                () => network.ManualUpdate(Time.deltaTime));
            network.ManualUpdate(Time.deltaTime);
            network.ManualUpdate(Time.deltaTime);

            GameObject localObject = new("LocalOwnedObject");
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                localObject,
                "LocalPrefab",
                clientGuid,
                Guid.NewGuid()));

            yield return RelayServerLauncher.StopCoroutine();

            Assert.IsTrue(network.HasRelayClient);

            yield return TestParameters.WaitForCondition(
                () => !network.HasRelayClient,
                TestParameters.DisconnectTimeoutThreshold,
                () => network.ManualUpdate(Time.deltaTime));

            Assert.IsFalse(network.IsConnected);
            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsTrue(localObject == null);

            UnityEngine.Object.DestroyImmediate(context);
        }
    }
}
