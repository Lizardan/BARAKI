using Unity.Services.Relay.Models;

namespace Game.Gameplay.Networking
{
    /// <summary>Holds Relay allocation between session create/join and NGO StartAsHost/Client.</summary>
    public static class MatchRelayTransportState
    {
        public static Allocation HostAllocation { get; private set; }
        public static JoinAllocation ClientAllocation { get; private set; }
        public static string RelayJoinCode { get; private set; } = string.Empty;
        public static bool HasHostAllocation => HostAllocation != null;
        public static bool HasClientAllocation => ClientAllocation != null;

        public static void SetHost(Allocation allocation, string joinCode)
        {
            HostAllocation = allocation;
            ClientAllocation = null;
            RelayJoinCode = joinCode ?? string.Empty;
        }

        public static void SetClient(JoinAllocation allocation, string joinCode)
        {
            ClientAllocation = allocation;
            HostAllocation = null;
            RelayJoinCode = joinCode ?? string.Empty;
        }

        public static void Clear()
        {
            HostAllocation = null;
            ClientAllocation = null;
            RelayJoinCode = string.Empty;
        }
    }
}
