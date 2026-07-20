using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Transport
{
    public class RelayServerProcess
    {
        private const string ProcessMarker = "-relay-server";
        private const string UnityPackageEntry = "com.bananaparty.websocketrelay/Runtime/RelayServer~/Source/index.ts";
        private const string StandaloneEntry = "Source/index.ts";

        private Process _process;

        public bool IsRunning => _process != null && !_process.HasExited;

        public static string GetServerDirectory() =>
            Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Packages",
                "com.bananaparty.websocketrelay",
                "Runtime",
                "RelayServer~"));

        public void Start(bool verboseDebug = false, bool createNoWindow = false, int? relayPort = null)
        {
            if (IsRunning)
                return;

            _process = Launch(createNoWindow, verboseDebug, relayPort);
        }

        public void Stop()
        {
            if (_process == null)
                return;

            try
            {
                StopProcess(_process);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Failed to stop server process: {e.Message}");
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        private static Process Launch(bool createNoWindow, bool verboseDebug, int? relayPort)
        {
            string serverDirectory = GetServerDirectory();
            KillAll();

            string bunPath = GetBunPath(serverDirectory);
            if (!File.Exists(bunPath))
                throw new FileNotFoundException($"Bundled Bun runtime not found at: {bunPath}");

            (string workingDirectory, string entryScript) = GetLaunchPaths(serverDirectory);
            ProcessStartInfo startInfo = CreateBunStartInfo(bunPath, serverDirectory, workingDirectory, entryScript, createNoWindow, verboseDebug, relayPort);
            Process process = Process.Start(startInfo);

            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (_, e) => ForwardLine(e.Data, isError: false);
            process.ErrorDataReceived += (_, e) => ForwardLine(e.Data, isError: true);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        private static void StopProcess(Process process)
        {
            KillAll();

            if (process == null || process.HasExited)
                return;

            process.Kill();
            process.WaitForExit(5000);
        }

        public static void KillAll()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                KillAllWindows();
            else
                KillAllUnix();
        }

        private static void KillAllWindows()
        {
            string embeddedBunPath = Path.GetFullPath(GetBunPath(GetServerDirectory()));

            foreach (Process process in Process.GetProcessesByName("bun"))
            {
                using (process)
                {
                    if (process.HasExited)
                        continue;

                    if (!Path.GetFullPath(process.MainModule.FileName).Equals(embeddedBunPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
        }

        private static void KillAllUnix()
        {
            string embeddedBunPath = Path.GetFullPath(GetBunPath(GetServerDirectory()));

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "pkill",
                Arguments = $"-f \"{embeddedBunPath}.*{ProcessMarker}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            using Process process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }

        private static ProcessStartInfo CreateBunStartInfo(
            string bunPath,
            string serverDirectory,
            string workingDirectory,
            string entryScript,
            bool createNoWindow,
            bool verboseDebug,
            int? relayPort)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = bunPath,
                Arguments = $"--cwd \"{workingDirectory}\" {entryScript} {ProcessMarker}",
                WorkingDirectory = serverDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            startInfo.Environment["RELAY_DEBUG"] = verboseDebug ? "1" : "0";
            if (relayPort.HasValue)
                startInfo.Environment["RELAY_PORT"] = relayPort.Value.ToString();

            return startInfo;
        }

        private static (string WorkingDirectory, string EntryScript) GetLaunchPaths(string serverDirectory)
        {
            string unityPackageManifest = Path.GetFullPath(Path.Combine(serverDirectory, "..", "..", "package.json"));
            if (File.Exists(unityPackageManifest))
            {
                return (
                    Path.GetFullPath(Path.Combine(serverDirectory, "..", "..", "..")),
                    UnityPackageEntry);
            }

            return (serverDirectory, StandaloneEntry);
        }

        private static string GetBunPath(string serverDirectory)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(serverDirectory, "Bun", "bun-windows-x64", "bun.exe");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Path.Combine(serverDirectory, "Bun", "bun-darwin-aarch64", "bun");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Path.Combine(serverDirectory, "Bun", "bun-linux-x64", "bun");

            throw new PlatformNotSupportedException("Unsupported operating system");
        }

        private static void ForwardLine(string line, bool isError)
        {
            if (string.IsNullOrEmpty(line))
                return;

            if (isError)
                UnityEngine.Debug.LogWarning($"[RelayServer] {line}");
            else
                UnityEngine.Debug.Log($"[RelayServer] {line}");
        }
    }
}
