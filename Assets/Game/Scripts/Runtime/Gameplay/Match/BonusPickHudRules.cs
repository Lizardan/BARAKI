using System;
using Game.Gameplay.Networking;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Bonus overlay data source (PRE-001): local controller offline/host,
    /// authoritative snapshot on pure clients. Mirrors <see cref="MatchHudGoldRules"/>.
    /// Two windows: panel 0 = auto-fate pick (rolled when the two windows open, i.e. once the
    /// base-focus fly-in ends), panel 1 = choice from the offered subset of
    /// <see cref="BonusPickRules.OfferSize"/> slots.
    /// </summary>
    public static class BonusPickHudRules
    {
        /// <summary>
        /// Reads the local player's bonus pick slot from the snapshot by panel.
        /// <paramref name="panel"/> 0 = auto pick, 1 = chosen pick. None when absent.
        /// </summary>
        public static bool TryGetSnapshotBonusPick(
            MatchSnapshot snapshot,
            int localSlot,
            int panel,
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
                    bonusSlot = panel == 1
                        ? snapshot.Players[i].BonusPickSlot2
                        : snapshot.Players[i].BonusPickSlot;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Reads the local player's offered subset for the second window. Empty when absent.</summary>
        public static bool TryGetSnapshotBonusOffer(
            MatchSnapshot snapshot,
            int localSlot,
            out int[] offer)
        {
            offer = Array.Empty<int>();
            if (snapshot?.Players == null)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Players.Length; i++)
            {
                if (snapshot.Players[i].Slot == localSlot)
                {
                    offer = snapshot.Players[i].BonusPickOfferSlots ?? Array.Empty<int>();
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

        /// <summary>
        /// True while the local bonus pick window is open (deadline counting, no choice yet).
        /// Keyed on the second (choice) pick: the auto-fate pick is always set straight away.
        /// </summary>
        public static bool IsPickWindowOpen(float deadlineSeconds, int ownPickSlot) =>
            deadlineSeconds > 0f && ownPickSlot == BonusPickRules.NoneSlot;

        /// <summary>
        /// Clients prefer the snapshot when it has arrived; otherwise they keep the local
        /// controller values so the overlay is not blank until the first snapshot (or after a
        /// dropped snapshot RPC). The local deadline stays 0 until the early-phase roll, so the
        /// overlay stays hidden while the camera drives to the base.
        /// </summary>
        public static void ResolveOverlay(
            bool useSnapshot,
            MatchSnapshot snapshot,
            int localSlot,
            float controllerDeadline,
            int controllerAutoPick,
            int controllerPick2,
            int[] controllerOffer,
            out float deadlineSeconds,
            out int autoPick,
            out int pick2,
            out int[] offer)
        {
            deadlineSeconds = controllerDeadline;
            autoPick = controllerAutoPick;
            pick2 = controllerPick2;
            offer = controllerOffer ?? Array.Empty<int>();
            if (!useSnapshot)
            {
                return;
            }

            if (TryGetSnapshotDeadline(snapshot, out var snapshotDeadline))
            {
                deadlineSeconds = snapshotDeadline;
            }

            if (TryGetSnapshotBonusPick(snapshot, localSlot, panel: 0, out var snapshotAuto))
            {
                autoPick = snapshotAuto;
            }

            if (TryGetSnapshotBonusPick(snapshot, localSlot, panel: 1, out var snapshotPick2))
            {
                pick2 = snapshotPick2;
            }

            if (TryGetSnapshotBonusOffer(snapshot, localSlot, out var snapshotOffer))
            {
                offer = snapshotOffer;
            }
        }
    }
}