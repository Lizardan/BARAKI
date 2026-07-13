using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Networking
{
    /// <summary>Headless and desktop-compatible dedicated server command-line entry.</summary>
    public sealed class DedicatedServerEntry : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (!IsServerMode(Environment.GetCommandLineArgs()))
            {
                return;
            }

            var entryObject = new GameObject(nameof(DedicatedServerEntry));
            DontDestroyOnLoad(entryObject);
            entryObject.AddComponent<DedicatedServerEntry>();
        }

        private void Start()
        {
            RunServerAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask RunServerAsync(System.Threading.CancellationToken cancellationToken)
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            var port = ReadUShortArgument(args, "-port", "PORT", MatchNetworkEndpoint.DefaultPort);
            var players = ReadIntArgument(
                args,
                "-players",
                "PLAYER_COUNT",
                MatchSetup.DefaultPlayerCount);
            if (!MatchModeRules.IsValidPlayerCount(players))
            {
                players = MatchSetup.DefaultPlayerCount;
            }

            var handle = new MatchSessionHandle(
                "SERVER",
                players,
                localPlayerSlot: 0,
                transportEndpoint: $"ws://127.0.0.1:{port}");
            MatchNetworkSession.ApplyHandle(handle);
            GameSession.Begin(new MatchSetup(players, 0));

            if (SceneManager.GetActiveScene().name != GameSceneNames.Game)
            {
                await SceneManager.LoadSceneAsync(GameSceneNames.Game)
                    .ToUniTask(cancellationToken: cancellationToken);
            }

            var bootstrap = MatchNetworkBootstrap.Ensure();
            bootstrap.ConfigureEndpoint("127.0.0.1", port, listenAll: true);
            if (!bootstrap.StartAsServer())
            {
                Debug.LogError($"BARAKI server failed to listen on port {port}.");
                return;
            }

            Debug.Log($"BARAKI dedicated server listening on 0.0.0.0:{port} for {players} players.");
        }

        private static bool IsServerMode(string[] args)
        {
#if UNITY_SERVER
            return true;
#else
            if (Application.isBatchMode
                || string.Equals(
                    Environment.GetEnvironmentVariable("BARAKI_SERVER"),
                    "1",
                    StringComparison.Ordinal))
            {
                return true;
            }

            return HasArgument(args, "-batchmode") || HasArgument(args, "-barakiServer");
#endif
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static ushort ReadUShortArgument(
            string[] args,
            string argument,
            string environmentVariable,
            ushort fallback)
        {
            var value = ReadArgument(args, argument)
                        ?? Environment.GetEnvironmentVariable(environmentVariable);
            return ushort.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        private static int ReadIntArgument(
            string[] args,
            string argument,
            string environmentVariable,
            int fallback)
        {
            var value = ReadArgument(args, argument)
                        ?? Environment.GetEnvironmentVariable(environmentVariable);
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static string ReadArgument(string[] args, string argument)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
