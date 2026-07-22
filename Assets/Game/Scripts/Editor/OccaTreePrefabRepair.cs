using System.IO;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class OccaTreePrefabRepair
    {
        const string SourceFolder = "Assets/OccaSoftware/Low Poly Fantasy Village/Prefabs";
        const string OutputFolder = "Assets/Game/Art/OccaTreeRepair";
        const string MaterialPath = OutputFolder + "/OccaTreeGradient.mat";
        const string TexturePath = OutputFolder + "/OccaTreeGradient.asset";

        static readonly string[] PrefabNames =
        {
            "Pine Tree_1", "Pine Tree_2", "Pine Tree_3", "Pine Tree_4", "Pine Tree_5",
            "Tree_1", "Tree_2", "Tree_3", "Tree_4", "Tree_5", "Tree_6", "Tree_7", "Tree_8",
        };

        static readonly string[] FlowerPrefabNames =
        {
            "Flower_1", "Flower_2", "Flower_3", "Flower_4", "Flower_5", "Flower Pot",
        };

        static readonly Color[] FlowerColors =
        {
            new(0.85f, 0.12f, 0.1f),
            new(1f, 0.72f, 0.08f),
            new(0.12f, 0.42f, 0.95f),
            new(1f, 0.28f, 0.62f),
            new(0.58f, 0.18f, 0.82f),
            new(0.95f, 0.45f, 0.15f),
        };

        [MenuItem("Tools/BARAKI/Repair Occa Environment Prefabs")]
        public static void RepairPrefabs()
        {
            EnsureOutputFolder();
            var material = CreateOrUpdateMaterial();
            var repairedCount = 0;

            for (var i = 0; i < PrefabNames.Length; i++)
            {
                var prefabPath = $"{SourceFolder}/{PrefabNames[i]}.prefab";
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var filters = root.GetComponentsInChildren<MeshFilter>(true);
                    for (var filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                    {
                        var source = filters[filterIndex].sharedMesh;
                        if (source == null)
                        {
                            continue;
                        }

                        var repaired = OccaPaletteMeshRepair.RepairMesh(source);
                        var assetName = $"{Sanitize(PrefabNames[i])}_{filterIndex}";
                        repaired.name = assetName;
                        var meshPath = $"{OutputFolder}/{assetName}.asset";
                        var meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (meshAsset == null)
                        {
                            repaired.hideFlags = HideFlags.None;
                            AssetDatabase.CreateAsset(repaired, meshPath);
                            meshAsset = repaired;
                        }
                        else
                        {
                            EditorUtility.CopySerialized(repaired, meshAsset);
                            Object.DestroyImmediate(repaired);
                        }

                        filters[filterIndex].sharedMesh = meshAsset;
                    }

                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        var materials = renderers[rendererIndex].sharedMaterials;
                        for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        {
                            materials[materialIndex] = material;
                        }

                        renderers[rendererIndex].sharedMaterials = materials;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    repairedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            RepairFlowerPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[OccaTreePrefabRepair] Repaired {repairedCount} tree prefabs " +
                $"and {FlowerPrefabNames.Length} flower prefabs.");
        }

        static void RepairFlowerPrefabs()
        {
            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/OccaSoftware/Low Poly Fantasy Village/Textures/Color.png");
            var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat");
            var readable = CreateReadableCopy(sourceTexture);
            try
            {
                for (var i = 0; i < FlowerPrefabNames.Length; i++)
                {
                    var material = CreateOrUpdateFlowerMaterial(
                        sourceMaterial,
                        readable,
                        i,
                        FlowerColors[i]);
                    var prefabPath = $"{SourceFolder}/{FlowerPrefabNames[i]}.prefab";
                    var root = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                        for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                        {
                            var materials = renderers[rendererIndex].sharedMaterials;
                            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                            {
                                materials[materialIndex] = material;
                            }

                            renderers[rendererIndex].sharedMaterials = materials;
                        }

                        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(readable);
            }
        }

        static Material CreateOrUpdateFlowerMaterial(
            Material sourceMaterial,
            Texture2D sourceTexture,
            int index,
            Color accent)
        {
            var assetName = $"OccaFlower{index + 1}";
            var texturePath = $"{OutputFolder}/{assetName}.asset";
            var texture = new Texture2D(
                sourceTexture.width,
                sourceTexture.height,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = assetName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = sourceTexture.GetPixels();
            for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                var color = pixels[pixelIndex];
                if (color.r > 0.25f
                    && color.r > color.g * 1.25f
                    && color.r > color.b * 1.25f)
                {
                    var brightness = Mathf.Max(color.r, 0.35f);
                    pixels[pixelIndex] = new Color(
                        accent.r * brightness,
                        accent.g * brightness,
                        accent.b * brightness,
                        color.a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            var textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (textureAsset == null)
            {
                AssetDatabase.CreateAsset(texture, texturePath);
                textureAsset = texture;
            }
            else
            {
                EditorUtility.CopySerialized(texture, textureAsset);
                Object.DestroyImmediate(texture);
            }

            var materialPath = $"{OutputFolder}/{assetName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(sourceMaterial) { name = assetName };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture("_BaseMap", textureAsset);
            material.SetTexture("_MainTex", textureAsset);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateOrUpdateMaterial()
        {
            var template = Resources.Load<Material>("Art/OccaColor");
            var runtimeMaterial = OccaPaletteMeshRepair.GetOrCreateTreeMaterial(template);
            var runtimeTexture = runtimeMaterial.GetTexture("_BaseMap") as Texture2D;

            var textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (textureAsset == null)
            {
                textureAsset = Object.Instantiate(runtimeTexture);
                textureAsset.name = "OccaTreeGradient";
                textureAsset.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(textureAsset, TexturePath);
            }
            else
            {
                EditorUtility.CopySerialized(runtimeTexture, textureAsset);
            }

            var materialAsset = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (materialAsset == null)
            {
                materialAsset = new Material(runtimeMaterial)
                {
                    name = "OccaTreeGradient",
                    hideFlags = HideFlags.None,
                };
                AssetDatabase.CreateAsset(materialAsset, MaterialPath);
            }
            else
            {
                EditorUtility.CopySerialized(runtimeMaterial, materialAsset);
                materialAsset.hideFlags = HideFlags.None;
            }

            materialAsset.SetTexture("_BaseMap", textureAsset);
            materialAsset.SetTexture("_MainTex", textureAsset);
            EditorUtility.SetDirty(materialAsset);
            return materialAsset;
        }

        static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Game/Art", "OccaTreeRepair");
            }
        }

        static string Sanitize(string value) =>
            Path.GetFileNameWithoutExtension(value).Replace(' ', '_');

        static Texture2D CreateReadableCopy(Texture2D source)
        {
            var target = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var copy = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return copy;
        }
    }
}
