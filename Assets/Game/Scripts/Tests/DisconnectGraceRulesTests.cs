using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class DisconnectGraceRulesTests
    {
        [Test]
        public void LobbyDisconnect_ClearsImmediately()
        {
            Assert.IsTrue(DisconnectGraceRules.ShouldClearSlotImmediately(matchStarted: false));
            Assert.IsFalse(DisconnectGraceRules.ShouldReserveSlot(matchStarted: false));
        }

        [Test]
        public void MidMatchDisconnect_ReservesSlot()
        {
            Assert.IsFalse(DisconnectGraceRules.ShouldClearSlotImmediately(matchStarted: true));
            Assert.IsTrue(DisconnectGraceRules.ShouldReserveSlot(matchStarted: true));
        }

        [Test]
        public void EliminateAfterGrace_UsesDefaultWindow()
        {
            Assert.IsFalse(DisconnectGraceRules.ShouldEliminateAfterGrace(30f));
            Assert.IsTrue(DisconnectGraceRules.ShouldEliminateAfterGrace(
                PlayerReconnectRules.DefaultGraceSeconds));
            Assert.IsTrue(DisconnectGraceRules.ShouldEliminateAfterGrace(120f));
        }

        [Test]
        public void HostSlot_IsSlotZero()
        {
            Assert.IsTrue(DisconnectGraceRules.IsHostSlotDisconnect(0));
            Assert.IsFalse(DisconnectGraceRules.IsHostSlotDisconnect(1));
        }
    }
}
