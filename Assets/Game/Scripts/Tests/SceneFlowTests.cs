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
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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
        public void GameSceneNames_MatchBuildPaths()
        {
            Assert.AreEqual(GameSceneNames.Bootstrap, "Bootstrap");
            Assert.AreEqual(GameSceneNames.MainMenu, "MainMenu");
            Assert.AreEqual(GameSceneNames.Lobby, "Lobby");
            Assert.AreEqual(GameSceneNames.Game, "Game");
        }
    }
}
