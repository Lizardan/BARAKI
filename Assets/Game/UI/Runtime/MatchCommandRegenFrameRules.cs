using UnityEngine;

namespace Game.UI
{
    /// <summary>Contour fill 0..1 for barracks call charge regen on command buttons.</summary>
    public static class MatchCommandRegenFrameRules
    {
        /// <summary>
        /// Just spent → 0; about to restore → 1. No active regen → 0 (frame hidden).
        /// </summary>
        public static float GetFill01(float remainingSeconds, float regenSeconds)
        {
            if (remainingSeconds <= 0f || regenSeconds <= 0f)
            {
                return 0f;
            }

            var elapsed = regenSeconds - remainingSeconds;
            return Mathf.Clamp01(elapsed / regenSeconds);
        }
    }
}
