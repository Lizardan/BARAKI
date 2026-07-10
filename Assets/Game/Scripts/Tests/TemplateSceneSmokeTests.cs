using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Tests
{
    public sealed class TemplateSceneSmokeTests
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

        [Test]
        public void SampleBurstVfxPrefab_HasVisualEffect()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Effects/SampleBurst.prefab");
            Assert.IsNotNull(prefab, "SampleBurst VFX prefab should exist.");
            Assert.IsNotNull(prefab.GetComponent<VisualEffect>(), "Prefab should use VisualEffect.");
        }

        [Test]
        public void SampleBurstVfxGraph_Exists()
        {
            var graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                "Assets/Game/Art/VFX/SampleBurst.vfx");
            Assert.IsNotNull(graph, "SampleBurst.vfx should exist under Art/VFX.");
        }
    }
}
