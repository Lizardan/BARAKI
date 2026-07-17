using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// Applies Coplay MCP stdio prefs and the Windows hidden launcher during project bootstrap.
    /// Avoids MCP For Unity HTTP UI on Unity 6.5 (SerializedProperty finalize crash).
    /// </summary>
    public static class CoplayMcpSetupMenu
    {
        private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
        private const string AutoRegisterKey = "MCPForUnity.AutoRegisterEnabled";
        private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
        private const string LockCursorConfigKey = "MCPForUnity.LockCursorConfig";
        private const string BootstrapRoot = "Assets/Game/Settings/ProjectBootstrap";
        private const string HiddenLauncherFileName = "unity-mcp-hidden.ps1";
        private const string UnityMcpServerKey = "unityMCP";
        private const string McpPackageName = "com.coplaydev.unity-mcp";
        private const string McpServerFromPlaceholder = "__MCP_SERVER_FROM__";
        private const string UnitySocketPortKey = "MCPForUnity.UnitySocketPort";
        private const string McpEnsureSessionKey = "Game.Editor.McpEnsureRanThisSession";
        private const int PreferredUnityMcpPort = 6400;

        private static string ResolveUvxExecutable(string userProfile)
        {
            var candidates = new[]
            {
                Path.Combine(userProfile, ".local", "bin", "uvx.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "uv",
                    "uvx.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string ResolveMcpServerFromPackage()
        {
            var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(p => p.name == McpPackageName);

            if (package == null || string.IsNullOrEmpty(package.version))
            {
                return "mcpforunityserver";
            }

            return FormatMcpServerFromArg(package.version);
        }

        private static string FormatMcpServerFromArg(string version)
        {
            if (version == "unknown")
            {
                return "mcpforunityserver";
            }

            // Semver prerelease (e.g. 9.4.5-beta.1) is not a valid uvx pin — match Coplay defaults.
            if (version.IndexOf('-', StringComparison.Ordinal) >= 0)
            {
                return "mcpforunityserver>=0.0.0a0";
            }

            return $"mcpforunityserver=={version}";
        }

        private static JsonArray BuildUvxStdioArgs(string serverFrom)
        {
            return new JsonArray
            {
                "--from",
                serverFrom,
                "mcp-for-unity",
                "--transport",
                "stdio"
            };
        }

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
        /// Sync Cursor config and stdio session UI once per Editor session. Never restarts the bridge.
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
            SyncCursorMcpConfigIfNeeded(logSuccess: false);
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

        public static bool SyncCursorMcpConfigIfNeeded(bool logSuccess)
        {
            var expectedServerFrom = ResolveMcpServerFromPackage();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var mcpPath = Path.Combine(userProfile, ".cursor", "mcp.json");

            if (File.Exists(mcpPath))
            {
                try
                {
                    var json = File.ReadAllText(mcpPath);
                    if (json.Contains(expectedServerFrom, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Template] Could not read Cursor MCP config: {ex.Message}");
                }
            }

            return ApplyHiddenConsoleLauncher(logSuccess);
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

        public static bool ApplyHiddenConsoleLauncher(bool logSuccess)
        {
#if !UNITY_EDITOR_WIN
            return false;
#else
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                  ?? throw new InvalidOperationException("Could not resolve project root.");

                var embeddedScript = Path.Combine(
                    projectRoot,
                    BootstrapRoot,
                    "Cursor",
                    "scripts",
                    HiddenLauncherFileName);

                if (!File.Exists(embeddedScript))
                {
                    Debug.LogWarning($"[Template] Hidden MCP launcher script missing: {embeddedScript}");
                    return false;
                }

                var scriptsDir = Path.Combine(userProfile, ".cursor", "scripts");
                Directory.CreateDirectory(scriptsDir);

                var targetScript = Path.Combine(scriptsDir, HiddenLauncherFileName);
                var serverFrom = ResolveMcpServerFromPackage();
                var scriptContent = File.ReadAllText(embeddedScript);
                if (scriptContent.Contains(McpServerFromPlaceholder, StringComparison.Ordinal))
                {
                    scriptContent = scriptContent.Replace(McpServerFromPlaceholder, serverFrom);
                }

                File.WriteAllText(targetScript, scriptContent);

                var mcpPath = Path.Combine(userProfile, ".cursor", "mcp.json");
                Directory.CreateDirectory(Path.GetDirectoryName(mcpPath) ?? userProfile);

                JsonObject root;
                if (File.Exists(mcpPath))
                {
                    root = JsonNode.Parse(File.ReadAllText(mcpPath))?.AsObject() ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                if (root["mcpServers"] is not JsonObject servers)
                {
                    servers = new JsonObject();
                    root["mcpServers"] = servers;
                }

                var uvxExecutable = ResolveUvxExecutable(userProfile);
                if (!string.IsNullOrEmpty(uvxExecutable))
                {
                    servers[UnityMcpServerKey] = new JsonObject
                    {
                        ["command"] = uvxExecutable,
                        ["args"] = BuildUvxStdioArgs(serverFrom)
                    };
                }
                else
                {
                    servers[UnityMcpServerKey] = new JsonObject
                    {
                        ["command"] = "powershell.exe",
                        ["args"] = new JsonArray
                        {
                            "-NoProfile",
                            "-ExecutionPolicy",
                            "Bypass",
                            "-WindowStyle",
                            "Hidden",
                            "-File",
                            targetScript
                        }
                    };
                }

                var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Default)
                {
                    WriteIndented = true
                };
                File.WriteAllText(mcpPath, root.ToJsonString(jsonOptions));

                if (logSuccess)
                {
                    Debug.Log(
                        "[Template] Coplay MCP: hidden console launcher applied to ~/.cursor/mcp.json. Restart Cursor.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Template] Could not apply hidden MCP console launcher: {ex.Message}");
                return false;
            }
#endif
        }
    }
}
