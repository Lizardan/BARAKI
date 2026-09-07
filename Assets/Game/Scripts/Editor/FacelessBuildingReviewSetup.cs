using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>Builds static Faceless building review prefabs (Nazjatar MDX) plus the numbered review scene.</summary>
    public static class FacelessBuildingReviewSetup
    {
        const string SourceFolder = "Assets/Nazjatar_Houses_By_Ageron";
        const string TexFolder = "Assets/Game/Art/Races/Faceless/Buildings/Review/Textures";
        const string MeshFolder = "Assets/Game/Art/Races/Faceless/Buildings/Review/Meshes";
        const string MatFolder = "Assets/Game/Art/Races/Faceless/Buildings/Review/Mats";
        const string PrefabFolder = "Assets/Game/Prefabs/Races/Faceless/_ReviewBuildings";
        const string ScenePath = "Assets/Game/Scenes/Dev/FacelessBuildingsReview.unity";
        const float ReviewMaxDimension = 6f;
        const float RowSpacing = 16f;
        const float HumanRowZ = 0f;
        const float FacelessRowZ = 30f;

        static readonly (string Stem, string FileName)[] Models =
        {
            ("01_FacelessBuilding", "8nzj_nazjatar_building_small006_2532410.mdx"),
            ("02_FacelessBuilding", "8nzj_nazjatar_building_small01_2324290.mdx"),
            ("03_FacelessBuilding", "8nzj_nazjatar_building_small01_2565378.mdx"),
            ("04_FacelessBuilding", "8nzj_nazjatar_building_small01_2578738.mdx"),
            ("05_FacelessBuilding", "8nzj_nazjatar_building_small03_2406772.mdx"),
        };

        [MenuItem("BARAKI/Faceless/Rebuild Building Review")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static string RebuildAll()
        {
            ContentAssetPaths.EnsureFolder("Assets/Game/Art/Races/Faceless/Buildings/Review");
            ContentAssetPaths.EnsureFolder(TexFolder);
            ContentAssetPaths.EnsureFolder(MeshFolder);
            ContentAssetPaths.EnsureFolder(MatFolder);
            ContentAssetPaths.EnsureFolder(PrefabFolder);
            ContentAssetPaths.EnsureFolder("Assets/Game/Scenes/Dev");

            if (Directory.Exists(TexFolder))
            {
                foreach (var texPath in Directory.GetFiles(TexFolder, "*.png"))
                {
                    ConfigureAlbedo(texPath.Replace('\\', '/'));
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var log = new List<string>();
            var stems = new string[Models.Length];
            for (var i = 0; i < Models.Length; i++)
            {
                var (stem, fileName) = Models[i];
                stems[i] = stem;
                var src = Path.Combine(SourceFolder, fileName).Replace('\\', '/');
                var doc = FacelessMdxDocument.Load(src);
                var (mesh, materials) = BuildStaticMesh(stem, doc, MeshFolder, MatFolder);
                var prefabPath = PrefabFolder + "/" + stem + ".prefab";
                BuildPrefab(stem, mesh, materials, prefabPath);
                var size = mesh.bounds.size;
                log.Add($"{stem}: geosets={doc.Geosets.Count} bounds={size.x:F2}x{size.y:F2}x{size.z:F2} mats={materials.Length}");
            }

            PopulateScene(stems);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FacelessBuildingReviewSetup:\n" + string.Join("\n", log));
            return string.Join("\n", log);
        }

        internal static (Mesh mesh, Material[] materials) BuildStaticMesh(
            string stem,
            FacelessMdxDocument doc,
            string meshFolder,
            string matFolder)
        {
            var groups = new List<(string Stem, List<FacelessMdxDocument.Geoset> Geosets, bool TwoSided, bool HasTeamColor, bool ClipBlack, bool AlphaBlend)>();
            var index = new Dictionary<string, int>();
            foreach (var g in doc.Geosets)
            {
                var key = g.TextureStem
                          + (g.TwoSided ? "_2s" : "")
                          + (g.HasTeamColor ? "_team" : "")
                          + (g.ClipBlack ? "_clip" : "")
                          + (g.AlphaBlend ? "_blend" : "");
                if (!index.TryGetValue(key, out var gi))
                {
                    gi = groups.Count;
                    index[key] = gi;
                    groups.Add((g.TextureStem, new List<FacelessMdxDocument.Geoset>(), g.TwoSided, g.HasTeamColor, g.ClipBlack, g.AlphaBlend));
                }

                groups[gi].Geosets.Add(g);
            }

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var subTris = new List<int>[groups.Count];
            var materials = new Material[groups.Count];
            for (var s = 0; s < groups.Count; s++)
            {
                var (texStem, list, twoSided, hasTeamColor, clipBlack, alphaBlend) = groups[s];
                materials[s] = EnsureReviewMaterial(texStem, twoSided, hasTeamColor, clipBlack, alphaBlend, matFolder);
                var tris = new List<int>();
                foreach (var g in list)
                {
                    var offset = verts.Count;
                    verts.AddRange(g.Vertices);
                    norms.AddRange(g.Normals);
                    uvs.AddRange(g.Uvs);
                    for (var t = 0; t + 2 < g.Triangles.Length; t += 3)
                    {
                        tris.Add(g.Triangles[t] + offset);
                        tris.Add(g.Triangles[t + 2] + offset);
                        tris.Add(g.Triangles[t + 1] + offset);
                    }
                }

                subTris[s] = tris;
            }

            var mesh = new Mesh { name = stem };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = groups.Count;
            for (var s = 0; s < groups.Count; s++)
            {
                mesh.SetTriangles(subTris[s], s, true);
            }

            mesh.RecalculateBounds();
            var meshPath = meshFolder + "/" + stem + ".asset";
            return (SaveAsset(mesh, meshPath), materials);
        }

        internal static Material EnsureReviewMaterial(
            string textureStem,
            bool twoSided,
            bool hasTeamColor,
            bool clipBlack,
            bool alphaBlend,
            string matFolder)
        {
            var assetName = textureStem
                            + (twoSided ? "_2S" : "")
                            + (hasTeamColor ? "_Team" : "")
                            + (clipBlack ? "_Clip" : "")
                            + (alphaBlend ? "_Blend" : "")
                            + "_Unlit";
            var assetPath = matFolder + "/" + assetName + ".mat";
            var shader = Shader.Find("Game/Faceless/ReviewUnlit");
            if (shader == null)
            {
                throw new InvalidOperationException("Missing shader Game/Faceless/ReviewUnlit");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(mat, assetPath);
                mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            }
            else
            {
                mat.shader = shader;
            }

            var tex = LoadTexture(textureStem);
            if (tex == null && textureStem != "TeamColor")
            {
                Debug.LogError("Faceless building review texture missing: " + textureStem);
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_TeamColor", MatchPlayerColors.GetSlotColor(1));
            mat.SetFloat("_UseTeam", hasTeamColor && textureStem != "TeamColor" ? 1f : 0f);
            mat.SetFloat("_UseBlend", alphaBlend ? 1f : 0f);
            mat.SetFloat("_AlphaClip", clipBlack ? 0.12f : 0f);
            mat.SetFloat("_AtlasClip", 0f);
            mat.SetFloat("_Cull", twoSided || alphaBlend || clipBlack ? 0f : 2f);
            mat.SetFloat("_SrcBlend", alphaBlend ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            mat.SetFloat("_DstBlend", alphaBlend ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", alphaBlend ? 0f : 1f);
            mat.renderQueue = alphaBlend ? 3000 : clipBlack ? 2450 : 2000;
            if (textureStem == "TeamColor")
            {
                mat.SetColor("_BaseColor", MatchPlayerColors.GetSlotColor(1));
                mat.SetFloat("_UseTeam", 0f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        static Texture2D LoadTexture(string textureStem)
        {
            if (textureStem == "TeamColor")
            {
                return Texture2D.whiteTexture;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + "/" + textureStem + ".png");
        }

        static void BuildPrefab(string stem, Mesh mesh, Material[] materials, string prefabPath)
        {
            var root = new GameObject(stem);
            try
            {
                var filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void PopulateScene(string[] stems)
        {
            Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SetActiveScene(scene);
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var level = new GameObject("--- LEVEL ---");
            SceneManager.MoveGameObjectToScene(level, scene);

            var humanRow = new List<(string Label, string PrefabPath)>
            {
                ("TownHall", TtBuildingVisualSetup.TownHallPath),
                ("Tower", TtBuildingVisualSetup.TowerPath),
                ("Barracks", TtBuildingVisualSetup.BarracksPath),
            };
            for (var i = 0; i < humanRow.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(humanRow[i].PrefabPath);
                if (prefab == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(level.transform, true);
                instance.transform.position = new Vector3(i * RowSpacing, 0f, HumanRowZ);
                var top = GetBoundsTop(instance);
                CreateLabel(level.transform, humanRow[i].Label, new Vector3(i * RowSpacing, top + 2f, HumanRowZ));
            }

            for (var i = 0; i < stems.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + stems[i] + ".prefab");
                if (prefab == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(level.transform, true);
                var x = i * RowSpacing;
                instance.transform.position = new Vector3(x, 0f, FacelessRowZ);
                instance.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                NormalizeScale(instance, ReviewMaxDimension);
                var top = GetBoundsTop(instance);
                CreateLabel(level.transform, (i + 1).ToString(), new Vector3(x, top + 2f, FacelessRowZ));
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(level.transform, false);
            ground.transform.position = new Vector3(32f, -0.01f, 14f);
            ground.transform.localScale = new Vector3(10f, 1f, 6f);

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(level.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(level.transform, false);
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            camera.fieldOfView = 45f;
            camera.transform.position = new Vector3(24f, 12f, -30f);
            camera.transform.LookAt(new Vector3(24f, 1.5f, 12f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.11f, 0.15f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void NormalizeScale(GameObject instance, float maxDimension)
        {
            var bounds = GetWorldBounds(instance);
            var max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (max <= 0.001f)
            {
                return;
            }

            instance.transform.localScale = Vector3.one * (maxDimension / max);
        }

        static Bounds GetWorldBounds(GameObject instance)
        {
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        static float GetBoundsTop(GameObject instance)
        {
            return GetWorldBounds(instance).max.y;
        }

        static void CreateLabel(Transform parent, string text, Vector3 position)
        {
            var label = new GameObject("Label_" + text);
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = 0.4f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null && mesh.font != null)
            {
                renderer.sharedMaterial = mesh.font.material;
            }
        }

        internal static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var saved = AssetDatabase.LoadAssetAtPath<T>(path);
            if (saved == null)
            {
                throw new InvalidOperationException("Failed to save " + path);
            }

            return saved;
        }

        static void ConfigureAlbedo(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var dirty = false;
            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                dirty = true;
            }

            if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                dirty = true;
            }

            if (dirty)
            {
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            }
        }
    }
}
