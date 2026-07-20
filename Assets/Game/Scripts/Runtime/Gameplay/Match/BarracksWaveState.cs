namespace Game.Gameplay.Match
{
    public sealed class BarracksWaveState
    {
        public BarracksWaveState(
            int ownerSlot,
            string ownerRaceId,
            string barracksId,
            string laneId,
            int laneIndex,
            float timeUntilNextWave,
            float waveIntervalSeconds)
        {
            OwnerSlot = ownerSlot;
            OwnerRaceId = ownerRaceId;
            BarracksId = barracksId;
            LaneId = laneId;
            LaneIndex = laneIndex;
            TimeUntilNextWaveSeconds = timeUntilNextWave;
            WaveIntervalSeconds = waveIntervalSeconds;
            Level = 1;
            FrozenSquadLevel = 1;
            IsRuins = false;
            IsSpawnEnabled = true;
            CallCharges = new BarracksCallChargeState();
        }

        public int OwnerSlot { get; }
        public string OwnerRaceId { get; }
        public string BarracksId { get; }
        public string LaneId { get; }
        public int LaneIndex { get; }
        public int Level { get; set; }
        public int FrozenSquadLevel { get; set; }
        public bool IsRuins { get; set; }
        public bool IsSpawnEnabled { get; set; }
        public float TimeUntilNextWaveSeconds { get; set; }
        public float WaveIntervalSeconds { get; private set; }
        public BarracksCallChargeState CallCharges { get; }

        public int EffectiveSquadLevel =>
            BarracksWaveRules.GetEffectiveSquadLevel(Level, IsRuins, FrozenSquadLevel);

        public void RefreshInterval()
        {
            WaveIntervalSeconds = BarracksWaveRules.GetWaveIntervalSeconds(Level, IsRuins, OwnerRaceId);
        }
    }
}
