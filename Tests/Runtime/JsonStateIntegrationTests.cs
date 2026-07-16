using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BananaParty.WebSocketRelay;
using BananaParty.WebSocketRelay.Transport;

namespace BananaParty.WebSocketRelay.Tests
{
    public class JsonStateIntegrationTests
    {
        private static string ServerAddress => $"ws://127.0.0.1:{TestParameters.RelayServerPort}";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return RelayServerLauncher.StartCoroutine();
        }

        [UnityTest]
        public IEnumerator FullSerializationDeserializationFlow_OverRelay_Success()
        {
            // Arrange: Create two clients and their respective game states
            GameObject clientAObj = new GameObject("ClientA");
            GameObject clientBObj = new GameObject("ClientB");

            var stateA = clientAObj.AddComponent<MockGameState>();
            var stateB = clientBObj.AddComponent<MockGameState>();

            stateA.PlayTime = 10;
            stateA.Health = 80f;
            stateA.Position = new Vector3(1, 2, 3);
            stateA.NetworkAuthority = true;
            stateB.NetworkAuthority = false;

            TestRelayListener listenerA = new();
            TestRelayListener listenerB = new();
            using RelayClient relayA = new(ServerAddress, listenerA, Guid.NewGuid());
            using RelayClient relayB = new(ServerAddress, listenerB, Guid.NewGuid());

            relayA.Connect();
            relayB.Connect();

            yield return TestParameters.WaitForCondition(
                () => relayA.IsConnected && relayB.IsConnected,
                TestParameters.ConnectTimeoutThreshold,
                () =>
                {
                    relayA.ProcessIncomingMessages();
                    relayB.ProcessIncomingMessages();
                });
            Assert.IsTrue(relayA.IsConnected && relayB.IsConnected, "Relays failed to connect.");

            relayA.SubscribeToChannel("state-sync");
            relayB.SubscribeToChannel("state-sync");
            relayA.ProcessIncomingMessages();
            relayB.ProcessIncomingMessages();
            yield return null;

            // Act: Client A serializes and sends state via channel
            JsonStateOutput writeGraph = new();
            stateA.WriteNetworkState(writeGraph);
            byte[] sentBytes = Encoding.UTF8.GetBytes(writeGraph.ToString());

            bool captured = false;
            listenerB.ChannelMessageReceived += (_, channel, data) =>
            {
                if (channel != "state-sync" || captured)
                    return;

                JsonStateInput readGraph = new(Encoding.UTF8.GetString(data));
                stateB.ReadNetworkState(readGraph);
                captured = true;
            };

            relayA.Send("state-sync", sentBytes);

            yield return TestParameters.WaitForCondition(
                () => captured,
                TestParameters.ReceiveTimeoutThreshold,
                () => relayB.ProcessIncomingMessages());

            Assert.IsTrue(captured, "Channel message was never processed.");

            // Assert: Verify values were synchronized
            Assert.AreEqual(stateA.PlayTime, stateB.PlayTime);
            Assert.AreEqual(stateA.Health, stateB.Health, 0.01f);
            Assert.AreEqual(stateA.Position, stateB.Position);

            UnityEngine.Object.DestroyImmediate(clientAObj);
            UnityEngine.Object.DestroyImmediate(clientBObj);
        }

        private class MockGameState : MonoBehaviour, INetworkIdentity, INetworkState
        {
            public string NetworkStateName => nameof(MockGameState);
            public GameObject GameObject => throw new NotImplementedException();
            public string PrefabName => nameof(MockGameState);
            public string Channel { get; set; }
            public Guid NetworkIdentifier { get; set; } = Guid.NewGuid();
            public Guid NetworkOwner { get; set; } = Guid.NewGuid();
            public bool NetworkAuthority { get; set; }
            public bool DistanceBasedAuthority { get; set; } = false;

            public NetworkContext NetworkContext => throw new NotImplementedException();
            public int PlayTime { get; set; }
            public float Health { get; set; }
            public Vector3 Position { get; set; }

            public void WriteNetworkState(IStateOutput stateOutput)
            {
                stateOutput.WriteInt(nameof(PlayTime), PlayTime);
                stateOutput.WriteFloat(nameof(Health), Health);
                stateOutput.WriteVector3(nameof(Position), Position);
            }

            public void ReadNetworkState(IStateInput stateInput)
            {
                PlayTime = stateInput.ReadInt(nameof(PlayTime));
                Health = stateInput.ReadFloat(nameof(Health));
                Position = stateInput.ReadVector3(nameof(Position));
            }

            public bool ReadNetworkState(IStateInput stateInput, Guid senderGuid)
            {
                ReadNetworkState(stateInput);
                return true;
            }

            public void SendRpc(string rpcSubjectName, IStateOutput parametersStateOutput, bool invokeLocally = true) => throw new NotImplementedException();
        }
    }
}
