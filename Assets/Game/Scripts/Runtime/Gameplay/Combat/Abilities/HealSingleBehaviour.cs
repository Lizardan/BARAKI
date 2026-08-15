namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Caster Heal: heals the most-injured living ally within cast range (includes the caster).
    /// Costs mana; blocked when below <see cref="Data.UnitAbilityDef.ManaCost"/>.
    /// </summary>
    public sealed class HealSingleBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (caster.CurrentMana < def.ManaCost)
            {
                return false;
            }

            var range = def.CastRange > 0f ? def.CastRange : CasterSpellRules.CastRange;
            var target = CasterSpellRules.PickHealTarget(caster, ctx.Host.Units, range, ctx.Host.GetEffectiveMaxHp);
            if (target == null)
            {
                return false;
            }

            var amount = def.Heal > 0f ? def.Heal : CasterSpellRules.HealAmount;
            target.CurrentHp = HeroAbilityRules.ApplyHeal(
                target.CurrentHp,
                ctx.Host.GetEffectiveMaxHp(target),
                amount);

            caster.CurrentMana -= def.ManaCost;
            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                target.UnitId,
                target.WorldPosition,
                0f));
            return true;
        }
    }
}
