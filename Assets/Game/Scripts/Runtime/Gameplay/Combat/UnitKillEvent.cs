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
            UnitRole victimRole)
        {
            KillerOwnerSlot = killerOwnerSlot;
            VictimOwnerSlot = victimOwnerSlot;
            VictimUnitId = victimUnitId;
            GoldGranted = goldGranted;
            VictimRole = victimRole;
        }

        public int KillerOwnerSlot { get; }
        public int VictimOwnerSlot { get; }
        public int VictimUnitId { get; }
        public int GoldGranted { get; }
        public UnitRole VictimRole { get; }
    }
}
