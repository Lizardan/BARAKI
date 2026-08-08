using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMaterial = UnityEngine.Material;

namespace Game.Mdx
{
    /// <summary>
    /// Creates Unity materials for an MDX model's SD layers, mirroring the reference
    /// viewer's "one batch per (geoset, layer)" rule: every <see cref="Material.Layers"/>
    /// entry becomes its own material so each layer renders with its own texture,
    /// filter mode and blend state. Filter modes are baked into the shader's
    /// property-driven render state (<c>_SrcBlend/_DstBlend/_ZWrite/_Cull</c> in
    /// <c>Wc3MdxSd.shader</c>) and render queues, following <c>filtermode.ts</c>.
    /// </summary>
    public static class MdxMaterialBuilder
    {
        public const string ShaderName = "Game/Units/Wc3MdxSd";

        public const string PropBaseMap = "_BaseMap";
        public const string PropVertexColor = "_VertexColor";
        public const string PropGeosetColor = "_GeosetColor";
        public const string PropLayerAlpha = "_LayerAlpha";
        public const string PropUnshaded = "_Unshaded";
        public const string PropUvTranslate = "_UvTranslate";
        public const string PropUvRotate = "_UvRotate";
        public const string PropUvScale = "_UvScale";
        public const string PropLightDirection = "_LightDirection";
        public const string PropBoneMap = "_BoneMap";
        public const string PropSrcBlend = "_SrcBlend";
        public const string PropDstBlend = "_DstBlend";
        public const string PropZWrite = "_ZWrite";
        public const string PropCull = "_Cull";
        public const string PropAlphaDiscard = "_AlphaDiscard";
        public const string PropAlphaThreshold = "_AlphaThreshold";

        public const int QueueOpaque = 2000;
        public const int QueueAlphaTest = 2450;
        public const int QueueTransparent = 3000;

        /// <summary>Creates one material per (material, layer), in MDX material order.</summary>
        public static List<UnityMaterial> CreateMaterials(MdxModel model, IReadOnlyList<Texture2D> textures, Shader shader = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            shader = shader != null ? shader : Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Missing shader " + ShaderName + ". Import/compile Wc3MdxSd.shader first.");
            }

            var result = new List<UnityMaterial>();

            foreach (var material in model.Materials)
            {
                foreach (var layer in material.Layers)
                {
                    var mat = new UnityMaterial(shader) { name = "MDX_" + result.Count };

                    mat.SetColor(PropVertexColor, Color.white);
                    mat.SetColor(PropGeosetColor, Color.white);
                    mat.SetFloat(PropLayerAlpha, layer.Alpha);
                    mat.SetFloat(PropUnshaded, (layer.Flags & (uint)LayerFlags.Unshaded) != 0 ? 1f : 0f);

                    // Identity texture animation state.
                    mat.SetVector(PropUvTranslate, Vector4.zero);
                    mat.SetVector(PropUvRotate, new Vector4(0f, 1f, 0f, 0f));
                    mat.SetFloat(PropUvScale, 1f);
                    mat.SetVector(PropLightDirection, new Vector4(0f, 1f, 0f, 0f));

                    var textureId = layer.TextureId;
                    if (textureId >= 0 && textures != null && textureId < textures.Count && textures[textureId] != null)
                    {
                        mat.SetTexture(PropBaseMap, textures[textureId]);
                    }

                    mat.SetFloat(PropCull, (layer.Flags & (uint)LayerFlags.TwoSided) != 0 ? 0f : 2f);

                    var filterMode = layer.FilterMode;
                    if (filterMode > (uint)FilterMode.Modulate2x)
                    {
                        // The game downgrades unknown modes to Blend.
                        filterMode = (uint)FilterMode.Blend;
                    }

                    // TeamGlow (replaceable 2) is a glow/light quad: WC3 always draws it as
                    // a soft transparent additive glow regardless of the model's authored
                    // filter mode. Some mappers set FilterMode=None/Transparent on these
                    // layers, which otherwise renders an opaque square behind the unit.
                    int replaceableId = 0;
                    if (layer.TextureId >= 0 && layer.TextureId < model.Textures.Count)
                    {
                        replaceableId = (int)model.Textures[layer.TextureId].ReplaceableId;
                    }

                    if (replaceableId == 2)
                    {
                        ApplyFilterMode(mat, FilterMode.AddAlpha, material.PriorityPlane);
                    }
                    else
                    {
                        ApplyFilterMode(mat, (FilterMode)filterMode, material.PriorityPlane);
                    }

                    result.Add(mat);
                }
            }

            return result;
        }

        static void ApplyFilterMode(UnityMaterial mat, FilterMode mode, int priorityPlane)
        {
            var priority = Mathf.Clamp(priorityPlane, -1000, 1000);

            switch (mode)
            {
                case FilterMode.None:
                    // Blend One Zero == Blend Off, ZWrite On, no alpha discard.
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat(PropZWrite, 1f);
                    mat.SetFloat(PropAlphaDiscard, 0f);
                    mat.SetFloat(PropAlphaThreshold, 0f);
                    mat.renderQueue = QueueOpaque + priority;
                    break;
                case FilterMode.Transparent:
                    // Opaque render state + 1-bit alpha discard.
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat(PropZWrite, 1f);
                    mat.SetFloat(PropAlphaDiscard, 1f);
                    mat.SetFloat(PropAlphaThreshold, 0.75f);
                    mat.renderQueue = QueueAlphaTest + priority;
                    break;
                case FilterMode.Blend:
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat(PropZWrite, 0f);
                    mat.SetFloat(PropAlphaDiscard, 0f);
                    mat.SetFloat(PropAlphaThreshold, 0f);
                    mat.renderQueue = QueueTransparent + priority;
                    break;
                case FilterMode.Additive:
                case FilterMode.AddAlpha:
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.One);
                    mat.SetFloat(PropZWrite, 0f);
                    mat.SetFloat(PropAlphaDiscard, 0f);
                    mat.SetFloat(PropAlphaThreshold, 0f);
                    mat.renderQueue = QueueTransparent + priority;
                    break;
                case FilterMode.Modulate:
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.SrcColor);
                    mat.SetFloat(PropZWrite, 0f);
                    mat.SetFloat(PropAlphaDiscard, 1f);
                    mat.SetFloat(PropAlphaThreshold, 0.02f);
                    mat.renderQueue = QueueTransparent + priority;
                    break;
                case FilterMode.Modulate2x:
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.DstColor);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.SrcColor);
                    mat.SetFloat(PropZWrite, 0f);
                    mat.SetFloat(PropAlphaDiscard, 1f);
                    mat.SetFloat(PropAlphaThreshold, 0.02f);
                    mat.renderQueue = QueueTransparent + priority;
                    break;
                default:
                    mat.SetFloat(PropSrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat(PropDstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat(PropZWrite, 0f);
                    mat.SetFloat(PropAlphaDiscard, 0f);
                    mat.SetFloat(PropAlphaThreshold, 0f);
                    mat.renderQueue = QueueTransparent + priority;
                    break;
            }
        }
    }
}
