using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Points each unit prefab's <see cref="UnitCombatSettings"/> at its source definition asset
    /// (<see cref="UnitDefinition"/> / <see cref="HeroDefinition"/>) and clears baked numbers. The
    /// prefab stores references only; stats resolve live from the referenced ScriptableObject, so a
    /// balance edit on the asset applies without any sync step. Run once after upgrading; builders
    /// reassign refs automatically when a prefab is rebuilt (banner style).
    /// </summary>
    public static class UnitStatsSourceAssigner
    {
        static readonly UnitRole[] Roles =
        {
            UnitRole.Melee,
            UnitRole.Ranged,
            UnitRole.Caster,
            UnitRole.Siege,
            UnitRole.Flying,
            UnitRole.Super,
        };

        [MenuItem("BARAKI/Units/Assign Stats Sources")]
        public static void AssignAll()
        {
            var assigned = AssignRace(GameIds.Races.Human);
            assigned.AddRange(AssignRace(GameIds.Races.Faceless));
            AssetDatabase.SaveAssets();
            Debug.Log($"UnitStatsSourceAssigner: assigned sources to {assigned.Count} prefab(s).\n{string.Join("\n", assigned)}");
        }

        [MenuItem("BARAKI/Faceless/Assign Stats Sources")]
        public static void AssignFaceless()
        {
            var assigned = AssignRace(GameIds.Races.Faceless);
            AssetDatabase.SaveAssets();
            Debug.Log($"UnitStatsSourceAssigner: assigned sources to {assigned.Count} Faceless prefab(s).\n{string.Join("\n", assigned)}");
        }

        /// <summary>Assigns source refs for every registered prefab of the race. Safe to re-run.</summary>
        public static List<string> AssignRace(string raceId)
        {
            var assigned = new List<string>();
            var raceCatalog = AssetDatabase.LoadAssetAtPath<RaceCatalog>(ContentAssetPaths.RaceCatalog);
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (raceCatalog == null || visualCatalog == null)
            {
                Debug.LogError($"UnitStatsSourceAssigner: catalogs not found (race={raceCatalog} visual={visualCatalog}).");
                return assigned;
            }

            var race = raceCatalog.GetRace(raceId);
            if (race == null)
            {
                Debug.LogError($"UnitStatsSourceAssigner: race '{raceId}' not found in {ContentAssetPaths.RaceCatalog}.");
                return assigned;
            }

            foreach (var role in Roles)
            {
                var definition = race.GetUnit(role);
                if (definition == null)
                {
                    continue;
                }

                if (TryAssign(visualCatalog, raceId, role, heroSlot: 0, definition, out var path))
                {
                    assigned.Add($"{raceId} {role}: {path}");
                }
            }

            for (var bonusSlot = 1; bonusSlot <= 6; bonusSlot++)
            {
                var role = BonusKitRules.RoleForBonusSlot(bonusSlot);
                var definition = race.GetUnitBonus(role) ?? race.GetUnit(role);
                if (definition == null)
                {
                    continue;
                }

                if (TryAssign(visualCatalog, raceId, role, heroSlot: 0, definition, out var path, bonusSlot))
                {
                    assigned.Add($"{role} BONUS ({raceId}): {path}");
                }
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                var hero = race.GetHeroBySlot(slot);
                if (hero == null)
                {
                    continue;
                }

                if (TryAssign(visualCatalog, raceId, UnitRole.Hero, slot, hero, out var path))
                {
                    assigned.Add($"Hero{slot} ({raceId}): {path}");
                }
            }

            var titan = race.GetHeroBySlot(1);
            if (titan != null
                && TryAssign(visualCatalog, raceId, UnitRole.Titan, heroSlot: 0, titan, out var titanPath))
            {
                assigned.Add($"Titan ({raceId}): {titanPath}");
            }

            // Veteran champions (bonus slots 7–10, PRE-006b / FACELESS-012): both races assign the
            // base hero ref here; the veteran multipliers apply at resolution time.
            for (var bonusSlot = BonusKitRules.Hero1BonusSlot;
                 bonusSlot <= BonusKitRules.TitanBonusSlot;
                 bonusSlot++)
            {
                var isTitan = BonusKitRules.IsTitanBonusSlot(bonusSlot);
                var heroSlot = isTitan ? 1 : BonusKitRules.HeroSlotForBonusSlot(bonusSlot);
                var baseHero = race.GetHeroBySlot(heroSlot);
                if (baseHero == null)
                {
                    continue;
                }

                if (TryAssign(
                        visualCatalog,
                        raceId,
                        isTitan ? UnitRole.Titan : UnitRole.Hero,
                        isTitan ? 0 : heroSlot,
                        baseHero,
                        out var veteranPath,
                        bonusSlot))
                {
                    assigned.Add($"Veteran slot {bonusSlot} ({raceId}): {veteranPath}");
                }
            }

            return assigned;
        }

        /// <summary>
        /// One-time migration: bakes each registered prefab's uniform root scale × role presenter
        /// factor into <see cref="UnitDefinition.VisualScale"/> / <see cref="HeroDefinition.VisualScale"/>.
        /// The stored value IS the in-game scale (no presenter multiply at match time), so a balance
        /// edit on the asset changes the size directly. Assets left at 0 (no matching prefab) fall
        /// back to the authored prefab root scale at presentation time.
        /// </summary>
        [MenuItem("BARAKI/Units/Assign Visual Scales (from prefabs)")]
        public static void AssignVisualScales() => BakeVisualScales(force: false);

        /// <summary>
        /// Hard overwrite pass: rewrites every baked scale back to prefab × role factor. Use it for
        /// the migration (existing assets already carry pre-migration values) — a single run, after
        /// which manual tuning in the asset is preserved by <see cref="AssignVisualScales"/>.
        /// </summary>
        [MenuItem("BARAKI/Units/Bake Visual Scales (final, role-multiplied)")]
        public static void BakeVisualScalesFinal() => BakeVisualScales(force: true);

        static readonly (string Field, UnitRole Role)[] VisualFields =
        {
            ("_melee", UnitRole.Melee),
            ("_meleeBonus", UnitRole.Melee),
            ("_meleeServant", UnitRole.Melee),
            ("_ranged", UnitRole.Ranged),
            ("_rangedBonus", UnitRole.Ranged),
            ("_caster", UnitRole.Caster),
            ("_casterBonus", UnitRole.Caster),
            ("_siege", UnitRole.Siege),
            ("_siegeBonus", UnitRole.Siege),
            ("_flying", UnitRole.Flying),
            ("_flyingBonus", UnitRole.Flying),
            ("_super", UnitRole.Super),
            ("_superBonus", UnitRole.Super),
            ("_hero1", UnitRole.Hero),
            ("_hero1Bonus", UnitRole.Hero),
            ("_hero2", UnitRole.Hero),
            ("_hero2Bonus", UnitRole.Hero),
            ("_hero3", UnitRole.Hero),
            ("_hero3Bonus", UnitRole.Hero),
            ("_titan", UnitRole.Titan),
            ("_titanBonus", UnitRole.Titan),
        };

        static void BakeVisualScales(bool force)
        {
            var visualCatalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (visualCatalog == null)
            {
                Debug.LogError("UnitStatsSourceAssigner: visual catalog not found.");
                return;
            }

            var so = new SerializedObject(visualCatalog);
            var races = so.FindProperty("_races");
            var dirty = new HashSet<ScriptableObject>();
            var nonUniform = new List<string>();

            void Bake(GameObject prefab, UnitRole role)
            {
                if (prefab == null)
                {
                    return;
                }

                var scale = prefab.transform.localScale;
                if (Mathf.Abs(scale.x - scale.y) > 0.001f || Mathf.Abs(scale.x - scale.z) > 0.001f)
                {
                    nonUniform.Add($"{AssetDatabase.GetAssetPath(prefab)} ({scale})");
                }

                var settings = prefab.GetComponentInChildren<UnitCombatSettings>(true);
                if (settings == null)
                {
                    return;
                }

                // Final in-game scale = authored prefab root scale × role presenter factor.
                var target = scale.x * UnitGreyboxVisuals.ResolveAnimatedPresenterScale(role);

                void BakeInto(ScriptableObject definition)
                {
                    if (definition == null)
                    {
                        return;
                    }

                    var current = definition switch
                    {
                        UnitDefinition unit => unit.VisualScale,
                        HeroDefinition hero => hero.VisualScale,
                        _ => 0f,
                    };

                    if (!force && current > 0f)
                    {
                        return; // keep manual tuning
                    }

                    if (Mathf.Abs(current - target) <= 0.0001f)
                    {
                        return; // already baked
                    }

                    switch (definition)
                    {
                        case UnitDefinition unit:
                            unit.SetVisualScale(target);
                            break;
                        case HeroDefinition hero:
                            hero.SetVisualScale(target);
                            break;
                    }

                    dirty.Add(definition);
                }

                BakeInto(settings.UnitDefinition);
                BakeInto(settings.HeroDefinition);
            }

            for (var r = 0; r < races.arraySize; r++)
            {
                var raceProp = races.GetArrayElementAtIndex(r);
                var visuals = raceProp.FindPropertyRelative("_visuals");
                foreach (var (field, role) in VisualFields)
                {
                    Bake(visuals.FindPropertyRelative(field).objectReferenceValue as GameObject, role);
                }
            }

            foreach (var definition in dirty)
            {
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"UnitStatsSourceAssigner: baked VisualScale into {dirty.Count} definition(s)." +
                (nonUniform.Count > 0 ? $"\nNon-uniform prefab roots (kept X): {string.Join(", ", nonUniform)}" : string.Empty));
        }

        static bool TryAssign(
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            int heroSlot,
            ScriptableObject source,
            out string path,
            int bonusSlot = 0)
        {
            path = null;
            if (!visualCatalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var prefab) || prefab == null)
            {
                Debug.LogWarning(
                    $"UnitStatsSourceAssigner: no prefab for {role} slot {heroSlot} bonus {bonusSlot}.");
                return false;
            }

            path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var settings = root.GetComponentInChildren<UnitCombatSettings>(true);
                if (settings == null)
                {
                    settings = root.AddComponent<UnitCombatSettings>();
                }

                if (source is UnitDefinition unitDefinition)
                {
                    settings.AssignSource(unitDefinition);
                }
                else if (source is HeroDefinition heroDefinition)
                {
                    settings.AssignSource(heroDefinition);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}