namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless Titan veteran signature (bonus slot 10): while the veteran titan lives, the owner's
    /// army feeds on every blow — each owner unit lifesteals a fraction of the damage it deals.
    /// The healing itself is applied by <see cref="MatchCombatSystem.TryApplyAuraOfHunger"/>; this
    /// def is the passive marker that carries the slot and (optionally) the aura visual.
    /// </summary>
    public sealed class AuraOfHungerBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx) => false;

        public override string DescribeParams() =>
            "Пока титан жив, вся армия владельца лечится на 15% от нанесённого урона.";
    }
}
