using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class CombatAttackVisualBuilder
    {
        static readonly Color HumanArrowColor = new(0.45f, 0.28f, 0.12f);
        static readonly Color BugProjectileColor = new(0.3f, 0.85f, 0.25f);
        static readonly Color HumanSpellColor = new(0.9f, 0.15f, 0.1f);
        static readonly Color BugSpellColor = new(0.35f, 0.9f, 0.3f);

        const float ProjectileScale = 2f;

        public static GameObject CreateProjectileVisual(CombatProjectileState projectile, Transform parent)
        {
            ResolveVisualStyle(projectile, out var primitive, out var localScale, out var color);

            var visual = GameObject.CreatePrimitive(primitive);
            visual.name = projectile.IsBuildingAttack
                ? $"BuildingShot_{projectile.ProjectileId}"
                : $"Projectile_{projectile.ProjectileId}";
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = localScale;

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor(Shader.PropertyToID("_BaseColor"), color);
                renderer.SetPropertyBlock(block);
            }

            return visual;
        }

        /// <summary>Building shots = owner-colored cubes; unit shots keep race/role styling.</summary>
        public static void ResolveVisualStyle(
            CombatProjectileState projectile,
            out PrimitiveType primitive,
            out Vector3 localScale,
            out Color color)
        {
            if (projectile.IsBuildingAttack)
            {
                primitive = PrimitiveType.Cube;
                localScale = Vector3.one * TowerCombatRules.ProjectileCubeScale;
                color = MatchPlayerColors.GetSlotColor(projectile.AttackerOwnerSlot);
                return;
            }

            var isHuman = projectile.AttackerRaceId == GameIds.Races.Human;
            var isRanged = projectile.AttackerRole == UnitRole.Ranged;
            var isCaster = projectile.AttackerRole == UnitRole.Caster;

            if (isRanged)
            {
                if (isHuman)
                {
                    primitive = PrimitiveType.Cube;
                    localScale = new Vector3(0.08f, 0.08f, 0.55f) * ProjectileScale;
                    color = HumanArrowColor;
                }
                else
                {
                    primitive = PrimitiveType.Sphere;
                    localScale = Vector3.one * (0.22f * ProjectileScale);
                    color = BugProjectileColor;
                }

                return;
            }

            primitive = PrimitiveType.Cube;
            localScale = Vector3.one * ((isCaster ? 0.24f : 0.2f) * ProjectileScale);
            color = isHuman ? HumanSpellColor : BugSpellColor;
        }

        public static void UpdateProjectileTransform(Transform visual, CombatProjectileState projectile)
        {
            var progress = projectile.Progress;
            var position = CombatProjectileTrajectory.Evaluate(
                projectile.StartPosition,
                projectile.TargetPosition,
                progress,
                projectile.IsParabolic);
            visual.position = position;

            var nextProgress = Mathf.Min(1f, progress + 0.04f);
            var nextPosition = CombatProjectileTrajectory.Evaluate(
                projectile.StartPosition,
                projectile.TargetPosition,
                nextProgress,
                projectile.IsParabolic);
            var direction = nextPosition - position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = projectile.TargetPosition - projectile.StartPosition;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                visual.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}
