using System;
using System.Collections.Generic;

namespace BananaParty.WebSocketRelay
{
    public interface IStateFormat
    {
        IStateOutput CreateOutput();

        IStateInput CreateInput(ReadOnlyMemory<byte> payload);

        IReadOnlyList<Guid> GetRootIdentityIds(ReadOnlyMemory<byte> payload);

        byte[] ToPayload(IStateOutput stateOutput);
    }
}
