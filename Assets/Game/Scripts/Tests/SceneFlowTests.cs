using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using Game.UI;
using Game.UI.Controllers;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public sealed class SceneFlowTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Game.Editor.SceneFlowSetup.ConfigureFlowScenes();
            Game.Editor.RaceContentBuilder.EnsureContent();
        }

        [Test]
        public void BuildSettings_HasFlowSceneOrder()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.GreaterOrEqual(scenes.Length, 4);

            Assert.AreEqual("Assets/Game/Scenes/Bootstrap.unity", scenes[0].path);
            Assert.AreEqual("Assets/Game/Scenes/MainMenu.unity", scenes[1].path);
            Assert.AreEqual("Assets/Game/Scenes/Lobby.unity", scenes[2].path);
            Assert.AreEqual("Assets/Game/Scenes/Game.unity", scenes[3].path);

            foreach (var scene in scenes)
            {
                Assert.IsTrue(scene.enabled, $"Scene should be enabled: {scene.path}");
            }
        }

        [Test]
        public void MainMenuScene_HasMainMenuController()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/MainMenu.unity");
            Assert.IsNotNull(Camera.main, "MainMenu scene should include Main Camera.");
            var controller = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(controller, "MainMenu scene should include MainMenuController.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument.visualTreeAsset, "MainMenu UIDocument should reference MainMenu.uxml.");
        }

        [Test]
        public void LobbyScene_HasLobbyController()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Lobby.unity");
            Assert.IsNotNull(Camera.main, "Lobby scene should include Main Camera.");
            var controller = Object.FindAnyObjectByType<LobbyController>();
            Assert.IsNotNull(controller, "Lobby scene should include LobbyController.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument, "Lobby should include UIDocument.");
            Assert.IsNotNull(uiDocument.visualTreeAsset, "Lobby UIDocument should reference Lobby.uxml.");
            var root = uiDocument.visualTreeAsset.CloneTree();
            Assert.IsNotNull(
                root.Q<Button>("FillLocalButton"),
                "Lobby.uxml should keep FillLocalButton for Editor-only fill-slots.");
        }

        [Test]
        public void GameScene_HasMatchRuntime()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var runtime = Object.FindAnyObjectByType<MatchRuntime>();
            Assert.IsNotNull(runtime, "Game scene should include MatchRuntime.");
        }

        [Test]
        public void GameScene_HasRacePickController()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var controller = Object.FindAnyObjectByType<RacePickController>();
            Assert.IsNotNull(controller, "Game scene should include RacePickController.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument.visualTreeAsset, "RacePick UIDocument should reference RacePick.uxml.");
        }

        [Test]
        public void GameScene_HasMatchHudController()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var controller = Object.FindAnyObjectByType<MatchHudController>();
            Assert.IsNotNull(controller, "Game scene should include MatchHudController.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument.visualTreeAsset, "MatchHud UIDocument should reference MatchHud.uxml.");
        }

        [Test]
        public void GameScene_HasCameraAndArenaGreybox()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            Assert.IsNotNull(
                Object.FindAnyObjectByType<CinemachineBrain>(),
                "Main Camera should have CinemachineBrain.");
            Assert.IsNotNull(
                Object.FindAnyObjectByType<CinemachineCamera>(),
                "Game scene should include a CinemachineCamera.");

            var greybox = Object.FindAnyObjectByType<MatchArenaGreybox>();
            Assert.IsNotNull(greybox, "Game scene should include MatchArenaGreybox.");
            Assert.AreEqual(4, greybox.PlayerCount);

            Assert.IsNotNull(
                Object.FindAnyObjectByType<GameplayCameraPanController>(),
                "Game scene should include edge-scroll camera pan.");
        }

        [Test]
        public void GameScene_HasSelectionUiControllers()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            Assert.IsNotNull(Object.FindAnyObjectByType<MatchSelectionBridge>());
            Assert.IsNotNull(Object.FindAnyObjectByType<MatchMinimapController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<MatchContextStripController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<MatchInspectorController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<MatchSelectionUiGate>());
        }

        [Test]
        public void GameScene_HasNoMainMenu()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var menu = GameObject.Find("MainMenu");
            Assert.IsNull(menu, "Game scene should not contain MainMenu UI.");
        }

        [Test]
        public void BootstrapScene_HasNoNetworkPrefabSources()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var networkObjects = Object.FindObjectsByType<Unity.Netcode.NetworkObject>(
                FindObjectsInactive.Include);
            foreach (var networkObject in networkObjects)
            {
                Assert.AreNotEqual(
                    "NetworkLobbyState_PrefabSource",
                    networkObject.name,
                    "Bootstrap should not include unspawned NetworkLobbyState scene sources.");
                Assert.AreNotEqual(
                    "MatchNetworkAuthority_PrefabSource",
                    networkObject.name,
                    "Bootstrap should not include unspawned MatchNetworkAuthority scene sources.");
            }
        }

        [Test]
        public void NetworkLobbyPrefab_OwnsRacePickState()
        {
            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Resources/Networking/NetworkLobbyState.prefab");
            var authorityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Resources/Networking/MatchNetworkAuthority.prefab");

            Assert.IsNotNull(lobbyPrefab, "NetworkLobbyState prefab should exist.");
            Assert.IsNotNull(authorityPrefab, "MatchNetworkAuthority prefab should exist.");
            Assert.IsNotNull(
                lobbyPrefab.GetComponent<NetworkRacePickState>(),
                "Race pick state must replicate with NetworkLobbyState so clients can submit picks.");
            Assert.IsNull(
                authorityPrefab.GetComponent<NetworkRacePickState>(),
                "MatchNetworkAuthority should not own race pick state or clients can miss it during race pick.");
        }

        [Test]
        public void BootstrapScene_HasLauncherControllerAsSoleBootstrapUi()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var controller = Object.FindAnyObjectByType<LauncherController>();
            Assert.IsNotNull(controller, "Bootstrap scene should include LauncherController.");
            Assert.IsTrue(
                controller.enabled,
                "LauncherController must be enabled or bootstrap pipeline never starts.");

            Assert.IsNull(
                GameObject.Find("BootstrapLoading"),
                "Legacy BootstrapLoading GameObject must be removed from Bootstrap scene.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument, "Launcher should include UIDocument.");
            Assert.IsNotNull(uiDocument.visualTreeAsset, "Launcher UIDocument should reference Launcher.uxml.");
            StringAssert.Contains("Launcher", uiDocument.visualTreeAsset.name);

            var root = uiDocument.visualTreeAsset.CloneTree();
            Assert.IsNotNull(root.Q<Button>("PlayButton"), "Launcher.uxml should include PlayButton CTA.");
            Assert.IsNotNull(root.Q<Label>("ClientVersionLabel"), "Launcher.uxml should include ClientVersionLabel.");
            Assert.IsNull(root.Q<VisualElement>("ClientMeta"), "Launcher.uxml should not include ClientMeta.");
            Assert.IsNotNull(root.Q<VisualElement>("ProgressBlock"), "Launcher.uxml should include ProgressBlock.");
            Assert.IsNotNull(root.Q<Label>("ProgressStatusLabel"), "Launcher.uxml should include ProgressStatusLabel.");
            Assert.IsNotNull(root.Q<VisualElement>("ProgressFill"), "Launcher.uxml should include ProgressFill.");
            Assert.IsNotNull(root.Q<Label>("ProgressErrorLabel"), "Launcher.uxml should include ProgressErrorLabel.");
            Assert.IsNotNull(root.Q<VisualElement>("NewsList"), "Launcher.uxml should include NewsList.");
            Assert.IsNotNull(root.Q<VisualElement>("ChatGlobalMessages"), "Launcher.uxml should include ChatGlobalMessages.");
            Assert.IsNotNull(root.Q<Button>("ChatGlobalTabButton"), "Launcher.uxml should include ChatGlobalTabButton.");
        }

        [Test]
        public void MainMenuUxml_HasNoHubLoadingOverlaysOrUpdateControls()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Game/UI/Runtime/UXML/MainMenu.uxml");
            Assert.IsNotNull(asset, "MainMenu.uxml should exist.");

            var root = asset.CloneTree();
            Assert.IsNull(root.Q<VisualElement>("ProfileLoadingOverlay"),
                "MainMenu should not include ProfileLoadingOverlay.");
            Assert.IsNull(root.Q<VisualElement>("HubLoadingOverlay"),
                "MainMenu should not include HubLoadingOverlay.");
            Assert.IsNull(root.Q<Button>("VersionUpdateButton"),
                "MainMenu should not include VersionUpdateButton.");
            Assert.IsNull(root.Q<VisualElement>("VersionProgress"),
                "MainMenu should not include VersionProgress.");
            Assert.IsNotNull(root.Q<Label>("VersionLabel"),
                "MainMenu should still show local VersionLabel.");
        }

        [Test]
        public void GameSceneNames_MatchBuildPaths()
        {
            Assert.AreEqual(GameSceneNames.Bootstrap, "Bootstrap");
            Assert.AreEqual(GameSceneNames.MainMenu, "MainMenu");
            Assert.AreEqual(GameSceneNames.Lobby, "Lobby");
            Assert.AreEqual(GameSceneNames.Game, "Game");
        }
    }
}
