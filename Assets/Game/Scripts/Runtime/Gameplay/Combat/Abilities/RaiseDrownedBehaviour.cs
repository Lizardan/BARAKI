using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless caster slot 3 — Raise the Drowned / Поднять павшего: consumes ANY recent corpse in range
    /// (enemy corpses included, unlike the Human Resurrect) and summons one servant under the caster's
    /// control. Servant stats come from the fixed servant profile (Plan0909, Фаза 1); prefab scale ×1.25
    /// is a presenter concern.
    /// </summary>
    public sealed class RaiseDrownedBehaviour : UnitAbilityBehaviour
    {
        const float MinionSpawnOffset = 1.2f;

        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;
            if (caster.CurrentMana < def.ManaCost)
            {
                return false;
            }

            var range = def.CastRange > 0f ? def.CastRange : FacelessSpellRules.CastRange;
            var maxAge = def.DurationSeconds > 0f
                ? def.DurationSeconds
                : FacelessSpellRules.RaiseCorpseMaxAgeSeconds;

            var corpse = FacelessSpellRules.PickAnyCorpse(caster, ctx.Host.Corpses, range, maxAge);
            if (corpse == null)
            {
                return false;
            }

            var minion = ctx.Host.SummonMinion(caster.OwnerSlot, caster);
            if (minion == null)
            {
                return false;
            }

            ctx.Host.ConsumeCorpse(corpse);

            caster.CurrentMana -= def.ManaCost;
            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                minion.UnitId,
                corpse.WorldPosition,
                0f));
            return true;
        }

        public override string DescribeParams() =>
            $"труп ≤{FacelessSpellRules.RaiseCorpseMaxAgeSeconds:0} с, прислужник";
    }
}
