using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    /// <summary>
    /// On first project open: copies embedded Cursor rules and applies Coplay MCP stdio.
    /// Once per Editor session: opens the project folder in Cursor (not on domain reload).
    /// </summary>
    [InitializeOnLoad]
    public static class CursorProjectBootstrap
    {
        private const string BootstrapRoot = "Assets/Game/Settings/ProjectBootstrap";
        private const string CursorMarkerRelative = ".cursor/rules/unity-mcp.mdc";
        private const string CursorOpenedSessionKey = "Game.Editor.CursorOpenedThisSession";

        static CursorProjectBootstrap()
        {
            EditorApplication.delayCall += OnEditorLoad;
        }

        /// <summary>
        /// Copies root <c>.cursor/</c>, <c>.cursorrules</c>, and <c>.gitignore</c> into ProjectBootstrap for Hub packaging.
        /// Callable from Editor diagnostics; maintainers usually sync via Cursor agent before saving the Hub template.
        /// </summary>
        public static void SyncEmbeddedTemplateFromProjectRoot()
        {
            var projectRoot = GetProjectRoot();
            var bootstrapRoot = Path.Combine(projectRoot, BootstrapRoot);
            var cursorSource = Path.Combine(projectRoot, ".cursor");

            if (!Directory.Exists(cursorSource))
            {
                Debug.LogWarning("[Template] Project root has no .cursor/ folder to sync from.");
                return;
            }

            CopyDirectory(cursorSource, Path.Combine(bootstrapRoot, "Cursor"), overwrite: true);

            CopyIfExists(
                Path.Combine(projectRoot, ".cursorrules"),
                Path.Combine(bootstrapRoot, "cursorrules"));
            CopyIfExists(
                Path.Combine(projectRoot, ".gitignore"),
                Path.Combine(bootstrapRoot, "gitignore"));

            AssetDatabase.Refresh();
            Debug.Log("[Template] Synced ProjectBootstrap from project root .cursor/.");
        }

        private static void OnEditorLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var projectRoot = GetProjectRoot();

            if (!SessionState.GetBool(CursorOpenedSessionKey, false))
            {
                SessionState.SetBool(CursorOpenedSessionKey, true);
                TryOpenCursor(projectRoot);
            }

            RunFirstTimeBootstrap(projectRoot);
        }

        private static void RunFirstTimeBootstrap(string projectRoot)
        {
            var markerPath = Path.Combine(projectRoot, CursorMarkerRelative);

            if (File.Exists(markerPath))
            {
                return;
            }

            var bootstrapRoot = Path.Combine(projectRoot, BootstrapRoot);
            if (!Directory.Exists(bootstrapRoot))
            {
                Debug.LogWarning("[Template] ProjectBootstrap folder missing; skip Cursor bootstrap.");
                return;
            }

            CopyIfExists(
                Path.Combine(bootstrapRoot, "cursorrules"),
                Path.Combine(projectRoot, ".cursorrules"));
            CopyIfExists(
                Path.Combine(bootstrapRoot, "gitignore"),
                Path.Combine(projectRoot, ".gitignore"));

            var embeddedCursor = Path.Combine(bootstrapRoot, "Cursor");
            if (Directory.Exists(embeddedCursor))
            {
                CopyDirectory(embeddedCursor, Path.Combine(projectRoot, ".cursor"), overwrite: true);
            }

            Debug.Log(
                "[Template] Cursor bootstrap complete: .cursor/ copied. " +
                "Restart Cursor if it was already open for this folder.");
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName
                   ?? throw new InvalidOperationException("Could not resolve project root.");
        }

        private static void CopyIfExists(string source, string destination)
        {
            if (!File.Exists(source))
            {
                return;
            }

            var destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(source, destination, overwrite: true);
        }

        private static void CopyDirectory(string source, string destination, bool overwrite)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destination, fileName), overwrite);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), overwrite);
            }
        }

        private static void TryOpenCursor(string projectRoot)
        {
            var cursorExecutable = ResolveCursorExecutable();
            if (string.IsNullOrEmpty(cursorExecutable))
            {
                Debug.Log("[Template] Cursor.exe not found. Open the project folder in Cursor manually.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = cursorExecutable,
                    Arguments = $"\"{projectRoot}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.Log($"[Template] Could not launch Cursor: {ex.Message}. Open the project folder manually.");
            }
        }

        private static string ResolveCursorExecutable()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
            {
                Path.Combine(localAppData, "Programs", "cursor", "Cursor.exe"),
                Path.Combine(localAppData, "Programs", "Cursor", "Cursor.exe")
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

    }
}
