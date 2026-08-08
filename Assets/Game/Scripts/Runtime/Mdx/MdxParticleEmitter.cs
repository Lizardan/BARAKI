using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityMaterial = UnityEngine.Material;

namespace Game.Mdx
{
    /// <summary>
    /// Runtime particle emitter (PREM, standard <c>ParticleEmitter</c>). Ports the reference
    /// <c>particleemitter.ts</c>/<c>particle.ts</c> of mdx-m3-viewer.
    ///
    /// Each emitted particle references an external child model via <c>Path</c> (e.g.
    /// <c>SharedModels\Feather1.MDL</c>). The child file is resolved by file name under the SOURSE
    /// folder; when found its model is parsed once per emitter and its geosets are drawn at each
    /// particle's transform on a sequence-0 bind pose. When the child asset (or its texture) is
    /// missing the particle falls back to a small white billboard sprite so the emitter is never
    /// invisible. Emission/life/velocity mirror the reference: a latitude cone around the node's
    /// forward, speed, and gravity along the model's Z axis scaled by world scale.
    /// </summary>
    internal sealed class MdxParticleEmitter
    {
        const string SourceFolder = "Assets/NewModels/SOURSE";
        const int MaxPoolSize = 64;

        readonly MdxInstance _instance;
        readonly ParticleEmitter _data;
        readonly MdxInstance.MdxNodeRuntime _node;

        readonly SampledTrack _emissionRateTrack;
        readonly SampledTrack _latitudeTrack;
        readonly SampledTrack _lifeSpanTrack;
        readonly SampledTrack _gravityTrack;
        readonly SampledTrack _speedTrack;
        readonly SampledTrack _visibilityTrack;

        readonly float _emissionRate;
        readonly float _latitude;
        readonly float _lifeSpan;
        readonly float _gravity;
        readonly float _speed;

        readonly Particle[] _particles;
        int _alive;
        float _currentEmission;

        readonly MdxMeshData _childMesh;
        readonly IReadOnlyList<UnityMaterial> _childMaterials;
        readonly bool _hasChild;

        readonly Mesh _fallbackMesh;
        readonly UnityMaterial _fallbackMaterial;

        readonly float[] _scratch = new float[4];

        public MdxParticleEmitter(MdxInstance instance, ParticleEmitter data, MdxInstance.MdxNodeRuntime node)
        {
            _instance = instance;
            _data = data;
            _node = node;

            _emissionRate = Mathf.Max(0f, data.EmissionRate);
            _latitude = data.Latitude * Mathf.Rad2Deg;
            _lifeSpan = Mathf.Max(0.001f, data.LifeSpan);
            _gravity = data.Gravity;
            _speed = data.Speed;

            _emissionRateTrack = Track("KPEE");
            _latitudeTrack = Track("KPLT");
            _lifeSpanTrack = Track("KPEL");
            _gravityTrack = Track("KPEG");
            _speedTrack = Track("KPES");
            _visibilityTrack = Track("KPEV");

            var poolSize = Mathf.Clamp(Mathf.CeilToInt(_emissionRate * _lifeSpan) + 8, 8, MaxPoolSize);
            _particles = new Particle[poolSize];

            _childMesh = null;
            _childMaterials = null;
            var child = TryLoadChild(data.Path);
            if (child != null)
            {
                _childMesh = child.Data;
                _childMaterials = child.Materials;
                _hasChild = child.Data != null && child.Data.Geosets.Count > 0 && child.Materials != null;
            }

            _fallbackMesh = CreateFallbackMesh();
            _fallbackMaterial = CreateFallbackMaterial();
        }

        public void Update(float dt)
        {
            if (_instance.AllowParticleSpawn && Scalar(_visibilityTrack, 1f) > 0f)
            {
                var rate = Scalar(_emissionRateTrack, _emissionRate);
                _currentEmission += rate * dt;
            }

            while (_currentEmission >= 1f)
            {
                Spawn();
            }

            UpdateParticles(dt);
        }

        public void LateUpdate(float dt, UnityEngine.Camera camera)
        {
            UpdateParticles(dt);

            if (_alive == 0)
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

            var instanceMatrix = Matrix4x4.TRS(_instance.transform.position, _instance.transform.rotation, _instance.transform.lossyScale);

            for (var i = 0; i < _alive; i++)
            {
                var p = _particles[i];
                if (p.Health <= 0f)
                {
                    continue;
                }

                var local = Matrix4x4.TRS(p.Location, p.Rotation, p.WorldScale);
                var world = instanceMatrix * local;

                if (_hasChild)
                {
                    for (var g = 0; g < _childMesh.Geosets.Count; g++)
                    {
                        var geoset = _childMesh.Geosets[g];
                        var mi = geoset.MaterialId;
                        if (mi >= 0 && mi < _childMaterials.Count && _childMaterials[mi] != null)
                        {
                            Graphics.DrawMesh(geoset.Mesh, world, _childMaterials[mi], 0, camera);
                        }
                    }
                }
                else if (_fallbackMaterial != null)
                {
                    Graphics.DrawMesh(_fallbackMesh, world, _fallbackMaterial, 0, camera);
                }
            }
        }

        void Spawn()
        {
            if (_alive >= _particles.Length)
            {
                _currentEmission = 0f;
                return;
            }

            var particle = new Particle();
            var latitude = Scalar(_latitudeTrack, _latitude);
            var lifeSpan = Scalar(_lifeSpanTrack, _lifeSpan);
            var gravity = Scalar(_gravityTrack, _gravity);
            var speed = Scalar(_speedTrack, _speed);

            var node = _node;
            var worldScale = node.WorldScale;

            particle.Health = lifeSpan;
            particle.Gravity = gravity * worldScale.z;

            var rot = Quaternion.Euler(
                UnityEngine.Random.Range(-latitude, latitude),
                UnityEngine.Random.Range(-180f, 180f),
                UnityEngine.Random.Range(-latitude, latitude));

            var velocity = rot * Vector3.forward * speed;
            velocity = node.WorldRotation * velocity;
            velocity = Vector3.Scale(velocity, worldScale);

            particle.Velocity = velocity;
            particle.Location = node.WorldLocation;
            particle.Rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward) * node.WorldRotation;
            particle.WorldScale = worldScale;

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

        ChildData TryLoadChild(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var fileName = Path.GetFileName(path);
            var diskRoot = ResolveDiskRoot();
            string resolved = null;

            if (Directory.Exists(diskRoot))
            {
                foreach (var file in Directory.GetFiles(diskRoot, fileName, SearchOption.AllDirectories))
                {
                    resolved = file;
                    break;
                }
            }

            if (resolved == null)
            {
                return null;
            }

            var model = MdxModel.Load(File.ReadAllBytes(resolved));
            if (model == null)
            {
                return null;
            }

            var meshData = MdxMeshBuilder.Build(model);
            var textures = DecodeChildTextures(model, diskRoot);
            IReadOnlyList<UnityMaterial> materials;
            try
            {
                materials = MdxMaterialBuilder.CreateMaterials(model, textures);
            }
            catch
            {
                materials = null;
            }

            return new ChildData { Data = meshData, Materials = materials };
        }

        static string ResolveDiskRoot()
        {
            var dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return SourceFolder;
            }

            var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(dataPath));
            return Path.Combine(projectRoot ?? "", SourceFolder);
        }

        /// <summary>Best-effort decode of child model textures (BLP supported at runtime; DDS skipped).</summary>
        static IReadOnlyList<Texture2D> DecodeChildTextures(MdxModel model, string diskRoot)
        {
            var result = new Texture2D[model.Textures.Count];
            for (var i = 0; i < model.Textures.Count; i++)
            {
                var texture = model.Textures[i];
                if (texture.ReplaceableId != 0 || string.IsNullOrEmpty(texture.Path))
                {
                    continue;
                }

                var fileName = Path.GetFileName(texture.Path);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                string filePath = null;
                foreach (var file in Directory.GetFiles(diskRoot, fileName, SearchOption.AllDirectories))
                {
                    filePath = file;
                    break;
                }

                if (filePath == null)
                {
                    continue;
                }

                if (filePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    // DDS decoding lives in the editor assembly; skip at runtime.
                    continue;
                }

                try
                {
                    var blp = new BlpImage();
                    blp.Load(File.ReadAllBytes(filePath));
                    var rgba = blp.GetMipmap(0, out var width, out var height);
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
                    {
                        name = "PREM_Tex_" + fileName,
                        wrapMode = TextureWrapMode.Repeat,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    tex.LoadRawTextureData(rgba);
                    tex.Apply(false, false);
                    result[i] = tex;
                }
                catch
                {
                    result[i] = null;
                }
            }

            return result;
        }

        Mesh CreateFallbackMesh()
        {
            var mesh = new Mesh { name = "MdxPREM_Fallback_" + _data.Name };
            mesh.vertices = new[]
            {
                new Vector3(-1f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(-1f, -1f, 0f),
            };
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }

        UnityMaterial CreateFallbackMaterial()
        {
            var shader = Shader.Find("Game/Units/Wc3MdxParticle");
            if (shader == null)
            {
                return null;
            }

            var material = new UnityMaterial(shader) { name = "MdxPREM_Fallback_" + _data.Name };
            material.SetFloat(MdxMaterialBuilder.PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(MdxMaterialBuilder.PropDstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat(MdxMaterialBuilder.PropZWrite, 0f);
            material.renderQueue = MdxMaterialBuilder.QueueTransparent;
            return material;
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

        sealed class ChildData
        {
            public MdxMeshData Data;
            public IReadOnlyList<UnityMaterial> Materials;
        }

        struct Particle
        {
            public Vector3 Location;
            public Vector3 Velocity;
            public Quaternion Rotation;
            public Vector3 WorldScale;
            public float Health;
            public float Gravity;
        }
    }
}