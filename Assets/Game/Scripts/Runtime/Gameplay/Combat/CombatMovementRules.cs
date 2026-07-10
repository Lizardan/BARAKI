using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Steering and collision slide for world-space RTS movement (no NavMesh).</summary>
    public static class CombatMovementRules
    {
        public const float RepulsionRadiusMultiplier = 2f;
        public const float CombatRepulsionRadiusMultiplier = 3f;
        public const float ForwardBlockDistanceMultiplier = 1.5f;
        public const float BypassLateralFraction = 0.85f;
        public const float MarchFlowLateralFraction = 0.5f;
        public const int CollisionSlideMaxIterations = 4;
        public const int OverlapClampBinarySearchIterations = 8;

        public static Vector3 ComputeDesiredStep(Vector3 from, Vector3 to, float maxDistance)
        {
            var toTarget = to - from;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            return toTarget.normalized * maxDistance;
        }

        public static Vector3 ApplyRepulsion(
            Vector3 desiredStep,
            Vector3 myPosition,
            IReadOnlyList<Vector3> blockerPositions,
            float minSeparation,
            float repulsionRadiusMultiplier = RepulsionRadiusMultiplier)
        {
            if (blockerPositions == null || blockerPositions.Count == 0)
            {
                return desiredStep;
            }

            myPosition.y = 0f;
            var result = desiredStep;
            var maxMagnitude = desiredStep.magnitude;
            var repulsionRadius = minSeparation * repulsionRadiusMultiplier;

            foreach (var blocker in blockerPositions)
            {
                var blockerPosition = blocker;
                blockerPosition.y = 0f;
                var away = myPosition - blockerPosition;
                var distSq = away.sqrMagnitude;
                if (distSq < 0.0001f)
                {
                    continue;
                }

                var dist = Mathf.Sqrt(distSq);
                if (dist >= repulsionRadius)
                {
                    continue;
                }

                var weight = (repulsionRadius - dist) / repulsionRadius;
                result += away / dist * (maxMagnitude * weight);
            }

            if (maxMagnitude > 0.0001f && result.sqrMagnitude > maxMagnitude * maxMagnitude)
            {
                result = result.normalized * maxMagnitude;
            }

            return result;
        }

        public static Vector3 ComputeForwardBlockBypass(
            Vector3 myPosition,
            Vector3 targetPosition,
            IReadOnlyList<Vector3> blockerPositions,
            Vector3 forward,
            Vector3 right,
            float currentLateral,
            float maxStep)
        {
            if (blockerPositions == null || blockerPositions.Count == 0 || maxStep <= 0f)
            {
                return Vector3.zero;
            }

            myPosition.y = 0f;
            targetPosition.y = 0f;
            forward.y = 0f;
            right.y = 0f;

            var toTarget = targetPosition - myPosition;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            var toTargetDir = toTarget.normalized;
            var blockDistance = CombatFormationRules.MinUnitSeparation * ForwardBlockDistanceMultiplier;
            var blockDistanceSq = blockDistance * blockDistance;
            var blocked = false;

            foreach (var blocker in blockerPositions)
            {
                var blockerPosition = blocker;
                blockerPosition.y = 0f;
                var toBlocker = blockerPosition - myPosition;
                toBlocker.y = 0f;
                if (toBlocker.sqrMagnitude > blockDistanceSq)
                {
                    continue;
                }

                if (Vector3.Dot(toBlocker.normalized, toTargetDir) <= 0.2f)
                {
                    continue;
                }

                blocked = true;
                break;
            }

            if (!blocked)
            {
                return Vector3.zero;
            }

            var leftRoom = CombatFormationRules.MaxLateralOffset - currentLateral;
            var rightRoom = CombatFormationRules.MaxLateralOffset + currentLateral;
            var lateralSign = leftRoom >= rightRoom ? 1f : -1f;
            var lateralMagnitude = maxStep * BypassLateralFraction;
            return right * (lateralSign * lateralMagnitude);
        }

        public static float GetMarchSpreadSign(MatchUnitState unit, float currentLateral)
        {
            if (Mathf.Abs(currentLateral) > 0.05f)
            {
                return currentLateral > 0f ? 1f : -1f;
            }

            return unit.UnitId % 2 == 0 ? 1f : -1f;
        }

        public static Vector3 ComputeMarchFlowBypass(
            Vector3 right,
            float currentLateral,
            float maxStep,
            float spreadSign)
        {
            right.y = 0f;
            if (maxStep <= 0f)
            {
                return Vector3.zero;
            }

            return right * (spreadSign * maxStep * MarchFlowLateralFraction);
        }

        public static Vector3 ComputeMarchFlowBypass(
            Vector3 right,
            float currentLateral,
            float maxStep)
        {
            var leftRoom = CombatFormationRules.MaxLateralOffset - currentLateral;
            var rightRoom = CombatFormationRules.MaxLateralOffset + currentLateral;
            var lateralSign = leftRoom >= rightRoom ? 1f : -1f;
            return ComputeMarchFlowBypass(right, currentLateral, maxStep, lateralSign);
        }

        public static Vector3 ResolveCollisionSlide(
            Vector3 from,
            Vector3 step,
            float separationRadius,
            IReadOnlyList<Vector3> obstaclePositions)
        {
            from.y = 0f;
            step.y = 0f;
            if (step.sqrMagnitude < 0.000001f || obstaclePositions == null || obstaclePositions.Count == 0)
            {
                return step;
            }

            var maxMagnitude = step.magnitude;
            var result = step;
            var hadCollision = false;

            for (var iteration = 0; iteration < CollisionSlideMaxIterations; iteration++)
            {
                var changed = false;
                var tip = from + result;
                tip.y = 0f;

                foreach (var obstacle in obstaclePositions)
                {
                    var obstaclePosition = obstacle;
                    obstaclePosition.y = 0f;
                    var delta = tip - obstaclePosition;
                    var distSq = delta.sqrMagnitude;
                    if (distSq < 0.0001f)
                    {
                        delta = Vector3.right;
                        distSq = delta.sqrMagnitude;
                    }

                    var dist = Mathf.Sqrt(distSq);
                    if (dist >= separationRadius)
                    {
                        continue;
                    }

                    changed = true;
                    hadCollision = true;
                    var normal = delta / dist;
                    tip = obstaclePosition + normal * separationRadius;
                    result = tip - from;
                }

                if (!changed)
                {
                    break;
                }
            }

            if (!hadCollision && maxMagnitude > 0.0001f && result.sqrMagnitude > maxMagnitude * maxMagnitude)
            {
                result = result.normalized * maxMagnitude;
            }

            result.y = 0f;
            return result;
        }

        public static Vector3 ClampStepToAvoidOverlap(
            Vector3 from,
            Vector3 step,
            IReadOnlyList<Vector3> obstaclePositions,
            float minSeparation)
        {
            from.y = 0f;
            step.y = 0f;
            if (step.sqrMagnitude < 0.000001f || obstaclePositions == null || obstaclePositions.Count == 0)
            {
                return step;
            }

            if (!HasOverlap(from + step, obstaclePositions, minSeparation))
            {
                return step;
            }

            var low = 0f;
            var high = 1f;
            var best = Vector3.zero;
            var hasSafe = !HasOverlap(from, obstaclePositions, minSeparation);

            for (var i = 0; i < OverlapClampBinarySearchIterations; i++)
            {
                var mid = (low + high) * 0.5f;
                var candidate = step * mid;
                if (!HasOverlap(from + candidate, obstaclePositions, minSeparation))
                {
                    best = candidate;
                    hasSafe = true;
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            if (!hasSafe)
            {
                return Vector3.zero;
            }

            best.y = 0f;
            return best;
        }

        public static bool HasOverlap(
            Vector3 position,
            IReadOnlyList<Vector3> obstaclePositions,
            float minSeparation)
        {
            position.y = 0f;
            var minSeparationSq = minSeparation * minSeparation;

            foreach (var obstacle in obstaclePositions)
            {
                var obstaclePosition = obstacle;
                obstaclePosition.y = 0f;
                if ((position - obstaclePosition).sqrMagnitude < minSeparationSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static Vector3 ComputeOverlapEscapeStep(
            Vector3 position,
            Vector3 right,
            float maxStep,
            IReadOnlyList<Vector3> obstaclePositions,
            float minSeparation)
        {
            position.y = 0f;
            right.y = 0f;
            if (maxStep <= 0f || !HasOverlap(position, obstaclePositions, minSeparation))
            {
                return Vector3.zero;
            }

            foreach (var fraction in new[] { 1f, MarchFlowLateralFraction, 0.25f })
            {
                var escapeDistance = maxStep * fraction;
                if (escapeDistance <= 0.0001f)
                {
                    continue;
                }

                foreach (var sign in new[] { 1f, -1f })
                {
                    var candidate = position + right * (escapeDistance * sign);
                    if (!HasOverlap(candidate, obstaclePositions, minSeparation))
                    {
                        return right * (escapeDistance * sign);
                    }
                }
            }

            return Vector3.zero;
        }

        public static void DecomposeStep(
            Vector3 step,
            Vector3 forward,
            Vector3 right,
            out float forwardDelta,
            out float lateralDelta)
        {
            forwardDelta = Vector3.Dot(step, forward);
            lateralDelta = Vector3.Dot(step, right);
        }
    }
}
