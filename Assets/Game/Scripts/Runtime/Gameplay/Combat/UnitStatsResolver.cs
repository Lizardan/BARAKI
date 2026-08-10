using Game.Gameplay.Data;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Resolves a combat unit's stats. Prefab-based <see cref="UnitBalanceSettings"/> are
    /// authoritative when present (balance lives on the prefab); otherwise falls back to the
    /// race's <see cref="UnitDefinition"/> ScriptableObject.
    /// </summary>
    public static class UnitStatsResolver
    {
        public static UnitCombatStats Resolve(
            ICombatUnitCatalog catalog,
            UnitVisualCatalog visualCatalog,
            string raceId,
            UnitRole role)
        {
            if (visualCatalog != null
                && visualCatalog.TryGetPrefab(raceId, role, out var prefab)
                && prefab != null)
            {
                var settings = prefab.GetComponentInChildren<UnitBalanceSettings>();
                if (settings != null)
                {
                    return BuildFromSettings(settings, role);
                }
            }

            var definition = catalog?.GetRace(raceId)?.GetUnit(role);
            if (definition != null)
            {
                return UnitCombatStats.FromDefinition(definition);
            }

            return DefaultStats(role);
        }

        public static UnitCombatStats BuildFromSettings(UnitBalanceSettings settings, UnitRole role)
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
                settings.AttackRange,
                settings.MoveSpeed,
                settings.GoldBounty,
                ResolveMaxMana(settings, role));
        }

        static float ResolveMaxMana(UnitBalanceSettings settings, UnitRole role)
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
