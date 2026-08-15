using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Combat spell tuning + target-selection helpers for Caster units.
    /// GDD baseline: SPELL_HUMAN_1 heal 80 / r6 / cd10 / mana50, SPELL_HUMAN_2 frost AoE r5 dmg40 / cd14 / mana75,
    /// SPELL_HUMAN_3 resurrect corpse &lt;=20s / cd30 / mana150. All spells cast range 6.
    /// Caster pool 200 mana; regenerates at <see cref="ManaRegenPerSecond"/> (5/s ≈ covers one cast per CD).
    /// Numbers here are fallbacks when the <see cref="Data.UnitAbilityDef"/> leaves a value at 0.
    /// </summary>
    public static class CasterSpellRules
    {
        public const float CastRange = 6f;

        /// <summary>Mana refilled per second for mana units (Caster 200 pool).</summary>
        public const float ManaRegenPerSecond = 5f;

        public const int HealRequiredMagicLevel = 1;
        public const float HealAmount = 80f;
        public const float HealManaCost = 50f;
        public const float HealCooldownSeconds = 10f;

        public const int FrostRequiredMagicLevel = 2;
        public const float FrostDamage = 40f;
        public const float FrostRadius = 5f;
        public const float FrostManaCost = 75f;
        public const float FrostCooldownSeconds = 14f;
        /// <summary>Hard-stun duration applied to every Frost victim.</summary>
        public const float FrostFreezeSeconds = 1.5f;

        public const int ResurrectRequiredMagicLevel = 3;
        public const float ResurrectCorpseMaxAgeSeconds = 20f;
        public const float ResurrectManaCost = 150f;
        public const float ResurrectCooldownSeconds = 30f;

        public static float ApplyHeal(float currentHp, float maxHp)
        {
            return Mathf.Min(maxHp, currentHp + HealAmount);
        }

        /// <summary>Lowest-HP living ally (not full health) within range. Includes the caster itself.</summary>
        public static MatchUnitState PickHealTarget(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float range,
            System.Func<MatchUnitState, float> getMaxHp = null)
        {
            if (caster == null || units == null || units.Count == 0 || range <= 0f)
            {
                return null;
            }

            var rangeSq = range * range;
            MatchUnitState best = null;
            var bestFraction = float.MaxValue;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot != caster.OwnerSlot)
                {
                    continue;
                }

                var maxHp = getMaxHp != null ? getMaxHp(candidate) : candidate.Stats.MaxHp;
                if (candidate.CurrentHp >= maxHp - 0.001f)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) > rangeSq)
                {
                    continue;
                }

                var fraction = maxHp > 0f ? candidate.CurrentHp / maxHp : 1f;
                if (fraction < bestFraction)
                {
                    bestFraction = fraction;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Enemy whose neighbourhood holds the densest enemy cluster within <paramref name="aoeRadius"/>.
        /// Used as the ground-center for the Frost AoE burst.
        /// </summary>
        public static MatchUnitState PickFrostCenter(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float castRange,
            float aoeRadius)
        {
            if (caster == null || units == null || units.Count == 0 || castRange <= 0f || aoeRadius <= 0f)
            {
                return null;
            }

            var castRangeSq = castRange * castRange;
            var aoeRadiusSq = aoeRadius * aoeRadius;
            MatchUnitState best = null;
            var bestCount = -1;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot == caster.OwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) > castRangeSq)
                {
                    continue;
                }

                var count = 0;
                for (var j = 0; j < units.Count; j++)
                {
                    var other = units[j];
                    if (other == null
                        || !other.IsAlive
                        || other.OwnerSlot == caster.OwnerSlot)
                    {
                        continue;
                    }

                    if (HorizontalDistanceSq(candidate.WorldPosition, other.WorldPosition) <= aoeRadiusSq)
                    {
                        count++;
                    }
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>All living enemies within <paramref name="aoeRadius"/> of <paramref name="center"/> (includes center).</summary>
        public static List<MatchUnitState> GatherFrostVictims(
            MatchUnitState caster,
            MatchUnitState center,
            IReadOnlyList<MatchUnitState> units,
            float aoeRadius)
        {
            var victims = new List<MatchUnitState>();
            if (caster == null || center == null || units == null || aoeRadius <= 0f)
            {
                return victims;
            }

            var radiusSq = aoeRadius * aoeRadius;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot == caster.OwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(center.WorldPosition, candidate.WorldPosition) <= radiusSq)
                {
                    victims.Add(candidate);
                }
            }

            return victims;
        }

        /// <summary>Most valuable recent corpse (highest bounty) of a slain ally within range.</summary>
        public static CombatCorpseState PickResurrectCorpse(
            MatchUnitState caster,
            IReadOnlyList<CombatCorpseState> corpses,
            float range,
            float maxAgeSeconds)
        {
            if (caster == null || corpses == null || corpses.Count == 0 || range <= 0f)
            {
                return null;
            }

            var rangeSq = range * range;
            CombatCorpseState best = null;
            var bestValue = -1;
            for (var i = 0; i < corpses.Count; i++)
            {
                var corpse = corpses[i];
                if (corpse == null
                    || corpse.OwnerSlot != caster.OwnerSlot
                    || corpse.AgeSeconds > maxAgeSeconds)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, corpse.WorldPosition) > rangeSq)
                {
                    continue;
                }

                if (corpse.Value > bestValue)
                {
                    bestValue = corpse.Value;
                    best = corpse;
                }
            }

            return best;
        }

        static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }
}
