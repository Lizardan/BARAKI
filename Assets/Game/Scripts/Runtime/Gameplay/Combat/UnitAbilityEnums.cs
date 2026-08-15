namespace Game.Gameplay.Combat
{
    /// <summary>Active abilities cast on a cooldown; passives apply while unlocked.</summary>
    public enum AbilityKind
    {
        Active = 0,
        Passive = 1,
    }

    /// <summary>How <see cref="Data.UnitAbilityDef.UnlockValue"/> gates the ability.</summary>
    public enum AbilityUnlock
    {
        Always = 0,
        HeroLevel = 1,
        MagicLevel = 2,
    }
}
