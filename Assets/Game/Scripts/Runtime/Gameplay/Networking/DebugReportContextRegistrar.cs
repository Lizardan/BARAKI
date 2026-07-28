using System.Text;
using Game.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Networking
{
    /// <summary>Registers NET snapshot for playtest GitHub Issue reports.</summary>
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

            var phase = SessionFlowRules.ToFlowId(ResolveFlow());
            var clients = nm != null && nm.IsListening
                ? nm.ConnectedClientsIds.Count
                : 0;
            ResolveFilledReady(out var filled, out var capacity, out var ready);
            var matchSim = MatchNetworkSession.NetworkMatchSimStarted;
            var matchElapsed = string.Empty;
            if (phase == nameof(SessionFlowState.Match))
            {
                var runtime = Object.FindAnyObjectByType<Match.MatchRuntime>();
                var seconds = runtime?.Controller?.MatchTimeSeconds ?? 0f;
                matchElapsed = SessionFlowRules.ResolveElapsedBucket(seconds);
            }

            var builder = new StringBuilder(256);
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
                .Append(" filled=").Append(filled).Append('/').Append(capacity > 0 ? capacity : 0)
                .Append(" ready=").Append(ready).Append('/').Append(capacity > 0 ? capacity : 0)
                .Append(" phase=").Append(phase)
                .Append(" matchStarted=").Append(MatchNetworkSession.MatchStarted ? "1" : "0")
                .Append(" matchSim=").Append(matchSim ? "1" : "0")
                .Append(" matchElapsed=").Append(string.IsNullOrEmpty(matchElapsed) ? "-" : matchElapsed)
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

        private static SessionFlowState ResolveFlow() =>
            SessionFlowRules.Resolve(
                SceneManager.GetActiveScene().name,
                MatchNetworkSession.IsNetworked,
                MatchNetworkSession.HasNetworkLobby,
                MatchNetworkSession.MatchStarted,
                MatchNetworkSession.NetworkMatchSimStarted);

        private static void ResolveFilledReady(out int filled, out int capacity, out int ready)
        {
            filled = 0;
            capacity = 0;
            ready = 0;

            if (MatchNetworkSession.HasNetworkLobby)
            {
                capacity = MatchNetworkSession.LobbySlotCount;
                for (var i = 0; i < capacity; i++)
                {
                    var slot = MatchNetworkSession.GetLobbySlot(i);
                    if (!slot.IsOccupied)
                    {
                        continue;
                    }

                    filled++;
                    if (slot.IsReady)
                    {
                        ready++;
                    }
                }

                return;
            }

            var local = LocalMatchRegistry.Active;
            if (local == null)
            {
                capacity = MatchNetworkSession.PlayerCount;
                filled = capacity > 0 ? 1 : 0;
                return;
            }

            capacity = local.SlotCount;
            filled = LobbyReadyRules.CountOccupied(local);
            ready = LobbyReadyRules.CountReady(local);
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

            return value.Trim().Replace('\r', ' ').Replace(' ', '_').Replace('\n', ' ');
        }
    }
}
