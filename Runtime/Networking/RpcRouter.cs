using System;
using System.Collections.Generic;
using System.Text;

namespace BananaParty.WebSocketRelay
{
    public class RpcRouter
    {
        // Rpc message layout: [type:1][subjectNameLength:2][subjectName][networkIdentifier:16][parameters].
        private const int SubjectNameOffset = 3;
        private const int HeaderSize = SubjectNameOffset + 16;

        private readonly IStateFormat _stateFormat;
        private readonly List<IRpcTarget> _rpcTargets = new();
        private readonly Queue<(string channel, byte[] message)> _outgoingMessages = new();

        public RpcRouter(IStateFormat stateFormat)
        {
            _stateFormat = stateFormat;
        }

        public void RegisterTarget(IRpcTarget rpcTarget)
        {
            _rpcTargets.Add(rpcTarget);
        }

        public void UnregisterTarget(IRpcTarget rpcTarget)
        {
            _rpcTargets.Remove(rpcTarget);
        }

        public void Send(Guid networkIdentifier, string rpcSubjectName, IStateOutput parametersStateOutput, string channel, bool invokeLocally = true)
        {
            byte[] parametersPayload = _stateFormat.ToPayload(parametersStateOutput);
            _outgoingMessages.Enqueue((channel, CreateMessage(networkIdentifier, rpcSubjectName, parametersPayload)));

            if (invokeLocally)
                Dispatch(networkIdentifier, rpcSubjectName, parametersPayload);
        }

        public bool TryDequeueOutgoingMessage(out string channel, out byte[] message)
        {
            if (_outgoingMessages.Count == 0)
            {
                channel = null;
                message = null;
                return false;
            }

            (channel, message) = _outgoingMessages.Dequeue();
            return true;
        }

        public void ProcessIncomingMessage(byte[] data)
        {
            int subjectNameLength = data[1] | (data[2] << 8);
            string rpcSubjectName = Encoding.UTF8.GetString(data, SubjectNameOffset, subjectNameLength);
            Guid networkIdentifier = new Guid(data.AsSpan(SubjectNameOffset + subjectNameLength, 16));

            byte[] parametersPayload = new byte[data.Length - HeaderSize - subjectNameLength];
            Buffer.BlockCopy(data, HeaderSize + subjectNameLength, parametersPayload, 0, parametersPayload.Length);

            Dispatch(networkIdentifier, rpcSubjectName, parametersPayload);
        }

        public void ClearOutgoingMessages()
        {
            _outgoingMessages.Clear();
        }

        private static byte[] CreateMessage(Guid networkIdentifier, string rpcSubjectName, byte[] parametersPayload)
        {
            byte[] subjectNameBytes = Encoding.UTF8.GetBytes(rpcSubjectName);
            byte[] message = new byte[HeaderSize + subjectNameBytes.Length + parametersPayload.Length];
            message[0] = NetworkMessage.Rpc;
            message[1] = (byte)subjectNameBytes.Length;
            message[2] = (byte)(subjectNameBytes.Length >> 8);
            Buffer.BlockCopy(subjectNameBytes, 0, message, SubjectNameOffset, subjectNameBytes.Length);
            Buffer.BlockCopy(networkIdentifier.ToByteArray(), 0, message, SubjectNameOffset + subjectNameBytes.Length, 16);
            Buffer.BlockCopy(parametersPayload, 0, message, HeaderSize + subjectNameBytes.Length, parametersPayload.Length);
            return message;
        }

        private void Dispatch(Guid networkIdentifier, string rpcSubjectName, byte[] parametersPayload)
        {
            // The identifier is matched at dispatch time instead of being indexed at
            // registration time, because a target can register before its identifier
            // is assigned. Unity interleaves Awake/OnEnable across components during
            // scene load, so a component's OnEnable registration can run before
            // NetworkBinding.Awake assigns the identity's NetworkIdentifier.
            foreach (IRpcTarget rpcTarget in _rpcTargets)
            {
                if (rpcTarget.NetworkIdentity.NetworkIdentifier != networkIdentifier)
                    continue;

                if (rpcTarget.RpcSubjectName != rpcSubjectName)
                    continue;

                rpcTarget.ReceiveRpc(_stateFormat.CreateInput(parametersPayload));
            }
        }
    }
}
