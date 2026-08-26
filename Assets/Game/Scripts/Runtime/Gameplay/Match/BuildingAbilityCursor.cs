using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Procedural circle cursor used while aiming a main building ability (MAIN-001).
    /// Blue circle inside the allowed cast range around the base, red outside it.
    /// </summary>
    public static class BuildingAbilityCursor
    {
        const int Size = 48;

        static Texture2D s_inRange;
        static Texture2D s_outOfRange;
        static Vector2 s_hotspot;

        public static void Apply(bool inRange)
        {
            Cursor.SetCursor(inRange ? EnsureInRange() : EnsureOutOfRange(), s_hotspot, CursorMode.Auto);
        }

        public static void Clear()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        static Texture2D EnsureInRange() => Build(ref s_inRange, new Color(0.35f, 0.75f, 1f, 1f));

        static Texture2D EnsureOutOfRange() => Build(ref s_outOfRange, new Color(0.95f, 0.18f, 0.18f, 1f));

        static Texture2D Build(ref Texture2D texture, Color color)
        {
            if (texture != null)
            {
                return texture;
            }

            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "BuildingAbilityCursor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[Size * Size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            const float radius = Size * 0.5f - 4f;
            const float thickness = 2f;
            var mid = Size / 2;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var dx = x - mid + 0.5f;
                    var dy = y - mid + 0.5f;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance <= radius + thickness && distance >= radius - thickness)
                    {
                        pixels[y * Size + x] = color;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            s_hotspot = new Vector2(mid, mid);
            return texture;
        }
    }
}
