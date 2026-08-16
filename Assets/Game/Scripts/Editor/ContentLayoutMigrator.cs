using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-shot (idempotent) migration to the race → role → Bonus layout for
    /// ScriptableObjects, Prefabs, and UnitPortraits.
    /// </summary>
    public static class ContentLayoutMigrator
    {
        [MenuItem("BARAKI/Content/Migrate To Role Folders")]
        public static void MigrateFromMenu()
        {
            var log = MigrateAll();
            Debug.Log("ContentLayoutMigrator:\n" + log);
        }

        public static string MigrateAll()
        {
            var sb = new StringBuilder();
            ContentAssetPaths.EnsureHumanUnitFolders();
            UnitVisualPrefabBuilder.EnsureHumanPrefabFolders();
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitUnits);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitHeroes);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitBonus);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanHero1);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanHero2);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanHero3);

            foreach (var (from, to) in ScriptableObjectMoves())
            {
                sb.AppendLine(Move(from, to));
            }

            foreach (var (from, to) in PrefabMoves())
            {
                sb.AppendLine(Move(from, to));
            }

            foreach (var (from, to) in PortraitMoves())
            {
                sb.AppendLine(Move(from, to));
            }

            // Drop empty legacy controller folders if vacant.
            TryDeleteEmptyFolder("Assets/Game/Prefabs/Races/Humans/Units/Controllers", sb);
            TryDeleteEmptyFolder("Assets/Game/Prefabs/Races/Humans/Heroes/Controllers", sb);
            // Legacy Siege/Abilities (pre-Bonus) if empty after move.
            TryDeleteEmptyFolder("Assets/Game/ScriptableObjects/Races/Humans/Units/Siege/Abilities", sb);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return sb.ToString();
        }

        static IEnumerable<(string from, string to)> ScriptableObjectMoves()
        {
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_MELEE.asset",
                ContentAssetPaths.HumanMelee + "/UNIT_HUMAN_MELEE.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_RANGED.asset",
                ContentAssetPaths.HumanRanged + "/UNIT_HUMAN_RANGED.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_SIEGE.asset",
                ContentAssetPaths.HumanSiege + "/UNIT_HUMAN_SIEGE.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_FLYING.asset",
                ContentAssetPaths.HumanFlying + "/UNIT_HUMAN_FLYING.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_SUPER.asset",
                ContentAssetPaths.HumanSuper + "/UNIT_HUMAN_SUPER.asset");

            // Caster base already in Caster/; only bonus moves into Bonus/.
            yield return (
                ContentAssetPaths.HumanCaster + "/UNIT_HUMAN_CASTER_BONUS.asset",
                ContentAssetPaths.HumanCaster + "/Bonus/UNIT_HUMAN_CASTER_BONUS.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_CASTER_BONUS.asset",
                ContentAssetPaths.HumanCaster + "/Bonus/UNIT_HUMAN_CASTER_BONUS.asset");

            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_MELEE_BONUS.asset",
                ContentAssetPaths.HumanMelee + "/Bonus/UNIT_HUMAN_MELEE_BONUS.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_RANGED_BONUS.asset",
                ContentAssetPaths.HumanRanged + "/Bonus/UNIT_HUMAN_RANGED_BONUS.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_SIEGE_BONUS.asset",
                ContentAssetPaths.HumanSiegeBonus + "/UNIT_HUMAN_SIEGE_BONUS.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_FLYING_BONUS.asset",
                ContentAssetPaths.HumanFlying + "/Bonus/UNIT_HUMAN_FLYING_BONUS.asset");
            yield return (
                ContentAssetPaths.HumanUnits + "/UNIT_HUMAN_SUPER_BONUS.asset",
                ContentAssetPaths.HumanSuper + "/Bonus/UNIT_HUMAN_SUPER_BONUS.asset");

            // Siege regen aura: Units/Siege/Abilities → Units/Siege/Bonus/Abilities
            yield return (
                ContentAssetPaths.HumanUnits + "/Siege/Abilities/siege-regen-aura.asset",
                ContentAssetPaths.HumanSiegeAbilities + "/siege-regen-aura.asset");
        }

        static IEnumerable<(string from, string to)> PrefabMoves()
        {
            var units = UnitVisualPrefabBuilder.HumanPath;
            var heroes = UnitVisualPrefabBuilder.HumanHeroesPath;

            yield return (units + "/Human_Melee.prefab", UnitVisualPrefabBuilder.HumanMeleePath);
            yield return (units + "/Human_Ranged.prefab", UnitVisualPrefabBuilder.HumanRangedPath);
            yield return (units + "/Human_Caster.prefab", UnitVisualPrefabBuilder.HumanCasterPath);
            yield return (units + "/Human_Siege.prefab", UnitVisualPrefabBuilder.HumanSiegePath);
            yield return (units + "/Human_Flying.prefab", UnitVisualPrefabBuilder.HumanFlyingPath);
            yield return (units + "/Human_Super.prefab", UnitVisualPrefabBuilder.HumanSuperPath);
            yield return (units + "/Human_Titan.prefab", UnitVisualPrefabBuilder.HumanTitanPath);

            yield return (units + "/Human_Melee_BONUS.prefab", UnitVisualPrefabBuilder.HumanMeleeBonusPath);
            yield return (units + "/Human_Ranged_BONUS.prefab", UnitVisualPrefabBuilder.HumanRangedBonusPath);
            yield return (units + "/Human_Caster_BONUS.prefab", UnitVisualPrefabBuilder.HumanCasterBonusPath);
            yield return (units + "/Human_Siege_BONUS.prefab", UnitVisualPrefabBuilder.HumanSiegeBonusPath);
            yield return (units + "/Human_Flying_BONUS.prefab", UnitVisualPrefabBuilder.HumanFlyingBonusPath);
            yield return (units + "/Human_Super_BONUS.prefab", UnitVisualPrefabBuilder.HumanSuperBonusPath);

            yield return (heroes + "/Human_Hero1.prefab", UnitVisualPrefabBuilder.HumanHero1Path);
            yield return (heroes + "/Human_Hero2.prefab", UnitVisualPrefabBuilder.HumanHero2Path);
            yield return (heroes + "/Human_Hero3.prefab", UnitVisualPrefabBuilder.HumanHero3Path);

            // Controllers: shared folders → beside prefab
            yield return (units + "/Controllers/Human_Melee.controller",
                Beside(UnitVisualPrefabBuilder.HumanMeleePath, "Human_Melee.controller"));
            yield return (units + "/Controllers/Human_Ranged.controller",
                Beside(UnitVisualPrefabBuilder.HumanRangedPath, "Human_Ranged.controller"));
            yield return (units + "/Controllers/Human_Caster.controller",
                Beside(UnitVisualPrefabBuilder.HumanCasterPath, "Human_Caster.controller"));
            yield return (units + "/Controllers/Human_Siege.controller",
                Beside(UnitVisualPrefabBuilder.HumanSiegePath, "Human_Siege.controller"));
            yield return (units + "/Controllers/Human_Flying.controller",
                Beside(UnitVisualPrefabBuilder.HumanFlyingPath, "Human_Flying.controller"));
            yield return (units + "/Controllers/Human_Super.controller",
                Beside(UnitVisualPrefabBuilder.HumanSuperPath, "Human_Super.controller"));
            yield return (units + "/Controllers/Human_Titan.controller",
                Beside(UnitVisualPrefabBuilder.HumanTitanPath, "Human_Titan.controller"));

            yield return (units + "/Controllers/Human_Melee_BONUS.controller",
                Beside(UnitVisualPrefabBuilder.HumanMeleeBonusPath, "Human_Melee_BONUS.controller"));
            yield return (units + "/Controllers/Human_Ranged_BONUS.controller",
                Beside(UnitVisualPrefabBuilder.HumanRangedBonusPath, "Human_Ranged_BONUS.controller"));
            yield return (units + "/Controllers/Human_Caster_BONUS.controller",
                Beside(UnitVisualPrefabBuilder.HumanCasterBonusPath, "Human_Caster_BONUS.controller"));
            yield return (units + "/Controllers/Human_Siege_BONUS.controller",
                Beside(UnitVisualPrefabBuilder.HumanSiegeBonusPath, "Human_Siege_BONUS.controller"));
            yield return (units + "/Controllers/Human_Flying_BONUS.controller",
                Beside(UnitVisualPrefabBuilder.HumanFlyingBonusPath, "Human_Flying_BONUS.controller"));
            yield return (units + "/Controllers/Human_Super_BONUS.controller",
                Beside(UnitVisualPrefabBuilder.HumanSuperBonusPath, "Human_Super_BONUS.controller"));

            yield return (heroes + "/Controllers/Human_Hero1.controller",
                Beside(UnitVisualPrefabBuilder.HumanHero1Path, "Human_Hero1.controller"));
            yield return (heroes + "/Controllers/Human_Hero2.controller",
                Beside(UnitVisualPrefabBuilder.HumanHero2Path, "Human_Hero2.controller"));
            yield return (heroes + "/Controllers/Human_Hero3.controller",
                Beside(UnitVisualPrefabBuilder.HumanHero3Path, "Human_Hero3.controller"));
        }

        static IEnumerable<(string from, string to)> PortraitMoves()
        {
            var root = ContentAssetPaths.PortraitRoot;
            var units = ContentAssetPaths.HumanPortraitUnits;
            var heroes = ContentAssetPaths.HumanPortraitHeroes;
            var bonus = ContentAssetPaths.HumanPortraitBonus;

            yield return (root + "/Human_Melee.png", units + "/Melee.png");
            yield return (root + "/Human_Ranged.png", units + "/Ranged.png");
            yield return (root + "/Human_Caster.png", units + "/Caster.png");
            yield return (root + "/Human_Siege.png", units + "/Siege.png");
            yield return (root + "/Human_Flying.png", units + "/Flying.png");
            yield return (root + "/Human_Super.png", units + "/Super.png");
            yield return (root + "/Human_Titan.png", units + "/Titan.png");

            yield return (root + "/Human_Hero1.png", heroes + "/Hero1.png");
            yield return (root + "/Human_Hero2.png", heroes + "/Hero2.png");
            yield return (root + "/Human_Hero3.png", heroes + "/Hero3.png");

            yield return (root + "/Human_Melee_BONUS.png", bonus + "/Melee.png");
            yield return (root + "/Human_Ranged_BONUS.png", bonus + "/Ranged.png");
            yield return (root + "/Human_Caster_BONUS.png", bonus + "/Caster.png");
            yield return (root + "/Human_Siege_BONUS.png", bonus + "/Siege.png");
            yield return (root + "/Human_Flying_BONUS.png", bonus + "/Flying.png");
            yield return (root + "/Human_Super_BONUS.png", bonus + "/Super.png");
        }

        static string Beside(string prefabPath, string fileName)
        {
            var dir = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/') ?? prefabPath;
            return $"{dir}/{fileName}";
        }

        static string Move(string from, string to)
        {
            if (from == to)
            {
                return $"skip same: {from}";
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(to) != null)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(from) == null)
                {
                    return $"already: {to}";
                }

                return $"target exists, keep: {to} (left {from})";
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(from) == null)
            {
                return $"missing: {from}";
            }

            var dir = Path.GetDirectoryName(to)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir))
            {
                ContentAssetPaths.EnsureFolder(dir);
            }

            var error = AssetDatabase.MoveAsset(from, to);
            return string.IsNullOrEmpty(error) ? $"moved: {from} → {to}" : $"FAIL {from}: {error}";
        }

        static void TryDeleteEmptyFolder(string path, StringBuilder sb)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { path });
            if (guids.Length > 0)
            {
                sb.AppendLine($"keep non-empty: {path}");
                return;
            }

            if (AssetDatabase.DeleteAsset(path))
            {
                sb.AppendLine($"deleted empty: {path}");
            }
        }
    }
}
