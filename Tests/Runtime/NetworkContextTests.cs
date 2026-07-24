using System;
using System.Collections;
using BananaParty.WebSocketRelay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BananaParty.WebSocketRelay.Tests
{
    public class NetworkContextTests
    {
        [Test]
        public void ProcessChannelMessage_IgnoresLocalSender()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = Guid.NewGuid();

            context.ProcessChannelMessage(context.LocalClientIdentity, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ProcessChannelMessage_TracksRemotePlayer()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = Guid.NewGuid();
            Guid remotePlayer = Guid.NewGuid();

            context.ProcessChannelMessage(remotePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            Assert.AreEqual(1, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator TimedOutPlayer_RemovesOwnedIdentities()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext(playerTimeoutSeconds: 1f);
            context.LocalClientIdentity = Guid.NewGuid();
            Guid remotePlayer = Guid.NewGuid();

            context.ProcessChannelMessage(remotePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            GameObject remoteObject = new("RemoteOwnedObject");
            StubNetworkIdentity remoteIdentity = new(
                remoteObject,
                "RemotePrefab",
                remotePlayer,
                Guid.NewGuid());
            context.RegisterNetworkIdentity(remoteIdentity);

            context.ManualUpdate(1.1f);
            yield return null;

            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsTrue(remoteObject == null);

            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator TimedOutPlayer_KeepsIdentitiesWhenDestroyWhenAuthorityOwnerLeavesIsFalse()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext(playerTimeoutSeconds: 1f);
            context.LocalClientIdentity = Guid.NewGuid();
            Guid remotePlayer = Guid.NewGuid();

            context.ProcessChannelMessage(remotePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            GameObject remoteObject = new("PersistentOwnedObject");
            StubNetworkIdentity remoteIdentity = new(
                remoteObject,
                "RemotePrefab",
                remotePlayer,
                Guid.NewGuid())
            {
                DestroyWhenAuthorityOwnerLeaves = false
            };
            context.RegisterNetworkIdentity(remoteIdentity);

            context.ManualUpdate(1.1f);
            yield return null;

            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            Assert.AreEqual(1, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsFalse(remoteObject == null);

            UnityEngine.Object.DestroyImmediate(remoteObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator TimedOutPlayer_DoesNotRemoveOtherPlayersIdentities()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext(playerTimeoutSeconds: 2f);
            context.LocalClientIdentity = Guid.NewGuid();
            Guid timingOutPlayer = Guid.NewGuid();
            Guid activePlayer = Guid.NewGuid();

            context.ProcessChannelMessage(timingOutPlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());
            context.ProcessChannelMessage(activePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            GameObject timingOutObject = new("TimingOutObject");
            GameObject activeObject = new("ActiveObject");
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                timingOutObject,
                "TimingOutPrefab",
                timingOutPlayer,
                Guid.NewGuid()));
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                activeObject,
                "ActivePrefab",
                activePlayer,
                Guid.NewGuid()));

            context.ManualUpdate(1.1f);
            context.ProcessChannelMessage(activePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());
            context.ManualUpdate(1.1f);
            yield return null;

            Assert.AreEqual(1, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            Assert.AreEqual(1, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsTrue(timingOutObject == null);
            Assert.IsFalse(activeObject == null);

            UnityEngine.Object.DestroyImmediate(activeObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator ChannelMessage_ResetsPlayerTimeout()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext(playerTimeoutSeconds: 2f);
            context.LocalClientIdentity = Guid.NewGuid();
            Guid remotePlayer = Guid.NewGuid();

            context.ProcessChannelMessage(remotePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            GameObject remoteObject = new("RemoteOwnedObject");
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                remoteObject,
                "RemotePrefab",
                remotePlayer,
                Guid.NewGuid()));

            context.ManualUpdate(1.5f);
            context.ProcessChannelMessage(remotePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());
            context.ManualUpdate(1.5f);
            yield return null;

            Assert.AreEqual(1, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            Assert.AreEqual(1, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsFalse(remoteObject == null);

            UnityEngine.Object.DestroyImmediate(remoteObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator ClearNetworkSession_RemovesAllIdentitiesAndPlayers()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            Guid localPlayer = Guid.NewGuid();
            Guid remotePlayer = Guid.NewGuid();
            context.LocalClientIdentity = localPlayer;

            context.ProcessChannelMessage(remotePlayer, "room", NetworkContextTestHelpers.CreateEmptySyncIdentitiesMessage());

            GameObject localObject = new("LocalOwnedObject");
            GameObject remoteObject = new("RemoteOwnedObject");
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                localObject,
                "LocalPrefab",
                localPlayer,
                Guid.NewGuid()));
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                remoteObject,
                "RemotePrefab",
                remotePlayer,
                Guid.NewGuid()));

            context.ClearNetworkSession();
            yield return null;

            Assert.AreEqual(Guid.Empty, context.LocalClientIdentity);
            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkPlayerCount(context));
            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsTrue(localObject == null);
            Assert.IsTrue(remoteObject == null);

            UnityEngine.Object.DestroyImmediate(context);
        }

        [UnityTest]
        public IEnumerator OnDisconnectedFromRelay_ClearsNetworkSession()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = Guid.NewGuid();

            GameObject localObject = new("LocalOwnedObject");
            context.RegisterNetworkIdentity(new StubNetworkIdentity(
                localObject,
                "LocalPrefab",
                context.LocalClientIdentity,
                Guid.NewGuid()));

            Network network = new Network("ws://127.0.0.1:1", context);
            network.Connect(context.LocalClientIdentity);
            network.OnDisconnectedFromRelay();
            yield return null;

            Assert.AreEqual(Guid.Empty, context.LocalClientIdentity);
            Assert.AreEqual(0, NetworkContextTestHelpers.GetNetworkIdentityCount(context));
            Assert.IsTrue(localObject == null);

            UnityEngine.Object.DestroyImmediate(context);
        }
    }
}
