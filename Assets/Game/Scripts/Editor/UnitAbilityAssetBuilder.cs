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
    /// with the behaviour as a sub-asset) plus the shared <see cref="UnitAbilityCatalog"/> under <see cref="AbilitiesDir"/>.
    /// Existing def assets are reused by <see cref="UnitAbilityDef.AbilityId"/> so prefab references survive.
    /// Legacy <c>Ability{id}.asset</c> names are migrated to display names on the next build.
    /// </summary>
    public static class UnitAbilityAssetBuilder
    {
        public const string AbilitiesDir = "Assets/Game/ScriptableObjects/Abilities";
        public const string CatalogPath = AbilitiesDir + "/UnitAbilityCatalog.asset";

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
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(AbilitiesDir))
            {
                AssetDatabase.CreateFolder("Assets/Game/ScriptableObjects", "Abilities");
            }
        }

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
            var newPath = $"{AbilitiesDir}/{fileName}.asset";
            var legacyPath = $"{AbilitiesDir}/Ability{defaults.AbilityId}.asset";

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

        /// <summary>Finds an existing def by <see cref="UnitAbilityDef.AbilityId"/> regardless of its file name.</summary>
        static UnitAbilityDef FindByAbilityId(int abilityId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UnitAbilityDef", new[] { AbilitiesDir }))
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
