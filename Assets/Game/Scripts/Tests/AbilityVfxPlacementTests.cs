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
        public void ResolveWorld_Caster_AddsBodyHeight()
        {
            var feet = new Vector3(1f, N4PerimeterLaneGeometry.LaneHeight, 2f);
            var expected = feet + Vector3.up * AbilityVfxPlacement.BodyHeight;
            Assert.AreEqual(
                expected,
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

        [Test]
        public void FollowsHost_CasterAndTarget_Only()
        {
            Assert.IsTrue(AbilityVfxPlacement.FollowsHost(AbilityVfxAnchor.Caster));
            Assert.IsTrue(AbilityVfxPlacement.FollowsHost(AbilityVfxAnchor.Target));
            Assert.IsFalse(AbilityVfxPlacement.FollowsHost(AbilityVfxAnchor.Ground));
            Assert.IsFalse(AbilityVfxPlacement.FollowsHost(AbilityVfxAnchor.Impact));
            Assert.IsFalse(AbilityVfxPlacement.FollowsHost(AbilityVfxAnchor.Unspecified));
        }

        [Test]
        public void ResolveFollowLocalPosition_CasterAndTarget_AreBodyCenter()
        {
            Assert.AreEqual(
                Vector3.up * AbilityVfxPlacement.BodyHeight,
                AbilityVfxPlacement.ResolveFollowLocalPosition(AbilityVfxAnchor.Caster));
            Assert.AreEqual(
                Vector3.up * AbilityVfxPlacement.BodyHeight,
                AbilityVfxPlacement.ResolveFollowLocalPosition(AbilityVfxAnchor.Target));
        }

        [Test]
        public void ApplyOneShotTransform_Caster_ParentsAtBodyHeight()
        {
            var caster = new GameObject("CasterRoot");
            var target = new GameObject("TargetRoot");
            var fx = new GameObject("Fx");
            caster.transform.position = new Vector3(1f, N4PerimeterLaneGeometry.LaneHeight, 2f);
            target.transform.position = new Vector3(5f, N4PerimeterLaneGeometry.LaneHeight, 2f);
            try
            {
                AbilityVfxPlacement.ApplyOneShotTransform(
                    fx.transform,
                    AbilityVfxAnchor.Caster,
                    caster.transform,
                    target.transform,
                    Vector3.zero,
                    Vector3.zero,
                    Quaternion.identity);
                Assert.AreSame(caster.transform, fx.transform.parent);
                Assert.AreEqual(
                    Vector3.up * AbilityVfxPlacement.BodyHeight,
                    fx.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(fx);
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ApplyOneShotTransform_Target_ParentsAtBodyHeight()
        {
            var caster = new GameObject("CasterRoot");
            var target = new GameObject("TargetRoot");
            var fx = new GameObject("Fx");
            try
            {
                AbilityVfxPlacement.ApplyOneShotTransform(
                    fx.transform,
                    AbilityVfxAnchor.Target,
                    caster.transform,
                    target.transform,
                    Vector3.zero,
                    Vector3.zero,
                    Quaternion.identity);
                Assert.AreSame(target.transform, fx.transform.parent);
                Assert.AreEqual(
                    Vector3.up * AbilityVfxPlacement.BodyHeight,
                    fx.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(fx);
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ApplyOneShotTransform_Ground_WorldAtCasterGroundY_NoFollowParent()
        {
            var caster = new GameObject("CasterRoot");
            var target = new GameObject("TargetRoot");
            var world = new GameObject("WorldRoot");
            var fx = new GameObject("Fx");
            caster.transform.position = new Vector3(-1f, N4PerimeterLaneGeometry.LaneHeight, 8f);
            try
            {
                AbilityVfxPlacement.ApplyOneShotTransform(
                    fx.transform,
                    AbilityVfxAnchor.Ground,
                    caster.transform,
                    target.transform,
                    Vector3.zero,
                    Vector3.zero,
                    Quaternion.identity,
                    world.transform);
                Assert.AreSame(world.transform, fx.transform.parent);
                Assert.AreEqual(caster.transform.position.x, fx.transform.position.x);
                Assert.AreEqual(AbilityVfxPlacement.GroundY, fx.transform.position.y);
                Assert.AreEqual(caster.transform.position.z, fx.transform.position.z);
            }
            finally
            {
                Object.DestroyImmediate(fx);
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(world);
            }
        }

        [Test]
        public void ApplyOneShotTransform_Impact_ElevatesCenter_NoFollowParent()
        {
            var caster = new GameObject("CasterRoot");
            var target = new GameObject("TargetRoot");
            var fx = new GameObject("Fx");
            var impact = new Vector3(5f, 0f, 7f);
            try
            {
                AbilityVfxPlacement.ApplyOneShotTransform(
                    fx.transform,
                    AbilityVfxAnchor.Impact,
                    caster.transform,
                    target.transform,
                    impact,
                    Vector3.zero,
                    Quaternion.identity);
                Assert.IsNull(fx.transform.parent);
                Assert.AreEqual(impact.x, fx.transform.position.x);
                Assert.AreEqual(AbilityVfxPlacement.ElevateY, fx.transform.position.y);
                Assert.AreEqual(impact.z, fx.transform.position.z);
            }
            finally
            {
                Object.DestroyImmediate(fx);
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ApplyOneShotTransform_Euler_SetsLocalRotationWhenParented()
        {
            var caster = new GameObject("CasterRoot");
            var target = new GameObject("TargetRoot");
            var fx = new GameObject("Fx");
            try
            {
                AbilityVfxPlacement.ApplyOneShotTransform(
                    fx.transform,
                    AbilityVfxAnchor.Caster,
                    caster.transform,
                    target.transform,
                    Vector3.zero,
                    new Vector3(0f, 0f, 90f),
                    Quaternion.identity);
                Assert.AreEqual(90f, fx.transform.localEulerAngles.z, 0.1f);
            }
            finally
            {
                Object.DestroyImmediate(fx);
                Object.DestroyImmediate(caster);
                Object.DestroyImmediate(target);
            }
        }
    }
}
