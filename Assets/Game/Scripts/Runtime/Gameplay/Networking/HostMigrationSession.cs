namespace Game.Gameplay.Networking
{
    /// <summary>Process-wide flags while listen-host rebinds after host loss.</summary>
    public static class HostMigrationSession
    {
        public static bool IsRebinding { get; private set; }
        public static int DesignatedHostSlot { get; private set; } = -1;
        public static int PreviousHostSlot { get; private set; } = -1;
        public static byte[] LastGoodBytes { get; private set; }
        public static string PreviousRelayJoinCode { get; private set; } = string.Empty;

        public static void Begin(
            int previousHostSlot,
            int designatedHostSlot,
            byte[] lastGoodBytes,
            string previousRelayJoinCode)
        {
            IsRebinding = true;
            PreviousHostSlot = previousHostSlot;
            DesignatedHostSlot = designatedHostSlot;
            LastGoodBytes = lastGoodBytes;
            PreviousRelayJoinCode = previousRelayJoinCode ?? string.Empty;
        }

        public static void Clear()
        {
            IsRebinding = false;
            DesignatedHostSlot = -1;
            PreviousHostSlot = -1;
            LastGoodBytes = null;
            PreviousRelayJoinCode = string.Empty;
        }
    }
}
