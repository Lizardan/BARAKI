using System.Collections.Generic;
using Game.Editor;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ContentAssetLayoutTests
    {
        [Test]
        public void Catalogs_UseCanonicalPaths()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<RaceCatalog>(ContentAssetPaths.RaceCatalog));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(ContentAssetPaths.UnitVisualCatalog));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(ContentAssetPaths.UnitAbilityCatalog));

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<RaceCatalog>(
                ContentAssetPaths.Root + "/RaceCatalog.asset"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(
                ContentAssetPaths.Root + "/UnitVisualCatalog.asset"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(
                ContentAssetPaths.Root + "/Abilities/UnitAbilityCatalog.asset"));
        }

        [Test]
        public void HumanContent_IsGroupedByOwner()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_MELEE.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanCaster + "/UNIT_HUMAN_CASTER.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HeroDefinition>(
                ContentAssetPaths.HumanHero1 + "/HERO_HUMAN_1.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HeroDefinition>(
                ContentAssetPaths.HumanHero2 + "/HERO_HUMAN_2.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HeroDefinition>(
                ContentAssetPaths.HumanHero3 + "/HERO_HUMAN_3.asset"));

            Assert.AreEqual(6, AssetDatabase.FindAssets(
                "t:UnitDefinition", new[] { ContentAssetPaths.HumanUnits }).Length);
            Assert.AreEqual(3, AssetDatabase.FindAssets(
                "t:HeroDefinition", new[] { ContentAssetPaths.HumanHeroes }).Length);
            Assert.AreEqual(4, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanHero1Abilities }).Length);
            Assert.AreEqual(4, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanHero2Abilities }).Length);
            Assert.AreEqual(4, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanHero3Abilities }).Length);
            Assert.AreEqual(3, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanCasterAbilities }).Length);
            Assert.AreEqual(4, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanTitanAbilities }).Length);
        }

        [Test]
        public void HumanContent_HasNoEmptyScaffoldFolders()
        {
            foreach (var path in new[]
                     {
                         ContentAssetPaths.Humans + "/Abilities",
                         ContentAssetPaths.Humans + "/AI",
                         ContentAssetPaths.Humans + "/Bonuses",
                         ContentAssetPaths.Humans + "/Buildings",
                         ContentAssetPaths.Humans + "/Passives",
                         ContentAssetPaths.Humans + "/Tech",
                         ContentAssetPaths.HumanUnits + "/Base",
                         ContentAssetPaths.HumanUnits + "/Enhanced",
                         ContentAssetPaths.HumanHeroes + "/Base",
                         ContentAssetPaths.HumanHeroes + "/Enhanced",
                     })
            {
                Assert.IsFalse(AssetDatabase.IsValidFolder(path), path);
            }
        }

        [Test]
        public void AbilityCatalog_HasNineteenUniqueIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(
                ContentAssetPaths.UnitAbilityCatalog);
            Assert.IsNotNull(catalog);

            var ids = new HashSet<int>();
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:UnitAbilityDef", new[] { ContentAssetPaths.Humans }))
            {
                var def = AssetDatabase.LoadAssetAtPath<UnitAbilityDef>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(def);
                Assert.IsTrue(ids.Add(def.AbilityId), $"Duplicate ability id {def.AbilityId}.");
            }

            Assert.AreEqual(19, ids.Count);
        }

        [Test]
        public void HumanPrefabs_HaveOneCombatSettingsAndNoMissingScripts()
        {
            var prefabFolders = new[]
            {
                "Assets/Game/Prefabs/Races/Humans/Units",
                "Assets/Game/Prefabs/Races/Humans/Heroes",
            };

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", prefabFolders);
            Assert.AreEqual(10, prefabGuids.Length);

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                Assert.AreEqual(
                    1,
                    prefab.GetComponentsInChildren<UnitCombatSettings>(true).Length,
                    path);

                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Assert.AreEqual(
                        0,
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject),
                        $"{path}: {transform.name}");
                }
            }
        }
    }
}
