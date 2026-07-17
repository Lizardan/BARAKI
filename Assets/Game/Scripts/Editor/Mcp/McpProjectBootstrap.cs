using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// MCP/Cursor transport bootstrap. Lives in a separate asmdef so CI builds without com.coplaydev.unity-mcp.
    /// </summary>
    [InitializeOnLoad]
    public static class McpProjectBootstrap
    {
        private const string CursorMarkerRelative = ".cursor/rules/unity-mcp.mdc";

        static McpProjectBootstrap()
        {
            CoplayMcpSetupMenu.EnsurePreferredPortRegistryOnly();
            EditorApplication.delayCall += OnEditorLoad;
        }

        private static void OnEditorLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += OnEditorLoad;
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            var markerPath = Path.Combine(projectRoot, CursorMarkerRelative);
            if (!File.Exists(markerPath))
            {
                // Wait for CursorProjectBootstrap first-time .cursor/ copy.
                EditorApplication.delayCall += OnEditorLoad;
                return;
            }

            CoplayMcpSetupMenu.EnsureOnEditorLoad();
        }
    }
}
