using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Payload fired when any unit casts an ability (host-side; presenter listens for VFX/anim).</summary>
    public readonly struct AbilityCastEvent
    {
        public AbilityCastEvent(
            int casterUnitId,
            int ownerSlot,
            UnitAbilityDef def,
            int targetUnitId,
            Vector3 centerPosition,
            float radius,
            int serial = 0)
        {
            CasterUnitId = casterUnitId;
            OwnerSlot = ownerSlot;
            Def = def;
            TargetUnitId = targetUnitId;
            CenterPosition = centerPosition;
            Radius = radius;
            Serial = serial;
        }

        public int CasterUnitId { get; }
        public int OwnerSlot { get; }
        public UnitAbilityDef Def { get; }
        public int TargetUnitId { get; }
        public Vector3 CenterPosition { get; }
        public float Radius { get; }
        /// <summary>Monotonic per-match cast sequence number (host-side).</summary>
        public int Serial { get; }

        public AbilityCastEvent WithSerial(int serial) =>
            new(CasterUnitId, OwnerSlot, Def, TargetUnitId, CenterPosition, Radius, serial);
    }
}
