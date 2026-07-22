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
            AddBaseArenas(footprints, layout);
            return footprints;
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
