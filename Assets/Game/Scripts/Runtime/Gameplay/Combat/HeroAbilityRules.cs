using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Combat tuning + cast event for hero abilities.
    /// GDD: HERO_ABILITY_UNLOCK_LEVELS (1 / 4 / 7 / 10), unique kit per hero slot.
    /// Active abilities are CD-only (no mana). Slot-3 auras are global owner buffs while that hero is alive.
    /// </summary>
    public static class HeroAbilityRules
    {
        public const int KingSlot = 1;
        public const int PaladinSlot = 2;
        public const int PriestSlot = 3;

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
        public static HeroAbilityType GetAbilityType(int heroSlot, int abilitySlot)
        {
            return (heroSlot, abilitySlot) switch
            {
                (KingSlot, 1) => HeroAbilityType.Strike,
                (KingSlot, 2) => HeroAbilityType.Heal,
                (KingSlot, 3) => HeroAbilityType.AuraDamagePercent,
                (KingSlot, 4) => HeroAbilityType.Ultimate,
                (PaladinSlot, 1) => HeroAbilityType.Smite,
                (PaladinSlot, 2) => HeroAbilityType.Shield,
                (PaladinSlot, 3) => HeroAbilityType.AuraAttackSpeedPercent,
                (PaladinSlot, 4) => HeroAbilityType.Consecration,
                (PriestSlot, 1) => HeroAbilityType.HolyNova,
                (PriestSlot, 2) => HeroAbilityType.GreaterHeal,
                (PriestSlot, 3) => HeroAbilityType.AuraArmorPercent,
                (PriestSlot, 4) => HeroAbilityType.Revive,
                _ => HeroAbilityType.None,
            };
        }

        /// <summary>Floating label shown above the hero when the ability is fired.</summary>
        public static string GetDisplayName(HeroAbilityType ability)
        {
            return ability switch
            {
                HeroAbilityType.Strike => "Strike",
                HeroAbilityType.Heal => "Heal",
                HeroAbilityType.AuraDamagePercent
                    or HeroAbilityType.AuraAttackSpeedPercent
                    or HeroAbilityType.AuraArmorPercent
                    or HeroAbilityType.AuraMaxHpPercent => "Aura",
                HeroAbilityType.Ultimate => "Ultimate",
                HeroAbilityType.Smite => "Smite",
                HeroAbilityType.Shield => "Shield",
                HeroAbilityType.Consecration => "Consecration",
                HeroAbilityType.HolyNova => "Holy Nova",
                HeroAbilityType.GreaterHeal => "Greater Heal",
                HeroAbilityType.Revive => "Revive",
                HeroAbilityType.Slam => "Slam",
                HeroAbilityType.Rally => "Rally",
                HeroAbilityType.Stomp => "Stomp",
                _ => ability.ToString(),
            };
        }

        static float HorizontalDistanceSq(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }

    /// <summary>Payload fired when a hero unit uses an ability (host-side; presenter listens for VFX).</summary>
    public readonly struct HeroAbilityCastEvent
    {
        public HeroAbilityCastEvent(
            int casterUnitId,
            int ownerSlot,
            HeroAbilityType ability,
            Vector3 centerPosition,
            float radius,
            int targetUnitId = 0,
            int serial = 0)
        {
            CasterUnitId = casterUnitId;
            OwnerSlot = ownerSlot;
            Ability = ability;
            CenterPosition = centerPosition;
            Radius = radius;
            TargetUnitId = targetUnitId;
            Serial = serial;
        }

        public int CasterUnitId { get; }
        public int OwnerSlot { get; }
        public HeroAbilityType Ability { get; }
        public Vector3 CenterPosition { get; }
        public float Radius { get; }
        public int TargetUnitId { get; }
        public int Serial { get; }
    }

    /// <summary>Stationary ally heal field created by Greater Heal.</summary>
    public sealed class HeroHealZoneState
    {
        public int CasterUnitId;
        public int OwnerSlot;
        public Vector3 Center;
        public float Radius;
        public float RemainingSeconds;
        public float HealPerSecond;
    }
}
