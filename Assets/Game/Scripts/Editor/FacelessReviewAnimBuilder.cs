using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Data;
using Game.Gameplay.Dev;
using Game.Gameplay.Match;
using Game.UI.Controllers;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>Builds skinned Faceless review prefabs with Stand/Walk/Attack clips from MDX.</summary>
    public static class FacelessReviewAnimBuilder
    {
        const string SourceFolder = "Assets/FacelessRetexture_V2";
        const string PrefabFolder = "Assets/Game/Prefabs/Races/Faceless/_Review";
        const string AnimFolder = "Assets/Game/Art/Races/Faceless/Anim";
        const string MeshFolder = "Assets/Game/Art/Races/Faceless/Meshes";
        const string MatFolder = "Assets/Game/Art/Races/Faceless/Mats";
        const string ProductionFolder = "Assets/Game/Art/Races/Faceless/Production";
        const string ProductionAnimFolder = ProductionFolder + "/Anim";
        const string ProductionMeshFolder = ProductionFolder + "/Meshes";
        const string ProductionMatFolder = ProductionFolder + "/Mats";
        const string SkinTexPath = "Assets/Game/Art/Races/Faceless/FacelessOneUnbrokenV2.png";
        const string Wc3TexFolder = "Assets/Game/Art/Races/Faceless/Wc3";
        const string ScenePath = "Assets/Game/Scenes/Dev/FacelessReview.unity";
        const string UxmlPath = "Assets/Game/UI/Runtime/UXML/FacelessReview.uxml";
        const string PanelSettingsPath = "Assets/Game/Settings/UI/DefaultPanelSettings.asset";
        const int SampleStepMs = 33;
        const float TransparentAlphaThreshold = 0.45f;

        static readonly (string Stem, string FileName)[] Models =
        {
            ("01_FacelessOne", "FacelessOne_G.mdx"),
            ("02_FacelessOneBerserker", "FacelessOneBerserker_G.mdx"),
            ("03_RangedFacelessone", "RangedFacelessone_G.Mdx"),
            ("04_FacelessOneReaper", "FacelessOneReaper_G.mdx"),
            ("05_FacelessOneSorcerer_G1", "FacelessOneSorcerer_G1.mdx"),
            ("06_FacelessOneSorcerer_G2", "FacelessOneSorcerer_G2.mdx"),
            ("07_FacelessOneSorcerer_G3", "FacelessOneSorcerer_G3.mdx"),
            ("08_FacelessKing", "FacelessKing_G.mdx"),
            ("09_FacelessThanatos", "FacelessThanatos_G.mdx"),
            ("10_Unbroken_Izual", "Unbroken_Izual.mdx"),
            ("11_FacelessOneWorker", "FacelessOneWorker_G.mdx"),
            ("12_FacelessOneWorker_Portrait", "FacelessOneWorker_G_Portrait.mdx"),
        };

        /// <summary>Production role → MDX stem used to seed combat unit prefabs for the Faceless race.</summary>
        static readonly (string Role, string Stem, string PrefabName, string Folder)[] ProductionUnits =
        {
            ("Melee", "11_FacelessOneWorker", "Faceless_Melee", UnitVisualPrefabBuilder.FacelessPath + "/Melee"),
            ("Ranged", "03_RangedFacelessone", "Faceless_Ranged", UnitVisualPrefabBuilder.FacelessPath + "/Ranged"),
            ("Caster", "05_FacelessOneSorcerer_G1", "Faceless_Caster", UnitVisualPrefabBuilder.FacelessPath + "/Caster"),
            ("Siege", "01_FacelessOne", "Faceless_Siege", UnitVisualPrefabBuilder.FacelessPath + "/Siege"),
            ("Flying", "09_FacelessThanatos", "Faceless_Flying", UnitVisualPrefabBuilder.FacelessPath + "/Flying"),
            ("Super", "04_FacelessOneReaper", "Faceless_Super", UnitVisualPrefabBuilder.FacelessPath + "/Super"),
            (Hero1, "08_FacelessKing", "Faceless_Hero1", UnitVisualPrefabBuilder.FacelessHeroesPath + "/Hero1"),
            (Hero2, "07_FacelessOneSorcerer_G3", "Faceless_Hero2", UnitVisualPrefabBuilder.FacelessHeroesPath + "/Hero2"),
            (Hero3, "02_FacelessOneBerserker", "Faceless_Hero3", UnitVisualPrefabBuilder.FacelessHeroesPath + "/Hero3"),
            (Titan, "10_Unbroken_Izual", "Faceless_Titan", UnitVisualPrefabBuilder.FacelessHeroesPath + "/Titan"),
        };

        const string Hero1 = "Hero1";
        const string Hero2 = "Hero2";
        const string Hero3 = "Hero3";
        const string Titan = "Titan";

        [MenuItem("BARAKI/Faceless/Rebuild Review Anims")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static string RebuildAll()
        {
            ContentAssetPaths.EnsureFolder(PrefabFolder);
            ContentAssetPaths.EnsureFolder(AnimFolder);
            ContentAssetPaths.EnsureFolder(MeshFolder);
            ContentAssetPaths.EnsureFolder(MatFolder);
            DeleteAssetsInFolder(MatFolder, "t:Material");
            DeleteAssetsInFolder(MeshFolder, "t:Mesh");
            DeleteAssetsInFolder(AnimFolder, "t:AnimationClip");
            DeleteAssetsInFolder(AnimFolder, "t:AnimatorController");
            ConfigureAlbedo(SkinTexPath);
            if (Directory.Exists(Wc3TexFolder))
            {
                foreach (var texPath in Directory.GetFiles(Wc3TexFolder, "*.png"))
                {
                    ConfigureAlbedo(texPath.Replace('\\', '/'));
                }
            }

            RecolorForgottenOneTip();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var log = new List<string>();
            foreach (var (stem, fileName) in Models)
            {
                var src = Path.Combine(SourceFolder, fileName).Replace('\\', '/');
                var doc = FacelessMdxDocument.Load(src);
                var prefabPath = PrefabFolder + "/" + stem + ".prefab";
                BuildPrefab(stem, doc, prefabPath, log);
            }

            PopulateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FacelessReviewAnimBuilder:\n" + string.Join("\n", log));
            return string.Join("\n", log);
        }

        [MenuItem("BARAKI/Faceless/Rebuild Unit Prefabs")]
        public static string RebuildProductionPrefabs()
        {
            EnsureFolder(ProductionFolder);
            EnsureFolder(ProductionAnimFolder);
            EnsureFolder(ProductionMeshFolder);
            EnsureFolder(ProductionMatFolder);
            UnitVisualPrefabBuilder.EnsureFacelessPrefabFolders();
            DeleteAssetsInFolder(ProductionAnimFolder, "t:AnimatorController");

            ConfigureAlbedo(SkinTexPath);
            if (Directory.Exists(Wc3TexFolder))
            {
                foreach (var texPath in Directory.GetFiles(Wc3TexFolder, "*.png"))
                {
                    ConfigureAlbedo(texPath.Replace('\\', '/'));
                }
            }

            RecolorForgottenOneTip();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var log = new List<string>();
            foreach (var (role, stem, prefabName, folder) in ProductionUnits)
            {
                var src = Path.Combine(SourceFolder, FindModel(stem).FileName).Replace('\\', '/');
                var doc = FacelessMdxDocument.Load(src);
                var prefabPath = folder + "/" + prefabName + ".prefab";
                BuildProductionPrefab(stem, role, doc, prefabPath, log);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FacelessUnitPrefabBuilder:\n" + string.Join("\n", log));
            return string.Join("\n", log);
        }

        static (string Stem, string FileName) FindModel(string stem)
        {
            foreach (var model in Models)
            {
                if (model.Stem == stem)
                {
                    return model;
                }
            }

            throw new InvalidOperationException("Unknown Faceless stem: " + stem);
        }

        static void BuildProductionPrefab(
            string stem,
            string role,
            FacelessMdxDocument doc,
            string prefabPath,
            List<string> log)
        {
            var prefabName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            var root = new GameObject(prefabName);
            try
            {
                var bones = CreateSkeleton(root.transform, doc);
                var (mesh, materials) = BuildMesh(stem, doc, bones, ProductionMeshFolder, ProductionMatFolder);
                if (mesh.vertexCount == 0)
                {
                    log.Add($"{prefabName}: empty mesh");
                    return;
                }

                var smr = root.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.sharedMaterials = materials;
                smr.bones = bones;
                smr.rootBone = FindRootBone(bones);
                smr.updateWhenOffscreen = true;

                var stand = doc.FindSequence("stand", "portrait") ?? doc.FindSequence();
                var walk = doc.FindSequence("walk") ?? stand;
                var attack = doc.FindSequence("attack") ?? stand;
                var death = doc.FindSequence("death") ?? doc.FindSequence();
                var spell = doc.FindSequence("spell", "channeling");

                var standClip = BakeClip(stem, UnitCombatAnimatorDriver.StandState, doc, bones, stand, ProductionAnimFolder);
                var walkClip = BakeClip(stem, UnitCombatAnimatorDriver.WalkState, doc, bones, walk, ProductionAnimFolder);
                var attackClip = BakeClip(stem, UnitCombatAnimatorDriver.AttackState, doc, bones, attack, ProductionAnimFolder);
                var deathClip = BakeClip(stem, UnitCombatAnimatorDriver.DeathState, doc, bones, death, ProductionAnimFolder, loop: false);
                var castClip = spell.HasValue
                    ? BakeClip(stem, UnitCombatAnimatorDriver.CastState, doc, bones, spell, ProductionAnimFolder)
                    : null;

                var controller = BuildProductionController(
                    System.IO.Path.ChangeExtension(prefabPath, ".controller"),
                    standClip,
                    walkClip,
                    attackClip,
                    deathClip,
                    castClip);

                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                root.AddComponent<FacelessUnitTeamColor>();
                root.AddComponent<UnitCombatSettings>();

                smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ProductionMeshFolder + "/" + stem + ".asset");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.Add($"{prefabName} [{role}]: bones={doc.Nodes.Count} geos={doc.Geosets.Count} " +
                        $"stand={stand?.Name} walk={walk?.Name} attack={attack?.Name} " +
                        $"death={death?.Name} cast={spell?.Name ?? "(none)"}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static AnimatorController BuildProductionController(
            string controllerPath,
            AnimationClip stand,
            AnimationClip walk,
            AnimationClip attack,
            AnimationClip death,
            AnimationClip cast)
        {
            var path = controllerPath;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(UnitCombatAnimatorDriver.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(UnitCombatAnimatorDriver.AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitCombatAnimatorDriver.DeathParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitCombatAnimatorDriver.AttackVariantParam, AnimatorControllerParameterType.Float);
            if (cast != null)
            {
                controller.AddParameter(UnitCombatAnimatorDriver.CastVariantParam, AnimatorControllerParameterType.Float);
            }

            var sm = controller.layers[0].stateMachine;
            var standState = AddState(sm, UnitCombatAnimatorDriver.StandState, stand);
            var walkState = AddState(sm, UnitCombatAnimatorDriver.WalkState, walk);
            var attackState = AddState(sm, UnitCombatAnimatorDriver.AttackState, attack);
            var deathState = AddState(sm, UnitCombatAnimatorDriver.DeathState, death);
            sm.defaultState = standState;

            AddSpeedTransition(standState, walkState, AnimatorConditionMode.Greater, 0.1f);
            AddSpeedTransition(walkState, standState, AnimatorConditionMode.Less, 0.1f);
            AddTriggerTransition(standState, attackState, UnitCombatAnimatorDriver.AttackParam, UnitCombatAnimatorDriver.AttackState);
            AddTriggerTransition(walkState, attackState, UnitCombatAnimatorDriver.AttackParam, UnitCombatAnimatorDriver.AttackState);
            AddTriggerTransition(standState, deathState, UnitCombatAnimatorDriver.DeathParam, UnitCombatAnimatorDriver.DeathState);
            AddTriggerTransition(walkState, deathState, UnitCombatAnimatorDriver.DeathParam, UnitCombatAnimatorDriver.DeathState);
            AddTriggerTransition(attackState, deathState, UnitCombatAnimatorDriver.DeathParam, UnitCombatAnimatorDriver.DeathState);

            AnimatorState castState = null;
            if (cast != null)
            {
                castState = AddState(sm, UnitCombatAnimatorDriver.CastState, cast);
                AddTriggerTransition(castState, deathState, UnitCombatAnimatorDriver.DeathParam, UnitCombatAnimatorDriver.DeathState);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var state = sm.AddState(name);
            state.motion = clip;
            return state;
        }

        static void AddSpeedTransition(
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            float threshold)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = UnitCombatAnimatorDriver.LocomotionCrossFadeDuration;
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = mode,
                    parameter = UnitCombatAnimatorDriver.SpeedParam,
                    threshold = threshold,
                },
            };
        }

        static void AddTriggerTransition(
            AnimatorState from,
            AnimatorState to,
            string parameter,
            string destinationState)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = UnitCombatAnimatorDriver.ResolveCrossFadeDuration(destinationState);
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = parameter,
                    threshold = 0f,
                },
            };
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        static void BuildPrefab(
            string stem,
            FacelessMdxDocument doc,
            string prefabPath,
            List<string> log)
        {
            var root = new GameObject(stem);
            try
            {
                var bones = CreateSkeleton(root.transform, doc);
                var (mesh, materials) = BuildMesh(stem, doc, bones, MeshFolder, MatFolder);
                if (mesh.vertexCount == 0)
                {
                    log.Add($"{stem}: bones={doc.Nodes.Count} geos=0 (empty mesh)");
                    return;
                }

                var smr = root.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.sharedMaterials = materials;
                smr.bones = bones;
                smr.rootBone = FindRootBone(bones);
                smr.updateWhenOffscreen = true;

                var stand = doc.FindSequence("stand", "portrait") ?? doc.FindSequence();
                var walk = doc.FindSequence("walk") ?? stand;
                var attack = doc.FindSequence("attack") ?? stand;
                var standClip = BakeClip(stem, "Stand", doc, bones, stand, AnimFolder);
                var walkClip = BakeClip(stem, "Walk", doc, bones, walk, AnimFolder);
                var attackClip = BakeClip(stem, "Attack", doc, bones, attack, AnimFolder);
                var controller = BuildController(stem, standClip, walkClip, attackClip);

                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                root.AddComponent<FacelessReviewUnit>();

                var bounds = smr.bounds;
                var labelGo = new GameObject("Number");
                labelGo.transform.SetParent(root.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, bounds.size.y + 0.25f, 0f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = stem.Length >= 2 ? stem[..2] : stem;
                tm.fontSize = 64;
                tm.characterSize = 0.08f;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.yellow;
                tm.fontStyle = FontStyle.Bold;

                smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/" + stem + ".asset");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                var texList = new HashSet<string>();
                foreach (var g in doc.Geosets)
                {
                    texList.Add(g.TextureStem);
                }

                log.Add($"{stem}: bones={doc.Nodes.Count} geos={doc.Geosets.Count} tex={string.Join(",", texList)} " +
                        $"stand={stand?.Name} walk={walk?.Name} attack={attack?.Name}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static Transform[] CreateSkeleton(Transform root, FacelessMdxDocument doc)
        {
            var byId = new Dictionary<int, Transform>();
            var bones = new Transform[doc.Nodes.Count];
            var created = new bool[doc.Nodes.Count];
            Transform Create(int index)
            {
                if (created[index])
                {
                    return bones[index];
                }

                var node = doc.Nodes[index];
                var go = new GameObject(UniqueName(node.Name, node.ObjectId));
                var parentIndex = IndexOf(doc, node.ParentId);
                var parent = parentIndex >= 0 ? Create(parentIndex) : root;
                go.transform.SetParent(parent, false);
                var parentPivot = parentIndex >= 0 ? doc.Nodes[parentIndex].Pivot : Vector3.zero;
                go.transform.localPosition = node.Pivot - parentPivot;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                bones[index] = go.transform;
                byId[node.ObjectId] = go.transform;
                created[index] = true;
                return go.transform;
            }

            for (var i = 0; i < doc.Nodes.Count; i++)
            {
                Create(i);
            }

            return bones;
        }

        static Transform FindRootBone(Transform[] bones)
        {
            foreach (var bone in bones)
            {
                if (bone != null && bone.name.StartsWith("Bone_Root", StringComparison.OrdinalIgnoreCase))
                {
                    return bone;
                }
            }

            return bones.Length > 0 ? bones[0] : null;
        }

        static int IndexOf(FacelessMdxDocument doc, int objectId)
        {
            if (objectId < 0)
            {
                return -1;
            }

            for (var i = 0; i < doc.Nodes.Count; i++)
            {
                if (doc.Nodes[i].ObjectId == objectId)
                {
                    return i;
                }
            }

            return -1;
        }

        static string UniqueName(string name, int objectId)
        {
            var stem = string.IsNullOrEmpty(name) ? "Bone" : name;
            return stem + "_" + objectId;
        }

        static Texture2D LoadReadablePng(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        static float Wrapped(float x)
        {
            return x - Mathf.Floor(x);
        }

        static List<FacelessMdxDocument.Geoset> SplitV2TransparentTriangles(
            List<FacelessMdxDocument.Geoset> source,
            Texture2D skin)
        {
            var result = new List<FacelessMdxDocument.Geoset>();
            foreach (var g in source)
            {
                var isCandidate = g.TextureStem.Equals("FacelessOneUnbrokenV2", StringComparison.OrdinalIgnoreCase)
                                  && !g.HasTeamColor
                                  && !g.ClipBlack
                                  && !g.AlphaBlend;
                if (!isCandidate || g.Triangles.Length < 3 || g.Uvs == null || g.Uvs.Length == 0)
                {
                    result.Add(g);
                    continue;
                }

                var opaqueTris = new List<int>();
                var clipTris = new List<int>();
                var w = skin.width;
                var h = skin.height;
                for (var t = 0; t + 2 < g.Triangles.Length; t += 3)
                {
                    var iA = g.Triangles[t];
                    var iB = g.Triangles[t + 1];
                    var iC = g.Triangles[t + 2];
                    var u = (g.Uvs[iA].x + g.Uvs[iB].x + g.Uvs[iC].x) / 3f;
                    var v = (g.Uvs[iA].y + g.Uvs[iB].y + g.Uvs[iC].y) / 3f;
                    var px = Mathf.Clamp((int)(Wrapped(u) * w), 0, w - 1);
                    var py = Mathf.Clamp((int)(Wrapped(v) * h), 0, h - 1);
                    if (skin.GetPixel(px, py).a < TransparentAlphaThreshold)
                    {
                        clipTris.Add(iA);
                        clipTris.Add(iB);
                        clipTris.Add(iC);
                    }
                    else
                    {
                        opaqueTris.Add(iA);
                        opaqueTris.Add(iB);
                        opaqueTris.Add(iC);
                    }
                }

                if (clipTris.Count == 0)
                {
                    result.Add(g);
                    continue;
                }

                if (opaqueTris.Count > 0)
                {
                    result.Add(new FacelessMdxDocument.Geoset
                    {
                        Vertices = g.Vertices,
                        Normals = g.Normals,
                        Uvs = g.Uvs,
                        Weights = g.Weights,
                        Triangles = opaqueTris.ToArray(),
                        TextureStem = g.TextureStem,
                        TwoSided = g.TwoSided,
                        HasTeamColor = false,
                        ClipBlack = false,
                        AlphaBlend = false,
                    });
                }

                result.Add(new FacelessMdxDocument.Geoset
                {
                    Vertices = g.Vertices,
                    Normals = g.Normals,
                    Uvs = g.Uvs,
                    Weights = g.Weights,
                    Triangles = clipTris.ToArray(),
                    TextureStem = g.TextureStem,
                    TwoSided = true,
                    HasTeamColor = false,
                    ClipBlack = true,
                    AlphaBlend = false,
                });
            }

            return result;
        }

        static void RecolorForgottenOneTip()
        {
            var path = Wc3TexFolder + "/ForgottenOne.png";
            var tex = LoadReadablePng(path);
            if (tex == null)
            {
                return;
            }

            try
            {
                var w = tex.width;
                var h = tex.height;
                var pixels = tex.GetPixels();
                var changed = false;
                for (var y = 0; y < (int)(0.29f * h); y++)
                {
                    for (var x = (int)(0.74f * w); x < w; x++)
                    {
                        var color = pixels[y * w + x];
                        var luma = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
                        var grey = new Color(luma, luma, luma, color.a);
                        if (Mathf.Abs(color.r - grey.r) > 0.001f
                            || Mathf.Abs(color.g - grey.g) > 0.001f
                            || Mathf.Abs(color.b - grey.b) > 0.001f)
                        {
                            pixels[y * w + x] = grey;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                {
                    return;
                }

                tex.SetPixels(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Faceless: ForgottenOne tip recolored to steel grey (idempotent).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [MenuItem("BARAKI/Faceless/Recolor ForgottenOne Tip")]
        public static void RecolorForgottenOneTipFromMenu()
        {
            RecolorForgottenOneTip();
        }

        static (Mesh mesh, Material[] materials) BuildMesh(
            string stem,
            FacelessMdxDocument doc,
            Transform[] bones,
            string meshFolder,
            string matFolder)
        {
            var groups = new List<(string Stem, List<FacelessMdxDocument.Geoset> Geosets, bool TwoSided, bool HasTeamColor, bool ClipBlack, bool AlphaBlend)>();
            var index = new Dictionary<string, int>();
            var splitSkin = LoadReadablePng(SkinTexPath);
            var geosets = splitSkin != null
                ? SplitV2TransparentTriangles(doc.Geosets, splitSkin)
                : new List<FacelessMdxDocument.Geoset>(doc.Geosets);

            if (splitSkin != null)
            {
                UnityEngine.Object.DestroyImmediate(splitSkin);
            }

            for (var gIdx = 0; gIdx < geosets.Count; gIdx++)
            {
                var g = geosets[gIdx];
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
            var weights = new List<BoneWeight>();
            var subTris = new List<int>[groups.Count];
            var materials = new Material[groups.Count];
            for (var s = 0; s < groups.Count; s++)
            {
                var (texStem, list, twoSided, hasTeamColor, clipBlack, alphaBlend) = groups[s];
                materials[s] = EnsureUnlitMaterial(texStem, twoSided, hasTeamColor, clipBlack, alphaBlend, matFolder);
                var tris = new List<int>();
                foreach (var g in list)
                {
                    var offset = verts.Count;
                    verts.AddRange(g.Vertices);
                    norms.AddRange(g.Normals);
                    uvs.AddRange(g.Uvs);
                    weights.AddRange(g.Weights);
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

            mesh.boneWeights = weights.ToArray();
            var bind = new Matrix4x4[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                bind[i] = bones[i].worldToLocalMatrix * bones[i].root.localToWorldMatrix;
            }

            mesh.bindposes = bind;
            mesh.RecalculateBounds();
            var meshPath = meshFolder + "/" + stem + ".asset";
            return (SaveAsset(mesh, meshPath), materials);
        }

        static Material EnsureUnlitMaterial(
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

            ApplyReviewMaterial(mat, textureStem, twoSided, hasTeamColor, clipBlack, alphaBlend);
            return mat;
        }

        static void ApplyReviewMaterial(
            Material mat,
            string textureStem,
            bool twoSided,
            bool hasTeamColor,
            bool clipBlack,
            bool alphaBlend)
        {
            var tex = LoadTexture(textureStem);
            if (tex == null && textureStem != "TeamColor")
            {
                Debug.LogError("Faceless review texture missing: " + textureStem);
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_TeamColor", MatchPlayerColors.GetSlotColor(1));
            mat.SetFloat("_UseTeam", hasTeamColor && textureStem != "TeamColor" ? 1f : 0f);
            mat.SetFloat("_UseBlend", alphaBlend ? 1f : 0f);
            mat.SetFloat("_AlphaClip", clipBlack ? 0.12f : 0f);
            mat.SetFloat(
                "_AtlasClip",
                textureStem.Equals("HeroAvatarFlame", StringComparison.OrdinalIgnoreCase) ? 0.16f : 0f);
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
        }

        static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
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

        static void DeleteAssetsInFolder(string folder, string filter)
        {
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
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

        static Texture2D LoadTexture(string textureStem)
        {
            if (textureStem == "TeamColor")
            {
                return Texture2D.whiteTexture;
            }

            if (textureStem.Equals("FacelessOneUnbrokenV2", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(SkinTexPath);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(Wc3TexFolder + "/" + textureStem + ".png");
        }

        static AnimationClip BakeClip(
            string stem,
            string clipName,
            FacelessMdxDocument doc,
            Transform[] bones,
            FacelessMdxDocument.Sequence? sequence,
            string animFolder,
            bool loop = true)
        {
            var clip = new AnimationClip
            {
                name = stem + "_" + clipName,
                frameRate = 30f,
                legacy = false,
            };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var start = sequence?.StartMs ?? 0;
            var end = sequence?.EndMs ?? start + 1000;
            if (end <= start)
            {
                end = start + 1000;
            }

            for (var i = 0; i < doc.Nodes.Count; i++)
            {
                var node = doc.Nodes[i];
                var path = AnimationUtility.CalculateTransformPath(bones[i], bones[i].root);
                var parentIndex = IndexOf(doc, node.ParentId);
                var restPos = node.Pivot - (parentIndex >= 0 ? doc.Nodes[parentIndex].Pivot : Vector3.zero);
                BakeVectorCurves(clip, path, "m_LocalPosition", node.Translation, start, end, restPos, addRest: true);
                BakeQuatCurves(clip, path, node.Rotation, start, end);
                if (node.Scale.Count > 0)
                {
                    BakeVectorCurves(clip, path, "m_LocalScale", node.Scale, start, end, Vector3.one, addRest: false);
                }
            }

            var clipPath = animFolder + "/" + stem + "_" + clipName + ".anim";
            return SaveAsset(clip, clipPath);
        }

        static void BakeVectorCurves(
            AnimationClip clip,
            string path,
            string prefix,
            List<FacelessMdxDocument.Vec3Key> keys,
            int start,
            int end,
            Vector3 fallback,
            bool addRest)
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            for (var t = start; t <= end; t += SampleStepMs)
            {
                var sampled = FacelessMdxDocument.SampleVec3(keys, t, addRest ? Vector3.zero : fallback);
                var v = addRest ? fallback + sampled : sampled;
                var time = (t - start) / 1000f;
                cx.AddKey(time, v.x);
                cy.AddKey(time, v.y);
                cz.AddKey(time, v.z);
            }

            FlattenTangents(cx);
            FlattenTangents(cy);
            FlattenTangents(cz);
            clip.SetCurve(path, typeof(Transform), prefix + ".x", cx);
            clip.SetCurve(path, typeof(Transform), prefix + ".y", cy);
            clip.SetCurve(path, typeof(Transform), prefix + ".z", cz);
        }

        static void BakeQuatCurves(
            AnimationClip clip,
            string path,
            List<FacelessMdxDocument.QuatKey> keys,
            int start,
            int end)
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            var cw = new AnimationCurve();
            var prev = Quaternion.identity;
            var havePrev = false;
            for (var t = start; t <= end; t += SampleStepMs)
            {
                var q = FacelessMdxDocument.SampleQuat(keys, t);
                if (havePrev && Quaternion.Dot(prev, q) < 0f)
                {
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }

                prev = q;
                havePrev = true;
                var time = (t - start) / 1000f;
                cx.AddKey(time, q.x);
                cy.AddKey(time, q.y);
                cz.AddKey(time, q.z);
                cw.AddKey(time, q.w);
            }

            FlattenTangents(cx);
            FlattenTangents(cy);
            FlattenTangents(cz);
            FlattenTangents(cw);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", cx);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", cy);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", cz);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", cw);
        }

        static void FlattenTangents(AnimationCurve curve)
        {
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
        }

        static AnimatorController BuildController(
            string stem,
            AnimationClip stand,
            AnimationClip walk,
            AnimationClip attack)
        {
            var path = AnimFolder + "/" + stem + ".controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            var standState = sm.AddState(UnitCombatAnimatorDriver.StandState);
            standState.motion = stand;
            var walkState = sm.AddState(UnitCombatAnimatorDriver.WalkState);
            walkState.motion = walk;
            var attackState = sm.AddState(UnitCombatAnimatorDriver.AttackState);
            attackState.motion = attack;
            sm.defaultState = standState;
            return controller;
        }

        static void PopulateScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            DestroyReviewUnits(scene);

            var systems = EnsureGroup(scene, "--- SYSTEMS ---");
            var level = EnsureGroup(scene, "--- LEVEL ---");
            var ui = EnsureGroup(scene, "--- UI ---");
            EnsureGroup(scene, "--- CAMERAS ---");
            EnsureGroup(scene, "--- DYNAMIC ---");

            var playback = UnityEngine.Object.FindAnyObjectByType<FacelessReviewPlayback>();
            if (playback == null)
            {
                var go = new GameObject("FacelessReviewPlayback");
                playback = go.AddComponent<FacelessReviewPlayback>();
            }

            playback.transform.SetParent(systems, true);
            EnsureReviewHud(ui);

            const float spacing = 5.5f;
            for (var i = 0; i < Models.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + Models[i].Stem + ".prefab");
                if (prefab == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(level, true);
                instance.transform.position = new Vector3(i * spacing, 0f, 0f);
            }

            if (Camera.main != null)
            {
                var mid = (Models.Length - 1) * spacing * 0.5f;
                Camera.main.fieldOfView = 40f;
                Camera.main.transform.position = new Vector3(mid, 6f, -48f);
                Camera.main.transform.LookAt(new Vector3(mid, 1.2f, 0f));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void DestroyReviewUnits(Scene scene)
        {
            var doomed = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                CollectReview(root.transform, doomed);
            }

            for (var i = 0; i < doomed.Count; i++)
            {
                if (doomed[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(doomed[i]);
                }
            }
        }

        static void CollectReview(Transform t, List<GameObject> doomed)
        {
            if (t.GetComponent<FacelessReviewUnit>() != null || IsReviewModelName(t.name))
            {
                doomed.Add(t.gameObject);
                return;
            }

            foreach (Transform child in t)
            {
                CollectReview(child, doomed);
            }
        }

        static bool IsReviewModelName(string name)
        {
            return name.StartsWith("01_") ||
                   name.StartsWith("02_") ||
                   name.StartsWith("03_") ||
                   name.StartsWith("04_") ||
                   name.StartsWith("05_") ||
                   name.StartsWith("06_") ||
                   name.StartsWith("07_") ||
                   name.StartsWith("08_") ||
                   name.StartsWith("09_") ||
                   name.StartsWith("10_") ||
                   name.StartsWith("11_") ||
                   name.StartsWith("12_");
        }

        static Transform EnsureGroup(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }
            }

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        static void EnsureReviewHud(Transform uiParent)
        {
            var existing = uiParent.Find("FacelessReviewHud");
            var go = existing != null ? existing.gameObject : new GameObject("FacelessReviewHud");
            if (existing == null)
            {
                go.transform.SetParent(uiParent, false);
            }

            var uiDocument = go.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = go.AddComponent<UIDocument>();
            }

            uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            uiDocument.sortingOrder = 100;

            var controller = go.GetComponent<FacelessReviewHudController>();
            if (controller == null)
            {
                controller = go.AddComponent<FacelessReviewHudController>();
            }

            var so = new SerializedObject(controller);
            so.FindProperty("_uiDocument").objectReferenceValue = uiDocument;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
