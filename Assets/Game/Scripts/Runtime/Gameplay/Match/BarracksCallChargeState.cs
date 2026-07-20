using System;
using Game.Gameplay.Data;

namespace Game.Gameplay.Match
{
    public sealed class BarracksCallChargeState
    {
        public const int CallableRoleCount = 6;

        private readonly int[] _current = new int[CallableRoleCount];
        private readonly int[] _max = new int[CallableRoleCount];
        /// <summary>Active regen countdown per role. 0 = not regenerating. Only one charge regenerates at a time.</summary>
        private readonly float[] _regenRemaining = new float[CallableRoleCount];

        public bool IsInitialized { get; private set; }

        public void Initialize(SquadCompositionDefinition composition)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            Initialize((ISquadCounts)composition);
        }

        public void Initialize(ISquadCounts composition)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            for (var i = 0; i < CallableRoleCount; i++)
            {
                var role = IndexToRole(i);
                var max = BarracksManualCallRules.GetMaxCharges(composition, role);
                _max[i] = max;
                _current[i] = max;
                _regenRemaining[i] = 0f;
            }

            IsInitialized = true;
        }

        public int GetCharges(UnitRole role) => _current[RoleToIndex(role)];

        public int GetMaxCharges(UnitRole role) => _max[RoleToIndex(role)];

        /// <summary>Active regen timer for the role, if any charge is recovering.</summary>
        public bool TryGetNextRegenRemaining(UnitRole role, out float remainingSeconds)
        {
            remainingSeconds = _regenRemaining[RoleToIndex(role)];
            return remainingSeconds > 0f;
        }

        public bool TrySpend(UnitRole role)
        {
            var index = RoleToIndex(role);
            if (_current[index] <= 0)
            {
                return false;
            }

            _current[index]--;
            // Sequential: only start a timer if none is running. Extra missing charges wait in queue.
            if (_regenRemaining[index] <= 0f)
            {
                _regenRemaining[index] = BarracksManualCallRules.RegenSeconds;
            }

            return true;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            for (var i = 0; i < CallableRoleCount; i++)
            {
                if (_current[i] >= _max[i])
                {
                    _regenRemaining[i] = 0f;
                    continue;
                }

                if (_regenRemaining[i] <= 0f)
                {
                    _regenRemaining[i] = BarracksManualCallRules.RegenSeconds;
                }

                _regenRemaining[i] -= deltaTime;
                while (_regenRemaining[i] <= 0f && _current[i] < _max[i])
                {
                    _current[i]++;
                    if (_current[i] < _max[i])
                    {
                        // Next missing charge starts a fresh full cycle (carry overshoot for large ticks).
                        _regenRemaining[i] += BarracksManualCallRules.RegenSeconds;
                    }
                    else
                    {
                        _regenRemaining[i] = 0f;
                    }
                }
            }
        }

        public void OnLevelUp(ISquadCounts newComposition)
        {
            if (newComposition == null)
            {
                throw new ArgumentNullException(nameof(newComposition));
            }

            for (var i = 0; i < CallableRoleCount; i++)
            {
                var role = IndexToRole(i);
                var oldMax = _max[i];
                var newMax = BarracksManualCallRules.GetMaxCharges(newComposition, role);
                _max[i] = newMax;
                _current[i] = Math.Min(_current[i], newMax);

                var delta = newMax - oldMax;
                if (delta > 0)
                {
                    _current[i] = Math.Min(_current[i] + delta, newMax);
                }

                if (_current[i] >= _max[i])
                {
                    _regenRemaining[i] = 0f;
                }
                else if (_regenRemaining[i] <= 0f)
                {
                    _regenRemaining[i] = BarracksManualCallRules.RegenSeconds;
                }
            }

            IsInitialized = true;
        }

        public void OnLevelUp(SquadCompositionDefinition newComposition)
        {
            if (newComposition == null)
            {
                throw new ArgumentNullException(nameof(newComposition));
            }

            OnLevelUp((ISquadCounts)newComposition);
        }

        public void Capture(int[] current, int[] max, float[] nextRegen)
        {
            if (current == null || max == null || nextRegen == null)
            {
                throw new ArgumentNullException();
            }

            if (current.Length < CallableRoleCount
                || max.Length < CallableRoleCount
                || nextRegen.Length < CallableRoleCount)
            {
                throw new ArgumentException("Charge snapshot arrays must hold 6 roles.");
            }

            for (var i = 0; i < CallableRoleCount; i++)
            {
                current[i] = _current[i];
                max[i] = _max[i];
                nextRegen[i] = _regenRemaining[i];
            }
        }

        public void ApplySnapshot(int[] current, int[] max, float[] nextRegen)
        {
            if (current == null || max == null || nextRegen == null)
            {
                throw new ArgumentNullException();
            }

            if (current.Length < CallableRoleCount
                || max.Length < CallableRoleCount
                || nextRegen.Length < CallableRoleCount)
            {
                throw new ArgumentException("Charge snapshot arrays must hold 6 roles.");
            }

            for (var i = 0; i < CallableRoleCount; i++)
            {
                _max[i] = Math.Max(0, max[i]);
                _current[i] = Math.Clamp(current[i], 0, _max[i]);
                if (_current[i] < _max[i])
                {
                    _regenRemaining[i] = nextRegen[i] > 0f
                        ? nextRegen[i]
                        : BarracksManualCallRules.RegenSeconds;
                }
                else
                {
                    _regenRemaining[i] = 0f;
                }
            }

            IsInitialized = true;
        }

        private static int RoleToIndex(UnitRole role)
        {
            if (!BarracksManualCallRules.IsCallableRole(role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "Manual call is not supported for this role.");
            }

            return (int)role;
        }

        private static UnitRole IndexToRole(int index) => (UnitRole)index;
    }
}
