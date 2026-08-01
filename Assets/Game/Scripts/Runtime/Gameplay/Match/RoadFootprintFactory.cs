using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Builds XZ road footprints for N=2 / N=4 from the same geometry specs as legacy mesh parts.</summary>
    public static class RoadFootprintFactory
    {
        public static List<Vector2[]> BuildN4(MatchArenaLayout layout)
        {
            var halfSize = layout.ArenaRadius;
            var width = MatchArenaGreyboxBuilder.RoadWidth;
            var height = MatchArenaGreyboxBuilder.RoadHeight;
            var footprints = new List<Vector2[]>(32);

            AddN4PerimeterStrips(footprints, halfSize, width);
            AddCardinalSpokeStrips(footprints, halfSize, width);
            footprints.Add(RoadFootprintShapes.Disc(N4RoadReferenceSpec.CenterArenaHalfSize));
            AddCardinalSpokeFillets(footprints, halfSize, height, width);
            AddPerimeterCorners(footprints, halfSize, height, forN2: false);
            AddBaseArenas(footprints, layout);
            return footprints;
        }

        public static List<Vector2[]> BuildN2(MatchArenaLayout layout)
        {
            var halfSize = layout.ArenaRadius;
            var width = MatchArenaGreyboxBuilder.RoadWidth;
            var height = MatchArenaGreyboxBuilder.RoadHeight;
            var footprints = new List<Vector2[]>(24);

            AddN2PerimeterStrips(footprints, halfSize, width);
            AddEastWestSpokeStrips(footprints, halfSize, width);
            footprints.Add(RoadFootprintShapes.Disc(N4RoadReferenceSpec.CenterArenaHalfSize));
            AddDuelSpokeFillets(footprints, halfSize, height, width);
            AddPerimeterCorners(footprints, halfSize, height, forN2: true);
            RotateAuthoredFootprintsToLayout(footprints);
            AddBaseArenas(footprints, layout);
            return footprints;
        }

        static List<Vector2[]> RotateAuthoredFootprintsToLayout(List<Vector2[]> footprints)
        {
            for (var i = 0; i < footprints.Count; i++)
            {
                var poly = footprints[i];
                if (poly == null || poly.Length == 0)
                {
                    continue;
                }

                var rotated = new Vector2[poly.Length];
                for (var p = 0; p < poly.Length; p++)
                {
                    var world = MatchArenaGenerator.RotateAuthoredToLayout(new Vector3(poly[p].x, 0f, poly[p].y));
                    rotated[p] = new Vector2(world.x, world.z);
                }

                footprints[i] = rotated;
            }

            return footprints;
        }

        public static List<Vector2[]> BuildN3(MatchArenaLayout layout)
        {
            var width = MatchArenaGreyboxBuilder.RoadWidth;
            var height = MatchArenaGreyboxBuilder.RoadHeight;
            var n = layout.PlayerCount;
            var footprints = new List<Vector2[]>(n * 8 + 4);

            AddBaseExitStraights(footprints, layout, width);
            AddExitCurveRamps(footprints, layout, width, alongStraight: true);
            AddPerimeterStraights(footprints, layout, width);
            AddCircularSpokeStrips(footprints, layout, width);
            footprints.Add(RoadFootprintShapes.Disc(N4RoadReferenceSpec.CenterArenaHalfSize, segments: 64));
            AddCircularSpokeFillets(footprints, layout, height, width);
            AddBaseArenas(footprints, layout);
            return footprints;
        }

        /// <summary>
        /// Circular ring for N=5..8:
        /// short straight base exits → curve onto the ring → circular arcs between joins.
        /// </summary>
        public static List<Vector2[]> BuildRing(MatchArenaLayout layout)
        {
            var width = MatchArenaGreyboxBuilder.RoadWidth;
            var height = MatchArenaGreyboxBuilder.RoadHeight;
            var n = layout.PlayerCount;
            var footprints = new List<Vector2[]>(n * 8 + 4);

            AddBaseExitStraights(footprints, layout, width);
            AddExitCurveRamps(footprints, layout, width, alongStraight: false);
            AddCircularPerimeterArcs(footprints, layout, width);
            AddCircularSpokeStrips(footprints, layout, width);
            footprints.Add(RoadFootprintShapes.Disc(N4RoadReferenceSpec.CenterArenaHalfSize, segments: 64));
            AddCircularSpokeFillets(footprints, layout, height, width);
            AddBaseArenas(footprints, layout);
            return footprints;
        }

        static void AddPerimeterStraights(List<Vector2[]> footprints, MatchArenaLayout layout, float width)
        {
            var radius = layout.ArenaRadius;
            var n = layout.PlayerCount;
            for (var i = 0; i < n; i++)
            {
                var current = layout.Slots[i];
                var next = layout.Slots[(i + 1) % n];
                var from = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                    current, Game.Core.GameIds.Buildings.BarracksRight, radius);
                var to = CircularRingRoadGeometry.GetExitCurveJoinPoint(
                    next, Game.Core.GameIds.Buildings.BarracksLeft, radius);
                AddStrip(footprints, from, to, width);
            }
        }

        static void AddCircularPerimeterArcs(List<Vector2[]> footprints, MatchArenaLayout layout, float width)
        {
            var radius = layout.ArenaRadius;
            var n = layout.PlayerCount;
            var overlap = CircularRingRoadGeometry.ArcDockOverlapRadians;
            var samples = CircularRingRoadGeometry.RingArcSamplesPerSegment;

            for (var i = 0; i < n; i++)
            {
                var current = layout.Slots[i];
                var next = layout.Slots[(i + 1) % n];
                // Pure circle begins after each exit's curve join (not at the exit tip).
                var a0 = CircularRingRoadGeometry.GetExitCurveJoinAngle(
                    current, Game.Core.GameIds.Buildings.BarracksRight, radius) - overlap;
                var a1 = CircularRingRoadGeometry.GetExitCurveJoinAngle(
                    next, Game.Core.GameIds.Buildings.BarracksLeft, radius) + overlap;
                if (a1 <= a0)
                {
                    a1 += Mathf.PI * 2f;
                }

                footprints.Add(ArcStrip(radius, width, a0, a1, samples));
            }
        }

        static void AddCircularSpokeStrips(List<Vector2[]> footprints, MatchArenaLayout layout, float width)
        {
            var inner = N4RoadReferenceSpec.SpokeStripInnerRadius;
            var outer = layout.ArenaRadius - CircularRingRoadGeometry.JunctionFilletRadius;
            if (outer <= inner + 0.01f)
            {
                return;
            }

            foreach (var slot in layout.Slots)
            {
                CircularRingRoadGeometry.GetBaseFrame(slot.BasePosition, out _, out var radial, out _);
                AddStrip(footprints, radial * inner, radial * outer, width);
            }
        }

        static void AddCircularSpokeFillets(
            List<Vector2[]> footprints,
            MatchArenaLayout layout,
            float height,
            float width)
        {
            var radius = CircularRingRoadGeometry.JunctionFilletRadius;
            foreach (var slot in layout.Slots)
            {
                CircularRingRoadGeometry.GetSpokeJunction(
                    slot.BasePosition,
                    out var junction,
                    out var spokeDir,
                    out var leftDir,
                    out var rightDir);
                AddSpokeFillets(footprints, junction, spokeDir, leftDir, rightDir, radius, height, width);
            }
        }

        /// <summary>Original short L/R/C straights in base local space.</summary>
        static void AddBaseExitStraights(List<Vector2[]> footprints, MatchArenaLayout layout, float width)
        {
            var exitLen = CircularRingRoadGeometry.ExitStraightLength;
            foreach (var slot in layout.Slots)
            {
                if (!slot.BuildingLocalOffsets.TryGetValue(Game.Core.GameIds.Buildings.BarracksCenter, out var centerLocal)
                    || !slot.BuildingLocalOffsets.TryGetValue(Game.Core.GameIds.Buildings.BarracksLeft, out var leftLocal)
                    || !slot.BuildingLocalOffsets.TryGetValue(Game.Core.GameIds.Buildings.BarracksRight, out var rightLocal))
                {
                    continue;
                }

                var centerStart = slot.BasePosition + slot.BaseRotation * centerLocal;
                var centerEnd = slot.BasePosition + slot.BaseRotation * new Vector3(0f, 0f, centerLocal.z + exitLen);
                AddStrip(footprints, centerStart, centerEnd, width);

                var leftStart = slot.BasePosition + slot.BaseRotation * leftLocal;
                var leftEnd = slot.BasePosition + slot.BaseRotation * new Vector3(leftLocal.x - exitLen, 0f, leftLocal.z);
                AddStrip(footprints, leftStart, leftEnd, width);

                var rightStart = slot.BasePosition + slot.BaseRotation * rightLocal;
                var rightEnd = slot.BasePosition + slot.BaseRotation * new Vector3(rightLocal.x + exitLen, 0f, rightLocal.z);
                AddStrip(footprints, rightStart, rightEnd, width);

                AddStrip(footprints, leftStart, rightStart, width);
            }
        }

        /// <summary>Easing after each straight exit onto the perimeter.</summary>
        static void AddExitCurveRamps(
            List<Vector2[]> footprints,
            MatchArenaLayout layout,
            float width,
            bool alongStraight)
        {
            var radius = layout.ArenaRadius;
            var samples = new List<Vector3>(CircularRingRoadGeometry.ExitCurveSamples + 1);
            foreach (var slot in layout.Slots)
            {
                AddExitCurveRamp(
                    footprints, slot, Game.Core.GameIds.Buildings.BarracksLeft, layout, radius, width, alongStraight, samples);
                AddExitCurveRamp(
                    footprints, slot, Game.Core.GameIds.Buildings.BarracksRight, layout, radius, width, alongStraight, samples);
            }
        }

        static void AddExitCurveRamp(
            List<Vector2[]> footprints,
            PlayerSlotLayout slot,
            string barracksId,
            MatchArenaLayout layout,
            float ringRadius,
            float width,
            bool alongStraight,
            List<Vector3> samples)
        {
            samples.Clear();
            if (alongStraight)
            {
                CircularRingRoadGeometry.SampleExitCurve(slot, barracksId, layout, samples);
            }
            else
            {
                CircularRingRoadGeometry.SampleExitCurve(slot, barracksId, ringRadius, samples);
            }

            if (samples.Count < 2)
            {
                return;
            }

            var inDir = CircularRingRoadGeometry.GetSideExitDir(slot, barracksId);
            var outDir = alongStraight
                ? CircularRingRoadGeometry.GetExitCurveOutDirAlongStraight(slot, barracksId, layout)
                : CircularRingRoadGeometry.GetExitCurveOutDir(slot, barracksId, ringRadius);
            var poly = RoadFootprintShapes.CenteredPolylineStrip(samples, width, inDir, outDir);
            if (poly.Length >= 3)
            {
                footprints.Add(poly);
            }
        }

        static Vector2[] ArcStrip(
            float radius,
            float width,
            float startAngle,
            float endAngle,
            int samples)
        {
            var halfWidth = width * 0.5f;
            var innerRadius = radius - halfWidth;
            var outerRadius = radius + halfWidth;
            var points = new Vector2[(samples + 1) * 2];

            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var angle = Mathf.Lerp(startAngle, endAngle, t);
                points[i] = new Vector2(
                    Mathf.Cos(angle) * outerRadius,
                    Mathf.Sin(angle) * outerRadius);
            }

            for (var i = 0; i <= samples; i++)
            {
                var t = 1f - i / (float)samples;
                var angle = Mathf.Lerp(startAngle, endAngle, t);
                points[samples + 1 + i] = new Vector2(
                    Mathf.Cos(angle) * innerRadius,
                    Mathf.Sin(angle) * innerRadius);
            }

            return points;
        }

        static void AddN4PerimeterStrips(List<Vector2[]> footprints, float halfSize, float width)
        {
            var innerNeg = N4RoadReferenceSpec.PerimeterHalfStripInnerBound;
            var innerPos = N4RoadReferenceSpec.PerimeterHalfStripOuterBound;
            var cornerOuter = N4RoadReferenceSpec.GetPerimeterStripCornerOuter(halfSize);

            AddStrip(footprints, new Vector3(-cornerOuter, 0f, halfSize), new Vector3(innerNeg, 0f, halfSize), width);
            AddStrip(footprints, new Vector3(innerPos, 0f, halfSize), new Vector3(cornerOuter, 0f, halfSize), width);
            AddStrip(footprints, new Vector3(-cornerOuter, 0f, -halfSize), new Vector3(innerNeg, 0f, -halfSize), width);
            AddStrip(footprints, new Vector3(innerPos, 0f, -halfSize), new Vector3(cornerOuter, 0f, -halfSize), width);
            AddStrip(footprints, new Vector3(halfSize, 0f, -cornerOuter), new Vector3(halfSize, 0f, innerNeg), width);
            AddStrip(footprints, new Vector3(halfSize, 0f, innerPos), new Vector3(halfSize, 0f, cornerOuter), width);
            AddStrip(footprints, new Vector3(-halfSize, 0f, -cornerOuter), new Vector3(-halfSize, 0f, innerNeg), width);
            AddStrip(footprints, new Vector3(-halfSize, 0f, innerPos), new Vector3(-halfSize, 0f, cornerOuter), width);
        }

        static void AddN2PerimeterStrips(List<Vector2[]> footprints, float halfSize, float width)
        {
            var innerNeg = -N2RoadReferenceSpec.SideFlankInnerBound;
            var innerPos = N2RoadReferenceSpec.SideFlankInnerBound;
            var flankOuter = N2RoadReferenceSpec.SideFlankOuterBound;
            var northSouthEdge = N2RoadReferenceSpec.GetNorthSouthRoadEdge();
            var cornerOuterX = N4RoadReferenceSpec.GetPerimeterStripCornerOuter(halfSize);

            AddStrip(footprints, new Vector3(-cornerOuterX, 0f, northSouthEdge), new Vector3(cornerOuterX, 0f, northSouthEdge), width);
            AddStrip(footprints, new Vector3(-cornerOuterX, 0f, -northSouthEdge), new Vector3(cornerOuterX, 0f, -northSouthEdge), width);
            AddStrip(footprints, new Vector3(halfSize, 0f, innerPos), new Vector3(halfSize, 0f, flankOuter), width);
            AddStrip(footprints, new Vector3(halfSize, 0f, -flankOuter), new Vector3(halfSize, 0f, innerNeg), width);
            AddStrip(footprints, new Vector3(-halfSize, 0f, innerPos), new Vector3(-halfSize, 0f, flankOuter), width);
            AddStrip(footprints, new Vector3(-halfSize, 0f, -flankOuter), new Vector3(-halfSize, 0f, innerNeg), width);
        }

        static void AddCardinalSpokeStrips(List<Vector2[]> footprints, float halfSize, float width)
        {
            N4RoadReferenceSpec.GetPositiveZSpokeStrip(halfSize, out var from, out var to);
            AddStrip(footprints, from, to, width);
            N4RoadReferenceSpec.GetNegativeZSpokeStrip(halfSize, out from, out to);
            AddStrip(footprints, from, to, width);
            N4RoadReferenceSpec.GetPositiveXSpokeStrip(halfSize, out from, out to);
            AddStrip(footprints, from, to, width);
            N4RoadReferenceSpec.GetNegativeXSpokeStrip(halfSize, out from, out to);
            AddStrip(footprints, from, to, width);
        }

        static void AddEastWestSpokeStrips(List<Vector2[]> footprints, float halfSize, float width)
        {
            N4RoadReferenceSpec.GetPositiveXSpokeStrip(halfSize, out var from, out var to);
            AddStrip(footprints, from, to, width);
            N4RoadReferenceSpec.GetNegativeXSpokeStrip(halfSize, out from, out to);
            AddStrip(footprints, from, to, width);
        }

        static void AddCardinalSpokeFillets(List<Vector2[]> footprints, float halfSize, float height, float width)
        {
            var radius = RoadJunctionBuilder.CenterLineTurnRadius;
            AddSpokeFillets(footprints, new Vector3(0f, 0f, -halfSize), Vector3.back, Vector3.left, Vector3.right, radius, height, width);
            AddSpokeFillets(footprints, new Vector3(0f, 0f, halfSize), Vector3.forward, Vector3.left, Vector3.right, radius, height, width);
            AddSpokeFillets(footprints, new Vector3(halfSize, 0f, 0f), Vector3.right, Vector3.forward, Vector3.back, radius, height, width);
            AddSpokeFillets(footprints, new Vector3(-halfSize, 0f, 0f), Vector3.left, Vector3.forward, Vector3.back, radius, height, width);
        }

        static void AddDuelSpokeFillets(List<Vector2[]> footprints, float halfSize, float height, float width)
        {
            var radius = RoadJunctionBuilder.CenterLineTurnRadius;
            AddSpokeFillets(footprints, new Vector3(halfSize, 0f, 0f), Vector3.right, Vector3.forward, Vector3.back, radius, height, width);
            AddSpokeFillets(footprints, new Vector3(-halfSize, 0f, 0f), Vector3.left, Vector3.forward, Vector3.back, radius, height, width);
        }

        static void AddSpokeFillets(
            List<Vector2[]> footprints,
            Vector3 junction,
            Vector3 spokeDir,
            Vector3 leftPerimeterDir,
            Vector3 rightPerimeterDir,
            float radius,
            float height,
            float width)
        {
            AddFillet(footprints, junction, spokeDir, leftPerimeterDir, radius, height, width);
            AddFillet(footprints, junction, spokeDir, rightPerimeterDir, radius, height, width);
        }

        static void AddPerimeterCorners(List<Vector2[]> footprints, float halfSize, float height, bool forN2)
        {
            for (var i = 0; i < 4; i++)
            {
                var corner = forN2
                    ? N2RoadReferenceSpec.GetMapCornerArcCorner(i, halfSize)
                    : N4RoadReferenceSpec.GetMapCornerArcCorner(i, halfSize);
                var mesh = PerimeterCornerArc.BuildCornerRoadMesh(corner, turnClockwise: true, height);
                AddRibbon(footprints, mesh);
            }
        }

        static void AddBaseArenas(List<Vector2[]> footprints, MatchArenaLayout layout)
        {
            var size = new Vector3(
                MatchArenaGreyboxBuilder.BaseArenaWidth,
                MatchArenaGreyboxBuilder.RoadHeight,
                MatchArenaGreyboxBuilder.BaseArenaDepth);
            foreach (var slot in layout.Slots)
            {
                var position = slot.BasePosition + slot.BaseRotation * new Vector3(
                    0f,
                    N4RoadReferenceSpec.CenterArenaPlatformY,
                    -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
                footprints.Add(RoadFootprintShapes.OrientedRect(position, slot.BaseRotation, size));
            }
        }

        static void AddStrip(List<Vector2[]> footprints, Vector3 from, Vector3 to, float width)
        {
            var poly = RoadFootprintShapes.OrientedStrip(from, to, width);
            if (poly.Length >= 3)
            {
                footprints.Add(poly);
            }
        }

        static void AddFillet(
            List<Vector2[]> footprints,
            Vector3 corner,
            Vector3 inDir,
            Vector3 outDir,
            float radius,
            float height,
            float width)
        {
            var mesh = RoadFilletArc.BuildMesh(corner, inDir, outDir, radius, width, height);
            AddRibbon(footprints, mesh);
        }

        static void AddRibbon(List<Vector2[]> footprints, Mesh mesh)
        {
            var poly = RoadFootprintShapes.FromRibbonMesh(mesh);
            if (poly.Length >= 3)
            {
                footprints.Add(poly);
            }

            Object.DestroyImmediate(mesh);
        }
    }
}
