namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Priest veteran signature (bonus slot 9): Greater Heal upgraded into a sanctuary —
    /// the heal field follows the caster while it lasts, healing the advancing column.
    /// </summary>
    public sealed class SanctuaryZoneBehaviour : UnitAbilityBehaviour
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
                FollowUnitId = caster.UnitId,
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

        public override string DescribeParams() =>
            "Святилище следует за жрецом и лечит союзников внутри, пока активно.";
    }
}
