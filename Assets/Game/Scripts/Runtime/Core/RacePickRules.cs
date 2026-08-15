using System;
using System.Collections.Generic;

namespace Game.Core
{
    public static class RacePickRules
    {
        /// <summary>Races with shipped content (tests / future unlocks).</summary>
        public static readonly string[] PlayableRaceIds =
        {
            GameIds.Races.Human,
        };

        /// <summary>Races allowed in race pick UI and bot fill (playtest gate).</summary>
        public static readonly string[] SelectableRaceIds =
        {
            GameIds.Races.Human,
        };

        public static bool IsPlayable(string raceId)
        {
            for (var i = 0; i < PlayableRaceIds.Length; i++)
            {
                if (PlayableRaceIds[i] == raceId)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsSelectable(string raceId)
        {
            for (var i = 0; i < SelectableRaceIds.Length; i++)
            {
                if (SelectableRaceIds[i] == raceId)
                {
                    return true;
                }
            }

            return false;
        }

        public static string PickRandomRace(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (SelectableRaceIds.Length == 0)
            {
                throw new InvalidOperationException("No selectable races.");
            }

            var index = random.Next(SelectableRaceIds.Length);
            return SelectableRaceIds[index];
        }

        public static string GetDisplayName(string raceId) => raceId switch
        {
            GameIds.Races.Human => "Люди",
            _ => raceId,
        };
    }

    /// <summary>Pure rules for replicated pre-match race selection.</summary>
    public static class RacePickNetworkRules
    {
        public static bool TryApplyPick(string[] picks, int slot, string raceId)
        {
            if (picks == null)
            {
                throw new ArgumentNullException(nameof(picks));
            }

            if (slot < 0 || slot >= picks.Length)
            {
                return false;
            }

            if (!RacePickRules.IsSelectable(raceId))
            {
                return false;
            }

            picks[slot] = raceId;
            return true;
        }

        public static bool IsComplete(string[] picks)
        {
            if (picks == null || picks.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < picks.Length; i++)
            {
                if (string.IsNullOrEmpty(picks[i]) || !RacePickRules.IsSelectable(picks[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static string[] ToRaceIdsArray(string[] picks)
        {
            if (!IsComplete(picks))
            {
                throw new InvalidOperationException("Race picks are incomplete.");
            }

            var copy = new string[picks.Length];
            Array.Copy(picks, copy, picks.Length);
            return copy;
        }

        /// <summary>
        /// Session must be rebuilt only before sim start, and only when player count or
        /// pick-list length drifted. Never wipe picks after <c>matchSimStarted</c> — a late
        /// EnsureSession/RPC would otherwise reset the match for everyone.
        /// </summary>
        public static bool ShouldReinitializeSession(
            int currentPlayerCount,
            int requestedPlayerCount,
            int pickCount,
            bool matchSimStarted) =>
            !matchSimStarted
            && (currentPlayerCount != requestedPlayerCount || pickCount != requestedPlayerCount);

        public static bool FillLocalStandInPicks(string[] picks, IReadOnlyList<bool> localStandInSlots)
        {
            if (picks == null)
            {
                throw new ArgumentNullException(nameof(picks));
            }

            if (localStandInSlots == null)
            {
                throw new ArgumentNullException(nameof(localStandInSlots));
            }

            if (RacePickRules.SelectableRaceIds.Length == 0)
            {
                throw new InvalidOperationException("No selectable races.");
            }

            var changed = false;
            var count = Math.Min(picks.Length, localStandInSlots.Count);
            for (var slot = 0; slot < count; slot++)
            {
                if (!localStandInSlots[slot] || !string.IsNullOrEmpty(picks[slot]))
                {
                    continue;
                }

                picks[slot] = RacePickRules.SelectableRaceIds[slot % RacePickRules.SelectableRaceIds.Length];
                changed = true;
            }

            return changed;
        }
    }

    /// <summary>
    /// Claims a pending network race pick before submit so reentrant change callbacks cannot loop.
    /// </summary>
    public static class RacePickPendingSubmitRules
    {
        public static bool TryClaimPending(ref string pendingRaceId, out string raceId)
        {
            raceId = pendingRaceId;
            if (string.IsNullOrEmpty(pendingRaceId))
            {
                return false;
            }

            pendingRaceId = null;
            return true;
        }

        public static void ApplySubmitResult(
            bool accepted,
            string raceId,
            ref string pendingRaceId,
            ref bool localSubmitted)
        {
            if (accepted)
            {
                localSubmitted = true;
                return;
            }

            pendingRaceId = raceId;
        }
    }
}
