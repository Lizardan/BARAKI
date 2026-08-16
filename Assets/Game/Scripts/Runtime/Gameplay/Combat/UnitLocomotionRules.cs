using System.Collections.Generic;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Movement: desired direction + local ally avoidance + walkable surface clamp.</summary>
    public static class UnitLocomotionRules
    {
        public const float TargetScanInterval = 0.2f;
        public const float RouteLookahead = 3f;
        public const float AvoidanceStrength = 3.5f;
        public const float StoppingDistance = 0.2f;

        /// <summary>
        /// Longitudinal braking gap: an ally straight ahead closer than this slows the unit
        /// (WC3-style flow). Lateral avoidance is never damped, so crowds slip around instead of jamming.
        /// </summary>
        public static float AllyBrakeGap => 0.75f * CombatFormationRules.MinUnitSeparation;

        /// <summary>Max yaw turn rate for sim FacingDirection (visual Slerp tracks this).</summary>
        public const float FacingTurnDegreesPerSecond = 450f;

        /// <summary>Physical road half-width (matches greybox ribbon).</summary>
        public static float RoadHalfWidth => MatchArenaGreyboxBuilder.RoadWidth * 0.5f;

        public static float AvoidanceRadius => CombatFormationRules.MinUnitSeparation;

        public static Vector3 GetRouteLookaheadDestination(LaneRoute route, Vector3 position, float maxStep)
        {
            return GetRouteLookaheadDestination(route, position, maxStep, route?.ProjectDistance(position) ?? 0f);
        }

        public static Vector3 GetRouteLookaheadDestination(
            LaneRoute route,
            Vector3 position,
            float maxStep,
            float progressDistance)
        {
            if (route == null)
            {
                return position;
            }

            position.y = 0f;
            var distance = route.ProjectDistanceForward(position, progressDistance);
            var lookAhead = Mathf.Max(RouteLookahead, maxStep * 1.25f);
            var targetDistance = route.IsClosedLoop
                ? distance + lookAhead
                : Mathf.Min(distance + lookAhead, route.TotalLength);
            var destination = route.EvaluateDistance(targetDistance);
            destination.y = position.y;
            return destination;
        }

        public static Vector3 ComputeAllyAvoidance(
            Vector3 position,
            Vector3 desiredDirection,
            IReadOnlyList<MatchUnitState> allies,
            float avoidanceRadius,
            float avoidanceStrength,
            int spreadSeed = 0)
        {
            position.y = 0f;
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                desiredDirection.Normalize();
            }
            else
            {
                desiredDirection = Vector3.forward;
            }

            var force = Vector3.zero;
            if (allies == null || allies.Count == 0)
            {
                return force;
            }

            var radiusSq = avoidanceRadius * avoidanceRadius;
            var right = Vector3.Cross(Vector3.up, desiredDirection).normalized;
            var spreadSign = spreadSeed % 2 == 0 ? 1f : -1f;
            var blockedAhead = false;

            foreach (var ally in allies)
            {
                var allyPosition = ally.WorldPosition;
                allyPosition.y = 0f;
                var away = position - allyPosition;
                var distSq = away.sqrMagnitude;
                if (distSq > radiusSq)
                {
                    continue;
                }

                var lateral = away - desiredDirection * Vector3.Dot(away, desiredDirection);
                if (lateral.sqrMagnitude > 0.0001f)
                {
                    lateral.Normalize();
                    if (distSq < 0.000001f)
                    {
                        // Exact overlap: push apart at full strength along the lateral axis.
                        force += lateral;
                    }
                    else
                    {
                        // Smooth falloff, no 1/dist spike: force peaks at contact and fades at the radius.
                        var dist = Mathf.Sqrt(distSq);
                        force += lateral * (1f - dist / avoidanceRadius);
                    }
                }
                else
                {
                    blockedAhead = true;
                    if (distSq < 0.000001f)
                    {
                        // Exact overlap straight ahead: deterministic side push by spread seed.
                        force += right * spreadSign;
                    }
                }
            }

            if (blockedAhead && force.sqrMagnitude < 0.0001f)
            {
                force = right * spreadSign;
            }

            return force * avoidanceStrength;
        }

        /// <summary>
        /// Longitudinal brake factor in [0..1] from the closest ally ahead on the movement axis.
        /// Allies behind, far off to the side or overlapped do not brake (they must slide around).
        /// </summary>
        static float ComputeAllyBrake(
            Vector3 position,
            Vector3 desiredDirection,
            IReadOnlyList<MatchUnitState> allies)
        {
            if (allies == null || allies.Count == 0)
            {
                return 1f;
            }

            var brakeGap = AllyBrakeGap;
            var closestForward = brakeGap;
            foreach (var ally in allies)
            {
                var allyPosition = ally.WorldPosition;
                allyPosition.y = 0f;
                var toOther = allyPosition - position;
                var distSq = toOther.sqrMagnitude;
                if (distSq < 0.0001f || distSq >= brakeGap * brakeGap)
                {
                    continue;
                }

                var dForward = Vector3.Dot(toOther, desiredDirection);
                if (dForward <= 0f || dForward >= closestForward)
                {
                    continue;
                }

                if (dForward / Mathf.Sqrt(distSq) < 0.5f)
                {
                    continue;
                }

                closestForward = dForward;
            }

            if (closestForward >= brakeGap)
            {
                return 1f;
            }

            return Mathf.Clamp01(closestForward / brakeGap);
        }

        public static Vector3 MoveTowards(
            Vector3 position,
            Vector3 destination,
            float maxStep,
            IReadOnlyList<MatchUnitState> allies,
            out Vector3 facingDirection,
            int spreadSeed = 0)
        {
            position.y = 0f;
            destination.y = 0f;
            facingDirection = Vector3.forward;

            if (maxStep <= 0.0001f)
            {
                return position;
            }

            var toDestination = destination - position;
            if (toDestination.sqrMagnitude <= StoppingDistance * StoppingDistance)
            {
                return position;
            }

            var desired = toDestination.normalized;
            var avoidance = ComputeAllyAvoidance(
                position,
                desired,
                allies,
                AvoidanceRadius,
                AvoidanceStrength,
                spreadSeed);
            var finalDirection = desired + avoidance;
            if (finalDirection.sqrMagnitude < 0.0001f)
            {
                finalDirection = desired;
            }
            else
            {
                finalDirection.Normalize();
            }

            // Brake only the longitudinal component (WC3-style follow-with-gap); the lateral
            // component stays full so units still slip around crowds instead of jamming up.
            var brake = ComputeAllyBrake(position, desired, allies);
            var forwardComponent = Vector3.Dot(finalDirection, desired);
            var lateralComponent = finalDirection - desired * forwardComponent;
            var stepDirection = desired * (forwardComponent * brake) + lateralComponent;
            if (stepDirection.sqrMagnitude < 0.0001f)
            {
                stepDirection = finalDirection;
            }
            else
            {
                stepDirection.Normalize();
            }

            var step = stepDirection * maxStep;
            if (step.sqrMagnitude > toDestination.sqrMagnitude)
            {
                step = toDestination;
            }

            facingDirection = step.sqrMagnitude > 0.0001f
                ? step.normalized
                : desired;

            var result = position + step;
            result.y = position.y;
            return result;
        }

        /// <summary>Facing from actual world displacement (horizontal). False when barely moved.</summary>
        public static bool TryGetFacingFromDisplacement(Vector3 from, Vector3 to, out Vector3 facing)
        {
            var delta = to - from;
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                facing = default;
                return false;
            }

            facing = delta.normalized;
            return true;
        }

        /// <summary>
        /// Yaw-only step toward desired facing. Never snaps; capped by
        /// <see cref="FacingTurnDegreesPerSecond"/> (or override).
        /// </summary>
        public static Vector3 StepFacingTowards(
            Vector3 currentFacing,
            Vector3 desiredFacing,
            float deltaTime,
            float maxDegreesPerSecond = FacingTurnDegreesPerSecond)
        {
            desiredFacing.y = 0f;
            if (desiredFacing.sqrMagnitude <= 0.0001f || deltaTime <= 0f || maxDegreesPerSecond <= 0f)
            {
                currentFacing.y = 0f;
                return currentFacing.sqrMagnitude > 0.0001f
                    ? currentFacing.normalized
                    : Vector3.forward;
            }

            desiredFacing.Normalize();
            currentFacing.y = 0f;
            if (currentFacing.sqrMagnitude <= 0.0001f)
            {
                return desiredFacing;
            }

            currentFacing.Normalize();
            var from = Quaternion.LookRotation(currentFacing, Vector3.up);
            var to = Quaternion.LookRotation(desiredFacing, Vector3.up);
            var stepped = Quaternion.RotateTowards(from, to, maxDegreesPerSecond * deltaTime);
            var result = stepped * Vector3.forward;
            result.y = 0f;
            return result.sqrMagnitude > 0.0001f ? result.normalized : desiredFacing;
        }

        public static bool IsInsideCenterArena(Vector3 position, float centerArenaRadius)
        {
            if (centerArenaRadius <= 0f)
            {
                return false;
            }

            position.y = 0f;
            return position.sqrMagnitude <= centerArenaRadius * centerArenaRadius;
        }

        /// <summary>
        /// Keep units on the road ribbon or inside the center arena. Legacy fallback when no SourceParts surface.
        /// </summary>
        public static Vector3 ClampToWalkable(
            LaneRoute route,
            Vector3 position,
            float progressDistance,
            float centerArenaRadius)
        {
            if (IsInsideCenterArena(position, centerArenaRadius))
            {
                return position;
            }

            return ClampToRoadCorridor(route, position, progressDistance);
        }

        public static Vector3 ClampToRoadCorridor(
            LaneRoute route,
            Vector3 position,
            float progressDistance)
        {
            if (route == null)
            {
                return position;
            }

            var distance = route.ProjectDistanceForward(position, progressDistance);
            var spine = route.EvaluateDistance(distance);
            spine.y = 0f;
            position.y = 0f;
            var offset = position - spine;
            var drift = offset.magnitude;
            var maxDrift = RoadHalfWidth;
            if (drift <= maxDrift || drift <= 0.0001f)
            {
                position.y = route.EvaluateDistance(distance).y;
                return position;
            }

            var clamped = spine + offset / drift * maxDrift;
            clamped.y = route.EvaluateDistance(distance).y;
            return clamped;
        }

        /// <summary>Caps world displacement so units never snap farther than one move step.</summary>
        public static Vector3 LimitDisplacement(Vector3 from, Vector3 to, float maxStep)
        {
            from.y = 0f;
            var targetY = to.y;
            to.y = 0f;
            if (maxStep <= 0.0001f)
            {
                from.y = targetY;
                return from;
            }

            var delta = to - from;
            var dist = delta.magnitude;
            if (dist <= maxStep || dist <= 0.0001f)
            {
                to.y = targetY;
                return to;
            }

            var limited = from + delta * (maxStep / dist);
            limited.y = targetY;
            return limited;
        }

        /// <summary>
        /// Walkable clamp, then step-limit from the previous position (run back onto surface, never teleport).
        /// When <paramref name="surface"/> is set, clamp uses SourceParts area only (no lane corridor).
        /// </summary>
        public static Vector3 ApplyWalkableLimit(
            LaneRoute route,
            Vector3 previousPosition,
            Vector3 proposedPosition,
            float maxStep,
            float progressDistance,
            float centerArenaRadius,
            WalkableSurface surface = null)
        {
            var clamped = surface != null
                ? surface.Clamp(proposedPosition)
                : ClampToWalkable(
                    route,
                    proposedPosition,
                    progressDistance,
                    centerArenaRadius);
            return LimitDisplacement(previousPosition, clamped, maxStep);
        }
    }
}
