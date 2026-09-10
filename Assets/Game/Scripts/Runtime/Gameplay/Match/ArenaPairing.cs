using System;
using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    /// <summary>Одна запланированная дуэль арены.</summary>
    public struct ArenaDuel
    {
        public int SlotA;
        public int HeroA;
        public int SlotB;
        public int HeroB;

        /// <summary>True для боя нечётного игрока против выбранного им соперника.</summary>
        public bool IsChallengerDuel;

        public ArenaDuel(int slotA, int heroA, int slotB, int heroB, bool isChallengerDuel = false)
        {
            SlotA = slotA;
            HeroA = heroA;
            SlotB = slotB;
            HeroB = heroB;
            IsChallengerDuel = isChallengerDuel;
        }

        public bool IsValid => SlotA >= 0 && SlotB >= 0;
    }

    /// <summary>
    /// Отбор участников арены и сборка пар по золоту: топ-1 против топ-2, топ-3 против топ-4,
    /// нечётный игрок остаётся без пары и позже сам выбирает соперника.
    /// </summary>
    public static class ArenaPairing
    {
        /// <summary>Герой нанят (куплен/активирован в главном здании) — состояние «живой/мёртвый» не важно.</summary>
        public static bool IsHeroHired(MatchController controller, int slot, int heroSlot)
        {
            if (controller == null || heroSlot < 1 || heroSlot > ArenaRules.HeroSlots)
            {
                return false;
            }

            var roster = controller.GetHeroRoster(slot);
            return roster != null && IsHired(roster.Get(heroSlot));
        }

        static bool IsHired(HeroSlotState state) =>
            state != null && state.State != HeroLifecycleState.None;

        /// <summary>Титан открыт исследованием в главном здании.</summary>
        public static bool IsTitanHired(MatchController controller, int slot)
        {
            var titan = controller?.GetTitanState(slot);
            return titan != null && titan.IsUnlocked;
        }

        /// <summary>Уровень героя (для статов бойца).</summary>
        public static int HeroLevel(MatchController controller, int slot, int heroSlot)
        {
            var state = controller?.GetHeroRoster(slot)?.Get(heroSlot);
            return state?.Level ?? HeroLevelRules.StartingLevel;
        }

        /// <summary>Уровень титана (для статов бойца).</summary>
        public static int TitanLevel(MatchController controller, int slot)
        {
            var titan = controller?.GetTitanState(slot);
            return titan?.Level ?? HeroLevelRules.StartingLevel;
        }

        /// <summary>
        /// Может ли игрок участвовать: не выбыл и у него есть хотя бы один доступный
        /// на этой арене боец (герой, не выступавший на прошлых аренах, либо титан).
        /// </summary>
        public static bool CanParticipate(
            MatchController controller,
            int slot,
            int arenaIndex,
            uint usedHeroMask)
        {
            if (controller == null)
            {
                return false;
            }

            var players = controller.Players;
            if (slot < 0 || slot >= players.Count || players[slot].IsEliminated)
            {
                return false;
            }

            if (ArenaRules.IsTitanArena(arenaIndex))
            {
                return IsTitanHired(controller, slot);
            }

            for (var heroSlot = 1; heroSlot <= ArenaRules.HeroSlots; heroSlot++)
            {
                if (IsHeroHired(controller, slot, heroSlot)
                    && !ArenaRules.HasUsedHero(usedHeroMask, slot, heroSlot))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Доступные слоты героев игрока на этой арене (для UI выбора).</summary>
        public static List<int> AvailableHeroSlots(
            MatchController controller,
            int slot,
            int arenaIndex,
            uint usedHeroMask)
        {
            var result = new List<int>();
            if (controller == null || ArenaRules.IsTitanArena(arenaIndex))
            {
                return result;
            }

            for (var heroSlot = 1; heroSlot <= ArenaRules.HeroSlots; heroSlot++)
            {
                if (IsHeroHired(controller, slot, heroSlot)
                    && !ArenaRules.HasUsedHero(usedHeroMask, slot, heroSlot))
                {
                    result.Add(heroSlot);
                }
            }

            return result;
        }

        /// <summary>Первый доступный боец — автовыбор по истечении времени.</summary>
        public static int FirstAvailablePick(
            MatchController controller,
            int slot,
            int arenaIndex,
            uint usedHeroMask)
        {
            if (controller == null)
            {
                return 0;
            }

            if (ArenaRules.IsTitanArena(arenaIndex))
            {
                return IsTitanHired(controller, slot) ? ArenaRules.PickValueTitan : 0;
            }

            var available = AvailableHeroSlots(controller, slot, arenaIndex, usedHeroMask);
            return available.Count > 0 ? available[0] : 0;
        }

        /// <summary>Участники, отсортированные по золоту по убыванию (тай-брейк — меньший слот).</summary>
        public static List<int> CollectParticipants(
            MatchController controller,
            int arenaIndex,
            uint usedHeroMask)
        {
            var result = new List<int>();
            if (controller == null)
            {
                return result;
            }

            for (var slot = 0; slot < controller.Players.Count && slot < ArenaRules.MaxSlots; slot++)
            {
                if (CanParticipate(controller, slot, arenaIndex, usedHeroMask))
                {
                    result.Add(slot);
                }
            }

            result.Sort((a, b) =>
            {
                var byGold = controller.Players[b].Gold.CompareTo(controller.Players[a].Gold);
                return byGold != 0 ? byGold : a.CompareTo(b);
            });

            return result;
        }

        /// <summary>
        /// Сборка дуэлей. Пары (0,1), (2,3) и т.д.; при нечётном числе участников
        /// последний становится «претендентом» и его дуэль добавляется отдельно.
        /// </summary>
        public static void BuildDuels(
            List<int> ordered,
            Func<int, int> pickOf,
            List<ArenaDuel> result)
        {
            result.Clear();
            if (ordered == null)
            {
                return;
            }

            for (var i = 0; i + 1 < ordered.Count; i += 2)
            {
                var a = ordered[i];
                var b = ordered[i + 1];
                result.Add(new ArenaDuel(a, pickOf(a), b, pickOf(b)));
            }

            // Дуэль нечётного игрока добавляется позже, когда известен выбор соперника.
        }

        /// <summary>Слот нечётного игрока или -1, если все участники разбиты на пары.</summary>
        public static int FindChallenger(List<int> ordered) =>
            ordered != null && ordered.Count % 2 == 1 ? ordered[ordered.Count - 1] : -1;

        /// <summary>Итоговая дуэль нечётного игрока против выбранного соперника.</summary>
        public static ArenaDuel BuildChallengerDuel(
            int challengerSlot,
            int challengerPick,
            int targetSlot,
            int targetPick) =>
            new(challengerSlot, challengerPick, targetSlot, targetPick, isChallengerDuel: true);
    }
}
