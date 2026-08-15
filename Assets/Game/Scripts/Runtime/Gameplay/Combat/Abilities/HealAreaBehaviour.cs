namespace Game.Gameplay.Combat
{
    /// <summary>King Heal: heals all living allies within radius (requires at least one injured ally).</summary>
    public sealed class HealAreaBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var radius = def.Radius > 0f ? def.Radius : HeroAbilityRules.HealRadius;
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, ctx.Host.Units, radius);
            var target = ctx.Host.FindMostInjuredAlly(allies);
            if (target == null)
            {
                return false;
            }

            var amount = def.Heal > 0f ? def.Heal : HeroAbilityRules.HealAmount;
            for (var i = 0; i < allies.Count; i++)
            {
                allies[i].CurrentHp = HeroAbilityRules.ApplyHeal(
                    allies[i].CurrentHp,
                    ctx.Host.GetEffectiveMaxHp(allies[i]),
                    amount);
            }

            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                target.UnitId,
                caster.WorldPosition,
                radius));
            return true;
        }
    }
}
