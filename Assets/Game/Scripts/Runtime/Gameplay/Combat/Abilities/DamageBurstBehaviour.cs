namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Burst damage. Single-target mode hits the nearest enemy within radius (Smite);
    /// otherwise damages every enemy around the caster.
    /// </summary>
    public sealed class DamageBurstBehaviour : UnitAbilityBehaviour
    {
        private bool _singleTarget = true;

        public void Configure(bool singleTarget = true)
        {
            _singleTarget = singleTarget;
        }

        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            var radius = def.Radius > 0f ? def.Radius : HeroAbilityRules.SmiteRadius;
            var damage = def.Damage;

            if (_singleTarget)
            {
                var target = HeroAbilityRules.FindNearestEnemy(caster, ctx.Host.Units, radius);
                if (target == null)
                {
                    return false;
                }

                ctx.Host.ApplyDamage(caster, target, damage, caster.OwnerSlot);
                ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
                ctx.Host.EmitCast(new AbilityCastEvent(
                    caster.UnitId,
                    caster.OwnerSlot,
                    def,
                    target.UnitId,
                    target.WorldPosition,
                    0.8f));
                return true;
            }

            var enemies = HeroAbilityRules.GatherEnemiesInRadius(caster, ctx.Host.Units, radius);
            if (enemies.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < enemies.Count; i++)
            {
                ctx.Host.ApplyDamage(caster, enemies[i], damage, caster.OwnerSlot);
            }

            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                targetUnitId: 0,
                caster.WorldPosition,
                radius));
            return true;
        }
    }
}
