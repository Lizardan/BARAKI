using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Faceless slots already implemented in code (FACELESS-011: unit bonuses 1–6;
        /// FACELESS-012: veteran heroes 7–9 + titan 10). Slots 11–12 stay greyed out
        /// until FACELESS-013 lands.
        /// </summary>
        public static readonly int[] FacelessImplementedSlots = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        /// <summary>True when this slot has working mechanics for the race (greyed out otherwise).</summary>
        public static bool IsSlotImplemented(int slot, string raceId)
        {
            if (!IsValidSlot(slot))
            {
                return false;
            }

            if (raceId == GameIds.Races.Faceless)
            {
                foreach (var implemented in FacelessImplementedSlots)
                {
                    if (implemented == slot)
                    {
                        return true;
                    }
                }

                return false;
            }

            return true;
        }

        /// <summary>Valid slot that is also implemented for the race — the only pickable ones.</summary>
        public static bool IsSlotAvailable(int slot, string raceId) =>
            IsValidSlot(slot) && IsSlotImplemented(slot, raceId);

        public static string GetSlotDisplayName(int slot, string raceId = GameIds.Races.Human)
        {
            if (raceId == GameIds.Races.Faceless)
            {
                return slot switch
                {
                    1 => "Hunger of the Old One",
                    2 => "Tainting Bolt",
                    3 => "Call of the Abyss",
                    4 => "Death Explosion",
                    5 => "Hungering Flight",
                    6 => "Feast on the Fallen",
                    7 => "Ancient Mantle",
                    8 => "Area of Miss",
                    9 => "Feast Zone",
                    10 => "Aura of Hunger",
                    11 => "Shadow of the Void",
                    12 => "Void Bastion",
                    _ => string.Empty,
                };
            }

            return slot switch
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
        }

        /// <summary>Short effect text for bonus overlay tooltips (RU). Empty for unknown slots.</summary>
        public static string GetSlotDescription(int slot, string raceId = GameIds.Races.Human)
        {
            if (raceId == GameIds.Races.Faceless)
            {
                return slot switch
                {
                    1 => "Древние melee: on-hit 15% — лечение 50% от нанесённого урона (вампиризм).",
                    2 => "Древние ranged: on-hit 15% — дот 3 урона/с в течение 3 с.",
                    3 => "Древние caster: при добивании врага призывает мини-меле (статы ×0.5).",
                    4 => "Древние siege: при смерти взрыв — 10% макс. HP вражеским юнитам в радиусе 3.",
                    5 => "Древние flying: при убийстве +15% скорости атаки на 3 с (стаки до 3).",
                    6 => "Древние super: при убийстве +80 HP и +10% скорости атаки на 3 с (стаки до 3).",
                    7 => "Ветеран-король: HP ×1.4, урон ×1.35, +2 брони. Ульта «Ancient Mantle»: сам герой +50% урона и +2 брони на 8 с, AoE-удар +30%. Morale +15% урона.",
                    8 => "Ветеран-колдун: HP ×1.4, урон ×1.35, +2 брони. «Area of Miss»: враги в радиусе 5 на 4 с промахиваются. Morale +15% скорости атаки.",
                    9 => "Ветеран-берсерк: HP ×1.4, урон ×1.35, +2 брони. «Feast Zone»: зона 10 с следует за героем, союзники внутри лечатся на 30% от урона. Morale +15% брони.",
                    10 => "Ветеран-титан: HP ×1.4, урон ×1.35, +2 брони. «Aura of Hunger»: пока титан жив, армия лечится на 15% от нанесённого урона.",
                    11 => "Тень пустоты: все войска владельца — 8% шанс полностью избежать атаки.",
                    12 => "Оплот пустоты: все здания владельца — атаки по ним промахиваются на 20%.",
                    _ => string.Empty,
                };
            }

            return slot switch
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
        }

        public static int GetRandomSlot(Random random, string raceId = GameIds.Races.Human)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (raceId == GameIds.Races.Faceless)
            {
                return FacelessImplementedSlots[random.Next(0, FacelessImplementedSlots.Length)];
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

        /// <summary>
        /// Randomly fills all slots that did not pick before the deadline.
        /// Pass <paramref name="raceIds"/> so races with a partial kit (Faceless) only roll
        /// slots that actually have mechanics.
        /// </summary>
        public static void FillTimeoutPicks(int[] picks, Random random, IReadOnlyList<string> raceIds = null)
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
                    var raceId = raceIds != null && slot < raceIds.Count
                        ? raceIds[slot]
                        : GameIds.Races.Human;
                    picks[slot] = BonusPickRules.GetRandomSlot(random, raceId);
                }
            }
        }
    }
}
