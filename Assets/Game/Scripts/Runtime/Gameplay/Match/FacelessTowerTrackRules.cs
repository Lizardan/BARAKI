using System;
using Game.Core;
using Game.Gameplay.Data;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Faceless tower upgrade tracks (FACELESS-017): ids, role gates and effect tuning.
    /// Parallel to <see cref="TowerTrackRules"/> (Human). Effects apply to regular units only.
    /// </summary>
    public static class FacelessTowerTrackRules
    {
        public const int TrackCount = 9;

        // Track indices (0..8)
        public const int ChitinousHideTrackIndex = 0;
        public const int HollowBarbsTrackIndex = 1;
        public const int VacuumCollapseTrackIndex = 2;
        public const int RitualOfTheDeepTrackIndex = 3;
        public const int UnnervingAimTrackIndex = 4;
        public const int FrenzyOfTheDeepTrackIndex = 5;
        public const int SplashOfTheDeepTrackIndex = 6;
        public const int HollowBonesTrackIndex = 7;
        public const int VoidHardeningTrackIndex = 8;

        /// <summary>Canonical order: index 0..8 → UI command slots 4..12.</summary>
        public static readonly string[] TrackIds =
        {
            GameIds.Upgrades.TowerFacelessChitinousHide,
            GameIds.Upgrades.TowerFacelessHollowBarbs,
            GameIds.Upgrades.TowerFacelessVacuumCollapse,
            GameIds.Upgrades.TowerFacelessRitualOfTheDeep,
            GameIds.Upgrades.TowerFacelessUnnervingAim,
            GameIds.Upgrades.TowerFacelessFrenzyOfTheDeep,
            GameIds.Upgrades.TowerFacelessSplashOfTheDeep,
            GameIds.Upgrades.TowerFacelessHollowBones,
            GameIds.Upgrades.TowerFacelessVoidHardening,
        };

        // Chitinous Hide (index 0)
        public const float ChitinousHideArmorPerLevel = 1f;
        public const float ChitinousHideMaxHpPercentAtMaxLevel = 0.15f;
        public static readonly UnitRole[] ChitinousHideRoles = { UnitRole.Melee, UnitRole.Super };

        // Hollow Barbs (index 1)
        public static readonly float[] HollowBarbsArmorPenByLevel = { 1f, 2f, 3f };
        public static readonly UnitRole[] HollowBarbsRoles = { UnitRole.Ranged, UnitRole.Flying };

        // Vacuum Collapse (index 2)
        public static readonly float[] VacuumCollapseSlowPercentByLevel = { 0.15f, 0.25f, 0.35f };
        public const float VacuumCollapseSlowDurationSeconds = 3f;
        public const float VacuumCollapseRadius = 3f;
        public static readonly UnitRole[] VacuumCollapseRoles = { UnitRole.Melee, UnitRole.Flying };

        // Ritual of the Deep (index 3)
        public static readonly float[] RitualReductionPercentByLevel = { 0.06f, 0.10f, 0.15f };
        public const float RitualRadius = 8f;
        public static readonly UnitRole[] RitualRoles = { UnitRole.Caster };

        // Unnerving Aim (index 4)
        public static readonly float[] UnnervingAimArmorDebuffByLevel = { 1f, 2f, 3f };
        public const float UnnervingAimDebuffDurationSeconds = 4f;
        public static readonly UnitRole[] UnnervingAimRoles = { UnitRole.Ranged, UnitRole.Caster };

        // Frenzy of the Deep (index 5)
        public static readonly float[] FrenzyAttackSpeedPercentByLevel = { 0.10f, 0.15f, 0.20f };
        public static readonly UnitRole[] FrenzyRoles = { UnitRole.Melee, UnitRole.Siege };

        // Splash of the Deep (index 6)
        public static readonly float[] SplashRadiusByLevel = { 0.5f, 1.0f, 1.5f };
        public static readonly float[] SplashDamagePercentByLevel = { 0.25f, 0.35f, 0.50f };
        public static readonly UnitRole[] SplashRoles = { UnitRole.Caster, UnitRole.Super };

        // Hollow Bones (index 7)
        public const float HollowBonesMoveSpeedPercentPerLevel = 0.08f;
        public static readonly UnitRole[] HollowBonesRoles = { UnitRole.Siege, UnitRole.Flying };

        // Void Hardening (index 8)
        public static readonly float[] VoidHardeningReductionPercentByLevel = { 0.10f, 0.15f, 0.20f };

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

        public static bool RoleMatches(int trackIndex, UnitRole role)
        {
            switch (trackIndex)
            {
                case 0: return Contains(ChitinousHideRoles, role);
                case 1: return Contains(HollowBarbsRoles, role);
                case 2: return Contains(VacuumCollapseRoles, role);
                case 3: return Contains(RitualRoles, role);
                case 4: return Contains(UnnervingAimRoles, role);
                case 5: return Contains(FrenzyRoles, role);
                case 6: return Contains(SplashRoles, role);
                case 7: return Contains(HollowBonesRoles, role);
                case 8:
                    // Void Hardening affects every regular unit.
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
