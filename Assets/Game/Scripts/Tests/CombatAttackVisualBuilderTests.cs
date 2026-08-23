using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class CombatAttackVisualBuilderTests
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        static CombatProjectileState BuildProjectile(
            UnitRole role,
            int ownerSlot = 0,
            bool fromBuilding = false,
            string sourceBuildingId = null)
        {
            return new CombatProjectileState(
                projectileId: 7,
                attackerUnitId: fromBuilding ? 0 : 1,
                targetUnitId: 3,
                attackerOwnerSlot: ownerSlot,
                attackerRole: role,
                attackerRaceId: Game.Core.GameIds.Races.Human,
                rawDamage: 12f,
                flightDuration: 0.6f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward * 6f,
                isParabolic: true,
                targetBuildingInstanceId: null,
                sourceBuildingInstanceId: fromBuilding ? 44 : null,
                sourceBuildingId: sourceBuildingId);
        }

        [Test]
        public void CreateProjectileVisual_Ranged_BuildsSmallBoltWithTeamColor()
        {
            var projectile = BuildProjectile(UnitRole.Ranged, ownerSlot: 1);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            Assert.IsNotNull(visual.transform.Find("Bolt"), "Ranged shot should use the bolt prefab");
            AssertScaleNear(visual.transform.localScale, 0.75f * 1.15f);
            Assert.IsTrue(visual.GetComponentsInChildren<Collider>().Length == 0, "Bolt must not have colliders");
            AssertTeamColorApplied(visual, MatchPlayerColors.GetSlotColor(1));

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_Flying_BuildsSmallBolt()
        {
            var projectile = BuildProjectile(UnitRole.Flying, ownerSlot: 2);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            Assert.IsNotNull(visual.transform.Find("Bolt"), "Flying shot should use the bolt prefab");
            AssertScaleNear(visual.transform.localScale, 0.75f * 1.15f);

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_Super_BuildsBallistaBoltLvl3AtUnitScale()
        {
            var projectile = BuildProjectile(UnitRole.Super, ownerSlot: 0);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            AssertBallistaBolt(visual);

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_Caster_BuildsFireballWithTrail()
        {
            var projectile = BuildProjectile(UnitRole.Caster, ownerSlot: 2);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            var core = visual.transform.Find("Core");
            Assert.IsNotNull(core, "Fireball should have a Core child");
            AssertScaleNear(core.localScale, 0.55f);
            Assert.IsNotNull(visual.GetComponent<TrailRenderer>(), "Fireball should carry a TrailRenderer");
            Assert.IsTrue(visual.GetComponentsInChildren<Collider>().Length == 0, "Fireball must not have colliders");

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_Hero_BuildsFireballLikeCaster()
        {
            var projectile = BuildProjectile(UnitRole.Hero, ownerSlot: 2);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            var core = visual.transform.Find("Core");
            Assert.IsNotNull(core, "Priest shot should use the caster fireball");
            AssertScaleNear(core.localScale, 0.55f);
            Assert.IsNotNull(visual.GetComponent<TrailRenderer>(), "Priest shot should carry a TrailRenderer");

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_TowerShot_IsArrowSizedOwnerColoredBolt()
        {
            var projectile = BuildProjectile(UnitRole.Ranged, ownerSlot: 3, fromBuilding: true, sourceBuildingId: "BUILDING_TOWER");
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            Assert.IsNotNull(visual.transform.Find("Bolt"), "Tower shot should use the bolt prefab");
            Assert.IsNull(visual.GetComponent<TrailRenderer>(), "Tower shot should have no trail");
            AssertScaleNear(visual.transform.localScale, 0.75f * 1.15f);
            AssertTeamColorApplied(visual, MatchPlayerColors.GetSlotColor(3));

            var meshFilter = visual.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(meshFilter?.sharedMesh);
            Assert.AreEqual("Bolt_lvl1", meshFilter.sharedMesh.name, "Towers keep the archer arrow mesh");

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_MainBuildingShot_IsBallistaBolt()
        {
            var projectile = BuildProjectile(UnitRole.Ranged, ownerSlot: 1, fromBuilding: true, sourceBuildingId: Game.Core.GameIds.Buildings.Main);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            AssertBallistaBolt(visual);

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_BarracksShot_IsBallistaBolt()
        {
            var projectile = BuildProjectile(
                UnitRole.Ranged,
                ownerSlot: 2,
                fromBuilding: true,
                sourceBuildingId: Game.Core.GameIds.Buildings.BarracksCenter);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            AssertBallistaBolt(visual);

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void CreateProjectileVisual_SuperSplash_BuildsCatapultRock()
        {
            var projectile = new CombatProjectileState(
                projectileId: 7,
                attackerUnitId: 1,
                targetUnitId: 3,
                attackerOwnerSlot: 0,
                attackerRole: UnitRole.Super,
                attackerRaceId: Game.Core.GameIds.Races.Human,
                rawDamage: 12f,
                flightDuration: 0.6f,
                startPosition: Vector3.zero,
                targetPosition: Vector3.forward * 6f,
                isParabolic: true,
                appliesSplashAoe: true);
            var root = new GameObject("Root");
            var visual = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, root.transform);

            var bolt = visual.transform.Find("Bolt");
            Assert.IsNotNull(bolt, "Catapult rock is parented as Bolt");
            var meshFilter = bolt.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(meshFilter?.sharedMesh);
            var rockPrefab = Resources.Load<GameObject>("Art/ProjectileCatapultRock");
            Assert.IsNotNull(rockPrefab);
            var rockMesh = rockPrefab.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
            Assert.AreSame(rockMesh, meshFilter.sharedMesh, "Bonus Super splash must use the catapult rock prefab, not the ballista bolt.");

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(root);
        }

        static void AssertBallistaBolt(GameObject visual)
        {
            var bolt = visual.transform.Find("Bolt");
            Assert.IsNotNull(bolt, "Should use the bolt prefab");
            AssertScaleNear(visual.transform.localScale, 1f);

            var meshFilter = bolt.GetComponentInChildren<MeshFilter>(true);
            Assert.IsNotNull(meshFilter, "Ballista bolt needs a MeshFilter");
            Assert.IsNotNull(meshFilter.sharedMesh, "Ballista bolt needs a mesh");
            Assert.AreEqual("Bolt_lvl3", meshFilter.sharedMesh.name, "Must match unit Bolt_lvl3");
            AssertScaleNear(meshFilter.transform.localScale, 1f);
        }

        static void AssertScaleNear(Vector3 scale, float expected)
        {
            Assert.IsTrue(
                Mathf.Abs(scale.x - expected) < 0.001f
                && Mathf.Abs(scale.y - expected) < 0.001f
                && Mathf.Abs(scale.z - expected) < 0.001f,
                $"Expected uniform scale {expected}, got {scale}");
        }

        static void AssertTeamColorApplied(GameObject visual, Color slotColor)
        {
            var renderer = visual.GetComponentInChildren<Renderer>(true);
            Assert.IsNotNull(renderer, "Bolt should have a renderer");
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var texture = block.GetTexture(BaseMapId);
            Assert.IsNotNull(texture, "Bolt should carry a team-color _BaseMap override");
            var teamColor = visual.GetComponentInChildren<TtUnitTeamColor>(true);
            Assert.IsNotNull(teamColor, "Bolt prefab should carry TtUnitTeamColor");
            Assert.IsTrue(
                teamColor.TeamTextures != null && teamColor.TeamTextures.Length == 4,
                "TeamTextures should contain all 4 slot colors");
        }
    }
}
