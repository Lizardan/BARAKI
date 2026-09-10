using System.Collections.Generic;
using Game.Gameplay.Match;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ArenaPairingTests
    {
        static int PickSelf(int slot) => slot + 1;

        [Test]
        public void BuildDuels_EvenCount_PairsByOrder()
        {
            var ordered = new List<int> { 4, 2, 0, 3 };
            var duels = new List<ArenaDuel>();

            ArenaPairing.BuildDuels(ordered, PickSelf, duels);

            Assert.AreEqual(2, duels.Count);
            Assert.AreEqual(4, duels[0].SlotA);
            Assert.AreEqual(2, duels[0].SlotB);
            Assert.AreEqual(0, duels[1].SlotA);
            Assert.AreEqual(3, duels[1].SlotB);
            Assert.IsTrue(duels[0].IsValid);
        }

        [Test]
        public void BuildDuels_OddCount_LeavesLastUnpaired()
        {
            var ordered = new List<int> { 4, 2, 0 };
            var duels = new List<ArenaDuel>();

            ArenaPairing.BuildDuels(ordered, PickSelf, duels);

            Assert.AreEqual(1, duels.Count);
            Assert.AreEqual(4, duels[0].SlotA);
            Assert.AreEqual(2, duels[0].SlotB);
        }

        [Test]
        public void FindChallenger_OnlyForOddCount()
        {
            Assert.AreEqual(-1, ArenaPairing.FindChallenger(new List<int> { 1, 2 }));
            Assert.AreEqual(0, ArenaPairing.FindChallenger(new List<int> { 1, 2, 0 }));
            Assert.AreEqual(3, ArenaPairing.FindChallenger(new List<int> { 4, 2, 1, 3, 0 }));
        }

        [Test]
        public void FindChallenger_NullOrEmpty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ArenaPairing.FindChallenger(null));
            Assert.AreEqual(-1, ArenaPairing.FindChallenger(new List<int>()));
        }

        [Test]
        public void BuildChallengerDuel_CarriesPicksAndFlag()
        {
            var duel = ArenaPairing.BuildChallengerDuel(2, 3, 0, 1);

            Assert.IsTrue(duel.IsChallengerDuel);
            Assert.AreEqual(2, duel.SlotA);
            Assert.AreEqual(3, duel.HeroA);
            Assert.AreEqual(0, duel.SlotB);
            Assert.AreEqual(1, duel.HeroB);
        }

        [Test]
        public void BuildDuels_UsesProvidedPicks()
        {
            var ordered = new List<int> { 1, 0 };
            var duels = new List<ArenaDuel>();
            var picks = new Dictionary<int, int> { { 1, 3 }, { 0, 2 } };

            ArenaPairing.BuildDuels(ordered, slot => picks[slot], duels);

            Assert.AreEqual(3, duels[0].HeroA);
            Assert.AreEqual(2, duels[0].HeroB);
        }

        [Test]
        public void BuildDuels_ClearsPreviousResults()
        {
            var ordered = new List<int> { 1, 0 };
            var duels = new List<ArenaDuel> { new(9, 9, 9, 9) };

            ArenaPairing.BuildDuels(ordered, PickSelf, duels);

            Assert.AreEqual(1, duels.Count);
            Assert.AreEqual(1, duels[0].SlotA);
        }
    }
}
