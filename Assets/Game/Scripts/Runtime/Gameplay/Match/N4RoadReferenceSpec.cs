using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Hand-tuned N=4 procedural road geometry constants.</summary>
    public static class N4RoadReferenceSpec
    {
        public const int SourcePartsCount = 29;

        public const float TransformTolerance = 0.06f;

        public const float ArenaHalfSize = MatchArenaGenerator.DefaultArenaRadius;

        public const float PerimeterHalfStripLength = 70f;

        public const float PerimeterHalfStripCenter = 55f;

        /// <summary>Centerline fillet radius at map corners (120 − 95).</summary>
        public const float PerimeterCornerCenterlineRadius = 25f;

        /// <summary>Corner mesh half-extent from arc center (centerline R + road half-width).</summary>
        public const float PerimeterCornerMeshHalfExtent = 35f;

        /// <summary>Center platform diameter (greybox visual).</summary>
        public const float CenterArenaDiameter = 50f;

        public const float CenterArenaHalfSize = CenterArenaDiameter * 0.5f;

        public const float SpokeConnectorCenter = 55.25f;

        public const float SpokeConnectorLength = 70.5f;

        public const float JunctionFilletRadius = MatchArenaGreyboxBuilder.RoadWidth * 1.5f;

        public const float FilletStripOverlap = 0.5f;

        public const float CenterArenaPlatformY = 0f;

        public static float NegativeHalfMin => -PerimeterHalfStripCenter - PerimeterHalfStripLength * 0.5f;

        public static float NegativeHalfMax => -PerimeterHalfStripCenter + PerimeterHalfStripLength * 0.5f;

        public static float PositiveHalfMin => PerimeterHalfStripCenter - PerimeterHalfStripLength * 0.5f;

        public static float PositiveHalfMax => PerimeterHalfStripCenter + PerimeterHalfStripLength * 0.5f;

        public static float SpokeConnectorHalfLength => SpokeConnectorLength * 0.5f;

        /// <summary>Perimeter half-strip outer centerline toward a map corner (tangent to corner arc).</summary>
        public static float GetPerimeterStripCornerOuter(float halfSize) =>
            halfSize - PerimeterCornerCenterlineRadius;

        public static float PerimeterHalfStripInnerBound =>
            -PerimeterHalfStripCenter + PerimeterHalfStripLength * 0.5f;

        public static float PerimeterHalfStripOuterBound =>
            PerimeterHalfStripCenter - PerimeterHalfStripLength * 0.5f;

        public static float PerimeterHalfStripCornerLength =>
            PerimeterHalfStripLength
            + (GetPerimeterStripCornerOuter(ArenaHalfSize) - (PerimeterHalfStripCenter + PerimeterHalfStripLength * 0.5f));

        public static Vector3 GetCornerArcCenterlineCenter(Vector3 corner) =>
            new Vector3(
                corner.x - Mathf.Sign(corner.x) * PerimeterCornerCenterlineRadius,
                0f,
                corner.z - Mathf.Sign(corner.z) * PerimeterCornerCenterlineRadius);

        public static Vector3 GetMapCornerArcCorner(int index, float halfSize = ArenaHalfSize) =>
            index switch
            {
                0 => new Vector3(halfSize, 0f, halfSize),
                1 => new Vector3(halfSize, 0f, -halfSize),
                2 => new Vector3(-halfSize, 0f, -halfSize),
                _ => new Vector3(-halfSize, 0f, halfSize),
            };

    }
}
