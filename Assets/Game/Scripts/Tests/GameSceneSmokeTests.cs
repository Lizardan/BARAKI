using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameSceneSmokeTests
    {
        [Test]
        public void GameScene_HasCinemachineBrain()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var brain = Object.FindAnyObjectByType<CinemachineBrain>();
            Assert.IsNotNull(brain, "Main Camera should have CinemachineBrain.");
        }

        [Test]
        public void GameScene_HasCinemachineCamera()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            Assert.IsNotNull(vcam, "Game scene should include a CinemachineCamera.");
        }

        [Test]
        public void GameScene_HasMatchArenaGreybox()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var greybox = Object.FindAnyObjectByType<MatchArenaGreybox>();
            Assert.IsNotNull(greybox, "Game scene should include MatchArenaGreybox.");
            Assert.AreEqual(4, greybox.PlayerCount);
        }

        [Test]
        public void GameScene_HasIsometricCameraPanController()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            var panController = Object.FindAnyObjectByType<GameplayCameraPanController>();
            Assert.IsNotNull(panController, "Game scene should include edge-scroll camera pan.");
        }
    }
}
