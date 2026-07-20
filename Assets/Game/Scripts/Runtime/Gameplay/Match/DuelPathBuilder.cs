using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Duel (N=2): side flanks are stadium segments —
    /// straight out of barracks → N=4 corner arc → straight → corner arc → straight into barracks.
    /// Center lane stays a pure east–west strip.
    /// </summary>
    public static class N2RoadReferenceSpec
    {
        public const float SemiMajor = MatchArenaGenerator.DefaultArenaRadius;

        /// <summary>Same corner radius as N=4 map corners.</summary>
        public const float CornerRadius = N4RoadReferenceSpec.PerimeterCornerCenterlineRadius;

        /// <summary>Straight run from side barracks before the first corner.</summary>
        public const float SideExitStraightLength = 10f;

        /// <summary>Local |offset| of side barracks on the base (world |Z| for duel sides).</summary>
        public const float BarracksLateralOffset = 12f;

        /// <summary>Absolute Z of the north/south straight between the two corner arcs.</summary>
        public static float FlankStraightAbsZ =>
            BarracksLateralOffset + SideExitStraightLength + CornerRadius;

        /// <summary>Peak |Z| of the flank (alias for tests / ring).</summary>
        public static float SemiMinor => FlankStraightAbsZ;

        public const int FlankArcSegments = 12;
        public const int PathArcSegments = 12;
        public const float CenterArenaDiameter = 44f;
        public const float CenterArenaHalfSize = CenterArenaDiameter * 0.5f;
        public const int SourcePartsCount = 6;
    }

    public static class DuelPathBuilder
    {
        public static List<Vector3> BuildFlankCenterline(
            PlayerSlotLayout westSlot,
            PlayerSlotLayout eastSlot,
            bool northSide,
            float halfSize,
            int arcSegments)
        {
            var westBarracksId = northSide ? GameIds.Buildings.BarracksLeft : GameIds.Buildings.BarracksRight;
            var eastBarracksId = northSide ? GameIds.Buildings.BarracksRight : GameIds.Buildings.BarracksLeft;

            var westStart = Flat(westSlot.GetBuildingWorldPosition(westBarracksId));
            var eastEnd = Flat(eastSlot.GetBuildingWorldPosition(eastBarracksId));
            return BuildFlankCenterlineFromBarracks(westStart, eastEnd, northSide);
        }

        /// <summary>Stadium half: barracks → straight → N4 corner → straight → corner → barracks.</summary>
        public static List<Vector3> BuildFlankCenterlineFromBarracks(
            Vector3 westBarracks,
            Vector3 eastBarracks,
            bool northSide)
        {
            westBarracks = Flat(westBarracks);
            eastBarracks = Flat(eastBarracks);

            var r = N2RoadReferenceSpec.CornerRadius;
            var straight = N2RoadReferenceSpec.SideExitStraightLength;
            var flankZ = northSide
                ? N2RoadReferenceSpec.FlankStraightAbsZ
                : -N2RoadReferenceSpec.FlankStraightAbsZ;
            var westOut = northSide ? Vector3.forward : Vector3.back;
            var eastOut = northSide ? Vector3.forward : Vector3.back;

            var westCorner = new Vector3(westBarracks.x, 0f, flankZ);
            var eastCorner = new Vector3(eastBarracks.x, 0f, flankZ);
            var turnClockwise = northSide;

            PerimeterCornerArc.GetClockwiseEndpoints(westCorner, out var westCwEntry, out var westCwExit, r);
            PerimeterCornerArc.GetClockwiseEndpoints(eastCorner, out var eastCwEntry, out var eastCwExit, r);

            var westEntry = turnClockwise ? westCwEntry : westCwExit;
            var westExit = turnClockwise ? westCwExit : westCwEntry;
            var eastEntry = turnClockwise ? eastCwEntry : eastCwExit;
            var eastExit = turnClockwise ? eastCwExit : eastCwEntry;

            var points = new List<Vector3>(48);
            Append(points, westBarracks);
            Append(points, westBarracks + westOut * straight);
            Append(points, westEntry);
            PerimeterCornerArc.AppendPathWaypoints(
                points,
                westCorner,
                turnClockwise,
                N4PerimeterLaneGeometry.LaneHeight,
                r);
            Append(points, westExit);
            AppendStraight(points, westExit, eastEntry);
            Append(points, eastEntry);
            PerimeterCornerArc.AppendPathWaypoints(
                points,
                eastCorner,
                turnClockwise,
                N4PerimeterLaneGeometry.LaneHeight,
                r);
            Append(points, eastExit);
            Append(points, eastBarracks + eastOut * straight);
            Append(points, eastBarracks);
            return points;
        }

        public static LanePath BuildFlankPath(
            Vector3 start,
            Vector3 end,
            PlayerSlotLayout owner,
            PlayerSlotLayout opponent,
            float halfSize,
            bool clockwise)
        {
            var ownerOnEast = owner.BasePosition.x >= 0f;
            var useNorth = ownerOnEast ? !clockwise : clockwise;
            var west = ownerOnEast ? opponent : owner;
            var east = ownerOnEast ? owner : opponent;

            var centerline = BuildFlankCenterline(
                west,
                east,
                useNorth,
                halfSize,
                N2RoadReferenceSpec.PathArcSegments);

            if (ownerOnEast)
            {
                centerline.Reverse();
            }

            if (centerline.Count >= 2)
            {
                centerline[0] = N4PerimeterLaneGeometry.WithHeight(start);
                centerline[^1] = N4PerimeterLaneGeometry.WithHeight(end);
            }

            return new LanePath(centerline);
        }

        public static LanePath BuildSharedFlankRing(float halfSize)
        {
            var westBarracksN = new Vector3(-halfSize, 0f, N2RoadReferenceSpec.BarracksLateralOffset);
            var eastBarracksN = new Vector3(halfSize, 0f, N2RoadReferenceSpec.BarracksLateralOffset);
            var westBarracksS = new Vector3(-halfSize, 0f, -N2RoadReferenceSpec.BarracksLateralOffset);
            var eastBarracksS = new Vector3(halfSize, 0f, -N2RoadReferenceSpec.BarracksLateralOffset);

            var north = BuildFlankCenterlineFromBarracks(westBarracksN, eastBarracksN, northSide: true);
            var south = BuildFlankCenterlineFromBarracks(westBarracksS, eastBarracksS, northSide: false);

            var closed = new List<Vector3>(north.Count + south.Count);
            closed.AddRange(north);
            for (var i = south.Count - 2; i >= 1; i--)
            {
                closed.Add(south[i]);
            }

            if (closed.Count > 0)
            {
                closed.Add(N4PerimeterLaneGeometry.WithHeight(closed[0]));
            }

            return new LanePath(closed, isClosedLoop: true);
        }

        /// <summary>North or south stadium polyline for minimap / previews.</summary>
        public static List<Vector3> SampleStadiumHalf(bool northSide, float halfSize = -1f)
        {
            halfSize = halfSize > 0f ? halfSize : N2RoadReferenceSpec.SemiMajor;
            var lateral = N2RoadReferenceSpec.BarracksLateralOffset;
            var z = northSide ? lateral : -lateral;
            return BuildFlankCenterlineFromBarracks(
                new Vector3(-halfSize, 0f, z),
                new Vector3(halfSize, 0f, z),
                northSide);
        }

        public static Vector3 GetBarracksOutwardDir(PlayerSlotLayout slot, string barracksId)
        {
            var local = barracksId == GameIds.Buildings.BarracksLeft ? Vector3.left : Vector3.right;
            var world = slot.BaseRotation * local;
            world.y = 0f;
            return world.sqrMagnitude < 0.0001f ? Vector3.forward : world.normalized;
        }

        public static Vector3 GetStraightExitPoint(PlayerSlotLayout slot, Vector3 barracksWorld, string barracksId) =>
            Flat(barracksWorld) + GetBarracksOutwardDir(slot, barracksId) * N2RoadReferenceSpec.SideExitStraightLength;

        public static Vector3 GetFlankCorner(bool northSide, bool eastSide, float halfSize = -1f)
        {
            halfSize = halfSize > 0f ? halfSize : N2RoadReferenceSpec.SemiMajor;
            var flankZ = northSide
                ? N2RoadReferenceSpec.FlankStraightAbsZ
                : -N2RoadReferenceSpec.FlankStraightAbsZ;
            return new Vector3(eastSide ? halfSize : -halfSize, 0f, flankZ);
        }

        static void AppendStraight(List<Vector3> points, Vector3 from, Vector3 to)
        {
            from = Flat(from);
            to = Flat(to);
            var distance = Vector3.Distance(from, to);
            if (distance < 0.2f)
            {
                return;
            }

            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / 20f));
            for (var i = 1; i < steps; i++)
            {
                var t = i / (float)steps;
                Append(points, Vector3.Lerp(from, to, t));
            }

            Append(points, to);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static void Append(List<Vector3> points, Vector3 next)
        {
            var elevated = N4PerimeterLaneGeometry.WithHeight(next);
            if (points.Count > 0)
            {
                var last = points[^1];
                last.y = 0f;
                if (Vector3.Distance(last, Flat(next)) < 0.2f)
                {
                    return;
                }
            }

            points.Add(elevated);
        }
    }
}
