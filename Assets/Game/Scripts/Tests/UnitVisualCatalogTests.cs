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
        public void AllTwelvePortraits_AreAssigned()
        {
            UnitPortraitBaker.BakeIntoCatalog(_catalog);
            foreach (var raceId in new[] { GameIds.Races.Human, GameIds.Races.Bug })
            {
                foreach (UnitRole role in System.Enum.GetValues(typeof(UnitRole)))
                {
                    Assert.IsTrue(
                        _catalog.TryGetPortrait(raceId, role, out var portrait),
                        $"Missing portrait for {raceId} {role}");
                    Assert.IsNotNull(portrait, $"Null portrait for {raceId} {role}");
                }
            }
        }

        [Test]
        public void HumanMeleePrefab_HasMultipleTeamAccentsAndMaterials()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            Assert.GreaterOrEqual(UnitVisualAccent.CountAccents(prefab.transform), 2,
                "Human_Melee should include multiple TeamAccent meshes (shield/tabard/plume).");

            var hasShieldAccent = false;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "TeamAccent_ShieldFace")
                {
                    hasShieldAccent = true;
                    break;
                }
            }

            Assert.IsTrue(hasShieldAccent, "Human_Melee should include TeamAccent_ShieldFace.");

            var renderers = prefab.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 8);
            foreach (var renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"{renderer.name} should reference a saved URP material.");
            }
        }

        [Test]
        public void BugMeleePrefab_HasTeamAccent()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Bug, UnitRole.Melee, out var prefab));
            Assert.GreaterOrEqual(UnitVisualAccent.CountAccents(prefab.transform), 1,
                "Bug_Melee should include at least one TeamAccent carapace mesh.");
        }

        [Test]
        public void ApplyTeamColor_TintsAllAccents()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            var instance = Object.Instantiate(prefab);
            try
            {
                var color = new Color(0.1f, 0.4f, 0.9f, 1f);
                UnitVisualAccent.ApplyTeamColor(instance.transform, color);
                var accents = 0;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith(UnitVisualAccent.TeamAccentTransformName))
                    {
                        continue;
                    }

                    var renderer = t.GetComponent<Renderer>();
                    Assert.IsNotNull(renderer);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    var tinted = block.GetColor("_BaseColor");
                    Assert.AreEqual(color.r, tinted.r, 0.001f);
                    Assert.AreEqual(color.g, tinted.g, 0.001f);
                    Assert.AreEqual(color.b, tinted.b, 0.001f);
                    Assert.AreEqual(color.a, tinted.a, 0.001f);
                    accents++;
                }

                Assert.GreaterOrEqual(accents, 2);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
