namespace Game.Gameplay.Combat
{
    public enum UnitBehaviorState
    {
        Move,
        Chase,
        Attack,
        /// <summary>Frost hard-stun: unit cannot move, attack or cast for the remaining freeze time.</summary>
        Frozen,
        /// <summary>
        /// Ability cast wind-up: unit holds still until <see cref="MatchUnitState.CastLockRemainingSeconds"/>
        /// expires so the Cast clip finishes before locomotion / auto-attack resume.
        /// </summary>
        Cast,
    }
}
