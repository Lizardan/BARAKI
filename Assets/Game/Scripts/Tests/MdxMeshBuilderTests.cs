using System;
using System.Collections.Generic;
using System.IO;
using Game.Mdx;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MdxMeshBuilderTests
    {
        const string SourseFolder = "NewModels/SOURSE";

        static string SourcePath(string name)
        {
            return Path.Combine(Application.dataPath, SourseFolder, name);
        }

        static MdxMeshData Build(string name)
        {
            var path = SourcePath(name);
            Assert.IsTrue(File.Exists(path), "Missing test model: " + path);
            return MdxMeshBuilder.BuildFromFile(path);
        }

        [Test]
        public void AirstrikeRocket_BuildsExpectedMesh()
        {
            var data = Build("Airstrike Rocket.mdx");

            Assert.AreEqual(1, data.Geosets.Count);

            var geoset = data.Geosets[0];
            var mesh = geoset.Mesh;

            Assert.AreEqual(0, geoset.MaterialId);
            Assert.AreEqual(MdxSkinningType.VertexGroups, geoset.SkinningType);
            Assert.AreEqual(515, mesh.vertexCount);
            Assert.AreEqual(912, mesh.triangles.Length);
            Assert.IsNotNull(mesh.uv);
            Assert.IsNotNull(mesh.normals);
        }

        [Test]
        public void GnollBrawler_BuildsExpectedMeshes()
        {
            var data = Build("GnollBrawler.mdx");

            Assert.AreEqual(75, data.NodeCount);
            Assert.AreEqual(31, data.BoneMap.Count);
            Assert.AreEqual(6, data.Geosets.Count);

            Assert.AreEqual(267, data.Geosets[0].Mesh.vertexCount);
            Assert.AreEqual(15, data.Geosets[5].Mesh.vertexCount);
        }

        [Test]
        public void Mesh_ExposesFullNodeListForMatrixIndices()
        {
            var data = Build("GnollBrawler.mdx");

            Assert.Greater(data.NodeCount, 30, "MATS must be able to reference helpers/attachments beyond the bone list.");

            foreach (var index in data.BoneMap)
            {
                Assert.Less(index, data.NodeCount);
            }
        }

        [Test]
        public void RenderedGeosets_HaveUvAndBoneChannels()
        {
            foreach (var name in new[] { "Airstrike Rocket.mdx", "GnollBrawler.mdx" })
            {
                var data = Build(name);

                foreach (var geoset in data.Geosets)
                {
                    var mesh = geoset.Mesh;
                    var uv2 = new List<Vector4>();
                    var uv6 = new List<Vector4>();
                    mesh.GetUVs(2, uv2);
                    mesh.GetUVs(6, uv6);

                    Assert.IsNotNull(mesh.uv, name + " geoset " + geoset.SourceGeosetIndex + " must have uv0");
                    Assert.AreEqual(mesh.vertexCount, uv2.Count, name + " must carry bone indices in uv2");

                    for (var v = 0; v < mesh.vertexCount; v++)
                    {
                        var count = (int)uv6[v].x;

                        if (count > 0)
                        {
                            Assert.LessOrEqual(count, geoset.MaxBones, name + " vertex " + v + " has too many bones");
                        }
                    }
                }
            }
        }

        [Test]
        public void VertexGroups_WeightsSumToOne()
        {
            var data = Build("Airstrike Rocket.mdx");
            var mesh = data.Geosets[0].Mesh;

            var weights0 = new List<Vector4>();
            var weights1 = new List<Vector4>();
            var info = new List<Vector4>();
            var bones0 = new List<Vector4>();
            mesh.GetUVs(4, weights0);
            mesh.GetUVs(5, weights1);
            mesh.GetUVs(6, info);
            mesh.GetUVs(2, bones0);

            for (var v = 0; v < mesh.vertexCount; v++)
            {
                var count = (int)info[v].x;
                var sum = weights0[v].x + weights0[v].y + weights0[v].z + weights0[v].w
                        + weights1[v].x + weights1[v].y + weights1[v].z + weights1[v].w;

                Assert.AreEqual(count, CountNonZero(bones0[v]), "vertex " + v + " bone count mismatch");
                Assert.AreEqual(1f, sum, 0.001f, "vertex " + v + " weights must sum to 1");
            }
        }

        static int CountNonZero(Vector4 bones)
        {
            var count = 0;
            if (bones.x > 0) count++;
            if (bones.y > 0) count++;
            if (bones.z > 0) count++;
            if (bones.w > 0) count++;
            return count;
        }
    }
}
