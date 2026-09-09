using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Caster kit of the Faceless (FACELESS-016). Canon: <c>GameDesign/Races.md</c> § Magic — Древние.
    /// Mirrors <see cref="CasterSpellRules"/> (Humans): same cast range, same slot → main magic level mapping,
    /// same +3 auto-attack damage per unlocked slot handled elsewhere.
    /// Design axis: Humans preserve their own (heal / resurrect ally); the Faceless feed on the foreign
    /// (damage + rot, AoE drain, raising ANY corpse — including enemy ones — as a controlled minion).
    /// </summary>
    public static class FacelessSpellRules
    {
        public const float CastRange = 6f;

        // Slot 1 — Blighting Gaze / Гниющий взор.
        public const int GazeRequiredMagicLevel = 1;
        public const float GazeDamage = 30f;
        public const float GazeDotDamagePerSecond = 4f;
        public const float GazeDotSeconds = 4f;
        public const float GazeManaCost = 50f;
        public const float GazeCooldownSeconds = 10f;

        // Slot 2 — Void Drain / Вытягивание жизни.
        public const int DrainRequiredMagicLevel = 2;
        public const float DrainDamage = 40f;
        public const float DrainRadius = 5f;
        /// <summary>Fraction of raw damage dealt returned to the caster as healing (0.30 = 30%).</summary>
        public const float DrainLifestealPercent = 0.30f;
        public const float DrainManaCost = 75f;
        public const float DrainCooldownSeconds = 14f;

        // Slot 3 — Raise the Drowned / Поднять павшего.
        public const int RaiseRequiredMagicLevel = 3;
        public const float RaiseCorpseMaxAgeSeconds = 15f;
        public const float RaiseManaCost = 150f;
        public const float RaiseCooldownSeconds = 30f;

        /// <summary>Living enemy with the highest current HP within range (canon priority: highest_hp_enemy_in_range).</summary>
        public static MatchUnitState PickGazeTarget(
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
            var bestHp = -1f;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null
                    || !candidate.IsAlive
                    || candidate.OwnerSlot == caster.OwnerSlot)
                {
                    continue;
                }

                if (HorizontalDistanceSq(caster.WorldPosition, candidate.WorldPosition) > rangeSq)
                {
                    continue;
                }

                if (candidate.CurrentHp > bestHp)
                {
                    bestHp = candidate.CurrentHp;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Most valuable recent corpse within range of ANY owner (canon: Faceless raise enemy corpses too).
        /// Unlike <see cref="CasterSpellRules.PickResurrectCorpse"/> the owner slot is not filtered.
        /// </summary>
        public static CombatCorpseState PickAnyCorpse(
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
                if (corpse == null || corpse.AgeSeconds > maxAgeSeconds)
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
