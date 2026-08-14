using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    /// <summary>
    /// Sutherland–Hodgman clip of the camera-frustum polygon to the minimap square
    /// (normalized 0..1). Keeps the overlay closed along the map edge instead of
    /// letting far corners run off-panel.
    /// </summary>
    public static class MatchMinimapViewportClip
    {
        public static bool TryClipToUnitSquare(
            Vector2 c0,
            Vector2 c1,
            Vector2 c2,
            Vector2 c3,
            List<Vector2> output,
            List<Vector2> scratch)
        {
            if (output == null || scratch == null)
            {
                return false;
            }

            output.Clear();
            output.Add(c0);
            output.Add(c1);
            output.Add(c2);
            output.Add(c3);

            ClipPlane(output, scratch, plane: 0);
            ClipPlane(scratch, output, plane: 1);
            ClipPlane(output, scratch, plane: 2);
            ClipPlane(scratch, output, plane: 3);

            return output.Count >= 3;
        }

        static void ClipPlane(List<Vector2> input, List<Vector2> output, int plane)
        {
            output.Clear();
            var count = input.Count;
            if (count == 0)
            {
                return;
            }

            var previous = input[count - 1];
            var previousInside = IsInside(previous, plane);
            for (var i = 0; i < count; i++)
            {
                var current = input[i];
                var currentInside = IsInside(current, plane);
                if (currentInside)
                {
                    if (!previousInside)
                    {
                        output.Add(Intersect(previous, current, plane));
                    }

                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(Intersect(previous, current, plane));
                }

                previous = current;
                previousInside = currentInside;
            }
        }

        static bool IsInside(Vector2 point, int plane) =>
            plane switch
            {
                0 => point.x >= 0f,
                1 => point.x <= 1f,
                2 => point.y >= 0f,
                _ => point.y <= 1f,
            };

        static Vector2 Intersect(Vector2 a, Vector2 b, int plane)
        {
            switch (plane)
            {
                case 0:
                    return new Vector2(0f, LerpComponent(a.y, b.y, a.x, b.x, 0f));
                case 1:
                    return new Vector2(1f, LerpComponent(a.y, b.y, a.x, b.x, 1f));
                case 2:
                    return new Vector2(LerpComponent(a.x, b.x, a.y, b.y, 0f), 0f);
                default:
                    return new Vector2(LerpComponent(a.x, b.x, a.y, b.y, 1f), 1f);
            }
        }

        static float LerpComponent(float aOut, float bOut, float aIn, float bIn, float edge)
        {
            var denom = bIn - aIn;
            if (Mathf.Abs(denom) < 0.000001f)
            {
                return aOut;
            }

            return aOut + (edge - aIn) / denom * (bOut - aOut);
        }
    }
}
