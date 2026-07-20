using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class MatchHudPassiveGoldRules
    {
        public static float GetFill01(float remainingSeconds, float intervalSeconds)
        {
            if (intervalSeconds <= 0f)
            {
                return 1f;
            }

            var remaining = Mathf.Clamp(remainingSeconds, 0f, intervalSeconds);
            return 1f - (remaining / intervalSeconds);
        }
    }
}
