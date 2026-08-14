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
    }
}
