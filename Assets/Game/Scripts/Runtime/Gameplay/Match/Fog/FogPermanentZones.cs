using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Fog
{
    /// <summary>Always-clear fog regions for the local player (base pad + own road halves).</summary>
    public sealed class FogPermanentZones
    {
        readonly List<Vector3[]> _roadPolylines = new();

        public FogOrientedRect BasePad { get; set; }

        public float RoadHalfWidth { get; set; } = MatchArenaGreyboxBuilder.RoadWidth * 0.5f;

        public IReadOnlyList<Vector3[]> RoadPolylines => _roadPolylines;

        public void AddRoadPolyline(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            var copy = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                p.y = 0f;
                copy[i] = p;
            }

            _roadPolylines.Add(copy);
        }

        public bool Contains(Vector3 worldPosition)
        {
            var flat = worldPosition;
            flat.y = 0f;

            if (BasePad.ContainsXZ(flat))
            {
                return true;
            }

            var halfWidth = Mathf.Max(0.01f, RoadHalfWidth);
            var halfWidthSq = halfWidth * halfWidth;
            for (var i = 0; i < _roadPolylines.Count; i++)
            {
                if (DistanceSqToPolylineXZ(flat, _roadPolylines[i]) <= halfWidthSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static float DistanceSqToPolylineXZ(Vector3 point, Vector3[] polyline)
        {
            var best = float.MaxValue;
            for (var i = 0; i < polyline.Length - 1; i++)
            {
                var d = DistanceSqToSegmentXZ(point, polyline[i], polyline[i + 1]);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        public static float DistanceSqToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            var abx = b.x - a.x;
            var abz = b.z - a.z;
            var lengthSq = abx * abx + abz * abz;
            if (lengthSq < 1e-8f)
            {
                var dx0 = point.x - a.x;
                var dz0 = point.z - a.z;
                return dx0 * dx0 + dz0 * dz0;
            }

            var t = ((point.x - a.x) * abx + (point.z - a.z) * abz) / lengthSq;
            t = Mathf.Clamp01(t);
            var cx = a.x + abx * t;
            var cz = a.z + abz * t;
            var dx = point.x - cx;
            var dz = point.z - cz;
            return dx * dx + dz * dz;
        }
    }

    public struct FogOrientedRect
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector2 HalfExtents;

        public bool ContainsXZ(Vector3 worldPosition)
        {
            var local = Quaternion.Inverse(Rotation) * (worldPosition - Center);
            return Mathf.Abs(local.x) <= HalfExtents.x && Mathf.Abs(local.z) <= HalfExtents.y;
        }
    }
}
