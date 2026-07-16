using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BananaParty.WebSocketRelay;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Tests
{
    internal static class NetworkContextTestHelpers
    {
        public static NetworkContext CreateContext(float playerTimeoutSeconds = 10f)
        {
            NetworkContext context = ScriptableObject.CreateInstance<NetworkContext>();
            SetPlayerTimeoutSeconds(context, playerTimeoutSeconds);
            return context;
        }

        public static void SetPlayerTimeoutSeconds(NetworkContext context, float playerTimeoutSeconds)
        {
            FieldInfo field = typeof(NetworkContext).GetField(
                "_playerTimeoutSeconds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(context, playerTimeoutSeconds);
        }

        public static int GetNetworkPlayerCount(NetworkContext context)
        {
            return context.NetworkPlayers.Count;
        }

        public static int GetNetworkIdentityCount(NetworkContext context)
        {
            return context.NetworkIdentities.Count;
        }

        public static int GetAuthorityOriginCount(NetworkContext context)
        {
            return context.AuthorityOrigins.Count;
        }

        public static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        public static byte[] CreateRpcMessage(Guid networkIdentifier, string rpcSubjectName, byte[] parametersPayload)
        {
            byte[] subjectNameBytes = Encoding.UTF8.GetBytes(rpcSubjectName);
            byte[] message = new byte[19 + subjectNameBytes.Length + parametersPayload.Length];
            message[0] = NetworkMessage.Rpc;
            message[1] = (byte)subjectNameBytes.Length;
            message[2] = (byte)(subjectNameBytes.Length >> 8);
            Buffer.BlockCopy(subjectNameBytes, 0, message, 3, subjectNameBytes.Length);
            Buffer.BlockCopy(networkIdentifier.ToByteArray(), 0, message, 3 + subjectNameBytes.Length, 16);
            Buffer.BlockCopy(parametersPayload, 0, message, 19 + subjectNameBytes.Length, parametersPayload.Length);
            return message;
        }

        public static byte[] CreateRpcParametersPayload(int value)
        {
            JsonStateOutput output = new(prettyPrint: false, bracesOnNewLine: false);
            output.WriteInt("value", value);
            return Encoding.UTF8.GetBytes(output.ToString());
        }

        public static JsonStateOutput CreateRpcParameters(int value)
        {
            JsonStateOutput output = new(prettyPrint: false, bracesOnNewLine: false);
            output.WriteInt("value", value);
            return output;
        }

        public static byte[] CreateTakeAuthorityRpcParameters(Guid networkIdentityId, Guid requesterGuid)
        {
            JsonStateOutput output = new(prettyPrint: false, bracesOnNewLine: false);
            output.WriteGuid("TakeAuthorityGuidKey", networkIdentityId);
            output.WriteGuid("TakeAuthorityRequesterGuidKey", requesterGuid);
            return Encoding.UTF8.GetBytes(output.ToString());
        }

        public static byte[] CreateEmptySyncIdentitiesMessage()
        {
            byte[] payload = Encoding.UTF8.GetBytes("{}");
            byte[] message = new byte[payload.Length + 1];
            message[0] = NetworkMessage.SyncIdentities;
            payload.CopyTo(message, 1);
            return message;
        }

        public static byte[] CreateSyncIdentitiesMessage(
            INetworkIdentity identity,
            Guid networkOwner,
            int componentValue = 0,
            bool includeComponentState = false)
        {
            JsonStateOutput output = new(prettyPrint: false, bracesOnNewLine: false);
            output.BeginObjectElement();
            output.BeginObjectProperty(identity.NetworkIdentifier.ToString());
            output.WriteString(nameof(NetworkIdentity.PrefabName), identity.PrefabName);
            output.WriteGuid(nameof(NetworkIdentity.NetworkOwner), networkOwner);
            output.BeginArrayProperty("NetworkStates");
            if (includeComponentState)
            {
                output.BeginObjectElement();
                output.WriteInt("value", componentValue);
                output.EndObject();
            }
            output.EndArray();
            output.EndObject();
            output.EndObject();

            byte[] payload = Encoding.UTF8.GetBytes(output.ToString());
            byte[] message = new byte[payload.Length + 1];
            message[0] = NetworkMessage.SyncIdentities;
            Buffer.BlockCopy(payload, 0, message, 1, payload.Length);
            return message;
        }

        public static NetworkIdentity CreatePlayerActor(
            NetworkContext context,
            Guid playerId,
            Vector3 position,
            string name = "Player")
        {
            GameObject gameObject = new(name);
            gameObject.SetActive(false);
            gameObject.transform.position = position;

            NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
            AuthorityOrigin authorityOrigin = gameObject.AddComponent<AuthorityOrigin>();

            SetPrivateField(networkIdentity, "_networkContext", context);
            SetPrivateField(authorityOrigin, "_networkContext", context);
            SetPrivateField(networkIdentity, "_distanceBasedAuthority", false);

            networkIdentity.NetworkOwner = playerId;
            networkIdentity.NetworkIdentifier = Guid.NewGuid();

            gameObject.SetActive(true);
            return networkIdentity;
        }

        public static NetworkIdentity CreateDistanceBasedObject(
            NetworkContext context,
            Vector3 position,
            Guid networkOwner,
            string name = "WorldObject")
        {
            GameObject gameObject = new(name);
            gameObject.SetActive(false);
            gameObject.transform.position = position;

            NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
            SetPrivateField(networkIdentity, "_networkContext", context);
            SetPrivateField(networkIdentity, "_distanceBasedAuthority", true);

            networkIdentity.NetworkOwner = networkOwner;
            networkIdentity.NetworkIdentifier = Guid.NewGuid();

            gameObject.SetActive(true);
            return networkIdentity;
        }
    }

    internal sealed class StubNetworkIdentity : INetworkIdentity
    {
        private readonly IReadOnlyList<INetworkState> _networkStates;

        public StubNetworkIdentity(
            GameObject gameObject,
            string prefabName,
            Guid networkOwner,
            Guid networkIdentifier,
            string channel = "test-channel",
            IReadOnlyList<INetworkState> networkStates = null)
        {
            GameObject = gameObject;
            PrefabName = prefabName;
            NetworkOwner = networkOwner;
            NetworkIdentifier = networkIdentifier;
            Channel = channel;
            _networkStates = networkStates ?? Array.Empty<INetworkState>();
        }

        public string PrefabName { get; }
        public GameObject GameObject { get; }
        public string Channel { get; set; }
        public Guid NetworkIdentifier { get; set; }
        public Guid NetworkOwner { get; set; }
        public bool NetworkAuthority => false;
        public bool DistanceBasedAuthority { get; set; }
        public string NetworkStateName => PrefabName;
        public NetworkContext NetworkContext => throw new NotImplementedException();

        public void WriteNetworkState(IStateOutput stateOutput)
        {
            stateOutput.WriteString(nameof(PrefabName), PrefabName);
            stateOutput.WriteGuid(nameof(NetworkOwner), NetworkOwner);

            stateOutput.BeginArrayProperty("NetworkStates");
            foreach (INetworkState networkState in _networkStates)
            {
                stateOutput.BeginObjectElement();
                networkState.WriteNetworkState(stateOutput);
                stateOutput.EndObject();
            }
            stateOutput.EndArray();
        }

        public void ReadNetworkState(IStateInput stateInput)
        {
            stateInput.ReadString(nameof(PrefabName));
            NetworkOwner = stateInput.ReadGuid(nameof(NetworkOwner));

            stateInput.BeginArrayProperty("NetworkStates");
            foreach (INetworkState networkState in _networkStates)
            {
                stateInput.BeginObjectElement();
                networkState.ReadNetworkState(stateInput);
                stateInput.EndObject();
            }
            stateInput.EndArray();
        }

        public bool ReadNetworkState(IStateInput stateInput, Guid senderGuid)
        {
            ReadNetworkState(stateInput);
            return true;
        }

        public void SendRpc(string rpcSubjectName, IStateOutput parametersStateOutput, bool invokeLocally = true) => throw new NotImplementedException();
    }

    internal sealed class StubRpcTarget : IRpcTarget
    {
        public StubRpcTarget(INetworkIdentity networkIdentity, string rpcSubjectName)
        {
            NetworkIdentity = networkIdentity;
            RpcSubjectName = rpcSubjectName;
        }

        public INetworkIdentity NetworkIdentity { get; }

        public string RpcSubjectName { get; }

        public int ReceiveCount { get; private set; }

        public int LastReceivedValue { get; private set; }

        public void ReceiveRpc(IStateInput parametersStateInput)
        {
            ReceiveCount++;
            LastReceivedValue = parametersStateInput.ReadInt("value");
        }
    }
}
