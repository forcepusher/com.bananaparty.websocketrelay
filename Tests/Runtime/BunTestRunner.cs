using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Tests
{
    public static class BunTestRunner
    {
        private const int RunTimeoutMs = 60_000;
        private const string RelayServerTestFile = "Source/RelayServer.test.ts";

        public static BunTestRunReport RunRelayServerTests()
        {
            string serverDirectory = GetServerDirectory();
            string bunExecutablePath = GetBunExecutablePath(serverDirectory);

            if (!File.Exists(bunExecutablePath))
            {
                return new BunTestRunReport(
                    Array.Empty<BunTestCaseResult>(),
                    -1,
                    string.Empty,
                    string.Empty,
                    $"Bun executable not found at: {bunExecutablePath}");
            }

            string testFilePath = Path.Combine(serverDirectory, RelayServerTestFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(testFilePath))
            {
                return new BunTestRunReport(
                    Array.Empty<BunTestCaseResult>(),
                    -1,
                    string.Empty,
                    string.Empty,
                    $"Bun test file not found at: {testFilePath}");
            }

            string junitReportPath = Path.Combine(Path.GetTempPath(), $"websocketrelay-bun-{Guid.NewGuid():N}.xml");

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = bunExecutablePath,
                    Arguments = $"test {RelayServerTestFile} --reporter=junit --reporter-outfile=\"{junitReportPath}\"",
                    WorkingDirectory = serverDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                using Process process = Process.Start(startInfo);
                if (process == null)
                {
                    return new BunTestRunReport(
                        Array.Empty<BunTestCaseResult>(),
                        -1,
                        string.Empty,
                        string.Empty,
                        "Failed to start bun test process.");
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(RunTimeoutMs))
                {
                    process.Kill();
                    return new BunTestRunReport(
                        Array.Empty<BunTestCaseResult>(),
                        -1,
                        standardOutput,
                        standardError,
                        $"Bun test process timed out after {RunTimeoutMs}ms.");
                }

                IReadOnlyList<BunTestCaseResult> cases = File.Exists(junitReportPath)
                    ? ParseJUnitReport(junitReportPath)
                    : Array.Empty<BunTestCaseResult>();

                return new BunTestRunReport(
                    cases,
                    process.ExitCode,
                    standardOutput,
                    standardError,
                    null);
            }
            finally
            {
                if (File.Exists(junitReportPath))
                    File.Delete(junitReportPath);
            }
        }

        public static string GetServerDirectory() =>
            Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.bananaparty.websocketrelay",
                "Runtime",
                "RelayServer~"));

        public static string GetBunExecutablePath(string serverDirectory) =>
            Application.platform switch
            {
                RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer =>
                    Path.Combine(serverDirectory, "Bun", "bun-windows-x64", "bun.exe"),
                RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer =>
                    Path.Combine(serverDirectory, "Bun", "bun-darwin-aarch64", "bun"),
                _ =>
                    Path.Combine(serverDirectory, "Bun", "bun-linux-x64", "bun"),
            };

        private static IReadOnlyList<BunTestCaseResult> ParseJUnitReport(string junitReportPath)
        {
            List<BunTestCaseResult> cases = new();
            XmlDocument document = new();
            document.Load(junitReportPath);

            XmlNodeList testCaseNodes = document.SelectNodes("//testcase");
            if (testCaseNodes == null)
                return cases;

            foreach (XmlNode testCaseNode in testCaseNodes)
            {
                string name = testCaseNode.Attributes?["name"]?.Value ?? "unknown";
                string className = testCaseNode.Attributes?["classname"]?.Value;
                string displayName = string.IsNullOrEmpty(className) ? name : $"{className} > {name}";

                XmlNode failureNode = testCaseNode.SelectSingleNode("failure");
                bool passed = failureNode == null;
                string failureMessage = passed
                    ? string.Empty
                    : failureNode.Attributes?["message"]?.Value ?? failureNode.InnerText;

                cases.Add(new BunTestCaseResult(displayName, passed, failureMessage));
            }

            return cases;
        }
    }
}
