using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Gameplay.Data
{
    [CreateAssetMenu(fileName = "RaceCatalog", menuName = "Game/Race Catalog")]
    public sealed class RaceCatalog : ScriptableObject
    {
        [SerializeField] private RaceDefinition[] _races;
        [SerializeField] private SquadCompositionDefinition[] _squadCompositions;
        [SerializeField] private StatUpgradeTrackDefinition[] _statTracks;

        public IReadOnlyList<RaceDefinition> Races => _races;
        public IReadOnlyList<SquadCompositionDefinition> SquadCompositions => _squadCompositions;
        public IReadOnlyList<StatUpgradeTrackDefinition> StatTracks => _statTracks;

        public RaceDefinition GetRace(string raceId) =>
            _races?.FirstOrDefault(r => r != null && r.Id == raceId);

        public SquadCompositionDefinition GetSquad(int barracksLevel) =>
            _squadCompositions?.FirstOrDefault(s => s != null && s.BarracksLevel == barracksLevel);

        public StatUpgradeTrackDefinition GetStatTrack(string trackId) =>
            _statTracks?.FirstOrDefault(t => t != null && t.Id == trackId);
    }
}
