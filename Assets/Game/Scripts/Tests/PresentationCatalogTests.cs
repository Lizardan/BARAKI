using System.Collections.Generic;
using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>
    /// Presentation contract guard: every ability that can be emitted through the
    /// snapshot SpellCast channel must resolve a VFX on the CLIENT. A def without
    /// VfxPrefab or with an invisible color fails silently on clients while the host
    /// still plays it locally — the exact "effect visible only on host" bug class.
    /// </summary>
    public sealed class PresentationCatalogTests
    {
        const string CatalogPath = "Assets/Game/ScriptableObjects/Catalogs/UnitAbilityCatalog.asset";

        [Test]
        public void EveryUnitAbilityDef_HasClientVisibleVfx()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath);
            Assert.Greater(catalog.Abilities.Count, 0);

            var offenders = new List<string>();
            foreach (var def in catalog.Abilities)
            {
                if (def == null)
                {
                    offenders.Add("<null def>");
                    continue;
                }

                if (def.Fx.VfxPrefab == null)
                {
                    offenders.Add($"{def.AbilityId}:{def.DisplayName}:VfxPrefab=null");
                }

                if (def.Fx.Color.a <= 0f)
                {
                    offenders.Add($"{def.AbilityId}:{def.DisplayName}:Color.a={def.Fx.Color.a}");
                }
            }

            Assert.IsEmpty(
                offenders,
                "Abilities without client-resolvable VfxPrefab play host-only:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void MainExtraSmiteDefs_HaveClientVisibleVfx()
        {
            var offenders = new List<string>();
            CollectMainExtraOffender(AbilityIds.MainBuildingSmite, offenders);
            CollectMainExtraOffender(AbilityIds.MainUnitSmite, offenders);

            Assert.IsEmpty(
                offenders,
                "Main extra smites without client-resolvable VfxPrefab play host-only:\n"
                + string.Join("\n", offenders));
        }

        private static void CollectMainExtraOffender(int abilityId, List<string> offenders)
        {
            if (!MainExtraAbilityFxDefs.TryGet(abilityId, out var def) || def == null)
            {
                offenders.Add($"{abilityId}:def missing");
                return;
            }

            if (def.Fx.VfxPrefab == null)
            {
                offenders.Add($"{abilityId}:{def.DisplayName}:VfxPrefab=null");
            }

            if (def.Fx.Color.a <= 0f)
            {
                offenders.Add($"{abilityId}:{def.DisplayName}:Color.a={def.Fx.Color.a}");
            }
        }
    }
}
