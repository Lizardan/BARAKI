namespace Game.Gameplay.Combat
{
    public enum UnitBehaviorState
    {
        Move,
        Chase,
        Attack,
        /// <summary>Frost hard-stun: unit cannot move, attack or cast for the remaining freeze time.</summary>
        Frozen,
    }
}
