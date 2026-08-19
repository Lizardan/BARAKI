using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;
using Game.Gameplay.Vfx;

namespace Game.Tests
{
    public sealed class AbilityFxPreserveTests
    {
        [Test]
        public void WithPreservedAuthored_KeepsPrefabAndColor()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var authored = new AbilityFx
                {
                    Color = new Color(0.2f, 0.8f, 0.1f, 1f),
                    VfxPrefab = cube,
                    Anchor = AbilityVfxAnchor.Target,
                };
                var defaults = new AbilityFx
                {
                    Color = Color.red,
                    VfxPrefab = null,
                    Anchor = AbilityVfxAnchor.Caster,
                };

                var merged = authored.WithPreservedAuthored(defaults);
                Assert.AreEqual(authored.Color, merged.Color);
                Assert.AreSame(cube, merged.VfxPrefab);
                Assert.AreEqual(AbilityVfxAnchor.Target, merged.Anchor);
                Assert.AreEqual(AbilityAnimKind.Unspecified, merged.AnimKind);
            Assert.IsTrue(string.IsNullOrEmpty(merged.AnimState));
            Assert.AreEqual(0f, merged.Scale);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void WithPreservedAuthored_EmptyAuthored_UsesDefaults()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var authored = default(AbilityFx);
                var defaults = new AbilityFx
                {
                    Color = Color.blue,
                    VfxPrefab = cube,
                    Anchor = AbilityVfxAnchor.Ground,
                };

                var merged = authored.WithPreservedAuthored(defaults);
                Assert.AreEqual(Color.blue, merged.Color);
                Assert.AreSame(cube, merged.VfxPrefab);
                Assert.AreEqual(AbilityVfxAnchor.Ground, merged.Anchor);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void WithPreservedAuthored_UnspecifiedAnimKind_TakesDefault()
        {
            var authored = new AbilityFx
            {
                Color = Color.white,
                AnimKind = AbilityAnimKind.Unspecified,
            };
            var defaults = new AbilityFx
            {
                Color = Color.red,
                AnimKind = AbilityAnimKind.Cast,
            };

            var merged = authored.WithPreservedAuthored(defaults);
            Assert.AreEqual(AbilityAnimKind.Cast, merged.AnimKind);
        }

        [Test]
        public void WithPreservedAuthored_AuthoredAnimKind_Wins()
        {
            var authored = new AbilityFx { AnimKind = AbilityAnimKind.Attack };
            var defaults = new AbilityFx { AnimKind = AbilityAnimKind.Cast };
            Assert.AreEqual(
                AbilityAnimKind.Attack,
                authored.WithPreservedAuthored(defaults).AnimKind);
        }

        [Test]
        public void WithPreservedAuthored_UnspecifiedAnchor_TakesDefault()
        {
            var authored = new AbilityFx
            {
                Color = Color.white,
                Anchor = AbilityVfxAnchor.Unspecified,
            };
            var defaults = new AbilityFx
            {
                Color = Color.red,
                Anchor = AbilityVfxAnchor.Caster,
            };

            var merged = authored.WithPreservedAuthored(defaults);
            Assert.AreEqual(Color.white, merged.Color);
            Assert.AreEqual(AbilityVfxAnchor.Caster, merged.Anchor);
        }

        [Test]
        public void WithPreservedAuthored_KeepsAnimStateVariantAndScale()
        {
            var authored = new AbilityFx
            {
                Color = Color.white,
                AnimState = "Attack",
                AnimVariant = 1,
                Scale = 2.5f,
            };
            var defaults = new AbilityFx
            {
                Color = Color.red,
                AnimState = "Cast",
                AnimVariant = 0,
                Scale = 4f,
            };

            var merged = authored.WithPreservedAuthored(defaults);
            Assert.AreEqual("Attack", merged.AnimState);
            Assert.AreEqual(1, merged.AnimVariant);
            Assert.AreEqual(2.5f, merged.Scale);
        }

        [Test]
        public void WithPreservedAuthored_EmptyAnimStateAndScale_TakesDefaults()
        {
            var authored = new AbilityFx { Color = Color.white };
            var defaults = new AbilityFx
            {
                Color = Color.red,
                AnimState = "Cast",
                AnimVariant = 1,
                Scale = 3f,
            };

            var merged = authored.WithPreservedAuthored(defaults);
            Assert.AreEqual("Cast", merged.AnimState);
            Assert.AreEqual(1, merged.AnimVariant);
            Assert.AreEqual(3f, merged.Scale);
        }

        [Test]
        public void ResolveScale_ZeroMeansOne()
        {
            Assert.AreEqual(1f, AbilityFx.ResolveScale(0f));
            Assert.AreEqual(2f, AbilityFx.ResolveScale(2f));
        }
    }
}
