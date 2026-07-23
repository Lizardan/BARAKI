using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    /// <summary>
    /// Once per Editor session: opens the project folder in Cursor.
    /// </summary>
    [InitializeOnLoad]
    public static class CursorProjectBootstrap
    {
        private const string CursorOpenedSessionKey = "Game.Editor.CursorOpenedThisSession";

        static CursorProjectBootstrap()
        {
            EditorApplication.delayCall += OnEditorLoad;
        }

        private static void OnEditorLoad()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SessionState.GetBool(CursorOpenedSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(CursorOpenedSessionKey, true);
            TryOpenCursor(GetProjectRoot());
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName
                   ?? throw new InvalidOperationException("Could not resolve project root.");
        }

        private static void TryOpenCursor(string projectRoot)
        {
            var cursorExecutable = ResolveCursorExecutable();
            if (string.IsNullOrEmpty(cursorExecutable))
            {
                Debug.Log("[BARAKI] Cursor.exe not found. Open the project folder in Cursor manually.");
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
                Debug.Log($"[BARAKI] Could not launch Cursor: {ex.Message}. Open the project folder manually.");
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
