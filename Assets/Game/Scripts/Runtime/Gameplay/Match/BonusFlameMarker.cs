using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Small blue flame marker placed above a bonus/veteran unit's head. It is a temporary visual
    /// placeholder for races that have no dedicated bonus models yet (Faceless) — the unit keeps its
    /// regular model and this flame signals the enhanced variant. Purely cosmetic: it builds its own
    /// soft volumetric flame at runtime, self-animates, and is destroyed together with its parent
    /// unit transform.
    ///
    /// The flame is a 3D cross of soft radial planes rotated around the Y axis (several billboard
    /// cards), so it reads as a volume from any viewing angle instead of a single flat sprite.
    /// The marker preserves the baked head-height offset each frame (a bob is added on top) so it
    /// stays above the head rather than collapsing to the feet.
    /// </summary>
    public sealed class BonusFlameMarker : MonoBehaviour
    {
        [SerializeField] private int _planeCount = 3;
        [SerializeField] private float _coreScale = 0.42f;
        [SerializeField] private float _glowScale = 0.95f;
        [SerializeField] private Color _coreColor = new Color(0.80f, 0.96f, 1f, 1f);
        [SerializeField] private Color _glowColor = new Color(0.16f, 0.52f, 1f, 0.55f);
        [SerializeField] private float _flickerSpeed = 9f;
        [SerializeField] private float _flickerAmount = 0.14f;
        [SerializeField] private float _bobAmount = 0.05f;

        private readonly List<SpriteRenderer> _cores = new();
        private readonly List<SpriteRenderer> _glows = new();
        private float _seed;
        private Vector3 _baseLocalPos;

        private void Awake() => BuildFlame();

        /// <summary>
        /// Builds the flame planes. Idempotent: existing FlamePlane children are removed first, so it
        /// is safe to call repeatedly (runtime Awake and edit-mode portrait baking share this path).
        /// </summary>
        public void BuildFlame()
        {
            _cores.Clear();
            _glows.Clear();
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("FlamePlane_"))
                {
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            _seed = Random.Range(0f, 100f);
            _baseLocalPos = transform.localPosition;

            for (var i = 0; i < _planeCount; i++)
            {
                var holder = new GameObject($"FlamePlane_{i}");
                holder.transform.SetParent(transform, false);
                holder.transform.localRotation = Quaternion.Euler(0f, i * (360f / _planeCount), 0f);
                holder.transform.localPosition = Vector3.zero;

                _glows.Add(CreateLayer(holder.transform, _glowColor, _glowScale, 0));
                _cores.Add(CreateLayer(holder.transform, _coreColor, _coreScale, 1));
            }
        }

        private SpriteRenderer CreateLayer(Transform parent, Color color, float scale, int sortingOrder)
        {
            var layer = new GameObject("FlameLayer");
            layer.transform.SetParent(parent, false);
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

            for (var i = 0; i < _cores.Count; i++)
            {
                if (_cores[i] != null)
                {
                    _cores[i].transform.localScale = Vector3.one * _coreScale * flicker;
                }
            }

            for (var i = 0; i < _glows.Count; i++)
            {
                if (_glows[i] != null)
                {
                    var pulse = 0.8f + 0.2f * Mathf.Sin(t * 1.7f);
                    var glow = _glows[i];
                    glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowColor.a * pulse);
                    glow.transform.localScale = Vector3.one * _glowScale * (1.6f - 0.6f * pulse);
                }
            }

            // Preserve the baked head-height offset and add the bob on top (do NOT zero localPosition).
            transform.localPosition = new Vector3(
                _baseLocalPos.x,
                _baseLocalPos.y + Mathf.Sin(t * 1.3f) * _bobAmount,
                _baseLocalPos.z);
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
