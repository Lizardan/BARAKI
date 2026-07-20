using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Idle-at-base hero parking opposite the center barracks (base rear / −Z).</summary>
    public static class HeroParkRules
    {
        public const float RearDistanceFactor = 0.9f;
        public const float SlotSpacing = 1.6f;

        public static Vector3 GetParkWorldPosition(
            MatchArenaLayout layout,
            int ownerSlot,
            int heroSlot,
            float mainToTowerDistance = 8f)
        {
            if (layout == null || ownerSlot < 0 || ownerSlot >= layout.Slots.Count)
            {
                return Vector3.zero;
            }

            var slot = layout.Slots[ownerSlot];
            var main = slot.GetBuildingWorldPosition(GameIds.Buildings.Main);
            var centerBarracks = slot.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            var forward = centerBarracks - main;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            var rear = -forward.normalized;
            var side = Vector3.Cross(Vector3.up, rear).normalized;
            var lateral = (heroSlot - 2) * SlotSpacing;
            return main + rear * (mainToTowerDistance * RearDistanceFactor) + side * lateral;
        }
    }
}
