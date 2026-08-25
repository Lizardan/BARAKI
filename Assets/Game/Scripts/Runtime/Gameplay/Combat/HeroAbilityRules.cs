using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Hero ability tuning + targeting helpers.
    /// GDD: HERO_ABILITY_UNLOCK_LEVELS (1 / 4 / 7 / 10), unique kit per hero slot.
    /// Numbers here are fallbacks when the <see cref="Data.UnitAbilityDef"/> leaves a value at 0;
    /// behaviours live in <see cref="Data.UnitAbilityDef.Behaviour"/>.
    /// </summary>
    public static class HeroAbilityRules
    {
        public const int KingSlot = 1;
        public const int PaladinSlot = 2;
        public const int PriestSlot = 3;

        /// <summary>Passive auras boost owner army only within this horizontal radius of the bearer.</summary>
        public const float AuraRadius = 8f;

        public const float StrikeRadius = 4f;
        public const float StrikeDamage = 60f;
        public const float StrikeCooldownSeconds = 6f;

        public const float HealRadius = 6f;
        public const float HealAmount = 120f;
        public const float HealCooldownSeconds = 10f;

        /// <summary>King aura: global damage bonus to the owner's army while the hero is alive (unlock lvl 7).</summary>
        public const float AuraDamageBonusPercent = 0.1f;

        public const float UltimateRadius = 6f;
        public const float UltimateDamage = 150f;
        public const float UltimateCooldownSeconds = 30f;
        public const float UltimateSelfDamageBonusPercent = 0.5f;
        public const float UltimateSelfBuffSeconds = 8f;

        public const float SmiteRadius = 5f;
        public const float SmiteDamage = 90f;
        public const float SmiteCooldownSeconds = 6f;

        public const float ShieldRadius = 6f;
        public const float ShieldArmorBonus = 8f;
        public const float ShieldDurationSeconds = 6f;
        public const float ShieldCooldownSeconds = 10f;

        /// <summary>Paladin aura: global attack-speed bonus to the owner's army while the hero is alive.</summary>
        public const float AuraAttackSpeedBonusPercent = 0.1f;

        public const float ConsecrationRadius = 6f;
        public const float ConsecrationDamage = 120f;
        public const float ConsecrationStunSeconds = 1.5f;
        public const float ConsecrationCooldownSeconds = 30f;

        public const float NovaRadius = 4f;
        public const float NovaDamage = 35f;
        public const float NovaHealAmount = 50f;
        public const float NovaCooldownSeconds = 6f;
        public const float NovaCastRange = 10f;

        public const float GreaterHealRadius = 10f;
        public const float GreaterHealHealPerSecond = 20f;
        public const float GreaterHealDurationSeconds = 10f;
        public const float GreaterHealCooldownSeconds = 10f;

        /// <summary>Priest aura: global armor bonus to the owner's army while the hero is alive.</summary>
        public const float AuraArmorBonusPercent = 0.1f;

        public const float ReviveRadius = 8f;
        public const float ReviveHealAmount = 150f;
        public const float ReviveHealRadius = 6f;
        public const float ReviveCooldownSeconds = 30f;

        public const float SlamRadius = 6f;
        public const float SlamDamage = 180f;
        public const float SlamCooldownSeconds = 6f;

        public const float RallyRadius = 8f;
        public const float RallyArmorBonus = 12f;
        public const float RallyDurationSeconds = 6f;
        public const float RallyCooldownSeconds = 10f;

        /// <summary>Titan Colossus: global max-HP bonus to the owner's army while the titan is alive.</summary>
        public const float AuraMaxHpBonusPercent = 0.15f;

        public const float StompRadius = 8f;
        public const float StompDamage = 360f;
        public const float StompStunSeconds = 2f;
        public const float StompCooldownSeconds = 30f;

        // --- PRE-006b: veteran champion signature abilities (bonus slots 7–10) ---

        /// <summary>Veteran auras are stronger morale versions of the base hero aura (10% → 15%).</summary>
        public const float VeteranAuraBonusPercent = 0.15f;

        /// <summary>King's Command: army-wide damage buff replacing the self-buff Ultimate.</summary>
        public const float KingsCommandPercent = 0.3f;
        public const float KingsCommandSeconds = 8f;
        public const float KingsCommandCooldownSeconds = UltimateCooldownSeconds;

        /// <summary>Aegis: Shield armor numbers + absorb shield worth this fraction of each ally's max HP.</summary>
        public const float AegisShieldMaxHpFraction = 0.25f;

        /// <summary>Greater Colossus: veteran titan MaxHp aura percent.</summary>
        public const float AuraMaxHpVeteranBonusPercent = 0.25f;

        /// <summary>All living enemies within <paramref name="radius"/> of the hero.</summary>
        public static List<MatchUnitState> GatherEnemiesInRadius(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float radius)
        {
            if (caster == null)
            {
                return new List<MatchUnitState>();
            }

            return GatherEnemiesAround(caster.OwnerSlot, caster.WorldPosition, units, radius);
        }

        public static List<MatchUnitState> GatherEnemiesAround(
            int ownerSlot,
            Vector3 center,
            IReadOnlyList<MatchUnitState> units,
            float radius)
        {
            var victims = new List<MatchUnitState>();
            if (units == null || radius <= 0f)
            {
                return victims;
            }

            var radiusSq = radius * radius;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot == ownerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(center, candidate.WorldPosition) <= radiusSq)
                {
                    victims.Add(candidate);
                }
            }

            return victims;
        }

        /// <summary>All living allies (including the hero) within <paramref name="radius"/>.</summary>
        public static List<MatchUnitState> GatherAlliesInRadius(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float radius)
        {
            if (caster == null)
            {
                return new List<MatchUnitState>();
            }

            return GatherAlliesAround(caster.OwnerSlot, caster.WorldPosition, units, radius);
        }

        public static List<MatchUnitState> GatherAlliesAround(
            int ownerSlot,
            Vector3 center,
            IReadOnlyList<MatchUnitState> units,
            float radius)
        {
            var allies = new List<MatchUnitState>();
            if (units == null || radius <= 0f)
            {
                return allies;
            }

            var radiusSq = radius * radius;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot != ownerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(center, candidate.WorldPosition) <= radiusSq)
                {
                    allies.Add(candidate);
                }
            }

            return allies;
        }

        public static float ApplyHeal(float currentHp, float maxHp) =>
            ApplyHeal(currentHp, maxHp, HealAmount);

        public static float ApplyHeal(float currentHp, float maxHp, float amount) =>
            Mathf.Min(maxHp, currentHp + amount);

        /// <summary>Nearest living enemy within <paramref name="radius"/>, or null.</summary>
        public static MatchUnitState FindNearestEnemy(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float radius)
        {
            var enemies = GatherEnemiesInRadius(caster, units, radius);
            MatchUnitState nearest = null;
            var bestSq = float.MaxValue;
            for (var i = 0; i < enemies.Count; i++)
            {
                var sq = HorizontalDistanceSq(caster.WorldPosition, enemies[i].WorldPosition);
                if (sq < bestSq)
                {
                    bestSq = sq;
                    nearest = enemies[i];
                }
            }

            return nearest;
        }

        /// <summary>
        /// Ally in <paramref name="castRange"/> whose nova best heals injured allies and hits enemies.
        /// Prefers anchors that do both; ties go to the most injured anchor. Includes the caster.
        /// </summary>
        public static MatchUnitState PickHolyNovaAnchor(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float castRange,
            float novaRadius)
        {
            if (caster == null || units == null || castRange <= 0f || novaRadius <= 0f)
            {
                return null;
            }

            var castRangeSq = castRange * castRange;
            MatchUnitState best = null;
            var bestHasBoth = false;
            var bestScore = -1;
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

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) > castRangeSq)
                {
                    continue;
                }

                var injured = 0;
                var enemies = 0;
                var novaRadiusSq = novaRadius * novaRadius;
                for (var j = 0; j < units.Count; j++)
                {
                    var other = units[j];
                    if (other == null || !other.IsAlive)
                    {
                        continue;
                    }

                    if (HorizontalDistanceSq(candidate.WorldPosition, other.WorldPosition) > novaRadiusSq)
                    {
                        continue;
                    }

                    if (other.OwnerSlot == caster.OwnerSlot)
                    {
                        if (other.CurrentHp < other.Stats.MaxHp - 0.001f)
                        {
                            injured++;
                        }
                    }
                    else
                    {
                        enemies++;
                    }
                }

                if (injured == 0 && enemies == 0)
                {
                    continue;
                }

                var hasBoth = injured > 0 && enemies > 0;
                var score = injured + enemies;
                var fraction = candidate.Stats.MaxHp > 0f
                    ? candidate.CurrentHp / candidate.Stats.MaxHp
                    : 1f;
                if (best == null
                    || (hasBoth && !bestHasBoth)
                    || (hasBoth == bestHasBoth && score > bestScore)
                    || (hasBoth == bestHasBoth && score == bestScore && fraction < bestFraction))
                {
                    best = candidate;
                    bestHasBoth = hasBoth;
                    bestScore = score;
                    bestFraction = fraction;
                }
            }

            return best;
        }

        /// <summary>Active ability for a hero slot (1..4).</summary>
        static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }

    /// <summary>Stationary ally heal field created by Greater Heal; Sanctuary follows its caster.</summary>
    public sealed class HeroHealZoneState
    {
        public int CasterUnitId;
        public int OwnerSlot;
        public Vector3 Center;
        public float Radius;
        public float RemainingSeconds;
        public float HealPerSecond;
        /// <summary>Non-zero: the zone follows this living unit's position each tick (Sanctuary).</summary>
        public int FollowUnitId;
    }
}
