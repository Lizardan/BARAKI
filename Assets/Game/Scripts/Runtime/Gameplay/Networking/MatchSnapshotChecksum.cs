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
                Mix(ref hash, (int)snapshot.BonusPickDeadlineSeconds);
                Mix(ref hash, snapshot.Units?.Length ?? 0);

                if (snapshot.Players != null)
                {
                    for (var i = 0; i < snapshot.Players.Length; i++)
                    {
                        Mix(ref hash, snapshot.Players[i].Slot);
                        Mix(ref hash, snapshot.Players[i].Gold);
                        Mix(ref hash, snapshot.Players[i].BonusPickSlot);
                        Mix(ref hash, snapshot.Players[i].BonusPickSlot2);
                        var offer = snapshot.Players[i].BonusPickOfferSlots;
                        Mix(ref hash, offer?.Length ?? 0);
                        if (offer != null)
                        {
                            for (var o = 0; o < offer.Length; o++)
                            {
                                Mix(ref hash, offer[o]);
                            }
                        }
                        Mix(ref hash, snapshot.Players[i].IsEliminated ? 1 : 0);
                        Mix(ref hash, (int)snapshot.Players[i].TitanResearchProgressSeconds);
                        Mix(ref hash, snapshot.Players[i].TitanState);
                        Mix(ref hash, snapshot.Players[i].TitanUnlocked ? 1 : 0);
                        Mix(ref hash, snapshot.Players[i].TitanLevel);
                        Mix(ref hash, snapshot.Players[i].TitanXp);
                        Mix(ref hash, snapshot.Players[i].DivineBlessingComplete ? 1 : 0);
                        Mix(ref hash, snapshot.Players[i].MainExtraAbilityId);
                        Mix(ref hash, (int)snapshot.Players[i].MainMana);
                        Mix(ref hash, (int)snapshot.Players[i].MainExtraAbilityCooldownRemaining);
                        Mix(ref hash, (int)snapshot.Players[i].IceRingCooldownRemaining);
                        Mix(ref hash, (int)snapshot.Players[i].WaveOfLightCooldownRemaining);
                    }
                }

                if (snapshot.Heroes != null)
                {
                    for (var i = 0; i < snapshot.Heroes.Length; i++)
                    {
                        Mix(ref hash, snapshot.Heroes[i].OwnerSlot);
                        Mix(ref hash, snapshot.Heroes[i].HeroSlot);
                        Mix(ref hash, snapshot.Heroes[i].State);
                        Mix(ref hash, snapshot.Heroes[i].Level);
                        Mix(ref hash, snapshot.Heroes[i].Xp);
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
