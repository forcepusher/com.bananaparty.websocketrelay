using System;
using System.Collections;
using BananaParty.WebSocketRelay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BananaParty.WebSocketRelay.Tests
{
    public class NetworkRpcTests
    {
        [Test]
        public void SendRpc_DispatchesLocallyToMatchingIdentityAndSubject()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);
            StubRpcTarget rpcTarget = new(networkIdentity, "TestSubject");
            context.RegisterRpcTarget(rpcTarget);

            context.SendRpc(
                networkIdentity.NetworkIdentifier,
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParameters(42),
                "room");

            Assert.AreEqual(1, rpcTarget.ReceiveCount);
            Assert.AreEqual(42, rpcTarget.LastReceivedValue);

            UnityEngine.Object.DestroyImmediate(networkIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void SendRpc_DoesNotDispatchToDifferentIdentity()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            StubNetworkIdentity targetIdentity = CreateRegisteredIdentity(context);
            StubNetworkIdentity otherIdentity = CreateRegisteredIdentity(context);
            StubRpcTarget rpcTarget = new(targetIdentity, "TestSubject");
            context.RegisterRpcTarget(rpcTarget);

            context.SendRpc(
                otherIdentity.NetworkIdentifier,
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParameters(7),
                "room");

            Assert.AreEqual(0, rpcTarget.ReceiveCount);

            UnityEngine.Object.DestroyImmediate(targetIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(otherIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void SendRpc_FiltersBySubjectName()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);
            StubRpcTarget matchingTarget = new(networkIdentity, "MatchingSubject");
            StubRpcTarget otherTarget = new(networkIdentity, "OtherSubject");
            context.RegisterRpcTarget(matchingTarget);
            context.RegisterRpcTarget(otherTarget);

            context.SendRpc(
                networkIdentity.NetworkIdentifier,
                "MatchingSubject",
                NetworkContextTestHelpers.CreateRpcParameters(11),
                "room");

            Assert.AreEqual(1, matchingTarget.ReceiveCount);
            Assert.AreEqual(0, otherTarget.ReceiveCount);

            UnityEngine.Object.DestroyImmediate(networkIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void SendRpc_QueuesOutgoingMessageWithChannelAndRpcHeader()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);

            context.SendRpc(
                networkIdentity.NetworkIdentifier,
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParameters(3),
                "sync-channel");

            Assert.IsTrue(context.TryDequeueOutgoingRpcMessage(out string channel, out byte[] message));
            Assert.AreEqual("sync-channel", channel);
            Assert.AreEqual(NetworkMessage.Rpc, message[0]);
            Assert.IsFalse(context.TryDequeueOutgoingRpcMessage(out _, out _));

            UnityEngine.Object.DestroyImmediate(networkIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void SendRpc_WithInvokeLocallyFalse_DoesNotDispatchLocally()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);
            StubRpcTarget rpcTarget = new(networkIdentity, "TestSubject");
            context.RegisterRpcTarget(rpcTarget);

            context.SendRpc(
                networkIdentity.NetworkIdentifier,
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParameters(42),
                "room",
                invokeLocally: false);

            Assert.AreEqual(0, rpcTarget.ReceiveCount);
            Assert.IsTrue(context.TryDequeueOutgoingRpcMessage(out _, out _));

            UnityEngine.Object.DestroyImmediate(networkIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ProcessChannelMessage_DispatchesIncomingRpcToMatchingIdentity()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = Guid.NewGuid();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);
            StubRpcTarget rpcTarget = new(networkIdentity, "TestSubject");
            context.RegisterRpcTarget(rpcTarget);

            byte[] rpcMessage = NetworkContextTestHelpers.CreateRpcMessage(
                networkIdentity.NetworkIdentifier,
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParametersPayload(99));

            context.ProcessChannelMessage(Guid.NewGuid(), "room", rpcMessage);

            Assert.AreEqual(1, rpcTarget.ReceiveCount);
            Assert.AreEqual(99, rpcTarget.LastReceivedValue);

            UnityEngine.Object.DestroyImmediate(networkIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ProcessChannelMessage_IncomingRpcIgnoresWrongIdentity()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = Guid.NewGuid();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);
            StubRpcTarget rpcTarget = new(networkIdentity, "TestSubject");
            context.RegisterRpcTarget(rpcTarget);

            byte[] rpcMessage = NetworkContextTestHelpers.CreateRpcMessage(
                Guid.NewGuid(),
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParametersPayload(5));

            context.ProcessChannelMessage(Guid.NewGuid(), "room", rpcMessage);

            Assert.AreEqual(0, rpcTarget.ReceiveCount);

            UnityEngine.Object.DestroyImmediate(networkIdentity.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator NetworkIdentity_SendRpc_DispatchesThroughContext()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            GameObject gameObject = new("RpcSender");
            NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
            NetworkContextTestHelpers.SetPrivateField(networkIdentity, "_networkContext", context);
            networkIdentity.NetworkIdentifier = Guid.NewGuid();
            networkIdentity.Channel = "room";
            context.RegisterNetworkIdentity(networkIdentity);

            StubRpcTarget rpcTarget = new(networkIdentity, "ComponentSubject");
            context.RegisterRpcTarget(rpcTarget);

            networkIdentity.SendRpc("ComponentSubject", NetworkContextTestHelpers.CreateRpcParameters(21));
            yield return null;

            Assert.AreEqual(1, rpcTarget.ReceiveCount);
            Assert.AreEqual(21, rpcTarget.LastReceivedValue);

            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator ClearNetworkSession_ClearsOutgoingRpcQueue()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            StubNetworkIdentity networkIdentity = CreateRegisteredIdentity(context);

            context.SendRpc(
                networkIdentity.NetworkIdentifier,
                "TestSubject",
                NetworkContextTestHelpers.CreateRpcParameters(1),
                "room");

            context.ClearNetworkSession();
            yield return null;

            Assert.IsFalse(context.TryDequeueOutgoingRpcMessage(out _, out _));

            UnityEngine.Object.DestroyImmediate(context);
        }

        private static StubNetworkIdentity CreateRegisteredIdentity(NetworkContext context)
        {
            GameObject gameObject = new("RpcIdentity");
            Guid networkIdentifier = Guid.NewGuid();
            StubNetworkIdentity networkIdentity = new(
                gameObject,
                "RpcPrefab",
                Guid.NewGuid(),
                networkIdentifier);
            context.RegisterNetworkIdentity(networkIdentity);
            return networkIdentity;
        }
    }
}
