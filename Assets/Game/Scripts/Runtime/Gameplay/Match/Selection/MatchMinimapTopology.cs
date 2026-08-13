using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public readonly struct MatchMinimapSegment
    {
        public MatchMinimapSegment(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }

        public Vector2 A { get; }
        public Vector2 B { get; }
    }

    public readonly struct MatchMinimapRect
    {
        public MatchMinimapRect(
            Vector2 center,
            Vector2 halfExtents,
            float rotationDegrees,
            int ownerSlot = -1)
        {
            Center = center;
            HalfExtents = halfExtents;
            RotationDegrees = rotationDegrees;
            OwnerSlot = ownerSlot;
        }

        public Vector2 Center { get; }
        public Vector2 HalfExtents { get; }
        public float RotationDegrees { get; }
        public int OwnerSlot { get; }

        /// <summary>World XZ corner using Unity yaw (matches <c>RoadFootprintShapes.OrientedRect</c>).</summary>
        public Vector2 GetWorldCorner(int index)
        {
            var hx = HalfExtents.x;
            var hy = HalfExtents.y;
            var local = index switch
            {
                0 => new Vector3(-hx, 0f, -hy),
                1 => new Vector3(hx, 0f, -hy),
                2 => new Vector3(hx, 0f, hy),
                _ => new Vector3(-hx, 0f, hy),
            };
            var world = Quaternion.Euler(0f, RotationDegrees, 0f) * local;
            return Center + new Vector2(world.x, world.z);
        }
    }

    public sealed class MatchMinimapTopology
    {
        public MatchMinimapTopology(
            IReadOnlyList<MatchMinimapRect> filledRects,
            IReadOnlyList<MatchMinimapSegment> roadSegments,
            float centerArenaRadius = N4RoadReferenceSpec.CenterArenaHalfSize)
        {
            FilledRects = filledRects;
            RoadSegments = roadSegments;
            CenterArenaRadius = Mathf.Max(0f, centerArenaRadius);
        }

        public IReadOnlyList<MatchMinimapRect> FilledRects { get; }
        public IReadOnlyList<MatchMinimapSegment> RoadSegments { get; }
        public float CenterArenaRadius { get; }
    }
}
