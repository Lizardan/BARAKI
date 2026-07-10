using System;

namespace Game.Core
{
    public static class RacePickRules
    {
        public static readonly string[] PlayableRaceIds =
        {
            GameIds.Races.Human,
            GameIds.Races.Bug,
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

        public static string PickRandomRace(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var index = random.Next(PlayableRaceIds.Length);
            return PlayableRaceIds[index];
        }

        public static string GetDisplayName(string raceId) => raceId switch
        {
            GameIds.Races.Human => "Люди",
            GameIds.Races.Bug => "Жуки",
            _ => raceId,
        };
    }
}
