using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// King veteran signature (bonus slot 7): grants a temporary damage buff to every living
    /// unit of the owner's army — the veteran leads the whole host into the attack.
    /// Reuses the Ultimate damage-buff pipeline per unit (<c>UltimateBuffRemaining/Percent</c>).
    /// </summary>
    public sealed class ArmyDamageShoutBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var radius = def.Radius > 0f ? def.Radius : HeroAbilityRules.UltimateRadius;
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, ctx.Host.Units, radius);
            if (enemies.Count == 0)
            {
                return false;
            }

            var duration = def.DurationSeconds > 0f ? def.DurationSeconds : HeroAbilityRules.KingsCommandSeconds;
            var percent = def.Percent > 0f ? def.Percent : HeroAbilityRules.KingsCommandPercent;

            var buffed = 0;
            var units = ctx.Host.Units;
            for (var i = 0; i < units.Count; i++)
            {
                var ally = units[i];
                if (ally == null || !ally.IsAlive || ally.OwnerSlot != caster.OwnerSlot)
                {
                    continue;
                }

                ally.UltimateBuffRemaining = Mathf.Max(ally.UltimateBuffRemaining, duration);
                ally.UltimateBuffPercent = Mathf.Max(ally.UltimateBuffPercent, percent);
                buffed++;
            }

            if (buffed == 0)
            {
                return false;
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
            var percent = Mathf.RoundToInt(HeroAbilityRules.KingsCommandPercent * 100f);
            return $"Вся армия владельца: +{percent}% урона на {HeroAbilityRules.KingsCommandSeconds:0} с.";
        }
    }
}
