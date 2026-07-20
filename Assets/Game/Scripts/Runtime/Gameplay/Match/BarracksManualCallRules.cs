using System;
using Game.Gameplay.Data;

namespace Game.Gameplay.Match
{
    public static class BarracksManualCallRules
    {
        public const float RegenSeconds = 30f;

        public static int GetGoldCost(UnitRole role) => role switch
        {
            UnitRole.Melee => 50,
            UnitRole.Ranged => 70,
            UnitRole.Caster => 80,
            UnitRole.Siege => 100,
            UnitRole.Flying => 150,
            UnitRole.Super => 150,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Manual call is not supported for this role."),
        };

        public static int GetMaxCharges(SquadCompositionDefinition composition, UnitRole role)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            return GetMaxCharges((ISquadCounts)composition, role);
        }

        public static int GetMaxCharges(ISquadCounts counts, UnitRole role)
        {
            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }

            return GetMaxCharges(role, counts.GetCount(role));
        }

        public static int GetMaxCharges(UnitRole role, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (!IsCallableRole(role))
            {
                return 0;
            }

            return count;
        }

        public static bool CanCall(bool enoughGold, int charges, bool barracksIntact, bool notEliminated) =>
            enoughGold && charges > 0 && barracksIntact && notEliminated;

        /// <summary>Fallback squad counts when RaceCatalog is unavailable (Edit Mode).</summary>
        public static ISquadCounts GetDefaultSquadCounts(int barracksLevel) => barracksLevel switch
        {
            2 => new FixedSquadCounts(3, 1, 1, 2, 0, 0),
            3 => new FixedSquadCounts(3, 2, 2, 2, 1, 0),
            4 => new FixedSquadCounts(4, 3, 2, 3, 1, 1),
            _ => new FixedSquadCounts(2, 1, 1, 0, 0, 0),
        };

        internal static bool IsCallableRole(UnitRole role) => role switch
        {
            UnitRole.Melee => true,
            UnitRole.Ranged => true,
            UnitRole.Caster => true,
            UnitRole.Siege => true,
            UnitRole.Flying => true,
            UnitRole.Super => true,
            _ => false,
        };

        sealed class FixedSquadCounts : ISquadCounts
        {
            readonly int _melee;
            readonly int _ranged;
            readonly int _caster;
            readonly int _siege;
            readonly int _flying;
            readonly int _super;

            public FixedSquadCounts(int melee, int ranged, int caster, int siege, int flying, int super)
            {
                _melee = melee;
                _ranged = ranged;
                _caster = caster;
                _siege = siege;
                _flying = flying;
                _super = super;
            }

            public int GetCount(UnitRole role) => role switch
            {
                UnitRole.Melee => _melee,
                UnitRole.Ranged => _ranged,
                UnitRole.Caster => _caster,
                UnitRole.Siege => _siege,
                UnitRole.Flying => _flying,
                UnitRole.Super => _super,
                _ => 0,
            };
        }
    }
}
