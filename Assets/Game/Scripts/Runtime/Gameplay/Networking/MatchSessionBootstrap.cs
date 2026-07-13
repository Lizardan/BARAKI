using System;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Picks LocalDev / NetDev / Discord session backend from env and WebGL shell.</summary>
    public static class MatchSessionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            EnsureBridgeObject();

            var hasSession = DiscordActivityBridge.TryGetSession(out var session);
            if (hasSession
                && string.Equals(session.InstanceId, "local-dev", StringComparison.Ordinal))
            {
                MatchSessionService.UseLocalDev();
                Debug.Log("MatchSessionBootstrap: LocalDev backend (browser smoke mode).");
                return;
            }

            if (hasSession && session.HasTransport)
            {
                MatchSessionService.UseDiscord();
                Debug.Log("MatchSessionBootstrap: Discord backend (shell session present).");
                return;
            }

            if (IsTruthy(Environment.GetEnvironmentVariable("BARAKI_DISCORD"))
                || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                MatchSessionService.UseDiscord();
                Debug.Log("MatchSessionBootstrap: Discord backend.");
                return;
            }

            if (IsTruthy(Environment.GetEnvironmentVariable("BARAKI_NETDEV")))
            {
                MatchSessionService.UseNetDev();
                Debug.Log("MatchSessionBootstrap: NetDev backend.");
                return;
            }

            MatchSessionService.UseLocalDev();
        }

        static void EnsureBridgeObject()
        {
            if (UnityEngine.Object.FindAnyObjectByType<DiscordActivityBridge>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(DiscordActivityBridge));
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DiscordActivityBridge>();
        }

        static bool IsTruthy(string value) =>
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
