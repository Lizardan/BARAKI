namespace Game.Gameplay.Match
{
    public readonly struct BuildingDestroyedEvent
    {
        public BuildingDestroyedEvent(int ownerSlot, string buildingId, int buildingInstanceId, int attackerOwnerSlot)
        {
            OwnerSlot = ownerSlot;
            BuildingId = buildingId;
            BuildingInstanceId = buildingInstanceId;
            AttackerOwnerSlot = attackerOwnerSlot;
        }

        public int OwnerSlot { get; }
        public string BuildingId { get; }
        public int BuildingInstanceId { get; }
        public int AttackerOwnerSlot { get; }
    }
}
