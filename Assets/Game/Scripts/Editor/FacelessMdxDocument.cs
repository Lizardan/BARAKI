using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Warcraft 3 MDX v800 reader for Faceless review import (skeleton + Stand/Walk/Attack).</summary>
    public sealed class FacelessMdxDocument
    {
        public const float Scale = 0.01f;

        public readonly List<Node> Nodes = new();
        public readonly List<Geoset> Geosets = new();
        public readonly List<Sequence> Sequences = new();
        public readonly HashSet<int> HiddenGeosets = new();
        public readonly List<TexInfo> Textures = new();
        public readonly List<MatInfo> Materials = new();
        public Vector3[] Pivots = Array.Empty<Vector3>();

        public readonly struct Sequence
        {
            public readonly string Name;
            public readonly int StartMs;
            public readonly int EndMs;

            public Sequence(string name, int startMs, int endMs)
            {
                Name = name;
                StartMs = startMs;
                EndMs = endMs;
            }
        }

        public sealed class Node
        {
            public string Name;
            public int ObjectId;
            public int ParentId;
            public Vector3 Pivot;
            public readonly List<Vec3Key> Translation = new();
            public readonly List<QuatKey> Rotation = new();
            public readonly List<Vec3Key> Scale = new();
        }

        public readonly struct Vec3Key
        {
            public readonly int TimeMs;
            public readonly Vector3 Value;

            public Vec3Key(int timeMs, Vector3 value)
            {
                TimeMs = timeMs;
                Value = value;
            }
        }

        public readonly struct QuatKey
        {
            public readonly int TimeMs;
            public readonly Quaternion Value;

            public QuatKey(int timeMs, Quaternion value)
            {
                TimeMs = timeMs;
                Value = value;
            }
        }

        public readonly struct TexInfo
        {
            public readonly int ReplaceableId;
            public readonly string Path;

            public TexInfo(int replaceableId, string path)
            {
                ReplaceableId = replaceableId;
                Path = path;
            }
        }

        public sealed class MatInfo
        {
            public readonly List<LayerInfo> Layers = new();
        }

        public readonly struct LayerInfo
        {
            public readonly int TextureId;
            public readonly int FilterMode;
            public readonly int ShadingFlags;

            public LayerInfo(int textureId, int filterMode, int shadingFlags)
            {
                TextureId = textureId;
                FilterMode = filterMode;
                ShadingFlags = shadingFlags;
            }
        }

        public sealed class Geoset
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int[] Triangles;
            public BoneWeight[] Weights;
            public string TextureStem;
            public bool TwoSided;
            public bool HasTeamColor;
            public bool ClipBlack;
            public bool AlphaBlend;
        }

        public static FacelessMdxDocument Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 8 || Encoding.ASCII.GetString(bytes, 0, 4) != "MDLX")
            {
                throw new InvalidDataException("Not MDX: " + path);
            }

            var doc = new FacelessMdxDocument();
            var geosPayload = Array.Empty<byte>();
            var geoaPayload = Array.Empty<byte>();
            var bonePayload = Array.Empty<byte>();
            var helpPayload = Array.Empty<byte>();
            byte[] seqs = null;
            byte[] pivt = null;
            byte[] texs = null;
            byte[] mtls = null;
            var version = 800;
            var i = 4;
            while (i + 8 <= bytes.Length)
            {
                var tag = Encoding.ASCII.GetString(bytes, i, 4);
                var size = BitConverter.ToInt32(bytes, i + 4);
                var payload = new byte[size];
                Buffer.BlockCopy(bytes, i + 8, payload, 0, size);
                switch (tag)
                {
                    case "VERS":
                        version = BitConverter.ToInt32(payload, 0);
                        break;
                    case "SEQS":
                        seqs = payload;
                        break;
                    case "GEOS":
                        geosPayload = payload;
                        break;
                    case "GEOA":
                        geoaPayload = payload;
                        break;
                    case "BONE":
                        bonePayload = payload;
                        break;
                    case "HELP":
                        helpPayload = payload;
                        break;
                    case "PIVT":
                        pivt = payload;
                        break;
                    case "TEXS":
                        texs = payload;
                        break;
                    case "MTLS":
                        mtls = payload;
                        break;
                }

                i += 8 + size;
            }

            if (version != 800)
            {
                throw new InvalidDataException("Unsupported MDX version " + version);
            }

            if (seqs != null)
            {
                for (var s = 0; s + 132 <= seqs.Length; s += 132)
                {
                    var name = ReadCString(seqs, s, 80);
                    var start = BitConverter.ToInt32(seqs, s + 80);
                    var end = BitConverter.ToInt32(seqs, s + 84);
                    doc.Sequences.Add(new Sequence(name, start, end));
                }
            }

            if (pivt != null)
            {
                var count = pivt.Length / 12;
                doc.Pivots = new Vector3[count];
                for (var p = 0; p < count; p++)
                {
                    var x = BitConverter.ToSingle(pivt, p * 12);
                    var y = BitConverter.ToSingle(pivt, p * 12 + 4);
                    var z = BitConverter.ToSingle(pivt, p * 12 + 8);
                    doc.Pivots[p] = ToUnity(x, y, z);
                }
            }

            if (texs != null)
            {
                for (var t = 0; t + 268 <= texs.Length; t += 268)
                {
                    doc.Textures.Add(new TexInfo(
                        BitConverter.ToInt32(texs, t),
                        ReadCString(texs, t + 4, 256)));
                }
            }

            if (mtls != null)
            {
                ReadMaterials(mtls, doc.Materials);
            }

            ReadNodes(bonePayload, extraBytes: 8, doc.Nodes);
            ReadNodes(helpPayload, extraBytes: 0, doc.Nodes);
            foreach (var node in doc.Nodes)
            {
                node.Pivot = node.ObjectId >= 0 && node.ObjectId < doc.Pivots.Length
                    ? doc.Pivots[node.ObjectId]
                    : Vector3.zero;
            }

            doc.HiddenGeosets.UnionWith(ReadHiddenGeosets(geoaPayload, StandSampleTimeMs(doc)));
            ReadGeosets(geosPayload, doc);
            return doc;
        }

        static int StandSampleTimeMs(FacelessMdxDocument doc)
        {
            foreach (var seq in doc.Sequences)
            {
                var name = seq.Name.Trim().ToLowerInvariant();
                if (name.StartsWith("stand") &&
                    !name.Contains("ready") &&
                    !name.Contains("victory") &&
                    !name.Contains("cinematic"))
                {
                    return seq.StartMs;
                }
            }

            foreach (var seq in doc.Sequences)
            {
                var name = seq.Name.Trim().ToLowerInvariant();
                if (name.Contains("portrait") || name.Contains("stand"))
                {
                    return seq.StartMs;
                }
            }

            return 0;
        }

        static void ReadMaterials(byte[] mtls, List<MatInfo> materials)
        {
            var i = 0;
            while (i + 4 <= mtls.Length)
            {
                var inclusive = BitConverter.ToInt32(mtls, i);
                var end = i + inclusive;
                if (inclusive < 12 || end > mtls.Length)
                {
                    break;
                }

                var mat = new MatInfo();
                var p = i + 12;
                if (p + 8 <= end && Encoding.ASCII.GetString(mtls, p, 4) == "LAYS")
                {
                    var layerCount = BitConverter.ToInt32(mtls, p + 4);
                    p += 8;
                    for (var l = 0; l < layerCount && p + 16 <= end; l++)
                    {
                        var layerSize = BitConverter.ToInt32(mtls, p);
                        var layerEnd = p + layerSize;
                        if (layerSize < 16 || layerEnd > end)
                        {
                            break;
                        }

                        mat.Layers.Add(new LayerInfo(
                            BitConverter.ToInt32(mtls, p + 12),
                            BitConverter.ToInt32(mtls, p + 4),
                            BitConverter.ToInt32(mtls, p + 8)));
                        p = layerEnd;
                    }
                }

                materials.Add(mat);
                i = end;
            }
        }

        public Sequence? FindSequence(params string[] preferences)
        {
            foreach (var pref in preferences)
            {
                var hit = FindSequenceSingle(pref);
                if (hit.HasValue)
                {
                    return hit;
                }
            }

            return Sequences.Count > 0 ? Sequences[0] : null;
        }

        Sequence? FindSequenceSingle(string preference)
        {
            var want = preference.ToLowerInvariant();
            foreach (var seq in Sequences)
            {
                var name = seq.Name.Trim().ToLowerInvariant();
                if (want == "stand")
                {
                    if (name.StartsWith("stand") &&
                        !name.Contains("ready") &&
                        !name.Contains("victory") &&
                        !name.Contains("channel") &&
                        !name.Contains("gold") &&
                        !name.Contains("lumber") &&
                        !name.Contains("cinematic"))
                    {
                        return seq;
                    }
                }
                else if (want == "walk")
                {
                    if (name.Contains("walk") && !name.Contains("attack") && !name.Contains("gold"))
                    {
                        return seq;
                    }
                }
                else if (want == "attack")
                {
                    if (name.Contains("attack") &&
                        !name.Contains("slam") &&
                        !name.Contains("walk") &&
                        !name.Contains("gold") &&
                        !name.Contains("lumber") &&
                        !name.Contains("spin"))
                    {
                        return seq;
                    }
                }
                else if (name.Contains(want))
                {
                    return seq;
                }
            }

            if (want == "stand")
            {
                foreach (var seq in Sequences)
                {
                    if (seq.Name.Trim().ToLowerInvariant().Contains("stand") ||
                        seq.Name.Trim().ToLowerInvariant().Contains("portrait"))
                    {
                        return seq;
                    }
                }
            }

            return null;
        }

        static void ReadNodes(byte[] payload, int extraBytes, List<Node> nodes)
        {
            var j = 0;
            while (j + 96 <= payload.Length)
            {
                var size = BitConverter.ToInt32(payload, j);
                if (size < 96 || j + size + extraBytes > payload.Length)
                {
                    break;
                }

                var node = new Node
                {
                    Name = Sanitize(ReadCString(payload, j + 4, 80)),
                    ObjectId = BitConverter.ToInt32(payload, j + 84),
                    ParentId = BitConverter.ToInt32(payload, j + 88),
                };
                ReadTracks(payload, j + 96, j + size, node);
                nodes.Add(node);
                j += size + extraBytes;
            }
        }

        static void ReadTracks(byte[] buf, int start, int end, Node node)
        {
            var p = start;
            while (p + 16 <= end)
            {
                var tag = Encoding.ASCII.GetString(buf, p, 4);
                if (tag is not ("KGTR" or "KGRT" or "KGSC" or "KATV"))
                {
                    break;
                }

                p += 4;
                var count = BitConverter.ToInt32(buf, p);
                var interp = BitConverter.ToInt32(buf, p + 4);
                p += 12;
                var hasTan = interp >= 2;
                for (var k = 0; k < count; k++)
                {
                    if (p + 8 > end)
                    {
                        return;
                    }

                    var time = BitConverter.ToInt32(buf, p);
                    p += 4;
                    switch (tag)
                    {
                        case "KGRT":
                            if (p + 16 > end) return;
                            var qx = BitConverter.ToSingle(buf, p);
                            var qy = BitConverter.ToSingle(buf, p + 4);
                            var qz = BitConverter.ToSingle(buf, p + 8);
                            var qw = BitConverter.ToSingle(buf, p + 12);
                            p += 16 + (hasTan ? 32 : 0);
                            node.Rotation.Add(new QuatKey(time, ToUnity(qx, qy, qz, qw)));
                            break;
                        case "KGTR":
                        case "KGSC":
                            if (p + 12 > end) return;
                            var x = BitConverter.ToSingle(buf, p);
                            var y = BitConverter.ToSingle(buf, p + 4);
                            var z = BitConverter.ToSingle(buf, p + 8);
                            p += 12 + (hasTan ? 24 : 0);
                            var v = ToUnity(x, y, z);
                            if (tag == "KGTR")
                            {
                                node.Translation.Add(new Vec3Key(time, v));
                            }
                            else
                            {
                                node.Scale.Add(new Vec3Key(time, new Vector3(x, z, y)));
                            }

                            break;
                        default:
                            p += 4 + (hasTan ? 8 : 0);
                            break;
                    }
                }
            }
        }

        static HashSet<int> ReadHiddenGeosets(byte[] geoa, int sampleMs)
        {
            var hidden = new HashSet<int>();
            var i = 0;
            while (i + 4 <= geoa.Length)
            {
                var inclusive = BitConverter.ToInt32(geoa, i);
                var end = i + inclusive;
                if (inclusive < 28 || end > geoa.Length)
                {
                    break;
                }

                var staticAlpha = BitConverter.ToSingle(geoa, i + 4);
                var geosetId = BitConverter.ToInt32(geoa, i + 24);
                var alpha = SampleKgao(geoa, i + 28, end, staticAlpha, sampleMs);
                if (alpha <= 0.01f)
                {
                    hidden.Add(geosetId);
                }

                i = end;
            }

            return hidden;
        }

        static float SampleKgao(byte[] buf, int start, int end, float staticAlpha, int sampleMs)
        {
            if (end - start < 16 || Encoding.ASCII.GetString(buf, start, 4) != "KGAO")
            {
                return staticAlpha;
            }

            var count = BitConverter.ToInt32(buf, start + 4);
            var interp = BitConverter.ToInt32(buf, start + 8);
            var p = start + 16;
            var stride = interp >= 2 ? 16 : 8;
            int? prevTime = null;
            var prevVal = staticAlpha;
            for (var k = 0; k < count && p + 8 <= end; k++)
            {
                var time = BitConverter.ToInt32(buf, p);
                var value = BitConverter.ToSingle(buf, p + 4);
                p += stride;
                if (time >= sampleMs)
                {
                    if (prevTime == null)
                    {
                        return time == sampleMs ? value : staticAlpha;
                    }

                    var span = time - prevTime.Value;
                    if (span <= 0)
                    {
                        return value;
                    }

                    return Mathf.Lerp(prevVal, value, (sampleMs - prevTime.Value) / (float)span);
                }

                prevTime = time;
                prevVal = value;
            }

            return prevTime == null ? staticAlpha : prevVal;
        }

        static void ReadGeosets(byte[] payload, FacelessMdxDocument doc)
        {
            var idToIndex = new Dictionary<int, int>();
            for (var n = 0; n < doc.Nodes.Count; n++)
            {
                idToIndex[doc.Nodes[n].ObjectId] = n;
            }

            var i = 0;
            var geosetIndex = 0;
            while (i + 4 <= payload.Length)
            {
                var inclusive = BitConverter.ToInt32(payload, i);
                var end = i + inclusive;
                if (inclusive < 8 || end > payload.Length)
                {
                    break;
                }

                var p = i + 4;
                Expect(payload, ref p, "VRTX");
                var vcount = BitConverter.ToInt32(payload, p);
                p += 4;
                var verts = ReadVec3Array(payload, ref p, vcount);
                Expect(payload, ref p, "NRMS");
                var ncount = BitConverter.ToInt32(payload, p);
                p += 4;
                var norms = ReadVec3Array(payload, ref p, ncount);
                Expect(payload, ref p, "PTYP");
                var tcount = BitConverter.ToInt32(payload, p);
                p += 4 + tcount * 4;
                Expect(payload, ref p, "PCNT");
                var gcount = BitConverter.ToInt32(payload, p);
                p += 4 + gcount * 4;
                Expect(payload, ref p, "PVTX");
                var fcount = BitConverter.ToInt32(payload, p);
                p += 4;
                var faces = new int[fcount];
                for (var f = 0; f < fcount; f++)
                {
                    faces[f] = BitConverter.ToUInt16(payload, p);
                    p += 2;
                }

                Expect(payload, ref p, "GNDX");
                var vgCount = BitConverter.ToInt32(payload, p);
                p += 4;
                var vertexGroups = new byte[vgCount];
                Buffer.BlockCopy(payload, p, vertexGroups, 0, vgCount);
                p += vgCount;
                Expect(payload, ref p, "MTGC");
                var mg = BitConverter.ToInt32(payload, p);
                p += 4;
                var matrixCounts = new int[mg];
                for (var m = 0; m < mg; m++)
                {
                    matrixCounts[m] = BitConverter.ToInt32(payload, p);
                    p += 4;
                }

                Expect(payload, ref p, "MATS");
                var mi = BitConverter.ToInt32(payload, p);
                p += 4;
                var matrixIds = new int[mi];
                for (var m = 0; m < mi; m++)
                {
                    matrixIds[m] = BitConverter.ToInt32(payload, p);
                    p += 4;
                }

                var materialId = BitConverter.ToInt32(payload, p);
                p += 12 + 28;
                var extCount = BitConverter.ToInt32(payload, p);
                p += 4 + extCount * 28;
                Expect(payload, ref p, "UVAS");
                var uvSets = BitConverter.ToInt32(payload, p);
                p += 4;
                var uvs = Array.Empty<Vector2>();
                for (var u = 0; u < uvSets; u++)
                {
                    Expect(payload, ref p, "UVBS");
                    var ucount = BitConverter.ToInt32(payload, p);
                    p += 4;
                    if (uvs.Length == 0)
                    {
                        uvs = new Vector2[ucount];
                        for (var v = 0; v < ucount; v++)
                        {
                            var tu = BitConverter.ToSingle(payload, p);
                            var tv = BitConverter.ToSingle(payload, p + 4);
                            uvs[v] = new Vector2(tu, 1f - tv);
                            p += 8;
                        }
                    }
                    else
                    {
                        p += ucount * 8;
                    }
                }

                if (!doc.HiddenGeosets.Contains(geosetIndex) &&
                    !IsJunk(verts, faces) &&
                    TryResolveVisual(
                        doc,
                        materialId,
                        out var textureStem,
                        out var twoSided,
                        out var hasTeamColor,
                        out var clipBlack,
                        out var alphaBlend,
                        out var filterMode))
                {
                    var groupBones = BuildGroupBones(matrixCounts, matrixIds);
                    var weights = new BoneWeight[verts.Length];
                    for (var v = 0; v < verts.Length; v++)
                    {
                        var g = v < vertexGroups.Length ? vertexGroups[v] : 0;
                        weights[v] = ToBoneWeight(groupBones, g, idToIndex);
                    }

                    // Small V2 FilterMode=1 geosets are ornaments (chains/spikes). Keep large V2 body
                    // opaque — JPEG alpha holes the main mesh.
                    if (!hasTeamColor &&
                        filterMode == 1 &&
                        verts.Length < 80 &&
                        textureStem.Equals("FacelessOneUnbrokenV2", StringComparison.OrdinalIgnoreCase))
                    {
                        clipBlack = true;
                        twoSided = true;
                    }

                    doc.Geosets.Add(new Geoset
                    {
                        Vertices = verts,
                        Normals = norms.Length == verts.Length ? norms : verts,
                        Uvs = uvs.Length == verts.Length ? uvs : new Vector2[verts.Length],
                        Triangles = faces,
                        Weights = weights,
                        TextureStem = textureStem,
                        TwoSided = twoSided,
                        HasTeamColor = hasTeamColor,
                        ClipBlack = clipBlack,
                        AlphaBlend = alphaBlend,
                    });
                }

                geosetIndex++;
                i = end;
            }
        }

        static List<int>[] BuildGroupBones(int[] counts, int[] ids)
        {
            var groups = new List<int>[counts.Length];
            var cursor = 0;
            for (var g = 0; g < counts.Length; g++)
            {
                var list = new List<int>(counts[g]);
                for (var n = 0; n < counts[g] && cursor < ids.Length; n++, cursor++)
                {
                    list.Add(ids[cursor]);
                }

                groups[g] = list;
            }

            return groups;
        }

        static BoneWeight ToBoneWeight(List<int>[] groups, int group, Dictionary<int, int> idToIndex)
        {
            var bw = new BoneWeight();
            if (groups.Length == 0)
            {
                bw.weight0 = 1f;
                return bw;
            }

            group = Mathf.Clamp(group, 0, groups.Length - 1);
            var bones = groups[group];
            var used = Mathf.Min(4, bones.Count);
            var w = used > 0 ? 1f / used : 1f;
            if (used > 0 && idToIndex.TryGetValue(bones[0], out var b0)) { bw.boneIndex0 = b0; bw.weight0 = w; }
            else { bw.weight0 = 1f; }
            if (used > 1 && idToIndex.TryGetValue(bones[1], out var b1)) { bw.boneIndex1 = b1; bw.weight1 = w; }
            if (used > 2 && idToIndex.TryGetValue(bones[2], out var b2)) { bw.boneIndex2 = b2; bw.weight2 = w; }
            if (used > 3 && idToIndex.TryGetValue(bones[3], out var b3)) { bw.boneIndex3 = b3; bw.weight3 = w; }
            return bw;
        }

        static bool TryResolveVisual(
            FacelessMdxDocument doc,
            int materialId,
            out string textureStem,
            out bool twoSided,
            out bool hasTeamColor,
            out bool clipBlack,
            out bool alphaBlend,
            out int filterMode)
        {
            textureStem = "FacelessOneUnbrokenV2";
            twoSided = false;
            hasTeamColor = false;
            clipBlack = false;
            alphaBlend = false;
            filterMode = 0;
            if (materialId < 0 || materialId >= doc.Materials.Count)
            {
                hasTeamColor = true;
                return true;
            }

            var layers = doc.Materials[materialId].Layers;
            string path = null;
            var foundDiffuse = false;
            foreach (var layer in layers)
            {
                twoSided |= (layer.ShadingFlags & 32) != 0;
                if (layer.TextureId < 0 || layer.TextureId >= doc.Textures.Count)
                {
                    continue;
                }

                var tex = doc.Textures[layer.TextureId];
                if (tex.ReplaceableId == 2 || IsFxTexture(tex.Path) || layer.FilterMode >= 3)
                {
                    continue;
                }

                if (tex.ReplaceableId == 1)
                {
                    hasTeamColor = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(tex.Path))
                {
                    foundDiffuse = true;
                    path = tex.Path;
                    filterMode = layer.FilterMode;
                }
            }

            if (!foundDiffuse)
            {
                // Team-color-only geosets (replaceable 1 layer, no diffuse) are real geometry —
                // e.g. the 08_FacelessKing horn middle. Render as pure player color.
                // FX-only geosets stay dropped (IsFxTexture filter above).
                if (hasTeamColor)
                {
                    textureStem = "TeamColor";
                    twoSided = true;
                    clipBlack = false;
                    alphaBlend = false;
                    return true;
                }

                return false;
            }

            textureStem = RemapTextureStem(path);
            var isSkin = textureStem.Equals("FacelessOneUnbrokenV2", StringComparison.OrdinalIgnoreCase);
            var isFlameKitbash = textureStem.Equals("HeroAvatarFlame", StringComparison.OrdinalIgnoreCase);
            twoSided |= isSkin || isFlameKitbash ||
                        textureStem.Equals("ForgottenOne", StringComparison.OrdinalIgnoreCase);
            clipBlack = !hasTeamColor && !isSkin && !isFlameKitbash && filterMode == 1;
            alphaBlend = !hasTeamColor && !isSkin && !isFlameKitbash && filterMode == 2;
            return true;
        }

        static string RemapTextureStem(string path)
        {
            var slash = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            var file = slash >= 0 ? path[(slash + 1)..] : path;
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.Equals("FacelessOne", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("FacelessOneUnbroken", StringComparison.OrdinalIgnoreCase))
            {
                return "FacelessOneUnbrokenV2";
            }

            return stem;
        }

        static bool IsFxTexture(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var n = path.ToLowerInvariant();
            return n.Contains("gutz") ||
                   n.Contains("ribbon") ||
                   n.Contains("clouds") ||
                   n.Contains("dust") ||
                   n.Contains("energy") ||
                   n.Contains("teamglow") ||
                   n.Contains("shadow.blp") ||
                   n.Contains("foam") ||
                   n.Contains("lavalump") ||
                   n.Contains("genericglow") ||
                   n.Contains("star9");
        }

        static bool IsJunk(Vector3[] verts, int[] faces)
        {
            return verts.Length <= 4 || faces.Length < 3;
        }

        static Vector3[] ReadVec3Array(byte[] buf, ref int p, int count)
        {
            var arr = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var x = BitConverter.ToSingle(buf, p);
                var y = BitConverter.ToSingle(buf, p + 4);
                var z = BitConverter.ToSingle(buf, p + 8);
                arr[i] = ToUnity(x, y, z);
                p += 12;
            }

            return arr;
        }

        static void Expect(byte[] buf, ref int p, string tag)
        {
            var got = Encoding.ASCII.GetString(buf, p, 4);
            if (got != tag)
            {
                throw new InvalidDataException("Expected " + tag + ", got " + got);
            }

            p += 4;
        }

        static string ReadCString(byte[] buf, int offset, int max)
        {
            var end = offset;
            var limit = Math.Min(buf.Length, offset + max);
            while (end < limit && buf[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(buf, offset, end - offset).Trim();
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Bone";
            }

            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        public static Vector3 ToUnity(float x, float y, float z) => new(x * Scale, z * Scale, y * Scale);

        public static Quaternion ToUnity(float x, float y, float z, float w) => new(x, z, y, -w);

        public static Vector3 SampleVec3(List<Vec3Key> keys, int timeMs, Vector3 fallback)
        {
            if (keys == null || keys.Count == 0)
            {
                return fallback;
            }

            if (timeMs <= keys[0].TimeMs)
            {
                return keys[0].Value;
            }

            if (timeMs >= keys[^1].TimeMs)
            {
                return keys[^1].Value;
            }

            for (var i = 0; i < keys.Count - 1; i++)
            {
                if (timeMs > keys[i + 1].TimeMs)
                {
                    continue;
                }

                var a = keys[i];
                var b = keys[i + 1];
                var u = (timeMs - a.TimeMs) / (float)Mathf.Max(1, b.TimeMs - a.TimeMs);
                return Vector3.LerpUnclamped(a.Value, b.Value, u);
            }

            return keys[^1].Value;
        }

        public static Quaternion SampleQuat(List<QuatKey> keys, int timeMs)
        {
            if (keys == null || keys.Count == 0)
            {
                return Quaternion.identity;
            }

            if (timeMs <= keys[0].TimeMs)
            {
                return keys[0].Value;
            }

            if (timeMs >= keys[^1].TimeMs)
            {
                return keys[^1].Value;
            }

            for (var i = 0; i < keys.Count - 1; i++)
            {
                if (timeMs > keys[i + 1].TimeMs)
                {
                    continue;
                }

                var a = keys[i];
                var b = keys[i + 1].Value;
                if (Quaternion.Dot(a.Value, b) < 0f)
                {
                    b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
                }

                var u = (timeMs - a.TimeMs) / (float)Mathf.Max(1, keys[i + 1].TimeMs - a.TimeMs);
                return Quaternion.SlerpUnclamped(a.Value, b, u);
            }

            return keys[^1].Value;
        }
    }
}
