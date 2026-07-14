using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class MatchArenaGreyboxBuilder
    {
        public const float RoadWidth = 20f;
        public const float RoadHeight = 0.08f;
        public const float DefaultBaseArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius;

        /// <summary>Hand-tuned N=4 perimeter half-edge strip length (prefab: 70).</summary>
        public const float PerimeterHalfStripLength = 70f;

        /// <summary>Hand-tuned N=4 distance from map center to half-edge strip center (prefab: 55).</summary>
        public const float PerimeterHalfStripCenter = 55f;

        /// <summary>Hand-tuned base pad width along local X (prefab: 40).</summary>
        public const float BaseArenaWidth = DefaultBaseArenaRadius * 2f;

        /// <summary>Hand-tuned base pad depth toward map edge (prefab: 30).</summary>
        public const float BaseArenaDepth = 30f;

        /// <summary>Hand-tuned base pad shift toward the outer map edge in slot local space (prefab: 5).</summary>
        public const float BaseArenaOutwardOffset = 5f;

        /// <summary>Hand-tuned perimeter corner centerline radius (ArenaRoads_N4: 120 − 95 = 25).</summary>
        public const float PerimeterCornerArcRadius = N4RoadReferenceSpec.PerimeterCornerCenterlineRadius;

        const float LaneLineWidthCenter = 0.5f;
        const float LaneLineWidthFlank = 0.35f;
        const float LaneLineHeight = N4PerimeterLaneGeometry.LaneHeight;
        const float RoadStripCornerOverlap = 1f;
        internal const float FilletStripOverlap = 0.5f;

        public static MatchArenaGreyboxSpec CreateSpec(MatchArenaLayout layout, LaneGraph graph)
        {
            var laneLineCount = layout.PlayerCount + 1;

            return new MatchArenaGreyboxSpec(
                layout.PlayerCount,
                layout.PlayerCount * BaseLayoutDefinition.BuildingsPerBase,
                laneLineCount,
                layout.PlayerCount is 2 or 4 ? 4 : MatchArenaGreyboxSpec.LegacyCenterRingSegments);
        }

        /// <summary>Map roads and per-base road geometry (no buildings or lane markers).</summary>
        public static void PopulateRoadPrefabContent(
            Transform root,
            MatchArenaLayout layout,
            LaneGraph graph,
            Material roadMaterial = null)
        {
            PopulateRoadNetwork(root, layout, graph, roadMaterial);
        }

        public static void Populate(Transform root, MatchArenaLayout layout, LaneGraph graph)
        {
            var basesRoot = PopulateRoadNetwork(root, layout, graph);

            foreach (var slot in layout.Slots)
            {
                var slotRoot = basesRoot.Find($"Player_{slot.SlotIndex}");
                if (slotRoot == null)
                {
                    continue;
                }

                foreach (var pair in slot.BuildingLocalOffsets)
                {
                    CreateBuildingMarker(slotRoot, pair.Key, pair.Value);
                }
            }

            CreateLaneMarkers(root, layout, graph);
        }

        static void CreateLaneMarkers(Transform root, MatchArenaLayout layout, LaneGraph graph)
        {
            var lanesRoot = CreateChild(root, "LaneMarkers");
            CreateSharedFlankRingLine(lanesRoot, layout.ArenaRadius, layout.PlayerCount);

            foreach (var lane in graph.Lanes)
            {
                if (!lane.IsCenterLane)
                {
                    continue;
                }

                if (layout.PlayerCount == 4 && lane.OwnerSlot >= lane.OpponentSlot)
                {
                    continue;
                }

                CreateCenterLaneLine(lanesRoot, lane);
            }
        }

        static void CreateCenterLaneLine(Transform lanesRoot, LaneSpline lane)
        {
            CreateLaneLineFromPath(
                lanesRoot,
                $"P{lane.OwnerSlot}_{lane.LaneId}",
                ExtractCenterDisplayPath(lane.Path),
                LaneLineWidthCenter,
                GetLaneColor(lane.LaneId));
        }

        /// <summary>Full straight spoke: barracks → center arena → opponent barracks.</summary>
        static LanePath ExtractCenterDisplayPath(LanePath path)
        {
            if (path.WaypointCount >= 5)
            {
                return new LanePath(new[]
                {
                    path.GetWaypoint(0),
                    path.GetWaypoint(2),
                    path.GetWaypoint(4),
                });
            }

            if (path.WaypointCount >= 3)
            {
                return new LanePath(new[]
                {
                    path.GetWaypoint(0),
                    path.GetWaypoint(1),
                    path.GetWaypoint(2),
                });
            }

            return path;
        }

        static Transform PopulateRoadNetwork(
            Transform root,
            MatchArenaLayout layout,
            LaneGraph graph,
            Material roadMaterial = null)
        {
            roadMaterial ??= CreateRoadMaterial(new Color(0.18f, 0.2f, 0.24f));

            if (layout.PlayerCount == 4)
            {
                N4SourcePartsBuilder.Populate(root, layout, roadMaterial);
            }
            else if (layout.PlayerCount == 2)
            {
                N2SourcePartsBuilder.Populate(root, layout, roadMaterial);
            }
            else
            {
                var roadsRoot = CreateChild(root, "Roads");
                CreatePerimeterRoads(roadsRoot, layout.ArenaRadius, layout.PlayerCount, roadMaterial);
                CreateCenterSpokes(roadsRoot, layout, graph.CenterArenaRadius, roadMaterial);
                CreateCenterArena(roadsRoot, graph.CenterArenaRadius, layout.ArenaRadius, layout.PlayerCount, roadMaterial);
            }

            var basesRoot = CreateChild(root, "Bases");
            foreach (var slot in layout.Slots)
            {
                var slotRoot = CreateChild(basesRoot, $"Player_{slot.SlotIndex}");
                slotRoot.position = slot.BasePosition;
                slotRoot.rotation = slot.BaseRotation;

                if (layout.PlayerCount is not 2 and not 4)
                {
                    PopulateBaseRoads(slotRoot, slot, roadMaterial);
                }
            }

            return basesRoot;
        }

        static void CreatePerimeterRoads(
            Transform roadsRoot,
            float halfSize,
            int playerCount,
            Material roadMaterial)
        {
            if (playerCount == 4)
            {
                CreatePerimeterRoadsN4(roadsRoot, halfSize, roadMaterial);
            }
            else
            {
                const int segments = 32;
                for (var i = 0; i < segments; i++)
                {
                    var a0 = 2f * Mathf.PI * i / segments;
                    var a1 = 2f * Mathf.PI * (i + 1) / segments;
                    var p0 = new Vector3(Mathf.Cos(a0) * halfSize, 0f, Mathf.Sin(a0) * halfSize);
                    var p1 = new Vector3(Mathf.Cos(a1) * halfSize, 0f, Mathf.Sin(a1) * halfSize);
                    CreateRoadStrip(roadsRoot, p0, p1, roadMaterial);
                    CreateRoadCornerPatch(roadsRoot, p0, roadMaterial);
                }
            }
        }

        static void CreatePerimeterRoadsN4(Transform roadsRoot, float halfSize, Material roadMaterial)
        {
            var h = halfSize;
            var innerNeg = N4RoadReferenceSpec.PerimeterHalfStripInnerBound;
            var innerPos = N4RoadReferenceSpec.PerimeterHalfStripOuterBound;
            var cornerOuter = N4RoadReferenceSpec.GetPerimeterStripCornerOuter(h);

            CreateRoadStrip(
                roadsRoot,
                new Vector3(-cornerOuter, 0f, h),
                new Vector3(innerNeg, 0f, h),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(innerPos, 0f, h),
                new Vector3(cornerOuter, 0f, h),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(-cornerOuter, 0f, -h),
                new Vector3(innerNeg, 0f, -h),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(innerPos, 0f, -h),
                new Vector3(cornerOuter, 0f, -h),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(h, 0f, -cornerOuter),
                new Vector3(h, 0f, innerNeg),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(h, 0f, innerPos),
                new Vector3(h, 0f, cornerOuter),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(-h, 0f, -cornerOuter),
                new Vector3(-h, 0f, innerNeg),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(-h, 0f, innerPos),
                new Vector3(-h, 0f, cornerOuter),
                roadMaterial,
                endExtension: 0f);

        }

        static void CreateCardinalSpokeConnectors(Transform roadsRoot, Material roadMaterial)
        {
            var center = N4RoadReferenceSpec.SpokeConnectorCenter;
            var halfLength = N4RoadReferenceSpec.SpokeConnectorHalfLength;

            CreateRoadStrip(
                roadsRoot,
                new Vector3(0f, 0f, center - halfLength),
                new Vector3(0f, 0f, center + halfLength),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(0f, 0f, -center - halfLength),
                new Vector3(0f, 0f, -center + halfLength),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(center - halfLength, 0f, 0f),
                new Vector3(center + halfLength, 0f, 0f),
                roadMaterial,
                endExtension: 0f);
            CreateRoadStrip(
                roadsRoot,
                new Vector3(-center - halfLength, 0f, 0f),
                new Vector3(-center + halfLength, 0f, 0f),
                roadMaterial,
                endExtension: 0f);
        }

        static void CreateCenterSpokes(
            Transform roadsRoot,
            MatchArenaLayout layout,
            float centerArenaRadius,
            Material roadMaterial)
        {
            foreach (var slot in layout.Slots)
            {
                var toBase = slot.BasePosition;
                toBase.y = 0f;
                if (toBase.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                var dir = toBase.normalized;
                var inner = dir * centerArenaRadius;
                var outer = dir * layout.ArenaRadius;
                CreateRoadStrip(roadsRoot, inner, outer, roadMaterial, 0f);
            }
        }

        static void CreateCenterArena(
            Transform roadsRoot,
            float radius,
            float arenaHalfSize,
            int playerCount,
            Material roadMaterial)
        {
            if (playerCount == 4)
            {
                var group = CreateChild(roadsRoot, "CenterArena");
                CreateCenterArenaPlatform(group, roadMaterial);

                for (var i = 0; i < 4; i++)
                {
                    CreatePerimeterCornerArcMesh(group, N4RoadReferenceSpec.GetMapCornerArcCorner(i, arenaHalfSize), roadMaterial);
                }

                return;
            }

            CreateArenaPlatform(
                roadsRoot,
                "CenterArena",
                radius,
                useSquare: false,
                roadMaterial,
                localSpace: false);
        }

        static void CreateCenterArenaPlatform(Transform parent, Material roadMaterial)
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Platform";
            platform.transform.SetParent(parent, false);
            platform.transform.localPosition = new Vector3(0f, N4RoadReferenceSpec.CenterArenaPlatformY, 0f);
            platform.transform.localRotation = Quaternion.identity;
            var size = N4RoadReferenceSpec.CenterArenaHalfSize * 2f;
            platform.transform.localScale = new Vector3(size, RoadHeight, size);

            DestroyCollider(platform.GetComponent<Collider>());
            platform.GetComponent<Renderer>().sharedMaterial = roadMaterial;
        }

        static void CreatePerimeterCornerArcMesh(Transform parent, Vector3 corner, Material roadMaterial)
        {
            var fill = new GameObject("PerimeterCornerArc");
            fill.transform.SetParent(parent, false);

            var mesh = PerimeterCornerArc.BuildCornerRoadMesh(corner, turnClockwise: true, RoadHeight);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadMaterial;
        }

        static void CreateArenaPlatform(
            Transform parent,
            string name,
            float radius,
            bool useSquare,
            Material roadMaterial,
            bool localSpace)
        {
            var platform = GameObject.CreatePrimitive(useSquare ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            platform.name = name;
            platform.transform.SetParent(parent, false);

            var position = new Vector3(0f, RoadHeight * 0.5f, 0f);
            if (localSpace)
            {
                platform.transform.localPosition = position;
                platform.transform.localRotation = Quaternion.identity;
            }
            else
            {
                platform.transform.position = position;
            }

            if (useSquare)
            {
                var size = radius * 2f;
                platform.transform.localScale = new Vector3(size, RoadHeight, size);
            }
            else
            {
                platform.transform.localScale = new Vector3(radius * 2f, RoadHeight * 0.5f, radius * 2f);
            }

            DestroyCollider(platform.GetComponent<Collider>());

            platform.GetComponent<Renderer>().sharedMaterial = roadMaterial;
        }

        public static void CreateRoadStripWorld(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float endExtension = -1f)
        {
            CreateRoadStrip(parent, from, to, material, endExtension, localSpace: false);
        }

        public static void CreateRoadStripLocal(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float endExtension = -1f)
        {
            CreateRoadStrip(parent, from, to, material, endExtension, localSpace: true);
        }

        static void CreateRoadStrip(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float endExtension = -1f,
            bool localSpace = false)
        {
            if (endExtension < 0f)
            {
                endExtension = RoadWidth * RoadStripCornerOverlap;
            }

            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "RoadStrip";
            strip.transform.SetParent(parent, false);

            var delta = to - from;
            var length = delta.magnitude;
            var mid = (from + to) * 0.5f;
            mid.y = RoadHeight * 0.5f;

            if (localSpace)
            {
                strip.transform.localPosition = mid;
                strip.transform.localRotation = length > 0.001f
                    ? Quaternion.LookRotation(delta.normalized, Vector3.up)
                    : Quaternion.identity;
            }
            else
            {
                strip.transform.position = mid;
                strip.transform.rotation = length > 0.001f
                    ? Quaternion.LookRotation(delta.normalized, Vector3.up)
                    : Quaternion.identity;
            }

            strip.transform.localScale = new Vector3(RoadWidth, RoadHeight, length + endExtension);

            DestroyCollider(strip.GetComponent<Collider>());

            strip.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static void CreatePerimeterCornerArc(Transform parent, Vector3 corner, Material roadMaterial)
        {
            CreateRoadCornerArc(parent, corner, roadMaterial);
        }

        static void CreateRoadCornerArc(Transform parent, Vector3 corner, Material roadMaterial)
        {
            var fill = new GameObject("RoadCornerArc");
            fill.transform.SetParent(parent, false);

            var mesh = PerimeterCornerArc.BuildCornerRoadMesh(corner, turnClockwise: true, RoadHeight);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadMaterial;
        }

        static void CreateRoadCornerPatch(Transform parent, Vector3 corner, Material material)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = "RoadCorner";
            patch.transform.SetParent(parent, false);
            patch.transform.position = new Vector3(corner.x, RoadHeight * 0.5f, corner.z);
            patch.transform.localScale = new Vector3(RoadWidth, RoadHeight, RoadWidth);

            DestroyCollider(patch.GetComponent<Collider>());

            patch.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static void CreateRoadCornerPatchLocal(Transform parent, Vector3 localCorner, Material material)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = "RoadCorner";
            patch.transform.SetParent(parent, false);
            patch.transform.localPosition = new Vector3(localCorner.x, RoadHeight * 0.5f, localCorner.z);
            patch.transform.localRotation = Quaternion.identity;
            patch.transform.localScale = new Vector3(RoadWidth, RoadHeight, RoadWidth);

            DestroyCollider(patch.GetComponent<Collider>());

            patch.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void PopulateBaseRoads(Transform slotRoot, PlayerSlotLayout slot, Material roadMaterial)
        {
            CreateBaseArenaPlatform(slotRoot, roadMaterial);
            RoadJunctionBuilder.CreateBaseCrossRoads(
                slotRoot,
                slot,
                DefaultBaseArenaRadius,
                RoadHeight,
                roadMaterial);
        }

        static void CreateBaseArenaPlatform(Transform slotRoot, Material roadMaterial)
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "BaseArena";
            platform.transform.SetParent(slotRoot, false);
            platform.transform.localPosition = new Vector3(0f, RoadHeight * 0.5f, -BaseArenaOutwardOffset);
            platform.transform.localRotation = Quaternion.identity;
            platform.transform.localScale = new Vector3(BaseArenaWidth, RoadHeight, BaseArenaDepth);

            DestroyCollider(platform.GetComponent<Collider>());
            platform.GetComponent<Renderer>().sharedMaterial = roadMaterial;
        }

        static void CreateBuildingMarker(Transform slotRoot, string buildingId, Vector3 localPosition)
        {
            var marker = GameObject.CreatePrimitive(GetPrimitive(buildingId));
            marker.name = buildingId;
            marker.transform.SetParent(slotRoot, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = GetScale(buildingId);

            DestroyCollider(marker.GetComponent<Collider>());

            ApplyColor(marker, GetColor(buildingId));
        }

        static void CreateSharedFlankRingLine(Transform lanesRoot, float ringRadius, int playerCount)
        {
            var path = playerCount == 2
                ? DuelPathBuilder.BuildSharedFlankRing(ringRadius)
                : playerCount == 4
                    ? N4RoadCenterlineBuilder.BuildSharedFlankRing(ringRadius)
                    : PerimeterRingPathBuilder.BuildSharedFlankRing(ringRadius, playerCount);
            CreateLaneLineFromPath(lanesRoot, "SharedFlankRing", path, LaneLineWidthFlank, GetSharedFlankRingColor(), loop: true);
        }

        static void CreateLaneLine(Transform lanesRoot, LaneSpline lane)
        {
            CreateLaneLineFromPath(
                lanesRoot,
                $"P{lane.OwnerSlot}_{lane.LaneId}",
                lane.Path,
                lane.IsCenterLane ? LaneLineWidthCenter : LaneLineWidthFlank,
                GetLaneColor(lane.LaneId));
        }

        static void CreateLaneLineFromPath(
            Transform lanesRoot,
            string name,
            LanePath path,
            float width,
            Color color,
            bool loop = false)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(lanesRoot, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = width;
            line.positionCount = path.WaypointCount;
            for (var i = 0; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                point.y = LaneLineHeight;
                line.SetPosition(i, point);
            }

            var lineMaterial = CreateLineMaterial(color);
            if (lineMaterial != null)
            {
                line.material = lineMaterial;
            }

            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static PrimitiveType GetPrimitive(string buildingId) => buildingId switch
        {
            GameIds.Buildings.Main => PrimitiveType.Sphere,
            _ when buildingId.StartsWith("BUILDING_TOWER") => PrimitiveType.Cylinder,
            _ when buildingId.StartsWith("BUILDING_BARRACKS") => PrimitiveType.Cube,
            _ => PrimitiveType.Cube,
        };

        static Vector3 GetScale(string buildingId) => buildingId switch
        {
            GameIds.Buildings.Main => new Vector3(3.5f, 2f, 3.5f),
            GameIds.Buildings.TowerNw or GameIds.Buildings.TowerNe or GameIds.Buildings.TowerSw or GameIds.Buildings.TowerSe
                => new Vector3(2f, 4f, 2f),
            GameIds.Buildings.BarracksCenter => new Vector3(4.5f, 1.8f, 4.5f),
            GameIds.Buildings.BarracksLeft or GameIds.Buildings.BarracksRight => new Vector3(4.5f, 1.8f, 4.5f),
            _ => Vector3.one,
        };

        static Color GetColor(string buildingId) => buildingId switch
        {
            GameIds.Buildings.Main => new Color(0.88f, 0.22f, 0.18f),
            _ when buildingId.StartsWith("BUILDING_TOWER") => new Color(0.32f, 0.52f, 0.88f),
            _ when buildingId.StartsWith("BUILDING_BARRACKS") => new Color(0.28f, 0.72f, 0.38f),
            _ => Color.gray,
        };

        static Color GetSharedFlankRingColor() => new Color(0.7f, 0.65f, 0.45f, 0.95f);

        static Color GetLaneColor(string laneId) => laneId switch
        {
            GameIds.Lanes.Left => new Color(0.95f, 0.55f, 0.35f, 0.95f),
            GameIds.Lanes.Center => new Color(0.95f, 0.85f, 0.35f, 0.95f),
            GameIds.Lanes.Right => new Color(0.45f, 0.75f, 0.95f, 0.95f),
            _ => Color.white,
        };

        static void ApplyColor(GameObject marker, Color color)
        {
            var renderer = marker.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var material = CreateSolidMaterial(color);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        static Material CreateSolidMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            return material;
        }

        static Material CreateRoadMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", 0.05f);
            return material;
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

        static Material CreateLineMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.color = color;
            return material;
        }
    }
}
