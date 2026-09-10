using System;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Single owner of <see cref="Time.timeScale"/> for user pause, host migration
    /// and disconnect hold.
    /// </summary>
    public static class MatchPauseGate
    {
        public static bool IsDisconnectHoldPaused { get; private set; }

        /// <summary>
        /// Explicit flag — do not read <see cref="HostMigrationCoordinator.Instance"/>:
        /// Edit Mode tests never run Awake, so Instance stays null.
        /// </summary>
        public static bool IsMigrationPaused { get; private set; }

        /// <summary>Pause requested by any player via the pause menu (synchronized by RPC).</summary>
        public static bool IsUserPaused { get; private set; }

        /// <summary>
        /// Матч остановлен ареной. В отличие от остальных причин <b>не</b> обнуляет
        /// <see cref="Time.timeScale"/>: арена должна жить (камера, анимации, VFX),
        /// поэтому останавливается только тик симуляции и команды игроков.
        /// </summary>
        public static bool IsArenaPaused { get; private set; }

        public static bool IsPaused => IsUserPaused || IsMigrationPaused || IsDisconnectHoldPaused;

        /// <summary>Что блокирует симуляцию матча и команды: любая пауза, включая арену.</summary>
        public static bool IsSimulationPaused => IsPaused || IsArenaPaused;

        public static event Action PausedChanged;

        public static void SetUserPaused(bool paused)
        {
            if (IsUserPaused == paused)
            {
                return;
            }

            IsUserPaused = paused;
            RefreshTimeScale();
            PausedChanged?.Invoke();
        }

        public static void SetDisconnectHoldPaused(bool paused)
        {
            IsDisconnectHoldPaused = paused;
            RefreshTimeScale();
        }

        public static void SetMigrationPaused(bool paused)
        {
            IsMigrationPaused = paused;
            RefreshTimeScale();
        }

        /// <summary>
        /// Останавливает тик матча и команды на время арены, не трогая
        /// <see cref="Time.timeScale"/> — арена тикает нескалированным временем.
        /// </summary>
        public static void SetArenaPaused(bool paused)
        {
            if (IsArenaPaused == paused)
            {
                return;
            }

            IsArenaPaused = paused;
            PausedChanged?.Invoke();
        }

        public static void RefreshTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        /// <summary>Edit Mode tests must not leak pause into the next case.</summary>
        public static void ResetForTests()
        {
            IsDisconnectHoldPaused = false;
            IsMigrationPaused = false;
            IsUserPaused = false;
            IsArenaPaused = false;
            Time.timeScale = 1f;
        }
    }
}
