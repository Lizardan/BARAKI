using Game.Gameplay.Combat;

namespace Game.Gameplay.Vfx
{
    /// <summary>Maps ability ids to the FX picker palette (aura vs hit vs generic cast).</summary>
    public static class AbilityVfxKindRules
    {
        public static AbilityVfxKind Resolve(int abilityId) => abilityId switch
        {
            AbilityIds.AuraDamagePercent
                or AbilityIds.AuraAttackSpeedPercent
                or AbilityIds.AuraArmorPercent
                or             AbilityIds.AuraMaxHpPercent
                or AbilityIds.GreaterColossus
                or AbilityIds.AuraHpRegen
                or AbilityIds.AuraOfHunger => AbilityVfxKind.Aura,

            AbilityIds.Strike
                or AbilityIds.Ultimate
                or AbilityIds.KingsCommand
                or AbilityIds.Smite
                or AbilityIds.Consecration
                or AbilityIds.Slam
                or AbilityIds.Stomp
                or AbilityIds.MeleeCleave
                or AbilityIds.RangedCrit
                or AbilityIds.CasterHybrid
                or AbilityIds.SuperCatapult
                or AbilityIds.BlightingGaze
                or AbilityIds.VoidDrain
                or AbilityIds.AncientMantle => AbilityVfxKind.Hit,

            _ => AbilityVfxKind.Cast,
        };

        public static string KitLabel(int abilityId) => abilityId switch
        {
            AbilityIds.CasterHeal or AbilityIds.Frost or AbilityIds.Resurrect => "Caster",
            AbilityIds.Heal or AbilityIds.Ultimate or AbilityIds.Strike
                or AbilityIds.AuraDamagePercent => "King",
            AbilityIds.Smite or AbilityIds.Shield or AbilityIds.Consecration
                or AbilityIds.AuraAttackSpeedPercent => "Paladin",
            AbilityIds.HolyNova or AbilityIds.GreaterHeal or AbilityIds.Revive
                or AbilityIds.AuraArmorPercent => "Priest",
            AbilityIds.Rally or AbilityIds.Stomp or AbilityIds.Slam
                or AbilityIds.AuraMaxHpPercent => "Titan",
            AbilityIds.AuraHpRegen => "Siege BONUS",
            AbilityIds.MeleeCleave => "Melee BONUS",
            AbilityIds.RangedCrit => "Ranged BONUS",
            AbilityIds.CasterHybrid => "Caster BONUS",
            AbilityIds.FlyingSpawn => "Flying BONUS",
            AbilityIds.SuperCatapult => "Super BONUS",
            AbilityIds.KingsCommand or AbilityIds.Aegis or AbilityIds.Sanctuary
                or AbilityIds.GreaterColossus => "Veteran BONUS",
            AbilityIds.BlightingGaze or AbilityIds.VoidDrain
                or AbilityIds.RaiseDrowned => "Faceless Caster",
            AbilityIds.AncientMantle => "Faceless King",
            AbilityIds.AreaOfMiss => "Faceless Warlock",
            AbilityIds.FeastZone => "Faceless Berserker",
            AbilityIds.AuraOfHunger => "Faceless Titan",
            AbilityIds.MainBuildingSmite or AbilityIds.MainUnitSmite => "Divine Blessing",
            AbilityIds.MainIceRing or AbilityIds.MainWaveOfLight => "Main Building",
            _ => "Ability",
        };

        /// <summary>
        /// Seed used when <see cref="AbilityFx.Anchor"/> is still <see cref="AbilityVfxAnchor.Unspecified"/>.
        /// Auras / self-AoE → caster; heals and point damage → target; ground rings → ground;
        /// splash / spawn → impact.
        /// </summary>
        public static AbilityVfxAnchor ResolveDefaultAnchor(int abilityId) => abilityId switch
        {
            AbilityIds.AuraDamagePercent
                or AbilityIds.AuraAttackSpeedPercent
                or AbilityIds.AuraArmorPercent
                or AbilityIds.AuraMaxHpPercent
                or AbilityIds.GreaterColossus
                or AbilityIds.AuraHpRegen
                or AbilityIds.Strike
                or AbilityIds.Ultimate
                or AbilityIds.KingsCommand
                or AbilityIds.Aegis
                or AbilityIds.Slam
                or AbilityIds.Stomp
                or AbilityIds.MeleeCleave
                or AbilityIds.Shield
                or AbilityIds.Rally
                or AbilityIds.RaiseDrowned
                or AbilityIds.AncientMantle
                or AbilityIds.FeastZone
                or AbilityIds.AuraOfHunger => AbilityVfxAnchor.Caster,

            AbilityIds.Frost
                or AbilityIds.Consecration
                or AbilityIds.Sanctuary
                or AbilityIds.MainIceRing
                or AbilityIds.MainWaveOfLight
                or AbilityIds.VoidDrain
                or AbilityIds.AreaOfMiss => AbilityVfxAnchor.Ground,

            AbilityIds.SuperCatapult
                or AbilityIds.FlyingSpawn => AbilityVfxAnchor.Impact,

            _ => AbilityVfxAnchor.Target,
        };

        public static AbilityVfxAnchor ResolveAnchor(int abilityId, AbilityVfxAnchor authored) =>
            authored != AbilityVfxAnchor.Unspecified
                ? authored
                : ResolveDefaultAnchor(abilityId);
    }
}
