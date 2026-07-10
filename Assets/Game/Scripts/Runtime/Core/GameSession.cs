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

        public static void Reset()
        {
            IsPlaying = false;
            ActiveSetup = null;
        }
    }
}
