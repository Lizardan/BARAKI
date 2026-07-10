namespace Game.Gameplay.Combat
{
    public sealed class CombatMeleeStrikeState
    {
        public CombatMeleeStrikeState(int attackerUnitId, int targetUnitId, float rawDamage, float duration)
        {
            AttackerUnitId = attackerUnitId;
            TargetUnitId = targetUnitId;
            RawDamage = rawDamage;
            TimeRemaining = duration;
            Duration = duration;
        }

        public int AttackerUnitId { get; }
        public int TargetUnitId { get; }
        public float RawDamage { get; }
        public float Duration { get; }
        public float TimeRemaining { get; set; }

        public float Progress => Duration > 0f ? 1f - TimeRemaining / Duration : 1f;
    }
}
