using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BarracksManualCallRulesTests
    {
        [Test]
        public void GetGoldCost_ReturnsRoleCosts()
        {
            Assert.AreEqual(50, BarracksManualCallRules.GetGoldCost(UnitRole.Melee));
            Assert.AreEqual(70, BarracksManualCallRules.GetGoldCost(UnitRole.Ranged));
            Assert.AreEqual(80, BarracksManualCallRules.GetGoldCost(UnitRole.Caster));
            Assert.AreEqual(100, BarracksManualCallRules.GetGoldCost(UnitRole.Siege));
            Assert.AreEqual(150, BarracksManualCallRules.GetGoldCost(UnitRole.Flying));
            Assert.AreEqual(150, BarracksManualCallRules.GetGoldCost(UnitRole.Super));
        }

        [Test]
        public void GetMaxCharges_L1Composition_UsesSquadCounts()
        {
            var l1 = new TestSquadCounts(melee: 2, ranged: 1, caster: 1);

            Assert.AreEqual(2, BarracksManualCallRules.GetMaxCharges(l1, UnitRole.Melee));
            Assert.AreEqual(1, BarracksManualCallRules.GetMaxCharges(l1, UnitRole.Ranged));
            Assert.AreEqual(1, BarracksManualCallRules.GetMaxCharges(l1, UnitRole.Caster));
            Assert.AreEqual(0, BarracksManualCallRules.GetMaxCharges(l1, UnitRole.Siege));
        }

        [Test]
        public void GetMaxCharges_IntCount_ReturnsCountForRole()
        {
            Assert.AreEqual(3, BarracksManualCallRules.GetMaxCharges(UnitRole.Melee, 3));
            Assert.AreEqual(0, BarracksManualCallRules.GetMaxCharges(UnitRole.Flying, 0));
        }

        [Test]
        public void CanCall_RequiresAllConditions()
        {
            Assert.IsTrue(BarracksManualCallRules.CanCall(
                enoughGold: true,
                charges: 1,
                barracksIntact: true,
                notEliminated: true));

            Assert.IsFalse(BarracksManualCallRules.CanCall(false, 1, true, true));
            Assert.IsFalse(BarracksManualCallRules.CanCall(true, 0, true, true));
            Assert.IsFalse(BarracksManualCallRules.CanCall(true, 1, false, true));
            Assert.IsFalse(BarracksManualCallRules.CanCall(true, 1, true, false));
        }

        [Test]
        public void Initialize_FillsChargesFromComposition()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(new TestSquadCounts(melee: 2, ranged: 1, caster: 1));

            Assert.AreEqual(2, state.GetCharges(UnitRole.Melee));
            Assert.AreEqual(1, state.GetCharges(UnitRole.Ranged));
            Assert.AreEqual(1, state.GetCharges(UnitRole.Caster));
        }

        [Test]
        public void TrySpend_DecrementsChargeWhenAvailable()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(new TestSquadCounts(melee: 2));

            Assert.IsTrue(state.TrySpend(UnitRole.Melee));
            Assert.AreEqual(1, state.GetCharges(UnitRole.Melee));
            Assert.IsTrue(state.TrySpend(UnitRole.Melee));
            Assert.AreEqual(0, state.GetCharges(UnitRole.Melee));
            Assert.IsFalse(state.TrySpend(UnitRole.Melee));
        }

        [Test]
        public void Tick_RegeneratesChargeAfterRegenSeconds()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(new TestSquadCounts(melee: 1));
            state.TrySpend(UnitRole.Melee);

            state.Tick(29.9f);
            Assert.AreEqual(0, state.GetCharges(UnitRole.Melee));

            state.Tick(0.2f);
            Assert.AreEqual(1, state.GetCharges(UnitRole.Melee));
        }

        [Test]
        public void Tick_RegeneratesMultipleSpentChargesSequentially()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(new TestSquadCounts(melee: 2));
            state.TrySpend(UnitRole.Melee);
            state.Tick(15f);
            state.TrySpend(UnitRole.Melee);

            // First spent charge finishes; second must start a fresh full regen, not mid-timer.
            state.Tick(15f);
            Assert.AreEqual(1, state.GetCharges(UnitRole.Melee));
            Assert.IsTrue(state.TryGetNextRegenRemaining(UnitRole.Melee, out var remaining));
            Assert.AreEqual(BarracksManualCallRules.RegenSeconds, remaining, 0.001f);

            state.Tick(29.9f);
            Assert.AreEqual(1, state.GetCharges(UnitRole.Melee));

            state.Tick(0.2f);
            Assert.AreEqual(2, state.GetCharges(UnitRole.Melee));
            Assert.IsFalse(state.TryGetNextRegenRemaining(UnitRole.Melee, out _));
        }

        [Test]
        public void TrySpend_WhileAlreadyRegenerating_DoesNotResetActiveTimer()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(new TestSquadCounts(melee: 2));
            state.TrySpend(UnitRole.Melee);
            state.Tick(10f);
            state.TrySpend(UnitRole.Melee);

            Assert.IsTrue(state.TryGetNextRegenRemaining(UnitRole.Melee, out var remaining));
            Assert.AreEqual(BarracksManualCallRules.RegenSeconds - 10f, remaining, 0.001f);
        }

        [Test]
        public void OnLevelUp_ClampsCurrentAndGrantsIncreasedMax()
        {
            var state = new BarracksCallChargeState();
            state.Initialize(new TestSquadCounts(melee: 2, ranged: 1));
            state.TrySpend(UnitRole.Melee);

            state.OnLevelUp(new TestSquadCounts(melee: 3, ranged: 1, siege: 2));

            Assert.AreEqual(2, state.GetCharges(UnitRole.Melee));
            Assert.AreEqual(3, state.GetMaxCharges(UnitRole.Melee));
            Assert.AreEqual(1, state.GetCharges(UnitRole.Ranged));
            Assert.AreEqual(2, state.GetCharges(UnitRole.Siege));
        }

        private sealed class TestSquadCounts : ISquadCounts
        {
            private readonly int _melee;
            private readonly int _ranged;
            private readonly int _caster;
            private readonly int _siege;
            private readonly int _flying;
            private readonly int _super;

            public TestSquadCounts(
                int melee = 0,
                int ranged = 0,
                int caster = 0,
                int siege = 0,
                int flying = 0,
                int super = 0)
            {
                _melee = melee;
                _ranged = ranged;
                _caster = caster;
                _siege = siege;
                _flying = flying;
                _super = super;
            }

            public int GetCount(UnitRole role) => role switch
            {
                UnitRole.Melee => _melee,
                UnitRole.Ranged => _ranged,
                UnitRole.Caster => _caster,
                UnitRole.Siege => _siege,
                UnitRole.Flying => _flying,
                UnitRole.Super => _super,
                _ => 0,
            };
        }
    }
}
