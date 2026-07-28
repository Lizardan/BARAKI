using System;

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
}
