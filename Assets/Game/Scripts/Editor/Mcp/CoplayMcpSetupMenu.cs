using System;
using System.Reflection;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Applies Coplay MCP stdio prefs during project bootstrap.
    /// Avoids MCP For Unity HTTP UI on Unity 6.6 (SerializedProperty finalize crash).
    /// </summary>
    public static class CoplayMcpSetupMenu
    {
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string AutoRegisterKey = "MCPForUnity.AutoRegisterEnabled";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string LockCursorConfigKey = "MCPForUnity.LockCursorConfig";
        private const string UnitySocketPortKey = "MCPForUnity.UnitySocketPort";
        private const string McpEnsureSessionKey = "Game.Editor.McpEnsureRanThisSession";
        private const int PreferredUnityMcpPort = 6400;

        public static void ApplyStdioTransport()
        {
            EditorPrefs.SetBool(UseHttpTransportKey, false);
            EditorPrefs.SetBool(AutoRegisterKey, false);
            EditorPrefs.SetBool(AutoStartOnLoadKey, false);
            EditorPrefs.SetBool(LockCursorConfigKey, true);
        }

        /// <summary>
        /// Call from static ctor before Coplay starts the bridge — only updates stored port prefs/files.
        /// </summary>
        public static void EnsurePreferredPortRegistryOnly()
        {
            if (PortManager.GetPortWithFallback() == PreferredUnityMcpPort)
            {
                EditorPrefs.SetInt(UnitySocketPortKey, PreferredUnityMcpPort);
                return;
            }

            if (!PortManager.IsPortAvailable(PreferredUnityMcpPort))
            {
                return;
            }

            try
            {
                PortManager.SetPreferredPort(PreferredUnityMcpPort);
                EditorPrefs.SetInt(UnitySocketPortKey, PreferredUnityMcpPort);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Template] MCP port reset to {PreferredUnityMcpPort} skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply stdio transport prefs and register the stdio session once per Editor session. Never restarts the bridge.
        /// </summary>
        public static void EnsureOnEditorLoad()
        {
            ApplyStdioTransport();

            if (SessionState.GetBool(McpEnsureSessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += OnEnsureMcpDelayed;
        }

        private static void OnEnsureMcpDelayed()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += OnEnsureMcpDelayed;
                return;
            }

            if (SessionState.GetBool(McpEnsureSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(McpEnsureSessionKey, true);
            SyncUnitySocketPortPref();
            _ = RegisterStdioSessionAsync();
        }

        public static void SyncUnitySocketPortPref()
        {
            int expectedPort = StdioBridgeHost.IsRunning
                ? StdioBridgeHost.GetCurrentPort()
                : PortManager.GetPortWithFallback();
            int savedPort = EditorPrefs.GetInt(UnitySocketPortKey, 0);
            if (savedPort == expectedPort)
            {
                return;
            }

            EditorPrefs.SetInt(UnitySocketPortKey, expectedPort);
        }

        /// <summary>
        /// Registers stdio session in TransportManager after Coplay auto-starts the bridge.
        /// Does not call StartAsync — that would Stop()+Start() and log twice.
        /// </summary>
        private static async Task RegisterStdioSessionAsync()
        {
            if (EditorConfigurationCache.Instance.UseHttpTransport)
            {
                return;
            }

            var transportManager = MCPServiceLocator.TransportManager;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (EditorApplication.isCompiling)
                {
                    await Task.Delay(200);
                    continue;
                }

                if (transportManager.IsRunning(TransportMode.Stdio))
                {
                    SyncUnitySocketPortPref();
                    RequestMcpHealthVerification();
                    return;
                }

                if (StdioBridgeHost.IsRunning)
                {
                    EnsureStdioTransportClientExists();
                    await transportManager.VerifyAsync(TransportMode.Stdio);
                    SyncUnitySocketPortPref();
                    RequestMcpHealthVerification();
                    return;
                }

                await Task.Delay(200);
            }
        }

        /// <summary>
        /// TransportManager.VerifyAsync is a no-op until the stdio client is created (private GetOrCreateClient).
        /// </summary>
        private static void EnsureStdioTransportClientExists()
        {
            var transportManager = MCPServiceLocator.TransportManager;
            if (transportManager.GetClient(TransportMode.Stdio) != null)
            {
                return;
            }

            MethodInfo getOrCreate = typeof(TransportManager).GetMethod(
                "GetOrCreateClient",
                BindingFlags.NonPublic | BindingFlags.Instance);
            getOrCreate?.Invoke(transportManager, new object[] { TransportMode.Stdio });
        }

        private static void RequestMcpHealthVerification()
        {
            Type windowType = Type.GetType(
                "MCPForUnity.Editor.Windows.MCPForUnityEditorWindow, MCPForUnity.Editor");
            MethodInfo request = windowType?.GetMethod(
                "RequestHealthVerification",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            request?.Invoke(null, null);
        }
    }
}