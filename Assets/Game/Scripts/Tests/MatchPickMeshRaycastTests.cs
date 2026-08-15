using Game.Gameplay.Match.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchPickMeshRaycastTests
    {
        [Test]
        public void TryHit_CubeCenterRay_Hits()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
                var ray = new Ray(new Vector3(0f, 0f, -3f), Vector3.forward);
                Assert.IsTrue(MatchPickMeshRaycast.TryHit(cube.transform, mesh, ray, 10f, out var distance));
                Assert.AreEqual(2.5f, distance, 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void TryHit_RayThroughEmptyBoundsCorner_MissesThinMesh()
        {
            var root = new GameObject("ThinMeshPick");
            try
            {
                var filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = CreateRightTriangleMesh();
                var ray = new Ray(new Vector3(0.9f, 0.9f, -2f), Vector3.forward);
                Assert.IsTrue(filter.sharedMesh.bounds.Contains(new Vector3(0.9f, 0.9f, 0f)));
                Assert.IsFalse(MatchPickMeshRaycast.TryHit(root.transform, filter.sharedMesh, ray, 10f, out _));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryResolveHit_WithoutMesh_KeepsPhysicsDistance()
        {
            var proxy = new GameObject("Proxy");
            try
            {
                var box = proxy.AddComponent<BoxCollider>();
                Assert.IsTrue(MatchPickMeshRaycast.TryResolveHit(
                    box,
                    new Ray(Vector3.zero, Vector3.forward),
                    physicsDistance: 4.2f,
                    maxDistance: 50f,
                    out var distance));
                Assert.AreEqual(4.2f, distance, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(proxy);
            }
        }

        static Mesh CreateRightTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
