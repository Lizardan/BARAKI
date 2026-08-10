using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Uniform-grid spatial hash for O(1) neighbour queries during target scan.
    /// </summary>
    public sealed class CombatSpatialGrid
    {
        const float CellSize = 12f;

        readonly Dictionary<(int, int), List<MatchUnitState>> _cells = new();

        public void Rebuild(IReadOnlyList<MatchUnitState> units)
        {
            Clear();
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsAlive) continue;
                var key = GetKey(unit.WorldPosition);
                if (!_cells.TryGetValue(key, out var cell))
                {
                    cell = new List<MatchUnitState>();
                    _cells[key] = cell;
                }
                cell.Add(unit);
            }
        }

        public void Clear()
        {
            foreach (var kvp in _cells)
                kvp.Value.Clear();
            _cells.Clear();
        }

        public void Query(Vector3 position, float radius, List<MatchUnitState> results)
        {
            var minCx = (int)Mathf.Floor((position.x - radius) / CellSize);
            var maxCx = (int)Mathf.Floor((position.x + radius) / CellSize);
            var minCz = (int)Mathf.Floor((position.z - radius) / CellSize);
            var maxCz = (int)Mathf.Floor((position.z + radius) / CellSize);

            for (var cx = minCx; cx <= maxCx; cx++)
            {
                for (var cz = minCz; cz <= maxCz; cz++)
                {
                    if (!_cells.TryGetValue((cx, cz), out var cell))
                        continue;
                    for (var i = 0; i < cell.Count; i++)
                        results.Add(cell[i]);
                }
            }
        }

        static (int, int) GetKey(Vector3 position)
        {
            return ((int)Mathf.Floor(position.x / CellSize),
                    (int)Mathf.Floor(position.z / CellSize));
        }
    }
}
