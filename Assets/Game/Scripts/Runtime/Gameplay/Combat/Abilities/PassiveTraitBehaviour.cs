namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Marker passive: never casts and contributes no aura. Used for bonus-unit traits
    /// (cleave / crit / hybrid / on-death spawn / splash) that combat resolves by AbilityId.
    /// </summary>
    public sealed class PassiveTraitBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx) => false;

        public override string DescribeParams() => "Пассивно";
    }
}
