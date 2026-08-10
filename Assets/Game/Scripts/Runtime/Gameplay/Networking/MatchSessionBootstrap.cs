using System;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Editor → LocalDev (unless BARAKI_UGS=1). Standalone → Unity Lobby+Relay.
    /// </summary>
    public static class MatchSessionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (IsTruthy(Environment.GetEnvironmentVariable("BARAKI_LOCALDEV")))
            {
                MatchSessionService.UseLocalDev();
                PlaytestLog.Info("Bootstrap", "LocalDev", ("reason", "BARAKI_LOCALDEV"));
                return;
            }

#if UNITY_EDITOR
            if (!IsTruthy(Environment.GetEnvironmentVariable("BARAKI_UGS")))
            {
                MatchSessionService.UseLocalDev();
                PlaytestLog.Info("Bootstrap", "LocalDev", ("reason", "EditorDefault"));
                return;
            }
#endif

            MatchSessionService.UseUnityLobbyRelay();
            PlaytestLog.Info("Bootstrap", "UnityLobbyRelay");
        }

        static bool IsTruthy(string value) =>
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
