using System.Collections.Generic;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Ticks pending melee strikes, resolves impacts via callback.
    /// Separated from MatchCombatSystem for clarity.
    /// </summary>
    public sealed class CombatMeleeStrikeSystem
    {
        readonly List<CombatMeleeStrikeState> _active = new();
        readonly List<CombatMeleeStrikeState> _impactBuffer = new();

        public IReadOnlyList<CombatMeleeStrikeState> Active => _active;

        public void Clear()
        {
            _active.Clear();
            _impactBuffer.Clear();
        }

        public void RemoveByOwner(int ownerSlot, System.Func<int, MatchUnitState> getUnitById)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var attacker = getUnitById(_active[i].AttackerUnitId);
                if (attacker != null && attacker.OwnerSlot == ownerSlot)
                {
                    var last = _active.Count - 1;
                    if (i != last) _active[i] = _active[last];
                    _active.RemoveAt(last);
                }
            }
        }

        public void Spawn(CombatMeleeStrikeState strike)
        {
            _active.Add(strike);
        }

        public void Tick(float deltaTime, IMeleeImpactHandler handler)
        {
            if (_active.Count == 0) return;

            _impactBuffer.Clear();
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var strike = _active[i];
                strike.TimeRemaining -= deltaTime;
                if (strike.TimeRemaining > 0f) continue;

                var last = _active.Count - 1;
                if (i != last) _active[i] = _active[last];
                _active.RemoveAt(last);
                _impactBuffer.Add(strike);
            }

            for (var i = 0; i < _impactBuffer.Count; i++)
                handler.ResolveMeleeImpact(_impactBuffer[i]);

            _impactBuffer.Clear();
        }
    }

    public interface IMeleeImpactHandler
    {
        void ResolveMeleeImpact(CombatMeleeStrikeState strike);
    }
}
