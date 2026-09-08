using System.Collections.Generic;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless caster slot 2 — Void Drain / Вытягивание жизни: ground AoE damage around the densest enemy
    /// cluster; the caster heals for a fraction of the RAW damage dealt (armor is not accounted for — matches
    /// how the def tuning is authored). Mirror of the Human Frost AoE: same center/victim selection.
    /// </summary>
    public sealed class VoidDrainBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (caster.CurrentMana < def.ManaCost)
            {
                return false;
            }

            var radius = def.Radius > 0f ? def.Radius : FacelessSpellRules.DrainRadius;
            var castRange = def.CastRange > 0f ? def.CastRange : FacelessSpellRules.CastRange;

            var center = CasterSpellRules.PickFrostCenter(caster, ctx.Host.Units, castRange, radius);
            if (center == null)
            {
                return false;
            }

            List<MatchUnitState> victims = CasterSpellRules.GatherFrostVictims(
                caster,
                center,
                ctx.Host.Units,
                radius);
            if (victims.Count == 0)
            {
                return false;
            }

            var damage = def.Damage > 0f ? def.Damage : FacelessSpellRules.DrainDamage;
            for (var i = 0; i < victims.Count; i++)
            {
                ctx.Host.ApplyDamage(caster, victims[i], damage, caster.OwnerSlot);
            }

            var lifesteal = def.Percent > 0f ? def.Percent : FacelessSpellRules.DrainLifestealPercent;
            var heal = damage * victims.Count * lifesteal;
            caster.CurrentHp = HeroAbilityRules.ApplyHeal(
                caster.CurrentHp,
                ctx.Host.GetEffectiveMaxHp(caster),
                heal);

            caster.CurrentMana -= def.ManaCost;
            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                center.UnitId,
                center.WorldPosition,
                radius));
            return true;
        }

        public override string DescribeParams() =>
            $"лечение {FacelessSpellRules.DrainLifestealPercent * 100f:0}% от урона";
    }
}
