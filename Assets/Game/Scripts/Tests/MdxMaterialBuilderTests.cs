using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Mdx;
using NUnit.Framework;
using UnityEngine;
using UnityMaterial = UnityEngine.Material;
using MdxFilterMode = Game.Mdx.FilterMode;

namespace Game.Tests
{
    public sealed class MdxMaterialBuilderTests
    {
        const string SourseFolder = "NewModels/SOURSE";

        static string SourcePath(string name)
        {
            return Path.Combine(Application.dataPath, SourseFolder, name);
        }

        static MdxModel Load(string name)
        {
            var path = SourcePath(name);
            Assert.IsTrue(File.Exists(path), "Missing test model: " + path);
            return MdxModel.Load(File.ReadAllBytes(path));
        }

        static List<UnityMaterial> Build(string name, IReadOnlyList<Texture2D> textures = null)
        {
            var shader = Shader.Find(MdxMaterialBuilder.ShaderName);
            Assert.IsNotNull(shader, "Wc3MdxSd shader must be compiled before material tests run.");
            return MdxMaterialBuilder.CreateMaterials(Load(name), textures, shader);
        }

        [Test]
        public void OneMaterialPerLayer_AcrossAllMdxMaterials()
        {
            foreach (var name in new[] { "Airstrike Rocket.mdx", "GnollBrawler.mdx" })
            {
                var model = Load(name);
                var materials = Build(name);

                var expected = model.Materials.Sum(m => m.Layers.Count);
                Assert.AreEqual(expected, materials.Count, name + " must create one material per layer");

                foreach (var mat in materials)
                {
                    Assert.AreEqual(MdxMaterialBuilder.ShaderName, mat.shader.name);
                }
            }
        }

        [Test]
        public void FilterMode_MapsToRenderStateAndQueue()
        {
            var model = Load("GnollBrawler.mdx");
            var materials = Build("GnollBrawler.mdx");

            var index = 0;
            foreach (var mdxMaterial in model.Materials)
            {
                foreach (var layer in mdxMaterial.Layers)
                {
                    var mat = materials[index++];
                    var mode = layer.FilterMode > (uint)MdxFilterMode.Modulate2x ? MdxFilterMode.Blend : (MdxFilterMode)layer.FilterMode;
                    var expected = ExpectedRenderState(mode);

                    Assert.AreEqual(expected.Src, mat.GetInt(MdxMaterialBuilder.PropSrcBlend), "layer filter " + mode + " src blend");
                    Assert.AreEqual(expected.Dst, mat.GetInt(MdxMaterialBuilder.PropDstBlend), "layer filter " + mode + " dst blend");
                    Assert.AreEqual(expected.ZWrite, mat.GetInt(MdxMaterialBuilder.PropZWrite), "layer filter " + mode + " zwrite");
                    Assert.AreEqual(expected.Discard, mat.GetFloat(MdxMaterialBuilder.PropAlphaDiscard), 0.001f, "layer filter " + mode + " discard");
                    Assert.AreEqual(expected.Threshold, mat.GetFloat(MdxMaterialBuilder.PropAlphaThreshold), 0.001f, "layer filter " + mode + " threshold");
                    Assert.AreEqual(ExpectedQueue(mode) + Mathf.Clamp(mdxMaterial.PriorityPlane, -1000, 1000), mat.renderQueue);
                }
            }
        }

        [Test]
        public void LayerAlpha_And_UnshadedFlag_AreBaked()
        {
            var model = Load("Airstrike Rocket.mdx");
            var materials = Build("Airstrike Rocket.mdx");

            var index = 0;
            foreach (var mdxMaterial in model.Materials)
            {
                foreach (var layer in mdxMaterial.Layers)
                {
                    var mat = materials[index++];

                    Assert.AreEqual(layer.Alpha, mat.GetFloat(MdxMaterialBuilder.PropLayerAlpha), 0.001f);

                    var expectUnshaded = (layer.Flags & (uint)LayerFlags.Unshaded) != 0 ? 1f : 0f;
                    Assert.AreEqual(expectUnshaded, mat.GetFloat(MdxMaterialBuilder.PropUnshaded), 0.001f);
                }
            }
        }

        [Test]
        public void NullModel_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => MdxMaterialBuilder.CreateMaterials(null, null));
        }

        struct ExpectedState
        {
            public int Src;
            public int Dst;
            public int ZWrite;
            public float Discard;
            public float Threshold;
        }

        static ExpectedState ExpectedRenderState(MdxFilterMode mode)
        {
            const int zero = (int)UnityEngine.Rendering.BlendMode.Zero;
            const int one = (int)UnityEngine.Rendering.BlendMode.One;
            const int dstColor = (int)UnityEngine.Rendering.BlendMode.DstColor;
            const int srcColor = (int)UnityEngine.Rendering.BlendMode.SrcColor;
            const int srcAlpha = (int)UnityEngine.Rendering.BlendMode.SrcAlpha;
            const int oneMinusSrcAlpha = (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

            switch (mode)
            {
                case MdxFilterMode.Transparent:
                    return new ExpectedState { Src = one, Dst = zero, ZWrite = 1, Discard = 1f, Threshold = 0.75f };
                case MdxFilterMode.Blend:
                    return new ExpectedState { Src = srcAlpha, Dst = oneMinusSrcAlpha, ZWrite = 0, Discard = 0f, Threshold = 0f };
                case MdxFilterMode.Additive:
                case MdxFilterMode.AddAlpha:
                    return new ExpectedState { Src = srcAlpha, Dst = one, ZWrite = 0, Discard = 0f, Threshold = 0f };
                case MdxFilterMode.Modulate:
                    return new ExpectedState { Src = zero, Dst = srcColor, ZWrite = 0, Discard = 1f, Threshold = 0.02f };
                case MdxFilterMode.Modulate2x:
                    return new ExpectedState { Src = dstColor, Dst = srcColor, ZWrite = 0, Discard = 1f, Threshold = 0.02f };
                default: // None -> Blend One Zero, ZWrite On.
                    return new ExpectedState { Src = one, Dst = zero, ZWrite = 1, Discard = 0f, Threshold = 0f };
            }
        }

        static int ExpectedQueue(MdxFilterMode mode)
        {
            switch (mode)
            {
                case MdxFilterMode.Transparent: return MdxMaterialBuilder.QueueAlphaTest;
                case MdxFilterMode.Blend:
                case MdxFilterMode.Additive:
                case MdxFilterMode.AddAlpha:
                case MdxFilterMode.Modulate:
                case MdxFilterMode.Modulate2x: return MdxMaterialBuilder.QueueTransparent;
                default: return MdxMaterialBuilder.QueueOpaque;
            }
        }
    }
}
