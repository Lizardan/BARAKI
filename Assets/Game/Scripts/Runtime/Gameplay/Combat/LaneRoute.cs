using System;
using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>March waypoints for a lane. Source path is used for spawn projection and leash.</summary>
    public sealed class LaneRoute
    {
        readonly LanePath _path;
        readonly Vector3[] _marchWaypoints;

        LaneRoute(LanePath path, Vector3[] marchWaypoints)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _marchWaypoints = marchWaypoints ?? throw new ArgumentNullException(nameof(marchWaypoints));
        }

        public LanePath Path => _path;

        public float TotalLength => _path.TotalLength;

        public bool IsClosedLoop => _path.IsClosedLoop;

        public int MarchWaypointCount => _marchWaypoints.Length;

        public float WrapDistance(float distanceFromStart) => _path.WrapDistance(distanceFromStart);

        /// <summary>Keeps march progress moving forward, wrapping on closed loops.</summary>
        public float AdvanceProgress(float currentProgress, float projectedProgress)
        {
            if (!IsClosedLoop)
            {
                return Mathf.Max(currentProgress, projectedProgress);
            }

            currentProgress = WrapDistance(currentProgress);
            projectedProgress = WrapDistance(projectedProgress);
            var delta = projectedProgress - currentProgress;
            if (delta < -TotalLength * 0.5f)
            {
                delta += TotalLength;
            }

            return delta < 0f ? currentProgress : projectedProgress;
        }

        public Vector3 GetMarchWaypoint(int index)
        {
            return _marchWaypoints[Mathf.Clamp(index, 0, _marchWaypoints.Length - 1)];
        }

        public Vector3 EvaluateDistance(float distanceFromStart)
        {
            return _path.EvaluateDistance(distanceFromStart);
        }

        public Vector3 EvaluateDirectionAtDistance(float distanceFromStart)
        {
            return _path.EvaluateDirectionAtDistance(distanceFromStart);
        }

        public Vector3 ResolveSpawnPosition(float distanceAlongLane, Vector3 formationOffset)
        {
            var spine = _path.EvaluateDistance(distanceAlongLane);
            var position = spine + formationOffset;
            position.y = spine.y;
            return position;
        }

        public float ProjectDistance(Vector3 worldPosition)
        {
            return _path.ProjectDistance(worldPosition);
        }

        public float ProjectDistanceForward(Vector3 worldPosition, float hintDistance, float searchRadius = 10f)
        {
            return _path.ProjectDistanceForward(worldPosition, hintDistance, searchRadius);
        }

        public int FindMarchWaypointIndex(Vector3 worldPosition)
        {
            var distance = ProjectDistance(worldPosition);
            for (var i = 0; i < _marchWaypoints.Length; i++)
            {
                var waypointDistance = ProjectDistance(_marchWaypoints[i]);
                if (waypointDistance > distance + 0.25f)
                {
                    return i;
                }
            }

            return _marchWaypoints.Length - 1;
        }

        public static LaneRoute FromPath(LanePath path, float sampleSpacing = 10f)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            sampleSpacing = Mathf.Max(4f, sampleSpacing);
            var points = new List<Vector3> { path.Start };
            var distance = sampleSpacing;
            while (distance < path.TotalLength - 0.01f)
            {
                points.Add(path.EvaluateDistance(distance));
                distance += sampleSpacing;
            }

            if ((points[points.Count - 1] - path.End).sqrMagnitude > 0.01f)
            {
                points.Add(path.End);
            }

            return new LaneRoute(path, points.ToArray());
        }
    }
}
