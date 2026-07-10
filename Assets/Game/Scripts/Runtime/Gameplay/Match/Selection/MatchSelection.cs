using System;

namespace Game.Gameplay.Match.Selection
{
    public sealed class MatchSelection
    {
        public event Action<MatchPickTarget> Changed;

        public MatchPickTarget Current { get; private set; } = MatchPickTarget.None;

        public void Select(MatchPickTarget target)
        {
            if (Current.Kind == target.Kind && Current.EntityId == target.EntityId)
            {
                return;
            }

            Current = target;
            Changed?.Invoke(Current);
        }

        public void SelectUnit(int unitId) => Select(MatchPickTarget.Unit(unitId));

        public void SelectBuilding(int buildingInstanceId) =>
            Select(MatchPickTarget.Building(buildingInstanceId));

        public void Clear() => Select(MatchPickTarget.None);
    }
}
