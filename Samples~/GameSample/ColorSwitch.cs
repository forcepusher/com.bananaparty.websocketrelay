using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    public class ColorSwitch : MonoBehaviour, IRpcTarget
    {
        private const string RandomColorParametername = "RandomColor";

        private NetworkIdentity _networkIdentity;

        public INetworkIdentity NetworkIdentity => _networkIdentity;

        private enum RpcType
        {
            RandomColorOnLeftClick,
            GreyColorOnRightClick
        }

        public string RpcSubjectName => nameof(ColorSwitch);

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
        }

        private void OnEnable()
        {
            _networkIdentity.NetworkContext.RegisterRpcTarget(this);
        }

        private void OnDisable()
        {
            _networkIdentity.NetworkContext.UnregisterRpcTarget(this);
        }

        private void Update()
        {
            if (_networkIdentity.NetworkAuthority)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    IStateOutput parametersOutput = _networkIdentity.NetworkContext.StateFormat.CreateOutput();
                    parametersOutput.WriteInt(nameof(RpcType), (int)RpcType.RandomColorOnLeftClick);
                    Color color = new(Random.value, Random.value, Random.value);
                    parametersOutput.WriteColor(RandomColorParametername, color);
                    _networkIdentity.SendRpc(RpcSubjectName, parametersOutput);
                }

                if (Input.GetMouseButtonDown(1))
                {
                    IStateOutput parametersOutput = _networkIdentity.NetworkContext.StateFormat.CreateOutput();
                    parametersOutput.WriteInt(nameof(RpcType), (int)RpcType.GreyColorOnRightClick);
                    _networkIdentity.SendRpc(RpcSubjectName, parametersOutput);
                }
            }
        }

        public void ReceiveRpc(IStateInput parametersStateInput)
        {
            RpcType rpcType = (RpcType)parametersStateInput.ReadInt(nameof(RpcType));
            switch (rpcType)
            {
                case RpcType.RandomColorOnLeftClick:
                    Color color = parametersStateInput.ReadColor(RandomColorParametername);
                    SetColor(color);
                    break;
                case RpcType.GreyColorOnRightClick:
                    SetColor(Color.grey);
                    break;
            }
        }

        private void SetColor(Color color)
        {
            GetComponent<Renderer>().material.color = color;
        }
    }
}
