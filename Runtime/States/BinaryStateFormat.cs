using System;
using System.Collections.Generic;

namespace BananaParty.WebSocketRelay
{
    public class BinaryStateFormat : IStateFormat
    {
        public IStateOutput CreateOutput() => new BinaryStateOutput();

        public IStateInput CreateInput(ReadOnlyMemory<byte> payload) => new BinaryStateInput(payload);

        public IReadOnlyList<Guid> GetRootIdentityIds(ReadOnlyMemory<byte> payload) => BinaryStateInput.GetRootIdentityIds(payload);

        public byte[] ToPayload(IStateOutput stateOutput)
        {
            using BinaryStateOutput binaryStateOutput = (BinaryStateOutput)stateOutput;
            return binaryStateOutput.ToArray();
        }
    }
}
