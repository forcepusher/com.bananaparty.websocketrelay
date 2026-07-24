using System;
using UnityEditor;
using UnityEngine;

namespace BananaParty.WebSocketRelay.Editor
{
    [CustomEditor(typeof(NetworkIdentity))]
    public class NetworkIdentityEditor : UnityEditor.Editor
    {
        private const float LabelWidth = 130f;
        private const float Padding = 6f;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override bool HasPreviewGUI() => true;

        public override GUIContent GetPreviewTitle() => new("Network Identity");

        public override void OnPreviewSettings()
        {
            if (!Application.isPlaying)
                return;

            var networkIdentity = (NetworkIdentity)target;
            using (new EditorGUI.DisabledScope(networkIdentity.NetworkContext == null))
            {
                if (GUILayout.Button("Claim Authority", EditorStyles.toolbarButton))
                    networkIdentity.ClaimAuthority();
            }
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var networkIdentity = (NetworkIdentity)target;

            if (Event.current.type == EventType.Repaint)
                background.Draw(r, false, false, false, false);

            Rect content = new(r.x + Padding, r.y + Padding, r.width - Padding * 2f, r.height - Padding * 2f);
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = content.y;

            bool hasContext = networkIdentity.NetworkContext != null;
            Guid localClient = hasContext ? networkIdentity.NetworkContext.LocalClientIdentity : Guid.Empty;

            using (new EditorGUI.DisabledScope(true))
            {
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = LabelWidth;

                DrawLine(ref y, content, lineHeight, spacing, "Has Authority", FormatAuthority(networkIdentity.NetworkAuthority, hasContext));
                DrawLine(ref y, content, lineHeight, spacing, "Has Authority Owner", networkIdentity.HasAuthorityOwner ? "Yes" : "No");
                DrawLine(ref y, content, lineHeight, spacing, "Authority Owner", FormatGuid(networkIdentity.NetworkAuthorityOwner, localClient));
                DrawLine(ref y, content, lineHeight, spacing, "Local Client", FormatGuid(localClient));
                DrawLine(ref y, content, lineHeight, spacing, "Network Identifier", FormatGuid(networkIdentity.NetworkIdentifier));
                DrawLine(ref y, content, lineHeight, spacing, "Authority Version", networkIdentity.NetworkAuthorityVersion.ToString());
                DrawLine(ref y, content, lineHeight, spacing, "Channel", string.IsNullOrEmpty(networkIdentity.Channel) ? "(none)" : networkIdentity.Channel);
                DrawLine(ref y, content, lineHeight, spacing, "Prefab Name", string.IsNullOrEmpty(networkIdentity.PrefabName) ? "(none)" : networkIdentity.PrefabName);

                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        private static void DrawLine(ref float y, Rect content, float lineHeight, float spacing, string label, string value)
        {
            var row = new Rect(content.x, y, content.width, lineHeight);
            EditorGUI.TextField(row, label, value);
            y += lineHeight + spacing;
        }

        private static string FormatAuthority(bool hasAuthority, bool hasContext)
        {
            if (!Application.isPlaying)
                return "(edit mode)";

            if (!hasContext)
                return "No NetworkContext";

            return hasAuthority ? "Yes (local)" : "No (remote)";
        }

        private static string FormatGuid(Guid guid, Guid? localClient = null)
        {
            if (guid == Guid.Empty)
                return "(none)";

            string value = guid.ToString();
            if (localClient.HasValue && localClient.Value != Guid.Empty && guid == localClient.Value)
                return $"{value} (local)";

            return value;
        }
    }
}
