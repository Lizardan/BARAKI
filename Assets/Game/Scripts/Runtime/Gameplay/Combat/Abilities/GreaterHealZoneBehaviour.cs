namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Priest Greater Heal: plants a stationary ally heal field centered on the caster
    /// (replaces any existing zone from the same caster).
    /// </summary>
    public sealed class GreaterHealZoneBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var radius = def.Radius > 0f ? def.Radius : HeroAbilityRules.GreaterHealRadius;
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, ctx.Host.Units, radius);
            var target = ctx.Host.FindMostInjuredAlly(allies);
            if (target == null)
            {
                return false;
            }

            var duration = def.DurationSeconds > 0f
                ? def.DurationSeconds
                : HeroAbilityRules.GreaterHealDurationSeconds;
            var healPerSecond = def.HealPerSecond > 0f
                ? def.HealPerSecond
                : HeroAbilityRules.GreaterHealHealPerSecond;
            ctx.Host.ReplaceHealZone(new HeroHealZoneState
            {
                CasterUnitId = caster.UnitId,
                OwnerSlot = caster.OwnerSlot,
                Center = caster.WorldPosition,
                Radius = radius,
                RemainingSeconds = duration,
                HealPerSecond = healPerSecond,
            });

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
