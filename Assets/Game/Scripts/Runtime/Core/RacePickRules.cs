using System;

namespace Game.Core
{
    public static class RacePickRules
    {
        /// <summary>Races with shipped content (tests / future unlocks).</summary>
        public static readonly string[] PlayableRaceIds =
        {
            GameIds.Races.Human,
            GameIds.Races.Bug,
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
            GameIds.Races.Bug => "Жуки",
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
    }
}
