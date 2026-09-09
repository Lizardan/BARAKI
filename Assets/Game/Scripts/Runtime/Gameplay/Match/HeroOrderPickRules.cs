namespace Game.Gameplay.Match
{
    /// <summary>
    /// Post-bonus-pick hero order (order of the three heroes on the barracks line).
    /// The order is a permutation of hero slots 1..3; the position in the order decides at
    /// which main level the hero unlocks for hire (position 1 → main level 1, etc.), decoupling
    /// the hero's identity from its slot number. Default order [1, 2, 3] keeps legacy behavior.
    /// </summary>
    public static class HeroOrderPickRules
    {
        public static int[] DefaultOrder() => new[] { 1, 2, 3 };

        /// <summary>True when <paramref name="order"/> is a permutation of slots 1..3.</summary>
        public static bool IsValidOrder(int[] order)
        {
            if (order == null || order.Length != HeroRules.MaxHeroSlots)
            {
                return false;
            }

            var seen = 0;
            for (var i = 0; i < order.Length; i++)
            {
                var slot = order[i];
                if (slot < 1 || slot > HeroRules.MaxHeroSlots)
                {
                    return false;
                }

                if ((seen & (1 << slot)) != 0)
                {
                    return false;
                }

                seen |= 1 << slot;
            }

            return true;
        }

        /// <summary>
        /// 1-based position of <paramref name="heroSlot"/> in the order; 0 when the hero is not
        /// present or the order is invalid (invalid orders are treated as the default).
        /// </summary>
        public static int GetPosition(int[] order, int heroSlot)
        {
            if (!IsValidOrder(order))
            {
                order = DefaultOrder();
            }

            for (var i = 0; i < order.Length; i++)
            {
                if (order[i] == heroSlot)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// True when <paramref name="heroSlot"/> may be hired at <paramref name="mainLevel"/>:
        /// its position in the order must be &lt;= main level. Closed hero limits are handled by
        /// the caller via <see cref="HeroRules.GetMaxHiredHeroes"/> where relevant.
        /// </summary>
        public static bool IsUnlockedAtLevel(int[] order, int heroSlot, int mainLevel)
        {
            if (!HeroRules.IsValidHeroSlot(heroSlot) || mainLevel < 1)
            {
                return false;
            }

            var position = GetPosition(order, heroSlot);
            return position >= 1 && position <= mainLevel;
        }
    }
}