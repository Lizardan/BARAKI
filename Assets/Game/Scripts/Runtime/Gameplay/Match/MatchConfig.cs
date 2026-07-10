using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Gameplay.Match
{
    public sealed class MatchConfig
    {
        public MatchConfig(
            int playerCount,
            IReadOnlyList<string> raceIds,
            float arenaRadius = MatchArenaGenerator.DefaultArenaRadius,
            float mainToTowerDistance = MatchArenaGenerator.DefaultMainToTowerDistance,
            float centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius)
        {
            PlayerCount = playerCount;
            RaceIds = raceIds;
            ArenaRadius = arenaRadius;
            MainToTowerDistance = mainToTowerDistance;
            CenterArenaRadius = centerArenaRadius;
        }

        public int PlayerCount { get; }
        public float ArenaRadius { get; }
        public float MainToTowerDistance { get; }
        public float CenterArenaRadius { get; }
        public IReadOnlyList<string> RaceIds { get; }

        public static MatchConfig FromSetup(MatchSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            return new MatchConfig(
                setup.PlayerCount,
                setup.RaceIds ?? CreateDefaultRaceIds(setup.PlayerCount));
        }

        public static MatchConfig MvpDefault(int playerCount = MatchSetup.DefaultPlayerCount)
        {
            return new MatchConfig(playerCount, CreateDefaultRaceIds(playerCount));
        }

        public string GetRaceId(int slotIndex)
        {
            if (RaceIds == null || slotIndex < 0 || slotIndex >= RaceIds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }

            return RaceIds[slotIndex];
        }

        private static string[] CreateDefaultRaceIds(int playerCount)
        {
            var races = new string[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                races[i] = i % 2 == 0 ? GameIds.Races.Human : GameIds.Races.Bug;
            }

            return races;
        }
    }
}
