using System;
using System.Collections.Generic;
using System.Text;

namespace BananaParty.WebSocketRelay
{
    public class JsonStateFormat : IStateFormat
    {
        public IStateOutput CreateOutput() => new JsonStateOutput(prettyPrint: false, bracesOnNewLine: false);

        public IStateInput CreateInput(ReadOnlyMemory<byte> payload) => new JsonStateInput(Encoding.UTF8.GetString(payload.Span));

        public IReadOnlyList<Guid> GetRootIdentityIds(ReadOnlyMemory<byte> payload) => JsonStateInput.GetRootIdentityIds(Encoding.UTF8.GetString(payload.Span));

        public byte[] ToPayload(IStateOutput stateOutput) => Encoding.UTF8.GetBytes(stateOutput.ToString());
    }
}
