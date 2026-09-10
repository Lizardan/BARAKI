using System.Collections.Generic;
using Game.Gameplay.Vfx;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Explosion + lingering ruin fire when buildings fall; hides the upper Model,
    /// keeps the TT mesh Foundation visible (same path for combat kills and main smite).
    /// </summary>
    public sealed class MatchBuildingFxPresenter : MonoBehaviour
    {
        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private MatchFxCatalog _fxCatalog;

        readonly Dictionary<int, bool> _ruinsState = new();
        readonly List<GameObject> _managedFx = new();

        void Awake()
        {
            ResolveRuntime();
            ResolveFxCatalog();
        }

        void Update()
        {
            ResolveRuntime();
            ResolveFxCatalog();

            if (_runtime == null || !_runtime.IsMatchStarted || _runtime.Controller?.Buildings == null)
            {
                return;
            }

            Sync(_runtime.Controller.Buildings);
        }

        void ResolveRuntime()
        {
            if (_runtime == null)
            {
                _runtime = MatchRuntime.Current;
            }
        }

        void ResolveFxCatalog()
        {
            if (_fxCatalog == null)
            {
                _fxCatalog = Resources.Load<MatchFxCatalog>("Fx/MatchFxCatalog");
            }
        }

        void Sync(BuildingRegistry buildings)
        {
            foreach (var building in buildings.Buildings)
            {
                if (_ruinsState.TryGetValue(building.InstanceId, out var wasRuins))
                {
                    if (wasRuins == building.IsRuins)
                    {
                        continue;
                    }

                    _ruinsState[building.InstanceId] = building.IsRuins;
                    if (!building.IsRuins)
                    {
                        continue;
                    }
                }
                else
                {
                    _ruinsState[building.InstanceId] = building.IsRuins;
                    if (!building.IsRuins)
                    {
                        continue;
                    }
                }

                OnBuildingDestroyed(building);
            }
        }

        void OnBuildingDestroyed(BuildingState building)
        {
            if (!Application.isPlaying || _fxCatalog == null)
            {
                return;
            }

            Spawn(building.WorldPosition, _fxCatalog.BuildingDestroyed, ExplosionLifetimeSeconds);
            Spawn(building.WorldPosition, _fxCatalog.BuildingBurning, 0f);
            ApplyRuinsVisual(building);
        }

        void Spawn(Vector3 position, GameObject prefab, float lifetimeSeconds)
        {
            if (prefab == null)
            {
                return;
            }

            var instance = Instantiate(prefab, position, prefab.transform.rotation);
            AbilityFxCameraShakeGuard.Strip(instance);
            if (lifetimeSeconds > 0f)
            {
                Destroy(instance, lifetimeSeconds);
            }
            else
            {
                _managedFx.Add(instance);
            }
        }

        void ApplyRuinsVisual(BuildingState building)
        {
            var visual = MatchArenaGreybox.FindBuildingVisual(building);
            if (visual == null)
            {
                return;
            }

            BuildingRuinsVisual.ApplyRuins(visual, building.BuildingId);
        }

        void ClearFx()
        {
            foreach (var fx in _managedFx)
            {
                if (fx != null)
                {
                    Destroy(fx);
                }
            }

            _managedFx.Clear();
        }

        void OnDisable() => ClearFx();

        const float ExplosionLifetimeSeconds = 6f;
    }
}
