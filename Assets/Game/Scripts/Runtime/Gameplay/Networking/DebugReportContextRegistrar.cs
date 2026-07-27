using System.Text;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Networking
{
    /// <summary>Registers NET snapshot for playtest Discord reports.</summary>
    public static class DebugReportContextRegistrar
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DebugReportContext.BuildNetSection = BuildNetSection;
        }

        private static string BuildNetSection()
        {
            var nm = NetworkManager.Singleton;
            var isServer = nm != null && nm.IsServer;
            var isClient = nm != null && nm.IsClient;
            var role = !MatchNetworkSession.IsNetworked
                ? "Offline"
                : isServer && isClient
                    ? "Host"
                    : isServer
                        ? "Server"
                        : isClient
                            ? "Client"
                            : "Offline";

            var phase = ResolvePhase();
            var clients = nm != null && nm.IsListening
                ? nm.ConnectedClientsIds.Count
                : 0;
            var builder = new StringBuilder(192);
            var displayName = SanitizeName(PlayerProfileService.DisplayName);
            var modePlayers = ResolveModePlayerCount();
            builder
                .Append("name=").Append(displayName)
                .Append(" role=").Append(role)
                .Append(" slot=").Append(MatchNetworkSession.LocalSlot)
                .Append(" listenHostSlot=").Append(MatchNetworkSession.ListenHostSlot)
                .Append(" clientId=")
                .Append(nm != null ? (long)nm.LocalClientId : -1L)
                .AppendLine();
            builder
                .Append("mode=").Append(modePlayers > 0 ? modePlayers + "p" : "-")
                .Append(" room=").Append(string.IsNullOrEmpty(MatchNetworkSession.RoomCode) ? "-" : MatchNetworkSession.RoomCode)
                .Append(" players=").Append(MatchNetworkSession.PlayerCount)
                .Append(" lobbySlots=").Append(MatchNetworkSession.LobbySlotCount)
                .Append(" phase=").Append(phase)
                .Append(" matchStarted=").Append(MatchNetworkSession.MatchStarted ? "1" : "0")
                .AppendLine();
            builder
                .Append("transport=")
                .Append(MatchNetworkSession.HasHandle ? MatchNetworkSession.TransportEndpointHint : "none")
                .AppendLine();
            builder
                .Append("ngo listening=").Append(nm != null && nm.IsListening ? "1" : "0")
                .Append(" connected=").Append(nm != null && nm.IsConnectedClient ? "1" : "0")
                .Append(" clients=").Append(clients)
                .AppendLine();
            builder
                .Append("migration rebinding=").Append(HostMigrationSession.IsRebinding ? "1" : "0")
                .Append(" prevHost=").Append(HostMigrationSession.PreviousHostSlot)
                .Append(" nextHost=").Append(HostMigrationSession.DesignatedHostSlot);
            return builder.ToString();
        }

        private static int ResolveModePlayerCount()
        {
            if (MatchNetworkSession.PlayerCount > 0)
            {
                return MatchNetworkSession.PlayerCount;
            }

            return MatchNetworkSession.LobbySlotCount;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Trim().Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
        }

        private static string ResolvePhase()
        {
            if (!MatchNetworkSession.IsNetworked)
            {
                return "Offline";
            }

            if (MatchNetworkSession.MatchStarted || MatchNetworkSession.NetworkMatchSimStarted)
            {
                return "Match";
            }

            if (MatchNetworkSession.IsNetworkRacePickActive)
            {
                return "RacePick";
            }

            if (MatchNetworkSession.HasNetworkLobby)
            {
                return "Lobby";
            }

            return "Connecting";
        }
    }
}
