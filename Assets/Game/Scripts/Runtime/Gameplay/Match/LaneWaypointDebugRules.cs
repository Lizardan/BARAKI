using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Waypoint samples for editor lane debug overlay.</summary>
    public static class LaneWaypointDebugRules
    {
        public static void AppendWaypoints(LanePath path, List<Vector3> into)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            for (var i = 0; i < path.WaypointCount; i++)
            {
                var point = path.GetWaypoint(i);
                point.y = 0f;
                into.Add(point);
            }
        }
    }
}
