using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Duel greybox: straight center + stadium flanks
    /// (straight barracks exit → N=4 corner → straight → corner → barracks).
    /// </summary>
    public static class N2SourcePartsBuilder
    {
        public const string RootName = "_SourceParts";
        public const int PartCount = N2RoadReferenceSpec.SourcePartsCount;

        public static Transform Populate(Transform parent, MatchArenaLayout layout, Material roadMaterial)
        {
            var halfSize = layout.ArenaRadius;
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var west = layout.Slots[1].BasePosition.x < layout.Slots[0].BasePosition.x
                ? layout.Slots[1]
                : layout.Slots[0];
            var east = west.SlotIndex == layout.Slots[0].SlotIndex
                ? layout.Slots[1]
                : layout.Slots[0];

            var north = DuelPathBuilder.BuildFlankCenterline(
                west,
                east,
                northSide: true,
                halfSize,
                N2RoadReferenceSpec.FlankArcSegments);
            var south = DuelPathBuilder.BuildFlankCenterline(
                west,
                east,
                northSide: false,
                halfSize,
                N2RoadReferenceSpec.FlankArcSegments);

            RoadRibbonMesh.Create(
                root,
                "FlankArcNorth",
                north,
                MatchArenaGreyboxBuilder.RoadHeight,
                roadMaterial);
            RoadRibbonMesh.Create(
                root,
                "FlankArcSouth",
                south,
                MatchArenaGreyboxBuilder.RoadHeight,
                roadMaterial);

            CreateCenterRoad(root, halfSize, roadMaterial);
            CreateCenterArena(root, roadMaterial);
            CreateWorldBaseArenas(root, layout, roadMaterial);
            return root;
        }

        static void CreateCenterRoad(Transform root, float halfSize, Material roadMaterial)
        {
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(-halfSize, 0f, 0f),
                new Vector3(halfSize, 0f, 0f),
                roadMaterial,
                endExtension: 0f);
        }

        static void CreateCenterArena(Transform root, Material roadMaterial)
        {
            var radius = N2RoadReferenceSpec.CenterArenaHalfSize;
            var roadHeight = MatchArenaGreyboxBuilder.RoadHeight;

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "CenterArena";
            platform.transform.SetParent(root, false);
            platform.transform.localPosition = new Vector3(0f, roadHeight * 0.5f, 0f);
            platform.transform.localRotation = Quaternion.identity;
            platform.transform.localScale = new Vector3(radius * 2f, roadHeight * 0.5f, radius * 2f);

            DestroyCollider(platform.GetComponent<Collider>());
            platform.GetComponent<Renderer>().sharedMaterial = roadMaterial;
        }

        static void CreateWorldBaseArenas(Transform root, MatchArenaLayout layout, Material roadMaterial)
        {
            foreach (var slot in layout.Slots)
            {
                var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "BaseArena";
                platform.transform.SetParent(root, false);
                platform.transform.position = slot.BasePosition + slot.BaseRotation * new Vector3(
                    0f,
                    N4RoadReferenceSpec.CenterArenaPlatformY,
                    -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset);
                platform.transform.rotation = slot.BaseRotation;
                platform.transform.localScale = new Vector3(
                    MatchArenaGreyboxBuilder.BaseArenaWidth,
                    MatchArenaGreyboxBuilder.RoadHeight,
                    MatchArenaGreyboxBuilder.BaseArenaDepth);

                DestroyCollider(platform.GetComponent<Collider>());
                platform.GetComponent<Renderer>().sharedMaterial = roadMaterial;
            }
        }

        static void DestroyCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(collider);
                return;
            }
#endif
            Object.Destroy(collider);
        }
    }
}
