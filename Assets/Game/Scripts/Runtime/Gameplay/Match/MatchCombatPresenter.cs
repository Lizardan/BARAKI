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
            public UnitRole Role;
            public bool IsParkedAtBase;
            /// <summary>World model scale relative to melee creep (titan ≈ 3).</summary>
            public float LocomotionScaleVsCreep = 1f;
            /// <summary>Persistent CFXR loop under passive-aura bearers. Null = no aura.</summary>
            public GameObject AuraFx;
            public PassiveAuraFxKind AuraFxKind;
            public float AuraFxRadius;
            public int AuraFxAbilityId;
            /// <summary>Loaded bolt/rock meshes on Super artillery (hidden while a swing is committed).</summary>
            public GameObject[] AmmoObjects;
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
        readonly HashSet<int> _projectileSplashIds = new();
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
                var renderPosition = position;
                var renderBehavior = unit.BehaviorState;
                var renderAttackSwing = unit.AttackSwingSerial;
                Quaternion? renderRotation = null;

                if (_runtime.TickMode == MatchTickMode.Client && !isFirstSpawn)
                {
                    // Snapshot interpolation: sample the authoritative buffer at renderTime
                    // (server time minus a small delay), so 15 Hz snapshots become smooth motion.
                    var renderTime = ResolveClientRenderTime(controller);
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

                if (_runtime.TickMode == MatchTickMode.Client || isFirstSpawn)
                {
                    visual.Root.position = renderPosition;
                }
                else
                {
                    visual.Root.position = NetworkUnitVisualRules.StepToward(
                        visual.Root.position,
                        renderPosition,
                        Time.deltaTime,
                        NetworkUnitVisualRules.HostCatchUpPerSecond);
                }

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

                SyncAuraDisc(visual, unit, combat);
                SyncSuperAmmoVisibility(visual, unit);
                DriveAnimator(visual, unit, combat, renderBehavior, renderAttackSwing);
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

            var hybridMelee = TryResolveCasterBonusHybridMelee(unit, combat, visual);

            float? attackVariant = null;
            if (HumanBonusUnitRules.IsCasterBonus(unit))
            {
                attackVariant = hybridMelee
                    ? HumanCasterBonusWeaponVisuals.MaceAttackVariant
                    : HumanCasterBonusWeaponVisuals.StaffAttackVariant;
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
                attackVariant);

            if (fireAttack
                && (hybridMelee
                    || CombatAttackRules.UsesMeleeStrike(unit.Role, unit.IsHero, unit.HeroSlot)))
            {
                visual.PendingImpactFxSeconds =
                    CombatAttackRules.ResolveSwingImpactDelay(attackInterval, unit.Role);
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

            return serverTimeEstimate - NetworkUnitVisualRules.ClientInterpDelaySeconds;
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
                abilityId = radius > 0f
                    ? PassiveAuraFxRules.ResolveAbilityIdFromColor(AbilityFx.FromRgbaInt(packedColor))
                    : 0;
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
            var needsRebuild = visual.AuraFx == null
                || visual.AuraFxKind != kind
                || visual.AuraFxAbilityId != abilityId
                || !Mathf.Approximately(visual.AuraFxRadius, radius);

            if (needsRebuild)
            {
                ClearAuraFx(visual);
                var prefab = _fxCatalog != null ? _fxCatalog.GetPassiveAuraPrefab(kind) : null;
                if (prefab == null)
                {
                    return;
                }

                var tint = PassiveAuraFxRules.ResolveTint(abilityId);
                visual.AuraFx = AuraFxVisuals.Attach(visual.Root, prefab, kind, tint, radius);
                visual.AuraFxKind = kind;
                visual.AuraFxRadius = radius;
                visual.AuraFxAbilityId = abilityId;
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
            var activeIds = new HashSet<int>();
            _projectileHitsBuilding.Clear();

            foreach (var projectile in combat.Projectiles)
            {
                activeIds.Add(projectile.ProjectileId);
                _projectileHitsBuilding[projectile.ProjectileId] =
                    projectile.TargetBuildingInstanceId.HasValue || projectile.IsBuildingAttack;
                if (projectile.AppliesSplashAoe
                    || (projectile.IsParabolic && projectile.AttackerRole == UnitRole.Super))
                {
                    _projectileSplashIds.Add(projectile.ProjectileId);
                }

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
                    var impactPosition = visual.position;
                    SpawnProjectileImpactFx(projectileId, impactPosition);
                    if (_projectileSplashIds.Contains(projectileId))
                    {
                        SpawnCatapultSplashDisc(impactPosition);
                    }

                    DestroyManaged(visual.gameObject);
                }

                _projectileVisuals.Remove(projectileId);
                _projectileSplashIds.Remove(projectileId);
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

        void SpawnCatapultSplashDisc(Vector3 impactPosition)
        {
            if (!CanSpawnFx() || _root == null)
            {
                return;
            }

            var disc = CreateAuraDisc(_root, HumanBonusUnitRules.CatapultAoeRadius);
            if (disc == null)
            {
                return;
            }

            disc.name = "CatapultSplashDisc";
            disc.SetParent(_root, true);
            disc.position = new Vector3(impactPosition.x, AuraDiscHeight, impactPosition.z);
            disc.localRotation = Quaternion.identity;
            disc.localScale = Vector3.one;

            var renderer = disc.GetComponent<Renderer>();
            if (renderer != null)
            {
                var color = AbilityFxColors.Ultimate;
                color.a *= AuraDiscFillAlpha;
                var block = new MaterialPropertyBlock();
                block.SetColor(Shader.PropertyToID("_BaseColor"), color);
                block.SetColor(Shader.PropertyToID("_Color"), color);
                renderer.SetPropertyBlock(block);
            }

            Destroy(disc.gameObject, HumanBonusUnitRules.CatapultSplashDiscSeconds);
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
