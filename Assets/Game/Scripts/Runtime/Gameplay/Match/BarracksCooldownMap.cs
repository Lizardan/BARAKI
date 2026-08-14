using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Per-barracks remaining seconds. Tick copies keys first so the dictionary
    /// is never mutated during enumeration (.NET invalidates on indexer set).
    /// </summary>
    public sealed class BarracksCooldownMap
    {
        readonly Dictionary<int, float> _remaining = new();
        readonly List<int> _tickKeys = new();

        public float Get(int barracksInstanceId) =>
            _remaining.TryGetValue(barracksInstanceId, out var remaining)
                ? remaining
                : 0f;

        public void Set(int barracksInstanceId, float remainingSeconds)
        {
            if (remainingSeconds <= 0f)
            {
                _remaining.Remove(barracksInstanceId);
                return;
            }

            _remaining[barracksInstanceId] = remainingSeconds;
        }

        public void Clear() => _remaining.Clear();

        public void Tick(float deltaTime)
        {
            if (_remaining.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            _tickKeys.Clear();
            foreach (var key in _remaining.Keys)
            {
                _tickKeys.Add(key);
            }

            for (var i = 0; i < _tickKeys.Count; i++)
            {
                var barracksId = _tickKeys[i];
                var remaining = _remaining[barracksId] - deltaTime;
                if (remaining <= 0f)
                {
                    _remaining.Remove(barracksId);
                }
                else
                {
                    _remaining[barracksId] = remaining;
                }
            }
        }
    }
}
