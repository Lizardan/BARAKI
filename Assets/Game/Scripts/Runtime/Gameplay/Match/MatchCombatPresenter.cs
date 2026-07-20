using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Unit markers driven by <see cref="MatchCombatSystem"/>.</summary>
    public sealed class MatchCombatPresenter : MonoBehaviour
    {
        sealed class UnitVisual
        {
            public Transform Root;
            public Transform Model;
            public Animator Animator;
            public UnitWorldStatusBars StatusBars;
            public Collider PickCollider;
            public float GroundRingDiameter;
            public bool HasSpawned;
            public readonly HashSet<int> TriggeredMeleeStrikeKeys = new();
            public readonly HashSet<int> TriggeredProjectileIds = new();
        }

        sealed class DyingVisual
        {
            public UnitVisual Visual;
            public float TimeRemaining;
        }

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private UnitVisualCatalog _visualCatalog;

        public UnitVisualCatalog VisualCatalog => _visualCatalog;
        [SerializeField] private float _fallbackUnitScale = 2.7f;
        [SerializeField] private float _fallbackUnitHeight = 3.6f;
        [SerializeField] private float _statusBarClearance = 0.5f;
        [SerializeField] private float _unitVisualScale = UnitGreyboxVisuals.Scale;
        [SerializeField] private float _deathVisualSeconds = UnitCombatAnimatorDriver.DeathVisualSeconds;

        readonly Dictionary<int, UnitVisual> _visuals = new();
        readonly List<DyingVisual> _dyingVisuals = new();
        readonly Dictionary<int, Transform> _projectileVisuals = new();
        Transform _root;
        Transform _projectileRoot;

        void Awake()
        {
            ResolveRuntime();
        }

        void ResolveRuntime()
        {
            if (_runtime == null)
            {
                _runtime = FindAnyObjectByType<MatchRuntime>();
            }
        }

        void Update()
        {
            ResolveRuntime();
            if (_runtime == null || !_runtime.IsMatchStarted)
            {
                return;
            }

            var controller = _runtime.Controller;
            if (controller?.Graph == null)
            {
                return;
            }

            EnsureRoot();
            SyncVisuals(controller, controller.Combat);
            SyncProjectiles(controller.Combat);
            TickDyingVisuals(Time.deltaTime);
        }

        /// <summary>Runs one presenter sync tick (Edit Mode tests / tooling).</summary>
        public void SyncNow()
        {
            ResolveRuntime();
            if (_runtime == null || !_runtime.IsMatchStarted)
            {
                return;
            }

            var controller = _runtime.Controller;
            if (controller?.Graph == null)
            {
                return;
            }

            EnsureRoot();
            SyncVisuals(controller, controller.Combat);
            SyncProjectiles(controller.Combat);
            TickDyingVisuals(0f);
        }

        void EnsureProjectileRoot()
        {
            if (_projectileRoot != null)
            {
                return;
            }

            var projectileObject = new GameObject("CombatProjectiles");
            _projectileRoot = projectileObject.transform;
            _projectileRoot.SetParent(transform, false);
        }

        void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            var rootObject = new GameObject("CombatUnits");
            _root = rootObject.transform;
            _root.SetParent(transform, false);
        }

        void SyncVisuals(MatchController controller, MatchCombatSystem combat)
        {
            var aliveIds = new HashSet<int>();

            foreach (var unit in combat.Units)
            {
                aliveIds.Add(unit.UnitId);
                if (!combat.TryGetUnitWorldPosition(unit, out var position))
                {
                    continue;
                }

                if (!_visuals.TryGetValue(unit.UnitId, out var visual))
                {
                    visual = CreateUnitVisual(unit, controller);
                    _visuals[unit.UnitId] = visual;
                }

                if (!visual.HasSpawned
                    || !NetworkUnitVisualRules.ShouldLerpPositions(_runtime.TickMode))
                {
                    visual.Root.position = position;
                    visual.HasSpawned = true;
                }
                else
                {
                    visual.Root.position = NetworkUnitVisualRules.StepToward(
                        visual.Root.position,
                        position,
                        Time.deltaTime);
                }

                var facing = unit.FacingDirection;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f)
                {
                    var targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                    visual.Root.rotation = Quaternion.Slerp(
                        visual.Root.rotation,
                        targetRotation,
                        12f * Time.deltaTime);
                }

                if (visual.Model != null)
                {
                    visual.Model.localPosition = UnitGreyboxVisuals.GetModelLocalOffset(unit.Role);
                }

                DriveAnimator(visual, unit, combat);

                visual.StatusBars.SetHealth(unit.CurrentHp / unit.Stats.MaxHp);
                if (unit.Stats.HasMana)
                {
                    visual.StatusBars.SetMana(unit.CurrentMana / unit.Stats.MaxMana);
                }
            }

            var toRemove = new List<int>();
            foreach (var pair in _visuals)
            {
                if (!aliveIds.Contains(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }

            foreach (var unitId in toRemove)
            {
                if (_visuals.TryGetValue(unitId, out var visual) && visual?.Root != null)
                {
                    BeginDeath(visual);
                }

                _visuals.Remove(unitId);
            }
        }

        void DriveAnimator(UnitVisual visual, MatchUnitState unit, MatchCombatSystem combat)
        {
            if (visual.Animator == null)
            {
                return;
            }

            visual.Animator.SetFloat(
                UnitCombatAnimatorDriver.SpeedParam,
                UnitCombatAnimatorDriver.ResolveSpeed(unit.BehaviorState));

            foreach (var strike in combat.MeleeStrikes)
            {
                if (strike.AttackerUnitId != unit.UnitId)
                {
                    continue;
                }

                var key = RuntimeHelpers.GetHashCode(strike);
                if (visual.TriggeredMeleeStrikeKeys.Add(key))
                {
                    visual.Animator.SetTrigger(UnitCombatAnimatorDriver.AttackParam);
                }
            }

            foreach (var projectile in combat.Projectiles)
            {
                if (projectile.AttackerUnitId != unit.UnitId)
                {
                    continue;
                }

                if (visual.TriggeredProjectileIds.Add(projectile.ProjectileId))
                {
                    visual.Animator.SetTrigger(UnitCombatAnimatorDriver.AttackParam);
                }
            }
        }

        void BeginDeath(UnitVisual visual)
        {
            UnregisterUnitPick(visual);
            if (visual.StatusBars != null)
            {
                DestroyManaged(visual.StatusBars.gameObject);
                visual.StatusBars = null;
            }

            if (visual.Animator != null)
            {
                visual.Animator.SetTrigger(UnitCombatAnimatorDriver.DeathParam);
            }

            _dyingVisuals.Add(new DyingVisual
            {
                Visual = visual,
                TimeRemaining = Mathf.Max(0.1f, _deathVisualSeconds),
            });
        }

        void TickDyingVisuals(float deltaTime)
        {
            for (var i = _dyingVisuals.Count - 1; i >= 0; i--)
            {
                var dying = _dyingVisuals[i];
                dying.TimeRemaining -= deltaTime;
                if (dying.TimeRemaining > 0f)
                {
                    continue;
                }

                if (dying.Visual?.Root != null)
                {
                    DestroyManaged(dying.Visual.Root.gameObject);
                }

                _dyingVisuals.RemoveAt(i);
            }
        }

        UnitVisual CreateUnitVisual(MatchUnitState unit, MatchController controller)
        {
            var raceId = ResolveRaceId(unit, controller);
            var rootObject = new GameObject($"Unit_{unit.UnitId}_{unit.Role}");
            var root = rootObject.transform;
            root.SetParent(_root, false);
            Transform model = null;
            Animator animator = null;
            if (_visualCatalog != null
                && _visualCatalog.TryGetPrefab(raceId, unit.Role, out var prefab)
                && prefab != null)
            {
                var instance = Instantiate(prefab, root);
                instance.name = prefab.name;
                animator = instance.GetComponentInChildren<Animator>();
                var scale = _unitVisualScale;
                if (animator != null)
                {
                    scale *= UnitGreyboxVisuals.AnimatedHumanScaleFactor;
                    animator.applyRootMotion = false;
                }

                // Keep authored prefab normalize (WC3 meshes) and apply presenter scale on top.
                instance.transform.localScale = prefab.transform.localScale * scale;
                UnitVisualAccent.ApplyTeamColor(instance.transform, MatchPlayerColors.GetSlotColor(unit.OwnerSlot));
                model = instance.transform;
            }
            else
            {
                model = CreateFallbackCapsule(root, unit.OwnerSlot);
            }

            var barHeight = ComputeVisualHeight(root) + _statusBarClearance;
            var statusBars = UnitWorldStatusBars.Create(root, barHeight, unit.Stats.HasMana);
            statusBars.SetHealth(1f);
            if (unit.Stats.HasMana)
            {
                statusBars.SetMana(1f);
            }

            var unitVisual = new UnitVisual
            {
                Root = root,
                Model = model,
                Animator = animator,
                StatusBars = statusBars,
                GroundRingDiameter = MatchPickFootprint.GetModelFootprintDiameter(model),
            };
            AttachUnitPickCollider(unitVisual, unit);
            return unitVisual;
        }

        void SyncProjectiles(MatchCombatSystem combat)
        {
            EnsureProjectileRoot();
            var activeIds = new HashSet<int>();

            foreach (var projectile in combat.Projectiles)
            {
                activeIds.Add(projectile.ProjectileId);
                if (!_projectileVisuals.TryGetValue(projectile.ProjectileId, out var visual)
                    || visual == null)
                {
                    var visualObject = CombatAttackVisualBuilder.CreateProjectileVisual(projectile, _projectileRoot);
                    visual = visualObject.transform;
                    _projectileVisuals[projectile.ProjectileId] = visual;
                }

                CombatAttackVisualBuilder.UpdateProjectileTransform(visual, projectile);
            }

            var toRemove = new List<int>();
            foreach (var pair in _projectileVisuals)
            {
                if (!activeIds.Contains(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }

            foreach (var projectileId in toRemove)
            {
                if (_projectileVisuals.TryGetValue(projectileId, out var visual) && visual != null)
                {
                    DestroyManaged(visual.gameObject);
                }

                _projectileVisuals.Remove(projectileId);
            }
        }

        static string ResolveRaceId(MatchUnitState unit, MatchController controller)
        {
            var players = controller.Players;
            if (unit.OwnerSlot >= 0 && unit.OwnerSlot < players.Count)
            {
                return players[unit.OwnerSlot].RaceId;
            }

            return GameIds.Races.Human;
        }

        Transform CreateFallbackCapsule(Transform root, int ownerSlot)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            body.transform.localPosition = Vector3.up * (_fallbackUnitHeight * 0.5f);
            body.transform.localScale = new Vector3(_fallbackUnitScale, _fallbackUnitHeight, _fallbackUnitScale);

            var collider = body.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyManaged(collider);
            }

            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor(Shader.PropertyToID("_BaseColor"), MatchPlayerColors.GetSlotColor(ownerSlot));
                renderer.SetPropertyBlock(block);
            }

            return body.transform;
        }

        void AttachUnitPickCollider(UnitVisual visual, MatchUnitState unit)
        {
            if (visual?.Root == null || _runtime == null)
            {
                return;
            }

            var bridge = _runtime.GetComponent<MatchSelectionBridge>();
            if (bridge == null)
            {
                return;
            }

            var height = Mathf.Max(1f, ComputeVisualHeight(visual.Root));
            var collider = MatchPickColliderUtility.EnsurePickCollider(
                visual.Root.gameObject,
                new Vector3(0f, height * 0.5f, 0f),
                new Vector3(1.8f, height, 1.8f));

            var handle = visual.Root.GetComponent<MatchPickHandle>();
            if (handle == null)
            {
                handle = visual.Root.gameObject.AddComponent<MatchPickHandle>();
            }

            handle.ConfigureUnit(unit.UnitId);
            bridge.RegisterPickCollider(collider, MatchPickTarget.Unit(unit.UnitId));
            visual.PickCollider = collider;
        }

        void UnregisterUnitPick(UnitVisual visual)
        {
            if (visual?.PickCollider == null || _runtime == null)
            {
                return;
            }

            var bridge = _runtime.GetComponent<MatchSelectionBridge>();
            bridge?.UnregisterPickCollider(visual.PickCollider);
            visual.PickCollider = null;
        }

        public bool TryGetUnitGroundRing(int unitId, out Vector3 center, out float diameter)
        {
            center = default;
            diameter = MatchPickFootprint.DefaultUnitDiameter * MatchPickFootprint.RingMargin;

            if (!_visuals.TryGetValue(unitId, out var visual) || visual?.Root == null)
            {
                return false;
            }

            center = visual.Root.position;
            diameter = visual.GroundRingDiameter > 0f
                ? visual.GroundRingDiameter
                : MatchPickFootprint.DefaultUnitDiameter * MatchPickFootprint.RingMargin;
            return true;
        }

        static float ComputeVisualHeight(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return 1.8f;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.max.y - root.position.y;
        }

        void OnDisable()
        {
            ClearVisuals();
        }

        public void ClearVisuals()
        {
            foreach (var pair in _visuals)
            {
                if (pair.Value?.Root != null)
                {
                    DestroyManaged(pair.Value.Root.gameObject);
                }
            }

            _visuals.Clear();

            foreach (var dying in _dyingVisuals)
            {
                if (dying.Visual?.Root != null)
                {
                    DestroyManaged(dying.Visual.Root.gameObject);
                }
            }

            _dyingVisuals.Clear();
            ClearProjectileVisuals();

            if (_root != null)
            {
                DestroyManaged(_root.gameObject);
                _root = null;
            }
        }

        void ClearProjectileVisuals()
        {
            foreach (var pair in _projectileVisuals)
            {
                if (pair.Value != null)
                {
                    DestroyManaged(pair.Value.gameObject);
                }
            }

            _projectileVisuals.Clear();

            if (_projectileRoot != null)
            {
                DestroyManaged(_projectileRoot.gameObject);
                _projectileRoot = null;
            }
        }

        static void DestroyManaged(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
