namespace Game.Gameplay.Combat
{
    public enum AbilityKind
    {
        Active = 0,
        Passive = 1,
    }

    public enum AbilityUnlock
    {
        Always = 0,
        HeroLevel = 1,
        MagicLevel = 2,
    }

    /// <summary>
    /// Combat behavior selected by a <see cref="UnitAbilitySlot"/>.
    /// Champion VFX events reuse these values; caster spells also live here so one kit covers every role.
    /// </summary>
    public enum AbilityType
    {
        None = 0,
        Strike = 1,
        Heal = 2,
        AuraDamagePercent = 3,
        Ultimate = 4,
        Smite = 5,
        Shield = 6,
        Consecration = 7,
        HolyNova = 8,
        GreaterHeal = 9,
        Revive = 10,
        AuraAttackSpeedPercent = 11,
        AuraArmorPercent = 12,
        Slam = 13,
        Rally = 14,
        AuraMaxHpPercent = 15,
        Stomp = 16,
        CasterHeal = 17,
        Frost = 18,
        Resurrect = 19,
    }

    /// <summary>Legacy name kept so existing call sites and snapshot comments stay readable.</summary>
    public enum HeroAbilityType
    {
        None = 0,
        Strike = 1,
        Heal = 2,
        AuraDamagePercent = 3,
        Ultimate = 4,
        Smite = 5,
        Shield = 6,
        Consecration = 7,
        HolyNova = 8,
        GreaterHeal = 9,
        Revive = 10,
        AuraAttackSpeedPercent = 11,
        AuraArmorPercent = 12,
        Slam = 13,
        Rally = 14,
        AuraMaxHpPercent = 15,
        Stomp = 16,
        CasterHeal = 17,
        Frost = 18,
        Resurrect = 19,
    }
}
