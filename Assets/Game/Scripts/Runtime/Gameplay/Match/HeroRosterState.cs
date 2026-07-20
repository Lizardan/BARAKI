using System;

namespace Game.Gameplay.Match
{
    public sealed class HeroSlotState
    {
        public int HeroSlot { get; }
        public HeroLifecycleState State { get; set; } = HeroLifecycleState.None;
        public float DeathCooldownRemaining { get; set; }
        public int? DeployedUnitId { get; set; }

        public HeroSlotState(int heroSlot)
        {
            HeroSlot = heroSlot;
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
                if (_slots[i].DeathCooldownRemaining > 0f)
                {
                    _slots[i].DeathCooldownRemaining =
                        Math.Max(0f, _slots[i].DeathCooldownRemaining - deltaTime);
                }
            }
        }
    }
}
