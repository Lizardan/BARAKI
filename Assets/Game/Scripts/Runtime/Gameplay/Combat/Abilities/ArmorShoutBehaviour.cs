using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Shield / Rally: while a buff is not already active and an enemy is in range,
    /// grants flat armor to the caster and allies within radius.
    /// </summary>
    public sealed class ArmorShoutBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (caster.ArmorBuffRemaining > 0f)
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
                allies[i].ArmorBuffRemaining = Mathf.Max(allies[i].ArmorBuffRemaining, duration);
                allies[i].ArmorBuffBonus = Mathf.Max(allies[i].ArmorBuffBonus, bonus);
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
    }
}
