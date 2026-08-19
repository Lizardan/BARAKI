using System;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Tiny looping PreviewRenderUtility for one VFX prefab (picker grid cell).</summary>
    public sealed class AbilityVfxThumbSession : IDisposable
    {
        public const int Size = 128;
        const float MaxDeltaSeconds = 0.05f;
        const float ShowcaseSeconds = 0.28f;
        const float FramePadding = 1.08f;
        const float MinExtent = 0.18f;

        readonly PreviewRenderUtility _preview;
        RenderTexture _targetRt;
        GameObject _instance;
        GameObject _prefab;
        Vector3 _cameraPosition = new(0f, 0.9f, -3.4f);
        Vector3 _lookAt = new(0f, 0.45f, 0f);
        float _near = 0.05f;
        float _far = 40f;
        float _orthoSize = 1f;
        float _elapsed;
        bool _particlesNeedRestart;
        bool _disposed;

        public AbilityVfxThumbSession()
        {
            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 30f;
            _preview.camera.nearClipPlane = 0.05f;
            _preview.camera.farClipPlane = 80f;
            _preview.camera.orthographic = true;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            _preview.lights[0].intensity = 1.35f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            _preview.lights[1].intensity = 0.7f;
            _preview.lights[1].transform.rotation = Quaternion.Euler(20f, 140f, 0f);
            _preview.ambientColor = new Color(0.45f, 0.45f, 0.48f, 1f);
            AbilityVfxPreviewPlayback.ConfigureCamera(_preview.camera);
            AbilityVfxPreviewPlayback.RetainCullingScope();
            _targetRt = CreateRt();
            _preview.camera.targetTexture = _targetRt;
            PlaceCamera();
        }

        public Texture Target => _disposed || _targetRt == null ? Texture2D.blackTexture : _targetRt;

        public GameObject Prefab => _prefab;

        public void Bind(GameObject prefab)
        {
            if (_disposed || _prefab == prefab && _instance != null)
            {
                return;
            }

            _prefab = prefab;
            Rebuild();
        }

        public void Clear()
        {
            if (_disposed)
            {
                return;
            }

            DestroyGo(_instance);
            _instance = null;
            _prefab = null;
        }

        public void Tick(float deltaTime)
        {
            if (_disposed || _instance == null || deltaTime <= 0f)
            {
                return;
            }

            var dt = Mathf.Min(deltaTime, MaxDeltaSeconds);
            _elapsed += dt;
            SimulateParticles(dt);
            if (AbilityVfxPreviewPlayback.HasVisualEffect(_instance))
            {
                if (_elapsed >= AbilityVfxPreviewPlayback.ThumbVfxLoopSeconds)
                {
                    AbilityVfxPreviewPlayback.PlayVisualEffects(_instance);
                    _elapsed = 0f;
                }
            }
            else if (AbilityVfxPreviewPlayback.CountAliveParticles(_instance) <= 0)
            {
                SimulateAt(ShowcaseSeconds);
            }

            Render();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AbilityVfxPreviewPlayback.ReleaseCullingScope();
            DestroyGo(_instance);
            _instance = null;
            if (_targetRt != null)
            {
                _preview.camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(_targetRt);
                _targetRt = null;
            }

            _preview.Cleanup();
        }

        void Rebuild()
        {
            DestroyGo(_instance);
            _instance = null;
            _elapsed = 0f;
            _particlesNeedRestart = true;
            if (_prefab == null)
            {
                Render();
                return;
            }

            _instance = UnityEngine.Object.Instantiate(_prefab);
            _instance.name = _prefab.name + " (thumb)";
            _instance.transform.position = Vector3.zero;
            _instance.transform.rotation = _prefab.transform.rotation;
            _preview.AddSingleGO(_instance);
            AuraFxVisuals.PrepareEditorPreview(_instance, softenLights: false);
            PrepareThumbPlayback(_instance);
            AbilityVfxPreviewPlayback.PrepareInstance(_instance);
            var previewAt = AbilityVfxPreviewPlayback.HasVisualEffect(_instance)
                ? AbilityVfxPreviewPlayback.ThumbVfxPeakSeconds
                : ShowcaseSeconds;
            SimulateAt(previewAt);
            if (!AbilityVfxPreviewPlayback.HasVisualEffect(_instance)
                && AbilityVfxPreviewPlayback.CountAliveParticles(_instance) <= 0)
            {
                SimulateAt(0.7f);
            }

            FrameToEffect();
            _elapsed = previewAt;
            _particlesNeedRestart = false;
            Render();
        }

        static void PrepareThumbPlayback(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Local;
            }
        }

        void FrameToEffect()
        {
            if (_instance == null)
            {
                return;
            }

            if (AbilityVfxPreviewPlayback.HasVisualEffect(_instance))
            {
                FrameVisualEffect();
                return;
            }

            var bounds = new Bounds();
            var hasBounds = false;
            EncapsulateRenderers(_instance, ref bounds, ref hasBounds);
            if (!hasBounds)
            {
                EncapsulateParticleShapes(_instance, ref bounds, ref hasBounds);
            }

            if (!hasBounds)
            {
                bounds = new Bounds(new Vector3(0f, 0.35f, 0f), Vector3.one * 0.8f);
            }

            var extent = Mathf.Max(MinExtent, bounds.extents.x, bounds.extents.y, bounds.extents.z);
            var look = bounds.center;
            var orbit = Quaternion.Euler(16f, -28f, 0f);
            _lookAt = look;
            _cameraPosition = look - orbit * Vector3.forward * (extent * 4f + 1.2f);
            _near = 0.05f;
            _far = Mathf.Max(12f, extent * 12f);
            _preview.camera.orthographic = true;
            _preview.camera.orthographicSize = extent * FramePadding;
            _orthoSize = _preview.camera.orthographicSize;
        }

        void FrameVisualEffect()
        {
            var look = Vector3.zero;
            var ortho = AbilityVfxPreviewPlayback.SlashOrthoSize;
            var bounds = new Bounds();
            var hasBounds = false;
            EncapsulateRenderers(_instance, ref bounds, ref hasBounds);
            if (hasBounds)
            {
                var extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                if (extent > 0.04f && extent < 2.5f)
                {
                    look = bounds.center;
                    ortho = Mathf.Clamp(extent * 1.15f, 0.16f, 0.42f);
                }
            }

            _lookAt = look;
            _cameraPosition = look + new Vector3(0f, 0.02f, -1.2f);
            _near = 0.02f;
            _far = 8f;
            _orthoSize = ortho;
            _preview.camera.orthographic = true;
            _preview.camera.orthographicSize = _orthoSize;
        }

        static void EncapsulateRenderers(GameObject root, ref Bounds bounds, ref bool hasBounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                var size = renderer.bounds.size;
                if (size.x + size.y + size.z < 0.02f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        static void EncapsulateParticleShapes(GameObject root, ref Bounds bounds, ref bool hasBounds)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                var shape = ps.shape;
                var radius = Mathf.Max(0.15f, shape.radius, ps.main.startSize.constantMax * 0.5f);
                var center = ps.transform.TransformPoint(shape.position);
                var local = new Bounds(center, Vector3.one * (radius * 2f));
                if (!hasBounds)
                {
                    bounds = local;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        void PlaceCamera()
        {
            var camera = _preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = _orthoSize;
            camera.nearClipPlane = _near;
            camera.farClipPlane = Mathf.Max(_far, _near + 1f);
            camera.transform.position = _cameraPosition;
            camera.transform.LookAt(_lookAt);
        }

        void Render()
        {
            if (_disposed || _targetRt == null)
            {
                return;
            }

            PlaceCamera();
            AbilityVfxPreviewPlayback.Render(_preview, _targetRt);
        }

        void SimulateAt(float seconds)
        {
            if (_instance == null)
            {
                return;
            }

            var time = Mathf.Max(0f, seconds);
            var systems = _instance.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ps.Simulate(time * Mathf.Max(0f, ps.main.simulationSpeed), false, true, false);
                ps.Pause(true);
            }

            AbilityVfxPreviewPlayback.SimulateVisualEffects(_instance, time, restart: true);
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

            AbilityVfxPreviewPlayback.SimulateVisualEffects(_instance, dt, restart);
        }

        static RenderTexture CreateRt()
        {
            var rt = new RenderTexture(Size, Size, 16)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        static void DestroyGo(GameObject go)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
