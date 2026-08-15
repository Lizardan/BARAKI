using System.IO;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>Canonical editor paths for ScriptableObject game content.</summary>
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
        public const string HumanBaseUnits = HumanUnits + "/Base";
        public const string HumanHeroes = Humans + "/Heroes";
        public const string HumanBaseHeroes = HumanHeroes + "/Base";
        public const string HumanAbilities = Humans + "/Abilities";
        public const string HumanHeroAbilities = HumanAbilities + "/Heroes";
        public const string HumanHero1Abilities = HumanHeroAbilities + "/Hero1";
        public const string HumanHero2Abilities = HumanHeroAbilities + "/Hero2";
        public const string HumanHero3Abilities = HumanHeroAbilities + "/Hero3";
        public const string HumanCasterAbilities = HumanAbilities + "/Caster";
        public const string HumanTitanAbilities = HumanAbilities + "/Titan";
        public const string HumanEnhancedUnitAbilities = HumanAbilities + "/EnhancedUnits";
        public const string HumanEnhancedHeroAbilities = HumanAbilities + "/EnhancedHeroes";
        public const string HumanEnhancedHero1Abilities = HumanEnhancedHeroAbilities + "/Hero1";
        public const string HumanEnhancedHero2Abilities = HumanEnhancedHeroAbilities + "/Hero2";
        public const string HumanEnhancedHero3Abilities = HumanEnhancedHeroAbilities + "/Hero3";

        public const string HumanEnhancedUnits = HumanUnits + "/Enhanced";
        public const string HumanEnhancedHeroes = HumanHeroes + "/Enhanced";
        public const string HumanEnhancedHero1 = HumanEnhancedHeroes + "/Hero1";
        public const string HumanEnhancedHero2 = HumanEnhancedHeroes + "/Hero2";
        public const string HumanEnhancedHero3 = HumanEnhancedHeroes + "/Hero3";
        public const string HumanBonuses = Humans + "/Bonuses";
        public const string HumanReplacementBonuses = HumanBonuses + "/Replacements";
        public const string HumanUniqueBonuses = HumanBonuses + "/Unique";
        public const string HumanBuildings = Humans + "/Buildings";
        public const string HumanPassives = Humans + "/Passives";
        public const string HumanTech = Humans + "/Tech";
        public const string HumanAi = Humans + "/AI";

        public const string Shared = Root + "/Shared";
        public const string SharedSquads = Shared + "/Squads";
        public const string SharedUpgrades = Shared + "/Upgrades";

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
