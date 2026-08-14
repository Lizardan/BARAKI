using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Unit markers driven by <see cref="MatchCombatSystem"/>.</summary>
    public sealed class MatchCombatPresenter : MonoBehaviour
    {
        public static MatchCombatPresenter Current { get; private set; }

        sealed class UnitVisual
        {
            public Transform Root;
            public Transform Model;
            public Animator Animator;
            public UnitCombatAnimatorPlayback AnimPlayback = new();
            public UnitWorldStatusBars StatusBars;
            public Collider PickCollider;
            public float GroundRingDiameter;
            public bool HasSpawned;
            public int LastAttackSwingSerial;
            public bool IsFogHidden;
            public Renderer[] CachedRenderers;
            public UnitRole Role;
            public bool IsParkedAtBase;
        }

        sealed class DyingVisual
        {
            public UnitVisual Visual;
            public float TimeRemaining;
        }

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private UnitVisualCatalog _visualCatalog;
        [SerializeField] private MatchFogOfWar _fogOfWar;
        [SerializeField] private MatchFxCatalog _fxCatalog;

        public UnitVisualCatalog VisualCatalog => _visualCatalog;
        [SerializeField] private float _fallbackUnitScale = 2.7f;
        [SerializeField] private float _fallbackUnitHeight = 3.6f;
        [SerializeField] private float _statusBarClearance = 0.5f;
        [SerializeField] private float _unitVisualScale = UnitGreyboxVisuals.Scale;
        [SerializeField] private float _deathVisualSeconds = UnitCombatAnimatorDriver.DeathVisualSeconds;

        readonly Dictionary<int, UnitVisual> _visuals = new();
        readonly List<DyingVisual> _dyingVisuals = new();
        readonly Dictionary<int, Transform> _projectileVisuals = new();
        readonly Dictionary<int, bool> _projectileHitsBuilding = new();
        Transform _root;
        Transform _projectileRoot;

        void Awake()
        {
            Current = this;
            ResolveRuntime();
        }

        void ResolveRuntime()
        {
            if (_runtime == null)
            {
                _runtime = MatchRuntime.Current;
            }

            if (_fogOfWar == null)
            {
                _fogOfWar = GetComponent<MatchFogOfWar>() ?? MatchFogOfWar.Current;
            }

            if (_fxCatalog == null)
            {
                _fxCatalog = Resources.Load<MatchFxCatalog>("Fx/MatchFxCatalog");
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
            if (_runtime.TickMode == MatchTickMode.Client)
            {
                controller.Combat.AdvanceProjectilePresentation(Time.deltaTime);
            }

            SyncVisuals(controller, controller.Combat);
            SyncSpellCasts(controller.Combat);
            SyncHeroAbilityCasts(controller.Combat);
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
            SyncSpellCasts(controller.Combat);
            SyncHeroAbilityCasts(controller.Combat);
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

                var isFirstSpawn = !visual.HasSpawned;
                if (isFirstSpawn
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

                visual.IsParkedAtBase = unit.IsParkedAtBase;

                var facing = unit.FacingDirection;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f)
                {
                    var targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                    // Instant facing only on first visual spawn; otherwise keep smooth turn.
                    visual.Root.rotation = isFirstSpawn
                        ? targetRotation
                        : Quaternion.Slerp(
                            visual.Root.rotation,
                            targetRotation,
                            8f * Time.deltaTime);
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

                ApplyFogVisibility(visual, unit, position);
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
                    if (visual.IsParkedAtBase)
                    {
                        // Parked hero dismissed to lane: vanish without a death animation.
                        DestroyManaged(visual.Root.gameObject);
                    }
                    else
                    {
                        BeginDeath(visual);
                    }
                }

                _visuals.Remove(unitId);
            }
        }

        void ApplyFogVisibility(UnitVisual visual, MatchUnitState unit, Vector3 worldPosition)
        {
            var shouldHide = false;
            if (_fogOfWar != null && _fogOfWar.IsInitialized && !_fogOfWar.FogDisabled)
            {
                var localSlot = MatchNetworkSession.LocalSlot >= 0
                    ? MatchNetworkSession.LocalSlot
                    : (GameSession.ActiveSetup?.LocalPlayerSlot ?? 0);
                if (unit.OwnerSlot != localSlot && !_fogOfWar.IsRevealed(worldPosition))
                {
                    shouldHide = true;
                }
            }

            if (visual.IsFogHidden == shouldHide)
            {
                return;
            }

            visual.IsFogHidden = shouldHide;
            SetUnitVisualHidden(visual, shouldHide);
        }

        void SetUnitVisualHidden(UnitVisual visual, bool hidden)
        {
            if (visual?.Root == null)
            {
                return;
            }

            if (visual.CachedRenderers == null)
            {
                visual.CachedRenderers = visual.Root.GetComponentsInChildren<Renderer>(true);
            }

            foreach (var renderer in visual.CachedRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = !hidden;
                }
            }

            if (visual.StatusBars != null)
            {
                visual.StatusBars.gameObject.SetActive(!hidden);
            }

            if (visual.PickCollider != null)
            {
                visual.PickCollider.enabled = !hidden;
            }
        }

        void DriveAnimator(UnitVisual visual, MatchUnitState unit, MatchCombatSystem combat)
        {
            if (visual.Animator == null)
            {
                return;
            }

            if (visual.IsParkedAtBase)
            {
                UnitCombatAnimatorDriver.TickStand(visual.Animator, visual.AnimPlayback);
                return;
            }

            var fireAttack = false;
            if (unit.AttackSwingSerial != visual.LastAttackSwingSerial)
            {
                visual.LastAttackSwingSerial = unit.AttackSwingSerial;
                fireAttack = unit.AttackSwingSerial > 0;
            }

            UnitCombatAnimatorDriver.Tick(
                visual.Animator,
                visual.AnimPlayback,
                unit.BehaviorState,
                fireAttack,
                fireDeath: false);

            if (fireAttack)
            {
                SpawnMeleeImpactFx(unit, combat);
            }
        }

        void SpawnMeleeImpactFx(MatchUnitState unit, MatchCombatSystem combat)
        {
            if (!CanSpawnFx())
            {
                return;
            }

            if (unit.CurrentTargetBuildingInstanceId.HasValue)
            {
                var building = _runtime?.Controller?.Buildings?.GetByInstanceId(unit.CurrentTargetBuildingInstanceId.Value);
                if (building != null && !building.IsRuins)
                {
                    SpawnFx(_fxCatalog.BuildingImpact, building.WorldPosition, ImpactFxLifetimeSeconds);
                }

                return;
            }

            if (unit.CurrentTargetId.HasValue
                && combat.TryGetUnitWorldPosition(unit.CurrentTargetId.Value, out var targetPosition))
            {
                SpawnFx(_fxCatalog.Blood, targetPosition, BloodFxLifetimeSeconds);
            }
        }

        void BeginDeath(UnitVisual visual)
        {
            UnregisterUnitPick(visual);
            SpawnDeathFx(visual);
            if (visual.StatusBars != null)
            {
                DestroyManaged(visual.StatusBars.gameObject);
                visual.StatusBars = null;
            }

            if (visual.Animator != null)
            {
                UnitCombatAnimatorDriver.Tick(
                    visual.Animator,
                    visual.AnimPlayback,
                    UnitBehaviorState.Attack,
                    fireAttack: false,
                    fireDeath: true);
            }

            _dyingVisuals.Add(new DyingVisual
            {
                Visual = visual,
                TimeRemaining = Mathf.Max(0.1f, _deathVisualSeconds),
            });
        }

        void SpawnDeathFx(UnitVisual visual)
        {
            if (!CanSpawnFx() || visual?.Root == null)
            {
                return;
            }

            if (visual.Role == UnitRole.Super)
            {
                SpawnFx(_fxCatalog.MachineDestroyed, visual.Root.position, MachineFxLifetimeSeconds);
            }
            else
            {
                SpawnFx(_fxCatalog.Blood, visual.Root.position, BloodFxLifetimeSeconds);
            }
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
                && _visualCatalog.TryGetPrefab(raceId, unit.Role, unit.HeroSlot, out var prefab)
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

                scale *= UnitGreyboxVisuals.GetChampionVisualScale(unit.Role);

                // Keep authored prefab normalize and apply presenter scale on top.
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
                Role = unit.Role,
                IsParkedAtBase = unit.IsParkedAtBase,
            };
            AttachUnitPickCollider(unitVisual, unit);
            return unitVisual;
        }

        void SyncProjectiles(MatchCombatSystem combat)
        {
            EnsureProjectileRoot();
            var activeIds = new HashSet<int>();
            _projectileHitsBuilding.Clear();

            foreach (var projectile in combat.Projectiles)
            {
                activeIds.Add(projectile.ProjectileId);
                _projectileHitsBuilding[projectile.ProjectileId] =
                    projectile.TargetBuildingInstanceId.HasValue || projectile.IsBuildingAttack;
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
                    SpawnProjectileImpactFx(projectileId, visual.position);
                    DestroyManaged(visual.gameObject);
                }

                _projectileVisuals.Remove(projectileId);
            }
        }

        void SpawnProjectileImpactFx(int projectileId, Vector3 impactPosition)
        {
            if (!CanSpawnFx())
            {
                return;
            }

            var hitsBuilding = _projectileHitsBuilding.TryGetValue(projectileId, out var building) && building;
            SpawnFx(
                hitsBuilding ? _fxCatalog.BuildingImpact : _fxCatalog.Blood,
                impactPosition,
                hitsBuilding ? ImpactFxLifetimeSeconds : BloodFxLifetimeSeconds);
        }

        void SpawnFx(GameObject prefab, Vector3 position, float lifetimeSeconds)
        {
            if (!CanSpawnFx() || prefab == null)
            {
                return;
            }

            var instance = Instantiate(prefab, position, prefab.transform.rotation);
            if (lifetimeSeconds > 0f)
            {
                Destroy(instance, lifetimeSeconds);
            }
        }

        /// <summary>Plays host + client spell-cast events (label over caster, "+" over target, Frost ring).</summary>
        void SyncSpellCasts(MatchCombatSystem combat)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var cast in combat.ConsumePendingSpellCasts())
            {
                ShowSpellFx(cast);
            }
        }

        void ShowSpellFx(CasterSpellCastEvent cast)
        {
            var color = SpellFxColor(cast.SpellType);
            if (TryGetUnitBarTop(cast.CasterUnitId, out var casterTop))
            {
                SpellFxFactory.CreateLabel(
                    _root,
                    casterTop + Vector3.up * 0.45f,
                    CasterSpellRules.GetDisplayName(cast.SpellType),
                    color);
            }

            switch (cast.SpellType)
            {
                case CasterSpellType.Heal:
                    if (TryGetUnitBarTop(cast.TargetUnitId, out var healTop))
                    {
                        SpellFxFactory.CreatePlus(_root, healTop, HealFxColor);
                    }

                    break;
                case CasterSpellType.Frost:
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, FrostFxColor);
                    break;
                case CasterSpellType.Resurrect:
                    SpellFxFactory.CreatePlus(_root, cast.CenterPosition + Vector3.up * 0.6f, ResurrectFxColor);
                    break;
            }
        }

        /// <summary>Plays hero ability cast events (label over hero, Strike/Ultimate rings, Heal "+").</summary>
        void SyncHeroAbilityCasts(MatchCombatSystem combat)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var cast in combat.ConsumePendingHeroAbilityCasts())
            {
                ShowHeroAbilityFx(cast);
            }
        }

        void ShowHeroAbilityFx(HeroAbilityCastEvent cast)
        {
            var color = HeroAbilityFxColor(cast.Ability);
            if (TryGetUnitBarTop(cast.CasterUnitId, out var casterTop))
            {
                SpellFxFactory.CreateLabel(
                    _root,
                    casterTop + Vector3.up * 0.5f,
                    HeroAbilityRules.GetDisplayName(cast.Ability),
                    color);
            }

            switch (cast.Ability)
            {
                case HeroAbilityType.Heal:
                    if (TryGetUnitBarTop(cast.TargetUnitId, out var healTop))
                    {
                        SpellFxFactory.CreatePlus(_root, healTop, HealFxColor);
                    }

                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, color, 0.85f);
                    break;
                case HeroAbilityType.GreaterHeal:
                    if (TryGetUnitBarTop(cast.TargetUnitId, out var zoneHealTop))
                    {
                        SpellFxFactory.CreatePlus(_root, zoneHealTop, HealFxColor);
                    }

                    SpellFxFactory.CreateRing(
                        _root,
                        cast.CenterPosition,
                        cast.Radius,
                        color,
                        HeroAbilityRules.GreaterHealDurationSeconds,
                        fadeOutNormalized: 0.88f);
                    break;
                case HeroAbilityType.Strike:
                case HeroAbilityType.Slam:
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, StrikeFxColor, 0.75f);
                    break;
                case HeroAbilityType.Ultimate:
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, UltimateFxColor, 1.2f);
                    SpellFxFactory.CreatePlus(_root, cast.CenterPosition + Vector3.up * 1.4f, UltimateFxColor, 1.2f, 0.7f);
                    break;
                case HeroAbilityType.Smite:
                    SpellFxFactory.CreateBurst(_root, cast.CenterPosition, PaladinFxColor);
                    break;
                case HeroAbilityType.Shield:
                case HeroAbilityType.Rally:
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, PaladinFxColor, 1.0f);
                    break;
                case HeroAbilityType.Consecration:
                case HeroAbilityType.Stomp:
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, PaladinFxColor, 1.3f);
                    SpellFxFactory.CreateBurst(_root, cast.CenterPosition, PaladinFxColor, 2.8f);
                    break;
                case HeroAbilityType.HolyNova:
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, PriestFxColor, 0.9f);
                    if (cast.TargetUnitId > 0 && TryGetUnitBarTop(cast.TargetUnitId, out var novaHealTop))
                    {
                        SpellFxFactory.CreatePlus(_root, novaHealTop, HealFxColor);
                    }

                    break;
                case HeroAbilityType.Revive:
                    SpellFxFactory.CreatePlus(_root, cast.CenterPosition + Vector3.up * 0.6f, ResurrectFxColor);
                    SpellFxFactory.CreateRing(_root, cast.CenterPosition, cast.Radius, PriestFxColor, 1.1f);
                    break;
            }
        }

        static Color HeroAbilityFxColor(HeroAbilityType ability)
        {
            return ability switch
            {
                HeroAbilityType.Heal or HeroAbilityType.GreaterHeal => HealFxColor,
                HeroAbilityType.Strike or HeroAbilityType.Slam => StrikeFxColor,
                HeroAbilityType.Ultimate => UltimateFxColor,
                HeroAbilityType.Smite
                    or HeroAbilityType.Shield
                    or HeroAbilityType.Consecration
                    or HeroAbilityType.Rally
                    or HeroAbilityType.Stomp => PaladinFxColor,
                HeroAbilityType.HolyNova or HeroAbilityType.Revive => PriestFxColor,
                _ => Color.white,
            };
        }

        bool TryGetUnitBarTop(int unitId, out Vector3 position)
        {
            position = default;
            if (!_visuals.TryGetValue(unitId, out var visual)
                || visual?.Root == null
                || visual.StatusBars == null)
            {
                return false;
            }

            position = visual.StatusBars.transform.position
                       + Vector3.up * visual.StatusBars.HealthBarTopLocalY;
            return true;
        }

        static Color SpellFxColor(CasterSpellType spellType)
        {
            return spellType switch
            {
                CasterSpellType.Heal => HealFxColor,
                CasterSpellType.Frost => FrostFxColor,
                CasterSpellType.Resurrect => ResurrectFxColor,
                _ => Color.white,
            };
        }

        bool CanSpawnFx() => Application.isPlaying && _fxCatalog != null;

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
            if (Current == this)
            {
                Current = null;
            }
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

        const float BloodFxLifetimeSeconds = 1.2f;
        const float MachineFxLifetimeSeconds = 5f;
        const float ImpactFxLifetimeSeconds = 1.5f;

        static readonly Color HealFxColor = new Color(0.25f, 1f, 0.4f, 1f);
        static readonly Color FrostFxColor = new Color(0.35f, 0.65f, 1f, 1f);
        static readonly Color ResurrectFxColor = new Color(1f, 0.85f, 0.25f, 1f);
        static readonly Color StrikeFxColor = new Color(1f, 0.55f, 0.2f, 1f);
        static readonly Color UltimateFxColor = new Color(1f, 0.3f, 0.2f, 1f);
        static readonly Color PaladinFxColor = new Color(1f, 0.84f, 0.28f, 1f);
        static readonly Color PriestFxColor = new Color(0.78f, 0.92f, 1f, 1f);
    }
}
