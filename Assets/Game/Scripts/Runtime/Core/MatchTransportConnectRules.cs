namespace Game.Core
{
    /// <summary>Pure rules for NGO client connect wait / fail messaging.</summary>
    public static class MatchTransportConnectRules
    {
        public const float DefaultTimeoutSeconds = 15f;

        public const string ConnectFailedMessage =
            "Нет связи с хостом (Relay/LAN). Проверь код лобби и сеть.";

        public static bool IsConnectComplete(bool isConnectedClient, bool hasNetworkLobby) =>
            isConnectedClient && hasNetworkLobby;

        public static bool HasTimedOut(float elapsedSeconds, float timeoutSeconds = DefaultTimeoutSeconds) =>
            elapsedSeconds >= timeoutSeconds;
    }
}
