using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.UI;
using Game.UI.Controllers;
using NUnit.Framework;
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
        public void BootstrapScene_HasSingleNetworkLobbyPrefabSource()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var lobbySources = Object.FindObjectsByType<Unity.Netcode.NetworkObject>(
                FindObjectsInactive.Include);
            var lobbyPrefabSources = 0;
            foreach (var networkObject in lobbySources)
            {
                if (networkObject.name == "NetworkLobbyState_PrefabSource")
                {
                    lobbyPrefabSources++;
                }
            }

            Assert.AreEqual(1, lobbyPrefabSources, "Bootstrap should contain exactly one NetworkLobbyState_PrefabSource.");
        }

        [Test]
        public void BootstrapScene_HasBootstrapLoadingController()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            var controller = Object.FindAnyObjectByType<BootstrapLoadingController>();
            Assert.IsNotNull(controller, "Bootstrap scene should include BootstrapLoadingController.");
            Assert.IsTrue(
                controller.enabled,
                "BootstrapLoadingController must be enabled or bootstrap pipeline never starts.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument, "BootstrapLoading should include UIDocument.");
            Assert.IsNotNull(uiDocument.visualTreeAsset, "Bootstrap UIDocument should reference BootstrapLoading.uxml.");

            var root = uiDocument.visualTreeAsset.CloneTree();
            Assert.IsNotNull(root.Q<Label>("StatusLabel"), "BootstrapLoading.uxml should include StatusLabel.");
            Assert.IsNotNull(root.Q<Label>("UpdateTitleLabel"), "BootstrapLoading.uxml should include UpdateTitleLabel.");
            Assert.IsNotNull(root.Q<VisualElement>("UpdateRangeLabel"), "BootstrapLoading.uxml should include UpdateRangeLabel.");
            Assert.IsNotNull(root.Q<Button>("UpdateButton"), "BootstrapLoading.uxml should include UpdateButton.");
            Assert.IsNotNull(root.Q<Button>("EnterGameButton"), "BootstrapLoading.uxml should include EnterGameButton.");
            Assert.IsNotNull(root.Q<Button>("QuitButton"), "BootstrapLoading.uxml should include QuitButton.");
            Assert.IsNotNull(root.Q<Label>("VersionLabel"), "BootstrapLoading.uxml should include VersionLabel.");
            Assert.IsNotNull(root.Q<VisualElement>("VersionProgress"), "BootstrapLoading.uxml should include VersionProgress.");
            Assert.IsNotNull(root.Q<VisualElement>("VersionProgressFill"), "BootstrapLoading.uxml should include VersionProgressFill.");
            Assert.IsNotNull(root.Q<Label>("VersionProgressLabel"), "BootstrapLoading.uxml should include VersionProgressLabel.");
            Assert.IsNotNull(root.Q<Label>("UpdateStatusLabel"), "BootstrapLoading.uxml should include UpdateStatusLabel.");

            Assert.IsNotNull(root.Q<VisualElement>("TopChrome"), "BootstrapLoading.uxml should include TopChrome.");
            Assert.IsNotNull(root.Q<VisualElement>("SideRail"), "BootstrapLoading.uxml should include SideRail.");
            Assert.IsNotNull(root.Q<VisualElement>("NewsFeed"), "BootstrapLoading.uxml should include NewsFeed.");
            Assert.IsNotNull(root.Q<VisualElement>("NewsFeatured"), "BootstrapLoading.uxml should include NewsFeatured.");
            Assert.IsNotNull(root.Q<VisualElement>("NewsListContainer"), "BootstrapLoading.uxml should include NewsListContainer.");
            Assert.IsNotNull(root.Q<Label>("NewsFeaturedTitle"), "BootstrapLoading.uxml should include NewsFeaturedTitle.");
            Assert.IsNotNull(root.Q<Label>("NewsFeaturedBody"), "BootstrapLoading.uxml should include NewsFeaturedBody.");
            Assert.IsNotNull(root.Q<Label>("SideStatusTitle"), "BootstrapLoading.uxml should include SideStatusTitle.");
            Assert.IsNotNull(root.Q<Label>("SideStatusLabel"), "BootstrapLoading.uxml should include SideStatusLabel.");
            Assert.IsNull(root.Q<VisualElement>("BrandLogo"), "Bootstrap launcher should not include BrandLogo.");
            Assert.IsNull(root.Q<VisualElement>("Hero"), "Bootstrap launcher should not include centered Hero.");
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
