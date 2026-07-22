using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public enum EnvironmentPropKind
    {
        Tree,
        Pine,
        Rock,
        Cliff,
        Flower,
        Mountain,
        PathPiece,
        River,
        Boat,
        Bridge,
        Lantern,
        Crate,
        Bench,
    }

    public readonly struct EnvironmentPropPlacement
    {
        public EnvironmentPropPlacement(
            EnvironmentPropKind kind,
            Vector3 position,
            float yawDegrees,
            Vector3? localScale = null)
        {
            Kind = kind;
            Position = position;
            YawDegrees = yawDegrees;
            LocalScale = localScale ?? Vector3.one;
        }

        public EnvironmentPropKind Kind { get; }
        public Vector3 Position { get; }
        public float YawDegrees { get; }
        public Vector3 LocalScale { get; }
    }

    /// <summary>
    /// Deterministic décor: uniform cobble on walkable roads; landscape + forest only in
    /// non-walkable pockets inside the map (roads stay flat).
    /// </summary>
    public static class MatchArenaEnvironmentRules
    {
        public const string DecorRootName = "EnvironmentDecor";

        public const float MinBaseDistance = 40f;
        public const float LandscapeOuterMargin = 6f;
        public const float CobbleStep = 1.85f;
        public const float CobbleLiftY = 0.03f;
        public const float NatureCellSize = 9f;
        public const float FlowerCellSize = 5.5f;
        const int FootprintSampleCount = 8;
        static readonly Vector3 CobbleScale = new(1.55f, 1f, 1.55f);

        public static int SeedForPlayerCount(int playerCount) =>
            unchecked(playerCount * 104729 + 17);

        public static bool AllowsWalkableOverlay(EnvironmentPropKind kind) =>
            kind == EnvironmentPropKind.PathPiece;

        /// <summary>Square map half-extent covering perimeter + corner fillets + thin fringe.</summary>
        public static float MapHalfExtent(float arenaRadius) =>
            arenaRadius + MatchArenaGreyboxBuilder.RoadWidth + LandscapeOuterMargin;

        public static bool IsWithinMapBounds(Vector3 position, float arenaRadius)
        {
            var max = MapHalfExtent(arenaRadius);
            return Mathf.Abs(position.x) <= max + 0.01f && Mathf.Abs(position.z) <= max + 0.01f;
        }

        public static float FootprintRadius(EnvironmentPropKind kind) => kind switch
        {
            EnvironmentPropKind.Mountain => 18f,
            EnvironmentPropKind.Cliff => 8f,
            EnvironmentPropKind.Tree => 4.5f,
            EnvironmentPropKind.Pine => 4.5f,
            EnvironmentPropKind.Rock => 2.5f,
            EnvironmentPropKind.Flower => 1.2f,
            EnvironmentPropKind.PathPiece => 1.1f,
            EnvironmentPropKind.River => 10f,
            EnvironmentPropKind.Boat => 3.5f,
            EnvironmentPropKind.Bridge => 8f,
            EnvironmentPropKind.Lantern => 1.8f,
            EnvironmentPropKind.Crate => 1.8f,
            EnvironmentPropKind.Bench => 2.2f,
            _ => 2f,
        };

        /// <summary>Legacy: mountains used to sit far outside the ring. Kept for tests as map fringe.</summary>
        public static float MinOuterDistance(EnvironmentPropKind kind, float arenaRadius) =>
            MapHalfExtent(arenaRadius) - FootprintRadius(kind);

        public static bool CanPlace(
            Vector3 position,
            WalkableSurface walkable,
            IReadOnlyList<PlayerSlotLayout> slots,
            float minBaseDistance = MinBaseDistance) =>
            CanPlace(position, walkable, slots, EnvironmentPropKind.Flower, arenaRadius: 0f, minBaseDistance);

        public static bool CanPlace(
            Vector3 position,
            WalkableSurface walkable,
            IReadOnlyList<PlayerSlotLayout> slots,
            EnvironmentPropKind kind,
            float arenaRadius,
            float minBaseDistance = MinBaseDistance)
        {
            if (AllowsWalkableOverlay(kind))
            {
                return true;
            }

            if (arenaRadius > 0.01f && !IsWithinMapBounds(position, arenaRadius))
            {
                return false;
            }

            var footprint = FootprintRadius(kind);
            var baseClearance = minBaseDistance + footprint * 0.35f;
            if (!IsClearOfBases(position, slots, baseClearance))
            {
                return false;
            }

            return FootprintClearOfWalkable(position, kind, walkable);
        }

        public static bool FootprintClearOfWalkable(
            Vector3 position,
            EnvironmentPropKind kind,
            WalkableSurface walkable)
        {
            if (AllowsWalkableOverlay(kind))
            {
                return true;
            }

            if (walkable == null)
            {
                return true;
            }

            if (walkable.Contains(position))
            {
                return false;
            }

            var footprint = FootprintRadius(kind);
            if (footprint <= 0.01f)
            {
                return true;
            }

            for (var i = 0; i < FootprintSampleCount; i++)
            {
                var angle = (Mathf.PI * 2f * i) / FootprintSampleCount;
                var sample = position + new Vector3(
                    Mathf.Cos(angle) * footprint,
                    0f,
                    Mathf.Sin(angle) * footprint);
                if (walkable.Contains(sample))
                {
                    return false;
                }
            }

            return true;
        }

        public static IReadOnlyList<EnvironmentPropPlacement> BuildPlacements(
            MatchArenaLayout layout,
            WalkableSurface walkable)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var rng = new System.Random(SeedForPlayerCount(layout.PlayerCount));
            var result = new List<EnvironmentPropPlacement>(4096);
            var radius = layout.ArenaRadius;

            TryAddCobblestoneGrid(result, walkable, rng);
            TryAddPocketLandscape(result, layout, walkable, rng, radius);
            TryAddNatureGrid(result, layout, walkable, rng, radius);
            TryAddRoadsideClutter(result, layout, walkable, rng, radius);

            return result;
        }

        public static int CountKind(IReadOnlyList<EnvironmentPropPlacement> placements, EnvironmentPropKind kind)
        {
            var count = 0;
            for (var i = 0; i < placements.Count; i++)
            {
                if (placements[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Uniform cobble carpet: sample walkable AABB on a fixed grid (no geometric gaps).</summary>
        static void TryAddCobblestoneGrid(
            List<EnvironmentPropPlacement> result,
            WalkableSurface walkable,
            System.Random rng)
        {
            if (walkable == null || walkable.PartCount == 0)
            {
                return;
            }

            walkable.GetBounds(out var min, out var max);
            var y = MatchArenaGreyboxBuilder.RoadHeight + CobbleLiftY;
            var step = CobbleStep;
            // Snap grid origin so coverage is stable across regenerations.
            var startX = Mathf.Floor(min.x / step) * step;
            var startZ = Mathf.Floor(min.y / step) * step;

            for (var x = startX; x <= max.x + 0.01f; x += step)
            {
                for (var z = startZ; z <= max.y + 0.01f; z += step)
                {
                    // Tiny deterministic jitter so stones don't look like a perfect lattice.
                    var jx = ((Hash(x, z) & 255) / 255f - 0.5f) * 0.35f;
                    var jz = (((Hash(x, z) >> 8) & 255) / 255f - 0.5f) * 0.35f;
                    var pos = new Vector3(x + jx, y, z + jz);
                    if (!walkable.Contains(pos))
                    {
                        continue;
                    }

                    result.Add(new EnvironmentPropPlacement(
                        EnvironmentPropKind.PathPiece,
                        pos,
                        (Hash(x, z) % 360),
                        CobbleScale));
                }
            }
        }

        /// <summary>Rivers + raised cliffs/rocks in diagonal pockets between roads (inside the map).</summary>
        static void TryAddPocketLandscape(
            List<EnvironmentPropPlacement> result,
            MatchArenaLayout layout,
            WalkableSurface walkable,
            System.Random rng,
            float radius)
        {
            var pocketCount = layout.PlayerCount <= 2 ? 2 : layout.PlayerCount;
            // Sit inside the ring, between spokes — not past the outer road fringe.
            var hubDist = radius * 0.58f;

            for (var p = 0; p < pocketCount; p++)
            {
                var pocketAngle = (Mathf.PI / pocketCount) + (Mathf.PI * 2f * p) / pocketCount;
                var tangent = new Vector3(-Mathf.Sin(pocketAngle), 0f, Mathf.Cos(pocketAngle));
                var radial = new Vector3(Mathf.Cos(pocketAngle), 0f, Mathf.Sin(pocketAngle));
                var hub = radial * hubDist;
                var yaw = pocketAngle * Mathf.Rad2Deg + 90f;

                // Soft hill height at pocket center.
                var hillY = 1.2f + (float)rng.NextDouble() * 1.4f;

                if (CanPlace(hub, walkable, layout.Slots, EnvironmentPropKind.River, radius, MinBaseDistance * 0.5f))
                {
                    result.Add(new EnvironmentPropPlacement(
                        EnvironmentPropKind.River,
                        new Vector3(hub.x, 0.02f, hub.z),
                        yaw,
                        new Vector3(12f, 0.1f, 18f)));

                    var boatPos = hub + tangent * 4f;
                    boatPos.y = 0.06f;
                    if (CanPlace(boatPos, walkable, layout.Slots, EnvironmentPropKind.Boat, radius, MinBaseDistance * 0.5f))
                    {
                        result.Add(new EnvironmentPropPlacement(EnvironmentPropKind.Boat, boatPos, yaw + 20f));
                    }
                }

                // Raised cliffs framing the pocket (terrain, not flat ground).
                for (var c = 0; c < 3; c++)
                {
                    var offset = tangent * ((c - 1) * 14f) + radial * (c == 1 ? -6f : 4f);
                    var cliffPos = hub + offset;
                    cliffPos.y = hillY * (0.4f + c * 0.15f);
                    if (!CanPlace(
                            new Vector3(cliffPos.x, 0f, cliffPos.z),
                            walkable,
                            layout.Slots,
                            EnvironmentPropKind.Cliff,
                            radius,
                            MinBaseDistance * 0.45f))
                    {
                        continue;
                    }

                    var scale = 0.55f + (float)rng.NextDouble() * 0.25f;
                    result.Add(new EnvironmentPropPlacement(
                        EnvironmentPropKind.Cliff,
                        cliffPos,
                        Yaw(rng),
                        Vector3.one * scale));
                }

                // Compact mound deeper in the pocket.
                var mound = hub + radial * 10f;
                mound.y = hillY + 0.8f;
                if (CanPlace(
                        new Vector3(mound.x, 0f, mound.z),
                        walkable,
                        layout.Slots,
                        EnvironmentPropKind.Mountain,
                        radius,
                        MinBaseDistance * 0.45f))
                {
                    result.Add(new EnvironmentPropPlacement(
                        EnvironmentPropKind.Mountain,
                        mound,
                        Yaw(rng),
                        Vector3.one * 0.22f));
                }

                // Guaranteed pocket clutter (crates) off walkable.
                for (var c = 0; c < 2; c++)
                {
                    var clutter = hub + tangent * ((c == 0 ? -1f : 1f) * 8f) + radial * 2f;
                    clutter.y = 0f;
                    if (!CanPlace(clutter, walkable, layout.Slots, EnvironmentPropKind.Crate, radius, MinBaseDistance * 0.35f))
                    {
                        continue;
                    }

                    clutter.y = LandscapeElevation(clutter, walkable) * 0.2f;
                    result.Add(new EnvironmentPropPlacement(
                        c == 0 ? EnvironmentPropKind.Crate : EnvironmentPropKind.Bench,
                        clutter,
                        Yaw(rng)));
                }
            }
        }

        /// <summary>Even forest/rock fill on a staggered grid across non-road pockets inside the map.</summary>
        static void TryAddNatureGrid(
            List<EnvironmentPropPlacement> result,
            MatchArenaLayout layout,
            WalkableSurface walkable,
            System.Random rng,
            float radius)
        {
            var half = MapHalfExtent(radius);
            var cell = NatureCellSize;
            var row = 0;
            for (var z = -half; z <= half; z += cell)
            {
                var xOffset = (row & 1) == 0 ? 0f : cell * 0.5f;
                row++;
                for (var x = -half + xOffset; x <= half; x += cell)
                {
                    var jitter = new Vector3(
                        (float)(rng.NextDouble() - 0.5) * cell * 0.35f,
                        0f,
                        (float)(rng.NextDouble() - 0.5) * cell * 0.35f);
                    var flat = new Vector3(x, 0f, z) + jitter;
                    if (!IsWithinMapBounds(flat, radius))
                    {
                        continue;
                    }

                    if (walkable != null && walkable.Contains(flat))
                    {
                        continue;
                    }

                    // Height rises toward pocket centers (away from roads).
                    var elev = LandscapeElevation(flat, walkable);
                    var roll = Hash(x, z) % 10;
                    EnvironmentPropKind kind;
                    float scale = 1f;
                    if (roll <= 4)
                    {
                        kind = EnvironmentPropKind.Tree;
                    }
                    else if (roll <= 7)
                    {
                        kind = EnvironmentPropKind.Pine;
                    }
                    else if (roll == 8)
                    {
                        kind = EnvironmentPropKind.Rock;
                        scale = 0.9f + (float)rng.NextDouble() * 0.4f;
                    }
                    else
                    {
                        kind = EnvironmentPropKind.Flower;
                    }

                    if (!CanPlace(flat, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.55f))
                    {
                        continue;
                    }

                    var pos = flat;
                    pos.y = elev;
                    result.Add(new EnvironmentPropPlacement(
                        kind,
                        pos,
                        Yaw(rng),
                        scale == 1f ? null : Vector3.one * scale));
                }
            }

            // Denser flowers in a finer pass near road edges (still off walkable).
            var fCell = FlowerCellSize;
            for (var z = -half; z <= half; z += fCell)
            {
                for (var x = -half; x <= half; x += fCell)
                {
                    if (((Hash(x, z) >> 4) & 3) != 0)
                    {
                        continue;
                    }

                    var flat = new Vector3(x, 0f, z);
                    if (!IsWithinMapBounds(flat, radius) || (walkable != null && walkable.Contains(flat)))
                    {
                        continue;
                    }

                    if (!NearWalkable(flat, walkable, MatchArenaGreyboxBuilder.RoadWidth * 0.5f + 6f))
                    {
                        continue;
                    }

                    if (!CanPlace(flat, walkable, layout.Slots, EnvironmentPropKind.Flower, radius, MinBaseDistance * 0.5f))
                    {
                        continue;
                    }

                    flat.y = LandscapeElevation(flat, walkable) * 0.35f;
                    result.Add(new EnvironmentPropPlacement(EnvironmentPropKind.Flower, flat, Yaw(rng)));
                }
            }
        }

        static void TryAddRoadsideClutter(
            List<EnvironmentPropPlacement> result,
            MatchArenaLayout layout,
            WalkableSurface walkable,
            System.Random rng,
            float radius)
        {
            var n = layout.PlayerCount;
            var roadside = MatchArenaGreyboxBuilder.RoadWidth * 0.5f + 3f;
            var samples = n * 24;

            for (var i = 0; i < samples; i++)
            {
                var angle = (Mathf.PI * 2f * i) / samples;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // Prefer inner fringe (into pockets), occasional outer fringe within map.
                var inward = i % 3 != 0;
                var pos = dir * radius + dir * (inward ? -roadside : roadside);
                pos.y = 0f;
                if (!IsWithinMapBounds(pos, radius))
                {
                    continue;
                }

                var kind = (i % 6) switch
                {
                    0 => EnvironmentPropKind.Lantern,
                    1 => EnvironmentPropKind.Rock,
                    _ => EnvironmentPropKind.Flower,
                };

                if (!CanPlace(pos, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.5f))
                {
                    continue;
                }

                pos.y = LandscapeElevation(pos, walkable) * 0.25f;
                result.Add(new EnvironmentPropPlacement(kind, pos, Yaw(rng)));
            }

            // Spoke fringes.
            for (var s = 0; s < 4; s++)
            {
                var angle = s * Mathf.PI * 0.5f;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var steps = n * 5;
                for (var i = 1; i < steps; i++)
                {
                    var t = (i / (float)steps) * radius * 0.9f;
                    var side = (i % 2 == 0 ? 1f : -1f) * roadside;
                    var pos = radial * t + tangent * side;
                    pos.y = 0f;
                    var kind = i % 4 == 0 ? EnvironmentPropKind.Lantern : EnvironmentPropKind.Flower;
                    if (!CanPlace(pos, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.5f))
                    {
                        continue;
                    }

                    pos.y = LandscapeElevation(pos, walkable) * 0.25f;
                    result.Add(new EnvironmentPropPlacement(kind, pos, Yaw(rng)));
                }
            }

            foreach (var slot in layout.Slots)
            {
                for (var i = 0; i < 3; i++)
                {
                    // Beside the base pad (slot-local X), slightly toward the field — avoids the pad mesh.
                    var local = new Vector3(
                        (i % 2 == 0 ? -1f : 1f) * (MatchArenaGreyboxBuilder.BaseArenaWidth * 0.5f + 6f + i),
                        0f,
                        -MatchArenaGreyboxBuilder.BaseArenaOutwardOffset + (float)rng.NextDouble() * 4f);
                    var pos = slot.BasePosition + slot.BaseRotation * local;
                    pos.y = 0f;
                    if (!IsWithinMapBounds(pos, radius))
                    {
                        continue;
                    }

                    var kind = i == 2 ? EnvironmentPropKind.Bench : EnvironmentPropKind.Crate;
                    if (!CanPlace(pos, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.25f))
                    {
                        continue;
                    }

                    result.Add(new EnvironmentPropPlacement(kind, pos, Yaw(rng)));
                }
            }
        }

        static float LandscapeElevation(Vector3 position, WalkableSurface walkable)
        {
            if (walkable == null)
            {
                return 0f;
            }

            // Approximate distance to road via clamp delta.
            var onRoad = walkable.Clamp(position);
            var dx = position.x - onRoad.x;
            var dz = position.z - onRoad.z;
            var dist = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Clamp(dist * 0.08f, 0f, 3.5f);
        }

        static bool NearWalkable(Vector3 position, WalkableSurface walkable, float maxDist)
        {
            if (walkable == null)
            {
                return false;
            }

            var onRoad = walkable.Clamp(position);
            var dx = position.x - onRoad.x;
            var dz = position.z - onRoad.z;
            return dx * dx + dz * dz <= maxDist * maxDist;
        }

        static bool IsClearOfBases(
            Vector3 position,
            IReadOnlyList<PlayerSlotLayout> slots,
            float minBaseDistance)
        {
            if (slots == null || slots.Count == 0)
            {
                return true;
            }

            var minDistSq = minBaseDistance * minBaseDistance;
            for (var i = 0; i < slots.Count; i++)
            {
                var delta = position - slots[i].BasePosition;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistSq)
                {
                    return false;
                }
            }

            return true;
        }

        static int Hash(float x, float z)
        {
            unchecked
            {
                var ix = Mathf.RoundToInt(x * 10f);
                var iz = Mathf.RoundToInt(z * 10f);
                var h = ix * 73856093 ^ iz * 19349663;
                return h & 0x7fffffff;
            }
        }

        static float Yaw(System.Random rng) => (float)rng.NextDouble() * 360f;
    }
}
