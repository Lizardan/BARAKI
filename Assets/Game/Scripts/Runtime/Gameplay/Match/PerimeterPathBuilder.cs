using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Square perimeter road (N=4) or circular ring (other N).</summary>
    public static class PerimeterPathBuilder
    {
        public static LanePath BuildFlankPath(
            Vector3 start,
            Vector3 end,
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            string originBarracksId,
            string destinationBarracksId,
            float halfSize,
            int playerCount,
            bool clockwise)
        {
            if (playerCount == 2)
            {
                return DuelPathBuilder.BuildFlankPath(
                    start,
                    end,
                    owner,
                    opponent,
                    halfSize,
                    clockwise);
            }

            if (playerCount == 4)
            {
                return BuildSquarePath(
                    start,
                    end,
                    owner,
                    opponent,
                    originBarracksId,
                    destinationBarracksId,
                    halfSize,
                    clockwise);
            }

            var startAngle = LaneGraphBuilder.AngleOnRing(owner.BasePosition);
            var endAngle = LaneGraphBuilder.AngleOnRing(opponent.BasePosition);
            return LaneGraphBuilder.BuildRingArcPath(start, end, startAngle, endAngle, halfSize, clockwise);
        }

        internal static LanePath BuildSquarePath(
            Vector3 start,
            Vector3 end,
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            string originBarracksId,
            string destinationBarracksId,
            float halfSize,
            bool clockwise)
        {
            var points = new List<Vector3> { N4PerimeterLaneGeometry.WithHeight(start) };

            if (BaseRoadCenterlineBuilder.IsSideBarracks(originBarracksId))
            {
                BaseRoadCenterlineBuilder.AppendFlankExitFromSideBarracks(
                    points,
                    owner,
                    originBarracksId,
                    halfSize);
            }
            else
            {
                N4RoadCenterlineBuilder.AppendBarracksToStripJoin(points, start, owner.BasePosition, halfSize);
            }

            var ownerEdge = GetEdgeIndex(owner.BasePosition, halfSize);
            var opponentEdge = GetEdgeIndex(opponent.BasePosition, halfSize);
            var edge = ownerEdge;
            var join = N4RoadCenterlineBuilder.GetStripJoinPoint(start, owner.BasePosition, halfSize);
            var travel = N4PerimeterLaneGeometry.GetTravelCoord(join, edge);

            while (edge != opponentEdge)
            {
                var corner = GetCornerAfterEdge(edge, clockwise, halfSize);
                PerimeterCornerArc.GetClockwiseEndpoints(
                    corner, out var cwEntry, out var cwExit, PerimeterCornerArc.CornerArcRadius);

                // Clockwise travel: arc goes cwEntry (current edge) → cwExit (next edge).
                // Counter-clockwise travel: arc is reversed, goes cwExit (current edge) → cwEntry (next edge).
                var arcStart = clockwise ? cwEntry : cwExit;
                var arcEnd = clockwise ? cwExit : cwEntry;

                N4PerimeterLaneGeometry.AppendStripEdgeTravel(
                    points,
                    edge,
                    travel,
                    N4PerimeterLaneGeometry.GetTravelCoord(arcStart, edge),
                    clockwise,
                    halfSize);
                PerimeterCornerArc.AppendPathWaypoints(points, corner, clockwise);

                edge = NextEdge(edge, clockwise);
                travel = N4PerimeterLaneGeometry.GetTravelCoord(arcEnd, edge);
            }

            var opponentJoin = N4PerimeterLaneGeometry.GetStripJoinPoint(end, opponent.BasePosition, halfSize);
            N4PerimeterLaneGeometry.AppendStripEdgeTravel(
                points,
                edge,
                travel,
                N4PerimeterLaneGeometry.GetTravelCoord(opponentJoin, edge),
                clockwise,
                halfSize);

            if (BaseRoadCenterlineBuilder.IsSideBarracks(destinationBarracksId))
            {
                BaseRoadCenterlineBuilder.AppendFlankEntryToSideBarracks(
                    points,
                    opponent,
                    destinationBarracksId,
                    halfSize);
            }
            else
            {
                N4RoadCenterlineBuilder.AppendStripJoinToBarracks(
                    points,
                    opponentJoin,
                    end,
                    opponent.BasePosition,
                    halfSize);
            }

            return new LanePath(points);
        }

        internal static Vector3 GetSpokeJoinPoint(Vector3 barracks, Vector3 basePosition, float halfSize)
        {
            return N4PerimeterLaneGeometry.GetStripJoinPoint(barracks, basePosition, halfSize);
        }

        internal static int GetEdgeIndex(Vector3 position, float halfSize)
        {
            if (position.z >= Mathf.Abs(position.x))
            {
                return 0;
            }

            if (position.x >= Mathf.Abs(position.z))
            {
                return 1;
            }

            if (position.z <= -Mathf.Abs(position.x))
            {
                return 2;
            }

            return 3;
        }

        internal static int NextEdge(int edge, bool clockwise)
        {
            return clockwise ? (edge + 1) & 3 : (edge + 3) & 3;
        }

        internal static Vector3 GetCornerAfterEdge(int edge, bool clockwise, float halfSize)
        {
            var h = halfSize;
            return (edge, clockwise) switch
            {
                (0, true) => new Vector3(h, 0f, h),
                (0, false) => new Vector3(-h, 0f, h),
                (1, true) => new Vector3(h, 0f, -h),
                (1, false) => new Vector3(h, 0f, h),
                (2, true) => new Vector3(-h, 0f, -h),
                (2, false) => new Vector3(h, 0f, -h),
                (3, true) => new Vector3(-h, 0f, h),
                (3, false) => new Vector3(-h, 0f, -h),
                _ => Vector3.zero,
            };
        }
    }
}
