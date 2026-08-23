namespace Game.Editor
{
    public static class UpdaterBuildRules
    {
        public const string BootstrapScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        public const string OutputDirectory = "build/Updater";
        public const string UpdaterOnlyDefine = "BARAKI_UPDATER_ONLY";

        private static readonly string[] s_scenes = { BootstrapScenePath };
        private static readonly string[] s_extraScriptingDefines = { UpdaterOnlyDefine };

        public static string[] Scenes => (string[])s_scenes.Clone();
        public static string[] ExtraScriptingDefines => (string[])s_extraScriptingDefines.Clone();
    }
}
