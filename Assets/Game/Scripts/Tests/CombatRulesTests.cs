using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class CombatRulesTests
    {
        [Test]
        public void ApplyArmor_ClampsToMinimumDamage()
        {
            Assert.AreEqual(1f, CombatRules.ApplyArmor(5f, 10f));
            Assert.AreEqual(3f, CombatRules.ApplyArmor(8f, 5f));
        }

        [Test]
        public void RollDamage_StaysWithinRange()
        {
            var random = new System.Random(7);
            for (var i = 0; i < 20; i++)
            {
                var damage = CombatRules.RollDamage(8f, 12f, random);
                Assert.GreaterOrEqual(damage, 8f);
                Assert.LessOrEqual(damage, 12f);
            }
        }

        [Test]
        public void CanAttackTarget_FlyingRequiresRangedOrBetter()
        {
            Assert.IsFalse(CombatRules.CanAttackTarget(UnitRole.Melee, UnitRole.Flying));
            Assert.IsFalse(CombatRules.CanAttackTarget(UnitRole.Siege, UnitRole.Flying));
            Assert.IsTrue(CombatRules.CanAttackTarget(UnitRole.Ranged, UnitRole.Flying));
            Assert.IsTrue(CombatRules.CanAttackTarget(UnitRole.Melee, UnitRole.Melee));
        }

        [Test]
        public void ComputeKillBounty_DoublesForHero()
        {
            Assert.AreEqual(8, CombatRules.ComputeKillBounty(8));
            Assert.AreEqual(160, CombatRules.ComputeKillBounty(80, isHero: true));
        }

        [Test]
        public void GetAttackIntervalSeconds_IsReciprocalOfSpeed()
        {
            Assert.AreEqual(1f, CombatRules.GetAttackIntervalSeconds(1f), 0.0001f);
            Assert.AreEqual(0.5f, CombatRules.GetAttackIntervalSeconds(2f), 0.0001f);
        }

        [Test]
        public void GetAggroRadius_IsAtLeastEightAndScalesWithAttackRange()
        {
            var melee = new UnitCombatStats(UnitRole.Melee, 100f, 0f, 8f, 10f, 1f, 1.5f, 4f, 8);
            var ranged = new UnitCombatStats(UnitRole.Ranged, 70f, 0f, 6f, 8f, 1f, 8f, 3.5f, 6);

            Assert.AreEqual(8f, CombatRules.GetAggroRadius(melee), 0.01f);
            Assert.AreEqual(20f, CombatRules.GetAggroRadius(ranged), 0.01f);
        }
    }
}
