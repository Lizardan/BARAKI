using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Single owner of <see cref="Time.timeScale"/> for host migration and disconnect hold.
    /// </summary>
    public static class MatchPauseGate
    {
        public static bool IsDisconnectHoldPaused { get; private set; }

        /// <summary>
        /// Explicit flag — do not read <see cref="HostMigrationCoordinator.Instance"/>:
        /// Edit Mode tests never run Awake, so Instance stays null.
        /// </summary>
        public static bool IsMigrationPaused { get; private set; }

        public static bool IsPaused => IsMigrationPaused || IsDisconnectHoldPaused;

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

        public static void RefreshTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        /// <summary>Edit Mode tests must not leak pause into the next case.</summary>
        public static void ResetForTests()
        {
            IsDisconnectHoldPaused = false;
            IsMigrationPaused = false;
            Time.timeScale = 1f;
        }
    }
}
