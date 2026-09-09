using System;
using Game.Gameplay.Networking;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Hero order panel data source (order pick): local controller offline/host, authoritative
    /// snapshot on pure clients. Mirrors <see cref="BonusPickHudRules"/>. The order + confirmed
    /// flag replicate inside the Players section of the snapshot (v25).
    /// </summary>
    public static class HeroOrderHudRules
    {
        /// <summary>Reads the local player's hero order and confirmed flag from the snapshot. False when absent.</summary>
        public static bool TryGetSnapshotHeroOrder(
            MatchSnapshot snapshot,
            int localSlot,
            out int[] order,
            out bool confirmed)
        {
            order = HeroOrderPickRules.DefaultOrder();
            confirmed = false;
            if (snapshot?.Players == null)
            {
                return false;
            }

            for (var i = 0; i < snapshot.Players.Length; i++)
            {
                if (snapshot.Players[i].Slot == localSlot)
                {
                    if (HeroOrderPickRules.IsValidOrder(snapshot.Players[i].HeroOrder))
                    {
                        order = snapshot.Players[i].HeroOrder;
                    }

                    confirmed = snapshot.Players[i].HeroOrderConfirmed;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the source of truth for the local hero order. On pure clients the snapshot
        /// is preferred (same rule as bonus pick); fallback keeps the controller values so the
        /// panel is never blank before the first snapshot.
        /// </summary>
        public static void ResolveHeroOrder(
            bool useSnapshot,
            MatchSnapshot snapshot,
            int localSlot,
            int[] controllerOrder,
            bool controllerConfirmed,
            out int[] order,
            out bool confirmed)
        {
            order = controllerOrder ?? HeroOrderPickRules.DefaultOrder();
            confirmed = controllerConfirmed;
            if (!useSnapshot)
            {
                return;
            }

            if (TryGetSnapshotHeroOrder(snapshot, localSlot, out var snapshotOrder, out var snapshotConfirmed))
            {
                order = snapshotOrder;
                confirmed = snapshotConfirmed;
            }
        }

        /// <summary>
        /// True while the hero order panel is shown: the bonus pick is already resolved (the
        /// second choice window has closed, either by a pick or by the deadline auto-assign) and
        /// the local order is not confirmed yet. Requires the resolved flag rather than only the
        /// "window open" state: before the bonus phase starts the deadline is still 0 and the
        /// window would count as closed, which would flash the hero panel too early.
        /// </summary>
        public static bool IsOrderWindowOpen(bool confirmed, bool bonusPickResolved)
        {
            if (confirmed)
            {
                return false;
            }

            return bonusPickResolved;
        }
    }
}