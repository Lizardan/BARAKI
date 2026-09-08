using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless Berserker veteran signature (bonus slot 9): a feast zone that follows the caster
    /// and heals allies inside for a fraction of the damage they personally deal (FACELESS-012).
    /// The healing itself is applied by <see cref="MatchCombatSystem"/> (TryApplyFeastZone) whenever
    /// a unit inside the zone deals damage; <see cref="HeroHealZoneState.HealFractionOfDamageDealt"/>
    /// being &gt; 0 also suppresses the flat per-tick heal in <see cref="MatchCombatSystem.TickHealZones"/>.
    /// </summary>
    public sealed class FeastZoneBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var radius = def.Radius > 0f ? def.Radius : FacelessBonusUnitRules.FeastZoneRadius;
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, ctx.Host.Units, radius);
            var target = ctx.Host.FindMostInjuredAlly(allies);
            if (target == null)
            {
                return false;
            }

            var duration = def.DurationSeconds > 0f
                ? def.DurationSeconds
                : FacelessBonusUnitRules.FeastZoneSeconds;
            ctx.Host.ReplaceHealZone(new HeroHealZoneState
            {
                CasterUnitId = caster.UnitId,
                OwnerSlot = caster.OwnerSlot,
                Center = caster.WorldPosition,
                Radius = radius,
                RemainingSeconds = duration,
                HealPerSecond = 0f,
                HealFractionOfDamageDealt = FacelessBonusUnitRules.FeastZoneHealFraction,
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
            "Зона следует за героем и лечит союзников внутри на 30% от нанесённого ими урона.";
    }
}
