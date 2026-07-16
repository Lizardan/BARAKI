using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Toggles Lobby+Relay backend for the current Editor process via BARAKI_UGS.
    /// Does not require restarting Unity.
    /// </summary>
    public static class BarakiUgsSessionMenu
    {
        const string EnvName = "BARAKI_UGS";

        [MenuItem("BARAKI/Networking/Use Unity Lobby+Relay in Editor")]
        static void Toggle()
        {
            var enabled = IsEnabled();
            if (enabled)
            {
                Environment.SetEnvironmentVariable(EnvName, null, EnvironmentVariableTarget.Process);
                Debug.Log("BARAKI: Editor Play Mode → LocalDev (BARAKI_UGS cleared).");
            }
            else
            {
                Environment.SetEnvironmentVariable(EnvName, "1", EnvironmentVariableTarget.Process);
                Debug.Log("BARAKI: Editor Play Mode → Unity Lobby+Relay (BARAKI_UGS=1). UGS project must be linked.");
            }
        }

        [MenuItem("BARAKI/Networking/Use Unity Lobby+Relay in Editor", true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(
                "BARAKI/Networking/Use Unity Lobby+Relay in Editor",
                IsEnabled());
            return true;
        }

        static bool IsEnabled()
        {
            var value = Environment.GetEnvironmentVariable(EnvName);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
