using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitVisualHeightTests
    {
        [Test]
        public void MeasureAboveFeet_UsesMeshTop_IgnoresParticleRenderer()
        {
            var feet = new GameObject("Feet");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(feet.transform, false);

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(model.transform, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);
                body.transform.localScale = new Vector3(1f, 1f, 1f);

                var aura = new GameObject("Aura");
                aura.transform.SetParent(feet.transform, false);
                var particles = aura.AddComponent<ParticleSystem>();
                Assert.IsNotNull(particles);
                var particleRenderer = aura.GetComponent<ParticleSystemRenderer>();
                Assert.IsNotNull(particleRenderer);
                // Inflate particle render bounds far above the mesh.
                particleRenderer.pivot = new Vector3(0f, 20f, 0f);

                var height = UnitVisualHeight.MeasureAboveFeet(feet.transform, model.transform);
                Assert.Less(height, 4f, "Particle VFX must not inflate HP-bar height.");
                Assert.Greater(height, 1.5f);
            }
            finally
            {
                Object.DestroyImmediate(feet);
            }
        }

        [Test]
        public void ShouldMeasure_OnlyMeshAndSkinned()
        {
            var go = new GameObject("Temp");
            try
            {
                var mesh = go.AddComponent<MeshRenderer>();
                Assert.IsTrue(UnitVisualHeight.ShouldMeasure(mesh));

                var particles = go.AddComponent<ParticleSystem>();
                Assert.IsNotNull(particles);
                var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
                Assert.IsFalse(UnitVisualHeight.ShouldMeasure(particleRenderer));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
