using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    public class KeyboardCharacterInput : MonoBehaviour, ICharacterInput
    {
        public Vector2 MovementInput { get; private set; } = Vector2.zero;

        public bool JumpInput { get; private set; } = false;

        public void PollInput()
        {
            Vector2 movementInput = Vector2.zero;
            JumpInput = false;

            if (Input.GetKey(KeyCode.W)) movementInput.y += 1f;
            if (Input.GetKey(KeyCode.S)) movementInput.y -= 1f;
            if (Input.GetKey(KeyCode.A)) movementInput.x -= 1f;
            if (Input.GetKey(KeyCode.D)) movementInput.x += 1f;

            MovementInput = movementInput;
            JumpInput = Input.GetKeyDown(KeyCode.Space);
        }
    }
}
