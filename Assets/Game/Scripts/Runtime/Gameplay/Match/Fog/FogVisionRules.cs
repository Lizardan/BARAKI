using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Fog
{
    /// <summary>Pure fog visibility rules (Edit Mode testable).</summary>
    public static class FogVisionRules
    {
        public static bool IsRevealed(
            FogPermanentZones permanent,
            IReadOnlyList<Vector3> localLivingUnitPositions,
            float visionRadius,
            Vector3 worldPosition,
            bool fogDisabled)
        {
            if (fogDisabled)
            {
                return true;
            }

            if (permanent != null && permanent.Contains(worldPosition))
            {
                return true;
            }

            return IsDynamicallyRevealed(localLivingUnitPositions, visionRadius, worldPosition);
        }

        public static bool IsDynamicallyRevealed(
            IReadOnlyList<Vector3> localLivingUnitPositions,
            float visionRadius,
            Vector3 worldPosition)
        {
            if (localLivingUnitPositions == null || localLivingUnitPositions.Count == 0 || visionRadius <= 0f)
            {
                return false;
            }

            var radiusSq = visionRadius * visionRadius;
            var px = worldPosition.x;
            var pz = worldPosition.z;
            for (var i = 0; i < localLivingUnitPositions.Count; i++)
            {
                var unit = localLivingUnitPositions[i];
                var dx = unit.x - px;
                var dz = unit.z - pz;
                if (dx * dx + dz * dz <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Enemy units/buildings in fog cannot be selected. Own targets and revealed enemies can.
        /// </summary>
        public static bool CanSelectTarget(
            int localPlayerSlot,
            int targetOwnerSlot,
            Vector3 targetWorldPosition,
            FogPermanentZones permanent,
            IReadOnlyList<Vector3> localLivingUnitPositions,
            float visionRadius,
            bool fogDisabled)
        {
            if (targetOwnerSlot == localPlayerSlot)
            {
                return true;
            }

            return IsRevealed(
                permanent,
                localLivingUnitPositions,
                visionRadius,
                targetWorldPosition,
                fogDisabled);
        }

        /// <summary>Own units always on minimap; enemies only when revealed / fog off.</summary>
        public static bool ShouldShowUnitOnMinimap(
            int localPlayerSlot,
            int unitOwnerSlot,
            Vector3 unitWorldPosition,
            FogPermanentZones permanent,
            IReadOnlyList<Vector3> localLivingUnitPositions,
            float visionRadius,
            bool fogDisabled)
        {
            return CanSelectTarget(
                localPlayerSlot,
                unitOwnerSlot,
                unitWorldPosition,
                permanent,
                localLivingUnitPositions,
                visionRadius,
                fogDisabled);
        }

        /// <summary>
        /// One-shot VFX / projectile meshes spawn only when the point is revealed, or fog is off.
        /// Simulation is unchanged.
        /// </summary>
        public static bool CanSpawnPresentationFx(bool fogDisabled, bool isRevealed) =>
            fogDisabled || isRevealed;
    }
}
