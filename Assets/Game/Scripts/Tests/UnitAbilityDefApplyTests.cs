using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitAbilityDefApplyTests
    {
        [Test]
        public void ApplyRadius_WritesCombatRadius()
        {
            var def = ScriptableObject.CreateInstance<UnitAbilityDef>();
            try
            {
                def.ApplyRadius(4.5f);
                Assert.AreEqual(4.5f, def.Radius);
                def.ApplyRadius(-1f);
                Assert.AreEqual(0f, def.Radius);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void ApplyCastRange_WritesSearchRange()
        {
            var def = ScriptableObject.CreateInstance<UnitAbilityDef>();
            try
            {
                def.ApplyCastRange(6f);
                Assert.AreEqual(6f, def.CastRange);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
