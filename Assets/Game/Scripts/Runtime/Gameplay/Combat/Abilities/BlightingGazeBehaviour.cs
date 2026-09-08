using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless caster slot 1 — Blighting Gaze / Гниющий взор: single-target burst plus a damage-over-time
    /// rot on the victim. The dot reuses the existing burn pipeline
    /// (<see cref="MatchUnitState.BurnDamagePerSecond"/>), it is not a new combat system.
    /// </summary>
    public sealed class BlightingGazeBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (caster.CurrentMana < def.ManaCost)
            {
                return false;
            }

            var range = def.CastRange > 0f ? def.CastRange : FacelessSpellRules.CastRange;
            var target = FacelessSpellRules.PickGazeTarget(caster, ctx.Host.Units, range);
            if (target == null)
            {
                return false;
            }

            var damage = def.Damage > 0f ? def.Damage : FacelessSpellRules.GazeDamage;
            ctx.Host.ApplyDamage(caster, target, damage, caster.OwnerSlot);

            if (target.IsAlive)
            {
                var dotSeconds = def.DurationSeconds > 0f
                    ? def.DurationSeconds
                    : FacelessSpellRules.GazeDotSeconds;
                var dotDps = FacelessSpellRules.GazeDotDamagePerSecond;

                // Strongest dot wins, duration refreshes — matches the Flaming Arrows convention.
                target.BurnDamagePerSecond = Mathf.Max(target.BurnDamagePerSecond, dotDps);
                target.BurnSecondsRemaining = Mathf.Max(target.BurnSecondsRemaining, dotSeconds);
                target.BurnSourceOwnerSlot = caster.OwnerSlot;
            }

            caster.CurrentMana -= def.ManaCost;
            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                target.UnitId,
                target.WorldPosition,
                0f));
            return true;
        }

        public override string DescribeParams() =>
            $"дот {FacelessSpellRules.GazeDotDamagePerSecond:0}/с × {FacelessSpellRules.GazeDotSeconds:0} с";
    }
}
