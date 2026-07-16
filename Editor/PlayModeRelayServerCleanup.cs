using BananaParty.WebSocketRelay.Transport;
using UnityEditor;

namespace BananaParty.WebSocketRelay.Editor
{
    [InitializeOnLoad]
    internal static class PlayModeRelayServerCleanup
    {
        static PlayModeRelayServerCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                RelayServerProcess.KillAll();
        }
    }
}
