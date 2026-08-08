using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Mdx;
using UnityEditor;
using UnityEngine;
using UnityMaterial = UnityEngine.Material;

namespace Game.Editor
{
    /// <summary>
    /// Builds prefabs from raw WC3 MDX files under <c>Assets/NewModels/SOURSE</c>.
    /// Each prefab is a root GameObject carrying a <see cref="MdxInstance"/> (raw .mdx
    /// bytes as a TextAsset) plus one child renderer per (geoset, layer) pair, following
    /// the reference viewer's one-batch-per-(geoset, layer) rule:
    ///   - meshes come from <see cref="MdxMeshBuilder.Build"/> (raw WC3 space; the root
    ///     rotation -90&deg; around X handled by the parent, like the GLB pipeline);
    ///   - materials come from <see cref="MdxMaterialBuilder.CreateMaterials"/>;
    ///   - textures are decoded from local BLP files by basename, with generated
    ///     TeamColor/TeamGlow replacements and a white fallback for missing files.
    /// Outputs go to <c>Assets/NewModels/MODEL_PREFAB/{Sources,Textures,Materials,Meshes,Prefabs}</c>.
    /// </summary>
    public static class MdxPrefabBuilder
    {
        public const string SourceFolder = "Assets/NewModels/SOURSE";
        public const string SourceAssetFolder = "Assets/NewModels/MODEL_PREFAB/Sources";
        public const string TextureFolder = "Assets/NewModels/MODEL_PREFAB/Textures";
        public const string MaterialFolder = "Assets/NewModels/MODEL_PREFAB/Materials";
        public const string MeshFolder = "Assets/NewModels/MODEL_PREFAB/Meshes";
        public const string PrefabFolder = "Assets/NewModels/MODEL_PREFAB/Prefabs";

        public static readonly string[] TestModels =
        {
            "GryphonRider.mdx",
            "Crossbowman.mdx",
            "FelOrc_Crossbowman.mdx",
            "Knight_Kul-Tiras_HD_TC.mdx",
            "Footman_Kul-Tiras_HD_TC.mdx",
        };

        // Classic WC3 team colors (TeamColor01..TeamColor24 in the game's listing).
        static readonly Color32[] TeamColors =
        {
            new Color32(200, 0, 0, 255),     // 1  Red
            new Color32(0, 0, 200, 255),     // 2  Blue
            new Color32(0, 200, 200, 255),   // 3  Teal
            new Color32(200, 0, 200, 255),   // 4  Purple
            new Color32(255, 200, 0, 255),   // 5  Yellow
            new Color32(255, 100, 0, 255),   // 6  Orange
            new Color32(0, 200, 0, 255),     // 7  Green
            new Color32(255, 100, 200, 255), // 8  Pink
            new Color32(100, 100, 100, 255), // 9  Gray
            new Color32(100, 150, 255, 255), // 10 Light Blue
            new Color32(0, 100, 0, 255),     // 11 Dark Green
            new Color32(100, 50, 0, 255),    // 12 Brown
            new Color32(150, 0, 100, 255),   // 13 Maroon
            new Color32(0, 0, 100, 255),     // 14 Navy
            new Color32(0, 150, 150, 255),   // 15 Turquoise
            new Color32(150, 100, 200, 255), // 16 Violet
            new Color32(200, 150, 50, 255),  // 17 Wheat
            new Color32(255, 150, 100, 255), // 18 Peach
            new Color32(150, 255, 150, 255), // 19 Mint
            new Color32(200, 150, 255, 255), // 20 Lavender
            new Color32(50, 50, 50, 255),    // 21 Coal
            new Color32(255, 250, 250, 255), // 22 Snow
            new Color32(0, 150, 100, 255),   // 23 Emerald
            new Color32(150, 100, 50, 255),  // 24 Peanut
        };

        // basename (case-insensitive) -> full BLP path under SourceFolder. Built lazily.
        static Dictionary<string, string> _blpIndex;
        // basename -> resolved Texture2D asset (cached across models in a run).
        static Dictionary<string, Texture2D> _textureCache;
        // basenames already proven missing (so we skip the recursive search next time).
        static readonly HashSet<string> MissingTextures = new(StringComparer.OrdinalIgnoreCase);

        [MenuItem("BARAKI/Units/Rebuild MDX Prefabs (MODEL_PREFAB)")]
        public static void RebuildAllFromMenu()
        {
            RebuildAll(null);
        }

        [MenuItem("BARAKI/Units/Build MDX Test Prefabs (5)")]
        public static void RebuildTestSetFromMenu()
        {
            RebuildAll(TestModels);
        }

        /// <summary>Rebuilds a single MDX prefab by name (throws on failure).</summary>
        public static void BuildOne(string mdxName)
        {
            BuildPrefab(FindMdx(mdxName));
        }

        [MenuItem("BARAKI/Units/Rebuild Placed MDX Prefabs (delete mats)")]
        public static void RebuildPlacedForceFromMenu()
        {
            var names = new[]
            {
                "Anzu", "Arakkoa", "ArakkoaMelee", "ArakkoaWarrior", "Arakkoa_ikar", "Arakkoa_Sage",
                "AstroSatyr", "Balrog", "BlackGryphonRider", "Bombardier", "ChaosMurloc",
                "Cleric_rogue", "Crossbowman", "Crossbowman_By_Kitabatake", "Eudora_experiment",
                "FelGeneralRed", "FelOrc_Crossbowman", "FootArcher", "Footman_Kul-Tiras_HD_TC",
                "Furion_RoC", "General_Draven", "GnollSlasher", "GnollStalker", "GryphonRider",
                "Guardsman_Kul-Tiras_HD_TC", "HeroSlicerv2", "Jenoth", "Kiro_experiment",
                "Knight_Kul-Tiras_HD_TC", "SkyHydra"
            };

            foreach (var n in names)
            {
                foreach (var f in Directory.GetFiles(ProjectPathToDisk(MaterialFolder), "Mat_" + n + "_*.mat"))
                {
                    File.Delete(f);
                    if (File.Exists(f + ".meta"))
                    {
                        File.Delete(f + ".meta");
                    }
                }
            }

            AssetDatabase.Refresh();
            RebuildAll(names.Select(x => x + ".mdx").ToArray());
            Debug.Log("RebuildPlaced: materials deleted and prefabs rebuilt for " + names.Length + " models.");
        }

        static string FindMdx(string fileName)
        {
            var matches = Directory.GetFiles(SourceFolder, fileName, SearchOption.AllDirectories);
            return matches.Length > 0 ? matches[0] : SourceFolder + "/" + fileName;
        }

        /// <summary>Rebuilds prefabs for the given MDX names (or all MDX in SOURSE when null).</summary>
        public static int RebuildAll(string[] modelNames)
        {
            EnsureFolders();
            EnsureTeamColorTextures();
            _blpIndex = null;
            _textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            MissingTextures.Clear();

            var files = modelNames != null
                ? modelNames.Select(FindMdx).ToArray()
                : Directory.GetFiles(SourceFolder, "*.mdx", SearchOption.AllDirectories);

            var built = 0;
            var failed = new List<string>();
            foreach (var mdx in files)
            {
                try
                {
                    BuildPrefab(mdx);
                    built++;
                }
                catch (Exception e)
                {
                    failed.Add(Path.GetFileName(mdx) + " : " + e.Message);
                    Debug.LogError("MdxPrefabBuilder: failed " + mdx + " -> " + e);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("MdxPrefabBuilder: built " + built + "/" + files.Length + " MDX prefabs."
                + (failed.Count > 0 ? " FAILED: " + string.Join(" | ", failed) : ""));
            return built;
        }

        static void BuildPrefab(string mdxPath)
        {
            _blpIndex ??= BuildBlpIndex();
            _textureCache ??= new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            EnsureFolders();

            var unitName = Path.GetFileNameWithoutExtension(mdxPath);
            var model = MdxModel.Load(File.ReadAllBytes(mdxPath));
            if (model == null)
            {
                throw new InvalidOperationException("Failed to parse " + mdxPath);
            }

            var meshData = MdxMeshBuilder.Build(model);
            var textures = ResolveModelTextures(model);
            var shader = Shader.Find(MdxMaterialBuilder.ShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Missing shader " + MdxMaterialBuilder.ShaderName);
            }

            var source = EnsureSourceAsset(mdxPath);
            var materials = CreateMaterialAssets(unitName, model, textures, shader);

            var root = new GameObject("Mdx_" + unitName);
            var instance = root.AddComponent<MdxInstance>();

            var bindings = new List<MdxRendererBinding>();

            // Runtime layer index is material-major (Material order, then Layers), matching BuildLayers.
            var layerStart = new int[model.Materials.Count];
            var layerTotal = 0;
            for (var m = 0; m < model.Materials.Count; m++)
            {
                layerStart[m] = layerTotal;
                layerTotal += model.Materials[m].Layers.Count;
            }

            foreach (var geoset in meshData.Geosets)
            {
                if (ShouldSkipGeoset(unitName, geoset.SourceGeosetIndex))
                {
                    continue;
                }

                var mesh = EnsureMeshAsset(unitName, geoset);
                var material = model.Materials[geoset.MaterialId];

                for (var l = 0; l < material.Layers.Count; l++)
                {
                    var layerIndex = layerStart[geoset.MaterialId] + l;
                    var go = new GameObject("g" + geoset.SourceGeosetIndex + "_l" + layerIndex);
                    go.transform.SetParent(root.transform, false);

                    var meshFilter = go.AddComponent<MeshFilter>();
                    meshFilter.sharedMesh = mesh;

                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[layerIndex];
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;

                    bindings.Add(new MdxRendererBinding
                    {
                        renderer = renderer,
                        geosetIndex = geoset.SourceGeosetIndex,
                        layerIndex = layerIndex,
                    });
                }
            }

            var teamColor = LoadTeamTextures("TeamColor");
            var teamGlow = LoadTeamTextures("TeamGlow");

            var so = new SerializedObject(instance);
            so.FindProperty("source").objectReferenceValue = source;
            SetBindingList(so, "rendererBindings", bindings);
            SetTextureList(so, "textures", textures);
            so.FindProperty("textureReplaceable").arraySize = model.Textures.Count;
            for (var i = 0; i < model.Textures.Count; i++)
            {
                so.FindProperty("textureReplaceable").GetArrayElementAtIndex(i).intValue = ResolveReplaceableId(model.Textures[i]);
            }
            SetTextureList(so, "teamColorTextures", teamColor);
            SetTextureList(so, "teamGlowTextures", teamGlow);
            so.FindProperty("sequenceLoopMode").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefabPath = PrefabFolder + "/" + unitName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log("MdxPrefabBuilder: " + unitName + " -> " + prefabPath
                + " (" + meshData.Geosets.Count + " geosets, " + bindings.Count + " batches)");
        }

        // --- Assets -----------------------------------------------------------

        static List<UnityMaterial> CreateMaterialAssets(string unitName, MdxModel model, IReadOnlyList<Texture2D> textures, Shader shader)
        {
            var created = MdxMaterialBuilder.CreateMaterials(model, textures, shader);
            var result = new List<UnityMaterial>(created.Count);

            for (var i = 0; i < created.Count; i++)
            {
                var path = MaterialFolder + "/Mat_" + unitName + "_" + i + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<UnityMaterial>(path);
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[i]);
                    result.Add(existing);
                    continue;
                }

                var mat = created[i];
                mat.name = "Mat_" + unitName + "_" + i;
                AssetDatabase.CreateAsset(mat, path);
                result.Add(mat);
            }

            return result;
        }

        static bool ShouldSkipGeoset(string unitName, int geosetIndex)
        {
            // Footman_Kul-Tiras_HD_TC: geoset 15 is a stray vertical TeamGlow quad
            // bound to a root bone pivoted far off-model. The model has no visibility
            // tracks, so it renders as an always-visible square beside the unit.
            return unitName == "Footman_Kul-Tiras_HD_TC" && geosetIndex == 15;
        }

        static Mesh EnsureMeshAsset(string unitName, MdxMeshGeoset geoset)
        {
            var path = MeshFolder + "/" + unitName + "_g" + geoset.SourceGeosetIndex + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(geoset.Mesh);
                return existing;
            }

            geoset.Mesh.name = unitName + "_g" + geoset.SourceGeosetIndex;
            AssetDatabase.CreateAsset(geoset.Mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        static TextAsset EnsureSourceAsset(string mdxPath)
        {
            var dest = SourceAssetFolder + "/" + Path.GetFileName(mdxPath) + ".bytes";
            var disk = Path.Combine(Application.dataPath, "..", dest);
            disk = Path.GetFullPath(disk);
            if (!File.Exists(disk))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(disk));
                File.Copy(mdxPath, disk, true);
                AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);
            }

            return AssetDatabase.LoadAssetAtPath<TextAsset>(dest);
        }

        // --- Textures ---------------------------------------------------------

        /// <summary>One entry per model texture; replaceable slots are null (runtime picks team textures).</summary>
        static List<Texture2D> ResolveModelTextures(MdxModel model)
        {
            var result = new List<Texture2D>(model.Textures.Count);
            foreach (var texture in model.Textures)
            {
                if (ResolveReplaceableId(texture) != 0)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(ResolveLocalTexture(texture.Path));
            }

            return result;
        }

        /// <summary>
        /// TeamColor/TeamGlow detection. Models usually set ReplaceableId (1/2), but some mappers
        /// ship TeamGlow textures with ReplaceableId=0 and only the "ReplaceableTextures\TeamGlow\"
        /// path to disambiguate, so fall back to path inspection.
        /// </summary>
        static int ResolveReplaceableId(Game.Mdx.Texture texture)
        {
            if (texture.ReplaceableId == 1 || texture.ReplaceableId == 2)
            {
                return (int)texture.ReplaceableId;
            }

            var path = texture.Path;
            if (path.StartsWith("ReplaceableTextures\\TeamColor", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (path.StartsWith("ReplaceableTextures\\TeamGlow", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return (int)texture.ReplaceableId;
        }

        static Texture2D ResolveLocalTexture(string mdxTexturePath)
        {
            var clean = mdxTexturePath.Replace('\\', '/');
            var baseName = Path.GetFileName(clean);
            var key = Path.GetFileNameWithoutExtension(clean);

            if (_textureCache.TryGetValue(baseName, out var cached))
            {
                return cached;
            }

            if (MissingTextures.Contains(baseName))
            {
                _textureCache[baseName] = EnsureWhiteTexture();
                return _textureCache[baseName];
            }

            var filePath = FindTextureFile(baseName, key);
            if (filePath != null)
            {
                var tex = EnsureTextureFromFile(filePath, key);
                _textureCache[baseName] = tex;
                return tex;
            }

            MissingTextures.Add(baseName);
            var fallback = EnsureWhiteTexture();
            _textureCache[baseName] = fallback;
            Debug.LogWarning("MdxPrefabBuilder: no local texture for '" + baseName + "', using white fallback.");
            return fallback;
        }

        static string FindTextureFile(string baseName, string key)
        {
            _blpIndex ??= BuildBlpIndex();
            // Prefer DDS sources: they are the original (Reforged HD) textures. Local BLP
            // files are lossy JPEG conversions that are sometimes broken (CMYK-encoded,
            // decode inverted), so they are only used when no DDS exists for the basename.
            if (_blpIndex.TryGetValue(key + ".dds", out var path))
            {
                return path;
            }
            if (_blpIndex.TryGetValue(baseName, out path))
            {
                return path;
            }
            if (_blpIndex.TryGetValue(key + ".blp", out path))
            {
                return path;
            }

            foreach (var entry in _blpIndex)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(entry.Key), key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        static Dictionary<string, string> BuildBlpIndex()
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(SourceFolder, "*.blp", SearchOption.AllDirectories))
            {
                index[Path.GetFileName(file)] = file;
            }

            foreach (var file in Directory.GetFiles(SourceFolder, "*.dds", SearchOption.AllDirectories))
            {
                index[Path.GetFileName(file)] = file;
            }

            return index;
        }

        static Texture2D EnsureTextureFromFile(string path, string key)
        {
            var pngPath = TextureFolder + "/" + key + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (existing != null)
            {
                return existing;
            }

            byte[] rgba;
            int width;
            int height;
            if (path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                if (!DdsDecoder.TryDecode(File.ReadAllBytes(path), out width, out height, out rgba))
                {
                    throw new InvalidOperationException("Failed to decode DDS " + path);
                }
            }
            else
            {
                var blp = new BlpImage();
                blp.Load(File.ReadAllBytes(path));
                rgba = blp.GetMipmap(0, out width, out height);
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rgba);
            tex.Apply(false, false);

            var pngBytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            var disk = ProjectPathToDisk(pngPath);
            Directory.CreateDirectory(Path.GetDirectoryName(disk));
            File.WriteAllBytes(disk, pngBytes);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        static void EnsureTeamColorTextures()
        {
            for (var i = 0; i < TeamColors.Length; i++)
            {
                var path = TextureFolder + "/TeamColor" + i.ToString("D2") + ".png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                {
                    continue;
                }

                var tex = GenerateTeamColorTexture(TeamColors[i]);
                SaveGeneratedPng(tex, path);
            }

            for (var i = 0; i < TeamColors.Length; i++)
            {
                var path = TextureFolder + "/TeamGlow" + i.ToString("D2") + ".png";
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                {
                    continue;
                }

                var tex = GenerateTeamGlowTexture(TeamColors[i]);
                SaveGeneratedPng(tex, path);
            }
        }

        static Texture2D EnsureWhiteTexture()
        {
            var path = TextureFolder + "/white.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            SaveGeneratedPng(tex, path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SaveGeneratedPng(Texture2D tex, string assetPath)
        {
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            var disk = ProjectPathToDisk(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(disk));
            File.WriteAllBytes(disk, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
        }

        // WC3 TeamColor textures are a color band; approximate with a vertical
        // highlight-to-shade gradient so team-colored polygons read as tinted.
        static Texture2D GenerateTeamColorTexture(Color32 color)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var t = y / (float)(size - 1);
                var brightness = 1.35f - t * 0.7f;
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp((int)(color.r * brightness), 0, 255),
                        (byte)Mathf.Clamp((int)(color.g * brightness), 0, 255),
                        (byte)Mathf.Clamp((int)(color.b * brightness), 0, 255),
                        255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        // Soft radial glow used by TeamGlow-replaceable layers.
        static Texture2D GenerateTeamGlowTexture(Color32 color)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var half = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), center) / half;
                    var intensity = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    var r = Mathf.Lerp(255, color.r, 0.5f) * (0.3f + 0.7f * intensity);
                    var g = Mathf.Lerp(255, color.g, 0.5f) * (0.3f + 0.7f * intensity);
                    var b = Mathf.Lerp(255, color.b, 0.5f) * (0.3f + 0.7f * intensity);
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp((int)r, 0, 255),
                        (byte)Mathf.Clamp((int)g, 0, 255),
                        (byte)Mathf.Clamp((int)b, 0, 255),
                        (byte)Mathf.Clamp((int)(intensity * 255f), 0, 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static List<Texture2D> LoadTeamTextures(string prefix)
        {
            var result = new List<Texture2D>(TeamColors.Length);
            for (var i = 0; i < TeamColors.Length; i++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + prefix + i.ToString("D2") + ".png");
                if (tex != null)
                {
                    result.Add(tex);
                }
            }

            return result;
        }

        // --- Folders / serialization -------------------------------------------

        static void EnsureFolders()
        {
            EnsureFolder(SourceAssetFolder);
            EnsureFolder(TextureFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);
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

        static void SetTextureList(SerializedObject so, string property, IReadOnlyList<Texture2D> list)
        {
            var prop = so.FindProperty(property);
            prop.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
        }

        static void SetBindingList(SerializedObject so, string property, IReadOnlyList<MdxRendererBinding> list)
        {
            var prop = so.FindProperty(property);
            prop.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("renderer").objectReferenceValue = list[i].renderer;
                element.FindPropertyRelative("geosetIndex").intValue = list[i].geosetIndex;
                element.FindPropertyRelative("layerIndex").intValue = list[i].layerIndex;
            }
        }

        static string ProjectPathToDisk(string assetPath)
        {
            var normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, "..", normalized);
        }
    }
}
