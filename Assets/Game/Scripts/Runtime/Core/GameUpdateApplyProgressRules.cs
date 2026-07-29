using System;
using System.IO;

namespace Game.Core
{
    /// <summary>Phases of the in-client update apply pipeline (single progress bar).</summary>
    public enum GameUpdateApplyPhase
    {
        Downloading = 0,
        Installing = 1,
        ReadyToRestart = 2,
    }

    /// <summary>One progress tick for the bootstrap update CTA bar (0..1 within the current phase).</summary>
    public readonly struct GameUpdateApplyProgress
    {
        public GameUpdateApplyPhase Phase { get; }
        public float Phase01 { get; }

        public GameUpdateApplyProgress(GameUpdateApplyPhase phase, float phase01)
        {
            Phase = phase;
            Phase01 = phase01;
        }

        public static GameUpdateApplyProgress FromPhase(GameUpdateApplyPhase phase, float phase01) =>
            new GameUpdateApplyProgress(phase, GameUpdateApplyProgressRules.Clamp01(phase01));
    }

    /// <summary>Per-phase 0..1 bar mapping and UI copy for the update CTA.</summary>
    public static class GameUpdateApplyProgressRules
    {
        /// <summary>Each phase fills the same bar independently from 0 to 100.</summary>
        public static float MapBarProgress(GameUpdateApplyPhase phase, float phase01)
        {
            if (phase == GameUpdateApplyPhase.ReadyToRestart)
            {
                return 1f;
            }

            return Clamp01(phase01);
        }

        public static string FormatSideStatus(GameUpdateApplyPhase phase, string remoteVersion)
        {
            switch (phase)
            {
                case GameUpdateApplyPhase.Downloading:
                    return "Загрузка " + GameUpdateUiRules.FormatVersionLabel(remoteVersion);
                case GameUpdateApplyPhase.Installing:
                    return "Установка обновления";
                case GameUpdateApplyPhase.ReadyToRestart:
                    return "Готово к перезапуску";
                default:
                    return string.Empty;
            }
        }

        public static string FormatProgressLabel(GameUpdateApplyPhase phase, float phase01)
        {
            if (phase == GameUpdateApplyPhase.ReadyToRestart)
            {
                return GameUpdateUiRules.RestartButtonLabel;
            }

            return GameUpdateUiRules.FormatProgressLabel(MapBarProgress(phase, phase01));
        }

        public static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
            {
                return 0f;
            }

            if (value >= 1f)
            {
                return 1f;
            }

            return value;
        }
    }

    /// <summary>Pure helpers for on-disk pending restart marker after DownloadAndPrepare.</summary>
    public static class GameUpdatePendingRestartRules
    {
        public const string MarkerFileName = "pending-restart.txt";

        public static string ResolveMarkerPath(string stagingRoot) =>
            Path.Combine(stagingRoot ?? string.Empty, MarkerFileName);

        public static string FormatMarker(string payloadDir, string remoteVersion) =>
            (payloadDir ?? string.Empty).Trim()
            + Environment.NewLine
            + (remoteVersion ?? string.Empty).Trim();

        public static bool TryParseMarker(string content, out string payloadDir, out string remoteVersion)
        {
            payloadDir = null;
            remoteVersion = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            using var reader = new StringReader(content);
            payloadDir = reader.ReadLine()?.Trim();
            remoteVersion = reader.ReadLine()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(payloadDir);
        }

        public static bool IsPayloadReady(string payloadDir)
        {
            if (string.IsNullOrWhiteSpace(payloadDir) || !Directory.Exists(payloadDir))
            {
                return false;
            }

            try
            {
                return Directory.GetFileSystemEntries(payloadDir).Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// After ReadyToRestart, late Downloading/Installing progress ticks must not roll the UI back.
        /// </summary>
        public static bool ShouldAcceptApplyProgress(bool isReadyToRestart, GameUpdateApplyPhase incoming) =>
            !isReadyToRestart || incoming == GameUpdateApplyPhase.ReadyToRestart;
    }
}
