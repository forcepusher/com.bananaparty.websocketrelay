using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Editor
{
    public static class ServerExporter
    {
        private const string PackageName = "com.bananaparty.websocketrelay";
        private const string LastExportPathKey = "com.bananaparty.websocketrelay.exportServerPath";

        [MenuItem("Tools/WebSocket Relay/Export Server")]
        public static void ExportServer()
        {
            string sourceDirectory = GetServerDirectory();
            if (!Directory.Exists(sourceDirectory))
            {
                EditorUtility.DisplayDialog(
                    "Export Server",
                    $"Server folder not found at:\n{sourceDirectory}",
                    "OK");
                return;
            }

            string lastExportPath = EditorPrefs.GetString(LastExportPathKey, "");
            if (!string.IsNullOrEmpty(lastExportPath) && !Directory.Exists(lastExportPath))
                lastExportPath = "";

            string destinationDirectory = EditorUtility.OpenFolderPanel("Export Server", lastExportPath, "");
            if (string.IsNullOrEmpty(destinationDirectory))
                return;

            EditorPrefs.SetString(LastExportPathKey, destinationDirectory);

            bool destinationHasContent = Directory.GetFileSystemEntries(destinationDirectory).Length > 0;
            if (destinationHasContent)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Export Server",
                    $"The selected folder is not empty:\n{destinationDirectory}\n\nClear the folder and export server files?",
                    "Export",
                    "Cancel");

                if (!proceed)
                    return;
            }

            try
            {
                ClearDirectory(destinationDirectory);

                foreach (string sourceEntry in Directory.GetFileSystemEntries(sourceDirectory))
                {
                    string entryName = Path.GetFileName(sourceEntry);
                    string destinationEntry = Path.Combine(destinationDirectory, entryName);
                    FileUtil.CopyFileOrDirectory(sourceEntry, destinationEntry);
                }

                Debug.Log($"Exported relay server to: {destinationDirectory}");
                EditorUtility.DisplayDialog(
                    "Export Server",
                    $"Server exported successfully to:\n{destinationDirectory}",
                    "OK");
                EditorUtility.RevealInFinder(destinationDirectory);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Export Server",
                    $"Failed to export server:\n{exception.Message}",
                    "OK");
            }
        }

        private static void ClearDirectory(string directoryPath)
        {
            foreach (string entry in Directory.GetFileSystemEntries(directoryPath))
                FileUtil.DeleteFileOrDirectory(entry);
        }

        private static string GetServerDirectory()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (packageInfo == null)
                throw new InvalidOperationException($"Package not found: {PackageName}");

            return Path.Combine(packageInfo.resolvedPath, "Runtime", "RelayServer~");
        }
    }
}
