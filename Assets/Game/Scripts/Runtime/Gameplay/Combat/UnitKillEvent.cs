using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    public readonly struct UnitKillEvent
    {
        public UnitKillEvent(
            int killerOwnerSlot,
            int victimOwnerSlot,
            int victimUnitId,
            int goldGranted,
            UnitRole victimRole,
            int killerUnitId = 0)
        {
            KillerOwnerSlot = killerOwnerSlot;
            VictimOwnerSlot = victimOwnerSlot;
            VictimUnitId = victimUnitId;
            GoldGranted = goldGranted;
            VictimRole = victimRole;
            KillerUnitId = killerUnitId;
        }

        public int KillerOwnerSlot { get; }
        public int VictimOwnerSlot { get; }
        public int VictimUnitId { get; }
        public int GoldGranted { get; }
        public UnitRole VictimRole { get; }
        /// <summary>Unit id of the killer (0 when the killer is a building/tower).</summary>
        public int KillerUnitId { get; }
    }
}
