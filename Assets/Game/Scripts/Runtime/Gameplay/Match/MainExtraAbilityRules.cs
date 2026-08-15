namespace Game.Gameplay.Match
{
    /// <summary>
    /// Pure rules for the post-Divine-Blessing main extra ability pick (PRE-003 stubs).
    /// Combat effects land in PRE-005 — this only gates unlock + one permanent pick.
    /// </summary>
    public static class MainExtraAbilityRules
    {
        public const int None = 0;
        public const int AbilityCount = 6;
        public const int CommandSlotIndex = 9;

        public static bool IsValidId(int abilityId) =>
            abilityId >= 1 && abilityId <= AbilityCount;

        public static bool IsUnlocked(
            int abilityId,
            int meleeLevel,
            int rangedLevel,
            int armorLevel,
            int passiveGoldLevel,
            int magicLevel)
        {
            return abilityId switch
            {
                1 => magicLevel >= 1,
                2 => meleeLevel >= 3,
                3 => armorLevel >= 3 || rangedLevel >= 3,
                4 => passiveGoldLevel >= 6,
                5 => magicLevel >= 2
                     && (meleeLevel >= 6 || rangedLevel >= 6 || armorLevel >= 6),
                6 => meleeLevel >= 6
                     && rangedLevel >= 6
                     && armorLevel >= 6
                     && magicLevel >= 3,
                _ => false,
            };
        }

        public static bool IsUnlocked(int abilityId, MatchPlayerState player)
        {
            if (player == null)
            {
                return false;
            }

            return IsUnlocked(
                abilityId,
                player.MeleeDamageLevel,
                player.RangedDamageLevel,
                player.HpArmorLevel,
                player.PassiveGoldLevel,
                player.MagicLevel);
        }

        public static bool CanPick(MatchPlayerState player, int abilityId)
        {
            if (player == null
                || !player.DivineBlessingComplete
                || player.MainExtraAbilityId != None
                || !IsValidId(abilityId))
            {
                return false;
            }

            return IsUnlocked(abilityId, player);
        }

        public static string GetDisplayName(int abilityId) =>
            IsValidId(abilityId) ? $"Способность {abilityId}" : string.Empty;

        public static string GetGateDescription(int abilityId) => abilityId switch
        {
            1 => "Магия ≥ 1",
            2 => "Урон мили ≥ 3",
            3 => "ХП/Броня ≥ 3 или урон дальн. ≥ 3",
            4 => "Passive Gold ≥ 6",
            5 => "Любой стат ≥ 6 и магия ≥ 2",
            6 => "Все статы ≥ 6 и магия ≥ 3",
            _ => string.Empty,
        };
    }
}
