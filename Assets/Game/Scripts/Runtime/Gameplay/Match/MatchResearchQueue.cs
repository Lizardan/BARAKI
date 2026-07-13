using System.Collections.Generic;

namespace Game.Gameplay.Match
{
    /// <summary>One active research per building instance (MVP).</summary>
    public sealed class MatchResearchQueue
    {
        readonly Dictionary<int, BuildingResearchState> _byBuildingInstanceId = new();

        public IReadOnlyDictionary<int, BuildingResearchState> Active => _byBuildingInstanceId;

        public void Clear() => _byBuildingInstanceId.Clear();

        public bool TryGet(int buildingInstanceId, out BuildingResearchState research) =>
            _byBuildingInstanceId.TryGetValue(buildingInstanceId, out research);

        public bool HasActive(int buildingInstanceId) =>
            _byBuildingInstanceId.ContainsKey(buildingInstanceId);

        public bool TryBegin(BuildingResearchState research)
        {
            if (research == null || _byBuildingInstanceId.ContainsKey(research.BuildingInstanceId))
            {
                return false;
            }

            _byBuildingInstanceId[research.BuildingInstanceId] = research;
            return true;
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
                var research = _byBuildingInstanceId[keys[i]];
                research.RemainingSeconds -= deltaTime;
                if (research.RemainingSeconds > 0f)
                {
                    continue;
                }

                research.RemainingSeconds = 0f;
                completed.Add(research);
                _byBuildingInstanceId.Remove(keys[i]);
            }

            return completed;
        }
    }
}
