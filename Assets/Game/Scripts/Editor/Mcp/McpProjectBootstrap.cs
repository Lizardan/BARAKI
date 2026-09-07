using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// MCP transport bootstrap. Lives in a separate asmdef so CI builds without com.coplaydev.unity-mcp.
    /// </summary>
    [InitializeOnLoad]
    public static class McpProjectBootstrap
    {
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

            CoplayMcpSetupMenu.EnsureOnEditorLoad();
        }
    }
}