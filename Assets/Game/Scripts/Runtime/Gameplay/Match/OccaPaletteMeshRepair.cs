using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Recolors OccaSoftware trees independently from the sparse source palette:
    /// the connected trunk/branch component is brown and foliage uses a safe green gradient.
    /// </summary>
    public static class OccaPaletteMeshRepair
    {
        const int PaletteWidth = 32;
        const float BrownUv = 0.125f;
        const float DarkGreenUv = 0.375f;
        const float LightGreenUv = 0.94f;
        const float GroundTolerance = 0.08f;
        const float BaseRadiusFraction = 0.3f;

        static readonly Color Brown = new(0.38f, 0.24f, 0.14f);
        static readonly Color DarkGreen = new(0.08f, 0.28f, 0.12f);
        static readonly Color MidGreen = new(0.12f, 0.48f, 0.16f);
        static readonly Color LightGreen = new(0.28f, 0.68f, 0.2f);

        static Material s_treeMaterial;
        static Texture2D s_treePalette;

        public static bool ShouldRepair(EnvironmentPropKind kind) =>
            kind is EnvironmentPropKind.Tree or EnvironmentPropKind.Pine;

        public static void RepairInstance(
            GameObject instance,
            Texture ignoredSourcePalette,
            EnvironmentPropKind kind = EnvironmentPropKind.Pine)
        {
            if (instance == null)
            {
                return;
            }

            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                filter.sharedMesh = RepairMesh(filter.sharedMesh);
            }
        }

        public static Material GetOrCreateTreeMaterial(Material template)
        {
            if (template == null)
            {
                return null;
            }

            if (s_treeMaterial != null)
            {
                return s_treeMaterial;
            }

            s_treePalette = BuildTreePalette();
            s_treeMaterial = new Material(template)
            {
                name = "OccaTreeGradient",
                hideFlags = HideFlags.HideAndDontSave,
            };
            s_treeMaterial.SetTexture("_BaseMap", s_treePalette);
            s_treeMaterial.SetTexture("_MainTex", s_treePalette);
            s_treeMaterial.SetColor("_BaseColor", Color.white);
            s_treeMaterial.SetColor("_Color", Color.white);
            s_treeMaterial.SetFloat("_Smoothness", 0f);
            s_treeMaterial.SetFloat("_Glossiness", 0f);
            s_treeMaterial.SetFloat("_SpecularHighlights", 0f);
            s_treeMaterial.SetFloat("_EnvironmentReflections", 0f);
            return s_treeMaterial;
        }

        public static Mesh RepairMesh(Mesh source)
        {
            if (source == null)
            {
                return source;
            }

            if (!source.isReadable)
            {
                Debug.LogWarning(
                    $"[OccaPaletteMeshRepair] Mesh '{source.name}' is not readable. " +
                    "Enable Read/Write in model import settings.");
                return source;
            }

            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var sourceTriangles = source.triangles;
            if (sourceVertices.Length == 0 || sourceTriangles.Length < 3)
            {
                return source;
            }

            var woodTriangles = FindWoodTriangles(sourceVertices, sourceTriangles, source.bounds);
            var vertices = new List<Vector3>(sourceTriangles.Length);
            var normals = new List<Vector3>(sourceTriangles.Length);
            var uvs = new List<Vector2>(sourceTriangles.Length);
            var triangles = new int[sourceTriangles.Length];
            var hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
            var minY = source.bounds.min.y;
            var height = Mathf.Max(source.bounds.size.y, 0.001f);

            for (var triangle = 0; triangle < sourceTriangles.Length / 3; triangle++)
            {
                var sourceIndex = triangle * 3;
                var baseIndex = vertices.Count;
                for (var corner = 0; corner < 3; corner++)
                {
                    var vertexIndex = sourceTriangles[sourceIndex + corner];
                    var vertex = sourceVertices[vertexIndex];
                    vertices.Add(vertex);
                    if (hasNormals)
                    {
                        normals.Add(sourceNormals[vertexIndex]);
                    }

                    var uvX = woodTriangles[triangle]
                        ? BrownUv
                        : Mathf.Lerp(
                            DarkGreenUv,
                            LightGreenUv,
                            Mathf.Clamp01((vertex.y - minY) / height));
                    uvs.Add(new Vector2(uvX, 0.5f));
                    triangles[sourceIndex + corner] = baseIndex + corner;
                }
            }

            var mesh = new Mesh
            {
                name = source.name + "_OccaRepaired",
                indexFormat = sourceTriangles.Length > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            if (hasNormals)
            {
                mesh.SetNormals(normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        public static bool[] FindWoodTriangles(
            Vector3[] vertices,
            int[] triangles,
            Bounds bounds)
        {
            var triangleCount = triangles.Length / 3;
            var parent = new int[triangleCount];
            var byPosition = new Dictionary<Vector3, int>(vertices.Length);
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                parent[triangle] = triangle;
                for (var corner = 0; corner < 3; corner++)
                {
                    var vertex = vertices[triangles[triangle * 3 + corner]];
                    if (byPosition.TryGetValue(vertex, out var otherTriangle))
                    {
                        Union(parent, triangle, otherTriangle);
                    }
                    else
                    {
                        byPosition.Add(vertex, triangle);
                    }
                }
            }

            var componentTouchesBase = new Dictionary<int, bool>();
            var baseY = bounds.min.y + bounds.size.y * GroundTolerance;
            var baseRadius = Mathf.Max(bounds.size.x, bounds.size.z) * BaseRadiusFraction;
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                var root = Find(parent, triangle);
                if (componentTouchesBase.TryGetValue(root, out var touches) && touches)
                {
                    continue;
                }

                for (var corner = 0; corner < 3; corner++)
                {
                    var vertex = vertices[triangles[triangle * 3 + corner]];
                    if (vertex.y <= baseY
                        && new Vector2(vertex.x, vertex.z).magnitude <= baseRadius)
                    {
                        componentTouchesBase[root] = true;
                        break;
                    }
                }
            }

            var result = new bool[triangleCount];
            for (var triangle = 0; triangle < triangleCount; triangle++)
            {
                result[triangle] = componentTouchesBase.TryGetValue(
                    Find(parent, triangle),
                    out var isWood) && isWood;
            }

            return result;
        }

        static Texture2D BuildTreePalette()
        {
            var texture = new Texture2D(PaletteWidth, 1, TextureFormat.RGBA32, false, false)
            {
                name = "OccaTreeGradient",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var colors = new Color[PaletteWidth];
            for (var x = 0; x < PaletteWidth; x++)
            {
                if (x < 8)
                {
                    colors[x] = Brown;
                    continue;
                }

                var t = (x - 8f) / (PaletteWidth - 9f);
                colors[x] = t < 0.55f
                    ? Color.Lerp(DarkGreen, MidGreen, t / 0.55f)
                    : Color.Lerp(MidGreen, LightGreen, (t - 0.55f) / 0.45f);
            }

            texture.SetPixels(colors);
            texture.Apply(false, false);
            return texture;
        }

        static int Find(int[] parent, int value)
        {
            while (parent[value] != value)
            {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }

            return value;
        }

        static void Union(int[] parent, int a, int b)
        {
            var rootA = Find(parent, a);
            var rootB = Find(parent, b);
            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }
    }
}
