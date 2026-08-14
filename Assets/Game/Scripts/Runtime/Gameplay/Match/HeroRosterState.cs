using System;
using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    public sealed class HeroSlotState
    {
        public int HeroSlot { get; }
        public HeroLifecycleState State { get; set; } = HeroLifecycleState.None;
        /// <summary>Barracks this hero was last deployed from (death cooldown is per-barracks).</summary>
        public int? LastDeployBarracksInstanceId { get; set; }
        /// <summary>Per-barracks death cooldown: barracks instance id → remaining seconds.</summary>
        readonly Dictionary<int, float> _barracksDeathCooldowns = new();
        public int? DeployedUnitId { get; set; }
        /// <summary>Hero level (per-match, survives death/redeploy). Starts at 1.</summary>
        public int Level { get; set; } = HeroLevelRules.StartingLevel;
        /// <summary>XP progress toward the next level. Carries within the match.</summary>
        public int Xp { get; set; }

        public HeroSlotState(int heroSlot)
        {
            HeroSlot = heroSlot;
        }

        /// <summary>Remaining death cooldown for a specific barracks. 0 = deploy allowed there.</summary>
        public float GetDeathCooldown(int barracksInstanceId) =>
            _barracksDeathCooldowns.TryGetValue(barracksInstanceId, out var remaining)
                ? remaining
                : 0f;

        public float GetLastBarracksDeathCooldown() =>
            LastDeployBarracksInstanceId.HasValue
                ? GetDeathCooldown(LastDeployBarracksInstanceId.Value)
                : 0f;

        public void RestoreDeathCooldown(int barracksInstanceId, float remainingSeconds)
        {
            _barracksDeathCooldowns.Clear();
            if (barracksInstanceId <= 0)
            {
                LastDeployBarracksInstanceId = null;
                return;
            }

            LastDeployBarracksInstanceId = barracksInstanceId;
            if (remainingSeconds > 0f)
            {
                _barracksDeathCooldowns[barracksInstanceId] = remainingSeconds;
            }
        }

        public void MarkDeployedFrom(int barracksInstanceId)
        {
            LastDeployBarracksInstanceId = barracksInstanceId;
        }

        /// <summary>Starts the death cooldown on the barracks this hero was last deployed from.</summary>
        public void StartDeathCooldown(float seconds)
        {
            if (LastDeployBarracksInstanceId.HasValue)
            {
                _barracksDeathCooldowns[LastDeployBarracksInstanceId.Value] = seconds;
            }
        }

        public void TickCooldowns(float deltaTime)
        {
            var expired = default(List<int>);
            foreach (var pair in _barracksDeathCooldowns)
            {
                var remaining = pair.Value - deltaTime;
                if (remaining <= 0f)
                {
                    (expired ??= new List<int>()).Add(pair.Key);
                }
                else
                {
                    _barracksDeathCooldowns[pair.Key] = remaining;
                }
            }

            if (expired == null)
            {
                return;
            }

            foreach (var barracksId in expired)
            {
                _barracksDeathCooldowns.Remove(barracksId);
            }
        }

        /// <summary>
        /// Grants XP and applies any level-ups (handles multi-level jumps).
        /// Match-scoped: progress survives hero death/redeploy, resets on new match.
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

    /// <summary>Per-player hero slots (1..3).</summary>
    public sealed class HeroRosterState
    {
        readonly HeroSlotState[] _slots;

        public HeroRosterState()
        {
            _slots = new HeroSlotState[HeroRules.MaxHeroSlots];
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new HeroSlotState(i + 1);
            }
        }

        public HeroSlotState Get(int heroSlot)
        {
            if (!HeroRules.IsValidHeroSlot(heroSlot))
            {
                throw new ArgumentOutOfRangeException(nameof(heroSlot));
            }

            return _slots[heroSlot - 1];
        }

        public int CountHired()
        {
            var count = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State != HeroLifecycleState.None)
                {
                    count++;
                }
            }

            return count;
        }

        public void Tick(float deltaTime)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i].TickCooldowns(deltaTime);
            }
        }
    }
}
