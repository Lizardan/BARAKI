using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Lobby → match handoff: player count and optional race picks.</summary>
    public sealed class MatchSetup
    {
        public const int DefaultPlayerCount = 4;
        public const int DefaultLocalPlayerSlot = 0;

        public MatchSetup(
            int playerCount = DefaultPlayerCount,
            int localPlayerSlot = DefaultLocalPlayerSlot,
            IReadOnlyList<string> raceIds = null)
        {
            PlayerCount = Math.Clamp(playerCount, 2, 8);
            LocalPlayerSlot = Math.Clamp(localPlayerSlot, 0, PlayerCount - 1);
            RaceIds = raceIds;
        }

        public int PlayerCount { get; }
        public int LocalPlayerSlot { get; }
        public IReadOnlyList<string> RaceIds { get; }

        public static MatchSetup Default => new MatchSetup();
    }
}
