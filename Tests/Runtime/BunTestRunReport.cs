using System.Collections.Generic;
using System.Linq;

namespace BananaParty.WebSocketRelay.Tests
{
    public class BunTestRunReport
    {
        public BunTestRunReport(
            IReadOnlyList<BunTestCaseResult> cases,
            int exitCode,
            string standardOutput,
            string standardError,
            string launchError)
        {
            Cases = cases;
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
            LaunchError = launchError;
        }

        public IReadOnlyList<BunTestCaseResult> Cases { get; }

        public int ExitCode { get; }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public string LaunchError { get; }

        public bool Succeeded =>
            string.IsNullOrEmpty(LaunchError)
            && ExitCode == 0
            && Cases.Count > 0
            && Cases.All(testCase => testCase.Passed);

        public BunTestCaseResult FindByName(string name) =>
            Cases.FirstOrDefault(testCase => testCase.Name == name);
    }
}
