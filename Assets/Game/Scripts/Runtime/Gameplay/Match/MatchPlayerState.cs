using System;

namespace Game.Gameplay.Match
{
    public sealed class MatchPlayerState
    {
        public MatchPlayerState(int slotIndex, string raceId, int startingGold)
        {
            SlotIndex = slotIndex;
            RaceId = raceId;
            Gold = startingGold;
            SyncMainManaMax(fillToMax: true);
        }

        public int SlotIndex { get; }
        public string RaceId { get; }
        public int Gold { get; set; }
        public int MainLevel { get; set; } = MatchEconomyRules.DefaultMainLevel;
        public int PassiveGoldLevel { get; set; }
        public int MagicLevel { get; set; }
        public int MeleeDamageLevel { get; set; }
        public int RangedDamageLevel { get; set; }
        public int HpArmorLevel { get; set; }
        public float PassiveGoldTickRemainingSeconds { get; set; } =
            MatchEconomyRules.PassiveGoldTickIntervalSeconds;
        public bool IsEliminated { get; set; }
        /// <summary>True after <c>UPG_MAIN_DIVINE_BLESSING</c> completes (FoW off for owner).</summary>
        public bool DivineBlessingComplete { get; set; }
        /// <summary>
        /// Chosen main extra ability id (1..6); <see cref="MainExtraAbilityRules.None"/> until picked.
        /// </summary>
        public int MainExtraAbilityId { get; set; }
        /// <summary>Chosen bonus slot (1..12: units, veterans, race uniques); <see cref="BonusPickRules.NoneSlot"/> until picked.</summary>
        public int BonusPickSlot { get; set; }
        public float MainMana { get; set; }
        public float MainManaMax { get; private set; }
        public float MainExtraAbilityCooldownRemaining { get; set; }
        /// <summary>Building ability cooldowns (MAIN-001), replicated in Players v23.</summary>
        public float IceRingCooldownRemaining { get; set; }
        /// <summary>Building ability cooldowns (MAIN-001), replicated in Players v23.</summary>
        public float WaveOfLightCooldownRemaining { get; set; }
        /// <summary>Tower upgrade track levels (PRE-007), index = <see cref="TowerTrackRules"/> order.</summary>
        public int[] TowerTrackLevels { get; private set; } = new int[TowerTrackRules.TrackCount];

        /// <summary>Track level by canonical index, clamped to the max level.</summary>
        public int GetTowerTrackLevel(int trackIndex) =>
            TowerTrackRules.GetLevel(TowerTrackLevels, trackIndex);

        /// <summary>Track level by upgrade id, clamped to the max level.</summary>
        public int GetTowerTrackLevel(string upgradeId) =>
            TowerTrackRules.GetLevel(TowerTrackLevels, upgradeId);

        public void SetTowerTrackLevels(int[] levels)
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            if (TowerTrackLevels == null || TowerTrackLevels.Length != levels.Length)
            {
                TowerTrackLevels = new int[levels.Length];
            }

            for (var i = 0; i < levels.Length; i++)
            {
                TowerTrackLevels[i] = Math.Max(0, levels[i]);
            }
        }

        public void SyncMainManaMax(bool fillToMax = false)
        {
            var previousMax = MainManaMax;
            MainManaMax = MainExtraAbilityRules.GetMainManaMax(MainLevel);
            if (fillToMax)
            {
                MainMana = MainManaMax;
                return;
            }

            var delta = MainManaMax - previousMax;
            if (delta > 0f)
            {
                MainMana += delta;
            }

            if (MainMana > MainManaMax)
            {
                MainMana = MainManaMax;
            }
        }
    }
}
