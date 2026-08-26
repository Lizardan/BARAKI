using System;
using Game.Core;
using Game.Gameplay.Data;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Race tower upgrade tracks (PRE-007): ids, per-level economy, role gates and effect tuning.
    /// Effects apply to regular units only — never heroes or the titan. Canonical track order
    /// matches <c>RACE_HUMAN.tower_tracks</c> and UI command slots 4–12.
    /// </summary>
    public static class TowerTrackRules
    {
        public const int TrackCount = 9;

        /// <summary>Canonical order: index 0..8 → UI command slots 4..12.</summary>
        public static readonly string[] TrackIds =
        {
            GameIds.Upgrades.TowerHumanFlamingArrows,
            GameIds.Upgrades.TowerHumanBulwark,
            GameIds.Upgrades.TowerHumanBloodrage,
            GameIds.Upgrades.TowerHumanBatteringRams,
            GameIds.Upgrades.TowerHumanArcaneFocus,
            GameIds.Upgrades.TowerHumanSkirmishers,
            GameIds.Upgrades.TowerHumanForcedMarch,
            GameIds.Upgrades.TowerHumanFieldMedics,
            GameIds.Upgrades.TowerHumanLastStand,
        };

        // Flaming Arrows (index 0)
        public const float BurnDamagePerSecondPerLevel = 2f;
        public const float BurnDurationSeconds = 2f;
        public static readonly UnitRole[] FlamingArrowsRoles = { UnitRole.Ranged, UnitRole.Flying };

        // Bulwark (index 1)
        public const float BulwarkArmorPerLevel = 1f;
        public const float BulwarkBlockDamageReduction = 0.2f;
        public static readonly UnitRole[] BulwarkRoles = { UnitRole.Melee, UnitRole.Siege };

        // Bloodrage (index 2)
        public static readonly float[] BloodrageAttackSpeedPercentByLevel = { 0.15f, 0.25f, 0.40f };
        public const float BloodrageDurationSeconds = 3f;
        public static readonly UnitRole[] BloodrageRoles = { UnitRole.Melee, UnitRole.Flying };

        // Battering Rams (index 3)
        public const float VsBuildingDamagePercentPerLevel = 0.25f;
        public const float SplashRadiusBonusAtMaxLevel = 1f;
        public static readonly UnitRole[] BatteringRamsRoles = { UnitRole.Siege, UnitRole.Super };

        // Arcane Focus (index 4)
        public static readonly float[] CasterCooldownFactorByLevel = { 0.88f, 0.76f, 0.64f };
        public static readonly UnitRole[] ArcaneFocusRoles = { UnitRole.Caster };

        // Skirmishers (index 5)
        public const float AttackRangeBonusPerLevel = 0.5f;
        public static readonly UnitRole[] SkirmisherRoles = { UnitRole.Ranged, UnitRole.Caster };

        // Forced March (index 6)
        public const float MoveSpeedPercentPerLevel = 0.08f;
        public static readonly UnitRole[] ForcedMarchRoles = { UnitRole.Melee, UnitRole.Siege, UnitRole.Caster };

        // Field Medics (index 7)
        public const float RegenHpPerSecondPerLevel = 1f;

        // Last Stand (index 8)
        public const float LowHealthThreshold = 0.3f;
        public static readonly float[] LastStandDamagePercentByLevel = { 0.20f, 0.30f, 0.40f };

        public static bool IsTowerTrack(string upgradeId) => TryGetTrackIndex(upgradeId, out _);

        public static bool TryGetTrackIndex(string upgradeId, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(upgradeId))
            {
                return false;
            }

            for (var i = 0; i < TrackIds.Length; i++)
            {
                if (TrackIds[i] == upgradeId)
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public static int GetLevel(int[] levels, string upgradeId) =>
            TryGetTrackIndex(upgradeId, out var index)
                ? GetLevel(levels, index)
                : 0;

        public static int GetLevel(int[] levels, int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
            {
                return 0;
            }

            return Math.Clamp(levels[index], 0, MatchEconomyRules.MaxTowerTrackLevel);
        }

        public static bool RoleMatches(int trackIndex, UnitRole role)
        {
            switch (trackIndex)
            {
                case 0: return Contains(FlamingArrowsRoles, role);
                case 1: return Contains(BulwarkRoles, role);
                case 2: return Contains(BloodrageRoles, role);
                case 3: return Contains(BatteringRamsRoles, role);
                case 4: return Contains(ArcaneFocusRoles, role);
                case 5: return Contains(SkirmisherRoles, role);
                case 6: return Contains(ForcedMarchRoles, role);
                case 7:
                case 8:
                    // Field Medics / Last Stand affect every regular unit.
                    return true;
                default:
                    return false;
            }
        }

        static bool Contains(UnitRole[] roles, UnitRole role)
        {
            foreach (var candidate in roles)
            {
                if (candidate == role)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
