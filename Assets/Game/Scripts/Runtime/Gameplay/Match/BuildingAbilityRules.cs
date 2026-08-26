using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Always-available main building active abilities (MAIN-001), gated on the building level.
    /// Separate subsystem from the Divine Blessing pick (<see cref="MainExtraAbilityRules"/> ids 1–6).
    /// </summary>
    public static class BuildingAbilityRules
    {
        public const int None = 0;
        public const int IceRingId = 1;
        public const int WaveOfLightId = 2;
        public const int AbilityCount = 2;

        /// <summary>UI command slots on the main building panel (0..11); Divine Blessing owns slot 11.</summary>
        public const int IceRingSlotIndex = 9;
        public const int WaveOfLightSlotIndex = 10;

        public const int IceRingRequiredMainLevel = 1;
        public const int WaveOfLightRequiredMainLevel = 2;

        public const float IceRingManaCost = 50f;
        public const float WaveOfLightManaCost = 150f;
        public const float IceRingCooldownSeconds = 60f;
        public const float WaveOfLightCooldownSeconds = 180f;

        /// <summary>Placeholder tuning (MAIN-001): final numbers at playtest.</summary>
        public const float IceRingDamage = 300f;
        public const float WaveOfLightDamage = 1500f;
        public const float IceRingFreezeSeconds = 3f;

        /// <summary>Ice Ring AoE radius — same circle as the caster Frost spell.</summary>
        public const float IceRingRadius = CasterSpellRules.FrostRadius;

        /// <summary>Wave of Light expands from the base to full radius over this many seconds.</summary>
        public const float WaveOfLightExpandSeconds = 1f;

        /// <summary>Ice Ring cast range from base = main→center-barracks distance × this.</summary>
        public const float IceRingCastRangeFactor = 3f;
        /// <summary>Wave of Light effect radius from base = main→center-barracks distance × this.</summary>
        public const float WaveOfLightRadiusFactor = 3f;

        public static bool IsValidId(int abilityId) =>
            abilityId is IceRingId or WaveOfLightId;

        public static int GetRequiredMainLevel(int abilityId) => abilityId switch
        {
            IceRingId => IceRingRequiredMainLevel,
            WaveOfLightId => WaveOfLightRequiredMainLevel,
            _ => int.MaxValue,
        };

        public static float GetManaCost(int abilityId) => abilityId switch
        {
            IceRingId => IceRingManaCost,
            WaveOfLightId => WaveOfLightManaCost,
            _ => 0f,
        };

        public static float GetCooldownSeconds(int abilityId) => abilityId switch
        {
            IceRingId => IceRingCooldownSeconds,
            WaveOfLightId => WaveOfLightCooldownSeconds,
            _ => 0f,
        };

        public static float GetDamage(int abilityId) => abilityId switch
        {
            IceRingId => IceRingDamage,
            WaveOfLightId => WaveOfLightDamage,
            _ => 0f,
        };

        /// <summary>Frozen seconds applied by the Ice Ring (0 for other abilities).</summary>
        public static float GetFreezeSeconds(int abilityId) =>
            abilityId == IceRingId ? IceRingFreezeSeconds : 0f;

        /// <summary>
        /// Ground units only (no Flying), enemies only, alive only. Heroes and titans walk — they are hit.
        /// </summary>
        public static bool AffectsUnit(MatchUnitState unit, int casterOwnerSlot) =>
            unit != null
            && unit.IsAlive
            && unit.OwnerSlot != casterOwnerSlot
            && unit.Role != UnitRole.Flying;

        public static bool IsUnlocked(int abilityId, int mainLevel) =>
            IsValidId(abilityId) && mainLevel >= GetRequiredMainLevel(abilityId);

        public static bool CanCast(MatchPlayerState player, int abilityId)
        {
            if (player == null || !IsUnlocked(abilityId, player.MainLevel))
            {
                return false;
            }

            return player.MainMana >= GetManaCost(abilityId)
                   && GetCooldownRemaining(player, abilityId) <= 0f;
        }

        public static float GetCooldownRemaining(MatchPlayerState player, int abilityId) =>
            abilityId switch
            {
                IceRingId => player?.IceRingCooldownRemaining ?? 0f,
                WaveOfLightId => player?.WaveOfLightCooldownRemaining ?? 0f,
                _ => 0f,
            };

        public static void SetCooldown(MatchPlayerState player, int abilityId, float seconds)
        {
            if (player == null)
            {
                return;
            }

            switch (abilityId)
            {
                case IceRingId:
                    player.IceRingCooldownRemaining = seconds;
                    break;
                case WaveOfLightId:
                    player.WaveOfLightCooldownRemaining = seconds;
                    break;
            }
        }

        public static void TickCooldowns(MatchPlayerState player, float deltaTime)
        {
            if (player == null)
            {
                return;
            }

            if (player.IceRingCooldownRemaining > 0f)
            {
                player.IceRingCooldownRemaining =
                    Mathf.Max(0f, player.IceRingCooldownRemaining - deltaTime);
            }

            if (player.WaveOfLightCooldownRemaining > 0f)
            {
                player.WaveOfLightCooldownRemaining =
                    Mathf.Max(0f, player.WaveOfLightCooldownRemaining - deltaTime);
            }
        }

        /// <summary>Main→center-barracks distance of the owner base; 0 when layout is unavailable.</summary>
        public static float GetBaseToBarracksDistance(MatchArenaLayout layout, int ownerSlot)
        {
            if (layout == null || ownerSlot < 0 || ownerSlot >= layout.Slots.Count)
            {
                return 0f;
            }

            var slot = layout.Slots[ownerSlot];
            var main = slot.GetBuildingWorldPosition(GameIds.Buildings.Main);
            var barracks = slot.GetBuildingWorldPosition(GameIds.Buildings.BarracksCenter);
            main.y = 0f;
            barracks.y = 0f;
            return Vector3.Distance(main, barracks);
        }

        public static float GetIceRingCastRange(float baseToBarracksDistance) =>
            baseToBarracksDistance * IceRingCastRangeFactor;

        public static float GetWaveOfLightRadius(float baseToBarracksDistance) =>
            baseToBarracksDistance * WaveOfLightRadiusFactor;

        /// <summary>Horizontal (XZ) distance check used for the Ice Ring cast range.</summary>
        public static bool IsInCastRange(Vector3 basePosition, Vector3 center, float castRange)
        {
            var dx = center.x - basePosition.x;
            var dz = center.z - basePosition.z;
            return dx * dx + dz * dz <= castRange * castRange;
        }

        public static string GetDisplayName(int abilityId) => abilityId switch
        {
            IceRingId => "Ледяное кольцо",
            WaveOfLightId => "Волна света",
            _ => string.Empty,
        };

        public static string GetGateDescription(int abilityId)
        {
            if (!IsValidId(abilityId))
            {
                return string.Empty;
            }

            return $"Уровень главного здания {GetRequiredMainLevel(abilityId)}+";
        }

        public static string GetEffectDescription(int abilityId) => abilityId switch
        {
            IceRingId =>
                $"Замораживает врагов в круге на {IceRingFreezeSeconds:0} с и наносит {IceRingDamage:0} урона\n" +
                $"Радиус области {IceRingRadius:0}\n" +
                $"Применяется только рядом с базой · Перезарядка {IceRingCooldownSeconds:0}с · {IceRingManaCost:0} маны",
            WaveOfLightId =>
                $"Волна от базы бьёт всех врагов в радиусе ({WaveOfLightRadiusFactor:0}× до казарм) на {WaveOfLightDamage:0} урона\n" +
                $"Волна расширяется за {WaveOfLightExpandSeconds:0} с — урон наносится фронтом\n" +
                $"Перезарядка {WaveOfLightCooldownSeconds:0}с · {WaveOfLightManaCost:0} маны",
            _ => string.Empty,
        };

        public static string GetTooltip(int abilityId)
        {
            var name = GetDisplayName(abilityId);
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            return $"{name}\n{GetEffectDescription(abilityId)}\nТребуется: {GetGateDescription(abilityId)}";
        }
    }
}
