using Game.Gameplay.Combat;
using Game.Gameplay.Data;

namespace Game.Gameplay.Vfx
{
    /// <summary>Which unit/hero (or main building) BARAKI Studio should spawn as caster.</summary>
    public readonly struct AbilityFxPreviewCaster
    {
        public AbilityFxPreviewCaster(
            UnitRole role,
            int heroSlot,
            int bonusSlot,
            string displayName,
            bool isBuilding = false,
            bool hidden = false)
        {
            Role = role;
            HeroSlot = heroSlot;
            BonusSlot = bonusSlot;
            DisplayName = displayName;
            IsBuilding = isBuilding;
            Hidden = hidden;
        }

        public UnitRole Role { get; }
        public int HeroSlot { get; }
        public int BonusSlot { get; }
        public string DisplayName { get; }
        public bool IsBuilding { get; }
        /// <summary>No caster model — preview shows only the target (Divine Blessing).</summary>
        public bool Hidden { get; }
    }

    /// <summary>Maps ability ids to the kit owner spawned in the FX viewer preview.</summary>
    public static class AbilityFxPreviewCasterRules
    {
        public static AbilityFxPreviewCaster Resolve(int abilityId) => abilityId switch
        {
            AbilityIds.Heal or AbilityIds.Ultimate or AbilityIds.Strike
                or AbilityIds.AuraDamagePercent =>
                Hero(HeroAbilityRules.KingSlot, "Король"),

            AbilityIds.Smite or AbilityIds.Shield or AbilityIds.Consecration
                or AbilityIds.AuraAttackSpeedPercent =>
                Hero(HeroAbilityRules.PaladinSlot, "Паладин"),

            AbilityIds.HolyNova or AbilityIds.GreaterHeal or AbilityIds.Revive
                or AbilityIds.AuraArmorPercent =>
                Hero(HeroAbilityRules.PriestSlot, "Жрец"),

            AbilityIds.Rally or AbilityIds.Stomp or AbilityIds.Slam
                or AbilityIds.AuraMaxHpPercent =>
                new AbilityFxPreviewCaster(UnitRole.Titan, 0, 0, "Титан"),

            AbilityIds.CasterHeal or AbilityIds.Frost or AbilityIds.Resurrect =>
                new AbilityFxPreviewCaster(UnitRole.Caster, 0, 0, "Кастер"),

            AbilityIds.AuraHpRegen => Bonus(UnitRole.Siege, "Осада BONUS"),
            AbilityIds.MeleeCleave => Bonus(UnitRole.Melee, "Мечник BONUS"),
            AbilityIds.RangedCrit => Bonus(UnitRole.Ranged, "Стрелок BONUS"),
            AbilityIds.CasterHybrid => Bonus(UnitRole.Caster, "Кастер BONUS"),
            AbilityIds.FlyingSpawn => Bonus(UnitRole.Flying, "Летун BONUS"),
            AbilityIds.SuperCatapult => Bonus(UnitRole.Super, "Супер BONUS"),

            AbilityIds.MainBuildingSmite or AbilityIds.MainUnitSmite =>
                new AbilityFxPreviewCaster(UnitRole.Hero, 0, 0, "", hidden: true),

            _ => new AbilityFxPreviewCaster(UnitRole.Caster, 0, 0, "Кастер"),
        };

        static AbilityFxPreviewCaster Hero(int heroSlot, string displayName) =>
            new(UnitRole.Hero, heroSlot, 0, displayName);

        static AbilityFxPreviewCaster Bonus(UnitRole role, string displayName) =>
            new(role, 0, HumanBonusUnitRules.BonusSlotForRole(role), displayName);
    }
}
