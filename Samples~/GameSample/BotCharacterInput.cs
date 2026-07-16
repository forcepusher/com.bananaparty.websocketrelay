using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    public class BotCharacterInput : MonoBehaviour, ICharacterInput
    {
        public Vector2 MovementInput { get; private set; } = Vector2.zero;

        public bool JumpInput { get; private set; } = false;

        private float _nextDecisionTime;
        private Vector2 _currentMovement;

        public void PollInput()
        {
            if (Time.time >= _nextDecisionTime)
            {
                // 20% chance to stay idle, otherwise move in a random cardinal/intercardinal direction
                if (Random.value < 0.2f)
                {
                    _currentMovement = Vector2.zero;
                }
                else
                {
                    float angle = Random.Range(0, 8) * Mathf.PI / 4f;
                    _currentMovement = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }

                _nextDecisionTime = Time.time + Random.Range(0.5f, 2.0f);
            }

            MovementInput = _currentMovement;

            // Small random chance to jump every frame for "silly" behavior
            JumpInput = Random.value < 0.02f;
        }
    }
}
