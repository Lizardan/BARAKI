using UnityEngine;
using UnityMaterial = UnityEngine.Material;

namespace Game.Mdx
{
    /// <summary>
    /// Runtime popcorn (CORN) particle emitter.
    ///
    /// PopcornFX particles are authored in external .pkfx files, which this project does not ship
    /// or support. As a lightweight placeholder the emitter spawns small billboard quads tinted with
    /// the object's static <c>Color</c>/<c>Alpha</c> at <c>EmissionRate</c>, ejected with <c>Speed</c>
    /// and fading over <c>LifeSpan</c>. This keeps CORN emitters visible (instead of silently empty)
    /// and is intentionally a visual approximation, not a PopcornFX reproduction.
    /// </summary>
    internal sealed class MdxParticleEmitterPopcorn
    {
        const int MaxPoolSize = 64;

        readonly MdxInstance _instance;
        readonly ParticleEmitterPopcorn _data;
        readonly MdxInstance.MdxNodeRuntime _node;

        readonly Particle[] _particles;
        int _alive;
        float _currentEmission;

        readonly Mesh _mesh;
        readonly UnityMaterial _material;
        readonly Vector3[] _positions;
        readonly Vector2[] _uvs;
        readonly Color32[] _colors;
        readonly int[] _indices;
        readonly Color _color;
        readonly Color _lifeSpanEnd;

        public MdxParticleEmitterPopcorn(MdxInstance instance, ParticleEmitterPopcorn data, MdxInstance.MdxNodeRuntime node)
        {
            _instance = instance;
            _data = data;
            _node = node;

            var poolSize = Mathf.Clamp(Mathf.CeilToInt(data.EmissionRate * Mathf.Max(data.LifeSpan, 0.1f)) + 8, 8, MaxPoolSize);
            _particles = new Particle[poolSize];

            _positions = new Vector3[poolSize * 4];
            _uvs = new Vector2[poolSize * 4];
            _colors = new Color32[poolSize * 4];
            _indices = new int[poolSize * 6];

            for (var i = 0; i < poolSize; i++)
            {
                var b = i * 4;
                _indices[i * 6 + 0] = b;
                _indices[i * 6 + 1] = b + 1;
                _indices[i * 6 + 2] = b + 2;
                _indices[i * 6 + 3] = b;
                _indices[i * 6 + 4] = b + 2;
                _indices[i * 6 + 5] = b + 3;
            }

            _mesh = new Mesh { name = "MdxPopcornMesh_" + data.Name };
            _mesh.MarkDynamic();
            _mesh.vertices = _positions;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = _indices;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(2048f, 2048f, 2048f));

            _color = new Color(data.Color[0], data.Color[1], data.Color[2], data.Alpha);
            _lifeSpanEnd = new Color(0f, 0f, 0f, 0f);

            _material = CreateMaterial();
        }

        public void Update(float dt)
        {
            if (_instance.AllowParticleSpawn)
            {
                _currentEmission += _data.EmissionRate * dt;
            }

            while (_currentEmission >= 1f)
            {
                Spawn();
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
            var cameraRight = inverseRotation * camera.transform.right;
            var cameraUp = inverseRotation * camera.transform.up;

            BuildMesh(cameraRight, cameraUp);

            Graphics.DrawMesh(_mesh, _instance.transform.localToWorldMatrix, _material, 0, camera);
        }

        UnityMaterial CreateMaterial()
        {
            var shader = Shader.Find("Game/Units/Wc3MdxParticle");
            if (shader == null)
            {
                return null;
            }

            var material = new UnityMaterial(shader) { name = "MdxPopcorn_" + _data.Name };
            material.SetFloat(MdxMaterialBuilder.PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(MdxMaterialBuilder.PropDstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat(MdxMaterialBuilder.PropZWrite, 0f);
            material.renderQueue = MdxMaterialBuilder.QueueTransparent;
            return material;
        }

        void Spawn()
        {
            if (_alive >= _particles.Length)
            {
                _currentEmission = 0f;
                return;
            }

            var node = _node;
            var worldScale = node.WorldScale;

            var particle = new Particle
            {
                Health = Mathf.Max(0.001f, _data.LifeSpan),
                LifeSpanInit = Mathf.Max(0.001f, _data.LifeSpan),
                Scale = 0.5f,
                Location = node.WorldLocation,
                Velocity = RandomUnitSphere() * _data.Speed,
            };

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
                    particle.Location += particle.Velocity * dt;
                    particles[i] = particle;
                }
            }

            _alive = alive;
        }

        void BuildMesh(Vector3 cameraRight, Vector3 cameraUp)
        {
            var positions = _positions;
            var uvs = _uvs;
            var colors = _colors;

            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = Vector3.zero;
            }

            var particles = _particles;
            var alive = _alive;

            for (var i = 0; i < alive; i++)
            {
                var particle = particles[i];
                var factor = 1f - (particle.Health / particle.LifeSpanInit);
                if (factor < 0f)
                {
                    factor = 0f;
                }
                else if (factor > 1f)
                {
                    factor = 1f;
                }

                var scale = particle.Scale;
                var color = (Color32)Color.Lerp(_color, _lifeSpanEnd, factor);
                var right = cameraRight * scale;
                var up = cameraUp * scale;
                var loc = particle.Location;

                var b = i * 4;
                positions[b + 0] = loc - right + up;
                positions[b + 1] = loc + right + up;
                positions[b + 2] = loc + right - up;
                positions[b + 3] = loc - right - up;

                uvs[b + 0] = new Vector2(0, 1);
                uvs[b + 1] = new Vector2(1, 1);
                uvs[b + 2] = new Vector2(1, 0);
                uvs[b + 3] = new Vector2(0, 0);

                colors[b + 0] = color;
                colors[b + 1] = color;
                colors[b + 2] = color;
                colors[b + 3] = color;
            }

            _mesh.vertices = positions;
            _mesh.uv = uvs;
            _mesh.colors32 = colors;
        }

        static Vector3 RandomUnitSphere()
        {
            var lon = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var lat = Mathf.Asin(UnityEngine.Random.Range(-1f, 1f));
            return new Vector3(
                Mathf.Cos(lat) * Mathf.Cos(lon),
                Mathf.Sin(lat),
                Mathf.Cos(lat) * Mathf.Sin(lon));
        }

        struct Particle
        {
            public Vector3 Location;
            public Vector3 Velocity;
            public float Scale;
            public float Health;
            public float LifeSpanInit;
        }
    }
}