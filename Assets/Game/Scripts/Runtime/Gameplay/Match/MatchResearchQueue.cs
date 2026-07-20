using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    /// <summary>Up to <see cref="MaxQueueLength"/> research jobs per building (head is active).</summary>
    public sealed class MatchResearchQueue
    {
        public const int MaxQueueLength = 3;

        readonly Dictionary<int, List<BuildingResearchState>> _byBuildingInstanceId = new();

        public void Clear() => _byBuildingInstanceId.Clear();

        /// <summary>Replaces all queues from an authoritative snapshot (no completion side-effects).</summary>
        public void ReplaceAll(IReadOnlyList<BuildingResearchState> items)
        {
            Clear();
            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                TryEnqueue(items[i]);
            }
        }

        public bool TryGetActive(int buildingInstanceId, out BuildingResearchState research)
        {
            if (_byBuildingInstanceId.TryGetValue(buildingInstanceId, out var queue)
                && queue.Count > 0)
            {
                research = queue[0];
                return true;
            }

            research = null;
            return false;
        }

        /// <summary>Legacy alias for active (head) research.</summary>
        public bool TryGet(int buildingInstanceId, out BuildingResearchState research) =>
            TryGetActive(buildingInstanceId, out research);

        public bool TryGetQueue(int buildingInstanceId, out IReadOnlyList<BuildingResearchState> queue)
        {
            if (_byBuildingInstanceId.TryGetValue(buildingInstanceId, out var list) && list.Count > 0)
            {
                queue = list;
                return true;
            }

            queue = System.Array.Empty<BuildingResearchState>();
            return false;
        }

        public int GetCount(int buildingInstanceId) =>
            _byBuildingInstanceId.TryGetValue(buildingInstanceId, out var queue) ? queue.Count : 0;

        public bool HasActive(int buildingInstanceId) => GetCount(buildingInstanceId) > 0;

        public bool HasSpace(int buildingInstanceId) => GetCount(buildingInstanceId) < MaxQueueLength;

        public int CountUpgrade(int buildingInstanceId, string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)
                || !_byBuildingInstanceId.TryGetValue(buildingInstanceId, out var queue))
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i].UpgradeId == upgradeId)
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryEnqueue(BuildingResearchState research)
        {
            if (research == null)
            {
                return false;
            }

            if (!_byBuildingInstanceId.TryGetValue(research.BuildingInstanceId, out var queue))
            {
                queue = new List<BuildingResearchState>(MaxQueueLength);
                _byBuildingInstanceId[research.BuildingInstanceId] = queue;
            }

            if (queue.Count >= MaxQueueLength)
            {
                return false;
            }

            queue.Add(research);
            return true;
        }

        /// <summary>Deprecated name kept for call sites; enqueues when space available.</summary>
        public bool TryBegin(BuildingResearchState research) => TryEnqueue(research);

        public IEnumerable<int> BuildingInstanceIds
        {
            get
            {
                foreach (var pair in _byBuildingInstanceId)
                {
                    if (pair.Value.Count > 0)
                    {
                        yield return pair.Key;
                    }
                }
            }
        }

        public List<BuildingResearchState> Tick(float deltaTime)
        {
            var completed = new List<BuildingResearchState>();
            if (deltaTime <= 0f || _byBuildingInstanceId.Count == 0)
            {
                return completed;
            }

            var keys = new List<int>(_byBuildingInstanceId.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                var buildingId = keys[i];
                if (!_byBuildingInstanceId.TryGetValue(buildingId, out var queue) || queue.Count == 0)
                {
                    continue;
                }

                var head = queue[0];
                head.RemainingSeconds -= deltaTime;
                if (head.RemainingSeconds > 0f)
                {
                    continue;
                }

                head.RemainingSeconds = 0f;
                completed.Add(head);
                queue.RemoveAt(0);
                if (queue.Count == 0)
                {
                    _byBuildingInstanceId.Remove(buildingId);
                }
            }

            return completed;
        }
    }
}
