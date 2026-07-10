using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>WC3-style movement: desired direction + local ally avoidance.</summary>
    public static class UnitLocomotionRules
    {
        public const float TargetScanInterval = 0.2f;
        public const float RouteLookahead = 3f;
        public const float AvoidanceStrength = 3.5f;
        public const float StoppingDistance = 0.2f;
        public const float MaxCombatDriftFromLane = 14f;
        public const float MaxMarchDriftFromLane = 4f;

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
            var targetDistance = Mathf.Min(distance + lookAhead, route.TotalLength);
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
            var blockedAhead = false;
            if (allies == null || allies.Count == 0)
            {
                return force;
            }

            var radiusSq = avoidanceRadius * avoidanceRadius;
            foreach (var ally in allies)
            {
                var allyPosition = ally.WorldPosition;
                allyPosition.y = 0f;
                var away = position - allyPosition;
                var distSq = away.sqrMagnitude;
                if (distSq < 0.000001f || distSq > radiusSq)
                {
                    continue;
                }

                var dist = Mathf.Sqrt(distSq);
                var lateral = away - desiredDirection * Vector3.Dot(away, desiredDirection);
                if (lateral.sqrMagnitude > 0.0001f)
                {
                    lateral.Normalize();
                    force += lateral * (1f / dist);
                }
                else
                {
                    blockedAhead = true;
                }
            }

            if (blockedAhead && force.sqrMagnitude < 0.0001f)
            {
                var right = Vector3.Cross(Vector3.up, desiredDirection).normalized;
                var spreadSign = spreadSeed % 2 == 0 ? 1f : -1f;
                force = right * spreadSign;
            }

            return force * avoidanceStrength;
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

            facingDirection = finalDirection;
            var step = finalDirection * maxStep;
            if (step.sqrMagnitude > toDestination.sqrMagnitude)
            {
                step = toDestination;
            }

            var result = position + step;
            result.y = position.y;
            return result;
        }

        public static Vector3 ClampToLaneDrift(LaneRoute route, Vector3 position, float maxDrift)
        {
            return ClampToLaneDrift(route, position, maxDrift, route?.ProjectDistance(position) ?? 0f);
        }

        public static Vector3 ClampToLaneDrift(
            LaneRoute route,
            Vector3 position,
            float maxDrift,
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
            if (drift <= maxDrift || drift <= 0.0001f)
            {
                position.y = route.EvaluateDistance(distance).y;
                return position;
            }

            var clamped = spine + offset / drift * maxDrift;
            clamped.y = route.EvaluateDistance(distance).y;
            return clamped;
        }
    }
}
