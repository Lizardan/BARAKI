using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.UI
{
    /// <summary>
    /// Barracks command buttons on the 3-col grid (12 slots, 1-based UI order):
    /// 1 upgrade, 2 empty, 3 titan, 4–6 siege/flying/super, 7–9 melee/ranged/caster, 10–12 heroes.
    /// </summary>
    public static class MatchBarracksCallSlotRules
    {
        public const int BarracksUpgradeSlot = 0;
        public const int TitanDeploySlot = 2;
        public const int SiegeRowStart = 3;
        public const int MeleeRowStart = 6;
        public const int HeroDeploySlotStart = 9;

        public static bool TryGetHeroDeploySlot(int heroSlot, out int slotIndex)
        {
            if (!HeroRules.IsValidHeroSlot(heroSlot))
            {
                slotIndex = -1;
                return false;
            }

            slotIndex = HeroDeploySlotStart + heroSlot - 1;
            return true;
        }

        /// <summary>
        /// Command slot for a hero by its 0-based position in the confirmed order. Barracks
        /// deploy buttons follow the rearranged hero order (position 0 = first deployed hero);
        /// <see cref="TryGetHeroDeploySlot"/> stays for callers keyed by the hero's own slot.
        /// </summary>
        public static bool TryGetHeroDeploySlotByPosition(int position, out int slotIndex)
        {
            if (position < 0 || position >= HeroRules.MaxHeroSlots)
            {
                slotIndex = -1;
                return false;
            }

            slotIndex = HeroDeploySlotStart + position;
            return true;
        }

        public static bool TryGetTitanDeploySlot(out int slotIndex)
        {
            slotIndex = TitanDeploySlot;
            return true;
        }

        public static bool TryGetCommandSlot(UnitRole role, out int slotIndex)
        {
            slotIndex = role switch
            {
                UnitRole.Siege => SiegeRowStart,
                UnitRole.Flying => SiegeRowStart + 1,
                UnitRole.Super => SiegeRowStart + 2,
                UnitRole.Melee => MeleeRowStart,
                UnitRole.Ranged => MeleeRowStart + 1,
                UnitRole.Caster => MeleeRowStart + 2,
                _ => -1,
            };
            return slotIndex >= 0;
        }
    }
}
