using System;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Prefab pools for procedural arena nature décor.</summary>
    public sealed class MatchArenaEnvironmentPrefabSet
    {
        public GameObject[] Trees { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Pines { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Rocks { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Cliffs { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Flowers { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Mountains { get; set; } = Array.Empty<GameObject>();
        public GameObject[] PathPieces { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Boats { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Bridges { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Lanterns { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Crates { get; set; } = Array.Empty<GameObject>();
        public GameObject[] Benches { get; set; } = Array.Empty<GameObject>();

        public GameObject Pick(EnvironmentPropKind kind, System.Random rng)
        {
            var pool = kind switch
            {
                EnvironmentPropKind.Tree => Trees,
                EnvironmentPropKind.Pine => Pines,
                EnvironmentPropKind.Rock => Rocks,
                EnvironmentPropKind.Cliff => Cliffs,
                EnvironmentPropKind.Flower => Flowers,
                EnvironmentPropKind.Mountain => Mountains,
                EnvironmentPropKind.PathPiece => PathPieces,
                EnvironmentPropKind.Boat => Boats,
                EnvironmentPropKind.Bridge => Bridges,
                EnvironmentPropKind.Lantern => Lanterns,
                EnvironmentPropKind.Crate => Crates,
                EnvironmentPropKind.Bench => Benches,
                _ => null,
            };

            if (pool == null || pool.Length == 0)
            {
                return null;
            }

            return pool[rng.Next(0, pool.Length)];
        }
    }

    /// <summary>Spawns OccaSoftware nature props and road dressing.</summary>
    public static class MatchArenaEnvironmentDecorator
    {
        public const string CatalogResourcePath = "Environment/MatchArenaEnvironmentCatalog";
        public const string OccaColorResourcePath = "Art/OccaColor";
        public const string WaterMaterialResourcePath = "Art/EnvironmentWater";

        public static void Populate(
            Transform parent,
            MatchArenaLayout layout,
            WalkableSurface walkable,
            MatchArenaEnvironmentPrefabSet prefabs)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            prefabs ??= new MatchArenaEnvironmentPrefabSet();

            ClearDecor(parent);

            var root = new GameObject(MatchArenaEnvironmentRules.DecorRootName).transform;
            root.SetParent(parent, false);

            var placements = MatchArenaEnvironmentRules.BuildPlacements(layout, walkable);
            var rng = new System.Random(MatchArenaEnvironmentRules.SeedForPlayerCount(layout.PlayerCount));
            var colorMaterial = Resources.Load<Material>(OccaColorResourcePath);
            var waterMaterial = Resources.Load<Material>(WaterMaterialResourcePath);

            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement.Kind == EnvironmentPropKind.River)
                {
                    SpawnRiverSegment(root, placement, i, waterMaterial);
                    continue;
                }

                var prefab = prefabs.Pick(placement.Kind, rng);
                if (prefab == null)
                {
                    continue;
                }

                var instance = UnityEngine.Object.Instantiate(prefab, root);
                instance.name = $"{placement.Kind}_{i}";
                instance.transform.SetPositionAndRotation(
                    placement.Position,
                    Quaternion.Euler(0f, placement.YawDegrees, 0f));
                if (placement.LocalScale != Vector3.one)
                {
                    instance.transform.localScale = placement.LocalScale;
                }

                var instanceMaterial = colorMaterial;
                if (OccaPaletteMeshRepair.ShouldRepair(placement.Kind) && colorMaterial != null)
                {
                    var palette = colorMaterial.GetTexture("_BaseMap") ?? colorMaterial.mainTexture;
                    OccaPaletteMeshRepair.RepairInstance(instance, palette, placement.Kind);
                    instanceMaterial = OccaPaletteMeshRepair.GetOrCreateTreeMaterial(colorMaterial);
                }

                EnsureOccaColorMaterial(instance, instanceMaterial);
                DisableColliders(instance);
            }
        }

        public static MatchArenaEnvironmentPrefabSet LoadPrefabSetOrEmpty()
        {
            var catalog = Resources.Load<MatchArenaEnvironmentCatalog>(CatalogResourcePath);
            return catalog != null ? catalog.ToPrefabSet() : new MatchArenaEnvironmentPrefabSet();
        }

        /// <summary>Edit Mode / unit-test helper: primitive stand-ins for each prop kind.</summary>
        public static MatchArenaEnvironmentPrefabSet CreateTestPrefabSet()
        {
            return new MatchArenaEnvironmentPrefabSet
            {
                Trees = new[] { CreatePrimitivePrefab("TestTree", PrimitiveType.Cylinder) },
                Pines = new[] { CreatePrimitivePrefab("TestPine", PrimitiveType.Cylinder) },
                Rocks = new[] { CreatePrimitivePrefab("TestRock", PrimitiveType.Sphere) },
                Cliffs = new[] { CreatePrimitivePrefab("TestCliff", PrimitiveType.Cube) },
                Flowers = new[] { CreatePrimitivePrefab("TestFlower", PrimitiveType.Capsule) },
                Mountains = new[] { CreatePrimitivePrefab("TestMountain", PrimitiveType.Cube) },
                PathPieces = new[] { CreatePrimitivePrefab("TestPath", PrimitiveType.Cube) },
                Boats = new[] { CreatePrimitivePrefab("TestBoat", PrimitiveType.Cube) },
                Bridges = new[] { CreatePrimitivePrefab("TestBridge", PrimitiveType.Cube) },
                Lanterns = new[] { CreatePrimitivePrefab("TestLantern", PrimitiveType.Cylinder) },
                Crates = new[] { CreatePrimitivePrefab("TestCrate", PrimitiveType.Cube) },
                Benches = new[] { CreatePrimitivePrefab("TestBench", PrimitiveType.Cube) },
            };
        }

        /// <summary>Destroys objects created by <see cref="CreateTestPrefabSet"/>.</summary>
        public static void DestroyTestPrefabSet(MatchArenaEnvironmentPrefabSet prefabs)
        {
            if (prefabs == null)
            {
                return;
            }

            DestroyTestPrefabPool(prefabs.Trees);
            DestroyTestPrefabPool(prefabs.Pines);
            DestroyTestPrefabPool(prefabs.Rocks);
            DestroyTestPrefabPool(prefabs.Cliffs);
            DestroyTestPrefabPool(prefabs.Flowers);
            DestroyTestPrefabPool(prefabs.Mountains);
            DestroyTestPrefabPool(prefabs.PathPieces);
            DestroyTestPrefabPool(prefabs.Boats);
            DestroyTestPrefabPool(prefabs.Bridges);
            DestroyTestPrefabPool(prefabs.Lanterns);
            DestroyTestPrefabPool(prefabs.Crates);
            DestroyTestPrefabPool(prefabs.Benches);
        }

        public static void ClearDecor(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            // Destroy is deferred in Play Mode; unparent so Find/Populate cannot hit a dying root.
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name != MatchArenaEnvironmentRules.DecorRootName)
                {
                    continue;
                }

                child.SetParent(null, false);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        public static void EnsureOccaColorMaterial(GameObject instance, Material colorMaterial)
        {
            if (instance == null || colorMaterial == null)
            {
                return;
            }

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0)
                {
                    renderer.sharedMaterial = colorMaterial;
                    continue;
                }

                var changed = false;
                for (var m = 0; m < shared.Length; m++)
                {
                    var material = shared[m];
                    if (ShouldPreserveEnvironmentMaterial(material))
                    {
                        continue;
                    }

                    shared[m] = colorMaterial;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = shared;
                }
            }
        }

        internal static bool ShouldPreserveEnvironmentMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            return string.Equals(material.name, "Light", StringComparison.OrdinalIgnoreCase)
                   || material.name.StartsWith("OccaFlower", StringComparison.Ordinal)
                   || material.IsKeywordEnabled("_EMISSION");
        }

        static void SpawnRiverSegment(
            Transform root,
            EnvironmentPropPlacement placement,
            int index,
            Material waterMaterial)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"River_{index}";
            segment.transform.SetParent(root, false);
            segment.transform.SetPositionAndRotation(
                placement.Position,
                Quaternion.Euler(0f, placement.YawDegrees, 0f));
            segment.transform.localScale = placement.LocalScale;

            DisableColliders(segment);

            var renderer = segment.GetComponent<Renderer>();
            if (renderer != null && waterMaterial != null)
            {
                renderer.sharedMaterial = waterMaterial;
            }
        }

        static GameObject CreatePrimitivePrefab(string name, PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = HideFlags.HideAndDontSave;
            go.SetActive(false);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return go;
        }

        static void DestroyTestPrefabPool(GameObject[] pool)
        {
            if (pool == null)
            {
                return;
            }

            for (var i = 0; i < pool.Length; i++)
            {
                var prefab = pool[i];
                if (prefab == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(prefab);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }
            }
        }

        static void DisableColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }
}
