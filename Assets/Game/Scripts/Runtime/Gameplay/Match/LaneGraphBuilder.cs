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

        /// <summary>
        /// Center barracks → arena → opponent barracks → Main (open march toward current enemy).
        /// On enemy elimination the match retargets this path clockwise to the next alive foe.
        /// </summary>
        internal static LanePath BuildCenterPath(
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            float centerArenaRadius)
        {
            var start = owner.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            var enemyBarracks = opponent.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            var enemyMain = opponent.GetBuildingWorldPosition(GameIds.Buildings.Main);
            var center = new Vector3(0f, 0.15f, 0f);

            List<Vector3> points;
            if (centerArenaRadius <= 0.01f
                || !TryGetSegmentCircleIntersections(
                    start,
                    enemyBarracks,
                    centerArenaRadius,
                    out var tEnter,
                    out var tExit))
            {
                points = new List<Vector3>
                {
                    WithLaneHeight(start),
                    center,
                    WithLaneHeight(enemyBarracks),
                };
            }
            else
            {
                var entry = WithLaneHeight(Vector3.Lerp(start, enemyBarracks, tEnter));
                var exit = WithLaneHeight(Vector3.Lerp(start, enemyBarracks, tExit));
                points = new List<Vector3>
                {
                    WithLaneHeight(start),
                    entry,
                    center,
                    exit,
                    WithLaneHeight(enemyBarracks),
                };
            }

            points.Add(WithLaneHeight(enemyMain));
            return new LanePath(points, isClosedLoop: false);
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

        /// <summary>
        /// Shared perimeter ring: left barracks CW forever, right barracks CCW forever.
        /// </summary>
        internal static LanePath BuildFlankPath(
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            float ringRadius,
            int playerCount,
            string laneId)
        {
            var originBarracksId = BaseLayoutDefinition.GetFlankOriginBarracks(laneId);
            var clockwise = laneId == GameIds.Lanes.Left;
            var start = owner.GetBuildingWorldPosition(originBarracksId);

            if (playerCount == 2)
            {
                return BuildDuelFlankLoop(owner, opponent, ringRadius, originBarracksId, start, clockwise);
            }

            // N4+ ring is the shared perimeter strip — orient from the strip join,
            // then prepend barracks → join so spawn distance is measured from the barracks.
            var ring = PerimeterRingPathBuilder.BuildSharedFlankRing(ringRadius, playerCount);
            var join = N4RoadCenterlineBuilder.GetStripJoinPoint(start, owner.BasePosition, ringRadius);
            var oriented = OrientClosedLoop(ring, join, reverse: !clockwise);
            return EnsureN4BarracksStripEntry(oriented, start, owner.BasePosition, ringRadius);
        }

        /// <summary>Full stadium ring from live barracks positions; Left=CW, Right=CCW.</summary>
        static LanePath BuildDuelFlankLoop(
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            float halfSize,
            string originBarracksId,
            Vector3 start,
            bool clockwise)
        {
            var ownerOnEast = owner.BasePosition.x >= 0f;
            var west = ownerOnEast ? opponent : owner;
            var east = ownerOnEast ? owner : opponent;

            var north = DuelPathBuilder.BuildFlankCenterline(
                west,
                east,
                northSide: true,
                halfSize,
                N2RoadReferenceSpec.PathArcSegments);
            var south = DuelPathBuilder.BuildFlankCenterline(
                west,
                east,
                northSide: false,
                halfSize,
                N2RoadReferenceSpec.PathArcSegments);

            var closed = new List<Vector3>(north.Count + south.Count);
            closed.AddRange(north);
            for (var i = south.Count - 2; i >= 1; i--)
            {
                closed.Add(south[i]);
            }

            if (closed.Count > 0)
            {
                closed.Add(WithLaneHeight(closed[0]));
            }

            var ring = OrientClosedLoop(new LanePath(closed, isClosedLoop: true), start, reverse: !clockwise);
            return EnsureBarracksStraightEntry(ring, owner, originBarracksId, start);
        }

        /// <summary>
        /// Shared ring samples often sit on the straight exit, not the barracks pad.
        /// Force Start = barracks and keep the outward straight as the first segment.
        /// </summary>
        static LanePath EnsureBarracksStraightEntry(
            LanePath ring,
            PlayerSlotLayout owner,
            string originBarracksId,
            Vector3 start)
        {
            var open = ExtractOpenRing(ring);
            if (open.Count < 2)
            {
                return ring;
            }

            start = WithLaneHeight(start);
            var outward = DuelPathBuilder.GetBarracksOutwardDir(owner, originBarracksId);
            var exit = WithLaneHeight(Flat(start) + outward * N2RoadReferenceSpec.SideExitStraightLength);

            var startFlat = Flat(start);
            if (Vector3.Distance(Flat(open[0]), startFlat) <= 0.25f
                && Vector3.Distance(Flat(open[1]), Flat(exit)) <= 1f)
            {
                open[0] = start;
                open[1] = exit;
                return new LanePath(open, isClosedLoop: true);
            }

            var bestIndex = 0;
            var bestDistSq = float.MaxValue;
            var exitFlat = Flat(exit);
            for (var i = 0; i < open.Count; i++)
            {
                var distSq = (Flat(open[i]) - exitFlat).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            var rebuilt = new List<Vector3>(open.Count + 2) { start, exit };
            for (var i = 0; i < open.Count; i++)
            {
                var point = open[(bestIndex + i) % open.Count];
                if (i == 0 && Vector3.Distance(Flat(point), exitFlat) <= 1f)
                {
                    continue;
                }

                if (Vector3.Distance(Flat(point), startFlat) <= 0.25f)
                {
                    continue;
                }

                rebuilt.Add(point);
            }

            return new LanePath(rebuilt, isClosedLoop: true);
        }

        /// <summary>
        /// N4 shared ring is sampled on the perimeter strip. Prepend barracks → join so
        /// path distance 0 is the barracks pad (same as center / N2 flanks).
        /// </summary>
        static LanePath EnsureN4BarracksStripEntry(
            LanePath ring,
            Vector3 barracks,
            Vector3 basePosition,
            float halfSize)
        {
            var open = ExtractOpenRing(ring);
            if (open.Count < 2)
            {
                return ring;
            }

            barracks = WithLaneHeight(barracks);
            var barracksFlat = Flat(barracks);
            if (Vector3.Distance(Flat(open[0]), barracksFlat) <= 0.25f)
            {
                return new LanePath(open, isClosedLoop: true);
            }

            // Go straight to the strip join — do not route via edge junction (that walks inward).
            var join = N4RoadCenterlineBuilder.GetStripJoinPoint(barracks, basePosition, halfSize);
            var rebuilt = new List<Vector3> { barracks };
            if (Vector3.Distance(barracksFlat, Flat(join)) > 0.05f)
            {
                rebuilt.Add(join);
            }

            var joinFlat = Flat(rebuilt[^1]);
            var bestIndex = 0;
            var bestDistSq = float.MaxValue;
            for (var i = 0; i < open.Count; i++)
            {
                var distSq = (Flat(open[i]) - joinFlat).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            for (var i = 0; i < open.Count; i++)
            {
                var point = open[(bestIndex + i) % open.Count];
                if (i == 0 && Vector3.Distance(Flat(point), joinFlat) <= 1f)
                {
                    continue;
                }

                if (Vector3.Distance(Flat(point), barracksFlat) <= 0.25f)
                {
                    continue;
                }

                rebuilt.Add(point);
            }

            return new LanePath(rebuilt, isClosedLoop: true);
        }

        static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        internal static LanePath OrientClosedLoop(LanePath source, Vector3 startNear, bool reverse)
        {
            var oriented = OrientClosedLoopPoints(source, startNear, reverse);
            return new LanePath(oriented, isClosedLoop: true);
        }

        internal static List<Vector3> OrientClosedLoopPoints(LanePath source, Vector3 startNear, bool reverse)
        {
            var open = ExtractOpenRing(source);
            if (open.Count < 2)
            {
                return open;
            }

            // Reverse direction first, then rotate so the path still begins at the barracks.
            if (reverse)
            {
                open.Reverse();
            }

            var bestIndex = 0;
            var bestDistSq = float.MaxValue;
            startNear.y = 0f;
            for (var i = 0; i < open.Count; i++)
            {
                var p = open[i];
                p.y = 0f;
                var distSq = (p - startNear).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            var rotated = new List<Vector3>(open.Count);
            for (var i = 0; i < open.Count; i++)
            {
                rotated.Add(open[(bestIndex + i) % open.Count]);
            }

            return rotated;
        }

        static List<Vector3> ExtractOpenRing(LanePath source)
        {
            var count = source.WaypointCount;
            if (count >= 2)
            {
                var first = source.GetWaypoint(0);
                var last = source.GetWaypoint(count - 1);
                first.y = 0f;
                last.y = 0f;
                if ((first - last).sqrMagnitude <= 0.01f)
                {
                    count--;
                }
            }

            var open = new List<Vector3>(count);
            for (var i = 0; i < count; i++)
            {
                open.Add(source.GetWaypoint(i));
            }

            return open;
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
