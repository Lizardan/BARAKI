using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Small blue flame marker placed above a bonus/veteran unit's head. It is a temporary visual
    /// placeholder for races that have no dedicated bonus models yet (Faceless) — the unit keeps its
    /// regular model and this flame signals the enhanced variant. Purely cosmetic: it builds its own
    /// soft sprite texture, self-animates, and is destroyed together with its parent unit transform.
    /// </summary>
    public sealed class BonusFlameMarker : MonoBehaviour
    {
        [SerializeField] private float _coreScale = 0.42f;
        [SerializeField] private float _glowScale = 0.95f;
        [SerializeField] private Color _coreColor = new Color(0.80f, 0.96f, 1f, 1f);
        [SerializeField] private Color _glowColor = new Color(0.16f, 0.52f, 1f, 0.55f);
        [SerializeField] private float _flickerSpeed = 9f;
        [SerializeField] private float _flickerAmount = 0.14f;
        [SerializeField] private float _bobAmount = 0.05f;

        private SpriteRenderer _core;
        private SpriteRenderer _glow;
        private float _seed;

        private void Awake()
        {
            _seed = Random.Range(0f, 100f);

            _glow = CreateLayer(_glowColor, _glowScale, 0);
            _core = CreateLayer(_coreColor, _coreScale, 1);
        }

        private SpriteRenderer CreateLayer(Color color, float scale, int sortingOrder)
        {
            var layer = new GameObject("FlameLayer");
            layer.transform.SetParent(transform, false);
            layer.transform.localScale = Vector3.one * scale;

            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetFlameSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void Update()
        {
            var t = Time.time * _flickerSpeed + _seed;
            var flicker = 1f + Mathf.Sin(t) * _flickerAmount + Mathf.Sin(t * 2.3f) * _flickerAmount * 0.5f;

            if (_core != null)
            {
                _core.transform.localScale = Vector3.one * _coreScale * flicker;
            }

            if (_glow != null)
            {
                var pulse = 0.8f + 0.2f * Mathf.Sin(t * 1.7f);
                _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowColor.a * pulse);
                _glow.transform.localScale = Vector3.one * _glowScale * (1.6f - 0.6f * pulse);
            }

            transform.localPosition = new Vector3(0f, Mathf.Sin(t * 1.3f) * _bobAmount, 0f);
        }

        private static Texture2D _sharedTexture;
        private static Sprite _sharedSprite;

        private static Sprite GetFlameSprite()
        {
            if (_sharedSprite != null)
            {
                return _sharedSprite;
            }

            const int size = 64;
            _sharedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var center = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha; // soften the edge for a glow-like falloff
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            _sharedTexture.SetPixels32(pixels);
            _sharedTexture.Apply();
            _sharedSprite = Sprite.Create(
                _sharedTexture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                64f);
            return _sharedSprite;
        }
    }
}
