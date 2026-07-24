using System;
using System.Collections.Generic;
using BananaParty.WebSocketRelay;
using NUnit.Framework;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Tests
{
    public class JsonStateTests
    {
        [Test]
        public void ShouldWriteAndReadPrimitives()
        {
            var output = new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);
            output.WriteInt("Score", 10);
            output.WriteInt("Level", 5);

            var input = new JsonStateInput(output.ToString());

            Assert.AreEqual(10, input.ReadInt("Score"));
            Assert.AreEqual(5, input.ReadInt("Level"));
        }

        [Test]
        public void ShouldWriteAndReadVector3()
        {
            var output = new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);
            output.WriteVector3("Position", new Vector3(1, 2, 3));

            var input = new JsonStateInput(output.ToString());

            Assert.AreEqual(new Vector3(1, 2, 3), input.ReadVector3("Position"));
        }

        [Test]
        public void ShouldWriteAndReadColor()
        {
            var output = new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);
            output.WriteColor("Tint", new Color(0.25f, 0.5f, 0.75f, 1f));

            var input = new JsonStateInput(output.ToString());

            Assert.AreEqual(new Color(0.25f, 0.5f, 0.75f, 1f), input.ReadColor("Tint"));
        }

        [Test]
        public void ShouldHandlePrettyPrint()
        {
            var output = new JsonStateOutput(prettyPrint: true, bracesOnNewLine: true);
            output.WriteInt("X", 1);

            string json = output.ToString();

            Assert.IsTrue(json.Contains("\n"));
            Assert.IsTrue(json.Contains("\"X\":"));
        }

        [Test]
        public void ShouldPrettyPrintNetworkStateStructure()
        {
            Guid networkId = Guid.Parse("054c4725-f87c-4acd-98dc-81dcb03fd235");
            Guid networkAuthorityOwner = Guid.Parse("b27c471a-b17d-4a89-8285-0dee8e74b771");

            var output = new JsonStateOutput(prettyPrint: true, bracesOnNewLine: true);
            output.BeginObjectElement();
            output.BeginObjectProperty(networkId.ToString());
            output.WriteGuid("NetworkAuthorityOwner", networkAuthorityOwner);
            output.BeginArrayProperty("NetworkStates");
            output.BeginObjectElement();
            output.WriteInt("_health", 100);
            output.WriteVector3("_position", Vector3.zero);
            output.EndObject();
            output.EndArray();
            output.EndObject();
            output.EndObject();

            string json = output.ToString();

            Assert.IsTrue(json.TrimStart().StartsWith("{"));
            Assert.IsTrue(json.TrimEnd().EndsWith("}"));
            Assert.IsTrue(json.Contains($"\"{networkId}\":"));
            Assert.IsTrue(json.Contains("\"NetworkStates\":"));
            Assert.IsTrue(json.Contains("["));
            Assert.IsFalse(json.Contains($"\"{nameof(MockCharacterState)}\":"));
            Assert.IsFalse(json.Contains("\"StateName\""));
            Assert.IsFalse(json.Contains("\"NetworkIdentifier\""));
            Assert.IsTrue(json.Contains("\"_position\":{\"x\":0,\"y\":0,\"z\":0}"));
        }

        [Test]
        public void ShouldRoundTripKeyedObjectPropertyLookup()
        {
            Guid networkId = Guid.Parse("bf0c3839-ff9c-4ef4-9442-482648647d53");
            Guid networkAuthorityOwner = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");

            var output = new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);
            output.BeginObjectElement();
            output.BeginObjectProperty(networkId.ToString());
            output.WriteGuid("NetworkAuthorityOwner", networkAuthorityOwner);
            output.EndObject();
            output.EndObject();

            string json = output.ToString();
            var input = new JsonStateInput(json);
            input.BeginObjectElement();
            input.BeginObjectProperty(networkId.ToString());
            Assert.AreEqual(networkAuthorityOwner, input.ReadGuid("NetworkAuthorityOwner"));
            input.EndObject();
            input.EndObject();
        }

        [Test]
        public void ShouldRoundTripKeyedNetworkStatesLayer()
        {
            Guid networkAuthorityOwner = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");
            Guid networkId = Guid.Parse("bf0c3839-ff9c-4ef4-9442-482648647d53");

            var output = new JsonStateOutput(prettyPrint: true, bracesOnNewLine: true);
            output.BeginObjectElement();
            output.BeginObjectProperty(networkId.ToString());
            output.WriteGuid("NetworkAuthorityOwner", networkAuthorityOwner);
            output.BeginArrayProperty("NetworkStates");
            output.BeginObjectElement();
            output.WriteInt("_health", 5);
            output.WriteVector3("_position", new Vector3(1f, 2f, 3f));
            output.EndObject();
            output.EndArray();
            output.EndObject();
            output.EndObject();

            string json = output.ToString();
            var input = new JsonStateInput(json);
            input.BeginObjectElement();
            input.BeginObjectProperty(networkId.ToString());
            Assert.AreEqual(networkAuthorityOwner, input.ReadGuid("NetworkAuthorityOwner"));
            input.BeginArrayProperty("NetworkStates");
            input.BeginObjectElement();
            Assert.AreEqual(5, input.ReadInt("_health"));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), input.ReadVector3("_position"));
            input.EndObject();
            input.EndArray();
            input.EndObject();
            input.EndObject();
        }

        [Test]
        public void ShouldRoundTripKeyedNetworkStatesOutOfOrder()
        {
            Guid networkId1 = Guid.Parse("bf0c3839-ff9c-4ef4-9442-482648647d53");
            Guid networkAuthorityOwner1 = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");
            Guid networkId2 = Guid.Parse("5640008b-7dd5-4056-a15e-2c18d65e9018");
            Guid networkAuthorityOwner2 = Guid.Parse("dcf6650b-88cb-42d7-8bda-1875e41a75fa");

            var characterState1 = new MockCharacterState { Health = 100, Position = new Vector3(1f, 2f, 3f) };
            var characterState2 = new MockCharacterState { Health = 75, Position = new Vector3(4f, 5f, 6f) };

            var output = new JsonStateOutput(prettyPrint: true, bracesOnNewLine: true);
            output.BeginObjectElement();
            WriteIdentity(output, networkId2, networkAuthorityOwner2, characterState2);
            WriteIdentity(output, networkId1, networkAuthorityOwner1, characterState1);
            output.EndObject();

            characterState1.Health = 0;
            characterState1.Position = Vector3.zero;
            characterState2.Health = 0;
            characterState2.Position = Vector3.zero;

            string json = output.ToString();

            var inputForIdentity2 = new JsonStateInput(json);
            inputForIdentity2.BeginObjectElement();
            MockCharacterState readCharacterState2 = ReadIdentity(inputForIdentity2, networkId2, out Guid readNetworkAuthorityOwner2);

            var inputForIdentity1 = new JsonStateInput(json);
            inputForIdentity1.BeginObjectElement();
            MockCharacterState readCharacterState1 = ReadIdentity(inputForIdentity1, networkId1, out Guid readNetworkAuthorityOwner1);

            Assert.AreEqual(networkAuthorityOwner1, readNetworkAuthorityOwner1);
            Assert.AreEqual(100, readCharacterState1.Health);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), readCharacterState1.Position);

            Assert.AreEqual(networkAuthorityOwner2, readNetworkAuthorityOwner2);
            Assert.AreEqual(75, readCharacterState2.Health);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), readCharacterState2.Position);
        }

        [Test]
        public void ShouldRoundTripPrettyPrintedNetworkStates()
        {
            Guid networkId1 = Guid.Parse("bf0c3839-ff9c-4ef4-9442-482648647d53");
            Guid networkAuthorityOwner1 = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");
            Guid networkId2 = Guid.Parse("5640008b-7dd5-4056-a15e-2c18d65e9018");
            Guid networkAuthorityOwner2 = Guid.Parse("dcf6650b-88cb-42d7-8bda-1875e41a75fa");

            var characterState1 = new MockCharacterState { Health = 100, Position = new Vector3(1f, 2f, 3f) };
            var characterState2 = new MockCharacterState { Health = 75, Position = new Vector3(4f, 5f, 6f) };

            var output = new JsonStateOutput(prettyPrint: true, bracesOnNewLine: true);
            WriteNetworkSnapshot(
                output,
                (networkId1, networkAuthorityOwner1, characterState1),
                (networkId2, networkAuthorityOwner2, characterState2));

            characterState1.Health = 0;
            characterState1.Position = Vector3.zero;
            characterState2.Health = 0;
            characterState2.Position = Vector3.zero;

            string json = output.ToString();

            var inputForIdentity1 = new JsonStateInput(json);
            inputForIdentity1.BeginObjectElement();
            MockCharacterState readCharacterState1 = ReadIdentity(inputForIdentity1, networkId1, out Guid readNetworkAuthorityOwner1);

            var inputForIdentity2 = new JsonStateInput(json);
            inputForIdentity2.BeginObjectElement();
            MockCharacterState readCharacterState2 = ReadIdentity(inputForIdentity2, networkId2, out Guid readNetworkAuthorityOwner2);

            Assert.AreEqual(networkAuthorityOwner1, readNetworkAuthorityOwner1);
            Assert.AreEqual(100, readCharacterState1.Health);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), readCharacterState1.Position);

            Assert.AreEqual(networkAuthorityOwner2, readNetworkAuthorityOwner2);
            Assert.AreEqual(75, readCharacterState2.Health);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), readCharacterState2.Position);
        }

        private static void WriteNetworkSnapshot(
            IStateOutput stateOutput,
            (Guid NetworkIdentifier, Guid NetworkAuthorityOwner, MockCharacterState CharacterState) identity1,
            (Guid NetworkIdentifier, Guid NetworkAuthorityOwner, MockCharacterState CharacterState) identity2)
        {
            stateOutput.BeginObjectElement();
            WriteIdentity(stateOutput, identity1.NetworkIdentifier, identity1.NetworkAuthorityOwner, identity1.CharacterState);
            WriteIdentity(stateOutput, identity2.NetworkIdentifier, identity2.NetworkAuthorityOwner, identity2.CharacterState);
            stateOutput.EndObject();
        }

        private static void WriteIdentity(
            IStateOutput stateOutput,
            Guid networkIdentifier,
            Guid networkAuthorityOwner,
            MockCharacterState characterState)
        {
            stateOutput.BeginObjectProperty(networkIdentifier.ToString());
            stateOutput.WriteGuid("NetworkAuthorityOwner", networkAuthorityOwner);
            stateOutput.BeginArrayProperty("NetworkStates");
            stateOutput.BeginObjectElement();
            characterState.WriteNetworkState(stateOutput);
            stateOutput.EndObject();
            stateOutput.EndArray();
            stateOutput.EndObject();
        }

        private static MockCharacterState ReadIdentity(
            IStateInput stateInput,
            Guid networkIdentifier,
            out Guid networkAuthorityOwner)
        {
            stateInput.BeginObjectProperty(networkIdentifier.ToString());
            networkAuthorityOwner = stateInput.ReadGuid("NetworkAuthorityOwner");
            stateInput.BeginArrayProperty("NetworkStates");
            stateInput.BeginObjectElement();
            var characterState = new MockCharacterState();
            characterState.ReadNetworkState(stateInput);
            stateInput.EndObject();
            stateInput.EndArray();
            stateInput.EndObject();
            return characterState;
        }

        private sealed class MockCharacterState : INetworkState
        {
            public string NetworkStateName => nameof(MockCharacterState);
            public int Health { get; set; }
            public Vector3 Position { get; set; }

            public void WriteNetworkState(IStateOutput stateOutput)
            {
                stateOutput.WriteInt("_health", Health);
                stateOutput.WriteVector3("_position", Position);
            }

            public void ReadNetworkState(IStateInput stateInput)
            {
                Health = stateInput.ReadInt("_health");
                Position = stateInput.ReadVector3("_position");
            }
        }

        [Test]
        public void ShouldApplyMultipleIdentitiesWithFreshParserPerIdentity()
        {
            Guid botId = Guid.Parse("8e18e9b4-619b-43ed-976b-18765d6465da");
            Guid playerId = Guid.Parse("368ab72d-dfe8-4bf2-8f26-538f5f50ae24");
            Guid botAuthorityOwner = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");
            Guid playerAuthorityOwner = Guid.Parse("dcf6650b-88cb-42d7-8bda-1875e41a75fa");

            var output = new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);
            output.BeginObjectElement();
            WriteNetworkIdentity(output, botId, botAuthorityOwner, "BotCharacter", 100, Vector3.zero);
            WriteNetworkIdentity(output, playerId, playerAuthorityOwner, "PlayerCharacter", 75, new Vector3(1f, 2f, 3f));
            output.EndObject();

            string json = output.ToString();
            Guid[] identityIds = { botId, playerId };

            foreach (Guid networkIdentifier in identityIds)
            {
                JsonStateInput stateInput = new(json);
                stateInput.BeginObjectElement();
                stateInput.BeginObjectProperty(networkIdentifier.ToString());
                string prefabName = stateInput.ReadString("PrefabName");
                stateInput.ReadGuid("NetworkAuthorityOwner");
                stateInput.BeginArrayProperty("NetworkStates");
                stateInput.BeginObjectElement();
                stateInput.ReadInt("_health");
                stateInput.ReadVector3("_position");
                stateInput.EndObject();
                stateInput.EndArray();
                stateInput.EndObject();

                if (networkIdentifier == botId)
                    Assert.AreEqual("BotCharacter", prefabName);
                else
                    Assert.AreEqual("PlayerCharacter", prefabName);
            }
        }

        [Test]
        public void ShouldApplyMultipleIdentitiesSequentiallyOnSameJsonStateInput()
        {
            Guid botId = Guid.Parse("8e18e9b4-619b-43ed-976b-18765d6465da");
            Guid playerId = Guid.Parse("368ab72d-dfe8-4bf2-8f26-538f5f50ae24");
            Guid botAuthorityOwner = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");
            Guid playerAuthorityOwner = Guid.Parse("dcf6650b-88cb-42d7-8bda-1875e41a75fa");

            var output = new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);
            output.BeginObjectElement();
            WriteNetworkIdentity(output, botId, botAuthorityOwner, "BotCharacter", 100, Vector3.zero);
            WriteNetworkIdentity(output, playerId, playerAuthorityOwner, "PlayerCharacter", 75, new Vector3(1f, 2f, 3f));
            output.EndObject();

            string json = output.ToString();
            Guid[] identityIds = { botId, playerId };

            JsonStateInput stateInput = new(json);
            stateInput.BeginObjectElement();

            foreach (Guid networkIdentifier in identityIds)
            {
                stateInput.BeginObjectProperty(networkIdentifier.ToString());
                string prefabName = stateInput.ReadString("PrefabName");
                stateInput.ReadGuid("NetworkAuthorityOwner");
                stateInput.BeginArrayProperty("NetworkStates");
                stateInput.BeginObjectElement();
                stateInput.ReadInt("_health");
                stateInput.ReadVector3("_position");
                stateInput.EndObject();
                stateInput.EndArray();
                stateInput.EndObject();

                if (networkIdentifier == botId)
                    Assert.AreEqual("BotCharacter", prefabName);
                else
                    Assert.AreEqual("PlayerCharacter", prefabName);
            }

            stateInput.EndObject();
        }

        private static void WriteNetworkIdentity(
            IStateOutput stateOutput,
            Guid networkIdentifier,
            Guid networkAuthorityOwner,
            string prefabName,
            int health,
            Vector3 position)
        {
            stateOutput.BeginObjectProperty(networkIdentifier.ToString());
            stateOutput.WriteString("PrefabName", prefabName);
            stateOutput.WriteGuid("NetworkAuthorityOwner", networkAuthorityOwner);
            stateOutput.BeginArrayProperty("NetworkStates");
            stateOutput.BeginObjectElement();
            stateOutput.WriteInt("_health", health);
            stateOutput.WriteVector3("_position", position);
            stateOutput.EndObject();
            stateOutput.EndArray();
            stateOutput.EndObject();
        }

        [Test]
        public void ShouldRoundTripBinaryNetworkStatesWithGuidLookup()
        {
            Guid networkId1 = Guid.Parse("bf0c3839-ff9c-4ef4-9442-482648647d53");
            Guid networkAuthorityOwner1 = Guid.Parse("bea8ee69-bdcf-4eda-8755-bf4c4a886c29");
            Guid networkId2 = Guid.Parse("5640008b-7dd5-4056-a15e-2c18d65e9018");
            Guid networkAuthorityOwner2 = Guid.Parse("dcf6650b-88cb-42d7-8bda-1875e41a75fa");
            Guid unknownId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            var characterState1 = new MockCharacterState { Health = 100, Position = new Vector3(1f, 2f, 3f) };
            var characterState2 = new MockCharacterState { Health = 75, Position = new Vector3(4f, 5f, 6f) };

            using var output = new BinaryStateOutput();
            output.BeginObjectElement();
            WriteIdentity(output, networkId2, networkAuthorityOwner2, characterState2);
            WriteIdentity(output, networkId1, networkAuthorityOwner1, characterState1);
            output.EndObject();

            var input = new BinaryStateInput(output.GetBuffer());
            input.BeginObjectElement();
            Assert.Throws<KeyNotFoundException>(() => input.BeginObjectProperty(unknownId.ToString()));
            MockCharacterState readCharacterState1 = ReadIdentity(input, networkId1, out Guid readNetworkAuthorityOwner1);
            Assert.AreEqual(networkAuthorityOwner1, readNetworkAuthorityOwner1);
            Assert.AreEqual(100, readCharacterState1.Health);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), readCharacterState1.Position);
            input.EndObject();
        }

        [Test]
        public void ShouldRoundTripBinary()
        {
            using var output = new BinaryStateOutput();
            output.WriteInt("Score", 10);
            output.WriteVector3("Position", new Vector3(1, 2, 3));
            output.WriteColor("Tint", new Color(0.25f, 0.5f, 0.75f, 1f));

            var input = new BinaryStateInput(output.GetBuffer());

            Assert.AreEqual(10, input.ReadInt("Score"));
            Assert.AreEqual(new Vector3(1, 2, 3), input.ReadVector3("Position"));
            Assert.AreEqual(new Color(0.25f, 0.5f, 0.75f, 1f), input.ReadColor("Tint"));
        }
    }
}
