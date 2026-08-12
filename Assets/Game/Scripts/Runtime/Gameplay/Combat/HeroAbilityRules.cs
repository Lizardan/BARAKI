using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Combat tuning + cast event for hero abilities.
    /// GDD baseline: HERO_ABILITY_UNLOCK_LEVELS (Strike 1 / Heal 4 / Aura 7 / Ultimate 10).
    /// Active abilities are CD-only (no mana); Aura is a global owner buff while the hero is alive.
    /// </summary>
    public static class HeroAbilityRules
    {
        public const float StrikeRadius = 4f;
        public const float StrikeDamage = 60f;
        public const float StrikeCooldownSeconds = 6f;

        public const float HealRadius = 6f;
        public const float HealAmount = 120f;
        public const float HealCooldownSeconds = 10f;

        /// <summary>Global damage bonus to the owner's army while the hero is alive (unlock lvl 7).</summary>
        public const float AuraDamageBonusPercent = 0.1f;

        public const float UltimateRadius = 6f;
        public const float UltimateDamage = 150f;
        public const float UltimateCooldownSeconds = 30f;
        public const float UltimateSelfDamageBonusPercent = 0.5f;
        public const float UltimateSelfBuffSeconds = 8f;

        /// <summary>All living enemies within <paramref name="radius"/> of the hero.</summary>
        public static List<MatchUnitState> GatherEnemiesInRadius(
            MatchUnitState caster,
            IReadOnlyList<MatchUnitState> units,
            float radius)
        {
            var victims = new List<MatchUnitState>();
            if (caster == null || units == null || radius <= 0f)
            {
                return victims;
            }

            var radiusSq = radius * radius;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot == caster.OwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) <= radiusSq)
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
            var allies = new List<MatchUnitState>();
            if (caster == null || units == null || radius <= 0f)
            {
                return allies;
            }

            var radiusSq = radius * radius;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot != caster.OwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) <= radiusSq)
                {
                    allies.Add(candidate);
                }
            }

            return allies;
        }

        public static float ApplyHeal(float currentHp, float maxHp) =>
            Mathf.Min(maxHp, currentHp + HealAmount);

        /// <summary>Floating label shown above the hero when the ability is fired.</summary>
        public static string GetDisplayName(HeroAbilityType ability)
        {
            return ability switch
            {
                HeroAbilityType.Strike => "Strike",
                HeroAbilityType.Heal => "Heal",
                HeroAbilityType.Aura => "Aura",
                HeroAbilityType.Ultimate => "Ultimate",
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
}
