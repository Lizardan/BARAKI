namespace Game.Core
{
    public enum LauncherProgressPhase
    {
        Ready = 0,
        Downloading = 1,
        Installing = 2,
        Checking = 3,
        UpdateAvailable = 4,
        ReadyToRestart = 5,
        Warming = 6,
    }

    /// <summary>Pure helpers for launcher progress copy, CTA, and visibility.</summary>
    public static class LauncherProgressRules
    {
        public const string CtaPlayLabel = "ИГРАТЬ";
        public const string CtaUpdateLabel = "ОБНОВИТЬ";

        public static string StatusLabel(
            LauncherProgressPhase phase,
            string warmingDetail = null,
            string localVersion = null,
            string remoteVersion = null) =>
            phase switch
            {
                LauncherProgressPhase.Downloading => "Загрузка…",
                LauncherProgressPhase.Installing => "Установка…",
                LauncherProgressPhase.Checking => "Проверка версии…",
                LauncherProgressPhase.UpdateAvailable => FormatUpdateAvailableStatus(remoteVersion),
                LauncherProgressPhase.ReadyToRestart => "Готово к перезапуску",
                LauncherProgressPhase.Warming => string.IsNullOrWhiteSpace(warmingDetail)
                    ? "Подготовка…"
                    : warmingDetail.Trim(),
                _ => "Готово к запуску",
            };

        private static string FormatUpdateAvailableStatus(string remoteVersion)
        {
            const string baseText = "Доступно обновление";
            if (string.IsNullOrWhiteSpace(remoteVersion))
            {
                return baseText;
            }

            return baseText
                + " <color=#4A9EFF>"
                + GameUpdateUiRules.FormatVersionLabel(remoteVersion)
                + "</color>";
        }

        public static string CtaLabel(LauncherProgressPhase phase) =>
            phase switch
            {
                LauncherProgressPhase.UpdateAvailable => CtaUpdateLabel,
                LauncherProgressPhase.ReadyToRestart => GameUpdateUiRules.RestartButtonLabel,
                _ => CtaPlayLabel,
            };

        public static bool ShouldShowProgressDetails(LauncherProgressPhase phase) =>
            phase is LauncherProgressPhase.Downloading or LauncherProgressPhase.Installing;

        public static bool IsCtaEnabled(LauncherProgressPhase phase) =>
            phase is LauncherProgressPhase.Ready
                or LauncherProgressPhase.UpdateAvailable
                or LauncherProgressPhase.ReadyToRestart;

        /// <summary>Legacy alias used by older call sites.</summary>
        public static bool IsPlayEnabled(LauncherProgressPhase phase) => IsCtaEnabled(phase);

        public static LauncherProgressPhase FromApplyPhase(GameUpdateApplyPhase phase) =>
            phase switch
            {
                GameUpdateApplyPhase.Installing => LauncherProgressPhase.Installing,
                GameUpdateApplyPhase.ReadyToRestart => LauncherProgressPhase.ReadyToRestart,
                _ => LauncherProgressPhase.Downloading,
            };
    }
}
