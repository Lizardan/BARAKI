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
        public void HumanContent_IsGroupedByCategory()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanMelee + "/UNIT_HUMAN_MELEE.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanMeleeBonus + "/UNIT_HUMAN_MELEE_BONUS.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanCaster + "/UNIT_HUMAN_CASTER.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanCasterBonus + "/UNIT_HUMAN_CASTER_BONUS.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanSiegeBonus + "/UNIT_HUMAN_SIEGE_BONUS.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<HeroDefinition>(
                ContentAssetPaths.HumanHero1 + "/HERO_HUMAN_1.asset"));

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_MELEE_BONUS.asset"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_MELEE.asset"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnitDefinition>(
                ContentAssetPaths.HumanMelee + "/Bonus/UNIT_HUMAN_MELEE_BONUS.asset"));

            Assert.AreEqual(6, AssetDatabase.FindAssets(
                "t:UnitDefinition", new[] { ContentAssetPaths.HumanUnits }).Length);
            Assert.AreEqual(6, AssetDatabase.FindAssets(
                "t:UnitDefinition", new[] { ContentAssetPaths.HumanBonusUnits }).Length);
            Assert.AreEqual(3, AssetDatabase.FindAssets(
                "t:HeroDefinition", new[] { ContentAssetPaths.HumanHeroes }).Length);
            Assert.AreEqual(4, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanHero1Abilities }).Length);
            Assert.AreEqual(3, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanCasterAbilities }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanSiegeAbilities }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanMeleeAbilities }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanRangedAbilities }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanCasterBonusAbilities }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanFlyingAbilities }).Length);
            Assert.AreEqual(1, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanSuperAbilities }).Length);
            Assert.AreEqual(4, AssetDatabase.FindAssets(
                "t:UnitAbilityDef", new[] { ContentAssetPaths.HumanTitanAbilities }).Length);
        }

        [Test]
        public void HumanPortraits_AreGroupedByRaceAndKind()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.HumanPortraitUnits + "/Melee.png"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.HumanPortraitHeroes + "/Hero1.png"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.HumanPortraitHeroes + "/Titan.png"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.HumanPortraitBonusUnits + "/Melee.png"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.PortraitRoot + "/Human_Melee.png"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.HumanPortraitUnits + "/Titan.png"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>(
                ContentAssetPaths.HumanPortraits + "/Bonus/Melee.png"));
        }

        [Test]
        public void HumanPrefabs_LiveInCategoryFolders()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanMeleePath));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanMeleeBonusPath));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanHero1Path));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanTitanPath));

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanPath + "/Human_Melee.prefab"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanHeroesPath + "/Human_Hero1.prefab"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanPath + "/Titan/Human_Titan.prefab"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                UnitVisualPrefabBuilder.HumanPath + "/Melee/Bonus/Human_Melee_BONUS.prefab"));
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
                         ContentAssetPaths.HumanBonusHeroes,
                         ContentAssetPaths.HumanUnits + "/Base",
                         ContentAssetPaths.HumanUnits + "/Enhanced",
                         ContentAssetPaths.HumanUnits + "/Titan",
                         ContentAssetPaths.HumanUnits + "/Melee/Bonus",
                         ContentAssetPaths.HumanHeroes + "/Base",
                         ContentAssetPaths.HumanHeroes + "/Enhanced",
                         ContentAssetPaths.HumanPortraits + "/Bonus",
                         UnitVisualPrefabBuilder.HumanPath + "/Controllers",
                         UnitVisualPrefabBuilder.HumanPath + "/Titan",
                         UnitVisualPrefabBuilder.HumanPath + "/Melee/Bonus",
                         UnitVisualPrefabBuilder.HumanHeroesPath + "/Controllers",
                     })
            {
                Assert.IsFalse(AssetDatabase.IsValidFolder(path), path);
            }
        }

        [Test]
        public void AbilityCatalog_HasTwentyFiveUniqueIds()
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

            Assert.AreEqual(25, ids.Count);
            Assert.IsTrue(ids.Contains(AbilityIds.AuraHpRegen));
            Assert.IsTrue(ids.Contains(AbilityIds.MeleeCleave));
            Assert.IsTrue(ids.Contains(AbilityIds.SuperCatapult));
        }

        [Test]
        public void HumanPrefabs_HaveOneCombatSettingsAndNoMissingScripts()
        {
            var prefabFolders = new[]
            {
                UnitVisualPrefabBuilder.HumanPath,
                UnitVisualPrefabBuilder.HumanBonusUnitsPath,
                UnitVisualPrefabBuilder.HumanHeroesPath,
            };

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", prefabFolders);
            Assert.AreEqual(16, prefabGuids.Length);

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
