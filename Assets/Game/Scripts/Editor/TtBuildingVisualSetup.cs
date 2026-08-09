using System;
using System.IO;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds the three Human building prefabs from ToonyTinyPeople TT_RTS building FBX models:
    /// copies the matched model, lifts it so its base sits on the ground plane, keeps the native
    /// FBX scale, and attaches <see cref="TtUnitTeamColor"/> with the four slot-color texture
    /// variants (buildings share the same atlas-recolor mechanic as units).
    /// Run via menu BARAKI/Buildings/Rebuild TT Prefabs.
    /// </summary>
    public static class TtBuildingVisualSetup
    {
        const string TtModelFolder = "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/buildings";
        const string TtTextureFolder =
            "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/materials/color/Buildings/textures";

        public const string BuildingsFolder = "Assets/Game/Prefabs/Races/Humans/Buildings";
        public const string TownHallPath = BuildingsFolder + "/Human_TownHall.prefab";
        public const string TowerPath = BuildingsFolder + "/Human_Tower.prefab";
        public const string BarracksPath = BuildingsFolder + "/Human_Barracks.prefab";

        static readonly string[] TeamTextureFiles =
        {
            "TT_RTS_Buildings_red.tga",
            "TT_RTS_Buildings_blue.tga",
            "TT_RTS_Buildings_green.tga",
            "TT_RTS_Buildings_yellow.tga",
        };

        readonly struct BuildingSetup
        {
            public readonly string DestinationPath;
            public readonly string ModelPath;

            public BuildingSetup(string destinationPath, string modelPath)
            {
                DestinationPath = destinationPath;
                ModelPath = modelPath;
            }
        }

        static readonly BuildingSetup[] BuildingSetups =
        {
            new(TownHallPath, TtModelFolder + "/TownHall.FBX"),
            new(TowerPath, TtModelFolder + "/Tower_A.FBX"),
            new(BarracksPath, TtModelFolder + "/Barracks.FBX"),
        };

        [MenuItem("BARAKI/Buildings/Rebuild TT Prefabs")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static void RebuildAll()
        {
            EnsureFolder(BuildingsFolder);

            foreach (var setup in BuildingSetups)
            {
                BuildPrefab(setup);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildingVisualPrefabBuilder.EnsureContent();
            Debug.Log("TtBuildingVisualSetup: rebuilt " + BuildingSetups.Length + " Human building prefabs.");
        }

        static void BuildPrefab(BuildingSetup setup)
        {
            var modelRoot = LoadModelRoot(setup.ModelPath);

            var root = new GameObject(Path.GetFileNameWithoutExtension(setup.DestinationPath));
            try
            {
                var model = UnityEngine.Object.Instantiate(modelRoot, root.transform);
                model.name = "Model";

                var lift = ComputeBaseLift(model);
                model.transform.localPosition = new Vector3(0f, lift, 0f);
                model.transform.localScale = Vector3.one;

                root.transform.localScale = Vector3.one;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localPosition = Vector3.zero;

                StripRigidAndColliders(root);

                var ttColor = root.GetComponent<TtUnitTeamColor>();
                if (ttColor == null)
                {
                    ttColor = root.AddComponent<TtUnitTeamColor>();
                }

                ttColor.TeamTextures = LoadTeamTextures();

                PrefabUtility.SaveAsPrefabAsset(root, setup.DestinationPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static GameObject LoadModelRoot(string modelPath)
        {
            var model = AssetDatabase.LoadMainAssetAtPath(modelPath) as GameObject;
            if (model == null)
            {
                throw new InvalidOperationException("Missing building model at " + modelPath);
            }

            return model;
        }

        /// <summary>Lifts the model so its renderer bounds rest on the ground plane (bounds.min.y == 0).</summary>
        static float ComputeBaseLift(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return 0f;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return -bounds.min.y;
        }

        static void StripRigidAndColliders(GameObject root)
        {
            var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(rigidbodies[i]);
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(colliders[i]);
            }
        }

        static Texture2D[] LoadTeamTextures()
        {
            var textures = new Texture2D[TeamTextureFiles.Length];
            for (var i = 0; i < TeamTextureFiles.Length; i++)
            {
                var path = TtTextureFolder + "/" + TeamTextureFiles[i];
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (textures[i] == null)
                {
                    throw new InvalidOperationException("Missing building team texture " + path);
                }
            }

            return textures;
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
