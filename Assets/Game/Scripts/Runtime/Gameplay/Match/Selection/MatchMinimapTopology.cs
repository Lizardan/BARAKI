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
    }

    public sealed class MatchMinimapTopology
    {
        public MatchMinimapTopology(
            IReadOnlyList<MatchMinimapRect> filledRects,
            IReadOnlyList<MatchMinimapSegment> roadSegments)
        {
            FilledRects = filledRects;
            RoadSegments = roadSegments;
        }

        public IReadOnlyList<MatchMinimapRect> FilledRects { get; }
        public IReadOnlyList<MatchMinimapSegment> RoadSegments { get; }
    }
}
