using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Opens Bootstrap when the Editor starts with no scene loaded (e.g. fresh project from Hub template).
    /// </summary>
    [InitializeOnLoad]
    public static class DefaultBootstrapSceneLoader
    {
        private const string BootstrapScenePath = "Assets/Game/Scenes/Bootstrap.unity";

        static DefaultBootstrapSceneLoader()
        {
            EditorApplication.delayCall += TryOpenBootstrapScene;
        }

        private static void TryOpenBootstrapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(activeScene.path))
            {
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            var bootstrapPath = Path.Combine(projectRoot, BootstrapScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(bootstrapPath))
            {
                return;
            }

            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        }
    }
}
