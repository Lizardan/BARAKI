using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Procedural N=4 road mesh: flat <c>_SourceParts</c> with 29 children.</summary>
    public static class N4SourcePartsBuilder
    {
        public const string RootName = "_SourceParts";
        public const int PartCount = N4RoadReferenceSpec.SourcePartsCount;

        public static Transform Populate(Transform parent, MatchArenaLayout layout, Material roadMaterial)
        {
            var halfSize = layout.ArenaRadius;

            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            CreatePerimeterStrips(root, halfSize, roadMaterial);
            CreateSpokeConnectorStrips(root, roadMaterial);
            CreateCenterArena(root, roadMaterial);
            RoadJunctionBuilder.CreateCardinalSpokeFillets(
                root,
                halfSize,
                MatchArenaGreyboxBuilder.RoadHeight,
                roadMaterial);
            CreatePerimeterCornerArcs(root, halfSize, roadMaterial);
            CreateWorldBaseArenas(root, layout, roadMaterial);

            return root;
        }

        static void CreatePerimeterStrips(Transform root, float halfSize, Material roadMaterial)
        {
            var innerNeg = N4RoadReferenceSpec.PerimeterHalfStripInnerBound;
            var innerPos = N4RoadReferenceSpec.PerimeterHalfStripOuterBound;
            var cornerOuter = N4RoadReferenceSpec.GetPerimeterStripCornerOuter(halfSize);

            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(-cornerOuter, 0f, halfSize),
                new Vector3(innerNeg, 0f, halfSize),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(innerPos, 0f, halfSize),
                new Vector3(cornerOuter, 0f, halfSize),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(-cornerOuter, 0f, -halfSize),
                new Vector3(innerNeg, 0f, -halfSize),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(innerPos, 0f, -halfSize),
                new Vector3(cornerOuter, 0f, -halfSize),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(halfSize, 0f, -cornerOuter),
                new Vector3(halfSize, 0f, innerNeg),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(halfSize, 0f, innerPos),
                new Vector3(halfSize, 0f, cornerOuter),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(-halfSize, 0f, -cornerOuter),
                new Vector3(-halfSize, 0f, innerNeg),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(-halfSize, 0f, innerPos),
                new Vector3(-halfSize, 0f, cornerOuter),
                roadMaterial,
                endExtension: 0f);
        }

        static void CreateSpokeConnectorStrips(Transform root, Material roadMaterial)
        {
            var center = N4RoadReferenceSpec.SpokeConnectorCenter;
            var halfLength = N4RoadReferenceSpec.SpokeConnectorHalfLength;

            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(0f, 0f, center - halfLength),
                new Vector3(0f, 0f, center + halfLength),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(0f, 0f, -center - halfLength),
                new Vector3(0f, 0f, -center + halfLength),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(center - halfLength, 0f, 0f),
                new Vector3(center + halfLength, 0f, 0f),
                roadMaterial,
                endExtension: 0f);
            MatchArenaGreyboxBuilder.CreateRoadStripWorld(
                root,
                new Vector3(-center - halfLength, 0f, 0f),
                new Vector3(-center + halfLength, 0f, 0f),
                roadMaterial,
                endExtension: 0f);
        }

        static void CreateCenterArena(Transform root, Material roadMaterial)
        {
            var radius = N4RoadReferenceSpec.CenterArenaHalfSize;
            var roadHeight = MatchArenaGreyboxBuilder.RoadHeight;

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "CenterArena";
            platform.transform.SetParent(root, false);
            platform.transform.localPosition = new Vector3(0f, 0f, 0f);
            platform.transform.localRotation = Quaternion.identity;
            platform.transform.localScale = new Vector3(radius * 2f, roadHeight * 0.5f, radius * 2f);

            DestroyCollider(platform.GetComponent<Collider>());
            platform.GetComponent<Renderer>().sharedMaterial = roadMaterial;
        }

        static void CreatePerimeterCornerArcs(Transform root, float halfSize, Material roadMaterial)
        {
            for (var i = 0; i < 4; i++)
            {
                CreatePerimeterCornerArc(
                    root,
                    N4RoadReferenceSpec.GetMapCornerArcCorner(i, halfSize),
                    roadMaterial);
            }
        }

        static void CreatePerimeterCornerArc(Transform parent, Vector3 corner, Material roadMaterial)
        {
            var fill = new GameObject("PerimeterCornerArc");
            fill.transform.SetParent(parent, false);

            var mesh = PerimeterCornerArc.BuildCornerRoadMesh(corner, turnClockwise: true, MatchArenaGreyboxBuilder.RoadHeight);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadMaterial;
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
