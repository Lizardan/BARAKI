using Game.Core;
using Game.UI.Controllers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>
    /// Creates MainMenu / Lobby scenes, build order, and removes menu UI from Game.unity.
    /// </summary>
    public static class SceneFlowSetup
    {
        private const string BootstrapPath = "Assets/Game/Scenes/Bootstrap.unity";
        private const string MainMenuPath = "Assets/Game/Scenes/MainMenu.unity";
        private const string LobbyPath = "Assets/Game/Scenes/Lobby.unity";
        private const string GamePath = "Assets/Game/Scenes/Game.unity";
        private const string PanelSettingsPath = "Assets/Game/Settings/UI/DefaultPanelSettings.asset";
        private const string BootstrapUxmlPath = "Assets/Game/UI/Runtime/UXML/BootstrapLoading.uxml";
        private const string MainMenuUxmlPath = "Assets/Game/UI/Runtime/UXML/MainMenu.uxml";
        private const string LobbyUxmlPath = "Assets/Game/UI/Runtime/UXML/Lobby.uxml";

        public static void ConfigureFlowScenes()
        {
            EnsureFolder("Assets/Game/Scenes");

            ConfigureBootstrapScene();
            ConfigureMainMenuScene();
            ConfigureLobbyScene();
            RemoveMainMenuFromGameScene();
            TemplateSceneSetup.ConfigureGameScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[SceneFlow] Bootstrap → MainMenu → Lobby → Game configured.");
        }

        private static void ConfigureBootstrapScene()
        {
            var scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            EnsureUiCameraGroup();

            var uiGroup = GameObject.Find("--- UI ---");
            if (uiGroup == null)
            {
                uiGroup = new GameObject("--- UI ---");
            }

            var existing = GameObject.Find("BootstrapLoading");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            EnsureMenuUi(
                uiGroup.transform,
                "BootstrapLoading",
                BootstrapUxmlPath,
                typeof(BootstrapLoadingController));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = GameSceneNames.MainMenu;

            EnsureUiCameraGroup();
            var uiGroup = new GameObject("--- UI ---");
            EnsureMenuUi(
                uiGroup.transform,
                "MainMenu",
                MainMenuUxmlPath,
                typeof(MainMenuController));

            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        private static void ConfigureLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = GameSceneNames.Lobby;

            EnsureUiCameraGroup();
            var uiGroup = new GameObject("--- UI ---");
            EnsureMenuUi(
                uiGroup.transform,
                "Lobby",
                LobbyUxmlPath,
                typeof(LobbyController));

            EditorSceneManager.SaveScene(scene, LobbyPath);
        }

        private static void EnsureUiCameraGroup()
        {
            var camerasGroup = GameObject.Find("--- CAMERAS ---");
            if (camerasGroup == null)
            {
                camerasGroup = new GameObject("--- CAMERAS ---");
            }

            var existing = GameObject.FindWithTag("MainCamera");
            if (existing != null)
            {
                existing.transform.SetParent(camerasGroup.transform, false);
                return;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(camerasGroup.transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.047f, 0.055f, 0.078f);
            camera.depth = -10;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void EnsureMenuUi(
            Transform uiParent,
            string objectName,
            string uxmlPath,
            System.Type controllerType)
        {
            var menuObject = new GameObject(objectName);
            menuObject.transform.SetParent(uiParent, false);

            var uiDocument = menuObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            uiDocument.sortingOrder = 100;

            var controller = menuObject.AddComponent(controllerType);
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_uiDocument").objectReferenceValue = uiDocument;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveMainMenuFromGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Single);
            var menu = GameObject.Find("MainMenu");
            if (menu != null)
            {
                Object.DestroyImmediate(menu);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                SceneEntry(BootstrapPath),
                SceneEntry(MainMenuPath),
                SceneEntry(LobbyPath),
                SceneEntry(GamePath),
            };
        }

        private static EditorBuildSettingsScene SceneEntry(string path)
        {
            return new EditorBuildSettingsScene(path, true);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folder = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
