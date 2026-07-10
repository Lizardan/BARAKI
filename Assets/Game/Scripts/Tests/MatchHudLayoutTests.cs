using System.Collections;
using Game.UI.Controllers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests
{
    public sealed class MatchHudLayoutTests
    {
        [UnityTest]
        public IEnumerator BottomDock_IsAnchoredToLowerHalf_AndHasExpectedHeight()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            var controller = Object.FindAnyObjectByType<MatchHudController>();
            Assert.IsNotNull(controller, "Game scene should include MatchHudController.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument, "MatchHud should include UIDocument.");

            var root = uiDocument.rootVisualElement;
            Assert.IsNotNull(root, "MatchHud UIDocument should have a root visual element.");

            var dock = root.Q<VisualElement>("BottomDock");
            Assert.IsNotNull(dock, "MatchHud should include BottomDock.");

            var deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var rootHeight = GetResolvedHeight(root);
                var dockHeight = GetResolvedHeight(dock);
                if (rootHeight > 100f && dockHeight > 300f)
                {
                    break;
                }

                yield return null;
            }

            Assert.Greater(GetResolvedHeight(root), 100f, "HUD root should have non-zero height.");
            Assert.AreEqual(350f, GetResolvedHeight(dock), 1f);
            Assert.Greater(
                dock.worldBound.yMax,
                root.worldBound.height * 0.5f,
                "Bottom dock should sit in the lower half of the HUD.");
        }

        [Test]
        public void GameScene_MatchHud_DockUsesMatchDockPanelClasses()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");

            var controller = Object.FindAnyObjectByType<MatchHudController>();
            Assert.IsNotNull(controller, "Game scene should include MatchHudController.");

            var uiDocument = controller.GetComponent<UIDocument>();
            Assert.IsNotNull(uiDocument, "MatchHud should include UIDocument.");

            var root = uiDocument.rootVisualElement;
            var dock = root.Q<VisualElement>("BottomDock");
            Assert.IsNotNull(dock);

            Assert.IsTrue(dock.ClassListContains("match-bottom-dock"));
            Assert.IsTrue(root.Q("MinimapPanel").ClassListContains("match-dock-panel"));
            Assert.IsTrue(root.Q("ContextStrip").ClassListContains("match-dock-panel"));
            Assert.IsTrue(root.Q("InspectorPanel").ClassListContains("match-dock-panel"));
            Assert.IsFalse(root.Q("MinimapPanel").ClassListContains("fantasy-panel"));
        }

        static float GetResolvedHeight(VisualElement element)
        {
            var layoutHeight = element.layout.height;
            if (!float.IsNaN(layoutHeight) && layoutHeight > 0f)
            {
                return layoutHeight;
            }

            var resolvedHeight = element.resolvedStyle.height;
            if (!float.IsNaN(resolvedHeight) && resolvedHeight > 0f)
            {
                return resolvedHeight;
            }

            return element.worldBound.height;
        }
    }
}
