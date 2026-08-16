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
        /// <summary>Chosen unit bonus slot (1..6); <see cref="BonusPickRules.NoneSlot"/> until picked.</summary>
        public int BonusPickSlot { get; set; }
        public float MainMana { get; set; }
        public float MainManaMax { get; private set; }
        public float MainExtraAbilityCooldownRemaining { get; set; }

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
