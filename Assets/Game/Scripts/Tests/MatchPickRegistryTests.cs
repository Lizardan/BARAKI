using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchPickRegistryTests
    {
        [Test]
        public void TryResolve_ReturnsRegisteredTarget()
        {
            var registry = new MatchPickRegistry();
            var gameObject = new GameObject("PickTest");
            var collider = gameObject.AddComponent<BoxCollider>();

            registry.Register(collider, MatchPickTarget.Unit(42));

            Assert.IsTrue(registry.TryResolve(collider, out var target));
            Assert.IsTrue(target.IsUnit);
            Assert.AreEqual(42, target.EntityId);

            Object.DestroyImmediate(gameObject);
        }
    }
}
