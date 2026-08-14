using Game.Gameplay.Networking;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Bonus overlay data source (PRE-001): local controller offline/host,
    /// authoritative snapshot on pure clients. Mirrors <see cref="MatchHudGoldRules"/>.
    /// </summary>
    public static class BonusPickHudRules
    {
        /// <summary>Reads the local player's bonus pick slot from the snapshot. None when absent.</summary>
        public static bool TryGetSnapshotBonusPick(
            MatchSnapshot snapshot,
            int localSlot,
            out int bonusSlot)
        {
            bonusSlot = BonusPickRules.NoneSlot;
            if (snapshot?.Players == null)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Players.Length; i++)
            {
                if (snapshot.Players[i].Slot == localSlot)
                {
                    bonusSlot = snapshot.Players[i].BonusPickSlot;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Reads the overlay deadline from the snapshot. 0 when snapshot absent.</summary>
        public static bool TryGetSnapshotDeadline(
            MatchSnapshot snapshot,
            out float deadlineSeconds)
        {
            deadlineSeconds = 0f;
            if (snapshot == null)
            {
                return false;
            }

            deadlineSeconds = snapshot.BonusPickDeadlineSeconds;
            return true;
        }

        /// <summary>True while the local bonus pick window is open (deadline counting, no pick yet).</summary>
        public static bool IsPickWindowOpen(float deadlineSeconds, int ownPickSlot) =>
            deadlineSeconds > 0f && ownPickSlot == BonusPickRules.NoneSlot;
    }
}
