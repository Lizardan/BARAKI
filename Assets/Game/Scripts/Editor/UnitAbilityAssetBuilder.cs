using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Vfx;
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
            yield return AbilityKitDefaults.CreateKingBonus();
            yield return AbilityKitDefaults.CreatePaladinBonus();
            yield return AbilityKitDefaults.CreatePriestBonus();
            yield return AbilityKitDefaults.CreateTitanBonus();

            // --- Faceless kits (FACELESS-012 / FACELESS-016 / FACELESS-011): caster, champions 7–10, unit bonuses 1–6 ---
            yield return AbilityKitDefaults.CreateFacelessCaster();
            yield return AbilityKitDefaults.CreateFacelessBonus(UnitRole.Melee);
            yield return AbilityKitDefaults.CreateFacelessBonus(UnitRole.Ranged);
            yield return AbilityKitDefaults.CreateFacelessBonus(UnitRole.Caster);
            yield return AbilityKitDefaults.CreateFacelessBonus(UnitRole.Siege);
            yield return AbilityKitDefaults.CreateFacelessBonus(UnitRole.Flying);
            yield return AbilityKitDefaults.CreateFacelessBonus(UnitRole.Super);
            yield return AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, BonusKitRules.Hero1BonusSlot);
            yield return AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, BonusKitRules.Hero2BonusSlot);
            yield return AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, BonusKitRules.Hero3BonusSlot);
            yield return AbilityKitDefaults.CreateVeteranKit(GameIds.Races.Faceless, BonusKitRules.TitanBonusSlot);
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
            ContentAssetPaths.EnsureHumanBonusHeroFolders();
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessCasterAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessHero1Abilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessHero2Abilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessHero3Abilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessTitanAbilities);

            // Bonus-unit marker def folders (slots 1–6, FACELESS-011).
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessBonusUnits);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessMeleeBonusAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessRangedBonusAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessCasterBonusAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessSiegeBonusAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessFlyingBonusAbilities);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessSuperBonusAbilities);
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
            var newPath = $"{GetAbilityDirectory(defaults.AbilityId)}/{fileName}.asset";

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
            var seededFx = defaults.Fx;
            seededFx.Anchor = AbilityVfxKindRules.ResolveDefaultAnchor(defaults.AbilityId);
            seededFx.AnimKind = AbilityAnimRules.ResolveKind(defaults.AbilityId);
            var preservedFx = existing.Fx.WithPreservedAuthored(seededFx);
            var preservedRadius = existing.Radius > 0f ? existing.Radius : defaults.Radius;
            var preservedCastRange = existing.CastRange > 0f ? existing.CastRange : defaults.CastRange;
            var preservedSecondaryRadius = existing.SecondaryRadius > 0f
                ? existing.SecondaryRadius
                : defaults.SecondaryRadius;

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
                preservedFx,
                defaults.Damage,
                defaults.Heal,
                defaults.HealPerSecond,
                preservedRadius,
                preservedCastRange,
                defaults.CooldownSeconds,
                defaults.DurationSeconds,
                defaults.Percent,
                defaults.ManaCost,
                defaults.StunSeconds,
                defaults.FlatBonus,
                preservedSecondaryRadius,
                defaults.SecondaryHeal);

            ApplyVfxPrefab(def);

            EditorUtility.SetDirty(def);
            return def;
        }

        static string GetAbilityDirectory(int abilityId) => abilityId switch
        {
            // --- Faceless kits (kept under the Faceless race folder for a uniform structure) ---
            AbilityIds.BlightingGaze => ContentAssetPaths.FacelessCasterAbilities,
            AbilityIds.VoidDrain => ContentAssetPaths.FacelessCasterAbilities,
            AbilityIds.RaiseDrowned => ContentAssetPaths.FacelessCasterAbilities,
            AbilityIds.AncientMantle => ContentAssetPaths.FacelessHero1Abilities,
            AbilityIds.AreaOfMiss => ContentAssetPaths.FacelessHero2Abilities,
            AbilityIds.FeastZone => ContentAssetPaths.FacelessHero3Abilities,
            AbilityIds.AuraOfHunger => ContentAssetPaths.FacelessTitanAbilities,
            AbilityIds.FacelessHunger => ContentAssetPaths.FacelessMeleeBonusAbilities,
            AbilityIds.FacelessTaint => ContentAssetPaths.FacelessRangedBonusAbilities,
            AbilityIds.FacelessCallOfAbyss => ContentAssetPaths.FacelessCasterBonusAbilities,
            AbilityIds.FacelessDeathExplosion => ContentAssetPaths.FacelessSiegeBonusAbilities,
            AbilityIds.FacelessHungeringFlight => ContentAssetPaths.FacelessFlyingBonusAbilities,
            AbilityIds.FacelessFeast => ContentAssetPaths.FacelessSuperBonusAbilities,

            AbilityIds.AuraHpRegen => ContentAssetPaths.HumanSiegeAbilities,
            AbilityIds.MeleeCleave => ContentAssetPaths.HumanMeleeAbilities,
            AbilityIds.RangedCrit => ContentAssetPaths.HumanRangedAbilities,
            AbilityIds.CasterHybrid => ContentAssetPaths.HumanCasterBonusAbilities,
            AbilityIds.FlyingSpawn => ContentAssetPaths.HumanFlyingAbilities,
            AbilityIds.SuperCatapult => ContentAssetPaths.HumanSuperAbilities,
            AbilityIds.KingsCommand => ContentAssetPaths.HumanBonusHero1Abilities,
            AbilityIds.Aegis => ContentAssetPaths.HumanBonusHero2Abilities,
            AbilityIds.Sanctuary => ContentAssetPaths.HumanBonusHero3Abilities,
            AbilityIds.GreaterColossus => ContentAssetPaths.HumanBonusTitanAbilities,
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
                         "t:UnitAbilityDef", new[] { ContentAssetPaths.Humans, ContentAssetPaths.Faceless }))
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

        static readonly Dictionary<int, string> VfxPrefabPaths = new()
        {
            // Caster
            { AbilityIds.CasterHeal,    "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 Hit Light B (Air).prefab" },
            { AbilityIds.Frost,         "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.Resurrect,     "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 Souls Escape.prefab" },

            // King (Hero1)
            { AbilityIds.Heal,          "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 LightGlow A (Loop).prefab" },
            { AbilityIds.Ultimate,      "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab" },
            { AbilityIds.Strike,        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab" },

            // Paladin (Hero2)
            { AbilityIds.Smite,         "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab" },
            { AbilityIds.Shield,        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.Consecration,  "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR2 Firewall A.prefab" },

            // Priest (Hero3)
            { AbilityIds.HolyNova,      "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Impact Glowing HDR (Blue).prefab" },
            { AbilityIds.GreaterHeal,   "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Ambient Glows.prefab" },
            { AbilityIds.Revive,        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Falling Stars.prefab" },

            // Titan
            { AbilityIds.Slam,          "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR2 Ground Hit.prefab" },
            { AbilityIds.Rally,         "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Electrified 3.prefab" },
            { AbilityIds.Stomp,         "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR3 Fire Explosion B.prefab" },

            // Bonus units
            { AbilityIds.AuraDamagePercent, "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.AuraAttackSpeedPercent, "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.AuraArmorPercent, "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.AuraMaxHpPercent, "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.AuraHpRegen,   "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 LightGlow A (Loop).prefab" },
            { AbilityIds.MeleeCleave,   "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab" },
            { AbilityIds.RangedCrit,    "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/CFXR3 Hit Fire B (Air).prefab" },
            { AbilityIds.CasterHybrid,  "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR3 Hit Electric C (Air).prefab" },
            { AbilityIds.FlyingSpawn,   "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Falling Stars.prefab" },
            { AbilityIds.SuperCatapult, "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR3 Fire Explosion B.prefab" },

            // Veteran champions (PRE-006b)
            { AbilityIds.KingsCommand,  "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab" },
            { AbilityIds.Aegis,         "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
            { AbilityIds.Sanctuary,     "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Ambient Glows.prefab" },
            { AbilityIds.GreaterColossus, "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab" },
        };

        static void ApplyVfxPrefab(UnitAbilityDef def)
        {
            if (!VfxPrefabPaths.TryGetValue(def.AbilityId, out var path))
            {
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"UnitAbilityAssetBuilder: CFXR prefab not found: {path} (ability {def.AbilityId})");
                return;
            }

            var so = new SerializedObject(def);
            var fxProp = so.FindProperty("_fx").FindPropertyRelative("VfxPrefab");
            if (fxProp != null && fxProp.objectReferenceValue == null)
            {
                fxProp.objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
