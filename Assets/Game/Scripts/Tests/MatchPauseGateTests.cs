using System;
using Game.Gameplay.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class MatchPauseGateTests
    {
        [TearDown]
        public void TearDown()
        {
            MatchPauseGate.ResetForTests();
            Time.timeScale = 1f;
        }

        [Test]
        public void SetUserPaused_TruePausesAndFiresEvent()
        {
            var fired = false;
            MatchPauseGate.PausedChanged += OnPausedChanged;

            MatchPauseGate.SetUserPaused(true);

            Assert.IsTrue(MatchPauseGate.IsUserPaused);
            Assert.IsTrue(MatchPauseGate.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(fired);

            MatchPauseGate.PausedChanged -= OnPausedChanged;
            return;

            void OnPausedChanged() => fired = true;
        }

        [Test]
        public void SetUserPaused_FalseResumes()
        {
            MatchPauseGate.SetUserPaused(true);
            MatchPauseGate.SetUserPaused(false);

            Assert.IsFalse(MatchPauseGate.IsUserPaused);
            Assert.IsFalse(MatchPauseGate.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void SetUserPaused_NoopWhenSameValueDoesNotFire()
        {
            var fired = false;
            MatchPauseGate.SetUserPaused(true);
            MatchPauseGate.PausedChanged += OnPausedChanged;

            MatchPauseGate.SetUserPaused(true);

            Assert.IsFalse(fired);

            MatchPauseGate.PausedChanged -= OnPausedChanged;
            return;

            void OnPausedChanged() => fired = true;
        }

        [Test]
        public void Paused_IsTrueWhenMigrationOrDisconnectHoldActive()
        {
            MatchPauseGate.SetMigrationPaused(true);
            Assert.IsTrue(MatchPauseGate.IsPaused);

            MatchPauseGate.SetMigrationPaused(false);
            MatchPauseGate.SetDisconnectHoldPaused(true);
            Assert.IsTrue(MatchPauseGate.IsPaused);
        }

        [Test]
        public void ResetForTests_ClearsAllFlagsAndRestoresTimeScale()
        {
            MatchPauseGate.SetUserPaused(true);
            MatchPauseGate.SetMigrationPaused(true);
            MatchPauseGate.SetDisconnectHoldPaused(true);

            MatchPauseGate.ResetForTests();

            Assert.IsFalse(MatchPauseGate.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
