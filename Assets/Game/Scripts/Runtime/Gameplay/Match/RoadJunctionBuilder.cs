using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Road routing turns: one fillet per 90° direction change.</summary>
    public static class RoadJunctionBuilder
    {
        /// <summary>Sharper turn at base main junction and spoke × perimeter (perimeter map corners stay wide).</summary>
        public static float CenterLineTurnRadius => MatchArenaGreyboxBuilder.RoadWidth * 1.5f;

        /// <summary>Tight fillet near main — long straight run from each barracks before the turn.</summary>
        public static float BaseTurnRadius => MatchArenaGreyboxBuilder.RoadWidth * 0.35f;

        public static float GetTurnArcRadius(float extent = -1f)
        {
            if (extent > 0f)
            {
                return Mathf.Min(BaseTurnRadius, extent * 0.25f);
            }

            return CenterLineTurnRadius;
        }

        public static void CreateBaseCrossRoads(
            Transform slotRoot,
            PlayerSlotLayout slot,
            float baseArenaRadius,
            float roadHeight,
            Material material)
        {
            if (!slot.BuildingLocalOffsets.TryGetValue(GameIds.Buildings.BarracksCenter, out var centerBarracks)
                || !slot.BuildingLocalOffsets.TryGetValue(GameIds.Buildings.BarracksLeft, out _)
                || !slot.BuildingLocalOffsets.TryGetValue(GameIds.Buildings.BarracksRight, out _))
            {
                return;
            }

            var extentX = Mathf.Max(
                Mathf.Abs(slot.BuildingLocalOffsets[GameIds.Buildings.BarracksLeft].x),
                Mathf.Abs(slot.BuildingLocalOffsets[GameIds.Buildings.BarracksRight].x));
            var extentZ = centerBarracks.z;
            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;
            var turnRadius = Mathf.Min(BaseTurnRadius, extentZ * 0.25f);
            var filletZ = turnRadius;
            var centerStripStart = filletZ + turnRadius;

            MatchArenaGreyboxBuilder.CreateRoadStripLocal(
                slotRoot,
                new Vector3(0f, 0f, centerStripStart),
                new Vector3(0f, 0f, extentZ),
                material,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripLocal(
                slotRoot,
                new Vector3(-extentX, 0f, 0f),
                new Vector3(extentX, 0f, 0f),
                material,
                endExtension: 0f);

            CreateBaseFillets(slotRoot, slot, roadHeight, material);

            MatchArenaGreyboxBuilder.CreateRoadCornerPatchLocal(
                slotRoot,
                new Vector3(-baseArenaRadius, 0f, -halfWidth),
                material);
            MatchArenaGreyboxBuilder.CreateRoadCornerPatchLocal(
                slotRoot,
                new Vector3(baseArenaRadius, 0f, -halfWidth),
                material);
        }

        public static void CreateBaseFillets(
            Transform slotRoot,
            PlayerSlotLayout slot,
            float roadHeight,
            Material material)
        {
            if (!slot.BuildingLocalOffsets.TryGetValue(GameIds.Buildings.BarracksCenter, out var centerBarracks)
                || !slot.BuildingLocalOffsets.TryGetValue(GameIds.Buildings.BarracksLeft, out _)
                || !slot.BuildingLocalOffsets.TryGetValue(GameIds.Buildings.BarracksRight, out _))
            {
                return;
            }

            var extentZ = centerBarracks.z;
            var halfWidth = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;
            var turnRadius = Mathf.Min(BaseTurnRadius, extentZ * 0.25f);
            var filletZ = turnRadius;

            RoadFilletArc.CreateFilletLocal(
                slotRoot,
                new Vector3(-halfWidth, 0f, filletZ),
                Vector3.back,
                Vector3.left,
                turnRadius,
                roadHeight,
                material);
            RoadFilletArc.CreateFilletLocal(
                slotRoot,
                new Vector3(halfWidth, 0f, filletZ),
                Vector3.back,
                Vector3.right,
                turnRadius,
                roadHeight,
                material);
        }

        public static void CreateCardinalSpokeFillets(
            Transform parent,
            float halfSize,
            float roadHeight,
            Material material)
        {
            var radius = CenterLineTurnRadius;

            CreateSpokeFillets(parent, new Vector3(0f, 0f, -halfSize), Vector3.back, Vector3.left, Vector3.right, radius, roadHeight, material);
            CreateSpokeFillets(parent, new Vector3(0f, 0f, halfSize), Vector3.forward, Vector3.left, Vector3.right, radius, roadHeight, material);
            CreateSpokeFillets(parent, new Vector3(halfSize, 0f, 0f), Vector3.right, Vector3.forward, Vector3.back, radius, roadHeight, material);
            CreateSpokeFillets(parent, new Vector3(-halfSize, 0f, 0f), Vector3.left, Vector3.forward, Vector3.back, radius, roadHeight, material);
        }

        /// <summary>East/West spoke × perimeter fillets for duel (N=2) map.</summary>
        public static void CreateDuelSpokeFillets(
            Transform parent,
            float halfSize,
            float roadHeight,
            Material material)
        {
            var radius = CenterLineTurnRadius;
            CreateSpokeFillets(parent, new Vector3(halfSize, 0f, 0f), Vector3.right, Vector3.forward, Vector3.back, radius, roadHeight, material);
            CreateSpokeFillets(parent, new Vector3(-halfSize, 0f, 0f), Vector3.left, Vector3.forward, Vector3.back, radius, roadHeight, material);
        }

        static void CreateSpokeFillets(
            Transform parent,
            Vector3 junction,
            Vector3 spokeDir,
            Vector3 leftPerimeterDir,
            Vector3 rightPerimeterDir,
            float radius,
            float roadHeight,
            Material material)
        {
            RoadFilletArc.CreateFillet(parent, junction, spokeDir, leftPerimeterDir, radius, roadHeight, material);
            RoadFilletArc.CreateFillet(parent, junction, spokeDir, rightPerimeterDir, radius, roadHeight, material);
        }

        public static void CreateCardinalSpokeJunctions(
            Transform parent,
            float halfSize,
            float centerArenaRadius,
            float roadHeight,
            Material material)
        {
            var radius = CenterLineTurnRadius;

            CreateSpokeJunction(parent, new Vector3(0f, 0f, -halfSize), Vector3.back, Vector3.left, Vector3.right, centerArenaRadius, radius, roadHeight, material);
            CreateSpokeJunction(parent, new Vector3(0f, 0f, halfSize), Vector3.forward, Vector3.left, Vector3.right, centerArenaRadius, radius, roadHeight, material);
            CreateSpokeJunction(parent, new Vector3(halfSize, 0f, 0f), Vector3.right, Vector3.forward, Vector3.back, centerArenaRadius, radius, roadHeight, material);
            CreateSpokeJunction(parent, new Vector3(-halfSize, 0f, 0f), Vector3.left, Vector3.forward, Vector3.back, centerArenaRadius, radius, roadHeight, material);
        }

        static void CreateSpokeJunction(
            Transform parent,
            Vector3 junction,
            Vector3 spokeDir,
            Vector3 leftPerimeterDir,
            Vector3 rightPerimeterDir,
            float centerArenaRadius,
            float radius,
            float roadHeight,
            Material material)
        {
            var spokeOrigin = junction - spokeDir * (junction.magnitude - centerArenaRadius);
            var overlap = MatchArenaGreyboxBuilder.FilletStripOverlap;
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                parent,
                spokeOrigin,
                junction - spokeDir * (radius - overlap),
                material,
                endExtension: 0f);

            RoadFilletArc.CreateFillet(parent, junction, spokeDir, leftPerimeterDir, radius, roadHeight, material);
            RoadFilletArc.CreateFillet(parent, junction, spokeDir, rightPerimeterDir, radius, roadHeight, material);
        }
    }
}
