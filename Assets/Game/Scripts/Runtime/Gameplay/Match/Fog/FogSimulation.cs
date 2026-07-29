using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.Gameplay.Match.Fog
{
    /// <summary>
    /// Simplified density fog: permanent clear + unit discs + regrow. No wind/swirl/advection.
    /// </summary>
    public sealed class FogSimulation : MonoBehaviour
    {
        [SerializeField] private ComputeShader _compute;
        [SerializeField] private Material _fogMaterial;
        [SerializeField] private int _resolution = 512;
        [SerializeField] private float _areaSize = 280f;
        [SerializeField, Range(0f, 5f)] private float _regrowRate = 0.9f;
        [SerializeField] private Color _fogColor = new(0.02f, 0.02f, 0.03f, 1f);
        [SerializeField] private Color _shadowColor = new(0.01f, 0.01f, 0.015f, 1f);
        /// <summary>Keep near 1 — high values crush soft density gradients into hard blocks.</summary>
        [SerializeField] private float _densityScale = 1f;
        [SerializeField, Range(0f, 1f)] private float _opacity = 0.82f;

        RenderTexture[] _density;
        RenderTexture _permanentMask;
        int _current;
        int _kernel;
        int _groups;
        ComputeBuffer _clearerBuffer;
        int _clearerCapacity;
        MeshRenderer _volumeRenderer;
        GameObject _volumeObject;
        float _visionRadiusUv = 0.05f;
        bool _ready;

        public struct ClearerData
        {
            public Vector2 Position;
            public float Radius;
        }

        public void Initialize(float arenaRadius, FogPermanentZones permanent, float visionRadiusWorld)
        {
            ReleaseResources();

            // Cover camera pan radius + frustum beyond max zoom so fog edges stay off-screen.
            // Pan bounds = ArenaRadius + 32 (GameplayCameraSettings.DefaultPanBoundsRadius).
            var panBounds = arenaRadius + 32f;
            const float frustumMargin = 180f;
            _areaSize = Mathf.Max(40f, panBounds * 2f + frustumMargin * 2f);
            // Inspector may still hold old densityScale=10 which turns soft fog into hard blocks.
            _densityScale = Mathf.Clamp(_densityScale, 0.25f, 1.5f);
            _visionRadiusUv = visionRadiusWorld / _areaSize;

            if (_compute == null)
            {
                _compute = Resources.Load<ComputeShader>("Fog/FogSim");
            }

            if (_fogMaterial == null)
            {
                _fogMaterial = Resources.Load<Material>("Fog/FogVolume");
            }

            if (_fogMaterial == null)
            {
                var shader = Shader.Find("Game/FogVolume");
                if (shader != null)
                {
                    _fogMaterial = new Material(shader);
                }
            }

            if (_compute == null || _fogMaterial == null)
            {
                Debug.LogWarning("FogSimulation: missing compute or fog material.");
                _ready = false;
                return;
            }

            _kernel = _compute.FindKernel("CSMain");
            _groups = Mathf.CeilToInt(_resolution / 8f);
            _density = new RenderTexture[2];
            for (var i = 0; i < 2; i++)
            {
                _density[i] = new RenderTexture(_resolution, _resolution, 0, RenderTextureFormat.RFloat)
                {
                    enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                _density[i].Create();
            }

            _permanentMask = new RenderTexture(_resolution, _resolution, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _permanentMask.Create();
            BakePermanentMask(permanent);

            FillDensity(1f);
            for (var i = 0; i < 8; i++)
            {
                Step(emptyClearers: true, dt: 0.1f);
            }

            EnsureVolume();
            ApplyMaterialProps();
            _ready = true;
            SetActiveVisual(true);
        }

        public void Tick(IReadOnlyList<Vector3> clearerWorldPositions, float dt)
        {
            if (!_ready)
            {
                return;
            }

            Step(clearerWorldPositions, dt);
        }

        public void SetActiveVisual(bool active)
        {
            if (_volumeObject != null)
            {
                _volumeObject.SetActive(active);
            }
        }

        void Step(IReadOnlyList<Vector3> clearerWorldPositions, float dt)
        {
            UploadClearers(clearerWorldPositions);
            Dispatch(dt);
        }

        void Step(bool emptyClearers, float dt)
        {
            if (emptyClearers)
            {
                UploadClearers(System.Array.Empty<Vector3>());
            }

            Dispatch(dt);
        }

        void Dispatch(float dt)
        {
            var next = 1 - _current;
            _compute.SetInt("_Resolution", _resolution);
            _compute.SetFloat("_Regrow", _regrowRate);
            _compute.SetFloat("dt", Mathf.Max(0f, dt));
            _compute.SetTexture(_kernel, "Source", _density[_current]);
            _compute.SetTexture(_kernel, "Result", _density[next]);
            _compute.SetTexture(_kernel, "PermanentMask", _permanentMask);
            _compute.Dispatch(_kernel, _groups, _groups, 1);
            _fogMaterial.SetTexture("_DensityTex", _density[next]);
            _current = next;
        }

        void UploadClearers(IReadOnlyList<Vector3> clearerWorldPositions)
        {
            var count = clearerWorldPositions?.Count ?? 0;
            var capacity = Mathf.Max(1, count);
            if (_clearerBuffer == null || _clearerCapacity < capacity)
            {
                _clearerBuffer?.Release();
                _clearerCapacity = Mathf.Max(capacity, 8);
                _clearerBuffer = new ComputeBuffer(_clearerCapacity, Marshal.SizeOf<ClearerData>());
            }

            var data = new ClearerData[_clearerCapacity];
            for (var i = 0; i < count; i++)
            {
                data[i] = new ClearerData
                {
                    Position = WorldToUv(clearerWorldPositions[i]),
                    Radius = _visionRadiusUv,
                };
            }

            _clearerBuffer.SetData(data);
            _compute.SetInt("_ClearerCount", count);
            _compute.SetBuffer(_kernel, "_Clearers", _clearerBuffer);
        }

        void BakePermanentMask(FogPermanentZones permanent)
        {
            var pixels = new float[_resolution * _resolution];
            if (permanent != null)
            {
                for (var y = 0; y < _resolution; y++)
                {
                    for (var x = 0; x < _resolution; x++)
                    {
                        var uv = new Vector2((x + 0.5f) / _resolution, (y + 0.5f) / _resolution);
                        var world = UvToWorld(uv);
                        pixels[y * _resolution + x] = permanent.Contains(world) ? 1f : 0f;
                    }
                }
            }

            var tex = new Texture2D(_resolution, _resolution, TextureFormat.RGBAFloat, false, true);
            var colors = new Color[_resolution * _resolution];
            for (var i = 0; i < pixels.Length; i++)
            {
                colors[i] = new Color(pixels[i], 0f, 0f, 1f);
            }

            tex.SetPixels(colors);
            tex.Apply(false, false);
            Graphics.Blit(tex, _permanentMask);
            DestroyManaged(tex);
        }

        void FillDensity(float value)
        {
            var fill = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            fill.SetPixel(0, 0, new Color(value, 0f, 0f, 1f));
            fill.Apply();
            Graphics.Blit(fill, _density[0]);
            Graphics.Blit(fill, _density[1]);
            DestroyManaged(fill);
        }

        void EnsureVolume()
        {
            if (_volumeObject != null)
            {
                DestroyManaged(_volumeObject);
                _volumeObject = null;
                _volumeRenderer = null;
            }

            // Fullscreen triangle — shader ignores transform and samples fog by scene depth XZ.
            _volumeObject = new GameObject("FogFullscreen");
            _volumeObject.transform.SetParent(transform, false);
            var filter = _volumeObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateFullscreenTriangleMesh();
            _volumeRenderer = _volumeObject.AddComponent<MeshRenderer>();
            _volumeRenderer.material = _fogMaterial;
            _fogMaterial = _volumeRenderer.material;
            _volumeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _volumeRenderer.receiveShadows = false;
            _volumeRenderer.allowOcclusionWhenDynamic = false;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.depthTextureMode |= DepthTextureMode.Depth;
            }
        }

        static Mesh CreateFullscreenTriangleMesh()
        {
            var mesh = new Mesh { name = "FogFullscreenTriangle" };
            // Positions unused by VS (SV_VertexID), but required for draw call.
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(3f, -1f, 0f),
                new Vector3(-1f, 3f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2 };
            // Prevent frustum culling of the origin-centered overlay.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            mesh.UploadMeshData(true);
            return mesh;
        }

        void ApplyMaterialProps()
        {
            if (_fogMaterial == null)
            {
                return;
            }

            _fogMaterial.SetColor("_FogColor", _fogColor);
            _fogMaterial.SetColor("_ShadowColor", _shadowColor);
            _fogMaterial.SetFloat("_DensityScale", Mathf.Clamp(_densityScale, 0.25f, 1.5f));
            _fogMaterial.SetFloat("_Opacity", _opacity);
            _fogMaterial.SetFloat("_FogAreaSize", _areaSize);
            _fogMaterial.SetFloat("_Softness", 1.75f);
            _fogMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;
        }

        Vector2 WorldToUv(Vector3 worldPos)
        {
            return new Vector2(worldPos.x / _areaSize + 0.5f, worldPos.z / _areaSize + 0.5f);
        }

        Vector3 UvToWorld(Vector2 uv)
        {
            return new Vector3((uv.x - 0.5f) * _areaSize, 0f, (uv.y - 0.5f) * _areaSize);
        }

        void OnDestroy()
        {
            ReleaseResources();
        }

        void ReleaseResources()
        {
            _clearerBuffer?.Release();
            _clearerBuffer = null;

            if (_density != null)
            {
                foreach (var rt in _density)
                {
                    if (rt != null)
                    {
                        rt.Release();
                    }
                }

                _density = null;
            }

            if (_permanentMask != null)
            {
                _permanentMask.Release();
                _permanentMask = null;
            }

            if (_volumeObject != null)
            {
                DestroyManaged(_volumeObject);
                _volumeObject = null;
                _volumeRenderer = null;
            }

            _ready = false;
        }

        static void DestroyManaged(Object target)
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
