using Game.Gameplay.Combat;

namespace Game.Gameplay.Match
{
    /// <summary>Maps combat behavior to Animator parameter values for unit visuals.</summary>
    public static class UnitCombatAnimatorDriver
    {
        public const string SpeedParam = "Speed";
        public const string AttackParam = "Attack";
        public const string DeathParam = "Death";
        public const float DeathVisualSeconds = 3.2f;

        public static float ResolveSpeed(UnitBehaviorState behaviorState) =>
            behaviorState is UnitBehaviorState.Move or UnitBehaviorState.Chase ? 1f : 0f;
    }
}
