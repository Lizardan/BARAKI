using UnityEngine;
using UnityMaterial = UnityEngine.Material;

namespace Game.Mdx
{
    /// <summary>
    /// Runtime ribbon emitter (RIBB). Ports the reference <c>ribbon.ts</c>/<c>ribbonemitter.ts</c>/
    /// <c>geometryemitterfuncs.ts (bindRibbonEmitterBuffer/Shader, renderEmitter)</c> of mdx-m3-viewer:
    /// each emission stamps an "above"/"below" line segment at the node's world pivot (offset by
    /// animated <c>HeightAbove</c>/<c>HeightBelow</c>), the segments are chained oldest-first, and a
    /// camera-facing billboard quad is rebuilt every frame between consecutive segments. The texture
    /// is sliced by <c>Columns</c> x <c>Rows</c> and the requested sub-sprite is spread along the
    /// whole chain; gravity pulls the segment tips down over the segment's life.
    /// </summary>
    internal sealed class MdxRibbonEmitter
    {
        const int MaxPoolSize = 128;

        readonly MdxInstance _instance;
        readonly RibbonEmitter _data;
        readonly MdxInstance.MdxNodeRuntime _node;

        readonly SampledTrack _heightBelowTrack;
        readonly SampledTrack _heightAboveTrack;
        readonly SampledTrack _alphaTrack;
        readonly SampledTrack _colorTrack;
        readonly SampledTrack _textureSlotTrack;
        readonly SampledTrack _visibilityTrack;

        readonly float _lifeSpan;
        readonly float _emissionRate;
        readonly uint _columns;
        readonly uint _rows;
        readonly float _gravity;
        readonly int _materialId;

        readonly RibbonSegment[] _segments;
        int _alive;
        float _currentEmission;

        readonly Mesh _mesh;
        readonly UnityMaterial _material;
        readonly Vector3[] _meshPositions;
        readonly Vector2[] _meshUvs;
        readonly Color32[] _meshColors;
        readonly int[] _meshIndices;
        readonly float[] _scratch = new float[4];

        public MdxRibbonEmitter(MdxInstance instance, RibbonEmitter data, MdxInstance.MdxNodeRuntime node)
        {
            _instance = instance;
            _data = data;
            _node = node;

            _lifeSpan = data.LifeSpan;
            _emissionRate = data.EmissionRate * 2f;
            _columns = data.Columns;
            _rows = data.Rows;
            _gravity = data.Gravity;
            _materialId = data.MaterialId;

            _heightBelowTrack = Track("KRHB");
            _heightAboveTrack = Track("KRHA");
            _alphaTrack = Track("KRAL");
            _colorTrack = Track("KRCO");
            _textureSlotTrack = Track("KRTX");
            _visibilityTrack = Track("KRVS");

            var poolSize = Mathf.Clamp(Mathf.CeilToInt(_emissionRate * Mathf.Max(_lifeSpan, 0.1f)) + 8, 8, MaxPoolSize);
            _segments = new RibbonSegment[poolSize];
            _meshPositions = new Vector3[poolSize * 4];
            _meshUvs = new Vector2[poolSize * 4];
            _meshColors = new Color32[poolSize * 4];
            _meshIndices = new int[poolSize * 6];

            for (var i = 0; i < poolSize; i++)
            {
                var baseIndex = i * 4;
                _meshIndices[i * 6 + 0] = baseIndex;
                _meshIndices[i * 6 + 1] = baseIndex + 1;
                _meshIndices[i * 6 + 2] = baseIndex + 2;
                _meshIndices[i * 6 + 3] = baseIndex;
                _meshIndices[i * 6 + 4] = baseIndex + 2;
                _meshIndices[i * 6 + 5] = baseIndex + 3;
            }

            _mesh = new Mesh { name = "MdxRibbonMesh_" + data.Name };
            _mesh.MarkDynamic();
            _mesh.vertices = _meshPositions;
            _mesh.uv = _meshUvs;
            _mesh.colors32 = _meshColors;
            _mesh.triangles = _meshIndices;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(2048f, 2048f, 2048f));

            _material = CreateMaterial();
        }

        public void Update(float dt)
        {
            if (_instance.AllowParticleSpawn && Scalar(_visibilityTrack, 1f) > 0f)
            {
                var alpha = Scalar(_alphaTrack, _data.Alpha);
                if (alpha > 0f)
                {
                    _currentEmission += _emissionRate * dt;
                }
            }

            while (_currentEmission >= 1f)
            {
                Emit();
            }
        }

        public void LateUpdate(float dt, UnityEngine.Camera camera)
        {
            UpdateSegments(dt);

            if (_material == null || _alive == 0)
            {
                return;
            }

            if (camera == null)
            {
                camera = UnityEngine.Camera.main;
            }

            if (camera == null)
            {
                return;
            }

            BuildMesh();

            Graphics.DrawMesh(_mesh, _instance.transform.localToWorldMatrix, _material, 0, camera);
        }

        UnityMaterial CreateMaterial()
        {
            var shader = Shader.Find("Game/Units/Wc3MdxParticle");
            if (shader == null)
            {
                return null;
            }

            var texture = ResolveRibbonTexture();
            if (texture == null)
            {
                return null;
            }

            var material = new UnityMaterial(shader) { name = "MdxRibbon_" + _data.Name };
            material.mainTexture = texture;

            var filterMode = 0;
            if (_materialId >= 0 && _materialId < _instance.Model.Materials.Count)
            {
                var layers = _instance.Model.Materials[_materialId].Layers;
                if (layers.Count > 0)
                {
                    filterMode = (int)(LayerFilterMode(layers[0]) % 7);
                }
            }

            var blend = FilterModeBlend(filterMode);
            material.SetFloat(MdxMaterialBuilder.PropSrcBlend, (float)blend.Src);
            material.SetFloat(MdxMaterialBuilder.PropDstBlend, (float)blend.Dst);
            material.SetFloat(MdxMaterialBuilder.PropZWrite, 0f);
            material.renderQueue = MdxMaterialBuilder.QueueTransparent;

            return material;
        }

        Texture2D ResolveRibbonTexture()
        {
            if (_materialId < 0 || _materialId >= _instance.Model.Materials.Count)
            {
                return null;
            }

            var layers = _instance.Model.Materials[_materialId].Layers;
            if (layers.Count == 0)
            {
                return null;
            }

            var layer = layers[0];
            if (layer.TextureId < 0 || layer.TextureId >= _instance.TextureCountInternal)
            {
                return null;
            }

            return _instance.ResolveRibbonTexture(layer.TextureId);
        }

        void Emit()
        {
            if (_alive >= _segments.Length)
            {
                _currentEmission = 0f;
                return;
            }

            var segment = new RibbonSegment();
            segment.Health = _lifeSpan;

            var node = _node;
            var pivot = node.Pivot;
            var worldMatrix = node.WorldMatrix;

            var heightBelow = Scalar(_heightBelowTrack, _data.HeightBelow);
            var heightAbove = Scalar(_heightAboveTrack, _data.HeightAbove);

            // WC3 model space is Z-up so "above/below" extend along the model's local Y (the
            // node's own vertical axis), offset around the pivot, then converted to Unity space.
            var below = new Vector3(pivot.x, pivot.y - heightBelow, pivot.z);
            var above = new Vector3(pivot.x, pivot.y + heightAbove, pivot.z);

            segment.Above = worldMatrix.MultiplyPoint(above);
            segment.Below = worldMatrix.MultiplyPoint(below);
            segment.Gravity = _gravity;

            _segments[_alive++] = segment;
            _currentEmission -= 1f;
        }

        void UpdateSegments(float dt)
        {
            var segments = _segments;
            var alive = _alive;

            for (var i = 0; i < alive; i++)
            {
                var segment = segments[i];
                segment.Health -= dt;

                if (segment.Health <= 0f)
                {
                    alive--;
                    segments[i] = segments[alive];
                    i--;
                }
                else
                {
                    var gravity = segment.Gravity * dt * dt;
                    segment.Below.y -= gravity;
                    segment.Above.y -= gravity;
                    segment.Color = SampleColor();
                    segment.Alpha = Scalar(_alphaTrack, _data.Alpha);
                    segment.Slot = (uint)Mathf.Clamp(Scalar(_textureSlotTrack, 0f), 0f, 255f);

                    segments[i] = segment;
                }
            }

            _alive = alive;
        }

        Color SampleColor()
        {
            if (_colorTrack != null)
            {
                _colorTrack.GetValue(_scratch, _instance.CurrentSequence, _instance.Frame, _instance.Counter);
                return new Color(_scratch[0], _scratch[1], _scratch[2]);
            }

            return new Color(_data.Color[0], _data.Color[1], _data.Color[2]);
        }

        void BuildMesh()
        {
            var positions = _meshPositions;
            var uvs = _meshUvs;
            var colors = _meshColors;

            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = Vector3.zero;
            }

            var segments = _segments;
            var alive = _alive;
            if (alive <= 1)
            {
                _mesh.vertices = positions;
                _mesh.uv = uvs;
                _mesh.colors32 = colors;
                return;
            }

            var columns = _columns;
            var rows = _rows;
            var chainLengthFactor = 1f / (alive - 1);

            var vi = 0;
            for (var i = 0; i < alive - 1; i++)
            {
                var cur = segments[i];
                var next = segments[i + 1];

                var left = ((cur.Slot % columns) + (1f - i * chainLengthFactor - chainLengthFactor)) / columns;
                var top = cur.Slot / columns;
                var right = left + chainLengthFactor;
                var bottom = top + 1f;

                var vi0 = vi + 0;
                var vi1 = vi + 1;
                var vi2 = vi + 2;
                var vi3 = vi + 3;

                positions[vi0] = cur.Above;
                positions[vi1] = cur.Below;
                positions[vi2] = next.Below;
                positions[vi3] = next.Above;

                var invColumns = 1f / columns;
                var invRows = 1f / rows;

                uvs[vi0] = new Vector2(right * invColumns, top * invRows);
                uvs[vi1] = new Vector2(right * invColumns, bottom * invRows);
                uvs[vi2] = new Vector2(left * invColumns, bottom * invRows);
                uvs[vi3] = new Vector2(left * invColumns, top * invRows);

                var c = new Color32(
                    (byte)Mathf.Clamp(cur.Color.r * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(cur.Color.g * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(cur.Color.b * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(cur.Alpha * 255f, 0f, 255f));

                colors[vi0] = c;
                colors[vi1] = c;
                colors[vi2] = c;
                colors[vi3] = c;

                vi += 4;
            }

            _mesh.vertices = positions;
            _mesh.uv = uvs;
            _mesh.colors32 = colors;
        }

        float Scalar(SampledTrack track, float fallback)
        {
            if (track == null)
            {
                return fallback;
            }

            track.GetValue(_scratch, _instance.CurrentSequence, _instance.Frame, _instance.Counter);
            return _scratch[0];
        }

        SampledTrack Track(string name)
        {
            var animation = _data.FindAnimation(name);
            return animation != null ? SampledTrack.Create(_instance.Model, animation) : null;
        }

        static uint LayerFilterMode(Layer layer)
        {
            return layer.FilterMode;
        }

        static BlendModePair FilterModeBlend(int mode)
        {
            switch (mode)
            {
                case 1:
                    return new BlendModePair(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One);
                case 2:
                    return new BlendModePair(UnityEngine.Rendering.BlendMode.Zero, UnityEngine.Rendering.BlendMode.SrcColor);
                case 3:
                    return new BlendModePair(UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.SrcColor);
                case 4:
                    return new BlendModePair(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                default:
                    return new BlendModePair(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
        }

        struct BlendModePair
        {
            public readonly UnityEngine.Rendering.BlendMode Src;
            public readonly UnityEngine.Rendering.BlendMode Dst;

            public BlendModePair(UnityEngine.Rendering.BlendMode src, UnityEngine.Rendering.BlendMode dst)
            {
                Src = src;
                Dst = dst;
            }
        }

        struct RibbonSegment
        {
            public Vector3 Above;
            public Vector3 Below;
            public float Health;
            public float Gravity;
            public Color Color;
            public float Alpha;
            public uint Slot;
        }
    }
}