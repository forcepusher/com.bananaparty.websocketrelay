using System;
using System.Collections.Generic;
using BananaParty.WebSocketRelay;
using NUnit.Framework;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Tests
{
    public class NetworkAuthorityOwnerTests
    {
        private static readonly Guid PlayerA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid PlayerB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        private static readonly Guid LocalClient = Guid.Parse("00000000-0000-0000-0000-000000000099");

        [Test]
        public void ClaimAuthorityRpc_AppliesNetworkAuthorityOwner()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = LocalClient;

            NetworkIdentity bot = CreateRegisteredBot(context, PlayerB);

            byte[] rpcMessage = NetworkContextTestHelpers.CreateRpcMessage(
                bot.NetworkIdentifier,
                nameof(NetworkIdentity.ClaimAuthority),
                NetworkContextTestHelpers.CreateClaimAuthorityRpcParameters(PlayerA));

            context.ProcessChannelMessage(PlayerA, "room", rpcMessage);

            Assert.AreEqual(PlayerA, bot.NetworkAuthorityOwner);

            UnityEngine.Object.DestroyImmediate(bot.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ChannelStateSync_AppliesNetworkAuthorityOwnerFromPayload()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = LocalClient;
            NetworkIdentity bot = CreateRegisteredBot(context, PlayerB);

            byte[] message = NetworkContextTestHelpers.CreateSyncIdentitiesMessage(
                bot,
                PlayerA);

            context.ProcessChannelMessage(PlayerA, "room", message);

            Assert.AreEqual(PlayerA, bot.NetworkAuthorityOwner);

            UnityEngine.Object.DestroyImmediate(bot.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ChannelStateSync_RecoversMissedRpcViaAuthorityOwnerBroadcast()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = LocalClient;
            NetworkIdentity bot = CreateRegisteredBot(context, PlayerB, out StubNetworkState networkState);

            byte[] message = NetworkContextTestHelpers.CreateSyncIdentitiesMessage(
                bot,
                PlayerA,
                componentValue: 42,
                includeComponentState: true);

            context.ProcessChannelMessage(PlayerA, "room", message);

            Assert.AreEqual(PlayerA, bot.NetworkAuthorityOwner);
            Assert.AreEqual(42, networkState.LastReadValue);

            UnityEngine.Object.DestroyImmediate(bot.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ChannelStateSync_RejectsStaleComponentStateAfterAuthorityOwnerTransfer()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = LocalClient;
            NetworkIdentity bot = CreateRegisteredBot(context, PlayerB, out StubNetworkState networkState);

            byte[] message = NetworkContextTestHelpers.CreateSyncIdentitiesMessage(
                bot,
                PlayerB,
                componentValue: 99,
                includeComponentState: true);

            context.ProcessChannelMessage(PlayerA, "room", message);

            Assert.AreEqual(PlayerB, bot.NetworkAuthorityOwner);
            Assert.AreEqual(0, networkState.LastReadValue);

            UnityEngine.Object.DestroyImmediate(bot.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ChannelStateSync_RejectsInFlightStateFromPreviousOwnerAfterClaim()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = LocalClient;
            NetworkIdentity bot = CreateRegisteredBot(context, PlayerA, out StubNetworkState networkState);
            bot.NetworkAuthorityVersion = 1;

            // PlayerB claims authority with a newer version.
            byte[] rpcMessage = NetworkContextTestHelpers.CreateRpcMessage(
                bot.NetworkIdentifier,
                nameof(NetworkIdentity.ClaimAuthority),
                NetworkContextTestHelpers.CreateClaimAuthorityRpcParameters(PlayerB, claimVersion: 2));
            context.ProcessChannelMessage(PlayerB, "room", rpcMessage);

            // PlayerA's broadcast written before it learned of the claim must not
            // revert ownership or apply its outdated component state.
            byte[] staleMessage = NetworkContextTestHelpers.CreateSyncIdentitiesMessage(
                bot,
                PlayerA,
                componentValue: 99,
                includeComponentState: true,
                networkAuthorityVersion: 1);
            context.ProcessChannelMessage(PlayerA, "room", staleMessage);

            Assert.AreEqual(PlayerB, bot.NetworkAuthorityOwner);
            Assert.AreEqual(2, bot.NetworkAuthorityVersion);
            Assert.AreEqual(0, networkState.LastReadValue);

            UnityEngine.Object.DestroyImmediate(bot.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        [Test]
        public void ConcurrentClaimsWithSameVersion_ConvergeOnSmallerGuid()
        {
            NetworkContext context = NetworkContextTestHelpers.CreateContext();
            context.LocalClientIdentity = LocalClient;
            NetworkIdentity bot = CreateRegisteredBot(context, PlayerB);

            byte[] claimB = NetworkContextTestHelpers.CreateRpcMessage(
                bot.NetworkIdentifier,
                nameof(NetworkIdentity.ClaimAuthority),
                NetworkContextTestHelpers.CreateClaimAuthorityRpcParameters(PlayerB, claimVersion: 1));
            byte[] claimA = NetworkContextTestHelpers.CreateRpcMessage(
                bot.NetworkIdentifier,
                nameof(NetworkIdentity.ClaimAuthority),
                NetworkContextTestHelpers.CreateClaimAuthorityRpcParameters(PlayerA, claimVersion: 1));

            context.ProcessChannelMessage(PlayerB, "room", claimB);
            context.ProcessChannelMessage(PlayerA, "room", claimA);

            Assert.AreEqual(PlayerA, bot.NetworkAuthorityOwner);

            // Same claims in reverse order must converge on the same owner.
            context.ProcessChannelMessage(PlayerA, "room", claimA);
            context.ProcessChannelMessage(PlayerB, "room", claimB);

            Assert.AreEqual(PlayerA, bot.NetworkAuthorityOwner);

            UnityEngine.Object.DestroyImmediate(bot.GameObject);
            UnityEngine.Object.DestroyImmediate(context);
        }

        private static NetworkIdentity CreateRegisteredBot(NetworkContext context, Guid networkAuthorityOwner)
        {
            return CreateRegisteredBot(context, networkAuthorityOwner, withNetworkState: false, out _);
        }

        private static NetworkIdentity CreateRegisteredBot(
            NetworkContext context,
            Guid networkAuthorityOwner,
            out StubNetworkState networkState)
        {
            return CreateRegisteredBot(context, networkAuthorityOwner, withNetworkState: true, out networkState);
        }

        private static NetworkIdentity CreateRegisteredBot(
            NetworkContext context,
            Guid networkAuthorityOwner,
            bool withNetworkState,
            out StubNetworkState networkState)
        {
            GameObject gameObject = new("Bot");
            gameObject.SetActive(false);

            networkState = withNetworkState ? gameObject.AddComponent<StubNetworkState>() : null;

            NetworkIdentity bot = gameObject.AddComponent<NetworkIdentity>();
            NetworkContextTestHelpers.SetPrivateField(bot, "_networkContext", context);
            NetworkContextTestHelpers.SetPrivateField(bot, "_prefabName", "BotCharacter");
            NetworkContextTestHelpers.SetPrivateField(bot, "_distanceBasedAuthority", true);

            bot.NetworkAuthorityOwner = networkAuthorityOwner;
            bot.NetworkIdentifier = Guid.NewGuid();
            bot.Channel = "room";

            gameObject.SetActive(true);
            context.RegisterNetworkIdentity(bot);
            return bot;
        }

        private sealed class StubNetworkState : MonoBehaviour, INetworkState
        {
            public string NetworkStateName => nameof(StubNetworkState);

            public int LastReadValue { get; private set; }

            public void WriteNetworkState(IStateOutput stateOutput) =>
                throw new NotImplementedException();

            public void ReadNetworkState(IStateInput stateInput) =>
                LastReadValue = stateInput.ReadInt("value");
        }
    }
}
