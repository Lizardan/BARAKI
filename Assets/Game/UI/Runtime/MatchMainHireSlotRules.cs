using Game.Gameplay.Match;

namespace Game.UI
{
    /// <summary>
    /// Main-building hero hire buttons on the 3-col command grid:
    /// slots 6–8 are hero 1 / 2 / 3 (to the right of each other as main levels up).
    /// </summary>
    public static class MatchMainHireSlotRules
    {
        public const int HeroHireSlotStart = 6;

        public static bool TryGetHeroHireSlot(int heroSlot, out int slotIndex)
        {
            if (!HeroRules.IsValidHeroSlot(heroSlot))
            {
                slotIndex = -1;
                return false;
            }

            slotIndex = HeroHireSlotStart + heroSlot - 1;
            return true;
        }

        /// <summary>
        /// Command slot for a hero by its 0-based hire position in the confirmed order
        /// (position 0 shows the hero chosen for main level 1, and so on). This is what
        /// the main-building panel uses so that rearranged hero order reorders the buttons;
        /// <see cref="TryGetHeroHireSlot"/> stays for callers keyed by the hero's own slot.
        /// </summary>
        public static bool TryGetHeroHireSlotByPosition(int position, out int slotIndex)
        {
            if (position < 0 || position >= HeroRules.MaxHeroSlots)
            {
                slotIndex = -1;
                return false;
            }

            slotIndex = HeroHireSlotStart + position;
            return true;
        }
    }
}
