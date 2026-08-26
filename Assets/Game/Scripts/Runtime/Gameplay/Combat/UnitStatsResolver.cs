using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Resolves a combat unit's stats. Prefab-based <see cref="UnitCombatSettings"/> are
    /// authoritative runtime snapshots when present; otherwise falls back to the race's
    /// <see cref="UnitDefinition"/> / <see cref="HeroDefinition"/> source assets.
    /// </summary>
    public static class UnitStatsResolver
    {
        public static UnitCombatStats Resolve(
            ICombatUnitCatalog catalog,
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            MatchPlayerState player = null,
            int heroSlot = 0,
            int bonusSlot = 0)
        {
            var stats = ResolveBase(catalog, visualCatalog, raceId, role, heroSlot, bonusSlot);
            stats = RaceUpgradeStatsRules.Apply(stats, player);
            return TowerTrackUnitRules.Apply(stats, player);
        }

        /// <summary>
        /// Prefab or definition stats without race upgrades or hero level growth.
        /// Titan prefab settings are used as-is; missing titan settings fall back to 3× hero slot 1.
        /// </summary>
        public static UnitCombatStats ResolveBase(
            ICombatUnitCatalog catalog,
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            int heroSlot = 0,
            int bonusSlot = 0)
        {
            if (HumanBonusUnitRules.IsChampionBonusSlot(bonusSlot)
                && HumanBonusUnitRules.MatchesUnit(bonusSlot, role, heroSlot))
            {
                if (visualCatalog != null
                    && visualCatalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var veteranPrefab)
                    && veteranPrefab != null)
                {
                    var veteranSettings = veteranPrefab.GetComponentInChildren<UnitCombatSettings>();
                    if (veteranSettings != null)
                    {
                        return BuildFromSettings(veteranSettings, role);
                    }
                }

                return HumanBonusUnitRules.ApplyVeteranMultipliers(
                    ResolveBase(catalog, visualCatalog, raceId, role, heroSlot));
            }

            if (HumanBonusUnitRules.BonusSlotForRole(role) == bonusSlot)
            {
                if (visualCatalog != null
                    && visualCatalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var bonusPrefab)
                    && bonusPrefab != null)
                {
                    var bonusSettings = bonusPrefab.GetComponentInChildren<UnitCombatSettings>();
                    if (bonusSettings != null)
                    {
                        return BuildFromSettings(bonusSettings, role);
                    }
                }

                var bonusDefinition = catalog?.GetRace(raceId)?.GetUnitBonus(role);
                if (bonusDefinition != null)
                {
                    return UnitCombatStats.FromDefinition(bonusDefinition);
                }
            }

            if (visualCatalog != null
                && visualCatalog.TryGetPrefab(raceId, role, heroSlot, out var prefab)
                && prefab != null)
            {
                var settings = prefab.GetComponentInChildren<UnitCombatSettings>();
                if (settings != null)
                {
                    return BuildFromSettings(settings, role);
                }
            }

            if (role == UnitRole.Hero)
            {
                var slot = heroSlot >= 1 ? heroSlot : 1;
                var hero = catalog?.GetRace(raceId)?.GetHeroBySlot(slot);
                return hero != null ? FromHero(hero, UnitRole.Hero) : ChampionFallback(UnitRole.Hero);
            }

            if (role == UnitRole.Titan)
            {
                var hero = catalog?.GetRace(raceId)?.GetHeroBySlot(1);
                var baseStats = hero != null
                    ? FromHero(hero, UnitRole.Titan)
                    : ChampionFallback(UnitRole.Titan);
                return TitanRules.ScaleForTitan(baseStats);
            }

            var definition = catalog?.GetRace(raceId)?.GetUnit(role);
            if (definition != null)
            {
                return UnitCombatStats.FromDefinition(definition);
            }

            return DefaultStats(role);
        }

        public static UnitCombatStats BuildFromSettings(UnitCombatSettings settings, UnitRole role)
        {
            if (settings == null)
            {
                return DefaultStats(role);
            }

            return new UnitCombatStats(
                role,
                settings.MaxHp,
                settings.Armor,
                settings.DamageMin,
                settings.DamageMax,
                settings.AttackSpeed,
                role == UnitRole.Titan ? TitanRules.AttackRange : settings.AttackRange,
                settings.MoveSpeed,
                settings.GoldBounty,
                ResolveMaxMana(settings, role));
        }

        static UnitCombatStats FromHero(HeroDefinition hero, UnitRole role) =>
            new(
                role,
                hero.MaxHp,
                hero.Armor,
                hero.DamageMin,
                hero.DamageMax,
                hero.AttackSpeed,
                hero.AttackRange,
                hero.MoveSpeed,
                hero.GoldBounty);

        static UnitCombatStats ChampionFallback(UnitRole role) =>
            new(role, 600f, 4f, 35f, 45f, 1f, 1.5f, 4f, 80);

        static float ResolveMaxMana(UnitCombatSettings settings, UnitRole role)
        {
            if (settings.MaxMana > 0f)
            {
                return settings.MaxMana;
            }

            return role == UnitRole.Caster ? 200f : 0f;
        }

        static UnitCombatStats DefaultStats(UnitRole role) =>
            new(role, 200f, 1f, 10f, 14f, 1f, 1.5f, 3.5f, 20);
    }
}
