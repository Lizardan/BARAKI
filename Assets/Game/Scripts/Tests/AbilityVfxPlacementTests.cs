using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Vfx;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class AbilityVfxPlacementTests
    {
        [Test]
        public void ResolveWorld_Caster_IsFeet()
        {
            var feet = new Vector3(1f, N4PerimeterLaneGeometry.LaneHeight, 2f);
            Assert.AreEqual(
                feet,
                AbilityVfxPlacement.ResolveWorld(AbilityVfxAnchor.Caster, feet, Vector3.zero));
        }

        [Test]
        public void ResolveWorld_Target_AddsBodyHeight()
        {
            var target = new Vector3(3f, N4PerimeterLaneGeometry.LaneHeight, 4f);
            var expected = target + Vector3.up * AbilityVfxPlacement.BodyHeight;
            Assert.AreEqual(
                expected,
                AbilityVfxPlacement.ResolveWorld(AbilityVfxAnchor.Target, Vector3.zero, target));
        }

        [Test]
        public void ResolveWorld_Ground_UsesAbsoluteGroundYNotFeetY()
        {
            var caster = new Vector3(-1f, N4PerimeterLaneGeometry.LaneHeight, 8f);
            var pos = AbilityVfxPlacement.ResolveWorld(AbilityVfxAnchor.Ground, caster, Vector3.zero);
            Assert.AreEqual(caster.x, pos.x);
            Assert.AreEqual(AbilityVfxPlacement.GroundY, pos.y);
            Assert.AreEqual(caster.z, pos.z);
        }

        [Test]
        public void ResolveWorld_Impact_ElevatesCenter()
        {
            var impact = new Vector3(5f, 0f, 7f);
            var pos = AbilityVfxPlacement.ResolveWorld(
                AbilityVfxAnchor.Impact,
                Vector3.zero,
                Vector3.zero,
                impact);
            Assert.AreEqual(impact.x, pos.x);
            Assert.AreEqual(AbilityVfxPlacement.ElevateY, pos.y);
            Assert.AreEqual(impact.z, pos.z);

            var high = new Vector3(1f, 0.15f, 2f);
            Assert.AreEqual(
                high,
                AbilityVfxPlacement.ResolveWorld(AbilityVfxAnchor.Impact, Vector3.zero, Vector3.zero, high));
        }

        [Test]
        public void ResolvePreviewImpact_LastCallUsesCaster_OthersUseTarget()
        {
            var caster = new Vector3(-1f, 0.15f, 0f);
            var target = new Vector3(1f, 0.15f, 0f);
            Assert.AreEqual(
                caster,
                AbilityVfxPlacement.ResolvePreviewImpact(AbilityIds.FlyingSpawn, caster, target));
            Assert.AreEqual(
                target,
                AbilityVfxPlacement.ResolvePreviewImpact(AbilityIds.SuperCatapult, caster, target));
            Assert.AreEqual(
                target,
                AbilityVfxPlacement.ResolvePreviewImpact(AbilityIds.MainBuildingSmite, caster, target));
        }

        [Test]
        public void ResolveRadii_FallBackToKitDefaults()
        {
            Assert.AreEqual(HeroAbilityRules.AuraRadius, AbilityVfxPlacement.ResolveAuraRadius(0f));
            Assert.AreEqual(12f, AbilityVfxPlacement.ResolveAuraRadius(12f));
            Assert.AreEqual(CasterSpellRules.FrostRadius, AbilityVfxPlacement.ResolveFrostRadius(0f));
            Assert.AreEqual(5f, AbilityVfxPlacement.ResolveFrostRadius(5f));
        }

        [Test]
        public void ResolveOneShotLifetime_FrostUsesStun_OthersUseCastSafetyNet()
        {
            Assert.AreEqual(
                CasterSpellRules.FrostFreezeSeconds,
                AbilityVfxPlacement.ResolveOneShotLifetimeSeconds(AbilityIds.Frost, 0f));
            Assert.AreEqual(
                2.25f,
                AbilityVfxPlacement.ResolveOneShotLifetimeSeconds(AbilityIds.Frost, 2.25f));
            Assert.AreEqual(
                AbilityVfxPlacement.CastLifetimeSeconds,
                AbilityVfxPlacement.ResolveOneShotLifetimeSeconds(AbilityIds.Strike, 0f));
        }

        [Test]
        public void ResolveOneShotLocalScale_IsPrefabTimesScale_NotCombatRadius()
        {
            var prefab = new GameObject("OneShotScalePrefab");
            try
            {
                prefab.transform.localScale = new Vector3(2f, 2f, 2f);
                Assert.AreEqual(
                    new Vector3(2f, 2f, 2f),
                    AbilityVfxPlacement.ResolveOneShotLocalScale(prefab, 1f));
                Assert.AreEqual(
                    new Vector3(4f, 4f, 4f),
                    AbilityVfxPlacement.ResolveOneShotLocalScale(prefab, 2f));
                Assert.AreNotEqual(
                    Vector3.one * CasterSpellRules.FrostRadius,
                    AbilityVfxPlacement.ResolveOneShotLocalScale(prefab, 1f));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
