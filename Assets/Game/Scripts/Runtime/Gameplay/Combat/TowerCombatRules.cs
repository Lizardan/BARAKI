using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Tower fire / manual targeting rules from Buildings.md.</summary>
    public static class TowerCombatRules
    {
        public const float Range = 12f;
        public const float DamageMin = 18f;
        public const float DamageMax = 24f;
        public const float AttackSpeed = 0.8f;
        public const float ScanInterval = 0.25f;
        public const float ProjectileSpeed = 26f;
        public const float MuzzleHeight = 2.4f;
        public const float ProjectileCubeScale = 0.45f;

        public static float GetAttackIntervalSeconds() =>
            CombatRules.GetAttackIntervalSeconds(AttackSpeed);

        public static bool IsInRange(Vector3 towerPosition, Vector3 targetPosition) =>
            HorizontalDistance(towerPosition, targetPosition) <= Range;

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static bool CanTargetUnit(int towerOwnerSlot, MatchUnitState unit) =>
            unit != null
            && unit.IsAlive
            && unit.OwnerSlot != towerOwnerSlot;
    }
}
