using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Resolves a combat unit's stats. The prefab's <see cref="UnitCombatSettings"/> holds no numbers —
    /// only references to its source definition assets. Resolution reads the referenced
    /// <see cref="UnitDefinition"/> / <see cref="HeroDefinition"/> and derives hero / titan / veteran
    /// variants with the canonical multipliers (<see cref="TitanRules"/>, <see cref="BonusKitRules"/>).
    /// If a prefab has no source reference, the race catalog supplies the same definition by key.
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
        /// Prefab or definition stats without race upgrades or hero level growth. Titan stats are
        /// always hero slot 1 scaled to 3×; veteran (bonus slots 7–10) stats derive from the base
        /// hero with the canonical veteran multipliers.
        /// </summary>
        public static UnitCombatStats ResolveBase(
            ICombatUnitCatalog catalog,
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            int heroSlot = 0,
            int bonusSlot = 0)
        {
            var hasBonusKit = BonusKitRules.HasBonusKit(raceId);
            if (hasBonusKit
                && BonusKitRules.IsChampionBonusSlot(bonusSlot)
                && BonusKitRules.MatchesUnit(bonusSlot, role, heroSlot))
            {
                if (TryBuildFromPrefab(visualCatalog, raceId, role, heroSlot, bonusSlot, out var veteranStats))
                {
                    return veteranStats;
                }

                var hero = catalog?.GetRace(raceId)?.GetHeroBySlot(ResolveChampionBaseSlot(role, heroSlot));
                if (hero != null)
                {
                    return ResolveFromHero(hero, role, heroSlot, bonusSlot);
                }

                return BonusKitRules.ApplyVeteranMultipliers(
                    ResolveBase(catalog, visualCatalog, raceId, role, heroSlot));
            }

            if (hasBonusKit && BonusKitRules.BonusSlotForRole(role) == bonusSlot)
            {
                if (TryBuildFromPrefab(visualCatalog, raceId, role, heroSlot, bonusSlot, out var bonusStats))
                {
                    return bonusStats;
                }

                var bonusDefinition = catalog?.GetRace(raceId)?.GetUnitBonus(role);
                if (bonusDefinition != null)
                {
                    return UnitCombatStats.FromDefinition(bonusDefinition);
                }
            }

            if (TryBuildFromPrefab(visualCatalog, raceId, role, heroSlot, 0, out var baseStats))
            {
                return baseStats;
            }

            return ResolveFromCatalog(catalog, raceId, role, heroSlot, bonusSlot);
        }

        /// <summary>
        /// Stats for a single <see cref="UnitCombatSettings"/> resolved from its referenced source.
        /// <paramref name="role"/>/<paramref name="heroSlot"/>/<paramref name="bonusSlot"/> decide which
        /// hero variant (base / veteran) applies when the source is a <see cref="HeroDefinition"/>.
        /// </summary>
        public static UnitCombatStats BuildFromSettings(
            UnitCombatSettings settings,
            UnitRole role,
            int heroSlot = 0,
            int bonusSlot = 0)
        {
            if (settings == null)
            {
                return DefaultStats(role);
            }

            var hero = settings.HeroDefinition;
            if (hero != null)
            {
                return ResolveFromHero(hero, role, heroSlot, bonusSlot);
            }

            var definition = settings.UnitDefinition;
            if (definition != null)
            {
                return UnitCombatStats.FromDefinition(definition);
            }

            return DefaultStats(role);
        }

        /// <summary>
        /// If the source is a hero and the key maps to a champion bonus slot (veteran/titan-veteran),
        /// apply the veteran multipliers; otherwise scale a titan to 3× and pass a base hero through.
        /// </summary>
        static UnitCombatStats ResolveFromHero(HeroDefinition hero, UnitRole role, int heroSlot, int bonusSlot)
        {
            if (BonusKitRules.IsChampionBonusSlot(bonusSlot) && BonusKitRules.MatchesUnit(bonusSlot, role, heroSlot))
            {
                return ResolveVeteran(hero, role);
            }

            var baseStats = FromHero(hero, role);
            return role == UnitRole.Titan ? TitanRules.ScaleForTitan(baseStats) : baseStats;
        }

        /// <summary>
        /// Canonical veteran numbers. Matches the historical pre-baked prefab values:
        /// HP ×1.4, damage ×1.35, armor +2, range = base hero range; titan additionally ×3
        /// (armor = hero ×3 +2) and range = <see cref="TitanRules.AttackRange"/>.
        /// </summary>
        static UnitCombatStats ResolveVeteran(HeroDefinition hero, UnitRole role)
        {
            var hp = hero.MaxHp * BonusKitRules.VeteranHpMultiplier;
            var damageMin = hero.DamageMin * BonusKitRules.VeteranDamageMultiplier;
            var damageMax = hero.DamageMax * BonusKitRules.VeteranDamageMultiplier;
            var armor = hero.Armor + BonusKitRules.VeteranArmorBonus;
            var range = hero.AttackRange;

            if (role == UnitRole.Titan)
            {
                hp *= TitanRules.BaseStatMultiplier;
                damageMin *= TitanRules.BaseStatMultiplier;
                damageMax *= TitanRules.BaseStatMultiplier;
                armor = hero.Armor * TitanRules.BaseStatMultiplier + BonusKitRules.VeteranArmorBonus;
                range = TitanRules.AttackRange;
            }

            return new UnitCombatStats(
                role,
                hp,
                armor,
                damageMin,
                damageMax,
                hero.AttackSpeed,
                range,
                hero.MoveSpeed,
                hero.GoldBounty);
        }

        static bool TryBuildFromPrefab(
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role,
            int heroSlot,
            int bonusSlot,
            out UnitCombatStats stats)
        {
            stats = default;
            if (visualCatalog == null
                || !visualCatalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var prefab)
                || prefab == null)
            {
                return false;
            }

            var settings = prefab.GetComponentInChildren<UnitCombatSettings>();
            if (settings == null || !settings.HasStatsSource)
            {
                return false;
            }

            stats = BuildFromSettings(settings, role, heroSlot, bonusSlot);
            return true;
        }

        static UnitCombatStats ResolveFromCatalog(
            ICombatUnitCatalog catalog,
            string raceId,
            UnitRole role,
            int heroSlot,
            int bonusSlot)
        {
            var race = catalog?.GetRace(raceId);

            if (role == UnitRole.Hero)
            {
                var slot = heroSlot >= 1 ? heroSlot : 1;
                var hero = race != null ? race.GetHeroBySlot(slot) : null;
                return hero != null
                    ? ResolveFromHero(hero, role, slot, bonusSlot)
                    : ChampionFallback(UnitRole.Hero);
            }

            if (role == UnitRole.Titan)
            {
                var hero = race != null ? race.GetHeroBySlot(1) : null;
                if (hero != null)
                {
                    return ResolveFromHero(hero, UnitRole.Titan, 1, bonusSlot);
                }

                return TitanRules.ScaleForTitan(ChampionFallback(UnitRole.Titan));
            }

            var definition = race?.GetUnit(role);
            return definition != null ? UnitCombatStats.FromDefinition(definition) : DefaultStats(role);
        }

        static int ResolveChampionBaseSlot(UnitRole role, int heroSlot) => role == UnitRole.Titan ? 1 : heroSlot;

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

        static UnitCombatStats DefaultStats(UnitRole role) =>
            new(role, 200f, 1f, 10f, 14f, 1f, 1.5f, 3.5f, 20);
    }
}