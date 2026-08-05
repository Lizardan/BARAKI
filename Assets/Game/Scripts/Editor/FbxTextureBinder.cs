using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds preview prefabs from the converted WC3 FBX under
    /// <c>Assets/NewModels/UnitModels/fbx_test</c>. Re-imports with material import mode
    /// None, then for each skin mesh assigns a URP Lit material:
    /// - TeamColor geosets get a solid white base (WC3 tints them at runtime with the
    ///   player color; the baked red palette reads as a red flood otherwise);
    /// - TeamGlow shadow/glow planes (tiny 4-vert quads) are disabled;
    /// - everything else gets Mat_&lt;texture&gt;.mat bound to its PNG.
    /// Prefabs are written next to the FBX so the setup survives re-imports.
    /// </summary>
    public static class FbxTextureBinder
    {
        public const string FbxFolder = "Assets/NewModels/UnitModels/fbx_test";
        public const string TextureFolder = "Assets/NewModels/UnitModels/textures";
        public const string MaterialFolder = FbxFolder + "/Materials";
        public const string PrefabFolder = "Assets/NewModels/UnitModels/prefabs";

        const string WhiteMatName = "Mat_TeamColorWhite";

        /// <summary>WC3 effect/doodad geosets that read as visual garbage on a static
        /// unit (guts, whirlwinds, lightning zaps, shockwave discs, blood splatter).
        /// They are particles in WC3 and should not be rendered as part of the unit.</summary>
        static readonly System.Collections.Generic.HashSet<string> JunkMaterials =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "gutz",
                "wirlwinds",
                "zap1",
                "shockwave1b",
                "tj_bloodsput",
            };

        /// <summary>Duplicate/secondary weapon geometry that reads as visual garbage on a
        /// static unit (an extra crossbow/bow kept for another animation, a scabbard sword
        /// that duplicates the one in hand, tiny duplicate meshes).</summary>
        static readonly System.Collections.Generic.HashSet<string> HiddenMeshNames =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "crossbowman by kitabatake.006",
                "gryphonrider.011",
                "knight unmounted.002",
                "t1mili3.001",
            };

        /// <summary>Meshes tinted in the player color. Mapped from the source MDX team-color
        /// geosets (materials whose texture has ReplaceableId=1), so the whole command-color
        /// part is covered: e.g. Footman's team-color geoset is Swordsman.005 (Arthas), not the
        /// Uther body. Each gets a copy of its textured material with an "_Accent" suffix so the
        /// preview panel can tint it without losing the base texture.</summary>
        static readonly System.Collections.Generic.Dictionary<string, string[]> AccentMeshes =
            new System.Collections.Generic.Dictionary<string, string[]>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Ballista_RoC", new[] { "balista.003", "balista.004" } },
                { "BlackGryphonRider", new[] { "GryphonRider.001", "GryphonRider.014" } },
                { "Crossbowman_By_Kitabatake", new[] { "Crossbowman by Kitabatake.001", "Crossbowman by Kitabatake.003" } },
                { "FelOrc_Crossbowman", new[] { "FelOrc_Crossbowman", "FelOrc_Crossbowman.001", "FelOrc_Crossbowman.003", "FelOrc_Crossbowman.004", "FelOrc_Crossbowman.005" } },
                { "Footman_Kul-Tiras_HD_TC", new[] { "Swordsman.005" } },
                { "KnightUnmounted", new[] { "Knight Unmounted.001" } },
                { "RogeFootman", new[] { "t1mili3.002", "t1mili3.003", "t1mili3.006" } },
            };

        [MenuItem("BARAKI/Units/Bind Fbx Test Textures")]
        public static void BindAll()
        {
            var fbxPaths = AssetDatabase.FindAssets("t:Model", new[] { FbxFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToArray();

            if (fbxPaths.Length == 0)
            {
                Debug.LogWarning("FbxTextureBinder: no FBX under " + FbxFolder);
                return;
            }

            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            foreach (var fbxPath in fbxPaths)
            {
                BuildPrefab(fbxPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("FbxTextureBinder: rebuilt " + fbxPaths.Length + " preview prefabs.");
        }

        static void BuildPrefab(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer != null && importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                Debug.LogWarning("FbxTextureBinder: cannot load " + fbxPath);
                return;
            }

            var name = Path.GetFileNameWithoutExtension(fbxPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            int hidden = 0;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                var matName = renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.name
                    : "Default-Material";
                matName = NormalizeMaterialName(matName);

                if (matName.StartsWith("TeamGlow", System.StringComparison.OrdinalIgnoreCase)
                    && IsGlowPlane(renderer))
                {
                    renderer.gameObject.SetActive(false);
                    hidden++;
                    continue;
                }

                if (JunkMaterials.Contains(matName))
                {
                    renderer.gameObject.SetActive(false);
                    hidden++;
                    continue;
                }

                if (HiddenMeshNames.Contains(renderer.gameObject.name))
                {
                    renderer.gameObject.SetActive(false);
                    hidden++;
                    continue;
                }

                if (AccentMeshes.TryGetValue(name, out var accentMeshes)
                    && System.Array.IndexOf(accentMeshes, renderer.gameObject.name) >= 0)
                {
                    renderer.sharedMaterial = GetAccentMaterial(matName);
                    continue;
                }

                renderer.sharedMaterial = ResolveMaterial(matName);
            }

            if (hidden > 0)
            {
                Debug.Log("FbxTextureBinder: " + name + " hid " + hidden + " glow/shadow planes and effect junk.");
            }

            var prefabPath = PrefabFolder + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
        }

        /// <summary>Glow/shadow quads are tiny flat planes (4 verts).</summary>
        static bool IsGlowPlane(SkinnedMeshRenderer renderer)
        {
            return renderer.sharedMesh != null && renderer.sharedMesh.vertexCount <= 8;
        }

        static Material ResolveMaterial(string matName)
        {
            if (matName.StartsWith("TeamColor", System.StringComparison.OrdinalIgnoreCase))
            {
                return GetWhiteMaterial();
            }

            return GetTexturedMaterial(matName);
        }

        /// <summary>Tintable copy of a textured material for command-color meshes. Uses the
        /// TeamColorDiffuse shader so the team tint is revealed through the WC3 alpha underlay
        /// (transparent texels) and gold pigment instead of multiplying over dark albedo.</summary>
        static Material GetAccentMaterial(string baseMatName)
        {
            var matName = NormalizeMaterialName(baseMatName);
            var matPath = MaterialFolder + "/Mat_" + matName + "_Accent.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material != null && material.shader == FindTeamColorShader())
            {
                return material;
            }

            if (material != null)
            {
                AssetDatabase.DeleteAsset(matPath);
            }

            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + "/Mat_" + matName + ".mat");
            material = new Material(FindTeamColorShader()) { name = "Mat_" + matName + "_Accent" };
            if (baseMat != null && baseMat.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", baseMat.GetTexture("_BaseMap"));
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            AssetDatabase.CreateAsset(material, matPath);
            return material;
        }

        static Material GetWhiteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + WhiteMatName + ".mat");
            if (material != null && material.shader != FindLitShader())
            {
                material.shader = FindLitShader();
            }

            if (material != null)
            {
                return material;
            }

            var shader = FindLitShader();
            material = new Material(shader) { name = WhiteMatName };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", null);
            }

            ApplyPreviewSettings(material);
            AssetDatabase.CreateAsset(material, MaterialFolder + "/" + WhiteMatName + ".mat");
            return material;
        }

        static Material GetTexturedMaterial(string matName)
        {
            var texture = FindTexture(matName);
            var matPath = MaterialFolder + "/Mat_" + matName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material != null && material.shader != FindLitShader())
            {
                material.shader = FindLitShader();
            }

            if (material == null)
            {
                material = new Material(FindLitShader()) { name = "Mat_" + matName };
                AssetDatabase.CreateAsset(material, matPath);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            ApplyPreviewSettings(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>WC3 geosets often use double-sided faces; URP Lit defaults to backface
        /// culling, which makes some parts look transparent from one side.</summary>
        static void ApplyPreviewSettings(Material material)
        {
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            // WC3 atlases carry alpha in their textures (feathers, cloth edges, cutouts);
            // with Opaque those texels render black. Clip them so the shape shows instead.
            // URP Lit ignores a manual _ALPHATEST_ON during runtime preview renders, but the
            // Unlit shader honors it, so preview materials use Unlit + alpha clip.
            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 1f);
                material.EnableKeyword("_ALPHATEST_ON");
            }

            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", 0.33f);
            }
        }

        static Shader FindLitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture");
        }

        static Shader FindTeamColorShader()
        {
            return AssetDatabase.LoadAssetAtPath<Shader>(
                       HumanAnimatedUnitSetup.TeamColorDiffuseShaderPath)
                   ?? Shader.Find("Game/Units/TeamColorDiffuse");
        }

        static Texture2D FindTexture(string matName)
        {
            var candidate = NormalizeMaterialName(matName);
            if (candidate.Length == 0)
            {
                return null;
            }

            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            foreach (var guid in textureGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, candidate, System.StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }

        /// <summary>Strip "Mat_" prefix and Blender ".001/.002" duplicate suffixes.</summary>
        static string NormalizeMaterialName(string name)
        {
            var result = name ?? string.Empty;
            while (result.StartsWith("Mat_", System.StringComparison.Ordinal))
            {
                result = result.Substring(4);
            }

            if (result.Length >= 5)
            {
                var dot = result.LastIndexOf('.');
                if (dot > 0 && result.Length - dot == 4)
                {
                    var suffix = result.Substring(dot + 1);
                    if (int.TryParse(suffix, out _))
                    {
                        result = result.Substring(0, dot);
                    }
                }
            }

            return result;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
