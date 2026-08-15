using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Ground AoE damage around a picked center (Frost: cluster mode via <see cref="Data.UnitAbilityDef.CastRange"/>)
    /// or around the caster (Strike/Slam/Consecration/Stomp/Ultimate). Optional stun and ultimate self-buff.
    /// </summary>
    public sealed class GroundAoeBehaviour : UnitAbilityBehaviour
    {
        [SerializeField] private bool _applyUltimateSelfBuff;

        public void Configure(bool applyUltimateSelfBuff = false)
        {
            _applyUltimateSelfBuff = applyUltimateSelfBuff;
        }

        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var clusterMode = def.CastRange > 0f;
            var radius = def.Radius > 0f
                ? def.Radius
                : (clusterMode ? CasterSpellRules.FrostRadius : HeroAbilityRules.StrikeRadius);

            List<MatchUnitState> victims;
            MatchUnitState center = null;
            if (clusterMode)
            {
                center = CasterSpellRules.PickFrostCenter(caster, ctx.Host.Units, def.CastRange, radius);
                if (center == null)
                {
                    return false;
                }

                victims = CasterSpellRules.GatherFrostVictims(caster, center, ctx.Host.Units, radius);
            }
            else
            {
                victims = HeroAbilityRules.GatherEnemiesInRadius(caster, ctx.Host.Units, radius);
                if (victims.Count == 0)
                {
                    return false;
                }
            }

            var damage = def.Damage;
            var stun = def.StunSeconds;
            for (var i = 0; i < victims.Count; i++)
            {
                var victim = victims[i];
                ctx.Host.ApplyDamage(caster, victim, damage, caster.OwnerSlot);
                if (stun > 0f && (clusterMode || victim.IsAlive))
                {
                    victim.FrozenRemainingSeconds = Mathf.Max(victim.FrozenRemainingSeconds, stun);
                }
            }

            if (_applyUltimateSelfBuff)
            {
                caster.UltimateBuffRemaining = def.DurationSeconds > 0f
                    ? def.DurationSeconds
                    : HeroAbilityRules.UltimateSelfBuffSeconds;
                caster.UltimateBuffPercent = def.Percent > 0f
                    ? def.Percent
                    : HeroAbilityRules.UltimateSelfDamageBonusPercent;
            }

            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                center != null ? center.UnitId : 0,
                center != null ? center.WorldPosition : caster.WorldPosition,
                radius));
            return true;
        }
    }
}
