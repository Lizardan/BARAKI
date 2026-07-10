namespace Game.Gameplay.Match.Selection
{
    public readonly struct MatchPickTarget
    {
        public MatchPickTarget(MatchPickTargetKind kind, int entityId)
        {
            Kind = kind;
            EntityId = entityId;
        }

        public MatchPickTargetKind Kind { get; }
        public int EntityId { get; }

        public bool IsUnit => Kind == MatchPickTargetKind.Unit;
        public bool IsBuilding => Kind == MatchPickTargetKind.Building;
        public bool HasTarget => Kind != MatchPickTargetKind.None;

        public static MatchPickTarget None => new(MatchPickTargetKind.None, -1);

        public static MatchPickTarget Unit(int unitId) => new(MatchPickTargetKind.Unit, unitId);

        public static MatchPickTarget Building(int buildingInstanceId) =>
            new(MatchPickTargetKind.Building, buildingInstanceId);
    }
}
