namespace Game.Gameplay.Match
{
    /// <summary>Per-player titan state: passive research progress, lifecycle and per-barracks death cooldown.</summary>
    public sealed class TitanState
    {
        public TitanLifecycleState State { get; set; } = TitanLifecycleState.Locked;
        /// <summary>Seconds of completed passive research. Freezes (does not reset) while any hero is away.</summary>
        public float ResearchProgressSeconds { get; set; }
        /// <summary>Barracks the titan was last summoned from (death cooldown is per-barracks).</summary>
        public int? LastSummonBarracksInstanceId { get; set; }
        /// <summary>Per-barracks death cooldown: barracks instance id → remaining seconds.</summary>
        readonly BarracksCooldownMap _barracksDeathCooldowns = new();
        public int? DeployedUnitId { get; set; }
        /// <summary>Titan level (per-match, survives death/redeploy). Starts at 1.</summary>
        public int Level { get; set; } = HeroLevelRules.StartingLevel;
        /// <summary>XP progress toward the next level. Carries within the match.</summary>
        public int Xp { get; set; }

        public bool IsUnlocked => State != TitanLifecycleState.Locked;

        public float GetLastBarracksDeathCooldown() =>
            LastSummonBarracksInstanceId.HasValue
                ? GetDeathCooldown(LastSummonBarracksInstanceId.Value)
                : 0f;

        public void RestoreDeathCooldown(int barracksInstanceId, float remainingSeconds)
        {
            _barracksDeathCooldowns.Clear();
            if (barracksInstanceId <= 0)
            {
                LastSummonBarracksInstanceId = null;
                return;
            }

            LastSummonBarracksInstanceId = barracksInstanceId;
            _barracksDeathCooldowns.Set(barracksInstanceId, remainingSeconds);
        }

        /// <summary>Remaining death cooldown for a specific barracks. 0 = summon allowed there.</summary>
        public float GetDeathCooldown(int barracksInstanceId) =>
            _barracksDeathCooldowns.Get(barracksInstanceId);

        public void MarkSummonedFrom(int barracksInstanceId)
        {
            LastSummonBarracksInstanceId = barracksInstanceId;
        }

        /// <summary>Starts the death cooldown on the barracks the titan was last summoned from.</summary>
        public void StartDeathCooldown(float seconds)
        {
            if (LastSummonBarracksInstanceId.HasValue)
            {
                _barracksDeathCooldowns.Set(LastSummonBarracksInstanceId.Value, seconds);
            }
        }

        public void TickCooldowns(float deltaTime) => _barracksDeathCooldowns.Tick(deltaTime);

        /// <summary>
        /// Grants XP and applies any level-ups (handles multi-level jumps).
        /// Match-scoped: progress survives titan death/redeploy, resets on new match.
        /// </summary>
        public void AddXp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Xp += amount;
            while (HeroLevelRules.CanLevelUp(Level, Xp))
            {
                Xp -= HeroLevelRules.XpToNext(Level);
                Level++;
            }
        }
    }
}
