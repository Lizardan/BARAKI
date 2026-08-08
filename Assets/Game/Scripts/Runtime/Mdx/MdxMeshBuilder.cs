using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Mdx
{
    /// <summary>
    /// A node in the combined generic-object list. The order of the list is the node
    /// order of the game/reference viewer: bones, lights, helpers, attachments,
    /// particle emitters, particle emitters 2, popcorn emitters, ribbon emitters,
    /// event objects, collision shapes. Geoset matrix indices (MATS) index straight
    /// into this list, so pivots, parenting and bone textures all use <see cref="ListIndex"/>.
    /// </summary>
    public sealed class MdxMeshNode
    {
        public string Name = "";
        public int ObjectId;
        public int ParentId = -1;
        public Vector3 Pivot;
        public int ListIndex;
    }

    /// <summary>Skinning variant of a rendered geoset (mirrors the reference SkinningType).</summary>
    public enum MdxSkinningType
    {
        /// <summary>SD vertex groups, up to 4 bones per vertex. Uniform weight 1/boneCount.</summary>
        VertexGroups = 0,
        /// <summary>SD vertex groups, up to 8 bones per vertex. Uniform weight 1/boneCount.</summary>
        ExtendedVertexGroups = 1,
        /// <summary>HD skin buffer (B0-3 + W0-3 per vertex).</summary>
        Skin = 2,
    }

    /// <summary>Result of building one geoset into a Unity mesh.</summary>
    public sealed class MdxMeshGeoset
    {
        public Mesh Mesh;
        public int MaterialId;
        /// <summary>Index of the geoset inside the model's Geosets list.</summary>
        public int SourceGeosetIndex;
        public MdxSkinningType SkinningType;
        /// <summary>4 or 8, depending on the skinning type.</summary>
        public int MaxBones;
    }

    /// <summary>Everything needed to render a model via the custom skinning shader.</summary>
    public sealed class MdxMeshData
    {
        public readonly List<MdxMeshNode> Nodes = new();
        public readonly List<MdxMeshGeoset> Geosets = new();

        /// <summary>Indices (into <see cref="Nodes"/>) referenced by any geoset's MATS.</summary>
        public readonly List<int> BoneMap = new();

        public int NodeCount => Nodes.Count;
    }

    /// <summary>
    /// Builds Unity meshes and the combined node list from a parsed <see cref="MdxModel"/>.
    /// Geometry stays in raw WC3 space (the prefab root rotation handles orientation);
    /// vertex data layout mirrors <c>setupgeosets.ts</c> of the mdx-m3-viewer reference:
    ///   uv0    = texture coords (coordId 0), uv1 = coordId 1 when present
    ///   uv2    = bone indices 0-3, uv3 = bone indices 4-7
    ///   uv4    = weights 0-3, uv5 = weights 4-7 (uniform 1/boneCount for vertex groups, W/255 for Skin)
    ///   uv6.x  = boneCount (0 means identity), uv6.y = skinType (0=vertex groups, 1=skin)
    /// Skin-bone indices use the reference convention: vertex groups store matrix index + 1
    /// (1-based, 0 = unused, since matrix 0 is a valid bone), Skin stores the raw 0-based
    /// bone indices (all 4 always used).
    /// </summary>
    public static class MdxMeshBuilder
    {
        const int MaxRegularBones = 4;
        const int MaxExtendedBones = 8;

        public static MdxMeshData Build(MdxModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var data = new MdxMeshData();

            data.Nodes.AddRange(BuildNodeList(model));
            BuildBoneMap(model, data);

            for (var i = 0; i < model.Geosets.Count; i++)
            {
                var geoset = model.Geosets[i];

                // Only the base and -1 lods are rendered (same as the reference).
                if (geoset.Lod > 0)
                {
                    continue;
                }

                data.Geosets.Add(BuildGeosetMesh(model, geoset, i));
            }

            return data;
        }

        public static MdxMeshData BuildFromFile(string path)
        {
            return Build(MdxModel.Load(System.IO.File.ReadAllBytes(path)));
        }

        /// <summary>
        /// Builds the combined generic-object node list in the game/reference order
        /// (bones, lights, helpers, attachments, particle emitters, particle emitters 2,
        /// popcorn emitters, ribbon emitters, event objects, collision shapes).
        /// Geoset matrix indices (MATS) index straight into this list, so the runtime
        /// node evaluation and bone texture must use the same <see cref="MdxMeshNode.ListIndex"/>.
        /// </summary>
        public static List<MdxMeshNode> BuildNodeList(MdxModel model)
        {
            var list = new List<MdxMeshNode>();

            AppendNode(list, model.Bones, model);
            AppendNode(list, model.Lights, model);
            AppendNode(list, model.Helpers, model);
            AppendNode(list, model.Attachments, model);
            AppendNode(list, model.ParticleEmitters, model);
            AppendNode(list, model.ParticleEmitters2, model);
            AppendNode(list, model.ParticleEmittersPopcorn, model);
            AppendNode(list, model.RibbonEmitters, model);
            AppendNode(list, model.EventObjects, model);
            AppendNode(list, model.CollisionShapes, model);

            return list;
        }

        static void AppendNode<T>(List<MdxMeshNode> list, List<T> objects, MdxModel model) where T : GenericObject
        {
            for (var i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                var node = new MdxMeshNode
                {
                    Name = obj.Name,
                    ObjectId = obj.ObjectId,
                    ParentId = obj.ParentId,
                    Pivot = GetPivot(model, obj.ObjectId),
                    ListIndex = list.Count,
                };

                // Same self-parent fix as the reference viewer.
                if (node.ObjectId == node.ParentId)
                {
                    node.ParentId = -1;
                }

                list.Add(node);
            }
        }

        static Vector3 GetPivot(MdxModel model, int objectId)
        {
            if (objectId >= 0 && objectId < model.PivotPoints.Count)
            {
                var pivot = model.PivotPoints[objectId];
                return new Vector3(pivot[0], pivot[1], pivot[2]);
            }

            return Vector3.zero;
        }

        static void BuildBoneMap(MdxModel model, MdxMeshData data)
        {
            var seen = new HashSet<int>();

            for (var i = 0; i < model.Geosets.Count; i++)
            {
                var geoset = model.Geosets[i];

                if (geoset.Lod > 0)
                {
                    continue;
                }

                for (var j = 0; j < geoset.MatrixIndices.Length; j++)
                {
                    var index = (int)geoset.MatrixIndices[j];

                    if (index >= 0 && index < data.NodeCount && seen.Add(index))
                    {
                        data.BoneMap.Add(index);
                    }
                }
            }

            data.BoneMap.Sort();
        }

        static MdxMeshGeoset BuildGeosetMesh(MdxModel model, Geoset geoset, int sourceIndex)
        {
            var vertexCount = geoset.Vertices.Length / 3;
            var mesh = new Mesh();

            if (vertexCount > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.name = "geoset_" + sourceIndex;
            mesh.vertices = ToVector3Array(geoset.Vertices);

            if (geoset.Normals.Length >= vertexCount * 3)
            {
                mesh.normals = ToVector3Array(geoset.Normals);
            }

            if (geoset.UvSets.Count > 0)
            {
                mesh.uv = ToVector2Array(geoset.UvSets[0], vertexCount);

                if (geoset.UvSets.Count > 1)
                {
                    mesh.SetUVs(1, ToVector2Array(geoset.UvSets[1], vertexCount));
                }
            }

            if (geoset.Tangents.Length >= vertexCount * 4)
            {
                mesh.tangents = ToVector4Array(geoset.Tangents);
            }

            var skinning = DetermineSkinning(geoset);
            WriteSkin(geoset, vertexCount, skinning, mesh);

            mesh.triangles = BuildTriangles(geoset);

            var bounds = geoset.Extent;
            if (bounds.Max[0] > bounds.Min[0] || bounds.Max[1] > bounds.Min[1] || bounds.Max[2] > bounds.Min[2])
            {
                var min = new Vector3(bounds.Min[0], bounds.Min[1], bounds.Min[2]);
                var max = new Vector3(bounds.Max[0], bounds.Max[1], bounds.Max[2]);
                mesh.bounds = new Bounds((min + max) * 0.5f, max - min);
            }
            else
            {
                mesh.RecalculateBounds();
            }

            return new MdxMeshGeoset
            {
                Mesh = mesh,
                MaterialId = (int)geoset.MaterialId,
                SourceGeosetIndex = sourceIndex,
                SkinningType = skinning,
                MaxBones = skinning == MdxSkinningType.ExtendedVertexGroups ? MaxExtendedBones : MaxRegularBones,
            };
        }

        static MdxSkinningType DetermineSkinning(Geoset geoset)
        {
            if (geoset.Skin.Length > 0)
            {
                return MdxSkinningType.Skin;
            }

            var biggestGroup = 0;
            for (var i = 0; i < geoset.MatrixGroups.Length; i++)
            {
                if (geoset.MatrixGroups[i] > biggestGroup)
                {
                    biggestGroup = (int)geoset.MatrixGroups[i];
                }
            }

            return biggestGroup > MaxRegularBones
                ? MdxSkinningType.ExtendedVertexGroups
                : MdxSkinningType.VertexGroups;
        }

        static void WriteSkin(Geoset geoset, int vertexCount, MdxSkinningType skinning, Mesh mesh)
        {
            var bones0 = new Vector4[vertexCount];
            var bones1 = new Vector4[vertexCount];
            var weights0 = new Vector4[vertexCount];
            var weights1 = new Vector4[vertexCount];
            var info = new Vector4[vertexCount];

            if (skinning == MdxSkinningType.Skin)
            {
                var skin = geoset.Skin;

                for (var v = 0; v < vertexCount; v++)
                {
                    var b = v * 8;
                    bones0[v] = new Vector4(skin[b], skin[b + 1], skin[b + 2], skin[b + 3]);
                    weights0[v] = new Vector4(skin[b + 4] / 255f, skin[b + 5] / 255f, skin[b + 6] / 255f, skin[b + 7] / 255f);
                    info[v] = new Vector4(4, 1, 0, 0);
                }
            }
            else
            {
                var groups = SliceMatrixGroups(geoset);
                var maxBones = skinning == MdxSkinningType.ExtendedVertexGroups ? MaxExtendedBones : MaxRegularBones;

                for (var v = 0; v < vertexCount; v++)
                {
                    var groupId = geoset.VertexGroups[v];

                    if (groupId < groups.Count)
                    {
                        var group = groups[groupId];
                        var count = Math.Min(group.Count, maxBones);

                        var weight = count > 0 ? 1f / count : 0f;

                        for (var j = 0; j < count; j++)
                        {
                            // 1-based, like the reference (0 = no bone). Bone/matrix index 0 is
                            // valid, so +1 is needed to distinguish it from an unused slot.
                            var index = group[j] + 1;

                            if (j < 4)
                            {
                                SetComponent(ref bones0[v], j, index);
                                SetComponent(ref weights0[v], j, weight);
                            }
                            else
                            {
                                SetComponent(ref bones1[v], j - 4, index);
                                SetComponent(ref weights1[v], j - 4, weight);
                            }
                        }

                        info[v] = new Vector4(count, 0, 0, 0);
                    }
                    else
                    {
                        // Invalid vertex group. The shader treats boneCount == 0 as identity,
                        // so the vertex keeps its bind position instead of collapsing (the
                        // reference gives a zero matrix here, which the game tolerates).
                        info[v] = new Vector4(0, 0, 0, 0);
                    }
                }
            }

            mesh.SetUVs(2, bones0);
            mesh.SetUVs(3, bones1);
            mesh.SetUVs(4, weights0);
            mesh.SetUVs(5, weights1);
            mesh.SetUVs(6, info);
        }

        static List<List<uint>> SliceMatrixGroups(Geoset geoset)
        {
            var groups = new List<List<uint>>();
            var offset = 0;

            for (var i = 0; i < geoset.MatrixGroups.Length; i++)
            {
                var size = (int)geoset.MatrixGroups[i];
                var group = new List<uint>(size);

                for (var j = 0; j < size && offset + j < geoset.MatrixIndices.Length; j++)
                {
                    group.Add(geoset.MatrixIndices[offset + j]);
                }

                groups.Add(group);
                offset += size;
            }

            return groups;
        }

        static int[] BuildTriangles(Geoset geoset)
        {
            var faces = geoset.Faces;
            var faceType = geoset.FaceTypeGroups.Length > 0 ? (int)geoset.FaceTypeGroups[0] : 4;

            switch (faceType)
            {
                case 4: // Triangles
                    return ToIntArray(faces);
                case 5: // Triangle strip (drawn as a single strip, like the reference)
                case 6: // Triangle fan
                    return faceType == 5 ? TriangulateStrip(faces) : TriangulateFan(faces);
                default:
                    return Array.Empty<int>();
            }
        }

        static int[] TriangulateStrip(ushort[] faces)
        {
            if (faces.Length < 3)
            {
                return Array.Empty<int>();
            }

            var triangles = new int[(faces.Length - 2) * 3];
            var t = 0;

            for (var i = 0; i + 2 < faces.Length; i++)
            {
                if ((i & 1) == 0)
                {
                    triangles[t++] = faces[i];
                    triangles[t++] = faces[i + 1];
                    triangles[t++] = faces[i + 2];
                }
                else
                {
                    triangles[t++] = faces[i + 1];
                    triangles[t++] = faces[i];
                    triangles[t++] = faces[i + 2];
                }
            }

            return triangles;
        }

        static int[] TriangulateFan(ushort[] faces)
        {
            if (faces.Length < 3)
            {
                return Array.Empty<int>();
            }

            var triangles = new int[(faces.Length - 2) * 3];
            var t = 0;

            for (var i = 1; i + 1 < faces.Length; i++)
            {
                triangles[t++] = faces[0];
                triangles[t++] = faces[i];
                triangles[t++] = faces[i + 1];
            }

            return triangles;
        }

        static Vector3[] ToVector3Array(float[] values)
        {
            var count = values.Length / 3;
            var result = new Vector3[count];

            for (var i = 0; i < count; i++)
            {
                var b = i * 3;
                result[i] = new Vector3(values[b], values[b + 1], values[b + 2]);
            }

            return result;
        }

        static Vector2[] ToVector2Array(float[] values, int vertexCount)
        {
            var count = Math.Min(vertexCount, values.Length / 2);
            var result = new Vector2[count];

            for (var i = 0; i < count; i++)
            {
                var b = i * 2;
                result[i] = new Vector2(values[b], values[b + 1]);
            }

            return result;
        }

        static Vector4[] ToVector4Array(float[] values)
        {
            var count = values.Length / 4;
            var result = new Vector4[count];

            for (var i = 0; i < count; i++)
            {
                var b = i * 4;
                result[i] = new Vector4(values[b], values[b + 1], values[b + 2], values[b + 3]);
            }

            return result;
        }

        static int[] ToIntArray(ushort[] values)
        {
            var result = new int[values.Length];

            for (var i = 0; i < values.Length; i++)
            {
                result[i] = values[i];
            }

            return result;
        }

        static void SetComponent(ref Vector4 vector, int index, float value)
        {
            switch (index)
            {
                case 0: vector.x = value; break;
                case 1: vector.y = value; break;
                case 2: vector.z = value; break;
                case 3: vector.w = value; break;
            }
        }
    }
}
