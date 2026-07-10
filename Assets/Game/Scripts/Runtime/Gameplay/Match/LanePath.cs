using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Piecewise-linear path through waypoints (MVP). t=0 start, t=1 end.
    /// </summary>
    public sealed class LanePath
    {
        readonly Vector3[] _waypoints;
        readonly float[] _segmentLengths;
        readonly float[] _cumulativeLengths;

        public LanePath(IReadOnlyList<Vector3> waypoints)
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                throw new ArgumentException("Lane path requires at least 2 waypoints.", nameof(waypoints));
            }

            _waypoints = new Vector3[waypoints.Count];
            for (var i = 0; i < waypoints.Count; i++)
            {
                _waypoints[i] = waypoints[i];
            }

            _segmentLengths = new float[_waypoints.Length - 1];
            _cumulativeLengths = new float[_waypoints.Length];
            for (var i = 0; i < _segmentLengths.Length; i++)
            {
                _segmentLengths[i] = Vector3.Distance(_waypoints[i], _waypoints[i + 1]);
                _cumulativeLengths[i + 1] = _cumulativeLengths[i] + _segmentLengths[i];
            }

            TotalLength = _cumulativeLengths[^1];
        }

        public float TotalLength { get; }

        public Vector3 Start => _waypoints[0];

        public Vector3 End => _waypoints[^1];

        public int WaypointCount => _waypoints.Length;

        public Vector3 GetWaypoint(int index) => _waypoints[index];

        public Vector3 EvaluateNormalized(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f)
            {
                return _waypoints[0];
            }

            if (t >= 1f)
            {
                return _waypoints[^1];
            }

            var targetDistance = t * TotalLength;
            return EvaluateDistance(targetDistance);
        }

        public Vector3 EvaluateDistance(float distanceFromStart)
        {
            distanceFromStart = Mathf.Clamp(distanceFromStart, 0f, TotalLength);
            if (distanceFromStart <= 0f)
            {
                return _waypoints[0];
            }

            if (distanceFromStart >= TotalLength)
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
            var t0 = Mathf.Clamp01(t);
            var t1 = Mathf.Clamp01(t + delta);
            if (t1 <= t0)
            {
                t1 = Mathf.Clamp01(t - delta);
                (t0, t1) = (t1, t0);
            }

            var a = EvaluateNormalized(t0);
            var b = EvaluateNormalized(t1);
            var dir = b - a;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        }

        public Vector3 EvaluateDirectionAtDistance(float distanceFromStart)
        {
            const float lookAhead = 2f;
            distanceFromStart = Mathf.Clamp(distanceFromStart, 0f, TotalLength);
            var ahead = Mathf.Min(TotalLength, distanceFromStart + lookAhead);
            var behind = Mathf.Max(0f, distanceFromStart - lookAhead * 0.5f);

            var a = EvaluateDistance(behind);
            var b = EvaluateDistance(ahead);
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
            hintDistance = Mathf.Clamp(hintDistance, 0f, TotalLength);
            var minDistance = Mathf.Max(0f, hintDistance - 1.5f);
            var maxDistance = Mathf.Min(TotalLength, hintDistance + searchRadius);
            var bestDistance = hintDistance;
            var bestDistSq = float.MaxValue;

            for (var i = 0; i < _segmentLengths.Length; i++)
            {
                var segmentStart = _cumulativeLengths[i];
                var segmentEnd = _cumulativeLengths[i + 1];
                if (segmentEnd < minDistance || segmentStart > maxDistance)
                {
                    continue;
                }

                if (!TryGetSegmentProjection(worldPosition, i, out var projectedDistance, out var distSq))
                {
                    continue;
                }

                if (projectedDistance + 0.25f < minDistance)
                {
                    continue;
                }

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestDistance = projectedDistance;
                }
            }

            return bestDistSq < float.MaxValue
                ? Mathf.Max(hintDistance, bestDistance)
                : Mathf.Max(hintDistance, ProjectDistance(worldPosition));
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
