using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public enum CasterSpellType
    {
        Heal = 0,
        Frost = 1,
        Resurrect = 2,
    }

    /// <summary>Payload fired when a Caster unit casts a spell (host-side; presenter listens for VFX/anim).</summary>
    public readonly struct CasterSpellCastEvent
    {
        public CasterSpellCastEvent(
            int casterUnitId,
            int ownerSlot,
            CasterSpellType spellType,
            int targetUnitId,
            Vector3 centerPosition,
            float radius)
        {
            CasterUnitId = casterUnitId;
            OwnerSlot = ownerSlot;
            SpellType = spellType;
            TargetUnitId = targetUnitId;
            CenterPosition = centerPosition;
            Radius = radius;
        }

        public int CasterUnitId { get; }
        public int OwnerSlot { get; }
        public CasterSpellType SpellType { get; }
        public int TargetUnitId { get; }
        public Vector3 CenterPosition { get; }
        public float Radius { get; }
    }

    /// <summary>
    /// Combat spell tuning + target-selection helpers for Caster units.
    /// GDD baseline: SPELL_HUMAN_1 heal 80 / r6 / cd10, SPELL_HUMAN_2 frost AoE r5 dmg40 / cd14,
    /// SPELL_HUMAN_3 resurrect corpse <=20s / cd30. All spells cast range 6.
    /// </summary>
    public static class CasterSpellRules
    {
        public const float CastRange = 6f;

        public const int HealRequiredMagicLevel = 1;
        public const float HealAmount = 80f;
        public const float HealCooldownSeconds = 10f;

        public const int FrostRequiredMagicLevel = 2;
        public const float FrostDamage = 40f;
        public const float FrostRadius = 5f;
        public const float FrostCooldownSeconds = 14f;

        public const int ResurrectRequiredMagicLevel = 3;
        public const float ResurrectCorpseMaxAgeSeconds = 20f;
        public const float ResurrectCooldownSeconds = 30f;

        public static float ApplyHeal(float currentHp, float maxHp)
        {
            return Mathf.Min(maxHp, currentHp + HealAmount);
        }

        /// <summary>Lowest-HP living ally (not full health) within range. Includes the caster itself.</summary>
        public static MatchUnitState PickHealTarget(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float range)
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
                    || candidate.OwnerSlot != caster.OwnerSlot
                    || candidate.CurrentHp >= candidate.Stats.MaxHp - 0.001f)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) > rangeSq)
                {
                    continue;
                }

                var fraction = candidate.CurrentHp / candidate.Stats.MaxHp;
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
