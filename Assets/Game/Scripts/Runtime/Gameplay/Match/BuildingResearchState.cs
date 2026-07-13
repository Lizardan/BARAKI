namespace Game.Gameplay.Match
{
    public sealed class BuildingResearchState
    {
        public BuildingResearchState(
            int buildingInstanceId,
            int ownerSlot,
            string buildingId,
            string upgradeId,
            int costPaid,
            float durationSeconds)
        {
            BuildingInstanceId = buildingInstanceId;
            OwnerSlot = ownerSlot;
            BuildingId = buildingId;
            UpgradeId = upgradeId;
            CostPaid = costPaid;
            DurationSeconds = durationSeconds;
            RemainingSeconds = durationSeconds;
        }

        public int BuildingInstanceId { get; }
        public int OwnerSlot { get; }
        public string BuildingId { get; }
        public string UpgradeId { get; }
        public int CostPaid { get; }
        public float DurationSeconds { get; }
        public float RemainingSeconds { get; set; }

        public float Progress01 =>
            DurationSeconds <= 0f
                ? 1f
                : 1f - (RemainingSeconds / DurationSeconds);
    }
}
