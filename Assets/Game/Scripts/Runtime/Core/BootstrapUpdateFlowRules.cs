namespace Game.Core
{
    /// <summary>Pure decisions for Bootstrap after update check completes.</summary>
    public static class BootstrapUpdateFlowRules
    {
        public const string IdleCtaSynchronizingLabel = "СИНХРОНИЗАЦИЯ";

        public static bool ShouldOfferEnterGame(bool isUpdaterOnlyBuild, bool updateRequired) =>
            !isUpdaterOnlyBuild && !updateRequired;

        /// <summary>
        /// Idle CTA keeps a fixed label while side status shows the detailed bootstrap phase.
        /// </summary>
        public static string FormatIdleCtaLabel(string status) => IdleCtaSynchronizingLabel;
    }
}
