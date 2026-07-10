using System;
using System.Collections.Generic;
using Game.Gameplay.Combat;

namespace Game.Gameplay.Match
{
    public sealed class EliminationService
    {
        public event Action<int> PlayerEliminated;

        public void HandleBuildingDestroyed(
            BuildingDestroyedEvent destroyed,
            BuildingRegistry buildings,
            IList<MatchPlayerState> players,
            BarracksWaveScheduler waveScheduler,
            MatchCombatSystem combat)
        {
            if (destroyed.OwnerSlot < 0
                || destroyed.OwnerSlot >= players.Count
                || players[destroyed.OwnerSlot].IsEliminated)
            {
                return;
            }

            if (BuildingRules.IsBarracks(destroyed.BuildingId))
            {
                waveScheduler.SetBarracksRuins(destroyed.OwnerSlot, destroyed.BuildingId);
            }

            if (!buildings.AreAllBuildingsRuined(destroyed.OwnerSlot))
            {
                return;
            }

            EliminatePlayer(destroyed.OwnerSlot, players, waveScheduler, combat);
        }

        public int? TryGetLastStandingWinner(IReadOnlyList<MatchPlayerState> players)
        {
            if (players == null)
            {
                return null;
            }

            int? winner = null;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].IsEliminated)
                {
                    continue;
                }

                if (winner.HasValue)
                {
                    return null;
                }

                winner = i;
            }

            return winner;
        }

        private void EliminatePlayer(
            int ownerSlot,
            IList<MatchPlayerState> players,
            BarracksWaveScheduler waveScheduler,
            MatchCombatSystem combat)
        {
            var player = players[ownerSlot];
            if (player.IsEliminated)
            {
                return;
            }

            player.IsEliminated = true;
            waveScheduler.SetPlayerSpawnEnabled(ownerSlot, false);
            combat?.DespawnUnitsForOwner(ownerSlot);
            PlayerEliminated?.Invoke(ownerSlot);
        }
    }
}
