using System;

namespace Game.Core
{
    /// <summary>
    /// Signals that the player left the main menu and entered gameplay.
    /// </summary>
    public static class GameSession
    {
        public static bool IsPlaying { get; private set; }

        public static MatchSetup ActiveSetup { get; private set; }

        public static event Action Started;

        public static void Begin(MatchSetup setup = null)
        {
            if (IsPlaying)
            {
                return;
            }

            ActiveSetup = setup ?? MatchSetup.Default;
            IsPlaying = true;
            Started?.Invoke();
        }

        /// <summary>
        /// Replaces lobby handoff data while a session is already playing
        /// (e.g. offline race pick assigns a random local slot).
        /// </summary>
        public static void UpdateActiveSetup(MatchSetup setup)
        {
            if (!IsPlaying || setup == null)
            {
                return;
            }

            ActiveSetup = setup;
        }

        public static void Reset()
        {
            IsPlaying = false;
            ActiveSetup = null;
        }
    }
}
