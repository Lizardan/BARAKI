using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    public interface ICombatUnitCatalog
    {
        RaceDefinition GetRace(string raceId);
        SquadCompositionDefinition GetSquad(int barracksLevel);
    }

    public sealed class RaceCatalogCombatCatalog : ICombatUnitCatalog
    {
        readonly RaceCatalog _catalog;

        public RaceCatalogCombatCatalog(RaceCatalog catalog)
        {
            _catalog = catalog;
        }

        public RaceDefinition GetRace(string raceId) => _catalog.GetRace(raceId);

        public SquadCompositionDefinition GetSquad(int barracksLevel) => _catalog.GetSquad(barracksLevel);
    }
}
