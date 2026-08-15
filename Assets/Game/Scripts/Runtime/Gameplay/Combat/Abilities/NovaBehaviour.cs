namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Priest Holy Nova: burst around a picked ally anchor — damages enemies and heals allies nearby.
    /// Skipped while the caster's Greater Heal zone is still active (historical behaviour).
    /// </summary>
    public sealed class NovaBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (ctx.Host.HasActiveHealZone(caster.UnitId))
            {
                return false;
            }

            var novaRadius = def.Radius > 0f ? def.Radius : HeroAbilityRules.NovaRadius;
            var castRange = def.CastRange > 0f ? def.CastRange : HeroAbilityRules.NovaCastRange;
            var anchor = HeroAbilityRules.PickHolyNovaAnchor(caster, ctx.Host.Units, castRange, novaRadius);
            if (anchor == null)
            {
                return false;
            }

            var center = anchor.WorldPosition;
            var enemies = HeroAbilityRules.GatherEnemiesAround(caster.OwnerSlot, center, ctx.Host.Units, novaRadius);
            var allies = HeroAbilityRules.GatherAlliesAround(caster.OwnerSlot, center, ctx.Host.Units, novaRadius);
            var injured = ctx.Host.FindMostInjuredAlly(allies);
            if (enemies.Count == 0 && injured == null)
            {
                return false;
            }

            var damage = def.Damage > 0f ? def.Damage : HeroAbilityRules.NovaDamage;
            var heal = def.Heal > 0f ? def.Heal : HeroAbilityRules.NovaHealAmount;
            for (var i = 0; i < enemies.Count; i++)
            {
                ctx.Host.ApplyDamage(caster, enemies[i], damage, caster.OwnerSlot);
            }

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
                anchor.UnitId,
                center,
                novaRadius));
            return true;
        }
    }
}
