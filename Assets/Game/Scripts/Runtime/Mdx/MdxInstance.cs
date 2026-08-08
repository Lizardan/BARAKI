using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMaterial = UnityEngine.Material;

namespace Game.Mdx
{
    /// <summary>Binds a rendered (geoset, layer) pair to its <see cref="Renderer"/>.</summary>
    [Serializable]
    public struct MdxRendererBinding
    {
        public Renderer renderer;
        /// <summary>Index into the model's Geosets list.</summary>
        public int geosetIndex;
        /// <summary>Global (material-major, layer-minor) layer index.</summary>
        public int layerIndex;
    }

    /// <summary>
    /// Runtime driver for WC3 MDX models. Expects a prefab built by MdxPrefabBuilder:
    /// a <see cref="TextAsset"/> with the raw .mdx bytes plus one <see cref="Renderer"/>
    /// per (geoset, layer). Skinning and animation happen fully on the GPU/CPU side:
    /// the bone map is an RGBA float texture (width = nodeCount*4, one 4x4 matrix per
    /// node as one 4x4 column texels), and per-frame batch state (geoset color,
    /// layer alpha, UV animations, texture swaps) is set on per-instance material
    /// copies (URP SRP Batcher ignores MaterialPropertyBlock for textures).
    ///
    /// Mirrors <c>modelinstance.ts</c>/<c>skeletalnode.ts</c> of the mdx-m3-viewer
    /// reference: sequences are evaluated from the raw tracks, constant tracks are skipped
    /// via per-sequence "variants", and global sequences use a free-running ms counter.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public sealed class MdxInstance : MonoBehaviour
    {
        [Header("Prefab data (filled by MdxPrefabBuilder)")]
        [SerializeField] TextAsset source;
        [SerializeField] List<MdxRendererBinding> rendererBindings = new();
        [SerializeField] List<Texture2D> textures = new();
        [SerializeField] List<int> textureReplaceable = new();
        [SerializeField] List<Texture2D> teamColorTextures = new();
        [SerializeField] List<Texture2D> teamGlowTextures = new();

        [Header("Orientation")]
        [Tooltip("WC3 model space is Z-up, Unity is Y-up. This rotation is applied to every root bone so a prefab stands upright at identity transform. Default: 180 degrees about the (0,1,1)/sqrt(2) axis (maps MDX +Z up to Unity +Y, and MDX +Y forward to Unity +Z).")]
        [SerializeField] Quaternion basisConversion = new Quaternion(0f, 0.7071068f, 0.7071068f, 0f);

        [Header("Playback")]
        [SerializeField] int sequenceLoopMode;
        [SerializeField] UnityEngine.Camera sceneCamera;

        MdxModel _model;
        List<MdxMeshNode> _nodes;
        MdxNodeRuntime[] _nodeRuntimes;
        int[] _hierarchy;
        GeosetRuntime[] _geosetRuntimes;
        LayerRuntime[] _layerRuntimes;
        Texture2D _boneTexture;
        Color[] _boneTexels;
        List<GenericObject> _genericObjects;
        readonly List<MdxParticleEmitter2> _particleEmitters = new();
        readonly List<MdxParticleEmitter> _particleEmitter1 = new();
        readonly List<MdxParticleEmitterPopcorn> _cornEmitters = new();
        readonly List<MdxRibbonEmitter> _ribbonEmitters = new();
        bool _allowParticleSpawn;

        UnityMaterial[] _instanceMaterials;
        bool _hasBlockChanges;

        float _frame;
        float _counter;
        int _sequenceId = -1;
        bool _sequenceEnded;
        bool _forced = true;
        bool _initialized;
        Color _vertexColor = Color.white;
        int _teamColor;
        bool _teamColorDirty;

        readonly float[] _scratch = new float[4];

        public TextAsset Source => source;
        public MdxModel Model => _model;
        public int SequenceCount => _model != null ? _model.Sequences.Count : 0;
        public int CurrentSequence => _sequenceId;
        public bool SequenceEnded => _sequenceEnded;
        public int SequenceLoopMode { get => sequenceLoopMode; set => sequenceLoopMode = value; }
        public UnityEngine.Camera SceneCamera { get => sceneCamera; set => sceneCamera = value; }
        public Color VertexColor { get => _vertexColor; set { _vertexColor = value; _hasBlockChanges = true; } }
        public int TeamColor
        {
            get => _teamColor;
            set
            {
                if (_teamColor != value)
                {
                    _teamColor = value;
                    _teamColorDirty = true;
                    if (_initialized)
                    {
                        ApplyBatches();
                    }
                }
            }
        }

        internal bool AllowParticleSpawn => _allowParticleSpawn;
        internal float Frame => _frame;
        internal float Counter => _counter;
        internal int TextureCountInternal => textures != null ? textures.Count : 0;

        void Awake()
        {
            Initialize();
        }

        void OnDestroy()
        {
            if (_instanceMaterials != null)
            {
                for (var i = 0; i < _instanceMaterials.Length; i++)
                {
                    if (_instanceMaterials[i] != null)
                    {
                        DestroyImmediate(_instanceMaterials[i]);
                        _instanceMaterials[i] = null;
                    }
                }
                _instanceMaterials = null;
            }
        }

        /// <summary>Parses the source model and prepares all runtime state. Idempotent.</summary>
        public void Initialize()
        {
            if (_initialized && _model != null)
            {
                return;
            }

            if (source == null)
            {
                return;
            }

            _model = MdxModel.Load(source.bytes);
            if (_model == null)
            {
                return;
            }

            BuildNodes();
            BuildParticleEmitters();
            BuildBoneTexture();
            BuildGeosets();
            BuildLayers();
            BuildBlocks();
            ResetAnimations();

            _initialized = true;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_initialized)
            {
                return;
            }

            UpdateAnimations(Time.deltaTime * 1000f);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || (_particleEmitters.Count == 0 && _particleEmitter1.Count == 0 && _cornEmitters.Count == 0 && _ribbonEmitters.Count == 0))
            {
                return;
            }

            var camera = sceneCamera != null ? sceneCamera : UnityEngine.Camera.main;

            for (var i = 0; i < _particleEmitters.Count; i++)
            {
                _particleEmitters[i].LateUpdate(Time.deltaTime, camera);
            }

            for (var i = 0; i < _particleEmitter1.Count; i++)
            {
                _particleEmitter1[i].LateUpdate(Time.deltaTime, camera);
            }

            for (var i = 0; i < _cornEmitters.Count; i++)
            {
                _cornEmitters[i].LateUpdate(Time.deltaTime, camera);
            }

            for (var i = 0; i < _ribbonEmitters.Count; i++)
            {
                _ribbonEmitters[i].LateUpdate(Time.deltaTime, camera);
            }
        }

        void BuildNodes()
        {
            _nodes = MdxMeshBuilder.BuildNodeList(_model);
            var count = _nodes.Count;
            _nodeRuntimes = new MdxNodeRuntime[count];
            _hierarchy = new int[count];

            var objects = CollectGenericObjects();
            _genericObjects = objects;
            if (objects.Count != count)
            {
                throw new InvalidOperationException("Node list mismatch: " + objects.Count + " objects vs " + count + " nodes.");
            }

            for (var i = 0; i < count; i++)
            {
                var node = _nodes[i];
                var obj = objects[i];
                var runtime = new MdxNodeRuntime
                {
                    Pivot = node.Pivot,
                    DontInheritTranslation = (obj.Flags & (int)GenericObjectFlags.DontInheritTranslation) != 0,
                    DontInheritRotation = (obj.Flags & (int)GenericObjectFlags.DontInheritRotation) != 0,
                    DontInheritScaling = (obj.Flags & (int)GenericObjectFlags.DontInheritScaling) != 0,
                    Billboarded = (obj.Flags & (int)GenericObjectFlags.Billboarded) != 0,
                    BillboardedX = (obj.Flags & (int)GenericObjectFlags.BillboardedLockX) != 0,
                    BillboardedY = (obj.Flags & (int)GenericObjectFlags.BillboardedLockY) != 0,
                    BillboardedZ = (obj.Flags & (int)GenericObjectFlags.BillboardedLockZ) != 0,
                    Translation = CreateTrack(_model, obj, "KGTR"),
                    Rotation = CreateTrack(_model, obj, "KGRT"),
                    Scale = CreateTrack(_model, obj, "KGSC"),
                };

                runtime.ParentIndex = FindNodeIndex(node.ParentId, count);
                runtime.BuildVariants(_model.Sequences.Count);

                _nodeRuntimes[i] = runtime;
            }

            // Sorted order (parents before children) for flat iteration, like the reference.
            var seen = new bool[count];
            var sortedCount = 0;
            for (var i = 0; i < count; i++)
            {
                if (!seen[i])
                {
                    sortedCount = AppendHierarchy(_nodeRuntimes, _hierarchy, seen, i, sortedCount);
                }
            }

            if (sortedCount != count)
            {
                throw new InvalidOperationException("Hierarchy did not cover all nodes.");
            }
        }

        List<GenericObject> CollectGenericObjects()
        {
            var list = new List<GenericObject>();
            list.AddRange(_model.Bones);
            list.AddRange(_model.Lights);
            list.AddRange(_model.Helpers);
            list.AddRange(_model.Attachments);
            list.AddRange(_model.ParticleEmitters);
            list.AddRange(_model.ParticleEmitters2);
            list.AddRange(_model.ParticleEmittersPopcorn);
            list.AddRange(_model.RibbonEmitters);
            list.AddRange(_model.EventObjects);
            list.AddRange(_model.CollisionShapes);
            return list;
        }

        static int AppendHierarchy(MdxNodeRuntime[] runtimes, int[] hierarchy, bool[] seen, int index, int outIndex)
        {
            if (seen[index])
            {
                return outIndex;
            }

            seen[index] = true;

            var parent = runtimes[index].ParentIndex;
            if (parent != -1 && !seen[parent])
            {
                outIndex = AppendHierarchy(runtimes, hierarchy, seen, parent, outIndex);
            }

            hierarchy[outIndex++] = index;
            return outIndex;
        }

        int FindNodeIndex(int objectId, int count)
        {
            if (objectId == -1)
            {
                return -1;
            }

            for (var i = 0; i < count; i++)
            {
                if (_nodes[i].ObjectId == objectId)
                {
                    return i;
                }
            }

            return -1;
        }

        static SampledTrack CreateTrack(MdxModel model, AnimatedObject owner, string name)
        {
            var animation = owner.FindAnimation(name);
            return animation != null ? SampledTrack.Create(model, animation) : null;
        }

        void BuildParticleEmitters()
        {
            var objects = _genericObjects;
            var count = _nodes.Count;

            for (var i = 0; i < count; i++)
            {
                if (objects[i] is ParticleEmitter2 data)
                {
                    _particleEmitters.Add(new MdxParticleEmitter2(this, data, _nodeRuntimes[i]));
                }
else if (objects[i] is ParticleEmitter data1)
                {
                    _particleEmitter1.Add(new MdxParticleEmitter(this, data1, _nodeRuntimes[i]));
                }
                else if (objects[i] is ParticleEmitterPopcorn popcorn)
                {
                    _cornEmitters.Add(new MdxParticleEmitterPopcorn(this, popcorn, _nodeRuntimes[i]));
                }
                else if (objects[i] is RibbonEmitter ribbonData)
                {
                    _ribbonEmitters.Add(new MdxRibbonEmitter(this, ribbonData, _nodeRuntimes[i]));
                }
            }
        }

        void UpdateParticleEmitters(float dt)
        {
            if (_particleEmitters.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _particleEmitters.Count; i++)
            {
                _particleEmitters[i].Update(dt);
            }

            for (var i = 0; i < _particleEmitter1.Count; i++)
            {
                _particleEmitter1[i].Update(dt);
            }

            for (var i = 0; i < _cornEmitters.Count; i++)
            {
                _cornEmitters[i].Update(dt);
            }

            for (var i = 0; i < _ribbonEmitters.Count; i++)
            {
                _ribbonEmitters[i].Update(dt);
            }
        }

        void BuildGeosets()
        {
            var count = _model.Geosets.Count;
            _geosetRuntimes = new GeosetRuntime[count];

            for (var g = 0; g < count; g++)
            {
                GeosetAnimation geosetAnimation = null;
                for (var i = 0; i < _model.GeosetAnimations.Count; i++)
                {
                    if (_model.GeosetAnimations[i].GeosetId == g)
                    {
                        geosetAnimation = _model.GeosetAnimations[i];
                        break;
                    }
                }

                var runtime = new GeosetRuntime
                {
                    Color = Color.white,
                    Alpha = 1,
                };

                if (geosetAnimation != null)
                {
                    // The reference sizzles the static RGB color to BGR... no: the file stores
                    // BGR, and the viewer sizzles it to RGB. The shader applies .bgra on top.
                    runtime.Color = new Color(
                        geosetAnimation.Color[2],
                        geosetAnimation.Color[1],
                        geosetAnimation.Color[0],
                        1);
                    runtime.Alpha = geosetAnimation.Alpha;
                    runtime.ColorTrack = CreateTrack(_model, geosetAnimation, "KGAC");
                    runtime.AlphaTrack = CreateTrack(_model, geosetAnimation, "KGAO");
                    runtime.BuildVariants(_model.Sequences.Count);
                }

                _geosetRuntimes[g] = runtime;
            }
        }

        void BuildLayers()
        {
            var list = new List<LayerRuntime>();

            foreach (var material in _model.Materials)
            {
                foreach (var layer in material.Layers)
                {
                    var runtime = new LayerRuntime
                    {
                        Alpha = layer.Alpha,
                        TextureId = layer.TextureId >= 0 ? layer.TextureId : 0,
                        AlphaTrack = CreateTrack(_model, layer, "KMTA"),
                        TextureIdTrack = CreateTrack(_model, layer, "KMTF"),
                    };

                    if (layer.TextureAnimationId >= 0
                        && layer.TextureAnimationId < _model.TextureAnimations.Count)
                    {
                        var anim = _model.TextureAnimations[layer.TextureAnimationId];
                        runtime.TranslationTrack = CreateTrack(_model, anim, "KTAT");
                        runtime.RotationTrack = CreateTrack(_model, anim, "KTAR");
                        runtime.ScaleTrack = CreateTrack(_model, anim, "KTAS");
                    }

                    // Identity UV animation state.
                    runtime.UvAnim[0] = 0;
                    runtime.UvAnim[1] = 0;
                    runtime.UvAnim[2] = 0;
                    runtime.UvAnim[3] = 1;
                    runtime.UvAnim[4] = 1;

                    runtime.BuildVariants(_model.Sequences.Count);
                    list.Add(runtime);
                }
            }

            _layerRuntimes = list.ToArray();
        }

        void BuildBoneTexture()
        {
            if (_nodes.Count == 0)
            {
                return;
            }

            _boneTexels = new Color[_nodes.Count * 4];
            _boneTexture = new Texture2D(_nodes.Count * 4, 1, TextureFormat.RGBAFloat, false, true)
            {
                name = "BoneMap_" + name,
                // NEAREST like the reference (bonetexture.glsl.ts), so matrix texels are never blended.
                filterMode = UnityEngine.FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        void BuildBlocks()
        {
            _instanceMaterials = new UnityMaterial[rendererBindings.Count];

            for (var i = 0; i < rendererBindings.Count; i++)
            {
                var src = rendererBindings[i].renderer != null
                    ? rendererBindings[i].renderer.sharedMaterial
                    : null;
                if (src != null)
                {
                    var copy = new UnityMaterial(src) { name = src.name + "_instance" };
                    _instanceMaterials[i] = copy;
                    rendererBindings[i].renderer.sharedMaterial = copy;
                }
            }
        }

        /// <summary>
        /// Sequence API. <c>sequence</c> is the index into the model's sequence list;
        /// -1 stops animation (the bind pose is used).
        /// </summary>
        public void SetSequence(int sequence)
        {
            var sequences = _model.Sequences;
            _sequenceId = sequence;

            if (sequence < 0 || sequence >= sequences.Count)
            {
                _sequenceId = -1;
                _frame = 0;
                _allowParticleSpawn = false;
            }
            else
            {
                _frame = sequences[sequence].Interval[0];
            }

            _forced = true;
            _sequenceEnded = false;
        }

        public void Stop()
        {
            SetSequence(-1);
        }

        public string GetSequenceName(int index)
        {
            return _model != null && index >= 0 && index < _model.Sequences.Count
                ? _model.Sequences[index].Name
                : "";
        }

        /// <summary>0 = never loop, 1 = loop per model, 2 = always loop.</summary>
        public void SetSequenceLoopMode(int mode)
        {
            sequenceLoopMode = mode;
        }

        void ResetAnimations()
        {
            _frame = 0;
            _counter = 0;
            _sequenceId = -1;
            _forced = true;
            _sequenceEnded = false;
            _allowParticleSpawn = false;

            // Initial static state so the model renders even without any sequence.
            UpdateAnimations(0f);
        }

        void UpdateAnimations(float dtMs)
        {
            _allowParticleSpawn = false;

            if (_sequenceId != -1)
            {
                _allowParticleSpawn = true;

                var sequences = _model.Sequences;
                var interval = sequences[_sequenceId].Interval;
                var nonLooping = sequences[_sequenceId].NonLooping;

                _frame += dtMs;
                _counter += dtMs;

                if (_frame >= interval[1])
                {
                    if (sequenceLoopMode == 2 || (sequenceLoopMode == 0 && nonLooping == 0))
                    {
                        _frame = interval[0];
                    }
                    else
                    {
                        _frame = interval[1];
                        _counter -= dtMs;
                        _allowParticleSpawn = false;
                    }

                    _sequenceEnded = true;
                }
                else
                {
                    _sequenceEnded = false;
                }
            }

            var forced = _forced;
            if (_sequenceId != -1 || forced)
            {
                UpdateNodes(forced);
                UpdateParticleEmitters(dtMs * 0.001f);
                UpdateBoneTexture();
                UpdateBatches(forced);
                ApplyBatches();
            }

            _forced = false;
        }

        void UpdateNodes(bool forced)
        {
            var sortedNodes = _nodeRuntimes;
            var sequence = _sequenceId;

            for (var k = 0; k < _hierarchy.Length; k++)
            {
                var index = _hierarchy[k];
                var runtime = sortedNodes[index];
                var parent = runtime.ParentIndex != -1 ? sortedNodes[runtime.ParentIndex] : null;

                var wasDirty = forced || (parent != null && parent.WasDirty) || runtime.AnyBillboarding;

                if (forced || runtime.GenericVariant(sequence))
                {
                    wasDirty = true;

                    if ((forced || runtime.TranslationVariant(sequence)) && runtime.Translation != null)
                    {
                        runtime.Translation.GetValue(_scratch, sequence, _frame, _counter);
                        runtime.LocalLocation = new Vector3(_scratch[0], _scratch[1], _scratch[2]);
                    }

                    if ((forced || runtime.RotationVariant(sequence)) && runtime.Rotation != null)
                    {
                        runtime.Rotation.GetValue(_scratch, sequence, _frame, _counter);
                        runtime.LocalRotation = new Quaternion(_scratch[0], _scratch[1], _scratch[2], _scratch[3]).normalized;
                    }

                    if ((forced || runtime.ScaleVariant(sequence)) && runtime.Scale != null)
                    {
                        runtime.Scale.GetValue(_scratch, sequence, _frame, _counter);
                        runtime.LocalScale = new Vector3(_scratch[0], _scratch[1], _scratch[2]);
                    }
                }

                runtime.WasDirty = wasDirty;

                if (wasDirty)
                {
                    RecalculateTransformation(runtime, parent);
                }
            }
        }

        void RecalculateTransformation(MdxNodeRuntime runtime, MdxNodeRuntime parent)
        {
            var parentMatrix = parent != null ? parent.WorldMatrix : Matrix4x4.identity;
            var parentInvLocation = parent != null ? parent.InverseWorldLocation : Vector3.zero;
            var parentInvRotation = parent != null ? parent.InverseWorldRotation : Quaternion.identity;
            var parentInvScale = parent != null ? parent.InverseWorldScale : Vector3.one;
            var parentWorldRotation = parent != null ? parent.WorldRotation : Quaternion.identity;
            var parentWorldScale = parent != null ? parent.WorldScale : Vector3.one;

            var localLocation = runtime.LocalLocation;
            var localRotation = runtime.LocalRotation;
            var localScale = runtime.LocalScale;
            var pivot = runtime.Pivot;

            Vector3 computedLocation = localLocation;
            Vector3 computedScaling = localScale;
            Quaternion computedRotation = localRotation;

            if (runtime.DontInheritTranslation)
            {
                computedLocation = -parentInvLocation + runtime.WorldLocation + localLocation;
            }

            if (runtime.DontInheritScaling)
            {
                // instance.worldScale is identity in model-local space.
                computedScaling = Vector3.Scale(parentInvScale, Vector3.one);
                computedScaling = Vector3.Scale(computedScaling, localScale);
            }

            var camera = sceneCamera != null ? sceneCamera : UnityEngine.Camera.main;
            if (runtime.Billboarded && camera != null)
            {
                computedRotation = parentInvRotation * CameraInverseRotation(camera);
                computedRotation = ConvertBasis(computedRotation);
                computedRotation = computedRotation * localRotation;
            }
            else if ((runtime.BillboardedX || runtime.BillboardedY || runtime.BillboardedZ) && camera != null)
            {
                if (runtime.BillboardedX)
                {
                    computedScaling = new Vector3(computedScaling.x, computedScaling.y, computedScaling.z * -1f);
                }

                // Inverse the local rotation, then combine with the parent's inverse world rotation.
                var inverseLocal = new Quaternion(-localRotation.x, -localRotation.y, -localRotation.z, localRotation.w);
                var rotation2 = inverseLocal * parentInvRotation;
                var cameraRay = rotation2 * CameraForward(camera);

                Quaternion spin;
                if (runtime.BillboardedX)
                {
                    spin = Quaternion.AngleAxis(Mathf.Atan2(cameraRay.z, cameraRay.y) * Mathf.Rad2Deg, Vector3.right);
                }
                else if (runtime.BillboardedY)
                {
                    spin = Quaternion.AngleAxis(Mathf.Atan2(-cameraRay.z, cameraRay.x) * Mathf.Rad2Deg, Vector3.up);
                }
                else
                {
                    spin = Quaternion.AngleAxis(Mathf.Atan2(cameraRay.y, cameraRay.x) * Mathf.Rad2Deg, Vector3.forward);
                }

                computedRotation = localRotation * spin;
            }

            if (runtime.DontInheritRotation)
            {
                computedRotation = parentInvRotation * computedRotation;
            }

            // Local matrix matching the reference viewer (gl-matrix
            // mat4.fromRotationTranslationScaleOrigin): translation = location + pivot - R*S*pivot.
            // This is NOT T(location)*R*S*T(-pivot) (which would shift every bone by -pivot and
            // crumple the whole skeleton). The pivot offset is folded into the translation so a
            // root bone with identity transform maps vertices to their authored positions.
            var rotationAppliedPivot = computedRotation * Vector3.Scale(computedScaling, pivot);
            runtime.LocalMatrix = Matrix4x4.TRS(computedLocation + pivot - rotationAppliedPivot, computedRotation, computedScaling);
            runtime.WorldMatrix = parentMatrix * runtime.LocalMatrix;

            // Root bones live in the raw WC3 Z-up model space; rotate them (and only them) into
            // Unity's Y-up space. Children inherit the conversion through the parent world matrix.
            if (parent == null && basisConversion != Quaternion.identity)
            {
                runtime.WorldMatrix = Matrix4x4.Rotate(basisConversion) * runtime.WorldMatrix;
            }

            runtime.WorldLocation = runtime.WorldMatrix.MultiplyPoint(pivot);
            runtime.InverseWorldLocation = -runtime.WorldLocation;
            runtime.WorldRotation = parentWorldRotation * computedRotation;
            if (parent == null && basisConversion != Quaternion.identity)
            {
                runtime.WorldRotation = basisConversion * runtime.WorldRotation;
            }

            runtime.InverseWorldRotation = Inverse(runtime.WorldRotation);
            runtime.WorldScale = Vector3.Scale(parentWorldScale, computedScaling);
            runtime.InverseWorldScale = SafeInverse(runtime.WorldScale);
        }

        void UpdateBoneTexture()
        {
            if (_boneTexture == null || _boneTexels == null)
            {
                return;
            }

            var runtimes = _nodeRuntimes;
            var texels = _boneTexels;

            for (var i = 0; i < runtimes.Length; i++)
            {
                var matrix = runtimes[i].WorldMatrix;
                var o = i * 4;
                texels[o] = matrix.GetColumn(0);
                texels[o + 1] = matrix.GetColumn(1);
                texels[o + 2] = matrix.GetColumn(2);
                texels[o + 3] = matrix.GetColumn(3);
            }

            _boneTexture.SetPixels(texels);
            _boneTexture.Apply(false, false);
        }

        void UpdateBatches(bool forced)
        {
            var sequence = _sequenceId;
            var frame = _frame;
            var counter = _counter;

            // Geoset colors.
            for (var g = 0; g < _geosetRuntimes.Length; g++)
            {
                var geoset = _geosetRuntimes[g];

                if (geoset.ColorTrack == null && geoset.AlphaTrack == null)
                {
                    continue;
                }

                if ((forced || geoset.ColorVariant(sequence)) && geoset.ColorTrack != null)
                {
                    geoset.ColorTrack.GetValue(_scratch, sequence, frame, counter);
                    geoset.Color = new Color(_scratch[0], _scratch[1], _scratch[2], geoset.Color.a);
                }

                if ((forced || geoset.AlphaVariant(sequence)) && geoset.AlphaTrack != null)
                {
                    geoset.AlphaTrack.GetValue(_scratch, sequence, frame, counter);
                    geoset.Color = new Color(geoset.Color.r, geoset.Color.g, geoset.Color.b, _scratch[0]);
                }
            }

            // Layers.
            for (var i = 0; i < _layerRuntimes.Length; i++)
            {
                var layer = _layerRuntimes[i];
                var uvAnim = layer.UvAnim;

                if ((forced || layer.AlphaVariant(sequence)) && layer.AlphaTrack != null)
                {
                    layer.AlphaTrack.GetValue(_scratch, sequence, frame, counter);
                    layer.Alpha = _scratch[0];
                }

                if ((forced || layer.TextureIdVariant(sequence)) && layer.TextureIdTrack != null)
                {
                    layer.TextureIdTrack.GetValue(_scratch, sequence, frame, counter);
                    layer.TextureId = (int)_scratch[0];
                }

                if (layer.TranslationTrack != null || layer.RotationTrack != null || layer.ScaleTrack != null)
                {
                    if ((forced || layer.TranslationVariant(sequence)) && layer.TranslationTrack != null)
                    {
                        layer.TranslationTrack.GetValue(_scratch, sequence, frame, counter);
                        uvAnim[0] = _scratch[0];
                        uvAnim[1] = _scratch[1];
                    }

                    if ((forced || layer.RotationVariant(sequence)) && layer.RotationTrack != null)
                    {
                        layer.RotationTrack.GetValue(_scratch, sequence, frame, counter);
                        uvAnim[2] = _scratch[2];
                        uvAnim[3] = _scratch[3];
                    }

                    if ((forced || layer.ScaleVariant(sequence)) && layer.ScaleTrack != null)
                    {
                        layer.ScaleTrack.GetValue(_scratch, sequence, frame, counter);
                        uvAnim[4] = _scratch[0];
                    }
                }
            }
        }

        void ApplyBatches()
        {
            if (_instanceMaterials == null)
            {
                return;
            }

            for (var b = 0; b < rendererBindings.Count; b++)
            {
                var binding = rendererBindings[b];
                if (binding.renderer == null)
                {
                    continue;
                }

                var mat = _instanceMaterials[b];
                if (mat == null)
                {
                    continue;
                }

                if (_boneTexture != null)
                {
                    mat.SetTexture(MdxMaterialBuilder.PropBoneMap, _boneTexture);
                }

                if (binding.geosetIndex >= 0 && binding.geosetIndex < _geosetRuntimes.Length)
                {
                    mat.SetColor(MdxMaterialBuilder.PropGeosetColor, _geosetRuntimes[binding.geosetIndex].Color);
                }

                if (binding.layerIndex >= 0 && binding.layerIndex < _layerRuntimes.Length)
                {
                    var layer = _layerRuntimes[binding.layerIndex];
                    var uvAnim = layer.UvAnim;

                    mat.SetFloat(MdxMaterialBuilder.PropLayerAlpha, layer.Alpha);
                    mat.SetVector(MdxMaterialBuilder.PropUvTranslate, new Vector4(uvAnim[0], uvAnim[1], 0f, 0f));
                    mat.SetVector(MdxMaterialBuilder.PropUvRotate, new Vector4(uvAnim[2], uvAnim[3], 0f, 0f));
                    mat.SetFloat(MdxMaterialBuilder.PropUvScale, uvAnim[4]);

                    // Always resolve the layer texture. Replaceable (TeamColor/TeamGlow)
                    // slots resolve to the team texture even before the first TeamColor
                    // change, otherwise their materials have no _BaseMap and the layer
                    // renders uncolored (default white) until the user switches colors.
                    var texture = ResolveTexture(layer.TextureId);
                    if (texture != null)
                    {
                        mat.SetTexture(MdxMaterialBuilder.PropBaseMap, texture);
                    }
                }

                mat.SetColor(MdxMaterialBuilder.PropVertexColor, _vertexColor);
            }

            _teamColorDirty = false;
            _hasBlockChanges = false;
        }

        Texture2D ResolveTexture(int textureId)
        {
            if (textureId < 0 || textureId >= textures.Count)
            {
                return null;
            }

            var replaceable = textureId < textureReplaceable.Count ? textureReplaceable[textureId] : 0;
            if (replaceable == 1)
            {
                return PickTeamTexture(teamColorTextures);
            }

            if (replaceable == 2)
            {
                return PickTeamTexture(teamGlowTextures);
            }

            return textures[textureId];
        }

        Texture2D PickTeamTexture(List<Texture2D> variants)
        {
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            return variants[_teamColor % variants.Count];
        }

        internal Texture2D ResolveEmitterTexture(ParticleEmitter2 data)
        {
            if (data.ReplaceableId == 1)
            {
                return PickTeamTexture(teamColorTextures);
            }

            if (data.ReplaceableId == 2)
            {
                return PickTeamTexture(teamGlowTextures);
            }

            var id = data.TextureId;
            if (id < 0 || id >= textures.Count)
            {
                return null;
            }

            return textures[id];
        }

        internal Texture2D ResolveRibbonTexture(int textureId)
        {
            if (textureId < 0 || textureId >= textures.Count)
            {
                return null;
            }

            var replaceable = textureId < textureReplaceable.Count ? textureReplaceable[textureId] : 0;
            if (replaceable == 1)
            {
                return PickTeamTexture(teamColorTextures);
            }

            if (replaceable == 2)
            {
                return PickTeamTexture(teamGlowTextures);
            }

            return textures[textureId];
        }

        // --- Space helpers for billboarding (WC3 model space is Z-up, Unity is Y-up) ---

        static Quaternion ConvertBasis(Quaternion rotation)
        {
            return rotation
                * Quaternion.AngleAxis(-90f, Vector3.up)
                * Quaternion.AngleAxis(-90f, Vector3.right);
        }

        static Quaternion CameraInverseRotation(UnityEngine.Camera camera)
        {
            // Convert the camera's world-space inverse rotation into the model's Z-up space.
            return Quaternion.AngleAxis(90f, Vector3.right) * Quaternion.Inverse(camera.transform.rotation);
        }

        static Vector3 CameraForward(UnityEngine.Camera camera)
        {
            return Quaternion.AngleAxis(90f, Vector3.right) * camera.transform.forward;
        }

        static Quaternion Inverse(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }

        static Vector3 SafeInverse(Vector3 v)
        {
            return new Vector3(
                v.x != 0f ? 1f / v.x : 0f,
                v.y != 0f ? 1f / v.y : 0f,
                v.z != 0f ? 1f / v.z : 0f);
        }

        /// <summary>Per-node runtime state (port of <c>SkeletalNode</c>).</summary>
        internal sealed class MdxNodeRuntime
        {
            public int ParentIndex = -1;
            public Vector3 Pivot;
            public bool DontInheritTranslation;
            public bool DontInheritRotation;
            public bool DontInheritScaling;
            public bool Billboarded;
            public bool BillboardedX;
            public bool BillboardedY;
            public bool BillboardedZ;

            public SampledTrack Translation;
            public SampledTrack Rotation;
            public SampledTrack Scale;

            bool[] _translationVariant;
            bool[] _rotationVariant;
            bool[] _scaleVariant;
            bool[] _genericVariant;

            public Vector3 LocalLocation;
            public Quaternion LocalRotation = Quaternion.identity;
            public Vector3 LocalScale = Vector3.one;
            public Vector3 WorldLocation;
            public Quaternion WorldRotation = Quaternion.identity;
            public Vector3 WorldScale = Vector3.one;
            public Vector3 InverseWorldLocation;
            public Quaternion InverseWorldRotation = Quaternion.identity;
            public Vector3 InverseWorldScale = Vector3.one;
            public Matrix4x4 LocalMatrix = Matrix4x4.identity;
            public Matrix4x4 WorldMatrix = Matrix4x4.identity;
            public bool WasDirty;

            public bool AnyBillboarding => Billboarded || BillboardedX || BillboardedY || BillboardedZ;

            public void BuildVariants(int sequenceCount)
            {
                _translationVariant = Variants(Translation, sequenceCount);
                _rotationVariant = Variants(Rotation, sequenceCount);
                _scaleVariant = Variants(Scale, sequenceCount);
                _genericVariant = new bool[sequenceCount];

                for (var i = 0; i < sequenceCount; i++)
                {
                    _genericVariant[i] = _translationVariant[i] || _rotationVariant[i] || _scaleVariant[i];
                }
            }

            public bool TranslationVariant(int sequence) => Variant(_translationVariant, sequence);
            public bool RotationVariant(int sequence) => Variant(_rotationVariant, sequence);
            public bool ScaleVariant(int sequence) => Variant(_scaleVariant, sequence);
            public bool GenericVariant(int sequence) => Variant(_genericVariant, sequence);

            static bool[] Variants(SampledTrack track, int sequenceCount)
            {
                var result = new bool[sequenceCount];
                if (track != null)
                {
                    for (var i = 0; i < sequenceCount; i++)
                    {
                        result[i] = track.IsVariant(i);
                    }
                }

                return result;
            }

            static bool Variant(bool[] variants, int sequence)
            {
                return sequence >= 0 && sequence < variants.Length && variants[sequence];
            }
        }

        /// <summary>Per-geoset color/alpha state.</summary>
        sealed class GeosetRuntime
        {
            public SampledTrack ColorTrack;
            public SampledTrack AlphaTrack;
            public Color Color = Color.white;
            public float Alpha = 1;

            bool[] _colorVariant;
            bool[] _alphaVariant;

            public void BuildVariants(int sequenceCount)
            {
                _colorVariant = Variants(ColorTrack, sequenceCount);
                _alphaVariant = Variants(AlphaTrack, sequenceCount);
            }

            public bool ColorVariant(int sequence) => Variant(_colorVariant, sequence);
            public bool AlphaVariant(int sequence) => Variant(_alphaVariant, sequence);
        }

        /// <summary>Per-layer (alpha / texture / UV) state.</summary>
        sealed class LayerRuntime
        {
            public SampledTrack AlphaTrack;
            public SampledTrack TextureIdTrack;
            public SampledTrack TranslationTrack;
            public SampledTrack RotationTrack;
            public SampledTrack ScaleTrack;

            public float Alpha = 1;
            public int TextureId;
            public readonly float[] UvAnim = new float[5];

            bool[] _alphaVariant;
            bool[] _textureIdVariant;
            bool[] _translationVariant;
            bool[] _rotationVariant;
            bool[] _scaleVariant;

            public void BuildVariants(int sequenceCount)
            {
                _alphaVariant = Variants(AlphaTrack, sequenceCount);
                _textureIdVariant = Variants(TextureIdTrack, sequenceCount);
                _translationVariant = Variants(TranslationTrack, sequenceCount);
                _rotationVariant = Variants(RotationTrack, sequenceCount);
                _scaleVariant = Variants(ScaleTrack, sequenceCount);
            }

            public bool AlphaVariant(int sequence) => Variant(_alphaVariant, sequence);
            public bool TextureIdVariant(int sequence) => Variant(_textureIdVariant, sequence);
            public bool TranslationVariant(int sequence) => Variant(_translationVariant, sequence);
            public bool RotationVariant(int sequence) => Variant(_rotationVariant, sequence);
            public bool ScaleVariant(int sequence) => Variant(_scaleVariant, sequence);
        }

        static bool[] Variants(SampledTrack track, int sequenceCount)
        {
            var result = new bool[sequenceCount];
            if (track != null)
            {
                for (var i = 0; i < sequenceCount; i++)
                {
                    result[i] = track.IsVariant(i);
                }
            }

            return result;
        }

        static bool Variant(bool[] variants, int sequence)
        {
            return sequence >= 0 && sequence < variants.Length && variants[sequence];
        }
    }
}
