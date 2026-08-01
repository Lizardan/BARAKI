using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Flank ring for N≥3 (except hand-tuned N=4):
    /// short straight base exits → curve onto the perimeter → edge between bases.
    /// N=3 uses straight edges; N=5..8 keep circular arcs between joins.
    /// </summary>
    public static class CircularRingRoadGeometry
    {
        /// <summary>Short straight run from side barracks before the curve onto the ring.</summary>
        public static float ExitStraightLength => MatchArenaGreyboxBuilder.RoadWidth * 0.75f;

        public static float JunctionFilletRadius => RoadJunctionBuilder.CenterLineTurnRadius;

        /// <summary>Handle length for the exit→perimeter easing curve (matches N=2/N=4 corner scale).</summary>
        public static float ExitCurveRadius => N4RoadReferenceSpec.PerimeterCornerCenterlineRadius;

        /// <summary>
        /// How far along the reference circle (arc length) past the exit tip the join sits.
        /// </summary>
        public static float ExitCurveArcLength => ExitCurveRadius * 2f;

        public const float ArcDockOverlapRadians = 0.05f;

        public const int RingArcSamplesPerSegment = 24;

        public const int ExitCurveSamples = 16;

        public static void GetBaseFrame(
            Vector3 basePosition,
            out Vector3 junction,
            out Vector3 radialOut,
            out Vector3 tangentCcw)
        {
            junction = basePosition;
            junction.y = 0f;
            if (junction.sqrMagnitude < 0.0001f)
            {
                radialOut = Vector3.right;
                tangentCcw = Vector3.forward;
                return;
            }

            radialOut = junction.normalized;
            tangentCcw = new Vector3(-radialOut.z, 0f, radialOut.x);
        }

        public static void GetSpokeJunction(
            Vector3 basePosition,
            out Vector3 junction,
            out Vector3 spokeDir,
            out Vector3 leftPerimeterDir,
            out Vector3 rightPerimeterDir)
        {
            GetBaseFrame(basePosition, out junction, out spokeDir, out var tangentCcw);
            leftPerimeterDir = tangentCcw;
            rightPerimeterDir = -tangentCcw;
        }

        public static Vector3 GetSideExitDir(PlayerSlotLayout slot, string barracksId)
        {
            var local = barracksId == Game.Core.GameIds.Buildings.BarracksLeft
                ? Vector3.left
                : Vector3.right;
            var world = slot.BaseRotation * local;
            world.y = 0f;
            return world.sqrMagnitude > 0.0001f ? world.normalized : local;
        }

        public static Vector3 GetSideExitEnd(PlayerSlotLayout slot, string barracksId)
        {
            var barracks = slot.GetBuildingWorldPosition(barracksId);
            barracks.y = 0f;
            return barracks + GetSideExitDir(slot, barracksId) * ExitStraightLength;
        }

        public static float GetSideExitAngle(PlayerSlotLayout slot, string barracksId)
        {
            var end = GetSideExitEnd(slot, barracksId);
            return Mathf.Atan2(end.z, end.x);
        }

        /// <summary>+1 if this exit travels CCW from the base, −1 if CW.</summary>
        public static float GetExitTravelSign(PlayerSlotLayout slot, string barracksId)
        {
            var tipAngle = GetSideExitAngle(slot, barracksId);
            var baseAngle = BaseAngle(slot.BasePosition);
            var delta = Mathf.DeltaAngle(baseAngle * Mathf.Rad2Deg, tipAngle * Mathf.Rad2Deg);
            return delta >= 0f ? 1f : -1f;
        }

        /// <summary>Point where the exit curve finishes and the perimeter edge begins.</summary>
        public static Vector3 GetExitCurveJoinPoint(PlayerSlotLayout slot, string barracksId, float ringRadius)
        {
            var tipAngle = GetSideExitAngle(slot, barracksId);
            var sign = GetExitTravelSign(slot, barracksId);
            var joinAngle = tipAngle + sign * (ExitCurveArcLength / Mathf.Max(1f, ringRadius));
            return new Vector3(Mathf.Cos(joinAngle), 0f, Mathf.Sin(joinAngle)) * ringRadius;
        }

        public static float GetExitCurveJoinAngle(PlayerSlotLayout slot, string barracksId, float ringRadius)
        {
            var join = GetExitCurveJoinPoint(slot, barracksId, ringRadius);
            return Mathf.Atan2(join.z, join.x);
        }

        /// <summary>Ring-tangent leave direction (circular perimeter between joins).</summary>
        public static Vector3 GetExitCurveOutDir(PlayerSlotLayout slot, string barracksId, float ringRadius)
        {
            var join = GetExitCurveJoinPoint(slot, barracksId, ringRadius);
            var sign = GetExitTravelSign(slot, barracksId);
            return sign > 0f ? RingTangentCcw(join) : -RingTangentCcw(join);
        }

        /// <summary>Leave direction along the straight edge to the neighbouring base join.</summary>
        public static Vector3 GetExitCurveOutDirAlongStraight(PlayerSlotLayout slot, string barracksId, MatchArenaLayout layout)
        {
            var radius = layout.ArenaRadius;
            var join = GetExitCurveJoinPoint(slot, barracksId, radius);
            var n = layout.PlayerCount;
            Vector3 other;
            if (barracksId == Game.Core.GameIds.Buildings.BarracksRight)
            {
                var next = layout.Slots[(slot.SlotIndex + 1) % n];
                other = GetExitCurveJoinPoint(next, Game.Core.GameIds.Buildings.BarracksLeft, radius);
            }
            else
            {
                var prev = layout.Slots[(slot.SlotIndex - 1 + n) % n];
                other = GetExitCurveJoinPoint(prev, Game.Core.GameIds.Buildings.BarracksRight, radius);
            }

            var dir = other - join;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return GetExitCurveOutDir(slot, barracksId, radius);
            }

            return dir.normalized;
        }

        /// <summary>
        /// Centerline from the straight exit tip onto the perimeter join
        /// (circular-tangent leave — used by N=5..8).
        /// </summary>
        public static void SampleExitCurve(
            PlayerSlotLayout slot,
            string barracksId,
            float ringRadius,
            List<Vector3> points,
            int samples = ExitCurveSamples)
        {
            SampleExitCurve(
                GetSideExitEnd(slot, barracksId),
                GetExitCurveJoinPoint(slot, barracksId, ringRadius),
                GetSideExitDir(slot, barracksId),
                GetExitCurveOutDir(slot, barracksId, ringRadius),
                points,
                samples);
        }

        /// <summary>Exit curve for N=3: leave direction follows the straight edge to the next base.</summary>
        public static void SampleExitCurve(
            PlayerSlotLayout slot,
            string barracksId,
            MatchArenaLayout layout,
            List<Vector3> points,
            int samples = ExitCurveSamples)
        {
            var radius = layout.ArenaRadius;
            SampleExitCurve(
                GetSideExitEnd(slot, barracksId),
                GetExitCurveJoinPoint(slot, barracksId, radius),
                GetSideExitDir(slot, barracksId),
                GetExitCurveOutDirAlongStraight(slot, barracksId, layout),
                points,
                samples);
        }

        static void SampleExitCurve(
            Vector3 tip,
            Vector3 join,
            Vector3 inDir,
            Vector3 outDir,
            List<Vector3> points,
            int samples)
        {
            var dist = Vector3.Distance(tip, join);
            var handle = Mathf.Max(ExitCurveRadius * 0.85f, dist * 0.4f);

            var p0 = tip;
            var p1 = tip + inDir * handle;
            var p2 = join - outDir * handle;
            var p3 = join;

            var count = Mathf.Max(4, samples);
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                points.Add(EvalCubicBezier(p0, p1, p2, p3, t));
            }
        }

        public static float BaseAngle(Vector3 basePosition) =>
            Mathf.Atan2(basePosition.z, basePosition.x);

        public static Vector3 SnapToRing(Vector3 point, float radius)
        {
            point.y = 0f;
            if (point.sqrMagnitude < 0.0001f)
            {
                return new Vector3(radius, 0f, 0f);
            }

            return point.normalized * radius;
        }

        public static Vector3 RingTangentCcw(Vector3 ringPoint)
        {
            ringPoint.y = 0f;
            if (ringPoint.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            var radial = ringPoint.normalized;
            return new Vector3(-radial.z, 0f, radial.x);
        }

        static Vector3 EvalCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u = 1f - t;
            var uu = u * u;
            var tt = t * t;
            return (uu * u * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (tt * t * p3);
        }
    }
}
