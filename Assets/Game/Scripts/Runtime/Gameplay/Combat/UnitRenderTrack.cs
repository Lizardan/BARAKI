using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Immutable sample of one unit's authoritative state at a server time.</summary>
    public readonly struct UnitRenderSample
    {
        public readonly float TimeSeconds;
        public readonly Vector3 Position;
        public readonly Vector3 Facing;
        public readonly UnitBehaviorState BehaviorState;
        public readonly int AttackSwingSerial;

        public UnitRenderSample(
            float timeSeconds,
            Vector3 position,
            Vector3 facing,
            UnitBehaviorState behaviorState,
            int attackSwingSerial)
        {
            TimeSeconds = timeSeconds;
            Position = position;
            Facing = facing;
            BehaviorState = behaviorState;
            AttackSwingSerial = attackSwingSerial;
        }
    }

    /// <summary>
    /// Small history of authoritative samples per unit, used by clients to interpolate smooth
    /// presentation between 15 Hz snapshots (snapshot interpolation).
    /// </summary>
    public sealed class UnitRenderTrack
    {
        const int MaxSamples = 8;

        readonly List<UnitRenderSample> _samples = new();

        public int SampleCount => _samples.Count;

        /// <summary>Appends a sample; out-of-order/duplicate timestamps are ignored.</summary>
        public void Add(
            float timeSeconds,
            Vector3 position,
            Vector3 facing,
            UnitBehaviorState behaviorState,
            int attackSwingSerial)
        {
            if (_samples.Count > 0 && _samples[_samples.Count - 1].TimeSeconds >= timeSeconds)
            {
                return;
            }

            _samples.Add(new UnitRenderSample(timeSeconds, position, facing, behaviorState, attackSwingSerial));
            while (_samples.Count > MaxSamples)
            {
                _samples.RemoveAt(0);
            }
        }

        public void Clear() => _samples.Clear();

        /// <summary>
        /// Returns the sample pair bracketing <paramref name="renderTimeSeconds"/> and a 0..1 alpha.
        /// Before the first sample returns (first, first, 0); after the last returns (last, last, 0).
        /// </summary>
        public bool TryGetPair(float renderTimeSeconds, out UnitRenderSample prev, out UnitRenderSample next, out float alpha)
        {
            prev = default;
            next = default;
            alpha = 0f;

            var count = _samples.Count;
            if (count == 0)
            {
                return false;
            }

            if (count == 1)
            {
                prev = _samples[0];
                next = prev;
                return true;
            }

            for (var i = 1; i < count; i++)
            {
                var nextSample = _samples[i];
                if (renderTimeSeconds <= nextSample.TimeSeconds)
                {
                    var prevSample = _samples[i - 1];
                    prev = prevSample;
                    next = nextSample;
                    var span = nextSample.TimeSeconds - prevSample.TimeSeconds;
                    alpha = span > 0.0001f
                        ? Mathf.Clamp01((renderTimeSeconds - prevSample.TimeSeconds) / span)
                        : 1f;
                    return true;
                }
            }

            prev = _samples[count - 1];
            next = prev;
            return true;
        }
    }
}
