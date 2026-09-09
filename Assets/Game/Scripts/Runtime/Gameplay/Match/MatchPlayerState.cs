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
        /// <summary>Chosen slot of the second (choice) window (1..12); <see cref="BonusPickRules.NoneSlot"/> until picked or timed out.</summary>
        public int BonusPickSlot2 { get; set; }
        /// <summary>Random subset (0..<see cref="BonusPickRules.OfferSize"/>) offered in the second window; excludes the auto pick.</summary>
        public int[] BonusPickOfferSlots { get; private set; } = Array.Empty<int>();
        /// <summary>
        /// Permutation of hero slots 1..3 set after the bonus pick (order pick): position in the
        /// order = main level at which the hero unlocks for hire. Default <c>[1,2,3]</c>.
        /// </summary>
        public int[] HeroOrder { get; set; } = HeroOrderPickRules.DefaultOrder();
        /// <summary>True once the player confirmed the order (host applies; snapshot replicates).</summary>
        public bool HeroOrderConfirmed { get; set; }

        /// <summary>Applies and confirms a valid hero order. Invalid orders are ignored.</summary>
        public void ConfirmHeroOrder(int[] order)
        {
            if (!HeroOrderPickRules.IsValidOrder(order))
            {
                return;
            }

            HeroOrder = order;
            HeroOrderConfirmed = true;
        }

        public void SetBonusPickOffer(int[] offer)
        {
            if (offer == null || offer.Length == 0)
            {
                return;
            }

            BonusPickOfferSlots = offer;
        }

        /// <summary>True when either pick equals <paramref name="slot"/> (auto and chosen bonuses stack).</summary>
        public bool HasBonusEffective(int slot) => BonusPickSlot == slot || BonusPickSlot2 == slot;
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
