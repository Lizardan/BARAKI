using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Faceless hero/titan kit ability — Call of the Deep / Зов глубин (Plan0909, Фаза 5).
    /// Summons 1–2 servants under the caster's control: when a recent corpse (ANY owner, unlike the
    /// Human Revive) lies in cast range the corpse is consumed and two servants are raised;
    /// otherwise a single servant spawns at the champion's side. Servant stats come from the fixed
    /// servant profile (Plan0909, Фаза 1); prefab scale ×1.25 is a presenter concern.
    /// </summary>
    public sealed class CallOfTheDeepBehaviour : UnitAbilityBehaviour
    {
        public override bool TryCast(in UnitAbilityContext ctx)
        {
            var caster = ctx.Caster;
            var def = ctx.Def;

            var range = def.CastRange > 0f ? def.CastRange : FacelessHeroRules.CallCastRange;
            var maxAge = def.DurationSeconds > 0f
                ? def.DurationSeconds
                : FacelessHeroRules.CallCorpseMaxAgeSeconds;

            var corpse = FacelessSpellRules.PickAnyCorpse(caster, ctx.Host.Corpses, range, maxAge);
            var count = corpse != null
                ? FacelessHeroRules.ServantsWithCorpse
                : FacelessHeroRules.ServantsWithoutCorpse;

            var minion = ctx.Host.SummonMinion(caster.OwnerSlot, caster);
            if (minion == null)
            {
                return false;
            }

            for (var i = 1; i < count; i++)
            {
                if (ctx.Host.SummonMinion(caster.OwnerSlot, caster) == null)
                {
                    break;
                }
            }

            if (corpse != null)
            {
                ctx.Host.ConsumeCorpse(corpse);
            }

            ctx.Host.ArmSlotCooldown(caster, ctx.SlotIndex, def.CooldownSeconds);
            ctx.Host.EmitCast(new AbilityCastEvent(
                caster.UnitId,
                caster.OwnerSlot,
                def,
                minion.UnitId,
                corpse != null ? corpse.WorldPosition : caster.WorldPosition,
                def.Radius));
            return true;
        }

        public override string DescribeParams() =>
            $"2 прислужника из трупа в радиусе {FacelessHeroRules.CallCastRange:0} (труп уничтожается), иначе 1 рядом с героем";
    }
}