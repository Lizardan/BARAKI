using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Paladin veteran signature (bonus slot 8): Shield upgraded with an absorb pool —
    /// allies in radius gain flat armor plus a shield worth a fraction of their max HP.
    /// Damage consumes the pool before health; the pool expires with the buff duration.
    /// </summary>
    public sealed class AegisBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (caster.ArmorBuffRemaining > 0f && caster.AbsorbSecondsRemaining > 0f)
            {
                return false;
            }

            var radius = def.Radius > 0f ? def.Radius : HeroAbilityRules.ShieldRadius;
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, ctx.Host.Units, radius);
            if (enemies.Count == 0)
            {
                return false;
            }

            var duration = def.DurationSeconds > 0f ? def.DurationSeconds : HeroAbilityRules.ShieldDurationSeconds;
            var bonus = def.FlatBonus > 0f ? def.FlatBonus : HeroAbilityRules.ShieldArmorBonus;
            var allies = HeroAbilityRules.GatherAlliesInRadius(caster, ctx.Host.Units, radius);
            for (var i = 0; i < allies.Count; i++)
            {
                var ally = allies[i];
                ally.ArmorBuffRemaining = Mathf.Max(ally.ArmorBuffRemaining, duration);
                ally.ArmorBuffBonus = Mathf.Max(ally.ArmorBuffBonus, bonus);

                var shieldHp = ctx.Host.GetEffectiveMaxHp(ally) * HeroAbilityRules.AegisShieldMaxHpFraction;
                ally.AbsorbRemaining = Mathf.Max(ally.AbsorbRemaining, shieldHp);
                ally.AbsorbSecondsRemaining = Mathf.Max(ally.AbsorbSecondsRemaining, duration);
            }

            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                caster.UnitId,
                caster.WorldPosition,
                radius));
            return true;
        }

        public override string DescribeParams()
        {
            var percent = Mathf.RoundToInt(HeroAbilityRules.AegisShieldMaxHpFraction * 100f);
            return $"Броня и щит ({percent}% запаса здоровья) герою и союзникам рядом на {HeroAbilityRules.ShieldDurationSeconds:0} с.";
        }
    }
}
