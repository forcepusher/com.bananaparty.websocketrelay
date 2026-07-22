using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    [RequireComponent(typeof(CharacterController))]
    public class Character : MonoBehaviour, INetworkState
    {
        public string NetworkStateName => nameof(Character);

        public INetworkIdentity NetworkIdentity => _networkIdentity;

        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float jumpHeight = 2f;

        private CharacterController _characteController;
        private ICharacterInput _characterInput;

        private float _verticalVelocity;

        private float _health = 100f;
        private Vector3 _position = Vector3.zero;

        private NetworkIdentity _networkIdentity;

        private void Awake()
        {
            _characteController = GetComponent<CharacterController>();
            _networkIdentity = GetComponent<NetworkIdentity>();

            _characterInput = GetComponent<ICharacterInput>();
        }

        private void Update()
        {
            _characterInput.PollInput();

            Move();
        }

        public void WriteNetworkState(IStateOutput stateOutput)
        {
            stateOutput.WriteFloat(nameof(_health), _health);
            stateOutput.WriteVector3(nameof(_position), transform.position);
        }

        public void ReadNetworkState(IStateInput stateInput)
        {
            float health = stateInput.ReadFloat(nameof(_health));
            Vector3 position = stateInput.ReadVector3(nameof(_position));

            if (!_networkIdentity.NetworkAuthority)
            {
                _health = health;
                _position = position;
                transform.position = position;
            }
        }

        private void Move()
        {
            if (_networkIdentity.NetworkAuthority)
            {
                Vector3 moveDirection = new Vector3(_characterInput.MovementInput.x, 0, _characterInput.MovementInput.y).normalized;

                if (moveDirection != Vector3.zero)
                {
                    _characteController.Move(moveDirection * moveSpeed * Time.deltaTime);

                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }

                if (_characterInput.JumpInput && _characteController.isGrounded)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * 2f * 9.81f);
                }

                if (_characteController.isGrounded && _verticalVelocity < 0)
                {
                    _verticalVelocity = -2f;
                }
                else
                {
                    _verticalVelocity -= 9.81f * Time.deltaTime;
                }

                _characteController.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
            }
        }
    }
}
