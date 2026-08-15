using System.IO;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Canonical editor paths for ScriptableObject game content.
    /// Layout is owner-first under each race: open Hero1 / Caster / Titan and see that kit at a glance.
    /// </summary>
    public static class ContentAssetPaths
    {
        public const string Root = "Assets/Game/ScriptableObjects";

        public const string Catalogs = Root + "/Catalogs";
        public const string RaceCatalog = Catalogs + "/RaceCatalog.asset";
        public const string UnitVisualCatalog = Catalogs + "/UnitVisualCatalog.asset";
        public const string UnitAbilityCatalog = Catalogs + "/UnitAbilityCatalog.asset";

        public const string Races = Root + "/Races";
        public const string Humans = Races + "/Humans";
        public const string HumanRace = Humans + "/RACE_HUMAN.asset";

        public const string HumanUnits = Humans + "/Units";
        public const string HumanCaster = HumanUnits + "/Caster";
        public const string HumanCasterAbilities = HumanCaster + "/Abilities";
        public const string HumanTitan = HumanUnits + "/Titan";
        public const string HumanTitanAbilities = HumanTitan + "/Abilities";

        public const string HumanHeroes = Humans + "/Heroes";
        public const string HumanHero1 = HumanHeroes + "/Hero1";
        public const string HumanHero2 = HumanHeroes + "/Hero2";
        public const string HumanHero3 = HumanHeroes + "/Hero3";
        public const string HumanHero1Abilities = HumanHero1 + "/Abilities";
        public const string HumanHero2Abilities = HumanHero2 + "/Abilities";
        public const string HumanHero3Abilities = HumanHero3 + "/Abilities";

        public const string Shared = Root + "/Shared";
        public const string SharedSquads = Shared + "/Squads";
        public const string SharedUpgrades = Shared + "/Upgrades";

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

        public static string HumanUnitDefinitionPath(string unitId) =>
            unitId == Game.Core.GameIds.Units.HumanCaster
                ? $"{HumanCaster}/{unitId}.asset"
                : $"{HumanUnits}/{unitId}.asset";

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
    }
}
