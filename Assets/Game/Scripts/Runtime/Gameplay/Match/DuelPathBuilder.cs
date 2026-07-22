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

        /// <summary>
        /// Straight run from side barracks to the E/W flank strip join (inner bound),
        /// same idea as N=4 barracks → strip join.
        /// </summary>
        public static float SideExitStraightLength =>
            SideFlankInnerBound - BarracksLateralOffset;

        /// <summary>Local |offset| of side barracks on the base (world |Z| for duel sides).</summary>
        public const float BarracksLateralOffset = 12f;

        /// <summary>Absolute Z of the north/south perimeter edge (matches greybox).</summary>
        public static float FlankStraightAbsZ => GetNorthSouthRoadEdge();

        /// <summary>Peak |Z| of the flank ring.</summary>
        public static float SemiMinor => GetNorthSouthRoadEdge();

        public const int FlankArcSegments = 12;
        public const int PathArcSegments = 12;

        /// <summary>Matches N=4 center platform for duel greybox.</summary>
        public const float CenterArenaDiameter = N4RoadReferenceSpec.CenterArenaDiameter;

        public const float CenterArenaHalfSize = N4RoadReferenceSpec.CenterArenaHalfSize;

        /// <summary>Side flank half-strip length on E/W walls (N=4 uses 70).</summary>
        public const float SideFlankHalfStripLength = 20f;

        /// <summary>Spoke × perimeter junction; same as N=4 inner bound (±20).</summary>
        public static float SideFlankInnerBound => N4RoadReferenceSpec.PerimeterHalfStripOuterBound;

        /// <summary>Outer end of side flank strips on E/W walls (±40).</summary>
        public static float SideFlankOuterBound => SideFlankInnerBound + SideFlankHalfStripLength;

        /// <summary>N/S perimeter edge moved inward so r=25 corners meet shortened flanks.</summary>
        public static float GetNorthSouthRoadEdge() => SideFlankOuterBound + CornerRadius;

        public static Vector3 GetMapCornerArcCorner(int index, float halfSize = SemiMajor) =>
            index switch
            {
                0 => new Vector3(halfSize, 0f, GetNorthSouthRoadEdge()),
                1 => new Vector3(halfSize, 0f, -GetNorthSouthRoadEdge()),
                2 => new Vector3(-halfSize, 0f, -GetNorthSouthRoadEdge()),
                _ => new Vector3(-halfSize, 0f, GetNorthSouthRoadEdge()),
            };

        /// <summary>Single unioned RoadSurface under _SourceParts.</summary>
        public const int SourcePartsCount = 1;
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

        /// <summary>Square N=2 half-loop: west barracks → perimeter → east barracks.</summary>
        public static List<Vector3> BuildFlankCenterlineFromBarracks(
            Vector3 westBarracks,
            Vector3 eastBarracks,
            bool northSide)
        {
            westBarracks = Flat(westBarracks);
            eastBarracks = Flat(eastBarracks);

            var halfSize = N2RoadReferenceSpec.SemiMajor;
            var straight = N2RoadReferenceSpec.SideExitStraightLength;
            var westOut = northSide ? Vector3.forward : Vector3.back;
            var eastOut = northSide ? Vector3.forward : Vector3.back;

            var points = new List<Vector3>(48);
            Append(points, westBarracks);
            Append(points, westBarracks + westOut * straight);
            AppendSquarePerimeterHalf(points, northSide, halfSize);
            Append(points, eastBarracks + eastOut * straight);
            Append(points, eastBarracks);
            return points;
        }

        static void AppendSquarePerimeterHalf(List<Vector3> points, bool northSide, float halfSize)
        {
            var r = N2RoadReferenceSpec.CornerRadius;
            var inner = N2RoadReferenceSpec.SideFlankInnerBound;
            var outer = N2RoadReferenceSpec.SideFlankOuterBound;
            var edgeZ = northSide
                ? N2RoadReferenceSpec.GetNorthSouthRoadEdge()
                : -N2RoadReferenceSpec.GetNorthSouthRoadEdge();
            var westX = -halfSize;
            var eastX = halfSize;
            var turnClockwise = northSide;

            var westCorner = new Vector3(westX, 0f, edgeZ);
            var eastCorner = new Vector3(eastX, 0f, edgeZ);

            PerimeterCornerArc.GetClockwiseEndpoints(westCorner, out var westCwEntry, out var westCwExit, r);
            PerimeterCornerArc.GetClockwiseEndpoints(eastCorner, out var eastCwEntry, out var eastCwExit, r);

            var westEntry = turnClockwise ? westCwEntry : westCwExit;
            var westExit = turnClockwise ? westCwExit : westCwEntry;
            var eastEntry = turnClockwise ? eastCwEntry : eastCwExit;
            var eastExit = turnClockwise ? eastCwExit : eastCwEntry;

            Append(points, new Vector3(westX, 0f, northSide ? inner : -inner));
            Append(points, new Vector3(westX, 0f, northSide ? outer : -outer));
            Append(points, westEntry);
            PerimeterCornerArc.AppendPathWaypoints(
                points,
                westCorner,
                turnClockwise,
                N4PerimeterLaneGeometry.LaneHeight,
                r);
            AppendStraight(points, westExit, eastEntry);
            Append(points, eastEntry);
            PerimeterCornerArc.AppendPathWaypoints(
                points,
                eastCorner,
                turnClockwise,
                N4PerimeterLaneGeometry.LaneHeight,
                r);
            Append(points, eastExit);
            Append(points, new Vector3(eastX, 0f, northSide ? inner : -inner));
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
            var edge = N2RoadReferenceSpec.GetNorthSouthRoadEdge();
            return new Vector3(
                eastSide ? halfSize : -halfSize,
                0f,
                northSide ? edge : -edge);
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
