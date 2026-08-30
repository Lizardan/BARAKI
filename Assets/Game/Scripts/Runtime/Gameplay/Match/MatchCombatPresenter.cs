using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using Game.Gameplay.Vfx;
using UnityEngine;
using UnityEngine.Rendering;

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
            public ParticleSystem[] CachedParticleSystems;
            public UnitRole Role;
            public bool IsParkedAtBase;
            /// <summary>World model scale relative to melee creep (titan ≈ 3).</summary>
            public float LocomotionScaleVsCreep = 1f;
            /// <summary>Persistent CFXR loop under passive-aura bearers. Null = no aura.</summary>
            public GameObject AuraFx;
            public PassiveAuraFxKind AuraFxKind;
            public float AuraFxRadius;
            public int AuraFxAbilityId;
            public GameObject AuraFxSourcePrefab;
            public float AuraFxVisualScale;
            public string AbilityAnimState;
            public int AbilityAnimVariant;
            public bool HasAbilityAnim;
            /// <summary>Loaded bolt/rock meshes on Super artillery (hidden while a swing is committed).</summary>
            public GameObject[] AmmoObjects;
        }

        sealed class DyingVisual
        {
            public UnitVisual Visual;
            public float TimeRemaining;
        }

        /// <summary>Playback state of one expanding Wave of Light cast (MAIN-001).</summary>
        sealed class WaveOfLightFxState
        {
            public float StartRealtime;
            public float MaxRadius;
            public Vector3 Center;
            public Transform Ring;
            public Transform Burst;
            public Vector3 BurstFinalScale;
            public bool Finished;
            public float FinishRealtime;
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
        readonly HashSet<int> _projectileSplashIds = new();
        readonly HashSet<int> _aliveUnitIds = new();
        readonly List<int> _unitsToRemove = new();
        readonly HashSet<int> _aliveProjectileIds = new();
        readonly HashSet<int> _rentedProjectileTrailsCleared = new();
        readonly List<int> _projectilesToRemove = new();
        readonly Stack<GameObject>[] _projectilePools =
        {
            new Stack<GameObject>(),
            new Stack<GameObject>(),
            new Stack<GameObject>(),
            new Stack<GameObject>(),
        };
        readonly Dictionary<int, int> _projectilePoolKind = new();
        readonly List<WaveOfLightFxState> _waveOfLightFxs = new();
        static Material s_waveOfLightRingMaterial;
        const float WaveOfLightTailSeconds = 0.5f;
        static readonly Color WaveOfLightColor = new(1f, 0.84f, 0.28f, 1f);
        Transform _root;
        Transform _projectileRoot;
        static Material s_auraDiscMaterial;
        static Texture2D s_auraGlowTexture;

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
            TickWaveOfLightFxs();
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
            _aliveUnitIds.Clear();

            foreach (var unit in combat.Units)
            {
                _aliveUnitIds.Add(unit.UnitId);
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
                var renderPosition = position;
                var renderBehavior = unit.BehaviorState;
                var renderAttackSwing = unit.AttackSwingSerial;
                Quaternion? renderRotation = null;

                // Snapshot interpolation for smooth presentation (clients + host)
                if (!isFirstSpawn)
                {
                    float renderTime;
                    if (_runtime.TickMode == MatchTickMode.Client)
                    {
                        // Client: server time estimate minus adaptive delay (n=3 or n=4 snapshots)
                        renderTime = ResolveClientRenderTime(controller);
                    }
                    else
                    {
                        // Host: local sim time minus default delay (4 snapshots)
                        var hostDelay = NetworkUnitVisualRules.DefaultInterpSnapshotCount / NetworkUnitVisualRules.SnapshotHz;
                        renderTime = controller.MatchTimeSeconds - hostDelay;
                    }
                    
                    if (combat.TryGetUnitRenderPair(
                            unit.UnitId,
                            renderTime,
                            out var prev,
                            out var next,
                            out var alpha))
                    {
                        renderPosition = Vector3.Lerp(prev.Position, next.Position, alpha);
                        renderRotation = NetworkUnitVisualRules.ResolveRenderFacing(prev.Facing, next.Facing, alpha);
                        if (alpha < 0.5f)
                        {
                            renderBehavior = prev.BehaviorState;
                            renderAttackSwing = prev.AttackSwingSerial;
                        }
                        else
                        {
                            renderBehavior = next.BehaviorState;
                            renderAttackSwing = next.AttackSwingSerial;
                        }
                    }
                }

                // First spawn always snaps to target position
                visual.Root.position = renderPosition;

                visual.HasSpawned = true;
                visual.IsParkedAtBase = unit.IsParkedAtBase;

                if (renderRotation.HasValue)
                {
                    visual.Root.rotation = renderRotation.Value;
                }
                else
                {
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
                }

                if (visual.Model != null)
                {
                    visual.Model.localPosition = UnitGreyboxVisuals.GetModelLocalOffset(unit.Role);
                }

                ApplyFogVisibility(visual, unit, position);
                if (visual.IsFogHidden)
                {
                    if (visual.Animator != null)
                    {
                        visual.Animator.enabled = false;
                    }

                    ClearAuraFx(visual);
                    visual.StatusBars.SetHealth(unit.CurrentHp / unit.Stats.MaxHp);
                    if (unit.Stats.HasMana)
                    {
                        visual.StatusBars.SetMana(unit.CurrentMana / unit.Stats.MaxMana);
                    }

                    continue;
                }

                SyncAuraDisc(visual, unit, combat);
                SyncSuperAmmoVisibility(visual, unit);
                DriveAnimator(visual, unit, combat, renderBehavior, renderAttackSwing);
                TickPendingImpactFx(visual, unit, combat, Time.deltaTime);

                visual.StatusBars.SetHealth(unit.CurrentHp / unit.Stats.MaxHp);
                if (unit.Stats.HasMana)
                {
                    visual.StatusBars.SetMana(unit.CurrentMana / unit.Stats.MaxMana);
                }
            }

            _unitsToRemove.Clear();
            foreach (var pair in _visuals)
            {
                if (!_aliveUnitIds.Contains(pair.Key))
                {
                    _unitsToRemove.Add(pair.Key);
                }
            }

            foreach (var unitId in _unitsToRemove)
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

            if (visual.Animator != null)
            {
                visual.Animator.enabled = !hidden;
            }

            if (visual.CachedParticleSystems == null)
            {
                visual.CachedParticleSystems = visual.Root.GetComponentsInChildren<ParticleSystem>(true);
            }

            foreach (var particles in visual.CachedParticleSystems)
            {
                if (particles == null)
                {
                    continue;
                }

                if (hidden)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                else if (!particles.isPlaying)
                {
                    particles.Play(true);
                }
            }

            if (hidden)
            {
                ClearAuraFx(visual);
            }
        }

        void DriveAnimator(
            UnitVisual visual,
            MatchUnitState unit,
            MatchCombatSystem combat,
            UnitBehaviorState behaviorState,
            int attackSwingSerial)
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
            if (attackSwingSerial != visual.LastAttackSwingSerial)
            {
                visual.LastAttackSwingSerial = attackSwingSerial;
                fireAttack = attackSwingSerial > 0;
            }

            var enteringCast = behaviorState == UnitBehaviorState.Cast
                && visual.LastBehaviorState != UnitBehaviorState.Cast;
            visual.LastBehaviorState = behaviorState;

            if (behaviorState is not UnitBehaviorState.Attack and not UnitBehaviorState.Cast)
            {
                visual.HasAbilityAnim = false;
                visual.AbilityAnimState = null;
            }

            RememberAbilityAnim(visual, unit, combat);

            var hybridMelee = TryResolveCasterBonusHybridMelee(unit, combat, visual);

            float? attackVariant = null;
            if (HumanBonusUnitRules.IsCasterBonus(unit) && !visual.HasAbilityAnim)
            {
                attackVariant = hybridMelee
                    ? HumanCasterBonusWeaponVisuals.MaceAttackVariant
                    : HumanCasterBonusWeaponVisuals.StaffAttackVariant;
            }

            float? authoredAttack = null;
            float? authoredCast = null;
            string stateOverride = null;
            if (visual.HasAbilityAnim)
            {
                stateOverride = visual.AbilityAnimState;
                if (!string.IsNullOrEmpty(visual.AbilityAnimState))
                {
                    var kind = AbilityAnimRules.ResolveKindFromState(visual.AbilityAnimState);
                    if (kind == AbilityAnimKind.Attack)
                    {
                        authoredAttack = visual.AbilityAnimVariant;
                    }
                    else if (kind == AbilityAnimKind.Cast)
                    {
                        authoredCast = visual.AbilityAnimVariant;
                    }
                }
            }

            if (authoredAttack.HasValue)
            {
                attackVariant = authoredAttack;
            }

            var attackInterval = combat.GetAttackIntervalSeconds(unit);
            var attackClipLength = AbilityAnimRules.ResolveAttackClipSeconds(
                unit.Role,
                unit.HeroSlot,
                unit.BonusSlot);
            UnitCombatAnimatorDriver.Tick(
                visual.Animator,
                visual.AnimPlayback,
                behaviorState,
                fireAttack,
                fireDeath: false,
                unit.MarchMoveSpeed,
                visual.LocomotionScaleVsCreep,
                attackInterval,
                attackClipLength,
                enteringCast,
                attackVariant,
                authoredCast,
                stateOverride);

            if (fireAttack
                && (hybridMelee
                    || CombatAttackRules.UsesMeleeStrike(unit.Role, unit.IsHero, unit.HeroSlot)))
            {
                visual.PendingImpactFxSeconds =
                    CombatAttackRules.ResolveSwingImpactDelay(attackInterval, unit.Role);
            }
        }

        void RememberAbilityAnim(UnitVisual visual, MatchUnitState unit, MatchCombatSystem combat)
        {
            if (!string.IsNullOrEmpty(unit.CastLockAnimState))
            {
                visual.HasAbilityAnim = true;
                visual.AbilityAnimState = unit.CastLockAnimState;
                visual.AbilityAnimVariant = unit.CastLockAnimVariant;
                return;
            }

            if (combat == null)
            {
                return;
            }

            var pending = combat.PendingPresenterCasts;
            for (var i = 0; i < pending.Count; i++)
            {
                var cast = pending[i];
                if (cast.CasterUnitId != unit.UnitId || cast.Def == null)
                {
                    continue;
                }

                var state = cast.Def.Fx.AnimState;
                if (string.IsNullOrEmpty(state))
                {
                    continue;
                }

                visual.HasAbilityAnim = true;
                visual.AbilityAnimState = state;
                visual.AbilityAnimVariant = cast.Def.Fx.AnimVariant;
                return;
            }
        }

        static bool TryResolveCasterBonusHybridMelee(
            MatchUnitState unit,
            MatchCombatSystem combat,
            UnitVisual visual)
        {
            if (!HumanBonusUnitRules.IsCasterBonus(unit)
                || combat == null
                || !unit.CurrentTargetId.HasValue)
            {
                return false;
            }

            if (!combat.TryGetUnitWorldPosition(unit.CurrentTargetId.Value, out var targetPosition))
            {
                return false;
            }

            var attackerPosition = visual.Root != null ? visual.Root.position : unit.WorldPosition;
            return HumanBonusUnitRules.IsHybridMeleeNow(unit, attackerPosition, targetPosition);
        }

        float ResolveClientRenderTime(MatchController controller)
        {
            var serverTimeEstimate = controller.MatchTimeSeconds;
            if (_runtime != null && _runtime.LastSnapshotArrivalRealtime >= 0f)
            {
                serverTimeEstimate += Time.time - _runtime.LastSnapshotArrivalRealtime;
            }

            var delay = _runtime != null 
                ? _runtime.AdaptiveInterpDelaySeconds 
                : NetworkUnitVisualRules.DefaultInterpSnapshotCount / NetworkUnitVisualRules.SnapshotHz;
            
            return serverTimeEstimate - delay;
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
                    if (!CanSpawnFx(building.WorldPosition))
                    {
                        return;
                    }

                    SpawnFx(_fxCatalog.BuildingImpact, building.WorldPosition, ImpactFxLifetimeSeconds);
                }

                return;
            }

            if (unit.CurrentTargetId.HasValue
                && combat.TryGetUnitWorldPosition(unit.CurrentTargetId.Value, out var targetPosition)
                && CanSpawnFx(targetPosition))
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
            if (!CanSpawnFx(visual?.Root != null ? visual.Root.position : (Vector3?)null)
                || visual?.Root == null)
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
                && _visualCatalog.TryGetPrefab(
                    raceId,
                    unit.Role,
                    unit.HeroSlot,
                    unit.BonusSlot,
                    out var prefab)
                && prefab != null)
            {
                var instance = Instantiate(prefab, root);
                instance.name = prefab.name;
                animator = instance.GetComponentInChildren<Animator>();
                var scale = UnitGreyboxVisuals.ResolveAnimatedPresenterScale(unit.Role, _unitVisualScale);
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                }

                // Keep authored prefab normalize and apply presenter scale on top.
                instance.transform.localScale = prefab.transform.localScale * scale;
                instance.transform.localPosition = UnitGreyboxVisuals.GetModelLocalOffset(unit.Role);
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
                AttachTitanBodyRays(root);
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
                AmmoObjects = CacheSuperAmmoObjects(model, unit.Role),
            };
            AttachUnitPickCollider(unitVisual, unit);
            return unitVisual;
        }

        static GameObject[] CacheSuperAmmoObjects(Transform model, UnitRole role)
        {
            if (model == null || role != UnitRole.Super)
            {
                return null;
            }

            var list = new List<GameObject>(4);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (IsSuperAmmoTransformName(t.name))
                {
                    list.Add(t.gameObject);
                }
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        static bool IsSuperAmmoTransformName(string name) =>
            name is "Bolt_lvl1" or "Bolt_lvl2" or "Bolt_lvl3"
                or "projectile_lvl1" or "projectile_lvl2" or "projectile_lvl3";

        /// <summary>
        /// While Super has a committed attack swing, hide the carriage ammo so the in-flight
        /// projectile is the only bolt/rock (matches the TT release pose).
        /// </summary>
        static void SyncSuperAmmoVisibility(UnitVisual visual, MatchUnitState unit)
        {
            if (visual?.AmmoObjects == null || unit == null || unit.Role != UnitRole.Super)
            {
                return;
            }

            var visible = unit.AttackCommitRemainingSeconds <= 0f;
            for (var i = 0; i < visual.AmmoObjects.Length; i++)
            {
                var ammo = visual.AmmoObjects[i];
                if (ammo != null && ammo.activeSelf != visible)
                {
                    ammo.SetActive(visible);
                }
            }
        }

        /// <summary>Persistent CFXR aura under a bearer (radius + tint via ability id / v19 color).</summary>
        void SyncAuraDisc(UnitVisual visual, MatchUnitState unit, MatchCombatSystem combat)
        {
            float radius;
            int packedColor;
            int abilityId;
            if (_runtime.TickMode == MatchTickMode.Client)
            {
                radius = unit.AuraRadius;
                packedColor = unit.AuraColorPacked;
                abilityId = unit.AuraAbilityId;
            }
            else if (!combat.TryGetAuraVisual(unit, out radius, out packedColor, out abilityId))
            {
                radius = 0f;
                abilityId = 0;
            }

            if (radius <= 0f || abilityId == 0)
            {
                ClearAuraFx(visual);
                return;
            }

            var kind = PassiveAuraFxRules.ResolveKind(abilityId);
            var authored = ResolveAbilityFx(combat, abilityId);
            var prefab = authored.VfxPrefab != null
                ? authored.VfxPrefab
                : _fxCatalog != null ? _fxCatalog.GetPassiveAuraPrefab(kind) : null;
            var tint = authored.Color.a > 0.01f
                ? authored.Color
                : PassiveAuraFxRules.ResolveTint(abilityId);
            var visualScale = AbilityFx.ResolveScale(authored.Scale);

            var needsRebuild = visual.AuraFx == null
                || visual.AuraFxKind != kind
                || visual.AuraFxAbilityId != abilityId
                || visual.AuraFxSourcePrefab != prefab
                || !Mathf.Approximately(visual.AuraFxRadius, radius)
                || !Mathf.Approximately(visual.AuraFxVisualScale, visualScale);

            if (needsRebuild)
            {
                ClearAuraFx(visual);
                if (prefab == null)
                {
                    return;
                }

                visual.AuraFx = AuraFxVisuals.Attach(visual.Root, prefab, kind, tint, radius, visualScale);
                visual.AuraFxKind = kind;
                visual.AuraFxRadius = radius;
                visual.AuraFxAbilityId = abilityId;
                visual.AuraFxSourcePrefab = prefab;
                visual.AuraFxVisualScale = visualScale;
            }
        }

        static void ClearAuraFx(UnitVisual visual)
        {
            if (visual.AuraFx != null)
            {
                DestroyManaged(visual.AuraFx);
                visual.AuraFx = null;
            }

            visual.AuraFxRadius = 0f;
            visual.AuraFxAbilityId = 0;
            visual.AuraFxSourcePrefab = null;
            visual.AuraFxVisualScale = 0f;
        }

        AbilityFx ResolveAbilityFx(MatchCombatSystem combat, int abilityId)
        {
            var def = combat != null && combat.AbilityCatalog != null
                ? combat.AbilityCatalog.Find(abilityId)
                : null;
            if (def == null)
            {
                MainExtraAbilityFxDefs.TryGet(abilityId, out def);
            }

            return def != null ? def.Fx : default;
        }

        const float AuraDiscFillAlpha = 0.16f;
        /// <summary>Above ground mesh to avoid z-fighting with the floor.</summary>
        const float AuraDiscHeight = 0.12f;
        /// <summary>Transparent+ so the disc draws after opaque ground / roads.</summary>
        const int AuraDiscRenderQueue = (int)RenderQueue.Transparent + 80;
        const int AuraDiscSortingOrder = 32;
        const int AuraGlowTextureSize = 128;
        const string AuraDiscMaterialName = "AuraDiscGlow";

        Transform CreateAuraDisc(Transform parent, float radius)
        {
            var go = new GameObject("AuraDisc");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, AuraDiscHeight, 0f);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetOrBuildAuraGlowQuad(radius * 2f);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetAuraDiscMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = AuraDiscSortingOrder;
            return go.transform;
        }

        static Mesh GetOrBuildAuraGlowQuad(float diameter)
        {
            // One shared unit quad; scale via transform would fight parent unit scale —
            // bake diameter into the mesh so each aura radius stays world-correct.
            var half = diameter * 0.5f;
            var mesh = new Mesh
            {
                name = $"AuraGlowQuad_{diameter:0.##}",
                vertices = new[]
                {
                    new Vector3(-half, 0f, -half),
                    new Vector3(half, 0f, -half),
                    new Vector3(half, 0f, half),
                    new Vector3(-half, 0f, half),
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                },
                // Up-facing (Y+) so the soft glow reads on the ground plane.
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Texture2D GetOrCreateAuraGlowTexture()
        {
            if (s_auraGlowTexture != null)
            {
                return s_auraGlowTexture;
            }

            var size = AuraGlowTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "AuraGlowRadial",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var center = (size - 1) * 0.5f;
            var maxDist = center;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / maxDist;
                    var dy = (y - center) / maxDist;
                    var t = Mathf.Sqrt(dx * dx + dy * dy);
                    // Soft glow: bright core, long transparent falloff to the rim.
                    var falloff = 1f - Mathf.Clamp01(t);
                    var alpha = falloff * falloff * (3f - 2f * falloff); // smoothstep-ish
                    alpha = Mathf.Pow(alpha, 1.35f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            s_auraGlowTexture = tex;
            return s_auraGlowTexture;
        }

        static Material GetAuraDiscMaterial()
        {
            if (s_auraDiscMaterial != null && s_auraDiscMaterial.name == AuraDiscMaterialName)
            {
                return s_auraDiscMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            s_auraDiscMaterial = new Material(shader)
            {
                name = AuraDiscMaterialName,
                hideFlags = HideFlags.HideAndDontSave,
            };
            s_auraDiscMaterial.SetFloat("_Surface", 1f);
            // Soft additive: tinted light bloom instead of an opaque paint disc.
            s_auraDiscMaterial.SetFloat("_Blend", 2f); // Additive (URP Unlit)
            s_auraDiscMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            s_auraDiscMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            s_auraDiscMaterial.SetInt("_ZWrite", 0);
            s_auraDiscMaterial.SetInt("_Cull", (int)CullMode.Off);
            if (s_auraDiscMaterial.HasProperty("_ZTest"))
            {
                s_auraDiscMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            }

            var glow = GetOrCreateAuraGlowTexture();
            if (glow != null && s_auraDiscMaterial.HasProperty("_BaseMap"))
            {
                s_auraDiscMaterial.SetTexture("_BaseMap", glow);
            }

            s_auraDiscMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            s_auraDiscMaterial.DisableKeyword("_ALPHATEST_ON");
            s_auraDiscMaterial.renderQueue = AuraDiscRenderQueue;
            return s_auraDiscMaterial;
        }

        void SyncProjectiles(MatchCombatSystem combat)
        {
            EnsureProjectileRoot();
            _aliveProjectileIds.Clear();
            _rentedProjectileTrailsCleared.Clear();
            _projectileHitsBuilding.Clear();

            foreach (var projectile in combat.Projectiles)
            {
                _aliveProjectileIds.Add(projectile.ProjectileId);
                _projectileHitsBuilding[projectile.ProjectileId] =
                    projectile.TargetBuildingInstanceId.HasValue || projectile.IsBuildingAttack;
                if (projectile.AppliesSplashAoe
                    || (projectile.IsParabolic && projectile.AttackerRole == UnitRole.Super))
                {
                    _projectileSplashIds.Add(projectile.ProjectileId);
                }

                var position = CombatProjectileTrajectory.Evaluate(
                    projectile.StartPosition,
                    projectile.TargetPosition,
                    projectile.ResolvePresentationProgress(),
                    projectile.IsParabolic);
                var revealed = IsPresentationRevealed(position);

                if (!revealed)
                {
                    if (_projectileVisuals.TryGetValue(projectile.ProjectileId, out var hidden)
                        && hidden != null)
                    {
                        RecycleProjectileVisual(projectile.ProjectileId, hidden);
                    }

                    continue;
                }

                if (!_projectileVisuals.TryGetValue(projectile.ProjectileId, out var visual)
                    || visual == null)
                {
                    visual = RentProjectileVisual(projectile).transform;
                    _projectileVisuals[projectile.ProjectileId] = visual;
                }

                CombatAttackVisualBuilder.UpdateProjectileTransform(visual, projectile);
                if (!_rentedProjectileTrailsCleared.Contains(projectile.ProjectileId))
                {
                    // First frame after rent: wipe any trail point recorded at the pooled
                    // position before the teleport above — otherwise the trail draws a
                    // straight line across the map from the old flight.
                    ResetProjectileTrails(visual.gameObject);
                    _rentedProjectileTrailsCleared.Add(projectile.ProjectileId);
                }
            }

            _projectilesToRemove.Clear();
            foreach (var pair in _projectileVisuals)
            {
                if (!_aliveProjectileIds.Contains(pair.Key))
                {
                    _projectilesToRemove.Add(pair.Key);
                }
            }

            foreach (var projectileId in _projectilesToRemove)
            {
                if (_projectileVisuals.TryGetValue(projectileId, out var visual) && visual != null)
                {
                    var impactPosition = visual.position;
                    SpawnProjectileImpactFx(projectileId, impactPosition);
                    RecycleProjectileVisual(projectileId, visual);
                }
                else
                {
                    _projectileVisuals.Remove(projectileId);
                    _projectileSplashIds.Remove(projectileId);
                    _projectilePoolKind.Remove(projectileId);
                }
            }
        }

        GameObject RentProjectileVisual(CombatProjectileState projectile)
        {
            var flaming = IsFlamingArrowsShot(projectile);
            var kind = ResolveProjectilePoolKind(projectile, flaming);
            var pool = _projectilePools[kind];
            while (pool.Count > 0)
            {
                var recycled = pool.Pop();
                if (recycled == null)
                {
                    continue;
                }

                recycled.SetActive(true);
                ResetProjectileTrails(recycled);
                _projectilePoolKind[projectile.ProjectileId] = kind;
                return recycled;
            }

            var created = CombatAttackVisualBuilder.CreateProjectileVisual(
                projectile,
                _projectileRoot,
                flamingArrows: flaming);
            _projectilePoolKind[projectile.ProjectileId] = kind;
            return created;
        }

        /// <summary>Flaming Arrows (PRE-007): ranged/flying shots and living tower shots burn.</summary>
        bool IsFlamingArrowsShot(CombatProjectileState projectile)
        {
            if (projectile.IsBuildingAttack)
            {
                if (!BuildingRules.IsTower(projectile.SourceBuildingId))
                {
                    return false;
                }
            }
            else if (!TowerTrackRules.RoleMatches(0, projectile.AttackerRole))
            {
                return false;
            }

            var players = _runtime?.Controller?.Players;
            if (players == null
                || projectile.AttackerOwnerSlot < 0
                || projectile.AttackerOwnerSlot >= players.Count)
            {
                return false;
            }

            return players[projectile.AttackerOwnerSlot].GetTowerTrackLevel(0) > 0;
        }

        void RecycleProjectileVisual(int projectileId, Transform visual)
        {
            _projectileVisuals.Remove(projectileId);
            _projectileSplashIds.Remove(projectileId);
            if (visual == null)
            {
                _projectilePoolKind.Remove(projectileId);
                return;
            }

            if (!_projectilePoolKind.TryGetValue(projectileId, out var kind))
            {
                kind = 0;
            }

            _projectilePoolKind.Remove(projectileId);
            ResetProjectileTrails(visual.gameObject);
            visual.gameObject.SetActive(false);
            visual.SetParent(_projectileRoot, false);
            _projectilePools[kind].Push(visual.gameObject);
        }

        static int ResolveProjectilePoolKind(CombatProjectileState projectile, bool flamingArrows = false)
        {
            if (projectile.AppliesSplashAoe
                && projectile.AttackerRole == UnitRole.Super
                && !projectile.IsBuildingAttack)
            {
                return 2;
            }

            if (projectile.AttackerRole is UnitRole.Caster or UnitRole.Hero)
            {
                return 1;
            }

            return flamingArrows ? 3 : 0;
        }

        static void ResetProjectileTrails(GameObject visual)
        {
            var trails = visual.GetComponentsInChildren<TrailRenderer>(true);
            for (var i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
            }
        }

        void SpawnProjectileImpactFx(int projectileId, Vector3 impactPosition)
        {
            if (!CanSpawnFx(impactPosition))
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
            if (!CanSpawnFx(position) || prefab == null)
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
                if (cast.Def != null && cast.Def.AbilityId == AbilityIds.MainWaveOfLight)
                {
                    StartWaveOfLightFx(cast);
                    continue;
                }

                ShowAbilityFx(cast);
            }
        }

        /// <summary>
        /// Wave of Light playback: a yellow ground ring grows from the base to the full
        /// cast radius over <see cref="BuildingAbilityRules.WaveOfLightExpandSeconds"/>,
        /// showing the real current front; the frost-style burst scales with it.
        /// </summary>
        void StartWaveOfLightFx(AbilityCastEvent cast)
        {
            if (!IsPresentationRevealed(cast.CenterPosition))
            {
                return;
            }

            EnsureRoot();
            var state = new WaveOfLightFxState
            {
                StartRealtime = Time.time,
                MaxRadius = Mathf.Max(1f, cast.Radius),
                Center = cast.CenterPosition,
            };

            var ringObject = new GameObject("WaveOfLightRing");
            ringObject.transform.SetParent(_root, false);
            var meshFilter = ringObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = SelectionRingMeshBuilder.BuildAnnulus(1f, 0.05f);
            var ringRenderer = ringObject.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = ResolveWaveOfLightRingMaterial();
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
            state.Ring = ringObject.transform;
            state.Ring.position = new Vector3(
                state.Center.x,
                MatchArenaGreyboxBuilder.RoadHeight + 0.03f,
                state.Center.z);

            var prefab = cast.Def.Fx.VfxPrefab;
            if (prefab != null)
            {
                var burst = Instantiate(prefab, _root);
                burst.transform.position = new Vector3(
                    state.Center.x,
                    MatchArenaGreyboxBuilder.RoadHeight + 0.15f,
                    state.Center.z);
                AbilityVfxTint.Apply(
                    burst,
                    cast.Def.Fx.Color.a > 0.01f ? cast.Def.Fx.Color : WaveOfLightColor);
                var baseScale = AbilityVfxPlacement.ResolveOneShotLocalScale(
                    prefab,
                    AbilityFx.ResolveScale(cast.Def.Fx.Scale));
                // Frost's authored size covers IceRingRadius metres — rescale to the wave radius.
                state.BurstFinalScale =
                    baseScale * (state.MaxRadius / BuildingAbilityRules.IceRingRadius);
                state.Burst = burst.transform;
                state.Burst.localScale = baseScale * 0.05f;
                // The burst dies together with the ring: 1s expansion + 0.5s tail.
                Destroy(
                    burst,
                    BuildingAbilityRules.WaveOfLightExpandSeconds
                    + WaveOfLightTailSeconds + 0.05f);
            }

            _waveOfLightFxs.Add(state);
        }

        void TickWaveOfLightFxs()
        {
            for (var i = _waveOfLightFxs.Count - 1; i >= 0; i--)
            {
                var wave = _waveOfLightFxs[i];
                var progress = Mathf.Clamp01(
                    (Time.time - wave.StartRealtime) / BuildingAbilityRules.WaveOfLightExpandSeconds);

                if (!wave.Finished)
                {
                    var radius = Mathf.Max(0.5f, wave.MaxRadius * progress);
                    if (wave.Ring != null)
                    {
                        wave.Ring.localScale = new Vector3(radius, 1f, radius);
                    }

                    if (wave.Burst != null)
                    {
                        wave.Burst.localScale = Vector3.Lerp(
                            wave.BurstFinalScale * 0.05f,
                            wave.BurstFinalScale,
                            progress);
                    }

                    if (progress >= 1f)
                    {
                        wave.Finished = true;
                        wave.FinishRealtime = Time.time;
                    }
                }
                else if (Time.time - wave.FinishRealtime >= WaveOfLightTailSeconds)
                {
                    if (wave.Ring != null)
                    {
                        Destroy(wave.Ring.gameObject);
                    }

                    _waveOfLightFxs.RemoveAt(i);
                }
            }
        }

        Material ResolveWaveOfLightRingMaterial()
        {
            if (s_waveOfLightRingMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    return null;
                }

                s_waveOfLightRingMaterial = new Material(shader);
                s_waveOfLightRingMaterial.SetColor("_BaseColor", WaveOfLightColor);
                s_waveOfLightRingMaterial.SetFloat("_Surface", 0f);
                s_waveOfLightRingMaterial.SetOverrideTag("RenderType", "Opaque");
                s_waveOfLightRingMaterial.SetInt("_Cull", (int)CullMode.Off);
                s_waveOfLightRingMaterial.renderQueue = (int)RenderQueue.Geometry + 11;
            }

            return s_waveOfLightRingMaterial;
        }

        void ShowAbilityFx(AbilityCastEvent cast)
        {
            var def = cast.Def;
            if (def == null || def.Fx.Color.a <= 0f)
            {
                return;
            }

            if (!IsPresentationRevealed(cast.CenterPosition))
            {
                return;
            }

            if (_visuals.TryGetValue(cast.CasterUnitId, out var casterVisual) && casterVisual.IsFogHidden)
            {
                return;
            }

            var color = def.Fx.Color;

            // Label always above caster
            if (TryGetUnitBarTop(cast.CasterUnitId, out var casterTop))
            {
                SpellFxFactory.CreateLabel(_root, casterTop, def.DisplayName, color);
            }

            // VFX prefab
            if (def.Fx.VfxPrefab != null)
            {
                SpawnVfx(
                    def.Fx.VfxPrefab,
                    cast,
                    color,
                    AbilityFx.ResolveScale(def.Fx.Scale),
                    def.Fx.Euler);
            }
        }

        void SpawnVfx(
            GameObject prefab,
            AbilityCastEvent cast,
            Color color,
            float visualScale,
            Vector3 euler)
        {
            var abilityId = cast.Def != null ? cast.Def.AbilityId : 0;
            var authored = cast.Def != null ? cast.Def.Fx.Anchor : AbilityVfxAnchor.Unspecified;
            var anchor = AbilityVfxKindRules.ResolveAnchor(abilityId, authored);
            var casterRoot = TryGetUnitRoot(cast.CasterUnitId, out var caster) ? caster : null;
            var targetRoot = TryGetUnitRoot(cast.TargetUnitId, out var target) ? target : null;

            var instance = UnityEngine.Object.Instantiate(prefab, _root);
            AbilityVfxPlacement.ApplyOneShotTransform(
                instance.transform,
                anchor,
                casterRoot,
                targetRoot,
                cast.CenterPosition,
                euler,
                prefab.transform.rotation,
                _root);
            instance.transform.localScale = AbilityVfxPlacement.ResolveOneShotLocalScale(prefab, visualScale);
            AbilityVfxTint.Apply(instance, color);
            var stun = cast.Def != null ? cast.Def.StunSeconds : 0f;
            Destroy(instance, AbilityVfxPlacement.ResolveOneShotLifetimeSeconds(abilityId, stun));
        }

        bool TryGetUnitBarTop(int unitId, out Vector3 position) => TryGetUnitPosition(unitId, out position);

        bool TryGetUnitRoot(int unitId, out Transform root)
        {
            root = null;
            if (unitId <= 0 || !_visuals.TryGetValue(unitId, out var visual) || visual?.Root == null)
            {
                return false;
            }

            root = visual.Root;
            return true;
        }

        bool TryGetUnitPosition(int unitId, out Vector3 position)
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

        bool CanSpawnFx() => CanSpawnFx(null);

        bool CanSpawnFx(Vector3? worldPosition)
        {
            if (!Application.isPlaying || _fxCatalog == null)
            {
                return false;
            }

            if (!worldPosition.HasValue)
            {
                return true;
            }

            return IsPresentationRevealed(worldPosition.Value);
        }

        bool IsPresentationRevealed(Vector3 worldPosition)
        {
            if (_fogOfWar == null || !_fogOfWar.IsInitialized || _fogOfWar.FogDisabled)
            {
                return true;
            }

            return FogVisionRules.CanSpawnPresentationFx(
                _fogOfWar.FogDisabled,
                _fogOfWar.IsRevealed(worldPosition));
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

        void AttachTitanBodyRays(Transform host)
        {
            if (host == null || _fxCatalog == null || _fxCatalog.AuraRunicLoop == null)
            {
                return;
            }

            AuraFxVisuals.AttachBodyRays(
                host,
                _fxCatalog.AuraRunicLoop,
                AbilityFxColors.AuraMaxHp,
                UnitGreyboxVisuals.ResolveAnimatedPresenterScale(UnitRole.Titan, _unitVisualScale));
            EnsureTitanGlowLight(host);
        }

        static void EnsureTitanGlowLight(Transform host)
        {
            if (host == null || host.Find("TitanDivineGlow") != null)
            {
                return;
            }

            var glow = new GameObject("TitanDivineGlow");
            glow.transform.SetParent(host, false);
            glow.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = AbilityFxColors.AuraMaxHp;
            light.intensity = 1.1f;
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
            _projectilePoolKind.Clear();
            for (var i = 0; i < _projectilePools.Length; i++)
            {
                while (_projectilePools[i].Count > 0)
                {
                    var pooled = _projectilePools[i].Pop();
                    if (pooled != null)
                    {
                        DestroyManaged(pooled);
                    }
                }
            }

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
