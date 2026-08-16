using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Creates MVP race/unit/hero/squad/upgrade ScriptableObject assets from GDD baseline stats.</summary>
    public static class RaceContentBuilder
    {
        public const string RootPath = ContentAssetPaths.Root;
        public const string CatalogPath = ContentAssetPaths.RaceCatalog;

        public static void EnsureContent()
        {
            EnsureFolder(ContentAssetPaths.HumanUnits);
            ContentAssetPaths.EnsureHumanUnitFolders();
            EnsureFolder(ContentAssetPaths.HumanHero1);
            EnsureFolder(ContentAssetPaths.HumanHero2);
            EnsureFolder(ContentAssetPaths.HumanHero3);
            EnsureFolder(ContentAssetPaths.Humans);
            EnsureFolder(ContentAssetPaths.SharedSquads);
            EnsureFolder(ContentAssetPaths.SharedUpgrades);

            var humanUnits = CreateHumanUnits();
            var humanBonusUnits = CreateHumanBonusUnits();
            var humanHeroes = CreateHeroes();

            var human = CreateRace(
                GameIds.Races.Human,
                "Люди",
                humanUnits,
                humanBonusUnits,
                humanHeroes,
                new[] { GameIds.Passives.HumanSteelArms, GameIds.Passives.HumanFortifiedLine },
                GameIds.Passives.HumanLevyTax);

            var squads = new[]
            {
                CreateSquad(1, 2, 1, 1, 0, 0, 0),
                CreateSquad(2, 3, 1, 1, 2, 0, 0),
                CreateSquad(3, 3, 2, 2, 2, 1, 0),
                CreateSquad(4, 4, 3, 2, 3, 1, 1),
            };

            var statTracks = new[]
            {
                CreateStatTrack(GameIds.Upgrades.MeleeDamage, 0.03f,
                    new[] { 75, 100, 125, 150, 175, 200, 225, 250, 275 },
                    new[] { 8f, 10f, 12f, 14f, 16f, 18f, 20f, 22f, 24f }),
                CreateStatTrack(GameIds.Upgrades.RangedDamage, 0.03f,
                    new[] { 75, 100, 125, 150, 175, 200, 225, 250, 275 },
                    new[] { 8f, 10f, 12f, 14f, 16f, 18f, 20f, 22f, 24f }),
                CreateStatTrack(GameIds.Upgrades.Armor, 0.03f,
                    new[] { 60, 80, 100, 120, 140, 160, 180, 200, 220 },
                    new[] { 6f, 8f, 10f, 12f, 14f, 16f, 18f, 20f, 22f }),
            };

            CreateOrUpdateCatalog(human, squads, statTracks);
            AssetDatabase.SaveAssets();
        }

        private static UnitDefinition[] CreateHumanUnits()
        {
            return new[]
            {
                CreateUnit(GameIds.Units.HumanMelee, GameIds.Races.Human, UnitRole.Melee,
                    120f, 1f, 8f, 10f, 1f, 1.5f, 4f, 8),
                CreateUnit(GameIds.Units.HumanRanged, GameIds.Races.Human, UnitRole.Ranged,
                    70f, 0f, 6f, 8f, 1f, 8f, RaceMarchSpeedRules.BaseMarchSpeed, 6),
                CreateUnit(GameIds.Units.HumanCaster, GameIds.Races.Human, UnitRole.Caster,
                    60f, 0f, 4f, 5f, 1f, 6f, RaceMarchSpeedRules.BaseMarchSpeed, 7),
                CreateUnit(GameIds.Units.HumanSiege, GameIds.Races.Human, UnitRole.Siege,
                    200f, 0f, 12f, 16f, 1f, 1.5f, RaceMarchSpeedRules.BaseMarchSpeed, 15),
                CreateUnit(GameIds.Units.HumanFlying, GameIds.Races.Human, UnitRole.Flying,
                    90f, 0f, 8f, 10f, 1f, 6f, RaceMarchSpeedRules.BaseMarchSpeed, 10),
                CreateUnit(GameIds.Units.HumanSuper, GameIds.Races.Human, UnitRole.Super,
                    500f, 2f, 30f, 40f, 0.5f, 8f, RaceMarchSpeedRules.BaseMarchSpeed, 50),
            };
        }

        /// <summary>PRE-006a enhanced defs (stats from Plans/PRE-006a-unit-bonuses.md).</summary>
        private static UnitDefinition[] CreateHumanBonusUnits()
        {
            return new[]
            {
                CreateUnit(GameIds.Units.HumanMeleeBonus, GameIds.Races.Human, UnitRole.Melee,
                    100f, 1f, 8f, 10f, 1f, 2f, 4f, 8),
                CreateUnit(GameIds.Units.HumanRangedBonus, GameIds.Races.Human, UnitRole.Ranged,
                    70f, 1f, 6f, 8f, 1f, 8f, RaceMarchSpeedRules.BaseMarchSpeed, 6),
                CreateUnit(GameIds.Units.HumanCasterBonus, GameIds.Races.Human, UnitRole.Caster,
                    80f, 0f, 4f, 5f, 1f, 6f, RaceMarchSpeedRules.BaseMarchSpeed, 7),
                CreateUnit(GameIds.Units.HumanSiegeBonus, GameIds.Races.Human, UnitRole.Siege,
                    250f, 0f, 12f, 16f, 1f, 1.5f, RaceMarchSpeedRules.BaseMarchSpeed, 15),
                CreateUnit(GameIds.Units.HumanFlyingBonus, GameIds.Races.Human, UnitRole.Flying,
                    100f, 0f, 8f, 10f, 1f, 6f, RaceMarchSpeedRules.BaseMarchSpeed, 10),
                CreateUnit(GameIds.Units.HumanSuperBonus, GameIds.Races.Human, UnitRole.Super,
                    500f, 2f, 30f, 40f, 0.5f, 10f, RaceMarchSpeedRules.BaseMarchSpeed, 50),
            };
        }

        private static HeroDefinition[] CreateHeroes()
        {
            var morale = new[] { "HERO_MORALE_SLOT_1", "HERO_MORALE_SLOT_2", "HERO_MORALE_SLOT_3" };
            var ids = new[] { GameIds.Heroes.Human1, GameIds.Heroes.Human2, GameIds.Heroes.Human3 };

            var heroes = new HeroDefinition[3];
            for (var i = 0; i < 3; i++)
            {
                heroes[i] = CreateHero(ids[i], GameIds.Races.Human, i + 1, morale[i]);
            }

            return heroes;
        }

        private static UnitDefinition CreateUnit(
            string id,
            string raceId,
            UnitRole role,
            float maxHp,
            float armor,
            float dmgMin,
            float dmgMax,
            float attackSpeed,
            float attackRange,
            float moveSpeed,
            int bounty)
        {
            var path = ContentAssetPaths.HumanUnitDefinitionPath(id);
            var unit = LoadOrCreate<UnitDefinition>(path);
            var so = new SerializedObject(unit);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_raceId").stringValue = raceId;
            so.FindProperty("_role").enumValueIndex = (int)role;
            so.FindProperty("_maxHp").floatValue = maxHp;
            so.FindProperty("_armor").floatValue = armor;
            so.FindProperty("_damageMin").floatValue = dmgMin;
            so.FindProperty("_damageMax").floatValue = dmgMax;
            so.FindProperty("_attackSpeed").floatValue = attackSpeed;
            so.FindProperty("_attackRange").floatValue = attackRange;
            so.FindProperty("_moveSpeed").floatValue = moveSpeed;
            so.FindProperty("_goldBounty").intValue = bounty;
            so.FindProperty("_maxMana").floatValue = role == UnitRole.Caster ? 200f : 0f;
            so.FindProperty("_marchSpeedOverride").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unit);
            return unit;
        }

        private static HeroDefinition CreateHero(string id, string raceId, int slot, string moraleId)
        {
            var path = $"{ContentAssetPaths.HumanHeroFolder(slot)}/{id}.asset";
            var hero = LoadOrCreate<HeroDefinition>(path);
            var so = new SerializedObject(hero);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_raceId").stringValue = raceId;
            so.FindProperty("_slot").intValue = slot;
            so.FindProperty("_idleMoraleId").stringValue = moraleId;
            so.FindProperty("_maxHp").floatValue = 600f;
            so.FindProperty("_armor").floatValue = 4f;
            so.FindProperty("_damageMin").floatValue = 35f;
            so.FindProperty("_damageMax").floatValue = 45f;
            so.FindProperty("_attackSpeed").floatValue = 1f;
            so.FindProperty("_attackRange").floatValue = slot == 3 ? 12f : 1.5f;
            so.FindProperty("_moveSpeed").floatValue = 4f;
            so.FindProperty("_goldBounty").intValue = 80;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hero);
            return hero;
        }

        private static RaceDefinition CreateRace(
            string id,
            string displayName,
            UnitDefinition[] units,
            UnitDefinition[] bonusUnits,
            HeroDefinition[] heroes,
            string[] positivePassives,
            string negativePassive)
        {
            var path = ContentAssetPaths.HumanRace;
            var race = LoadOrCreate<RaceDefinition>(path);
            var so = new SerializedObject(race);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_melee").objectReferenceValue = units[0];
            so.FindProperty("_ranged").objectReferenceValue = units[1];
            so.FindProperty("_caster").objectReferenceValue = units[2];
            so.FindProperty("_siege").objectReferenceValue = units[3];
            so.FindProperty("_flying").objectReferenceValue = units[4];
            so.FindProperty("_super").objectReferenceValue = units[5];
            if (bonusUnits != null && bonusUnits.Length >= 6)
            {
                so.FindProperty("_meleeBonus").objectReferenceValue = bonusUnits[0];
                so.FindProperty("_rangedBonus").objectReferenceValue = bonusUnits[1];
                so.FindProperty("_casterBonus").objectReferenceValue = bonusUnits[2];
                so.FindProperty("_siegeBonus").objectReferenceValue = bonusUnits[3];
                so.FindProperty("_flyingBonus").objectReferenceValue = bonusUnits[4];
                so.FindProperty("_superBonus").objectReferenceValue = bonusUnits[5];
            }

            so.FindProperty("_heroes").arraySize = heroes.Length;
            for (var i = 0; i < heroes.Length; i++)
            {
                so.FindProperty("_heroes").GetArrayElementAtIndex(i).objectReferenceValue = heroes[i];
            }

            so.FindProperty("_positivePassiveIds").arraySize = positivePassives.Length;
            for (var i = 0; i < positivePassives.Length; i++)
            {
                so.FindProperty("_positivePassiveIds").GetArrayElementAtIndex(i).stringValue = positivePassives[i];
            }

            so.FindProperty("_negativePassiveId").stringValue = negativePassive;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(race);
            return race;
        }

        private static SquadCompositionDefinition CreateSquad(
            int level,
            int melee,
            int ranged,
            int caster,
            int siege,
            int flying,
            int super)
        {
            var squadId = level switch
            {
                1 => GameIds.Squads.BarracksL1,
                2 => GameIds.Squads.BarracksL2,
                3 => GameIds.Squads.BarracksL3,
                _ => GameIds.Squads.BarracksL4,
            };

            var path = $"{ContentAssetPaths.SharedSquads}/{squadId}.asset";
            var squad = LoadOrCreate<SquadCompositionDefinition>(path);
            var so = new SerializedObject(squad);
            so.FindProperty("_barracksLevel").intValue = level;
            so.FindProperty("_meleeCount").intValue = melee;
            so.FindProperty("_rangedCount").intValue = ranged;
            so.FindProperty("_casterCount").intValue = caster;
            so.FindProperty("_siegeCount").intValue = siege;
            so.FindProperty("_flyingCount").intValue = flying;
            so.FindProperty("_superCount").intValue = super;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(squad);
            return squad;
        }

        private static StatUpgradeTrackDefinition CreateStatTrack(
            string id,
            float effectPerLevel,
            int[] costs,
            float[] times)
        {
            var path = $"{ContentAssetPaths.SharedUpgrades}/{id}.asset";
            var track = LoadOrCreate<StatUpgradeTrackDefinition>(path);
            var so = new SerializedObject(track);
            so.FindProperty("_id").stringValue = id;
            so.FindProperty("_effectPerLevel").floatValue = effectPerLevel;
            so.FindProperty("_maxLevel").intValue = 9;
            SetIntArray(so.FindProperty("_costsGold"), costs);
            SetFloatArray(so.FindProperty("_researchTimeSec"), times);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(track);
            return track;
        }

        private static void CreateOrUpdateCatalog(
            RaceDefinition human,
            SquadCompositionDefinition[] squads,
            StatUpgradeTrackDefinition[] statTracks)
        {
            var catalog = LoadOrCreate<RaceCatalog>(CatalogPath);
            var so = new SerializedObject(catalog);
            var races = new List<RaceDefinition> { human };
            if (catalog.Races != null)
            {
                foreach (var existing in catalog.Races)
                {
                    if (existing != null && existing.Id != human.Id)
                    {
                        races.Add(existing);
                    }
                }
            }

            var racesProperty = so.FindProperty("_races");
            racesProperty.arraySize = races.Count;
            for (var i = 0; i < races.Count; i++)
            {
                racesProperty.GetArrayElementAtIndex(i).objectReferenceValue = races[i];
            }
            so.FindProperty("_squadCompositions").arraySize = squads.Length;
            for (var i = 0; i < squads.Length; i++)
            {
                so.FindProperty("_squadCompositions").GetArrayElementAtIndex(i).objectReferenceValue = squads[i];
            }

            so.FindProperty("_statTracks").arraySize = statTracks.Length;
            for (var i = 0; i < statTracks.Length; i++)
            {
                so.FindProperty("_statTracks").GetArrayElementAtIndex(i).objectReferenceValue = statTracks[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetIntArray(SerializedProperty property, int[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).intValue = values[i];
            }
        }

        private static void SetFloatArray(SerializedProperty property, float[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).floatValue = values[i];
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
            {
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
