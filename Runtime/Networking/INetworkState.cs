namespace BananaParty.WebSocketRelay
{
    public interface INetworkState
    {
        string NetworkStateName { get; }
        void WriteNetworkState(IStateOutput stateOutput);
        void ReadNetworkState(IStateInput stateInput);
    }
}
