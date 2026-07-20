using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Center-lane march retarget: when the current enemy is eliminated,
    /// waves turn toward the next alive slot clockwise (GDD E1).
    /// Units past mid-halfway keep the old mid finish then remount a flank
    /// that does not march through their own base.
    /// </summary>
    public static class CenterMarchRetargetRules
    {
        /// <summary>Remaining path length (or world distance to end) treated as "arrived at mid finish".</summary>
        public const float RouteEndArrivalTolerance = 4f;

        /// <summary>How close a flank sample may get to own base before the arc is rejected.</summary>
        public const float OwnBasePassProximity = 28f;

        public const float FlankForwardSampleStep = 4f;

        /// <summary>
        /// First alive slot clockwise from <paramref name="eliminatedSlot"/>,
        /// skipping eliminated players and the marching owner.
        /// </summary>
        public static int? ResolveNextAliveClockwise(
            int eliminatedSlot,
            IReadOnlyList<MatchPlayerState> players,
            int ownerSlot = -1)
        {
            if (players == null || players.Count == 0)
            {
                return null;
            }

            var n = players.Count;
            if (eliminatedSlot < 0 || eliminatedSlot >= n)
            {
                return null;
            }

            for (var step = 1; step < n; step++)
            {
                var slot = (eliminatedSlot + step) % n;
                if (slot == ownerSlot || players[slot].IsEliminated)
                {
                    continue;
                }

                return slot;
            }

            return null;
        }

        public static bool HasReachedRouteEnd(float marchProgressDistance, float routeTotalLength)
        {
            if (routeTotalLength <= 0.01f)
            {
                return true;
            }

            return marchProgressDistance >= routeTotalLength - RouteEndArrivalTolerance;
        }

        public static bool HasReachedRouteEnd(
            float marchProgressDistance,
            float routeTotalLength,
            Vector3 worldPosition,
            Vector3 routeEnd)
        {
            if (HasReachedRouteEnd(marchProgressDistance, routeTotalLength))
            {
                return true;
            }

            worldPosition.y = 0f;
            routeEnd.y = 0f;
            return Vector3.Distance(worldPosition, routeEnd) <= RouteEndArrivalTolerance;
        }

        /// <summary>
        /// Distance along an open mid path to the waypoint nearest map origin (arena center).
        /// </summary>
        public static float GetCenterMeetDistance(LanePath path)
        {
            if (path == null || path.WaypointCount < 2 || path.TotalLength <= 0.01f)
            {
                return 0f;
            }

            var bestSqr = float.MaxValue;
            var bestPoint = path.Start;
            for (var i = 0; i < path.WaypointCount; i++)
            {
                var p = path.GetWaypoint(i);
                p.y = 0f;
                var sqr = p.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestPoint = path.GetWaypoint(i);
                }
            }

            return path.ProjectDistance(bestPoint);
        }

        /// <summary>
        /// Commit threshold: halfway from map-center meet to mid route end.
        /// </summary>
        public static float GetMidHalfwayCommitDistance(float meetDistance, float totalLength)
        {
            if (totalLength <= 0.01f)
            {
                return 0f;
            }

            meetDistance = Mathf.Clamp(meetDistance, 0f, totalLength);
            return meetDistance + 0.5f * (totalLength - meetDistance);
        }

        public static bool HasPassedMidHalfway(
            float marchProgressDistance,
            float meetDistance,
            float totalLength)
        {
            return marchProgressDistance >= GetMidHalfwayCommitDistance(meetDistance, totalLength);
        }

        /// <summary>
        /// Effective progress for retarget: chase/attack can leave WorldPosition ahead of
        /// <paramref name="marchProgressDistance"/> (ranged/mages), so use the farther of the two.
        /// </summary>
        public static float ResolveEffectiveMarchProgress(
            float marchProgressDistance,
            Vector3 worldPosition,
            LanePath path)
        {
            if (path == null)
            {
                return Mathf.Max(0f, marchProgressDistance);
            }

            var projected = path.ProjectDistance(worldPosition);
            return Mathf.Max(marchProgressDistance, projected);
        }

        public static bool HasPassedMidHalfway(
            float marchProgressDistance,
            Vector3 worldPosition,
            LanePath path)
        {
            if (path == null)
            {
                return false;
            }

            var progress = ResolveEffectiveMarchProgress(marchProgressDistance, worldPosition, path);
            return HasPassedMidHalfway(progress, GetCenterMeetDistance(path), path.TotalLength);
        }

        public static bool HasPassedMidHalfway(float marchProgressDistance, LanePath path)
        {
            if (path == null)
            {
                return false;
            }

            return HasPassedMidHalfway(
                marchProgressDistance,
                GetCenterMeetDistance(path),
                path.TotalLength);
        }

        /// <summary>
        /// Side lane that marches toward <paramref name="nextOpponentSlot"/> from the owner base.
        /// </summary>
        public static string ResolveFlankLaneId(
            int ownerSlot,
            int nextOpponentSlot,
            MatchArenaLayout layout)
        {
            if (layout == null || ownerSlot < 0 || ownerSlot >= layout.Slots.Count)
            {
                return GameIds.Lanes.Left;
            }

            var owner = layout.Slots[ownerSlot];
            if (nextOpponentSlot == owner.LeftOpponentSlot)
            {
                return GameIds.Lanes.Left;
            }

            if (nextOpponentSlot == owner.RightOpponentSlot)
            {
                return GameIds.Lanes.Right;
            }

            var n = layout.PlayerCount;
            var viaRight = RingStepCount(ownerSlot, nextOpponentSlot, n, clockwise: true);
            var viaLeft = RingStepCount(ownerSlot, nextOpponentSlot, n, clockwise: false);
            return viaLeft <= viaRight ? GameIds.Lanes.Left : GameIds.Lanes.Right;
        }

        /// <summary>
        /// Pick Left/Right from the unit's ring position so the forward arc reaches the next
        /// enemy without marching through the owner's base.
        /// </summary>
        public static string ResolveFlankLaneIdFromPosition(
            int ownerSlot,
            int nextOpponentSlot,
            Vector3 unitPosition,
            MatchArenaLayout layout,
            LaneRoute leftRoute,
            LaneRoute rightRoute)
        {
            if (layout == null || ownerSlot < 0 || ownerSlot >= layout.Slots.Count)
            {
                return ResolveFlankLaneId(ownerSlot, nextOpponentSlot, layout);
            }

            if (leftRoute == null || rightRoute == null)
            {
                return ResolveFlankLaneId(ownerSlot, nextOpponentSlot, layout);
            }

            var ownBase = layout.Slots[ownerSlot].BasePosition;
            var enemyMain = layout.Slots[nextOpponentSlot]
                .GetBuildingWorldPosition(GameIds.Buildings.Main);

            var leftOk = TryScoreFlankForward(
                leftRoute,
                unitPosition,
                enemyMain,
                ownBase,
                out var leftDistance,
                out var leftPassesOwnBase);
            var rightOk = TryScoreFlankForward(
                rightRoute,
                unitPosition,
                enemyMain,
                ownBase,
                out var rightDistance,
                out var rightPassesOwnBase);

            if (leftOk && rightOk)
            {
                if (!leftPassesOwnBase && rightPassesOwnBase)
                {
                    return GameIds.Lanes.Left;
                }

                if (!rightPassesOwnBase && leftPassesOwnBase)
                {
                    return GameIds.Lanes.Right;
                }

                return leftDistance <= rightDistance ? GameIds.Lanes.Left : GameIds.Lanes.Right;
            }

            if (leftOk)
            {
                return GameIds.Lanes.Left;
            }

            if (rightOk)
            {
                return GameIds.Lanes.Right;
            }

            return ResolveFlankLaneId(ownerSlot, nextOpponentSlot, layout);
        }

        /// <summary>
        /// Forward distance along a (usually closed) flank from unit toward target.
        /// Marks <paramref name="passesOwnBase"/> if any sample comes within
        /// <see cref="OwnBasePassProximity"/> of own base before the target.
        /// </summary>
        public static bool TryScoreFlankForward(
            LaneRoute route,
            Vector3 fromPosition,
            Vector3 targetPosition,
            Vector3 ownBase,
            out float forwardDistance,
            out bool passesOwnBase)
        {
            forwardDistance = float.MaxValue;
            passesOwnBase = true;
            if (route == null || route.TotalLength <= 0.01f)
            {
                return false;
            }

            var start = route.WrapDistance(route.ProjectDistance(fromPosition));
            var target = route.WrapDistance(route.ProjectDistance(targetPosition));
            forwardDistance = ForwardDistanceAlong(route, start, target);
            if (forwardDistance <= 0.01f)
            {
                passesOwnBase = false;
                return true;
            }

            ownBase.y = 0f;
            var proximitySq = OwnBasePassProximity * OwnBasePassProximity;
            passesOwnBase = false;
            var traveled = 0f;
            while (traveled < forwardDistance - 0.01f)
            {
                var sampleDist = route.WrapDistance(start + traveled);
                var sample = route.EvaluateDistance(sampleDist);
                sample.y = 0f;
                if ((sample - ownBase).sqrMagnitude <= proximitySq)
                {
                    // Ignore samples still near the unit start (unit may stand near a foreign base).
                    if (traveled > OwnBasePassProximity * 0.35f)
                    {
                        passesOwnBase = true;
                        break;
                    }
                }

                traveled += FlankForwardSampleStep;
            }

            return true;
        }

        public static bool ForwardArcPassesOwnBase(
            LaneRoute route,
            Vector3 fromPosition,
            Vector3 targetPosition,
            Vector3 ownBase)
        {
            return TryScoreFlankForward(
                       route,
                       fromPosition,
                       targetPosition,
                       ownBase,
                       out _,
                       out var passes)
                   && passes;
        }

        static float ForwardDistanceAlong(LaneRoute route, float fromDistance, float toDistance)
        {
            fromDistance = route.WrapDistance(fromDistance);
            toDistance = route.WrapDistance(toDistance);
            var delta = toDistance - fromDistance;
            if (route.IsClosedLoop && delta < 0f)
            {
                delta += route.TotalLength;
            }

            if (!route.IsClosedLoop)
            {
                delta = Mathf.Max(0f, delta);
            }

            return delta;
        }

        static int RingStepCount(int from, int to, int n, bool clockwise)
        {
            if (n <= 0)
            {
                return 0;
            }

            if (clockwise)
            {
                return (to - from + n) % n;
            }

            return (from - to + n) % n;
        }
    }
}
