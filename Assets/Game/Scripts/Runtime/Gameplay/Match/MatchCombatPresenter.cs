using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Greybox unit markers driven by <see cref="MatchCombatSystem"/>.</summary>
    public sealed class MatchCombatPresenter : MonoBehaviour
    {
        sealed class UnitVisual
        {
            public Transform Root;
            public Transform Model;
            public UnitWorldStatusBars StatusBars;
            public Collider PickCollider;
            public float GroundRingDiameter;
        }

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private UnitVisualCatalog _visualCatalog;
        [SerializeField] private float _fallbackUnitScale = 2.7f;
        [SerializeField] private float _fallbackUnitHeight = 3.6f;
        [SerializeField] private float _statusBarClearance = 0.5f;
        [SerializeField] private float _unitVisualScale = UnitGreyboxVisuals.Scale;

        readonly Dictionary<int, UnitVisual> _visuals = new();
        readonly Dictionary<int, Transform> _projectileVisuals = new();
        Transform _root;
        Transform _projectileRoot;

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = FindAnyObjectByType<MatchRuntime>();
            }
        }

        void Update()
        {
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

                visual.Root.position = position;

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
                    visual.Model.localPosition = GetMeleeLungeOffset(unit, combat, visual.Root);
                }

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
                    UnregisterUnitPick(visual);
                    Destroy(visual.Root.gameObject);
                }

                _visuals.Remove(unitId);
            }
        }

        UnitVisual CreateUnitVisual(MatchUnitState unit, MatchController controller)
        {
            var raceId = ResolveRaceId(unit, controller);
            var rootObject = new GameObject($"Unit_{unit.UnitId}_{unit.Role}");
            var root = rootObject.transform;
            root.SetParent(_root, false);
            Transform model = null;
            if (_visualCatalog != null
                && _visualCatalog.TryGetPrefab(raceId, unit.Role, out var prefab)
                && prefab != null)
            {
                var instance = Instantiate(prefab, root);
                instance.name = prefab.name;
                instance.transform.localScale = Vector3.one * _unitVisualScale;
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
                    Destroy(visual.gameObject);
                }

                _projectileVisuals.Remove(projectileId);
            }
        }

        static Vector3 GetMeleeLungeOffset(MatchUnitState unit, MatchCombatSystem combat, Transform visualRoot)
        {
            foreach (var strike in combat.MeleeStrikes)
            {
                if (strike.AttackerUnitId != unit.UnitId)
                {
                    continue;
                }

                var target = FindUnit(combat, strike.TargetUnitId);
                if (target == null)
                {
                    return Vector3.zero;
                }

                var toTarget = target.WorldPosition - unit.WorldPosition;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f)
                {
                    return Vector3.zero;
                }

                var amount = Mathf.Sin(strike.Progress * Mathf.PI) * CombatAttackRules.MeleeLungeDistance;
                var worldOffset = toTarget.normalized * amount;
                return visualRoot.InverseTransformDirection(worldOffset);
            }

            return Vector3.zero;
        }

        static MatchUnitState FindUnit(MatchCombatSystem combat, int unitId)
        {
            foreach (var unit in combat.Units)
            {
                if (unit.UnitId == unitId)
                {
                    return unit;
                }
            }

            return null;
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
                Destroy(collider);
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
                    Destroy(pair.Value.Root.gameObject);
                }
            }

            _visuals.Clear();
            ClearProjectileVisuals();

            if (_root != null)
            {
                Destroy(_root.gameObject);
                _root = null;
            }
        }

        void ClearProjectileVisuals()
        {
            foreach (var pair in _projectileVisuals)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _projectileVisuals.Clear();

            if (_projectileRoot != null)
            {
                Destroy(_projectileRoot.gameObject);
                _projectileRoot = null;
            }
        }
    }
}
