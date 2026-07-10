using System;
using System.Collections.Generic;
using Game.Gameplay.Data;

namespace Game.Gameplay.Combat
{
    public readonly struct SquadSpawnSlot
    {
        public SquadSpawnSlot(UnitRole role, int rowIndex, int indexInRow, int countInRow)
        {
            Role = role;
            RowIndex = rowIndex;
            IndexInRow = indexInRow;
            CountInRow = countInRow;
        }

        public UnitRole Role { get; }
        public int RowIndex { get; }
        public int IndexInRow { get; }
        public int CountInRow { get; }
    }

    public readonly struct SquadSpawnEntry
    {
        public SquadSpawnEntry(UnitRole role, int count)
        {
            Role = role;
            Count = count;
        }

        public UnitRole Role { get; }
        public int Count { get; }
    }

    public static class SquadSpawnRules
    {
        public static IReadOnlyList<SquadSpawnEntry> BuildEntries(SquadCompositionDefinition squad)
        {
            if (squad == null)
            {
                throw new ArgumentNullException(nameof(squad));
            }

            var entries = new List<SquadSpawnEntry>(6);
            AddEntry(entries, UnitRole.Melee, squad.MeleeCount);
            AddEntry(entries, UnitRole.Ranged, squad.RangedCount);
            AddEntry(entries, UnitRole.Caster, squad.CasterCount);
            AddEntry(entries, UnitRole.Siege, squad.SiegeCount);
            AddEntry(entries, UnitRole.Flying, squad.FlyingCount);
            AddEntry(entries, UnitRole.Super, squad.SuperCount);
            return entries;
        }

        /// <summary>Front-to-back rows: melee → ranged → caster → siege → flying → super.</summary>
        public static IReadOnlyList<SquadSpawnSlot> BuildSpawnPlan(SquadCompositionDefinition squad)
        {
            if (squad == null)
            {
                throw new ArgumentNullException(nameof(squad));
            }

            var slots = new List<SquadSpawnSlot>(squad.TotalUnits);
            var row = 0;
            row = AddRow(slots, UnitRole.Melee, squad.MeleeCount, row);
            row = AddRow(slots, UnitRole.Ranged, squad.RangedCount, row);
            row = AddRow(slots, UnitRole.Caster, squad.CasterCount, row);
            row = AddRow(slots, UnitRole.Siege, squad.SiegeCount, row);
            row = AddRow(slots, UnitRole.Flying, squad.FlyingCount, row);
            AddRow(slots, UnitRole.Super, squad.SuperCount, row);
            return slots;
        }

        public static int GetFrontRowIndex(SquadCompositionDefinition squad)
        {
            return 0;
        }

        public static int GetRearmostRowIndex(SquadCompositionDefinition squad)
        {
            var plan = BuildSpawnPlan(squad);
            var max = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                max = Math.Max(max, plan[i].RowIndex);
            }

            return max;
        }

        static int AddRow(List<SquadSpawnSlot> slots, UnitRole role, int count, int rowIndex)
        {
            if (count <= 0)
            {
                return rowIndex;
            }

            for (var i = 0; i < count; i++)
            {
                slots.Add(new SquadSpawnSlot(role, rowIndex, i, count));
            }

            return rowIndex + 1;
        }

        static void AddEntry(List<SquadSpawnEntry> entries, UnitRole role, int count)
        {
            if (count > 0)
            {
                entries.Add(new SquadSpawnEntry(role, count));
            }
        }
    }
}
