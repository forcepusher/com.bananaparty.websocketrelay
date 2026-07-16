namespace BananaParty.WebSocketRelay.Tests
{
    public class BunTestCaseResult
    {
        public BunTestCaseResult(string name, bool passed, string failureMessage)
        {
            Name = name;
            Passed = passed;
            FailureMessage = failureMessage;
        }

        public string Name { get; }

        public bool Passed { get; }

        public string FailureMessage { get; }
    }
}
