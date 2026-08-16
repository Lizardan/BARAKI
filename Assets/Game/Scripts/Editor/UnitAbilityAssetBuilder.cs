using System.Collections.Generic;
using System.Text;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds one <see cref="UnitAbilityDef"/> asset per ability (named from <see cref="UnitAbilityDef.DisplayName"/>,
    /// with the behaviour as a sub-asset), grouped by owner, plus the global <see cref="UnitAbilityCatalog"/>.
    /// Existing def assets are reused by <see cref="UnitAbilityDef.AbilityId"/> so prefab references survive.
    /// Legacy <c>Ability{id}.asset</c> names are migrated to display names on the next build.
    /// </summary>
    public static class UnitAbilityAssetBuilder
    {
        public const string CatalogPath = ContentAssetPaths.UnitAbilityCatalog;

        [MenuItem("BARAKI/Abilities/Build Ability Defs")]
        public static void BuildAll()
        {
            EnsureFolder();

            var unique = new Dictionary<int, UnitAbilityDef>();
            foreach (var kit in CollectKits())
            {
                foreach (var defaults in kit)
                {
                    if (defaults != null)
                    {
                        unique.TryAdd(defaults.AbilityId, defaults);
                    }
                }
            }

            var sorted = new List<UnitAbilityDef>(unique.Values);
            sorted.Sort((a, b) => a.AbilityId.CompareTo(b.AbilityId));

            var usedNames = new HashSet<string>();
            var allDefs = new List<UnitAbilityDef>();
            foreach (var defaults in sorted)
            {
                var name = DisplayNameToFileName(defaults.DisplayName);
                if (!usedNames.Add(name))
                {
                    name = $"{name}-{defaults.AbilityId}";
                    usedNames.Add(name);
                }

                allDefs.Add(EnsureDefAsset(defaults, name));
            }

            allDefs.Sort((a, b) => a.AbilityId.CompareTo(b.AbilityId));

            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UnitAbilityCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var knownIds = new HashSet<int>();
            foreach (var def in allDefs)
            {
                knownIds.Add(def.AbilityId);
            }

            foreach (var existing in catalog.Abilities)
            {
                if (existing == null || knownIds.Contains(existing.AbilityId))
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(existing);
                if (!path.StartsWith(
                        ContentAssetPaths.Humans + "/",
                        System.StringComparison.Ordinal))
                {
                    allDefs.Add(existing);
                    knownIds.Add(existing.AbilityId);
                }
            }

            allDefs.Sort((a, b) => a.AbilityId.CompareTo(b.AbilityId));
            catalog.ReplaceAbilities(allDefs.ToArray());
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"UnitAbilityAssetBuilder: {allDefs.Count} ability def(s) + catalog at {CatalogPath}.");
        }

        public static IEnumerable<UnitAbilityDef[]> CollectKits()
        {
            yield return AbilityKitDefaults.CreateKing();
            yield return AbilityKitDefaults.CreatePaladin();
            yield return AbilityKitDefaults.CreatePriest();
            yield return AbilityKitDefaults.CreateTitan();
            yield return AbilityKitDefaults.CreateCaster();
            yield return AbilityKitDefaults.CreateMeleeBonus();
            yield return AbilityKitDefaults.CreateRangedBonus();
            yield return AbilityKitDefaults.CreateCasterBonus();
            yield return AbilityKitDefaults.CreateSiegeRegen();
            yield return AbilityKitDefaults.CreateFlyingBonus();
            yield return AbilityKitDefaults.CreateSuperBonus();
        }

        static void EnsureFolder()
        {
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.Catalogs);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanHero1Abilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanHero2Abilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanHero3Abilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanCasterAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanTitanAbilities);
            ContentAssetPaths.EnsureHumanUnitFolders();
        }

        static string LegacyAbilityPath(int abilityId) =>
            $"{ContentAssetPaths.Humans}/Abilities/Ability{abilityId}.asset";

        /// <summary>"Holy Nova" -> "holy-nova" (ASCII, kebab-case).</summary>
        static string DisplayNameToFileName(string displayName)
        {
            var sb = new StringBuilder();
            var lastDash = false;
            foreach (var c in (displayName ?? string.Empty).Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    lastDash = false;
                }
                else if (sb.Length > 0 && !lastDash)
                {
                    sb.Append('-');
                    lastDash = true;
                }
            }

            return sb.ToString().Trim('-');
        }

        static UnitAbilityDef EnsureDefAsset(UnitAbilityDef defaults, string fileName)
        {
            var newPath = $"{GetAbilityDirectory(defaults.AbilityId)}/{fileName}.asset";
            var legacyPath = LegacyAbilityPath(defaults.AbilityId);

            var existing = AssetDatabase.LoadAssetAtPath<UnitAbilityDef>(newPath);
            if (existing != null && existing.AbilityId != defaults.AbilityId)
            {
                Debug.LogWarning(
                    $"UnitAbilityAssetBuilder: {newPath} holds id {existing.AbilityId}, expected {defaults.AbilityId}; " +
                    "will look up by id instead.");
                existing = null;
            }

            if (existing == null)
            {
                existing = AssetDatabase.LoadAssetAtPath<UnitAbilityDef>(legacyPath);
            }

            if (existing == null)
            {
                existing = FindByAbilityId(defaults.AbilityId);
            }

            if (existing != null)
            {
                var currentPath = AssetDatabase.GetAssetPath(existing);
                if (!string.Equals(currentPath, newPath, System.StringComparison.Ordinal))
                {
                    var error = AssetDatabase.MoveAsset(currentPath, newPath);
                    if (error != string.Empty)
                    {
                        Debug.LogWarning($"UnitAbilityAssetBuilder: rename {currentPath} -> {newPath} failed: {error}");
                    }
                }
            }
            else
            {
                existing = ScriptableObject.CreateInstance<UnitAbilityDef>();
                AssetDatabase.CreateAsset(existing, newPath);
            }

            var def = existing;

            var behaviour = defaults.Behaviour;
            if (existing.Behaviour != null
                && defaults.Behaviour != null
                && existing.Behaviour.GetType() == defaults.Behaviour.GetType())
            {
                behaviour = existing.Behaviour;
            }

            if (behaviour != null && AssetDatabase.GetAssetPath(behaviour) == string.Empty)
            {
                AssetDatabase.AddObjectToAsset(behaviour, def);
            }

            def.Configure(
                defaults.AbilityId,
                defaults.DisplayName,
                defaults.Description,
                defaults.Kind,
                defaults.Unlock,
                defaults.UnlockValue,
                behaviour,
                defaults.Fx,
                defaults.Damage,
                defaults.Heal,
                defaults.HealPerSecond,
                defaults.Radius,
                defaults.CastRange,
                defaults.CooldownSeconds,
                defaults.DurationSeconds,
                defaults.Percent,
                defaults.ManaCost,
                defaults.StunSeconds,
                defaults.FlatBonus,
                defaults.SecondaryRadius,
                defaults.SecondaryHeal);
            EditorUtility.SetDirty(def);
            return def;
        }

        static string GetAbilityDirectory(int abilityId) => abilityId switch
        {
            AbilityIds.AuraHpRegen => ContentAssetPaths.HumanSiegeAbilities,
            AbilityIds.MeleeCleave => ContentAssetPaths.HumanMeleeAbilities,
            AbilityIds.RangedCrit => ContentAssetPaths.HumanRangedAbilities,
            AbilityIds.CasterHybrid => ContentAssetPaths.HumanCasterBonusAbilities,
            AbilityIds.FlyingSpawn => ContentAssetPaths.HumanFlyingAbilities,
            AbilityIds.SuperCatapult => ContentAssetPaths.HumanSuperAbilities,
            _ when abilityId < AbilityIds.Heal => ContentAssetPaths.HumanCasterAbilities,
            _ when abilityId < AbilityIds.Smite => ContentAssetPaths.HumanHero1Abilities,
            _ when abilityId < AbilityIds.HolyNova => ContentAssetPaths.HumanHero2Abilities,
            _ when abilityId < AbilityIds.Rally => ContentAssetPaths.HumanHero3Abilities,
            _ => ContentAssetPaths.HumanTitanAbilities,
        };

        /// <summary>Finds an existing def by <see cref="UnitAbilityDef.AbilityId"/> regardless of its file name.</summary>
        static UnitAbilityDef FindByAbilityId(int abilityId)
        {
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:UnitAbilityDef", new[] { ContentAssetPaths.Humans }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<UnitAbilityDef>(path);
                if (def != null && def.AbilityId == abilityId)
                {
                    return def;
                }
            }

            return null;
        }
    }
}
