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
            public UnitBehaviorState LastBehaviorState;
            public float PendingImpactFxSeconds = -1f;
            public bool IsFogHidden;
            public Renderer[] CachedRenderers;
            public UnitRole Role;
            public bool IsParkedAtBase;
            /// <summary>World model scale relative to melee creep (titan ≈ 3).</summary>
            public float LocomotionScaleVsCreep = 1f;
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
            SyncAbilityCasts(controller.Combat);
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
            SyncAbilityCasts(controller.Combat);
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
                TickPendingImpactFx(visual, unit, combat, Time.deltaTime);

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
                visual.LastBehaviorState = UnitBehaviorState.Move;
                return;
            }

            var fireAttack = false;
            if (unit.AttackSwingSerial != visual.LastAttackSwingSerial)
            {
                visual.LastAttackSwingSerial = unit.AttackSwingSerial;
                fireAttack = unit.AttackSwingSerial > 0;
            }

            var enteringCast = unit.BehaviorState == UnitBehaviorState.Cast
                && visual.LastBehaviorState != UnitBehaviorState.Cast;
            visual.LastBehaviorState = unit.BehaviorState;

            var attackInterval = combat.GetAttackIntervalSeconds(unit);
            var attackClipLength = AbilityAnimRules.ResolveAttackClipSeconds(unit.Role, unit.HeroSlot);
            UnitCombatAnimatorDriver.Tick(
                visual.Animator,
                visual.AnimPlayback,
                unit.BehaviorState,
                fireAttack,
                fireDeath: false,
                unit.MarchMoveSpeed,
                visual.LocomotionScaleVsCreep,
                attackInterval,
                attackClipLength,
                enteringCast);

            if (fireAttack
                && CombatAttackRules.UsesMeleeStrike(unit.Role, unit.IsHero, unit.HeroSlot))
            {
                visual.PendingImpactFxSeconds =
                    CombatAttackRules.ResolveSwingImpactDelay(attackInterval, unit.Role);
            }
        }

        void TickPendingImpactFx(
            UnitVisual visual,
            MatchUnitState unit,
            MatchCombatSystem combat,
            float deltaTime)
        {
            if (visual.PendingImpactFxSeconds < 0f)
            {
                return;
            }

            visual.PendingImpactFxSeconds -= deltaTime;
            if (visual.PendingImpactFxSeconds > 0f)
            {
                return;
            }

            visual.PendingImpactFxSeconds = -1f;
            SpawnMeleeImpactFx(unit, combat);
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
                    fireDeath: true,
                    moveSpeed: UnitCombatAnimatorDriver.ReferenceMoveSpeed,
                    visualScaleVsCreep: visual.LocomotionScaleVsCreep);
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
                model = CreateFallbackCapsule(root, unit.OwnerSlot, unit.Role);
            }

            // Measure mesh height from the model only — ignore VFX under root (titan aura, etc.).
            var barHeight = ComputeVisualHeight(root, model) + _statusBarClearance;
            var statusBars = UnitWorldStatusBars.Create(root, barHeight, unit.Stats.HasMana);

            if (unit.Role == UnitRole.Titan)
            {
                AttachTitanDivineAura(root);
            }

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
                LocomotionScaleVsCreep = ResolveLocomotionScaleVsCreep(unit.Role),
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

        /// <summary>Plays ability cast events (label over caster + FX chosen by <see cref="UnitAbilityDef.Fx"/>).</summary>
        void SyncAbilityCasts(MatchCombatSystem combat)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var cast in combat.ConsumePendingAbilityCasts())
            {
                ShowAbilityFx(cast);
            }
        }

        void ShowAbilityFx(AbilityCastEvent cast)
        {
            var def = cast.Def;
            if (def == null || def.Fx.Color.a <= 0f)
            {
                return;
            }

            var fx = def.Fx;
            var color = fx.Color;
            if (TryGetUnitBarTop(cast.CasterUnitId, out var casterTop))
            {
                SpellFxFactory.CreateLabel(_root, casterTop, def.DisplayName, color);
            }

            var duration = fx.DurationSeconds > 0f ? fx.DurationSeconds : DefaultDuration(fx.Kind);
            var center = cast.CenterPosition;
            var radius = cast.Radius;
            switch (fx.Kind)
            {
                case FxKind.Plus:
                    if (cast.TargetUnitId > 0 && TryGetUnitBarTop(cast.TargetUnitId, out var plusTop))
                    {
                        SpellFxFactory.CreatePlus(_root, plusTop, color, duration);
                    }
                    else
                    {
                        SpellFxFactory.CreatePlus(_root, center + Vector3.up * 0.6f, color, duration);
                    }

                    break;
                case FxKind.Ring:
                    SpellFxFactory.CreateRing(_root, center, radius, color, duration, fadeOutNormalized: 0.88f);
                    break;
                case FxKind.Burst:
                    SpellFxFactory.CreateBurst(
                        _root,
                        center,
                        color,
                        height: fx.BurstHeight > 0f ? fx.BurstHeight : 2.4f);
                    break;
                case FxKind.RingPlus:
                    SpellFxFactory.CreateRing(_root, center, radius, color, duration, fadeOutNormalized: 0.88f);
                    if (cast.TargetUnitId > 0 && TryGetUnitBarTop(cast.TargetUnitId, out var ringPlusTop))
                    {
                        SpellFxFactory.CreatePlus(_root, ringPlusTop, color, duration);
                    }
                    else
                    {
                        SpellFxFactory.CreatePlus(_root, center + Vector3.up * 0.6f, color, duration);
                    }

                    break;
                case FxKind.RingBurst:
                    SpellFxFactory.CreateRing(_root, center, radius, color, duration, fadeOutNormalized: 0.88f);
                    SpellFxFactory.CreateBurst(
                        _root,
                        center,
                        color,
                        height: fx.BurstHeight > 0f ? fx.BurstHeight : 2.8f);
                    break;
                case FxKind.SkyBeam:
                    SpellFxFactory.CreateSkyBeam(
                        _root,
                        center,
                        color,
                        height: fx.BurstHeight > 0f ? fx.BurstHeight : 40f,
                        duration: duration,
                        impactRadius: radius > 0f ? radius : 1.8f);
                    SpellFxFactory.CreateLabel(
                        _root,
                        center + Vector3.up * 3.2f,
                        def.DisplayName,
                        color);
                    break;
            }
        }

        static float DefaultDuration(FxKind kind)
        {
            return kind switch
            {
                FxKind.Ring => 1.0f,
                FxKind.RingBurst => 1.3f,
                FxKind.Burst => 0.7f,
                FxKind.SkyBeam => 1.15f,
                _ => 1.1f,
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

            // Local Y follows the pitched billboard so the label sits on the HP strip face.
            const float labelClearance = 0.18f;
            position = visual.StatusBars.transform.TransformPoint(
                0f,
                visual.StatusBars.HealthBarTopLocalY + labelClearance,
                0f);
            return true;
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

        Transform CreateFallbackCapsule(Transform root, int ownerSlot, UnitRole role)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            var champion = UnitGreyboxVisuals.GetChampionVisualScale(role);
            var height = _fallbackUnitHeight * champion;
            var width = _fallbackUnitScale * champion;
            body.transform.localPosition = Vector3.up * (height * 0.5f);
            body.transform.localScale = new Vector3(width, height, width);

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

        void AttachTitanDivineAura(Transform root)
        {
            if (root == null || _fxCatalog == null)
            {
                return;
            }

            // Prefer the smaller building burn; fall back to the full ruin fire.
            var prefab = _fxCatalog.BuildingImpact != null
                ? _fxCatalog.BuildingImpact
                : _fxCatalog.BuildingBurning;
            if (prefab == null)
            {
                return;
            }

            var fx = Instantiate(prefab, root);
            fx.name = "TitanDivineAura";
            fx.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one * UnitGreyboxVisuals.TitanAuraFxScale;
            SoftenLoopingFx(fx);
            EnsureTitanGlowLight(root);
        }

        static void SoftenLoopingFx(GameObject fx)
        {
            if (fx == null)
            {
                return;
            }

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                var main = ps.main;
                main.maxParticles = Mathf.Max(6, main.maxParticles / 3);

                var emission = ps.emission;
                emission.rateOverTimeMultiplier *= 0.4f;
            }

            var lights = fx.GetComponentsInChildren<Light>(true);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null)
                {
                    continue;
                }

                light.intensity *= 0.45f;
                light.range *= 0.7f;
            }

            var audio = fx.GetComponentsInChildren<AudioSource>(true);
            for (var i = 0; i < audio.Length; i++)
            {
                if (audio[i] != null)
                {
                    audio[i].mute = true;
                    audio[i].enabled = false;
                }
            }
        }

        static void EnsureTitanGlowLight(Transform root)
        {
            if (root == null || root.Find("TitanDivineGlow") != null)
            {
                return;
            }

            var glow = new GameObject("TitanDivineGlow");
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.58f, 0.28f, 1f);
            light.intensity = 1.35f;
            light.range = 5.5f;
            light.shadows = LightShadows.None;
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

            var height = Mathf.Max(1f, ComputeVisualHeight(visual.Root, visual.Model));
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

        /// <summary>
        /// World height from <paramref name="feetRoot"/> up to the top of mesh/skinned renderers
        /// under <paramref name="measureFrom"/> (particles/trails excluded).
        /// </summary>
        internal static float ComputeVisualHeight(Transform feetRoot, Transform measureFrom = null)
        {
            return UnitVisualHeight.MeasureAboveFeet(feetRoot, measureFrom);
        }

        /// <summary>
        /// Relative stride size vs melee creep (hero 1.15, titan 3, creeps 1).
        /// </summary>
        static float ResolveLocomotionScaleVsCreep(UnitRole role) =>
            UnitGreyboxVisuals.GetChampionVisualScale(role);

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
    }
}
