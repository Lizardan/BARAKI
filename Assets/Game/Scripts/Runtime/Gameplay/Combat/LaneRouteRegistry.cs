using System.Collections.Generic;
using Game.Gameplay.Match;

namespace Game.Gameplay.Combat
{
    public sealed class LaneRouteRegistry
    {
        readonly Dictionary<(int ownerSlot, string laneId), LaneRoute> _routes = new();

        public static LaneRouteRegistry Build(LaneGraph graph, float sampleSpacing = 10f)
        {
            var registry = new LaneRouteRegistry();
            if (graph?.Lanes == null)
            {
                return registry;
            }

            foreach (var lane in graph.Lanes)
            {
                if (lane?.Path == null)
                {
                    continue;
                }

                registry._routes[(lane.OwnerSlot, lane.LaneId)] = LaneRoute.FromPath(lane.Path, sampleSpacing);
            }

            return registry;
        }

        public bool TryGetRoute(int ownerSlot, string laneId, out LaneRoute route)
        {
            return _routes.TryGetValue((ownerSlot, laneId), out route);
        }
    }
}
