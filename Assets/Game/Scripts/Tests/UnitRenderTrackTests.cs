using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitRenderTrackTests
    {
        [Test]
        public void TryGetPair_InterpolatesBetweenTwoSamples()
        {
            var track = new UnitRenderTrack();
            track.Add(1f, new Vector3(0f, 0f, 0f), Vector3.forward, UnitBehaviorState.Move, 0);
            track.Add(2f, new Vector3(10f, 0f, 0f), Vector3.right, UnitBehaviorState.Attack, 1);

            Assert.IsTrue(track.TryGetPair(1.5f, out var prev, out var next, out var alpha));
            Assert.AreEqual(1f, prev.TimeSeconds);
            Assert.AreEqual(2f, next.TimeSeconds);
            Assert.AreEqual(0.5f, alpha, 0.001f);
        }

        [Test]
        public void TryGetPair_BeforeFirstClampsToFirst()
        {
            var track = new UnitRenderTrack();
            track.Add(1f, new Vector3(0f, 0f, 0f), Vector3.forward, UnitBehaviorState.Move, 0);
            track.Add(2f, new Vector3(10f, 0f, 0f), Vector3.right, UnitBehaviorState.Attack, 1);

            Assert.IsTrue(track.TryGetPair(0.5f, out var prev, out _, out var alpha));
            Assert.AreEqual(0f, alpha, 0.001f);
            Assert.AreEqual(0f, prev.Position.x, 0.001f);
        }

        [Test]
        public void TryGetPair_AfterLastClampsToLast()
        {
            var track = new UnitRenderTrack();
            track.Add(1f, new Vector3(0f, 0f, 0f), Vector3.forward, UnitBehaviorState.Move, 0);
            track.Add(2f, new Vector3(10f, 0f, 0f), Vector3.right, UnitBehaviorState.Attack, 1);

            Assert.IsTrue(track.TryGetPair(3f, out _, out var next, out var alpha));
            Assert.AreEqual(0f, alpha, 0.001f);
            Assert.AreEqual(10f, next.Position.x, 0.001f);
        }

        [Test]
        public void Add_IgnoresOutOfOrderOrDuplicateTimestamps()
        {
            var track = new UnitRenderTrack();
            track.Add(1f, new Vector3(0f, 0f, 0f), Vector3.forward, UnitBehaviorState.Move, 0);
            track.Add(1f, new Vector3(99f, 0f, 0f), Vector3.forward, UnitBehaviorState.Move, 0);
            track.Add(0.5f, new Vector3(88f, 0f, 0f), Vector3.forward, UnitBehaviorState.Move, 0);

            Assert.AreEqual(1, track.SampleCount);
        }

        [Test]
        public void Add_CapsHistoryLength()
        {
            var track = new UnitRenderTrack();
            for (var i = 0; i < 20; i++)
            {
                track.Add(i, Vector3.zero, Vector3.forward, UnitBehaviorState.Move, 0);
            }

            Assert.AreEqual(8, track.SampleCount);
        }
    }
}
