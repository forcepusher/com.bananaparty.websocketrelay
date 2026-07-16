using System.Collections.Generic;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Samples
{
    public class DebugOutput : MonoBehaviour
    {
        [SerializeField] private int maxLogs = 100;
        private readonly List<string> logs = new List<string>();
        private readonly object lockObject = new object();
        private Vector2 scrollPosition;
        private bool isVisible = false;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                isVisible = !isVisible;
            }
        }

        private void OnEnable()
        {
            Application.logMessageReceivedThreaded += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleLog;
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            lock (lockObject)
            {
                logs.Add($"[{type}] {condition}");
                if (logs.Count > maxLogs)
                {
                    logs.RemoveAt(0);
                }
            }
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
            GUILayout.BeginVertical("box");

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            lock (lockObject)
            {
                foreach (var log in logs)
                {
                    GUILayout.Label(log);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
