using System.IO;
using Game.Core;
using Game.Gameplay.Data;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Canonical editor paths for ScriptableObject game content.
    /// Layout is race → category (<c>Units</c> / <c>BonusUnits</c> / <c>Heroes</c> / <c>BonusHeroes</c>)
    /// → role folder → optional <c>Abilities/</c>. Titan lives with heroes;
    /// veteran champions live in <c>BonusHeroes/</c> (PRE-006b).
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

        public const string HumanBonusHero1 = HumanBonusHeroes + "/Hero1";
        public const string HumanBonusHero2 = HumanBonusHeroes + "/Hero2";
        public const string HumanBonusHero3 = HumanBonusHeroes + "/Hero3";
        public const string HumanBonusTitan = HumanBonusHeroes + "/Titan";
        public const string HumanBonusHero1Abilities = HumanBonusHero1 + "/Abilities";
        public const string HumanBonusHero2Abilities = HumanBonusHero2 + "/Abilities";
        public const string HumanBonusHero3Abilities = HumanBonusHero3 + "/Abilities";
        public const string HumanBonusTitanAbilities = HumanBonusTitan + "/Abilities";

        public const string Faceless = Races + "/Faceless";
        public const string FacelessRace = Faceless + "/RACE_FACELESS.asset";

        public const string FacelessUnits = Faceless + "/Units";
        public const string FacelessMelee = FacelessUnits + "/Melee";
        public const string FacelessRanged = FacelessUnits + "/Ranged";
        public const string FacelessCaster = FacelessUnits + "/Caster";
        public const string FacelessSiege = FacelessUnits + "/Siege";
        public const string FacelessFlying = FacelessUnits + "/Flying";
        public const string FacelessSuper = FacelessUnits + "/Super";

        public const string FacelessHeroes = Faceless + "/Heroes";
        public const string FacelessTitan = FacelessHeroes + "/Titan";
        public const string FacelessHero1 = FacelessHeroes + "/Hero1";
        public const string FacelessHero2 = FacelessHeroes + "/Hero2";
        public const string FacelessHero3 = FacelessHeroes + "/Hero3";

        public const string FacelessCasterAbilities = FacelessCaster + "/Abilities";
        public const string FacelessHero1Abilities = FacelessHero1 + "/Abilities";
        public const string FacelessHero2Abilities = FacelessHero2 + "/Abilities";
        public const string FacelessHero3Abilities = FacelessHero3 + "/Abilities";
        public const string FacelessTitanAbilities = FacelessTitan + "/Abilities";

        public const string FacelessBonusUnits = Faceless + "/BonusUnits";
        public const string FacelessMeleeBonus = FacelessBonusUnits + "/Melee";
        public const string FacelessRangedBonus = FacelessBonusUnits + "/Ranged";
        public const string FacelessCasterBonus = FacelessBonusUnits + "/Caster";
        public const string FacelessSiegeBonus = FacelessBonusUnits + "/Siege";
        public const string FacelessFlyingBonus = FacelessBonusUnits + "/Flying";
        public const string FacelessSuperBonus = FacelessBonusUnits + "/Super";
        public const string FacelessMeleeBonusAbilities = FacelessMeleeBonus + "/Abilities";
        public const string FacelessRangedBonusAbilities = FacelessRangedBonus + "/Abilities";
        public const string FacelessCasterBonusAbilities = FacelessCasterBonus + "/Abilities";
        public const string FacelessSiegeBonusAbilities = FacelessSiegeBonus + "/Abilities";
        public const string FacelessFlyingBonusAbilities = FacelessFlyingBonus + "/Abilities";
        public const string FacelessSuperBonusAbilities = FacelessSuperBonus + "/Abilities";

        public const string Shared = Root + "/Shared";
        public const string SharedSquads = Shared + "/Squads";
        public const string SharedUpgrades = Shared + "/Upgrades";

        public const string PortraitRoot = "Assets/Game/Art/UI/UnitPortraits";
        public const string HumanPortraits = PortraitRoot + "/Humans";
        public const string HumanPortraitUnits = HumanPortraits + "/Units";
        public const string HumanPortraitHeroes = HumanPortraits + "/Heroes";
        public const string HumanPortraitBonusUnits = HumanPortraits + "/BonusUnits";
        public const string HumanPortraitBonusHeroes = HumanPortraits + "/BonusHeroes";

        public const string FacelessPortraits = PortraitRoot + "/Faceless";
        public const string FacelessPortraitUnits = FacelessPortraits + "/Units";
        public const string FacelessPortraitHeroes = FacelessPortraits + "/Heroes";
        public const string FacelessPortraitBonusUnits = FacelessPortraits + "/BonusUnits";
        public const string FacelessPortraitBonusHeroes = FacelessPortraits + "/BonusHeroes";

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

        /// <summary>Race-agnostic variant — derives race from the unit id prefix.</summary>
        public static string UnitDefinitionPath(string unitId)
        {
            var raceId = unitId.StartsWith("UNIT_FACELESS_") ? GameIds.Races.Faceless : GameIds.Races.Human;
            var role = RoleFromUnitId(unitId);
            var folder = unitId.EndsWith("_BONUS")
                ? HumanUnitBonusFolder(role)
                : RaceUnitRoleFolder(raceId, role);
            return $"{folder}/{unitId}.asset";
        }

        public static string RaceFolder(string raceId) =>
            raceId == GameIds.Races.Faceless ? Faceless : Humans;

        public static string RaceDefinitionPath(string raceId) =>
            raceId == GameIds.Races.Faceless ? FacelessRace : HumanRace;

        public static string RaceUnitRoleFolder(string raceId, UnitRole role)
        {
            if (raceId == GameIds.Races.Faceless)
            {
                return role switch
                {
                    UnitRole.Melee => FacelessMelee,
                    UnitRole.Ranged => FacelessRanged,
                    UnitRole.Caster => FacelessCaster,
                    UnitRole.Siege => FacelessSiege,
                    UnitRole.Flying => FacelessFlying,
                    UnitRole.Super => FacelessSuper,
                    UnitRole.Titan => FacelessTitan,
                    _ => FacelessUnits,
                };
            }

            return HumanUnitRoleFolder(role);
        }

        public static string RaceHeroFolder(string raceId, int slot)
        {
            if (raceId == GameIds.Races.Faceless)
            {
                return slot switch
                {
                    1 => FacelessHero1,
                    2 => FacelessHero2,
                    3 => FacelessHero3,
                    _ => FacelessHeroes,
                };
            }

            return HumanHeroFolder(slot);
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

        /// <summary>Veteran champion folders (bonus slots 7–10) + their ability folders.</summary>
        public static void EnsureHumanBonusHeroFolders()
        {
            EnsureFolder(HumanBonusHeroes);
            EnsureFolder(HumanBonusHero1);
            EnsureFolder(HumanBonusHero2);
            EnsureFolder(HumanBonusHero3);
            EnsureFolder(HumanBonusTitan);
            EnsureFolder(HumanBonusHero1Abilities);
            EnsureFolder(HumanBonusHero2Abilities);
            EnsureFolder(HumanBonusHero3Abilities);
            EnsureFolder(HumanBonusTitanAbilities);
            EnsureFolder(HumanPortraitBonusHeroes);
        }

        /// <summary>
        /// Faceless unit/hero/bonus-unit folders. Only folders that will hold real assets
        /// are created.
        /// </summary>
        public static void EnsureFacelessUnitFolders()
        {
            EnsureFolder(Faceless);
            EnsureFolder(FacelessUnits);
            EnsureFolder(FacelessMelee);
            EnsureFolder(FacelessRanged);
            EnsureFolder(FacelessCaster);
            EnsureFolder(FacelessSiege);
            EnsureFolder(FacelessFlying);
            EnsureFolder(FacelessSuper);
            EnsureFolder(FacelessHeroes);
            EnsureFolder(FacelessTitan);
            EnsureFolder(FacelessHero1);
            EnsureFolder(FacelessHero2);
            EnsureFolder(FacelessHero3);
            EnsureFolder(FacelessCasterAbilities);
            EnsureFolder(FacelessHero1Abilities);
            EnsureFolder(FacelessHero2Abilities);
            EnsureFolder(FacelessHero3Abilities);
            EnsureFolder(FacelessTitanAbilities);
            EnsureFolder(FacelessBonusUnits);
            EnsureFolder(FacelessMeleeBonus);
            EnsureFolder(FacelessRangedBonus);
            EnsureFolder(FacelessCasterBonus);
            EnsureFolder(FacelessSiegeBonus);
            EnsureFolder(FacelessFlyingBonus);
            EnsureFolder(FacelessSuperBonus);
            EnsureFolder(FacelessMeleeBonusAbilities);
            EnsureFolder(FacelessRangedBonusAbilities);
            EnsureFolder(FacelessCasterBonusAbilities);
            EnsureFolder(FacelessSiegeBonusAbilities);
            EnsureFolder(FacelessFlyingBonusAbilities);
            EnsureFolder(FacelessSuperBonusAbilities);
            EnsureFolder(FacelessPortraits);
            EnsureFolder(FacelessPortraitUnits);
            EnsureFolder(FacelessPortraitHeroes);
            EnsureFolder(FacelessPortraitBonusUnits);
            EnsureFolder(FacelessPortraitBonusHeroes);
        }
    }
}
