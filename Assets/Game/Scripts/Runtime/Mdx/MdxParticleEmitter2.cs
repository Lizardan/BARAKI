using UnityEngine;

namespace Game.Mdx
{
    internal sealed class MdxParticleEmitter2
    {
        const int MaxPoolSize = 512;

        readonly MdxInstance _instance;
        readonly ParticleEmitter2 _data;
        readonly MdxInstance.MdxNodeRuntime _node;

        readonly SampledTrack _visibilityTrack;
        readonly SampledTrack _widthTrack;
        readonly SampledTrack _lengthTrack;
        readonly SampledTrack _latitudeTrack;
        readonly SampledTrack _variationTrack;
        readonly SampledTrack _speedTrack;
        readonly SampledTrack _gravityTrack;
        readonly SampledTrack _emissionRateTrack;

        readonly bool _teamColored;
        readonly bool _lineEmitter;
        readonly bool _modelSpace;
        readonly bool _xYQuad;
        readonly bool _head;
        readonly bool _tail;
        readonly float _lifeSpan;
        readonly float _timeMiddle;
        readonly float _emissionRate;
        readonly float _columns;
        readonly float _rows;
        readonly float _tailLength;
        readonly bool _squirt;
        readonly Color[] _colors = new Color[3];
        readonly float[] _scaling = new float[3];
        readonly float[][] _intervals = { new float[3], new float[3], new float[3], new float[3] };

        Particle[] _particles;
        int _alive;
        float _currentEmission;
        float _lastEmissionKey = -1f;

        readonly Mesh _mesh;
        readonly UnityEngine.Material _material;
        readonly Vector3[] _meshPositions;
        readonly Vector2[] _meshUvs;
        readonly Color32[] _meshColors;
        readonly int[] _meshIndices;
        readonly float[] _scratch = new float[4];

        Vector3 _cameraRight;
        Vector3 _cameraUp;
        Vector3 _cameraForward;

        public MdxParticleEmitter2(MdxInstance instance, ParticleEmitter2 data, MdxInstance.MdxNodeRuntime node)
        {
            _instance = instance;
            _data = data;
            _node = node;

            var flags = data.Flags;
            _lineEmitter = (flags & (int)ParticleEmitter2Flags.LineEmitter) != 0;
            _modelSpace = (flags & (int)ParticleEmitter2Flags.ModelSpace) != 0;
            _xYQuad = (flags & (int)ParticleEmitter2Flags.XYQuad) != 0;
            _squirt = data.Squirt != 0;

            _teamColored = data.ReplaceableId == 1 || data.ReplaceableId == 2;
            _lifeSpan = data.LifeSpan;
            _timeMiddle = data.TimeMiddle;
            _emissionRate = data.EmissionRate * 2f;
            _columns = data.Columns;
            _rows = data.Rows;
            _tailLength = data.TailLength;

            var headOrTail = data.HeadOrTail;
            _head = headOrTail == 0 || headOrTail == 2;
            _tail = headOrTail == 1 || headOrTail == 2;

            for (var i = 0; i < 3; i++)
            {
                _colors[i] = new Color(
                    data.SegmentColors[i][0],
                    data.SegmentColors[i][1],
                    data.SegmentColors[i][2],
                    data.SegmentAlphas[i] / 255f);
                _scaling[i] = data.SegmentScaling[i];
            }

            for (var i = 0; i < 2; i++)
            {
                _intervals[i][0] = data.HeadIntervals[i][0];
                _intervals[i][1] = data.HeadIntervals[i][1];
                _intervals[i][2] = data.HeadIntervals[i][2];
                _intervals[i + 2][0] = data.TailIntervals[i][0];
                _intervals[i + 2][1] = data.TailIntervals[i][1];
                _intervals[i + 2][2] = data.TailIntervals[i][2];
            }

            _visibilityTrack = Track(data, "KP2V");
            _widthTrack = Track(data, "KP2N");
            _lengthTrack = Track(data, "KP2W");
            _latitudeTrack = Track(data, "KP2L");
            _variationTrack = Track(data, "KP2R");
            _speedTrack = Track(data, "KP2S");
            _gravityTrack = Track(data, "KP2G");
            _emissionRateTrack = Track(data, "KP2E");

            var poolSize = Mathf.Clamp(Mathf.CeilToInt(_emissionRate * Mathf.Max(_lifeSpan, 0.1f)) + 32, 64, MaxPoolSize);
            _particles = new Particle[poolSize];

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

            _mesh = new Mesh { name = "MdxParticleMesh_" + data.Name };
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
                var rate = Scalar(_emissionRateTrack, _emissionRate);

                if (_squirt)
                {
                    if (rate != _lastEmissionKey)
                    {
                        _currentEmission += rate;
                    }

                    _lastEmissionKey = rate;
                }
                else
                {
                    _currentEmission += rate * dt;
                }
            }

            var emission = _currentEmission;
            if (emission >= 1f)
            {
                for (var i = 0f; i < emission; i += 1f)
                {
                    if (_head)
                    {
                        Spawn(0);
                    }

                    if (_tail)
                    {
                        Spawn(1);
                    }
                }
            }
        }

        public void LateUpdate(float dt, UnityEngine.Camera camera)
        {
            UpdateParticles(dt);

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

            var inverseRotation = Quaternion.Inverse(_instance.transform.rotation);
            _cameraRight = inverseRotation * camera.transform.right;
            _cameraUp = inverseRotation * camera.transform.up;
            _cameraForward = inverseRotation * camera.transform.forward;

            BuildMesh();

            Graphics.DrawMesh(_mesh, _instance.transform.localToWorldMatrix, _material, 0, camera);
        }

        UnityEngine.Material CreateMaterial()
        {
            var shader = Shader.Find("Game/Units/Wc3MdxParticle");
            if (shader == null)
            {
                return null;
            }

            var material = new UnityEngine.Material(shader) { name = "MdxParticle_" + _data.Name };

            var texture = _instance.ResolveEmitterTexture(_data);
            if (texture != null)
            {
                material.mainTexture = texture;
            }

            var filterMode = (int)_data.FilterMode;
            if (filterMode > 4)
            {
                filterMode = 0;
            }

            var blend = FilterModeBlend(filterMode);
            material.SetFloat(MdxMaterialBuilder.PropSrcBlend, (float)blend.Src);
            material.SetFloat(MdxMaterialBuilder.PropDstBlend, (float)blend.Dst);
            material.SetFloat(MdxMaterialBuilder.PropZWrite, 0f);
            material.renderQueue = MdxMaterialBuilder.QueueTransparent;

            return material;
        }

        void Spawn(int tail)
        {
            if (_alive >= _particles.Length)
            {
                _currentEmission = 0f;
                return;
            }

            var particle = new Particle();
            var width = Scalar(_widthTrack, _data.Width) * 0.5f;
            var length = Scalar(_lengthTrack, _data.Length) * 0.5f;
            var latitude = Scalar(_latitudeTrack, _data.Latitude);
            var variation = Scalar(_variationTrack, _data.Variation);
            var speed = Scalar(_speedTrack, _data.Speed);
            var gravity = Scalar(_gravityTrack, _data.Gravity);

            var node = _node;
            var worldScale = node.WorldScale;

            particle.Health = _lifeSpan;
            particle.Tail = tail;
            particle.Gravity = gravity * worldScale.z;
            particle.Scale = worldScale;

            var location = new Vector3(
                node.Pivot.x + Random.Range(-width, width),
                node.Pivot.y + Random.Range(-length, length),
                node.Pivot.z);

            if (!_modelSpace)
            {
                location = node.WorldMatrix.MultiplyPoint(location);
            }

            particle.Location = location;

            var rotation = Quaternion.identity;
            rotation = rotation * Quaternion.AngleAxis(90f, Vector3.forward);
            rotation = rotation * Quaternion.AngleAxis(Random.Range(-latitude, latitude), Vector3.up);

            if (!_lineEmitter)
            {
                rotation = rotation * Quaternion.AngleAxis(Random.Range(-latitude, latitude), Vector3.right);
            }

            if (!_modelSpace)
            {
                rotation = node.WorldRotation * rotation;
            }

            var velocity = rotation * Vector3.forward * (speed * (1f + Random.Range(-variation, variation)));

            if (!_modelSpace)
            {
                velocity = Vector3.Scale(velocity, worldScale);
            }

            particle.Velocity = velocity;

            if (_xYQuad)
            {
                particle.Facing = Mathf.Atan2(velocity.y, velocity.x) - Mathf.PI + Mathf.PI / 8f;
            }

            _particles[_alive++] = particle;
            _currentEmission -= 1f;
        }

        void UpdateParticles(float dt)
        {
            var particles = _particles;
            var alive = _alive;

            for (var i = 0; i < alive; i++)
            {
                var particle = particles[i];
                particle.Health -= dt;

                if (particle.Health <= 0f)
                {
                    alive--;
                    particles[i] = particles[alive];
                    i--;
                }
                else
                {
                    particle.Velocity.z -= particle.Gravity * dt;
                    particle.Location += particle.Velocity * dt;
                    particles[i] = particle;
                }
            }

            _alive = alive;
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

            var particles = _particles;
            var alive = _alive;
            var vi = 0;
            var columns = _columns;
            var rows = _rows;

            for (var i = 0; i < alive; i++)
            {
                var particle = particles[i];
                var health = particle.Health;

                if (health <= 0f)
                {
                    vi += 4;
                    continue;
                }

                var factor = (_lifeSpan - health) / _lifeSpan;
                if (factor < 0f)
                {
                    factor = 0f;
                }
                else if (factor > 1f)
                {
                    factor = 1f;
                }

                int segment;
                float segmentFactor;
                if (factor < _timeMiddle)
                {
                    segment = 0;
                    segmentFactor = _timeMiddle > 0f ? factor / _timeMiddle : 1f;
                }
                else
                {
                    var range = 1f - _timeMiddle;
                    segment = 1;
                    segmentFactor = range > 0f ? (factor - _timeMiddle) / range : 1f;
                }

                if (segmentFactor > 1f)
                {
                    segmentFactor = 1f;
                }

                var scale = Mathf.Lerp(_scaling[segment], _scaling[segment + 1], segmentFactor);
                var color = (Color32)Color.Lerp(_colors[segment], _colors[segment + 1], segmentFactor);

                uint cell;
                if (_teamColored)
                {
                    cell = (uint)Mathf.Clamp(_instance.TeamColor, 0, 255);
                }
                else
                {
                    var interval = particle.Tail == 0 ? _intervals[segment] : _intervals[segment + 2];
                    cell = GetCell(interval, factor);
                }

                var left = Mathf.Floor((float)cell % columns);
                var top = Mathf.Floor((float)cell / columns);
                var right = (left + 1f) / columns;
                left /= columns;
                var bottom = (top + 1f) / rows;
                top /= rows;

                var location = particle.Location;
                var vi0 = vi + 0;
                var vi1 = vi + 1;
                var vi2 = vi + 2;
                var vi3 = vi + 3;

                if (particle.Tail == 0)
                {
                    var cs = Mathf.Cos(particle.Facing);
                    var sn = Mathf.Sin(particle.Facing);
                    var rightVec = _cameraRight * scale;
                    var upVec = _cameraUp * scale;

                    positions[vi0] = Quad(location, -1f, -1f, cs, sn, rightVec, upVec);
                    positions[vi1] = Quad(location, -1f, 1f, cs, sn, rightVec, upVec);
                    positions[vi2] = Quad(location, 1f, 1f, cs, sn, rightVec, upVec);
                    positions[vi3] = Quad(location, 1f, -1f, cs, sn, rightVec, upVec);

                    uvs[vi0] = new Vector2(right, top);
                    uvs[vi1] = new Vector2(left, top);
                    uvs[vi2] = new Vector2(left, bottom);
                    uvs[vi3] = new Vector2(right, bottom);
                }
                else
                {
                    var start = location - _tailLength * particle.Velocity;
                    var direction = particle.Velocity;
                    if (direction.sqrMagnitude < 0.0001f)
                    {
                        direction = Vector3.forward;
                    }

                    var normal = Vector3.Cross(_cameraForward, direction.normalized).normalized * (scale * particle.Scale.x);

                    positions[vi0] = start - normal;
                    positions[vi1] = location - normal;
                    positions[vi2] = location + normal;
                    positions[vi3] = start + normal;

                    uvs[vi0] = new Vector2(right, top);
                    uvs[vi1] = new Vector2(right, bottom);
                    uvs[vi2] = new Vector2(left, bottom);
                    uvs[vi3] = new Vector2(left, top);
                }

                colors[vi0] = color;
                colors[vi1] = color;
                colors[vi2] = color;
                colors[vi3] = color;

                vi += 4;
            }

            _mesh.vertices = positions;
            _mesh.uv = uvs;
            _mesh.colors32 = colors;
        }

        static Vector3 Quad(Vector3 location, float px, float py, float cs, float sn, Vector3 rightVec, Vector3 upVec)
        {
            var x = px * cs - py * sn;
            var y = px * sn + py * cs;
            return location + (rightVec * x + upVec * y);
        }

        uint GetCell(float[] interval, float factor)
        {
            var start = interval[0];
            var end = interval[1];
            var repeat = interval[2];
            var spriteCount = end - start;

            if (spriteCount > 0f)
            {
                var value = start + Mathf.Floor(spriteCount * repeat * factor) % spriteCount;
                return (uint)Mathf.Min(value, _columns * _rows - 1f);
            }

            return (uint)start;
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

        SampledTrack Track(ParticleEmitter2 data, string name)
        {
            var animation = data.FindAnimation(name);
            return animation != null ? SampledTrack.Create(_instance.Model, animation) : null;
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

        struct Particle
        {
            public Vector3 Location;
            public Vector3 Velocity;
            public Vector3 Scale;
            public float Health;
            public float Gravity;
            public int Tail;
            public float Facing;
        }
    }
}
