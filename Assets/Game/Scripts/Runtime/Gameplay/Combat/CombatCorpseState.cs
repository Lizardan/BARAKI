using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Snapshot of a slain unit kept on the host for a short window (resurrect candidates).
    /// Not serialized into the match snapshot — purely transient host state.
    /// </summary>
    public sealed class CombatCorpseState
    {
        public CombatCorpseState(MatchUnitState unit)
        {
            UnitId = unit.UnitId;
            OwnerSlot = unit.OwnerSlot;
            LaneId = unit.LaneId;
            Role = unit.Role;
            Stats = unit.Stats;
            WorldPosition = unit.WorldPosition;
            MarchFocusOpponentSlot = unit.MarchFocusOpponentSlot;
            Value = unit.Stats.GoldBounty;
            AgeSeconds = 0f;
        }

        public int UnitId { get; }
        public int OwnerSlot { get; }
        public string LaneId { get; }
        public UnitRole Role { get; }
        public UnitCombatStats Stats { get; }
        public Vector3 WorldPosition { get; }
        public int MarchFocusOpponentSlot { get; }
        public int Value { get; }
        public float AgeSeconds { get; set; }
    }
}
