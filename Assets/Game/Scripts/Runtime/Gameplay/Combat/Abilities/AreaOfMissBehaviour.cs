using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless Warlock veteran signature (bonus slot 8): a shroud of the void over the area —
    /// every enemy inside misses their attacks entirely (100% evade) for the duration.
    /// The evade itself is consumed by <see cref="MatchCombatSystem.ApplyDamage"/>, which returns
    /// 0 damage while <see cref="MatchUnitState.EvadeRemainingSeconds"/> is positive.
    /// </summary>
    public sealed class AreaOfMissBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var radius = def.Radius > 0f ? def.Radius : FacelessBonusUnitRules.AreaOfMissRadius;
            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, ctx.Host.Units, radius);
            if (enemies.Count == 0)
            {
                return false;
            }

            var duration = def.DurationSeconds > 0f
                ? def.DurationSeconds
                : FacelessBonusUnitRules.AreaOfMissSeconds;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                enemy.EvadeRemainingSeconds = Mathf.Max(enemy.EvadeRemainingSeconds, duration);
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

        public override string DescribeParams() =>
            $"Враги в радиусе {FacelessBonusUnitRules.AreaOfMissRadius:0} на {FacelessBonusUnitRules.AreaOfMissSeconds:0} с промахиваются (атаки не наносят урона).";
    }
}
