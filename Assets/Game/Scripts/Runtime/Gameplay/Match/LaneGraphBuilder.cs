using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class LaneGraphBuilder
    {
        public const float DefaultCenterArenaRadius = 20f;
        public const int RingArcSegmentsPerNeighbor = 6;

        public static LaneGraph Build(MatchArenaLayout layout, float centerArenaRadius = DefaultCenterArenaRadius)
        {
            var splines = new List<LaneSpline>(layout.Lanes.Count);
            var graph = new LaneGraph
            {
                TopologyId = layout.TopologyId,
                PlayerCount = layout.PlayerCount,
                CenterArenaRadius = centerArenaRadius,
            };

            foreach (var connection in layout.Lanes)
            {
                var owner = layout.Slots[connection.OwnerSlot];
                var opponent = layout.Slots[connection.OpponentSlot];
                var path = connection.IsCenterLane
                    ? BuildCenterPath(owner, opponent, centerArenaRadius)
                    : BuildFlankPath(owner, opponent, layout.ArenaRadius, layout.PlayerCount, connection.LaneId);

                var spline = new LaneSpline
                {
                    OwnerSlot = connection.OwnerSlot,
                    LaneId = connection.LaneId,
                    OriginBarracksId = connection.OriginBarracksId,
                    OpponentSlot = connection.OpponentSlot,
                    IsCenterLane = connection.IsCenterLane,
                    Path = path,
                };
                splines.Add(spline);
                graph.Register(spline);
            }

            graph.Lanes = splines;
            return graph;
        }

        /// <summary>Center barracks → arena entry → arena center → arena exit → opponent center barracks.</summary>
        internal static LanePath BuildCenterPath(
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            float centerArenaRadius)
        {
            var start = owner.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            var end = opponent.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            var center = new Vector3(0f, 0.15f, 0f);

            if (centerArenaRadius <= 0.01f
                || !TryGetSegmentCircleIntersections(start, end, centerArenaRadius, out var tEnter, out var tExit))
            {
                return new LanePath(new[] { WithLaneHeight(start), center, WithLaneHeight(end) });
            }

            var entry = WithLaneHeight(Vector3.Lerp(start, end, tEnter));
            var exit = WithLaneHeight(Vector3.Lerp(start, end, tExit));
            return new LanePath(new[] { WithLaneHeight(start), entry, center, exit, WithLaneHeight(end) });
        }

        /// <summary>XZ segment vs circle at origin; returns t along start→end where the segment enters and leaves.</summary>
        internal static bool TryGetSegmentCircleIntersections(
            Vector3 start,
            Vector3 end,
            float radius,
            out float tEnter,
            out float tExit)
        {
            tEnter = 0f;
            tExit = 0f;

            var delta = end - start;
            delta.y = 0f;
            var lengthSq = delta.sqrMagnitude;
            if (lengthSq < 0.0001f)
            {
                return false;
            }

            var ax = start.x;
            var az = start.z;
            var dx = delta.x;
            var dz = delta.z;
            var a = dx * dx + dz * dz;
            var b = 2f * (ax * dx + az * dz);
            var c = ax * ax + az * az - radius * radius;
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;
            }

            var sqrt = Mathf.Sqrt(discriminant);
            tEnter = (-b - sqrt) / (2f * a);
            tExit = (-b + sqrt) / (2f * a);

            if (tEnter > tExit)
            {
                (tEnter, tExit) = (tExit, tEnter);
            }

            const float margin = 0.001f;
            return tExit > margin && tEnter < 1f - margin;
        }

        /// <summary>Shared perimeter ring: left barracks CW, right barracks CCW.</summary>
        internal static LanePath BuildFlankPath(
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            float ringRadius,
            int playerCount,
            string laneId)
        {
            var originBarracksId = BaseLayoutDefinition.GetFlankOriginBarracks(laneId);
            var destinationBarracksId = BaseLayoutDefinition.GetFlankDestinationBarracks(laneId);
            var clockwise = laneId == GameIds.Lanes.Left;

            var start = owner.GetBuildingWorldPosition(originBarracksId);
            var end = opponent.GetBuildingWorldPosition(destinationBarracksId);

            return PerimeterPathBuilder.BuildFlankPath(
                start,
                end,
                owner,
                opponent,
                originBarracksId,
                destinationBarracksId,
                ringRadius,
                playerCount,
                clockwise);
        }

        internal static LanePath BuildRingArcPath(
            Vector3 start,
            Vector3 end,
            float startAngle,
            float endAngle,
            float ringRadius,
            bool clockwise)
        {
            var points = new List<Vector3> { WithLaneHeight(start) };

            for (var i = 1; i < RingArcSegmentsPerNeighbor; i++)
            {
                var t = i / (float)RingArcSegmentsPerNeighbor;
                var angle = LerpRingAngle(startAngle, endAngle, t, clockwise);
                points.Add(RingPoint(angle, ringRadius));
            }

            points.Add(WithLaneHeight(end));
            return new LanePath(points);
        }

        internal static float AngleOnRing(Vector3 position)
        {
            return Mathf.Atan2(position.z, position.x);
        }

        internal static float LerpRingAngle(float from, float to, float t, bool clockwise)
        {
            var delta = to - from;
            if (clockwise)
            {
                if (delta > 0f)
                {
                    delta -= 2f * Mathf.PI;
                }
            }
            else
            {
                if (delta < 0f)
                {
                    delta += 2f * Mathf.PI;
                }
            }

            return from + delta * t;
        }

        static Vector3 RingPoint(float angle, float radius)
        {
            return new Vector3(Mathf.Cos(angle) * radius, 0.15f, Mathf.Sin(angle) * radius);
        }

        static Vector3 WithLaneHeight(Vector3 point)
        {
            point.y = 0.15f;
            return point;
        }
    }
}
