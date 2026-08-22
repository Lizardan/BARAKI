using System;
using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// One live PreviewRenderUtility: kit caster model, melee target, floor, orbit camera.
    /// </summary>
    public sealed class AbilityFxPreviewSession : IDisposable
    {
        public const int DefaultSize = 512;
        const float MaxDeltaSeconds = 0.05f;
        const float CasterX = -1.45f;
        const float TargetX = 1.45f;
        const float CameraFocusY = 1.05f;
        const float MinCameraDistance = 6.5f;
        const float MaxCameraDistance = 52f;
        const float DefaultCameraDistance = 7.4f;
        const float DefaultYaw = 38f;
        const float DefaultPitch = 32f;
        const float UnityPlaneSize = 10f;

        readonly PreviewRenderUtility _preview;
        readonly GameObject _ground;
        readonly GameObject _casterRoot;
        readonly GameObject _targetRoot;
        readonly GameObject _keyLight;
        readonly GameObject _fillLight;
        readonly Material _groundMat;
        readonly GameObject _mechanicRoot;
        readonly LineRenderer _mechanicLine;
        readonly Transform _mechanicDisc;
        readonly Material _mechanicLineMat;
        readonly Material _mechanicFillMat;
        readonly UnitCombatAnimatorPlayback _casterPlayback = new();
        readonly UnitCombatAnimatorPlayback _targetPlayback = new();
        RenderTexture _targetRt;
        GameObject _casterModel;
        GameObject _casterPrefab;
        GameObject _targetModel;
        GameObject _targetPrefab;
        bool _targetIsBuilding;
        bool _soloTarget;
        bool _ruinsApplied;
        bool _ruinFxNeedRestart;
        readonly List<GameObject> _ruinFx = new();
        GameObject _instance;
        GameObject _vfxPrefab;
        Animator _casterAnimator;
        Animator _targetAnimator;
        UnitRole _casterRole;
        int _casterHeroSlot;
        int _casterBonusSlot;
        Color _tint;
        AbilityVfxKind _kind;
        AbilityVfxAnchor _anchor;
        AbilityAnimKind _animKind;
        string _animState;
        int _animVariant;
        float _visualScale = 1f;
        Vector3 _euler;
        int _abilityId = int.MinValue;
        float _gameplayRadius;
        float _stunSeconds;
        float _oneShotLifetime = AbilityVfxPlacement.CastLifetimeSeconds;
        float _elapsed;
        float _yaw = DefaultYaw;
        float _pitch = DefaultPitch;
        float _distance = DefaultCameraDistance;
        int _framedAbilityId = int.MinValue;
        float _framedRadius = -1f;
        bool _loop;
        bool _paused;
        bool _stopped;
        bool _hasVfxGraph;
        bool _particlesNeedRestart;
        bool _disposed;

        public AbilityFxPreviewSession()
        {
            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 32f;
            _preview.camera.nearClipPlane = 0.05f;
            _preview.camera.farClipPlane = 140f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            _preview.camera.allowHDR = true;
            _preview.lights[0].intensity = 1.35f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            _preview.lights[1].intensity = 0.7f;
            _preview.lights[1].transform.rotation = Quaternion.Euler(20f, 140f, 0f);
            _preview.ambientColor = new Color(0.45f, 0.45f, 0.48f, 1f);
            AbilityVfxPreviewPlayback.ConfigureCamera(_preview.camera);
            AbilityVfxPreviewPlayback.RetainCullingScope();

            _targetRt = CreateRt(DefaultSize, DefaultSize);
            _preview.camera.targetTexture = _targetRt;

            _groundMat = CreateUnlitMaterial(new Color(0.18f, 0.18f, 0.2f, 1f));
            _ground = CreateGround(_groundMat);
            _casterRoot = CreateRoot("FxPreviewCasterRoot", CasterX);
            _targetRoot = CreateRoot("FxPreviewTargetRoot", TargetX);
            CreateMechanicGizmo(out _mechanicRoot, out _mechanicLine, out _mechanicDisc, out _mechanicLineMat, out _mechanicFillMat);
            _keyLight = CreateDirectionalLight(
                "FxPreviewKeyLight",
                1.25f,
                Quaternion.Euler(40f, -30f, 0f));
            _fillLight = CreateDirectionalLight(
                "FxPreviewFillLight",
                0.55f,
                Quaternion.Euler(25f, 140f, 0f));
        }

        Color PreviewTint => _tint.a > 0.01f ? _tint : Color.white;

        public Vector3 CasterLabelWorld =>
            _casterRoot != null
                ? _casterRoot.transform.position + Vector3.up * 2.35f
                : Vector3.zero;

        public Vector3 DummyLabelWorld =>
            _targetRoot != null
                ? _targetRoot.transform.position + Vector3.up * 2.35f
                : Vector3.zero;

        public bool IsPaused => _paused;
        public bool IsStopped => _stopped;
        public bool HasEffect => _instance != null;
        public bool IsLooping => _loop;
        public float PlaybackDuration { get; private set; } = AbilityVfxPreviewPlayback.OneShotLoopSeconds;
        public float PlaybackElapsed => _elapsed;
        public float PlaybackProgress01 =>
            PlaybackDuration > 0.001f ? Mathf.Clamp01(_elapsed / PlaybackDuration) : 0f;

        public Vector3 AnchorMarkerWorld(AbilityVfxAnchor anchor)
        {
            var caster = _casterRoot != null
                ? _casterRoot.transform.position
                : new Vector3(CasterX, N4PerimeterLaneGeometry.LaneHeight, 0f);
            var target = _targetRoot != null
                ? _targetRoot.transform.position
                : new Vector3(TargetX, N4PerimeterLaneGeometry.LaneHeight, 0f);
            return anchor switch
            {
                AbilityVfxAnchor.Ground => AbilityVfxPlacement.SnapGroundY(caster),
                AbilityVfxAnchor.Target => target + Vector3.up * AbilityVfxPlacement.BodyHeight,
                AbilityVfxAnchor.Impact => AbilityVfxPlacement.SnapGroundY(target),
                _ => caster + Vector3.up * AbilityVfxPlacement.BodyHeight,
            };
        }

        public void SetCaster(
            GameObject prefab,
            UnitRole role,
            int heroSlot,
            int bonusSlot,
            bool isBuilding,
            bool hideCaster = false)
        {
            if (_disposed)
            {
                return;
            }

            _casterRole = role;
            _casterHeroSlot = heroSlot;
            _casterBonusSlot = bonusSlot;
            ApplySoloLayout(hideCaster);
            if (hideCaster)
            {
                if (_casterModel != null)
                {
                    DestroyGo(_casterModel);
                    _casterModel = null;
                    _casterPrefab = null;
                    _casterAnimator = null;
                    _casterPlayback.CurrentStateName = null;
                    _casterPlayback.IsDead = false;
                }

                return;
            }

            if (_casterPrefab == prefab && _casterModel != null)
            {
                return;
            }

            _casterPrefab = prefab;
            DestroyGo(_casterModel);
            _casterModel = null;
            _casterAnimator = null;
            _casterPlayback.CurrentStateName = null;
            _casterPlayback.IsDead = false;
            _casterModel = SpawnUnitModel(_casterRoot, prefab, role, isBuilding);
            _casterAnimator = ConfigureAnimator(_casterModel, _casterPlayback, playStand: false);
            FaceEachOther();
        }

        public void SetTarget(GameObject prefab, bool isBuilding = false)
        {
            if (_disposed)
            {
                return;
            }

            if (_targetPrefab == prefab && _targetIsBuilding == isBuilding && _targetModel != null)
            {
                return;
            }

            ClearRuinFx();
            _ruinsApplied = false;
            _targetPrefab = prefab;
            _targetIsBuilding = isBuilding;
            DestroyGo(_targetModel);
            _targetModel = null;
            _targetAnimator = null;
            _targetPlayback.CurrentStateName = null;
            _targetPlayback.IsDead = false;
            _targetModel = SpawnUnitModel(
                _targetRoot,
                prefab,
                isBuilding ? UnitRole.Hero : UnitRole.Melee,
                isBuilding);
            _targetAnimator = ConfigureAnimator(_targetModel, _targetPlayback, playStand: true);
            FaceEachOther();
        }

        public void SetEffect(
            GameObject prefab,
            Color tint,
            AbilityVfxKind kind,
            AbilityVfxAnchor anchor,
            int abilityId,
            AbilityAnimKind animKind,
            float gameplayRadius = 0f,
            float stunSeconds = 0f,
            string animState = null,
            int animVariant = 0,
            float visualScale = 0f,
            Vector3 euler = default)
        {
            if (_disposed)
            {
                return;
            }

            var scale = AbilityFx.ResolveScale(visualScale);
            var sameParams =
                _vfxPrefab == prefab
                && _kind == kind
                && _anchor == anchor
                && _abilityId == abilityId
                && _animKind == animKind
                && _animState == animState
                && _animVariant == animVariant
                && Mathf.Abs(_visualScale - scale) < 0.001f
                && (_euler - euler).sqrMagnitude < 0.0001f
                && Mathf.Abs(_gameplayRadius - gameplayRadius) < 0.001f
                && Mathf.Abs(_stunSeconds - stunSeconds) < 0.001f;
            if (_stopped)
            {
                // Radius/stun touch the ring and lifetime, not the FX instance: while stopped,
                // keep the effect hidden and just refresh the ring.
                var sameFx =
                    _vfxPrefab == prefab
                    && _kind == kind
                    && _anchor == anchor
                    && _abilityId == abilityId
                    && _animKind == animKind
                    && _animState == animState
                    && _animVariant == animVariant
                    && Mathf.Abs(_visualScale - scale) < 0.001f
                    && (_euler - euler).sqrMagnitude < 0.0001f;
                if (sameFx)
                {
                    _tint = tint;
                    _gameplayRadius = gameplayRadius;
                    _stunSeconds = stunSeconds;
                    UpdateMechanicRing();
                    return;
                }

                _stopped = false;
            }

            var sameVisual = sameParams && _instance != null;
            if (sameVisual && Approximately(tint, _tint))
            {
                UpdateMechanicRing();
                return;
            }

            _vfxPrefab = prefab;
            _kind = kind;
            _anchor = anchor;
            _abilityId = abilityId;
            _animKind = animKind;
            _animState = animState;
            _animVariant = animVariant;
            _visualScale = scale;
            _euler = euler;
            _gameplayRadius = gameplayRadius;
            _stunSeconds = stunSeconds;
            _oneShotLifetime = AbilityVfxPlacement.ResolveOneShotLifetimeSeconds(abilityId, stunSeconds);
            if (sameVisual)
            {
                _tint = tint;
                AbilityVfxTint.Apply(_instance, PreviewTint);
                UpdateMechanicRing();
                return;
            }

            _tint = tint;
            Rebuild();
            UpdateMechanicRing();
        }

        public void Replay()
        {
            _stopped = false;
            _paused = false;
            Rebuild();
        }

        /// <summary>Freeze frame: animators, particles and VFX Graph stop ticking, orbit/zoom stay live.</summary>
        public void SetPaused(bool paused) => _paused = paused;

        /// <summary>Remove the effect instance; the mechanic ring stays for radius tuning.
        /// SetEffect does not respawn until any authored parameter changes.</summary>
        public void StopEffect()
        {
            _stopped = true;
            _paused = false;
            _elapsed = 0f;
            ClearRuinFx();
            RestoreTargetIntact();
            _ruinsApplied = false;
            ClearInstance();
        }

        /// <summary>Reset orbit and let UpdateMechanicRing re-frame distance from the AoE radius.</summary>
        public void FocusCamera()
        {
            _yaw = DefaultYaw;
            _pitch = DefaultPitch;
            _distance = DefaultCameraDistance;
            _framedAbilityId = int.MinValue;
            _framedRadius = -1f;
        }

        public void Orbit(float deltaYaw, float deltaPitch)
        {
            _yaw += deltaYaw * 0.35f;
            _pitch = Mathf.Clamp(_pitch - deltaPitch * 0.35f, 8f, 75f);
        }

        public void Zoom(float scrollDelta)
        {
            _distance = Mathf.Clamp(_distance - scrollDelta * 0.35f, MinCameraDistance, MaxCameraDistance);
        }

        public void EnsureSize(int width, int height)
        {
            if (_disposed)
            {
                return;
            }

            width = Mathf.Clamp(width, 256, 1400);
            height = Mathf.Clamp(height, 256, 1400);
            if (_targetRt != null && _targetRt.width == width && _targetRt.height == height)
            {
                return;
            }

            _preview.camera.targetTexture = null;
            if (_targetRt != null)
            {
                Object.DestroyImmediate(_targetRt);
            }

            _targetRt = CreateRt(width, height);
            _preview.camera.targetTexture = _targetRt;
            RenderToTarget();
        }

        public bool TryWorldToGui(Vector3 world, Rect previewRect, out Vector2 gui)
        {
            gui = default;
            if (_disposed || _targetRt == null)
            {
                return false;
            }

            var screen = _preview.camera.WorldToScreenPoint(world);
            if (screen.z <= 0.01f)
            {
                return false;
            }

            gui = new Vector2(
                previewRect.x + screen.x / _targetRt.width * previewRect.width,
                previewRect.yMax - screen.y / _targetRt.height * previewRect.height);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_disposed || deltaTime <= 0f)
            {
                return;
            }

            if (_paused)
            {
                RenderToTarget();
                return;
            }

            var dt = Mathf.Min(deltaTime, MaxDeltaSeconds);
            if (_casterAnimator != null)
            {
                _casterAnimator.Update(dt);
            }

            if (_targetAnimator != null)
            {
                _targetAnimator.Update(dt);
            }

            if (_instance != null)
            {
                _elapsed += dt;
                SimulateParticles(_instance, dt, _particlesNeedRestart, PreviewTint);
                _particlesNeedRestart = false;
                TickBuildingCollapse(dt);
                if (!_loop
                    && _hasVfxGraph
                    && !AbilityFxPreviewTargetRules.PreviewBuildingCollapse(_abilityId))
                {
                    if (_elapsed >= AbilityVfxPreviewPlayback.OneShotLoopSeconds)
                    {
                        AbilityVfxPreviewPlayback.PlayVisualEffects(_instance, PreviewTint);
                        _elapsed = 0f;
                    }
                }
                else if (!_loop && _elapsed >= LoopAfterSeconds)
                {
                    Rebuild();
                }
            }

            RenderToTarget();
        }

        public Texture Target => _disposed || _targetRt == null ? Texture2D.blackTexture : _targetRt;

        public void RenderToTarget()
        {
            if (_disposed || _targetRt == null)
            {
                return;
            }

            PlaceCamera();
            AbilityVfxPreviewPlayback.Render(_preview, _targetRt);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AbilityVfxPreviewPlayback.ReleaseCullingScope();
            ClearRuinFx();
            ClearInstance();
            DestroyGo(_casterModel);
            DestroyGo(_targetModel);
            DestroyGo(_casterRoot);
            DestroyGo(_targetRoot);
            DestroyGo(_ground);
            DestroyGo(_mechanicRoot);
            DestroyGo(_keyLight);
            DestroyGo(_fillLight);
            DestroyMat(_groundMat);
            DestroyMat(_mechanicLineMat);
            DestroyMat(_mechanicFillMat);

            if (_targetRt != null)
            {
                _preview.camera.targetTexture = null;
                Object.DestroyImmediate(_targetRt);
                _targetRt = null;
            }

            _preview.Cleanup();
        }

        float LoopAfterSeconds =>
            AbilityFxPreviewTargetRules.PreviewBuildingCollapse(_abilityId)
                ? AbilityFxPreviewTargetRules.BuildingSmiteRuinDelaySeconds
                    + AbilityFxPreviewTargetRules.BuildingSmiteRuinsHoldSeconds
                : _oneShotLifetime;

        void Rebuild()
        {
            ClearRuinFx();
            RestoreTargetIntact();
            _ruinsApplied = false;
            ClearInstance();
            _elapsed = 0f;
            _loop = _kind == AbilityVfxKind.Aura;
            _particlesNeedRestart = true;
            SpawnVfx();
            _hasVfxGraph = _instance != null && AbilityVfxPreviewPlayback.HasVisualEffect(_instance);
            PlaybackDuration = AbilityFxPreviewTargetRules.PreviewBuildingCollapse(_abilityId)
                ? LoopAfterSeconds
                : !_loop && _hasVfxGraph
                    ? AbilityVfxPreviewPlayback.OneShotLoopSeconds
                    : Mathf.Max(0.05f, _oneShotLifetime);
            FireAnim();
            RenderToTarget();
        }

        void UpdateMechanicRing()
        {
            if (_disposed || _mechanicRoot == null)
            {
                return;
            }

            var mechanic = AbilityFxMechanicRules.Resolve(_abilityId, _gameplayRadius);
            var show = mechanic.ShowRing && mechanic.Radius > 0.01f;
            _mechanicRoot.SetActive(show);
            if (!show)
            {
                FitGround(0f);
                FrameCameraToMechanic(mechanic, 0f);
                return;
            }

            var previewR = AbilityFxMechanicRules.PreviewRingRadius(mechanic.Radius);
            FitGround(previewR);
            FrameCameraToMechanic(mechanic, previewR);
            var host = mechanic.RingHost == AbilityVfxAnchor.Target
                || mechanic.RingHost == AbilityVfxAnchor.Impact
                    ? _targetRoot
                    : _casterRoot;
            if (host == null)
            {
                _mechanicRoot.SetActive(false);
                return;
            }

            var localY = mechanic.FlatOnGround
                ? AbilityVfxPlacement.GroundY - host.transform.position.y
                : mechanic.FollowsHost
                    ? 0.07f
                    : 0.04f;
            _mechanicRoot.transform.SetParent(host.transform, false);
            _mechanicRoot.transform.localPosition = new Vector3(0f, localY, 0f);
            _mechanicRoot.transform.localRotation = Quaternion.identity;
            _mechanicRoot.transform.localScale = Vector3.one;

            var tint = mechanic.ChipColor;
            if (_tint.a > 0.01f)
            {
                tint = Color.Lerp(tint, _tint, 0.4f);
            }

            tint.a = 1f;
            if (_mechanicLine != null)
            {
                const int points = 48;
                _mechanicLine.positionCount = points;
                for (var i = 0; i < points; i++)
                {
                    var a = i / (float)points * Mathf.PI * 2f;
                    _mechanicLine.SetPosition(
                        i,
                        new Vector3(Mathf.Cos(a) * previewR, 0f, Mathf.Sin(a) * previewR));
                }

                _mechanicLine.startColor = tint;
                _mechanicLine.endColor = tint;
                _mechanicLine.widthMultiplier = Mathf.Clamp(previewR * 0.016f, 0.06f, 0.18f);
            }

            if (_mechanicLineMat != null)
            {
                ApplyColor(_mechanicLineMat, tint);
            }

            if (_mechanicDisc != null)
            {
                _mechanicDisc.localScale = new Vector3(previewR * 2f, 0.02f, previewR * 2f);
                var fill = tint;
                fill.a = mechanic.FlatOnGround ? 0.28f : mechanic.FollowsHost ? 0.14f : 0.20f;
                ApplyColor(_mechanicFillMat, fill);
            }
        }

        void FitGround(float radius)
        {
            if (_ground == null)
            {
                return;
            }

            var world = radius > 0.01f
                ? Mathf.Max(14f, radius * 2f + 8f)
                : 8f;
            _ground.transform.localScale = Vector3.one * (world / UnityPlaneSize);
        }

        void FrameCameraToMechanic(AbilityFxMechanic mechanic, float radius)
        {
            if (_abilityId == _framedAbilityId && Mathf.Abs(_framedRadius - radius) < 0.01f)
            {
                return;
            }

            _framedAbilityId = _abilityId;
            _framedRadius = radius;
            if (radius <= 0.01f)
            {
                _distance = DefaultCameraDistance;
                return;
            }

            var hostX = mechanic.RingHost == AbilityVfxAnchor.Target
                || mechanic.RingHost == AbilityVfxAnchor.Impact
                    ? TargetX
                    : CasterX;
            var extent = Mathf.Max(Mathf.Abs(CasterX), Mathf.Abs(TargetX), Mathf.Abs(hostX) + radius);
            var padding = 2.2f;
            var halfFov = _preview.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var needed = (extent + padding) / Mathf.Tan(halfFov) * 1.2f;
            _distance = Mathf.Clamp(needed, MinCameraDistance, MaxCameraDistance);
        }

        void SpawnVfx()
        {
            var prefab = ResolveSpawnPrefab();
            if (prefab == null || _casterRoot == null)
            {
                return;
            }

            var tint = PreviewTint;
            if (_kind == AbilityVfxKind.Aura)
            {
                var auraKind = PassiveAuraFxRules.ResolveKind(_abilityId);
                _instance = AuraFxVisuals.Attach(
                    _casterRoot.transform,
                    prefab,
                    auraKind,
                    tint,
                    AbilityVfxPlacement.ResolveAuraRadius(_gameplayRadius),
                    _visualScale);
                if (_instance == null)
                {
                    return;
                }

                AuraFxVisuals.PrepareEditorPreview(_instance, softenLights: false);
                _loop = true;
                AbilityVfxPreviewPlayback.PrepareInstance(_instance, tint);
                return;
            }

            _instance = Object.Instantiate(prefab);
            _instance.name = prefab.name + " (preview)";
            _preview.AddSingleGO(_instance);
            var caster = _casterRoot != null
                ? _casterRoot.transform.position
                : new Vector3(CasterX, N4PerimeterLaneGeometry.LaneHeight, 0f);
            var target = _targetRoot != null
                ? _targetRoot.transform.position
                : new Vector3(TargetX, N4PerimeterLaneGeometry.LaneHeight, 0f);
            var impact = AbilityVfxPlacement.ResolvePreviewImpact(_abilityId, caster, target);
            AbilityVfxPlacement.ApplyOneShotTransform(
                _instance.transform,
                _anchor,
                _casterRoot != null ? _casterRoot.transform : null,
                _targetRoot != null ? _targetRoot.transform : null,
                impact,
                _euler,
                prefab.transform.rotation);
            _instance.transform.localScale =
                AbilityVfxPlacement.ResolveOneShotLocalScale(prefab, _visualScale);

            AuraFxVisuals.PrepareEditorPreview(_instance, softenLights: false);
            AbilityVfxPreviewPlayback.PrepareInstance(_instance, tint);
            AbilityVfxTint.Apply(_instance, tint);
        }

        GameObject ResolveSpawnPrefab()
        {
            if (_vfxPrefab != null)
            {
                return _vfxPrefab;
            }

            if (_kind != AbilityVfxKind.Aura)
            {
                return null;
            }

            var catalog = Resources.Load<MatchFxCatalog>("Fx/MatchFxCatalog");
            return catalog != null
                ? catalog.GetPassiveAuraPrefab(PassiveAuraFxRules.ResolveKind(_abilityId))
                : null;
        }

        void FireAnim()
        {
            if (_casterAnimator == null)
            {
                return;
            }

            _casterPlayback.CurrentStateName = null;
            _casterPlayback.IsDead = false;
            var clip = AbilityAnimRules.ResolveAttackClipSeconds(
                _casterRole,
                _casterHeroSlot,
                _casterBonusSlot);
            var state = _animState;
            var kind = AbilityAnimRules.ResolveAnim(_abilityId, _animKind, state);
            float? attackVariant = null;
            float? castVariant = null;
            if (!string.IsNullOrEmpty(state))
            {
                var fromState = AbilityAnimRules.ResolveKindFromState(state);
                if (fromState == AbilityAnimKind.Attack)
                {
                    attackVariant = _animVariant;
                }
                else if (fromState == AbilityAnimKind.Cast)
                {
                    castVariant = _animVariant;
                }
            }

            switch (kind)
            {
                case AbilityAnimKind.Attack:
                    UnitCombatAnimatorDriver.Tick(
                        _casterAnimator,
                        _casterPlayback,
                        UnitBehaviorState.Attack,
                        fireAttack: true,
                        fireDeath: false,
                        attackIntervalSeconds: clip,
                        attackClipLength: clip,
                        attackVariantOverride: attackVariant,
                        stateOverride: state);
                    break;
                case AbilityAnimKind.Cast:
                    UnitCombatAnimatorDriver.Tick(
                        _casterAnimator,
                        _casterPlayback,
                        UnitBehaviorState.Cast,
                        fireAttack: false,
                        fireDeath: false,
                        enteringCast: true,
                        castVariantOverride: castVariant,
                        stateOverride: state);
                    break;
                default:
                    if (!string.IsNullOrEmpty(state))
                    {
                        UnitCombatAnimatorDriver.Tick(
                            _casterAnimator,
                            _casterPlayback,
                            UnitBehaviorState.Cast,
                            fireAttack: false,
                            fireDeath: false,
                            enteringCast: true,
                            stateOverride: state);
                    }
                    else
                    {
                        UnitCombatAnimatorDriver.TickStand(_casterAnimator, _casterPlayback);
                    }

                    break;
            }

            _casterAnimator.enabled = false;
            _casterAnimator.Update(0f);
        }

        void TickBuildingCollapse(float dt)
        {
            if (!AbilityFxPreviewTargetRules.PreviewBuildingCollapse(_abilityId))
            {
                return;
            }

            if (!_ruinsApplied
                && _elapsed >= AbilityFxPreviewTargetRules.BuildingSmiteRuinDelaySeconds)
            {
                ApplyPreviewBuildingDestroyed();
            }

            var restart = _ruinFxNeedRestart;
            _ruinFxNeedRestart = false;
            for (var i = 0; i < _ruinFx.Count; i++)
            {
                SimulateParticles(_ruinFx[i], dt, restart, Color.white);
            }
        }

        void ApplyPreviewBuildingDestroyed()
        {
            if (_ruinsApplied || _targetModel == null)
            {
                return;
            }

            _ruinsApplied = true;
            var buildingId = AbilityFxPreviewTargetRules.Resolve(_abilityId).BuildingId;
            BuildingRuinsVisual.ApplyRuins(_targetModel.transform, buildingId);

            var catalog = Resources.Load<MatchFxCatalog>("Fx/MatchFxCatalog");
            if (catalog == null)
            {
                return;
            }

            SpawnRuinFx(catalog.BuildingDestroyed);
            SpawnRuinFx(catalog.BuildingBurning);
            _ruinFxNeedRestart = true;
        }

        void SpawnRuinFx(GameObject prefab)
        {
            if (prefab == null || _targetRoot == null)
            {
                return;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name + " (preview-ruins)";
            _preview.AddSingleGO(instance);
            instance.transform.position = _targetRoot.transform.position;
            instance.transform.rotation = prefab.transform.rotation;
            AuraFxVisuals.PrepareEditorPreview(instance, softenLights: false);
            AbilityVfxPreviewPlayback.PrepareInstance(instance, Color.white);
            _ruinFx.Add(instance);
        }

        void RestoreTargetIntact()
        {
            if (_targetModel != null && _targetIsBuilding)
            {
                BuildingRuinsVisual.RestoreIntact(_targetModel.transform);
            }
        }

        void ClearRuinFx()
        {
            for (var i = 0; i < _ruinFx.Count; i++)
            {
                DestroyGo(_ruinFx[i]);
            }

            _ruinFx.Clear();
            _ruinFxNeedRestart = false;
        }

        void ApplySoloLayout(bool solo)
        {
            if (_soloTarget == solo)
            {
                return;
            }

            _soloTarget = solo;
            if (_casterRoot != null)
            {
                _casterRoot.SetActive(!solo);
            }

            if (_targetRoot != null)
            {
                var x = solo ? 0f : TargetX;
                _targetRoot.transform.position = new Vector3(
                    x,
                    N4PerimeterLaneGeometry.LaneHeight,
                    0f);
            }

            if (!solo)
            {
                FaceEachOther();
            }
        }

        void SimulateParticles(GameObject root, float dt, bool restart, Color tint)
        {
            if (root == null)
            {
                return;
            }

            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Simulate(dt * Mathf.Max(0f, ps.main.simulationSpeed), false, restart, false);
                ps.Pause(true);
            }

            AbilityVfxPreviewPlayback.SimulateVisualEffects(root, dt, restart, tint);
        }

        void FaceEachOther()
        {
            if (_soloTarget || _casterRoot == null || _targetRoot == null)
            {
                return;
            }

            LookAtFlat(_casterRoot.transform, _targetRoot.transform.position);
            LookAtFlat(_targetRoot.transform, _casterRoot.transform.position);
        }

        static void LookAtFlat(Transform from, Vector3 worldTarget)
        {
            var dir = worldTarget - from.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                from.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        void PlaceCamera()
        {
            var focus = new Vector3(0f, N4PerimeterLaneGeometry.LaneHeight + CameraFocusY, 0f);
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var camera = _preview.camera;
            camera.transform.position = focus + rotation * (Vector3.back * _distance);
            camera.transform.LookAt(focus);
        }

        GameObject SpawnUnitModel(GameObject root, GameObject prefab, UnitRole role, bool isBuilding)
        {
            if (prefab == null || root == null)
            {
                return null;
            }

            var model = Object.Instantiate(prefab, root.transform);
            model.name = prefab.name + " (preview)";
            StripColliders(model);
            var scale = isBuilding ? 1f : UnitGreyboxVisuals.ResolveAnimatedPresenterScale(role);
            model.transform.localScale = prefab.transform.localScale * scale;
            var offset = isBuilding ? Vector3.zero : UnitGreyboxVisuals.GetModelLocalOffset(role);
            model.transform.localPosition = offset;
            return model;
        }

        static Animator ConfigureAnimator(
            GameObject model,
            UnitCombatAnimatorPlayback playback,
            bool playStand)
        {
            if (model == null)
            {
                return null;
            }

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return null;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            if (playStand)
            {
                UnitCombatAnimatorDriver.TickStand(animator, playback);
            }

            animator.enabled = false;
            animator.Update(0f);
            return animator;
        }

        GameObject CreateRoot(string name, float x)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(x, N4PerimeterLaneGeometry.LaneHeight, 0f);
            _preview.AddSingleGO(go);
            return go;
        }

        GameObject CreateGround(Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "FxPreviewGround";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one * 0.55f;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            _preview.AddSingleGO(go);
            return go;
        }

        static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        void CreateMechanicGizmo(
            out GameObject root,
            out LineRenderer line,
            out Transform disc,
            out Material lineMat,
            out Material fillMat)
        {
            root = new GameObject("FxPreviewMechanicRing");
            _preview.AddSingleGO(root);

            lineMat = CreateUnlitMaterial(Color.white);
            MakeTransparent(lineMat);
            var lineGo = new GameObject("RingLine");
            _preview.AddSingleGO(lineGo);
            lineGo.transform.SetParent(root.transform, false);
            line = lineGo.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.widthMultiplier = 0.045f;
            line.positionCount = 48;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = lineMat;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;

            fillMat = CreateUnlitMaterial(new Color(1f, 1f, 1f, 0.22f));
            MakeTransparent(fillMat);
            var discGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            discGo.name = "RingFill";
            Object.DestroyImmediate(discGo.GetComponent<Collider>());
            _preview.AddSingleGO(discGo);
            discGo.transform.SetParent(root.transform, false);
            discGo.transform.localScale = new Vector3(2f, 0.012f, 2f);
            var renderer = discGo.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = fillMat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            disc = discGo.transform;
            root.SetActive(false);
        }

        static void MakeTransparent(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        static void ApplyColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        GameObject CreateDirectionalLight(string name, float intensity, Quaternion rotation)
        {
            var go = new GameObject(name);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = Color.white;
            light.shadows = LightShadows.None;
            go.transform.rotation = rotation;
            _preview.AddSingleGO(go);
            return go;
        }

        static RenderTexture CreateRt(int width, int height) => new(width, height, 16)
        {
            antiAliasing = 1,
            hideFlags = HideFlags.HideAndDontSave,
        };

        void ClearInstance()
        {
            if (_instance == null)
            {
                return;
            }

            Object.DestroyImmediate(_instance);
            _instance = null;
        }

        static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        static void DestroyGo(GameObject go)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        static void DestroyMat(Material material)
        {
            if (material != null)
            {
                Object.DestroyImmediate(material);
            }
        }

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f
            && Mathf.Abs(a.g - b.g) < 0.002f
            && Mathf.Abs(a.b - b.b) < 0.002f
            && Mathf.Abs(a.a - b.a) < 0.002f;
    }
}
