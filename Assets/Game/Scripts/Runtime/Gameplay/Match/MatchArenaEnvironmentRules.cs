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
    /// Deterministic landscape and forest décor outside walkable roads.
    /// Roads use one continuous material so units remain visually readable.
    /// </summary>
    public static class MatchArenaEnvironmentRules
    {
        public const string DecorRootName = "EnvironmentDecor";

        public const float MinBaseDistance = 40f;
        public const float LandscapeOuterMargin = 6f;
        public const float NatureCellSize = 8.5f;
        public const float FlowerCellSize = 7.5f;
        const int FootprintSampleCount = 8;

        public static int SeedForPlayerCount(int playerCount) =>
            unchecked(playerCount * 104729 + 17);

        public static bool AllowsWalkableOverlay(EnvironmentPropKind kind) => false;

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

        /// <summary>Dark pockets between roads: black water, cliffs, rocks and mountain silhouettes.</summary>
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

                if (CanPlace(hub, walkable, layout.Slots, EnvironmentPropKind.River, radius, MinBaseDistance * 0.5f))
                {
                    result.Add(new EnvironmentPropPlacement(
                        EnvironmentPropKind.River,
                        new Vector3(hub.x, 0.02f, hub.z),
                        yaw,
                        new Vector3(12f, 0.1f, 18f)));

                    TryAddPocketRockCluster(result, layout, walkable, rng, radius, hub, tangent, radial);
                }

                // Cliffs framing the pocket — sit on the ground plane (no airborne Y).
                for (var c = 0; c < 4; c++)
                {
                    var offset = tangent * ((c - 1.5f) * 12f) + radial * (c % 2 == 0 ? -5f : 5f);
                    var cliffPos = hub + offset;
                    cliffPos.y = 0f;
                    if (!CanPlace(
                            cliffPos,
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

                // Compact mountain mound deeper in the pocket — also grounded.
                var mound = hub + radial * 10f;
                mound.y = 0f;
                if (CanPlace(
                        mound,
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
                        Vector3.one * 0.28f));
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

                    result.Add(new EnvironmentPropPlacement(
                        c == 0 ? EnvironmentPropKind.Crate : EnvironmentPropKind.Bench,
                        clutter,
                        Yaw(rng)));
                }
            }
        }

        static void TryAddPocketRockCluster(
            List<EnvironmentPropPlacement> result,
            MatchArenaLayout layout,
            WalkableSurface walkable,
            System.Random rng,
            float radius,
            Vector3 hub,
            Vector3 tangent,
            Vector3 radial)
        {
            for (var i = 0; i < 3; i++)
            {
                var rockPos = hub
                              + tangent * ((i - 1) * 5f)
                              + radial * (4f + (float)rng.NextDouble() * 3f);
                rockPos.y = 0f;
                if (!CanPlace(rockPos, walkable, layout.Slots, EnvironmentPropKind.Rock, radius, MinBaseDistance * 0.35f))
                {
                    continue;
                }

                var scale = 1.1f + (float)rng.NextDouble() * 0.45f;
                result.Add(new EnvironmentPropPlacement(
                    EnvironmentPropKind.Rock,
                    rockPos,
                    Yaw(rng),
                    Vector3.one * scale));
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

                    var roll = Hash(x, z) % 10;
                    EnvironmentPropKind kind;
                    float scale = 1f;
                    if (roll <= 5)
                    {
                        kind = EnvironmentPropKind.Pine;
                    }
                    else if (roll <= 7)
                    {
                        kind = EnvironmentPropKind.Tree;
                    }
                    else
                    {
                        kind = EnvironmentPropKind.Rock;
                        scale = 0.9f + (float)rng.NextDouble() * 0.4f;
                    }

                    if (!CanPlace(flat, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.55f))
                    {
                        continue;
                    }

                    // Props sit on the ground plane; only cliffs/mountains are raised as terrain.
                    flat.y = 0f;
                    result.Add(new EnvironmentPropPlacement(
                        kind,
                        flat,
                        Yaw(rng),
                        scale == 1f ? null : Vector3.one * scale));
                }
            }

            // Sparse low-contrast accents near road edges. Keep rocks away from unit routes.
            var fCell = FlowerCellSize;
            for (var z = -half; z <= half; z += fCell)
            {
                for (var x = -half; x <= half; x += fCell)
                {
                    var accentHash = Hash(x, z) >> 4;
                    if ((accentHash & 3) != 0)
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

                    var kind = (accentHash % 12) switch
                    {
                        0 => EnvironmentPropKind.Lantern,
                        1 or 2 => EnvironmentPropKind.Flower,
                        _ => EnvironmentPropKind.Pine,
                    };

                    if (!CanPlace(flat, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.5f))
                    {
                        continue;
                    }

                    result.Add(new EnvironmentPropPlacement(kind, flat, Yaw(rng)));
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

                var kind = (i % 8) switch
                {
                    0 => EnvironmentPropKind.Lantern,
                    1 or 2 or 3 or 4 or 5 => EnvironmentPropKind.Pine,
                    _ => EnvironmentPropKind.Crate,
                };

                if (!CanPlace(pos, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.5f))
                {
                    continue;
                }

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
                    var kind = i % 5 == 0 ? EnvironmentPropKind.Lantern : EnvironmentPropKind.Pine;
                    if (!CanPlace(pos, walkable, layout.Slots, kind, radius, MinBaseDistance * 0.5f))
                    {
                        continue;
                    }

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
