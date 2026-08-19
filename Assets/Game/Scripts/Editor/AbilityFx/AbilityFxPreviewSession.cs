using System;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;
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

        readonly PreviewRenderUtility _preview;
        readonly GameObject _ground;
        readonly GameObject _casterRoot;
        readonly GameObject _targetRoot;
        readonly GameObject _keyLight;
        readonly GameObject _fillLight;
        readonly Material _groundMat;
        readonly UnitCombatAnimatorPlayback _casterPlayback = new();
        readonly UnitCombatAnimatorPlayback _targetPlayback = new();
        RenderTexture _targetRt;
        GameObject _casterModel;
        GameObject _casterPrefab;
        GameObject _targetModel;
        GameObject _targetPrefab;
        bool _targetIsBuilding;
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
        float _yaw = 38f;
        float _pitch = 26f;
        float _distance = 7.4f;
        bool _loop;
        bool _particlesNeedRestart;
        bool _disposed;

        public AbilityFxPreviewSession()
        {
            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 32f;
            _preview.camera.nearClipPlane = 0.05f;
            _preview.camera.farClipPlane = 80f;
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

        public void SetCaster(GameObject prefab, UnitRole role, int heroSlot, int bonusSlot, bool isBuilding)
        {
            if (_disposed)
            {
                return;
            }

            _casterRole = role;
            _casterHeroSlot = heroSlot;
            _casterBonusSlot = bonusSlot;
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
            var sameVisual =
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
                && Mathf.Abs(_stunSeconds - stunSeconds) < 0.001f
                && _instance != null;
            if (sameVisual && Approximately(tint, _tint))
            {
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
                return;
            }

            _tint = tint;
            Rebuild();
        }

        public void Replay() => Rebuild();

        public void Orbit(float deltaYaw, float deltaPitch)
        {
            _yaw += deltaYaw * 0.35f;
            _pitch = Mathf.Clamp(_pitch - deltaPitch * 0.35f, 8f, 75f);
        }

        public void Zoom(float scrollDelta)
        {
            _distance = Mathf.Clamp(_distance - scrollDelta * 0.35f, 3.2f, 16f);
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
                SimulateParticles(dt);
                if (!_loop && AbilityVfxPreviewPlayback.HasVisualEffect(_instance))
                {
                    if (_elapsed >= AbilityVfxPreviewPlayback.OneShotLoopSeconds)
                    {
                        AbilityVfxPreviewPlayback.PlayVisualEffects(_instance, PreviewTint);
                        _elapsed = 0f;
                    }
                }
                else if (!_loop && _elapsed >= _oneShotLifetime)
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
            ClearInstance();
            DestroyGo(_casterModel);
            DestroyGo(_targetModel);
            DestroyGo(_casterRoot);
            DestroyGo(_targetRoot);
            DestroyGo(_ground);
            DestroyGo(_keyLight);
            DestroyGo(_fillLight);
            DestroyMat(_groundMat);

            if (_targetRt != null)
            {
                _preview.camera.targetTexture = null;
                Object.DestroyImmediate(_targetRt);
                _targetRt = null;
            }

            _preview.Cleanup();
        }

        void Rebuild()
        {
            ClearInstance();
            _elapsed = 0f;
            _loop = _kind == AbilityVfxKind.Aura;
            _particlesNeedRestart = true;
            SpawnVfx();
            FireAnim();
            RenderToTarget();
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

        void SimulateParticles(float dt)
        {
            if (_instance == null)
            {
                return;
            }

            var restart = _particlesNeedRestart;
            _particlesNeedRestart = false;
            var systems = _instance.GetComponentsInChildren<ParticleSystem>(true);
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

            AbilityVfxPreviewPlayback.SimulateVisualEffects(_instance, dt, restart, PreviewTint);
        }

        void FaceEachOther()
        {
            if (_casterRoot == null || _targetRoot == null)
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
