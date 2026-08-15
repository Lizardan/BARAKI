namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Priest Revive: revives the most valuable recent allied corpse within radius,
    /// then heals allies around the caster.
    /// </summary>
    public sealed class ReviveBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var corpseRadius = def.Radius > 0f ? def.Radius : HeroAbilityRules.ReviveRadius;
            var corpse = CasterSpellRules.PickResurrectCorpse(
                caster,
                ctx.Host.Corpses,
                corpseRadius,
                CasterSpellRules.ResurrectCorpseMaxAgeSeconds);
            if (corpse == null)
            {
                return false;
            }

            var revived = ctx.Host.ResurrectUnit(corpse);
            if (revived == null)
            {
                return false;
            }

            var healRadius = def.SecondaryRadius > 0f ? def.SecondaryRadius : HeroAbilityRules.ReviveHealRadius;
            var heal = def.SecondaryHeal > 0f
                ? def.SecondaryHeal
                : (def.Heal > 0f ? def.Heal : HeroAbilityRules.ReviveHealAmount);
            var allies = HeroAbilityRules.GatherAlliesAround(
                caster.OwnerSlot,
                caster.WorldPosition,
                ctx.Host.Units,
                healRadius);
            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].CurrentHp = HeroAbilityRules.ApplyHeal(
                    allies[i].CurrentHp,
                    ctx.Host.GetEffectiveMaxHp(allies[i]),
                    heal);
            }

            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                revived.UnitId,
                revived.WorldPosition,
                healRadius));
            return true;
        }
    }
}
