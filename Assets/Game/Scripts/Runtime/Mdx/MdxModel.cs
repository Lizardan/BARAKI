using System;
using System.Collections.Generic;

namespace Game.Mdx
{
    /// <summary>
    /// A Warcraft 3 model. Binary MDX loader ported from <c>mdlx/model.ts</c>.
    /// </summary>
    public sealed class MdxModel
    {
        /// <summary>800 for WC3 RoC/TFT. &gt;800 for Reforged.</summary>
        public int Version = 800;
        public string Name = "";
        public string AnimationFile = "";
        public Extent Extent = new();
        public int BlendTime;
        public readonly List<Sequence> Sequences = new();
        public readonly List<int> GlobalSequences = new();
        public readonly List<Material> Materials = new();
        public readonly List<Texture> Textures = new();
        public readonly List<TextureAnimation> TextureAnimations = new();
        public readonly List<Geoset> Geosets = new();
        public readonly List<GeosetAnimation> GeosetAnimations = new();
        public readonly List<Bone> Bones = new();
        public readonly List<Light> Lights = new();
        public readonly List<Helper> Helpers = new();
        public readonly List<Attachment> Attachments = new();
        public readonly List<float[]> PivotPoints = new();
        public readonly List<ParticleEmitter> ParticleEmitters = new();
        public readonly List<ParticleEmitter2> ParticleEmitters2 = new();
        public readonly List<ParticleEmitterPopcorn> ParticleEmittersPopcorn = new();
        public readonly List<RibbonEmitter> RibbonEmitters = new();
        public readonly List<Camera> Cameras = new();
        public readonly List<EventObject> EventObjects = new();
        public readonly List<CollisionShape> CollisionShapes = new();
        public readonly List<FaceEffect> FaceEffects = new();
        public readonly List<float[]> BindPose = new();
        public readonly List<UnknownChunk> UnknownChunks = new();

        public static MdxModel Load(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 4)
            {
                throw new ArgumentException("Not a valid MDX buffer (too short).");
            }

            var signature = new BinaryStream(buffer).ReadBinary(4);
            if (signature != "MDLX")
            {
                throw new ArgumentException("Not a valid MDX buffer (missing MDLX).");
            }

            var model = new MdxModel();
            var stream = new BinaryStream(buffer);
            stream.Skip(4); // MDLX

            while (stream.Remaining > 0)
            {
                var tag = stream.ReadBinary(4);
                var size = stream.ReadUint32();

                switch (tag)
                {
                    case "VERS": model.Version = (int)stream.ReadUint32(); break;
                    case "MODL":
                        model.Name = stream.Read(80);
                        model.AnimationFile = stream.Read(260);
                        model.Extent = new Extent();
                        model.Extent.ReadMdx(stream);
                        model.BlendTime = (int)stream.ReadUint32();
                        break;
                    case "SEQS": LoadStaticObjects(model.Sequences, s => ReadSequence(s), stream, (int)(size / 132)); break;
                    case "GLBS": LoadGlobalSequenceChunk(model.GlobalSequences, stream, size); break;
                    case "MTLS": LoadDynamicObjects(model.Materials, s => ReadMaterial(s, model.Version), stream, (int)size); break;
                    case "TEXS": LoadStaticObjects(model.Textures, s => ReadTexture(s), stream, (int)(size / 268)); break;
                    case "TXAN": LoadDynamicObjects(model.TextureAnimations, s => ReadTextureAnimation(s), stream, (int)size); break;
                    case "GEOS": LoadDynamicObjects(model.Geosets, s => ReadGeoset(s, model.Version), stream, (int)size); break;
                    case "GEOA": LoadDynamicObjects(model.GeosetAnimations, s => ReadGeosetAnimation(s), stream, (int)size); break;
                    case "BONE": LoadDynamicObjects(model.Bones, s => ReadBone(s), stream, (int)size); break;
                    case "LITE": LoadDynamicObjects(model.Lights, s => ReadLight(s), stream, (int)size); break;
                    case "HELP": LoadDynamicObjects(model.Helpers, s => ReadHelper(s), stream, (int)size); break;
                    case "ATCH": LoadDynamicObjects(model.Attachments, s => ReadAttachment(s), stream, (int)size); break;
                    case "PIVT": LoadPivotPointChunk(model.PivotPoints, stream, size); break;
                    case "PREM": LoadDynamicObjects(model.ParticleEmitters, s => ReadParticleEmitter(s), stream, (int)size); break;
                    case "PRE2": LoadDynamicObjects(model.ParticleEmitters2, s => ReadParticleEmitter2(s), stream, (int)size); break;
                    case "CORN": LoadDynamicObjects(model.ParticleEmittersPopcorn, s => ReadParticleEmitterPopcorn(s), stream, (int)size); break;
                    case "RIBB": LoadDynamicObjects(model.RibbonEmitters, s => ReadRibbonEmitter(s), stream, (int)size); break;
                    case "CAMS": LoadDynamicObjects(model.Cameras, s => ReadCamera(s), stream, (int)size); break;
                    case "EVTS": LoadDynamicObjects(model.EventObjects, s => ReadEventObject(s), stream, (int)size); break;
                    case "CLID": LoadDynamicObjects(model.CollisionShapes, s => ReadCollisionShape(s), stream, (int)size); break;
                    case "FAFX": LoadStaticObjects(model.FaceEffects, s => ReadFaceEffect(s), stream, (int)(size / 340)); break;
                    case "BPOS": LoadBindPoseChunk(model.BindPose, stream, size); break;
                    default: model.UnknownChunks.Add(new UnknownChunk(stream, (int)size, tag)); break;
                }
            }

            return model;
        }

        static void LoadStaticObjects<T>(List<T> outList, Func<BinaryStream, T> reader, BinaryStream stream, int count)
        {
            for (var i = 0; i < count; i++)
            {
                outList.Add(reader(stream));
            }
        }

        static void LoadDynamicObjects<T>(List<T> outList, Func<BinaryStream, T> reader, BinaryStream stream, int size)
        {
            var end = stream.Index + size;

            while (stream.Index < end)
            {
                outList.Add(reader(stream));
            }
        }

        static void LoadGlobalSequenceChunk(List<int> outList, BinaryStream stream, uint size)
        {
            for (var i = 0; i < size / 4; i++)
            {
                outList.Add((int)stream.ReadUint32());
            }
        }

        static void LoadPivotPointChunk(List<float[]> outList, BinaryStream stream, uint size)
        {
            for (var i = 0; i < size / 12; i++)
            {
                outList.Add(stream.ReadFloat32Array(3));
            }
        }

        static void LoadBindPoseChunk(List<float[]> outList, BinaryStream stream, uint size)
        {
            var count = stream.ReadUint32();
            for (var i = 0; i < count; i++)
            {
                outList.Add(stream.ReadFloat32Array(12));
            }
        }

        static Sequence ReadSequence(BinaryStream s)
        {
            var obj = new Sequence();
            obj.ReadMdx(s);
            return obj;
        }

        static Texture ReadTexture(BinaryStream s)
        {
            var obj = new Texture();
            obj.ReadMdx(s);
            return obj;
        }

        static Material ReadMaterial(BinaryStream s, int version)
        {
            var obj = new Material();
            obj.ReadMdx(s, version);
            return obj;
        }

        static TextureAnimation ReadTextureAnimation(BinaryStream s)
        {
            var obj = new TextureAnimation();
            obj.ReadMdx(s);
            return obj;
        }

        static Geoset ReadGeoset(BinaryStream s, int version)
        {
            var obj = new Geoset();
            obj.ReadMdx(s, version);
            return obj;
        }

        static GeosetAnimation ReadGeosetAnimation(BinaryStream s)
        {
            var obj = new GeosetAnimation();
            obj.ReadMdx(s);
            return obj;
        }

        static Bone ReadBone(BinaryStream s)
        {
            var obj = new Bone();
            obj.ReadMdx(s);
            return obj;
        }

        static Light ReadLight(BinaryStream s)
        {
            var obj = new Light();
            obj.ReadMdx(s);
            return obj;
        }

        static Helper ReadHelper(BinaryStream s)
        {
            var obj = new Helper();
            obj.ReadMdx(s);
            return obj;
        }

        static Attachment ReadAttachment(BinaryStream s)
        {
            var obj = new Attachment();
            obj.ReadMdx(s);
            return obj;
        }

        static ParticleEmitter ReadParticleEmitter(BinaryStream s)
        {
            var obj = new ParticleEmitter();
            obj.ReadMdx(s);
            return obj;
        }

        static ParticleEmitter2 ReadParticleEmitter2(BinaryStream s)
        {
            var obj = new ParticleEmitter2();
            obj.ReadMdx(s);
            return obj;
        }

        static ParticleEmitterPopcorn ReadParticleEmitterPopcorn(BinaryStream s)
        {
            var obj = new ParticleEmitterPopcorn();
            obj.ReadMdx(s);
            return obj;
        }

        static RibbonEmitter ReadRibbonEmitter(BinaryStream s)
        {
            var obj = new RibbonEmitter();
            obj.ReadMdx(s);
            return obj;
        }

        static Camera ReadCamera(BinaryStream s)
        {
            var obj = new Camera();
            obj.ReadMdx(s);
            return obj;
        }

        static EventObject ReadEventObject(BinaryStream s)
        {
            var obj = new EventObject();
            obj.ReadMdx(s);
            return obj;
        }

        static CollisionShape ReadCollisionShape(BinaryStream s)
        {
            var obj = new CollisionShape();
            obj.ReadMdx(s);
            return obj;
        }

        static FaceEffect ReadFaceEffect(BinaryStream s)
        {
            var obj = new FaceEffect();
            obj.ReadMdx(s);
            return obj;
        }
    }
}
