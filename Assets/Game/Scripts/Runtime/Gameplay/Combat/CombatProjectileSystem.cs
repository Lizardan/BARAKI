using System.Collections.Generic;
using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Ticks in-flight projectiles, resolves impacts via callback.
    /// Separated from MatchCombatSystem for clarity.
    /// </summary>
    public sealed class CombatProjectileSystem
    {
        readonly List<CombatProjectileState> _active = new();
        readonly List<CombatProjectileState> _impactBuffer = new();
        int _nextId = 1;

        public IReadOnlyList<CombatProjectileState> Active => _active;

        public void Clear()
        {
            _active.Clear();
            _impactBuffer.Clear();
        }

        public void RemoveByOwner(int ownerSlot)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].AttackerOwnerSlot == ownerSlot)
                {
                    var last = _active.Count - 1;
                    if (i != last) _active[i] = _active[last];
                    _active.RemoveAt(last);
                }
            }
        }

        public CombatProjectileState Spawn(
            int attackerUnitId,
            int targetUnitId,
            int attackerOwnerSlot,
            UnitRole attackerRole,
            string raceId,
            float rawDamage,
            float flightDuration,
            UnityEngine.Vector3 start,
            UnityEngine.Vector3 end,
            bool isParabolic,
            bool appliesSplashAoe = false)
        {
            var projectile = new CombatProjectileState(
                _nextId++,
                attackerUnitId,
                targetUnitId,
                attackerOwnerSlot,
                attackerRole,
                raceId,
                rawDamage,
                flightDuration,
                start,
                end,
                isParabolic,
                appliesSplashAoe: appliesSplashAoe);
            _active.Add(projectile);
            return projectile;
        }

        public CombatProjectileState SpawnBuildingAttack(
            int attackerUnitId,
            int targetBuildingInstanceId,
            int attackerOwnerSlot,
            UnitRole attackerRole,
            string raceId,
            float rawDamage,
            float flightDuration,
            UnityEngine.Vector3 start,
            UnityEngine.Vector3 end,
            bool isParabolic = false,
            bool appliesSplashAoe = false)
        {
            var projectile = new CombatProjectileState(
                _nextId++,
                attackerUnitId,
                targetUnitId: -1,
                attackerOwnerSlot,
                attackerRole,
                raceId,
                rawDamage,
                flightDuration,
                start,
                end,
                isParabolic,
                targetBuildingInstanceId,
                appliesSplashAoe: appliesSplashAoe);
            _active.Add(projectile);
            return projectile;
        }

        public CombatProjectileState SpawnFromBuilding(
            int targetUnitId,
            int ownerSlot,
            string raceId,
            float rawDamage,
            float flightDuration,
            UnityEngine.Vector3 start,
            UnityEngine.Vector3 end,
            int sourceBuildingInstanceId,
            string sourceBuildingId)
        {
            var projectile = new CombatProjectileState(
                _nextId++,
                attackerUnitId: 0,
                targetUnitId,
                ownerSlot,
                UnitRole.Ranged,
                raceId,
                rawDamage,
                flightDuration,
                start,
                end,
                isParabolic: false,
                targetBuildingInstanceId: null,
                sourceBuildingInstanceId: sourceBuildingInstanceId,
                sourceBuildingId: sourceBuildingId);
            _active.Add(projectile);
            return projectile;
        }

        public void Tick(float deltaTime, IProjectileImpactHandler handler)
        {
            if (_active.Count == 0) return;

            _impactBuffer.Clear();
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var projectile = _active[i];
                projectile.Elapsed += deltaTime;
                if (projectile.Elapsed < projectile.FlightDuration) continue;

                var last = _active.Count - 1;
                if (i != last) _active[i] = _active[last];
                _active.RemoveAt(last);
                _impactBuffer.Add(projectile);
            }

            for (var i = 0; i < _impactBuffer.Count; i++)
                handler.ResolveProjectileImpact(_impactBuffer[i]);

            _impactBuffer.Clear();
        }

        /// <summary>
        /// Client visuals: advance flight without damage; remove finished shots so presenter can play impact FX.
        /// </summary>
        public void AdvancePresentation(float deltaTime)
        {
            if (deltaTime <= 0f || _active.Count == 0)
            {
                return;
            }

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var projectile = _active[i];
                projectile.Elapsed += deltaTime;
                if (projectile.Elapsed < projectile.FlightDuration)
                {
                    continue;
                }

                var last = _active.Count - 1;
                if (i != last)
                {
                    _active[i] = _active[last];
                }

                _active.RemoveAt(last);
            }
        }

        /// <summary>Client: append a spawn-event projectile for local presentation only.</summary>
        public void AddPresentation(CombatProjectileState projectile)
        {
            if (projectile == null)
            {
                return;
            }

            _active.Add(projectile);
            if (projectile.ProjectileId >= _nextId)
            {
                _nextId = projectile.ProjectileId + 1;
            }
        }
    }

    public interface IProjectileImpactHandler
    {
        void ResolveProjectileImpact(CombatProjectileState projectile);
    }
}
