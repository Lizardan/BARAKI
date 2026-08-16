using System.Collections.Generic;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Holds projectile auto-attacks until mid-swing, then hands them to the host for spawn.
    /// </summary>
    public sealed class CombatPendingProjectileSystem
    {
        readonly List<CombatPendingProjectileState> _active = new();
        readonly List<CombatPendingProjectileState> _readyBuffer = new();

        public IReadOnlyList<CombatPendingProjectileState> Active => _active;

        public void Clear()
        {
            _active.Clear();
            _readyBuffer.Clear();
        }

        public void Spawn(CombatPendingProjectileState pending)
        {
            _active.Add(pending);
        }

        public void Tick(float deltaTime, IPendingProjectileHandler handler)
        {
            if (_active.Count == 0)
            {
                return;
            }

            _readyBuffer.Clear();
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var pending = _active[i];
                pending.TimeRemaining -= deltaTime;
                if (pending.TimeRemaining > 0f)
                {
                    continue;
                }

                var last = _active.Count - 1;
                if (i != last)
                {
                    _active[i] = _active[last];
                }

                _active.RemoveAt(last);
                _readyBuffer.Add(pending);
            }

            for (var i = 0; i < _readyBuffer.Count; i++)
            {
                handler.ReleasePendingProjectile(_readyBuffer[i]);
            }

            _readyBuffer.Clear();
        }
    }

    public sealed class CombatPendingProjectileState
    {
        public CombatPendingProjectileState(
            int attackerUnitId,
            int targetUnitId,
            float rawDamage,
            float delaySeconds,
            int? targetBuildingInstanceId = null,
            UnityEngine.Vector3 aimWorldPosition = default)
        {
            AttackerUnitId = attackerUnitId;
            TargetUnitId = targetUnitId;
            TargetBuildingInstanceId = targetBuildingInstanceId;
            RawDamage = rawDamage;
            TimeRemaining = delaySeconds;
            AimWorldPosition = aimWorldPosition;
        }

        public int AttackerUnitId { get; }
        public int TargetUnitId { get; }
        public int? TargetBuildingInstanceId { get; }
        public float RawDamage { get; }
        public float TimeRemaining { get; set; }
        /// <summary>Locked aim at BeginAttack; used when the target dies mid-swing.</summary>
        public UnityEngine.Vector3 AimWorldPosition { get; }
    }

    public interface IPendingProjectileHandler
    {
        void ReleasePendingProjectile(CombatPendingProjectileState pending);
    }
}
