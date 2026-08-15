using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Procedural red crosshair cursor used while aiming a main extra ability.</summary>
    public static class MainExtraAbilityCursor
    {
        const int Size = 32;
        static Texture2D s_texture;
        static Vector2 s_hotspot;

        public static void Apply()
        {
            EnsureTexture();
            Cursor.SetCursor(s_texture, s_hotspot, CursorMode.Auto);
        }

        public static void Clear()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        static void EnsureTexture()
        {
            if (s_texture != null)
            {
                return;
            }

            s_texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "MainExtraAbilityCrosshair",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var red = new Color(0.95f, 0.18f, 0.18f, 1f);
            var pixels = new Color[Size * Size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            const int thickness = 2;
            const int arm = 10;
            var mid = Size / 2;
            for (var i = -arm; i <= arm; i++)
            {
                for (var t = -thickness; t <= thickness; t++)
                {
                    SetPixel(pixels, mid + i, mid + i + t, red);
                    SetPixel(pixels, mid + i, mid - i + t, red);
                    SetPixel(pixels, mid + i + t, mid + i, red);
                    SetPixel(pixels, mid + i + t, mid - i, red);
                }
            }

            s_texture.SetPixels(pixels);
            s_texture.Apply(false, true);
            s_hotspot = new Vector2(mid, mid);
        }

        static void SetPixel(Color[] pixels, int x, int y, Color color)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size)
            {
                return;
            }

            pixels[y * Size + x] = color;
        }
    }
}
