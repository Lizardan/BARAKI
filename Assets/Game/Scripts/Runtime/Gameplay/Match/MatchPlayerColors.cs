using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Fixed player slot colors for units and map markers (MVP: 4 FFA slots).</summary>
    public static class MatchPlayerColors
    {
        static readonly Color[] s_slotColors =
        {
            new(0.92f, 0.18f, 0.18f), // red
            new(0.2f, 0.45f, 0.95f),   // blue
            new(0.22f, 0.82f, 0.28f),  // green
            new(0.95f, 0.82f, 0.12f),  // yellow
        };

        public static Color GetSlotColor(int slotIndex)
        {
            return s_slotColors[Mathf.Clamp(slotIndex, 0, s_slotColors.Length - 1)];
        }

        public static int PaletteSize => s_slotColors.Length;

        /// <summary>Returns the index of the palette slot closest to <paramref name="color"/>.</summary>
        public static int NearestSlotIndex(Color color)
        {
            var best = 0;
            var bestSq = float.MaxValue;
            for (var i = 0; i < s_slotColors.Length; i++)
            {
                var d = s_slotColors[i] - color;
                var sq = d.r * d.r + d.g * d.g + d.b * d.b;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }
            return best;
        }
    }
}
