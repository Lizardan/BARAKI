using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AuraFxVisualsTests
    {
        [Test]
        public void StripNamedChildren_RemovesRaysOnly()
        {
            var root = new GameObject("Root");
            var rays = new GameObject(AuraFxVisuals.RaysChildName);
            rays.transform.SetParent(root.transform, false);
            var runes = new GameObject("Runes");
            runes.transform.SetParent(root.transform, false);

            AuraFxVisuals.StripNamedChildren(root, AuraFxVisuals.RaysChildName);

            Assert.IsTrue(root.transform.Find("Runes") != null);
            Assert.IsTrue(root.transform.Find(AuraFxVisuals.RaysChildName) == null);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void KeepOnlyNamedChildren_KeepsRaysOnly()
        {
            var root = new GameObject("Root");
            var rays = new GameObject(AuraFxVisuals.RaysChildName);
            rays.transform.SetParent(root.transform, false);
            var runes = new GameObject("Runes");
            runes.transform.SetParent(root.transform, false);

            AuraFxVisuals.KeepOnlyNamedChildren(root, AuraFxVisuals.RaysChildName);

            Assert.IsTrue(root.transform.Find(AuraFxVisuals.RaysChildName) != null);
            Assert.IsTrue(root.transform.Find("Runes") == null);

            Object.DestroyImmediate(root);
        }
    }
}
