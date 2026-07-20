namespace Game.Gameplay.Networking
{
    /// <summary>Lightweight debug hash for listen-host desync detection (not peer sim).</summary>
    public static class MatchSnapshotChecksum
    {
        public static uint Compute(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261u;
                Mix(ref hash, snapshot.PlayerCount);
                Mix(ref hash, snapshot.Phase);
                Mix(ref hash, snapshot.Units?.Length ?? 0);

                if (snapshot.Players != null)
                {
                    for (var i = 0; i < snapshot.Players.Length; i++)
                    {
                        Mix(ref hash, snapshot.Players[i].Slot);
                        Mix(ref hash, snapshot.Players[i].Gold);
                        Mix(ref hash, snapshot.Players[i].IsEliminated ? 1 : 0);
                    }
                }

                if (snapshot.Buildings != null)
                {
                    for (var i = 0; i < snapshot.Buildings.Length; i++)
                    {
                        Mix(ref hash, snapshot.Buildings[i].InstanceId);
                        Mix(ref hash, (int)snapshot.Buildings[i].Health);
                    }
                }

                return hash;
            }
        }

        public static bool Matches(MatchSnapshot snapshot, uint expected) =>
            expected == 0u || Compute(snapshot) == expected;

        static void Mix(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
            }
        }
    }
}
