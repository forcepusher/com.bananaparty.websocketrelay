using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    public interface ICharacterInput
    {
        void PollInput();

        Vector2 MovementInput { get; }

        bool JumpInput { get; }
    }
}
