using System.IO;
using Game.Gameplay.Data;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Canonical editor paths for ScriptableObject game content.
    /// Layout is race → category (<c>Units</c> / <c>BonusUnits</c> / <c>Heroes</c>) → role folder
    /// → optional <c>Abilities/</c>. Titan lives with heroes. <c>BonusHeroes/</c> is reserved
    /// (do not create the empty folder until those assets exist).
    /// </summary>
    public static class ContentAssetPaths
    {
        public const string Root = "Assets/Game/ScriptableObjects";

        public const string Catalogs = Root + "/Catalogs";
        public const string RaceCatalog = Catalogs + "/RaceCatalog.asset";
        public const string UnitVisualCatalog = Catalogs + "/UnitVisualCatalog.asset";
        public const string UnitAbilityCatalog = Catalogs + "/UnitAbilityCatalog.asset";
        public const string MainExtraAbilityFxCatalog = Game.Gameplay.Combat.MainExtraAbilityFxCatalog.AssetPath;
        public const string CustomAbilityFxPrefabs = "Assets/Game/Prefabs/Fx/Custom";
        public const string SkyBeamPrefab = CustomAbilityFxPrefabs + "/SkyBeam.prefab";

        public const string Races = Root + "/Races";
        public const string Humans = Races + "/Humans";
        public const string HumanRace = Humans + "/RACE_HUMAN.asset";

        public const string HumanUnits = Humans + "/Units";
        public const string HumanBonusUnits = Humans + "/BonusUnits";
        public const string HumanHeroes = Humans + "/Heroes";
        public const string HumanBonusHeroes = Humans + "/BonusHeroes";

        public const string HumanMelee = HumanUnits + "/Melee";
        public const string HumanRanged = HumanUnits + "/Ranged";
        public const string HumanCaster = HumanUnits + "/Caster";
        public const string HumanSiege = HumanUnits + "/Siege";
        public const string HumanFlying = HumanUnits + "/Flying";
        public const string HumanSuper = HumanUnits + "/Super";
        public const string HumanTitan = HumanHeroes + "/Titan";

        public const string HumanMeleeBonus = HumanBonusUnits + "/Melee";
        public const string HumanRangedBonus = HumanBonusUnits + "/Ranged";
        public const string HumanCasterBonus = HumanBonusUnits + "/Caster";
        public const string HumanSiegeBonus = HumanBonusUnits + "/Siege";
        public const string HumanFlyingBonus = HumanBonusUnits + "/Flying";
        public const string HumanSuperBonus = HumanBonusUnits + "/Super";

        public const string HumanCasterAbilities = HumanCaster + "/Abilities";
        public const string HumanTitanAbilities = HumanTitan + "/Abilities";
        public const string HumanMeleeAbilities = HumanMeleeBonus + "/Abilities";
        public const string HumanRangedAbilities = HumanRangedBonus + "/Abilities";
        public const string HumanCasterBonusAbilities = HumanCasterBonus + "/Abilities";
        public const string HumanSiegeAbilities = HumanSiegeBonus + "/Abilities";
        public const string HumanFlyingAbilities = HumanFlyingBonus + "/Abilities";
        public const string HumanSuperAbilities = HumanSuperBonus + "/Abilities";

        public const string HumanHero1 = HumanHeroes + "/Hero1";
        public const string HumanHero2 = HumanHeroes + "/Hero2";
        public const string HumanHero3 = HumanHeroes + "/Hero3";
        public const string HumanHero1Abilities = HumanHero1 + "/Abilities";
        public const string HumanHero2Abilities = HumanHero2 + "/Abilities";
        public const string HumanHero3Abilities = HumanHero3 + "/Abilities";

        public const string Shared = Root + "/Shared";
        public const string SharedSquads = Shared + "/Squads";
        public const string SharedUpgrades = Shared + "/Upgrades";

        public const string PortraitRoot = "Assets/Game/Art/UI/UnitPortraits";
        public const string HumanPortraits = PortraitRoot + "/Humans";
        public const string HumanPortraitUnits = HumanPortraits + "/Units";
        public const string HumanPortraitHeroes = HumanPortraits + "/Heroes";
        public const string HumanPortraitBonusUnits = HumanPortraits + "/BonusUnits";

        public static string HumanHeroFolder(int slot) =>
            slot switch
            {
                1 => HumanHero1,
                2 => HumanHero2,
                3 => HumanHero3,
                _ => HumanHeroes,
            };

        public static string HumanHeroAbilitiesFolder(int slot) =>
            slot switch
            {
                1 => HumanHero1Abilities,
                2 => HumanHero2Abilities,
                3 => HumanHero3Abilities,
                _ => HumanHeroes,
            };

        public static string HumanUnitRoleFolder(UnitRole role) => role switch
        {
            UnitRole.Melee => HumanMelee,
            UnitRole.Ranged => HumanRanged,
            UnitRole.Caster => HumanCaster,
            UnitRole.Siege => HumanSiege,
            UnitRole.Flying => HumanFlying,
            UnitRole.Super => HumanSuper,
            UnitRole.Titan => HumanTitan,
            _ => HumanUnits,
        };

        public static string HumanUnitBonusFolder(UnitRole role) => role switch
        {
            UnitRole.Melee => HumanMeleeBonus,
            UnitRole.Ranged => HumanRangedBonus,
            UnitRole.Caster => HumanCasterBonus,
            UnitRole.Siege => HumanSiegeBonus,
            UnitRole.Flying => HumanFlyingBonus,
            UnitRole.Super => HumanSuperBonus,
            UnitRole.Titan => HumanBonusHeroes + "/Titan",
            _ => HumanBonusUnits,
        };

        public static string HumanUnitDefinitionPath(string unitId)
        {
            var role = RoleFromUnitId(unitId);
            var isBonus = unitId.EndsWith("_BONUS");
            var folder = isBonus ? HumanUnitBonusFolder(role) : HumanUnitRoleFolder(role);
            return $"{folder}/{unitId}.asset";
        }

        static UnitRole RoleFromUnitId(string unitId)
        {
            if (unitId.Contains("TITAN")) return UnitRole.Titan;
            if (unitId.Contains("MELEE")) return UnitRole.Melee;
            if (unitId.Contains("RANGED")) return UnitRole.Ranged;
            if (unitId.Contains("CASTER")) return UnitRole.Caster;
            if (unitId.Contains("SIEGE")) return UnitRole.Siege;
            if (unitId.Contains("FLYING")) return UnitRole.Flying;
            if (unitId.Contains("SUPER")) return UnitRole.Super;
            return UnitRole.Melee;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }

        public static void EnsureHumanUnitFolders()
        {
            EnsureFolder(HumanUnits);
            EnsureFolder(HumanBonusUnits);
            EnsureFolder(HumanHeroes);
            EnsureFolder(HumanMelee);
            EnsureFolder(HumanRanged);
            EnsureFolder(HumanCaster);
            EnsureFolder(HumanSiege);
            EnsureFolder(HumanFlying);
            EnsureFolder(HumanSuper);
            EnsureFolder(HumanMeleeBonus);
            EnsureFolder(HumanRangedBonus);
            EnsureFolder(HumanCasterBonus);
            EnsureFolder(HumanSiegeBonus);
            EnsureFolder(HumanFlyingBonus);
            EnsureFolder(HumanSuperBonus);
            EnsureFolder(HumanMeleeAbilities);
            EnsureFolder(HumanRangedAbilities);
            EnsureFolder(HumanCasterBonusAbilities);
            EnsureFolder(HumanSiegeAbilities);
            EnsureFolder(HumanFlyingAbilities);
            EnsureFolder(HumanSuperAbilities);
            EnsureFolder(HumanCasterAbilities);
            EnsureFolder(HumanTitan);
            EnsureFolder(HumanTitanAbilities);
        }
    }
}
