using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class PendingMatchReconnectRulesTests
    {
        [Test]
        public void CanShowReturnButton_WithinGrace()
        {
            var saved = DateTime.UtcNow.Ticks;
            var now = saved + TimeSpan.FromSeconds(30).Ticks;
            Assert.IsTrue(PendingMatchReconnectRules.CanShowReturnButton(true, saved, now));
            Assert.IsFalse(PendingMatchReconnectRules.CanShowReturnButton(false, saved, now));
        }

        [Test]
        public void IsExpired_AfterGrace()
        {
            var saved = DateTime.UtcNow.Ticks;
            var now = saved + TimeSpan.FromSeconds(PlayerReconnectRules.DefaultGraceSeconds + 1).Ticks;
            Assert.IsTrue(PendingMatchReconnectRules.IsExpired(saved, now));
        }

        [Test]
        public void CanAttemptRejoin_RequiresMatchingTokenSlot()
        {
            var saved = DateTime.UtcNow.Ticks;
            var now = saved + TimeSpan.FromSeconds(5).Ticks;
            var token = PlayerReconnectRules.BuildSessionToken("ROOM", 2, "player-1");
            Assert.IsTrue(PendingMatchReconnectRules.CanAttemptRejoin(
                true, "ROOM", 2, token, saved, now));
            Assert.IsFalse(PendingMatchReconnectRules.CanAttemptRejoin(
                true, "ROOM", 1, token, saved, now));
            Assert.IsFalse(PendingMatchReconnectRules.CanAttemptRejoin(
                true, "", 2, token, saved, now));
        }
    }
}
