using System;
using Game.Core;
using Game.Gameplay.Networking;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Registers match cheats into <see cref="RuntimeDebugConsoleCommands"/>.</summary>
    public static class MatchDebugConsoleCommands
    {
        private const int DefaultGoldAmount = 1000;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            RuntimeDebugConsoleCommands.Register(
                "gold",
                "Дать золото всем в матче (по умолчанию 1000). В сети — с любого клиента. Пример: gold 1000",
                Gold);
        }

        private static string Gold(ReadOnlySpan<string> args)
        {
            var amount = DefaultGoldAmount;
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out amount) || amount <= 0)
                {
                    return "Использование: gold [amount]";
                }
            }

            // Networked match: any peer requests; host applies to all + snapshot sync.
            if (MatchNetworkCommands.TryRequestDebugAddGoldToAll(amount))
            {
                return $"+{amount} золота всем (синк по сети)";
            }

            if (!TryGetMutableController(out var controller))
            {
                return "Матч не запущен";
            }

            var granted = controller.DebugAddGoldToAll(amount);
            return $"+{amount} золота игрокам: {granted}";
        }

        private static bool TryGetMutableController(out MatchController controller)
        {
            controller = null;
            var runtime = MatchRuntime.Current;
            if (runtime == null)
            {
                return false;
            }

            controller = runtime.Controller;
            if (controller == null || !controller.IsRunning)
            {
                return false;
            }

            if (!MatchTickAuthority.ShouldTickSimulation(runtime.TickMode))
            {
                return false;
            }

            return true;
        }
    }
}
