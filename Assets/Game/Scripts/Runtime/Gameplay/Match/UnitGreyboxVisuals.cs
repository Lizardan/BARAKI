namespace Game.Gameplay.Match
{
    /// <summary>Shared greybox unit visual scale for presenter prefabs.</summary>
    public static class UnitGreyboxVisuals
    {
        public const float Scale = 2f;

        /// <summary>Animated Human (WC3 mercenary) models are authored larger than greybox capsules.</summary>
        public const float AnimatedHumanScaleFactor = 0.5f;

        /// <summary>
        /// WC3 meshes face +X; match locomotion faces +Z. Prefab yaw aligns model forward with Unity forward.
        /// </summary>
        public const float AnimatedHumanModelYawDegrees = 90f;
    }
}
