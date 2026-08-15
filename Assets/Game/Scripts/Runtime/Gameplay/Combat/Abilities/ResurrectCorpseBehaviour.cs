namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Caster Resurrect: revives the most valuable recent allied corpse within cast range.
    /// Costs mana; blocked when below <see cref="Data.UnitAbilityDef.ManaCost"/>.
    /// </summary>
    public sealed class ResurrectCorpseBehaviour : UnitAbilityBehaviour
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
            var maxAge = def.DurationSeconds > 0f
                ? def.DurationSeconds
                : CasterSpellRules.ResurrectCorpseMaxAgeSeconds;
            var corpse = CasterSpellRules.PickResurrectCorpse(caster, ctx.Host.Corpses, range, maxAge);
            if (corpse == null)
            {
                return false;
            }

            var revived = ctx.Host.ResurrectUnit(corpse);
            if (revived == null)
            {
                return false;
            }

            caster.CurrentMana -= def.ManaCost;
            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                revived.UnitId,
                revived.WorldPosition,
                0f));
            return true;
        }
    }
}
