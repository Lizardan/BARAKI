using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Piecewise-linear path through waypoints.
    /// Closed loops wrap distance so units can march forever.
    /// </summary>
    public sealed class LanePath
    {
        readonly Vector3[] _waypoints;
        readonly float[] _segmentLengths;
        readonly float[] _cumulativeLengths;

        public LanePath(IReadOnlyList<Vector3> waypoints, bool isClosedLoop = false)
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                throw new ArgumentException("Lane path requires at least 2 waypoints.", nameof(waypoints));
            }

            IsClosedLoop = isClosedLoop;
            var source = new List<Vector3>(waypoints.Count + 1);
            for (var i = 0; i < waypoints.Count; i++)
            {
                source.Add(waypoints[i]);
            }

            if (IsClosedLoop)
            {
                var first = source[0];
                var last = source[source.Count - 1];
                first.y = 0f;
                last.y = 0f;
                if ((first - last).sqrMagnitude > 0.01f)
                {
                    source.Add(source[0]);
                }
            }

            _waypoints = source.ToArray();
            _segmentLengths = new float[_waypoints.Length - 1];
            _cumulativeLengths = new float[_waypoints.Length];
            for (var i = 0; i < _segmentLengths.Length; i++)
            {
                _segmentLengths[i] = Vector3.Distance(_waypoints[i], _waypoints[i + 1]);
                _cumulativeLengths[i + 1] = _cumulativeLengths[i] + _segmentLengths[i];
            }

            TotalLength = _cumulativeLengths[^1];
        }

        public bool IsClosedLoop { get; }

        public float TotalLength { get; }

        public Vector3 Start => _waypoints[0];

        public Vector3 End => _waypoints[^1];

        public int WaypointCount => _waypoints.Length;

        public Vector3 GetWaypoint(int index) => _waypoints[index];

        public float WrapDistance(float distanceFromStart)
        {
            if (!IsClosedLoop || TotalLength <= 0.0001f)
            {
                return Mathf.Clamp(distanceFromStart, 0f, TotalLength);
            }

            var wrapped = distanceFromStart % TotalLength;
            if (wrapped < 0f)
            {
                wrapped += TotalLength;
            }

            return wrapped;
        }

        public Vector3 EvaluateNormalized(float t)
        {
            if (IsClosedLoop)
            {
                t -= Mathf.Floor(t);
                if (t < 0f)
                {
                    t += 1f;
                }

                return EvaluateDistance(t * TotalLength);
            }

            t = Mathf.Clamp01(t);
            if (t <= 0f)
            {
                return _waypoints[0];
            }

            if (t >= 1f)
            {
                return _waypoints[^1];
            }

            return EvaluateDistance(t * TotalLength);
        }

        public Vector3 EvaluateDistance(float distanceFromStart)
        {
            if (TotalLength <= 0.0001f)
            {
                return _waypoints[0];
            }

            distanceFromStart = WrapDistance(distanceFromStart);
            if (distanceFromStart <= 0f)
            {
                return _waypoints[0];
            }

            if (!IsClosedLoop && distanceFromStart >= TotalLength)
            {
                return _waypoints[^1];
            }

            for (var i = 1; i < _cumulativeLengths.Length; i++)
            {
                if (distanceFromStart > _cumulativeLengths[i])
                {
                    continue;
                }

                var segmentStart = _cumulativeLengths[i - 1];
                var segmentLength = _segmentLengths[i - 1];
                var segmentT = segmentLength > 0f
                    ? (distanceFromStart - segmentStart) / segmentLength
                    : 0f;
                return Vector3.Lerp(_waypoints[i - 1], _waypoints[i], segmentT);
            }

            return _waypoints[^1];
        }

        public Vector3 EvaluateDirection(float t)
        {
            const float delta = 0.001f;
            var a = EvaluateNormalized(t);
            var b = EvaluateNormalized(t + delta);
            var dir = b - a;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }

        public Vector3 EvaluateDirectionAtDistance(float distanceFromStart)
        {
            const float lookAhead = 2f;
            var a = EvaluateDistance(distanceFromStart - lookAhead * 0.5f);
            var b = EvaluateDistance(distanceFromStart + lookAhead);
            var dir = b - a;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : EvaluateDirection(0f);
        }

        public float ProjectDistance(Vector3 worldPosition)
        {
            worldPosition.y = 0f;
            var bestDistance = 0f;
            var bestDistSq = float.MaxValue;

            for (var i = 0; i < _segmentLengths.Length; i++)
            {
                if (!TryGetSegmentProjection(worldPosition, i, out var projectedDistance, out var distSq))
                {
                    continue;
                }

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestDistance = projectedDistance;
                }
            }

            return bestDistance;
        }

        /// <summary>Project onto the path without snapping to earlier segments (corners / fillets).</summary>
        public float ProjectDistanceForward(Vector3 worldPosition, float hintDistance, float searchRadius = 10f)
        {
            worldPosition.y = 0f;
            hintDistance = WrapDistance(hintDistance);
            var bestDistance = hintDistance;
            var bestDistSq = float.MaxValue;

            for (var i = 0; i < _segmentLengths.Length; i++)
            {
                var segmentStart = _cumulativeLengths[i];
                var segmentEnd = _cumulativeLengths[i + 1];
                if (!SegmentOverlapsForwardWindow(
                        segmentStart,
                        segmentEnd,
                        hintDistance,
                        searchRadius))
                {
                    continue;
                }

                if (!TryGetSegmentProjection(worldPosition, i, out var projectedDistance, out var distSq))
                {
                    continue;
                }

                if (!IsForwardProjection(projectedDistance, hintDistance))
                {
                    continue;
                }

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestDistance = projectedDistance;
                }
            }

            if (bestDistSq < float.MaxValue)
            {
                return PreferForwardDistance(hintDistance, bestDistance);
            }

            return PreferForwardDistance(hintDistance, ProjectDistance(worldPosition));
        }

        bool SegmentOverlapsForwardWindow(
            float segmentStart,
            float segmentEnd,
            float hintDistance,
            float searchRadius)
        {
            if (!IsClosedLoop)
            {
                var minDistance = Mathf.Max(0f, hintDistance - 1.5f);
                var maxDistance = Mathf.Min(TotalLength, hintDistance + searchRadius);
                return segmentEnd >= minDistance && segmentStart <= maxDistance;
            }

            var windowEnd = hintDistance + searchRadius;
            if (windowEnd <= TotalLength)
            {
                var minDistance = Mathf.Max(0f, hintDistance - 1.5f);
                return segmentEnd >= minDistance && segmentStart <= windowEnd;
            }

            var wrapEnd = windowEnd - TotalLength;
            return segmentEnd >= hintDistance - 1.5f || segmentStart <= wrapEnd;
        }

        bool IsForwardProjection(float projectedDistance, float hintDistance)
        {
            if (!IsClosedLoop)
            {
                return projectedDistance + 0.25f >= hintDistance - 1.5f;
            }

            var delta = projectedDistance - hintDistance;
            if (delta < -TotalLength * 0.5f)
            {
                delta += TotalLength;
            }

            return delta >= -1.5f;
        }

        float PreferForwardDistance(float hintDistance, float candidate)
        {
            if (!IsClosedLoop)
            {
                return Mathf.Max(hintDistance, candidate);
            }

            var delta = candidate - hintDistance;
            if (delta < -TotalLength * 0.5f)
            {
                delta += TotalLength;
            }

            return delta < 0f ? hintDistance : candidate;
        }

        bool TryGetSegmentProjection(
            Vector3 worldPosition,
            int segmentIndex,
            out float projectedDistance,
            out float distSq)
        {
            projectedDistance = 0f;
            distSq = float.MaxValue;

            var a = _waypoints[segmentIndex];
            var b = _waypoints[segmentIndex + 1];
            a.y = 0f;
            b.y = 0f;

            var ab = b - a;
            var ap = worldPosition - a;
            var segmentLengthSq = ab.sqrMagnitude;
            var t = segmentLengthSq > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(ap, ab) / segmentLengthSq)
                : 0f;
            var closest = a + ab * t;
            distSq = (closest - worldPosition).sqrMagnitude;
            projectedDistance = _cumulativeLengths[segmentIndex] + t * _segmentLengths[segmentIndex];
            return true;
        }
    }
}
