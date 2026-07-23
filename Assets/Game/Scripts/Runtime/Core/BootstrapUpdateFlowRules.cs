namespace Game.Core
{
    /// <summary>Pure decisions for Bootstrap after update check completes.</summary>
    public static class BootstrapUpdateFlowRules
    {
        public static bool ShouldOfferEnterGame(bool isUpdaterOnlyBuild, bool updateRequired) =>
            !isUpdaterOnlyBuild && !updateRequired;
    }
}
