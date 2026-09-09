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
            float centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius,
            bool autoFateBonuses = false)
        {
            PlayerCount = playerCount;
            RaceIds = raceIds;
            ArenaRadius = arenaRadius;
            MainToTowerDistance = mainToTowerDistance;
            CenterArenaRadius = centerArenaRadius;
            AutoFateBonuses = autoFateBonuses;
        }

        public int PlayerCount { get; }
        public float ArenaRadius { get; }
        public float MainToTowerDistance { get; }
        public float CenterArenaRadius { get; }
        public IReadOnlyList<string> RaceIds { get; }

        /// <summary>
        /// True when the auto-fate bonus (panel 0) and the pick deadline are rolled once the
        /// base-focus fly-in ends (early phase). Off by default so bare match setups stay
        /// deterministic; production match starts enable it via <see cref="FromSetup"/>.
        /// </summary>
        public bool AutoFateBonuses { get; }

        public static MatchConfig FromSetup(MatchSetup setup)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            return new MatchConfig(
                setup.PlayerCount,
                setup.RaceIds ?? CreateDefaultRaceIds(setup.PlayerCount),
                autoFateBonuses: true);
        }

        public static MatchConfig MvpDefault(
            int playerCount = MatchSetup.DefaultPlayerCount,
            bool autoFateBonuses = false)
        {
            return new MatchConfig(
                playerCount,
                CreateDefaultRaceIds(playerCount),
                autoFateBonuses: autoFateBonuses);
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
                races[i] = GameIds.Races.Human;
            }

            return races;
        }
    }
}
