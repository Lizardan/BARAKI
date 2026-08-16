using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public static class CombatProjectileTrajectory
    {
        public static float ComputeFlightDuration(Vector3 start, Vector3 end, float speed)
        {
            var delta = end - start;
            delta.y = 0f;
            return Mathf.Max(0.06f, delta.magnitude / speed);
        }

        public static Vector3 Evaluate(Vector3 start, Vector3 end, float progress, bool parabolic)
        {
            progress = Mathf.Clamp01(progress);
            var position = Vector3.Lerp(start, end, progress);
            if (parabolic)
            {
                position.y += 4f * CombatAttackRules.ParabolicArcHeight * progress * (1f - progress);
            }

            return position;
        }

        public static Vector3 GetProjectileOrigin(Vector3 unitPosition) =>
            GetProjectileOrigin(unitPosition, UnitRole.Melee, bonusSlot: 0, facing: Vector3.forward);

        /// <summary>
        /// Super artillery spawns from the TT muzzle offsets (Bolt_lvl3 / projectile_lvl1),
        /// not the generic body height used for creeps.
        /// </summary>
        public static Vector3 GetProjectileOrigin(
            Vector3 unitPosition,
            UnitRole role,
            int bonusSlot,
            Vector3 facing)
        {
            if (role != UnitRole.Super)
            {
                return unitPosition + Vector3.up * CombatAttackRules.ProjectileBodyHeight;
            }

            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f)
            {
                facing = Vector3.forward;
            }
            else
            {
                facing.Normalize();
            }

            // Authored local offsets from Human_Super / Human_Super_BONUS prefabs (root space).
            if (HumanBonusUnitRules.UsesCatapultSplash(bonusSlot))
            {
                return unitPosition + Vector3.up * 0.80f + facing * -0.87f;
            }

            return unitPosition + Vector3.up * 1.16f + facing * 0.59f;
        }

        public static Vector3 GetProjectileOrigin(MatchUnitState attacker)
        {
            if (attacker == null)
            {
                return Vector3.up * CombatAttackRules.ProjectileBodyHeight;
            }

            return GetProjectileOrigin(
                attacker.WorldPosition,
                attacker.Role,
                attacker.BonusSlot,
                attacker.FacingDirection);
        }

        public static Vector3 GetProjectileTarget(Vector3 unitPosition)
        {
            return unitPosition + Vector3.up * (CombatAttackRules.ProjectileBodyHeight * 0.65f);
        }
    }
}
