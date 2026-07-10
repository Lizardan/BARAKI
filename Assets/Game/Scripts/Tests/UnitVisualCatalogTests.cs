using Game.Core;
using Game.Editor;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitVisualCatalogTests
    {
        UnitVisualCatalog _catalog;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            UnitVisualPrefabBuilder.EnsureContent();
            _catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
        }

        [Test]
        public void Catalog_ExistsAfterEnsureContent()
        {
            Assert.IsNotNull(_catalog);
        }

        [Test]
        public void TryGetPrefab_HumanMelee_ReturnsNamedPrefab()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Human_Melee", prefab.name);
        }

        [Test]
        public void TryGetPrefab_BugCaster_ReturnsNamedPrefab()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Bug, UnitRole.Caster, out var prefab));
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Bug_Caster", prefab.name);
        }

        [Test]
        public void AllTwelvePrefabs_AreAssigned()
        {
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Bug })
            {
                foreach (UnitRole role in System.Enum.GetValues(typeof(UnitRole)))
                {
                    Assert.IsTrue(
                        _catalog.TryGetPrefab(raceId, role, out var prefab),
                        $"Missing prefab for {raceId} {role}");
                    Assert.IsNotNull(prefab, $"Null prefab for {raceId} {role}");
                }
            }
        }

        [Test]
        public void HumanMeleePrefab_HasTeamAccentAndMaterials()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            var accents = prefab.GetComponentsInChildren<Transform>(true);
            Transform accent = null;
            foreach (var t in accents)
            {
                if (t.name == UnitVisualAccent.TeamAccentTransformName)
                {
                    accent = t;
                    break;
                }
            }

            Assert.IsNotNull(accent, "Human_Melee should include TeamAccent for slot tinting.");
            Assert.IsNotNull(accent.parent, "TeamAccent should be parented to the body, not float at root.");

            var renderers = prefab.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 2);
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"{renderer.name} should reference a saved URP material.");
            }
        }
    }
}
