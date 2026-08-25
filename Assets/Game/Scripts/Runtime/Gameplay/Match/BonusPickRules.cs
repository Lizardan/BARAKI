using System;
using Game.Core;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Pure rules for the post-race-pick bonus overlay (PRE-001).
    /// See <c>GameDesign/Bonuses.md</c>.
    /// </summary>
    public static class BonusPickRules
    {
        /// <summary>Number of bonus slots per race (Bonuses.md: 12).</summary>
        public const int SlotCount = 12;

        /// <summary>Overlay duration in seconds; match keeps running (Bonuses.md).</summary>
        public const float OverlayDurationSeconds = 60f;

        /// <summary>Exactly one bonus per player.</summary>
        public const int MaxPicksPerPlayer = 1;

        /// <summary>Slot index meaning "nothing picked yet" (reserved).</summary>
        public const int NoneSlot = 0;

        /// <summary>
        /// Slot numbers in UI display order (PRE-001): Titan, Race #1, heroes 1–3,
        /// units (Melee..Super), Race #2. Only affects overlay button order — data/snapshot
        /// stay keyed by the stable slot_index (1..12).
        /// </summary>
        public static readonly int[] DisplayOrderSlots =
        {
            10, 11, 7, 8, 9, 1, 2, 3, 4, 5, 6, 12,
        };

        /// <summary>Bonus slots are 1-based (1..SlotCount) to match <c>Bonuses.md</c> slot_index.</summary>
        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

        public static string GetSlotId(int slot) => slot switch
        {
            1 => GameIds.Bonuses.Melee,
            2 => GameIds.Bonuses.Ranged,
            3 => GameIds.Bonuses.Caster,
            4 => GameIds.Bonuses.Siege,
            5 => GameIds.Bonuses.Flying,
            6 => GameIds.Bonuses.Super,
            7 => GameIds.Bonuses.Hero1,
            8 => GameIds.Bonuses.Hero2,
            9 => GameIds.Bonuses.Hero3,
            10 => GameIds.Bonuses.Titan,
            11 => GameIds.Bonuses.RaceUnique1,
            12 => GameIds.Bonuses.RaceUnique2,
            _ => string.Empty,
        };

        public static string GetSlotDisplayName(int slot) => slot switch
        {
            1 => "Melee",
            2 => "Ranged",
            3 => "Caster",
            4 => "Siege",
            5 => "Flying",
            6 => "Super",
            7 => "King Veteran",
            8 => "Paladin Veteran",
            9 => "Priest Veteran",
            10 => "Titan Veteran",
            11 => "March Discipline",
            12 => "Stone Masonry",
            _ => string.Empty,
        };

        /// <summary>Short effect text for bonus overlay tooltips (RU). Empty for unknown slots.</summary>
        public static string GetSlotDescription(int slot) => slot switch
        {
            1 => "Усиленный melee: −20 HP, дальность 2. On-hit 15%: AoE по врагам радиус 2.",
            2 => "Усиленный ranged: +1 броня. On-hit 15%: урон ×2.",
            3 => "Усиленный caster: +20 HP. Ближе 2 м — удар булавой (8–10), иначе ranged.",
            4 => "Усиленный siege: +50 HP. Аура: +1 HP/с союзникам в радиусе 8.",
            5 => "Усиленный flying: +10 HP. On-death 25%: спавн базового ranged.",
            6 => "Усиленный super: дальность 12 (мин. 5). Параболический снаряд, AoE 50% радиус 3.",
            7 => "Ветеран-король: HP ×1.4, урон ×1.35, +2 брони. Ульта «King's Command»: вся армия +30% урона на 8 с. Morale +15% урона.",
            8 => "Ветеран-паладин: HP ×1.4, урон ×1.35, +2 брони. «Aegis»: броня и щит союзникам рядом. Morale +15% скорости атаки.",
            9 => "Ветеран-жрец: HP ×1.4, урон ×1.35, +2 брони. «Sanctuary»: лечащая зона следует за жрецом. Morale +15% брони.",
            10 => "Ветеран-титан: HP ×1.4, урон ×1.35, +2 брони. «Greater Colossus»: армия +25% HP, пока титан жив.",
            11 => "Дисциплина марша: все войска (юниты, герои, титан) двигаются на 10% быстрее.",
            12 => "Каменная кладка: все здания получают +20% запаса здоровья — включая уже построенные.",
            _ => string.Empty,
        };

        /// <summary>Random valid bonus slot for timeout picks (Bonuses.md timeout_pick).</summary>
        public static int GetRandomSlot(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return random.Next(1, SlotCount + 1);
        }
    }

    /// <summary>Pure rules for replicated bonus picks (server authoritative).</summary>
    public static class BonusPickNetworkRules
    {
        public static bool TryApplyPick(int[] picks, int playerSlot, int bonusSlot)
        {
            if (picks == null)
            {
                throw new ArgumentNullException(nameof(picks));
            }

            if (playerSlot < 0 || playerSlot >= picks.Length)
            {
                return false;
            }

            if (!BonusPickRules.IsValidSlot(bonusSlot))
            {
                return false;
            }

            // Exactly one pick per player — reject a second pick.
            if (picks[playerSlot] != BonusPickRules.NoneSlot)
            {
                return false;
            }

            picks[playerSlot] = bonusSlot;
            return true;
        }

        /// <summary>Randomly fills all slots that did not pick before the deadline.</summary>
        public static void FillTimeoutPicks(int[] picks, Random random)
        {
            if (picks == null)
            {
                throw new ArgumentNullException(nameof(picks));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            for (var slot = 0; slot < picks.Length; slot++)
            {
                if (picks[slot] == BonusPickRules.NoneSlot)
                {
                    picks[slot] = BonusPickRules.GetRandomSlot(random);
                }
            }
        }
    }
}
