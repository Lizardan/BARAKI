using Game.Core;
using Game.Gameplay.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Match
{
    /// <summary>Floating spell label VFX: rises + fades out, then self-destructs.</summary>
    public sealed class SpellFxRiseFade : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        TextMesh[] _labels;
        Color[] _labelColors;
        float _duration;
        float _riseSpeed;
        float _fadeOutStart;
        float _elapsed;

        /// <summary>Fade-out for TextMesh label stack (fill + outline), vertex colors.</summary>
        public void ConfigureLabel(TextMesh[] labels, Color[] colors, float duration, float riseSpeed)
        {
            _labels = labels;
            _labelColors = colors;
            _duration = Mathf.Max(0.05f, duration);
            _riseSpeed = riseSpeed;
            _fadeOutStart = _duration * 0.4f;
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            if (_riseSpeed > 0f)
            {
                transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);
            }

            var alpha = 1f;
            if (_elapsed >= _fadeOutStart)
            {
                alpha = 1f - Mathf.InverseLerp(_fadeOutStart, _duration, _elapsed);
            }

            ApplyAlpha(alpha);
            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }

        void LateUpdate()
        {
            var camera = CameraCache.Main;
            if (camera == null)
            {
                return;
            }

            var yaw = GameplayCameraPanController.Current != null
                ? GameplayCameraPanController.Current.YawDegrees
                : 0f;
            transform.rotation = UnitWorldStatusBars.ResolvePitchOnlyBillboard(
                transform.position,
                camera.transform.position,
                yaw);
        }

        void ApplyAlpha(float alpha)
        {
            if (_labels == null || _labelColors == null)
            {
                return;
            }

            var count = Mathf.Min(_labels.Length, _labelColors.Length);
            for (var i = 0; i < count; i++)
            {
                var label = _labels[i];
                if (label == null)
                {
                    continue;
                }

                var c = _labelColors[i];
                c.a = Mathf.Max(0f, c.a * alpha);
                label.color = c;
            }
        }
    }

    /// <summary>Builds spell name labels from legacy TextMesh (URP Unlit materials).</summary>
    public static class SpellFxFactory
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        const float LabelWorldScale = 0.38f;
        const int LabelFontSize = 30;
        const float LabelOutlineOffset = 0.055f;
        const float LabelFillLocalZ = -0.01f;
        const float LabelOutlineLocalZ = 0.01f;

        static readonly Vector2[] LabelOutlineOffsets =
        {
            new(-1f, 0f),
            new(1f, 0f),
            new(0f, -1f),
            new(0f, 1f),
            new(-1f, -1f),
            new(-1f, 1f),
            new(1f, -1f),
            new(1f, 1f),
        };

        static Material s_textMaterial;
        static Font s_font;
        static bool s_fontResolved;

        /// <summary>Spell name label above the caster's bars, rises then fades.</summary>
        public static GameObject CreateLabel(
            Transform parent,
            Vector3 position,
            string text,
            Color color,
            float duration = 1.4f,
            float riseSpeed = 0.45f)
        {
            var root = new GameObject("SpellName");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * LabelWorldScale;

            var fillColor = MakeReadableFill(color);
            var outlineColor = new Color(0f, 0f, 0f, 0.95f);
            var labels = new TextMesh[LabelOutlineOffsets.Length + 1];
            var colors = new Color[labels.Length];

            for (var i = 0; i < LabelOutlineOffsets.Length; i++)
            {
                var offset = LabelOutlineOffsets[i] * LabelOutlineOffset;
                labels[i] = CreateTextMesh(
                    root.transform,
                    "Outline",
                    text,
                    outlineColor,
                    new Vector3(offset.x, offset.y, LabelOutlineLocalZ));
                colors[i] = outlineColor;
            }

            labels[^1] = CreateTextMesh(
                root.transform,
                "Fill",
                text,
                fillColor,
                new Vector3(0f, 0f, LabelFillLocalZ));
            colors[^1] = fillColor;

            var component = root.AddComponent<SpellFxRiseFade>();
            component.ConfigureLabel(labels, colors, duration, riseSpeed);
            return root;
        }

        static TextMesh CreateTextMesh(
            Transform parent,
            string objectName,
            string text,
            Color color,
            Vector3 localPosition)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var font = GetFont();
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = LabelFontSize;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            label.characterSize = 1f;
            if (font != null)
            {
                label.font = font;
            }

            var renderer = label.GetComponent<Renderer>();
            if (font != null && font.material != null)
            {
                renderer.sharedMaterial = font.material;
            }
            else
            {
                var material = GetTextMaterial();
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, color);
                    block.SetColor(ColorId, color);
                    renderer.SetPropertyBlock(block);
                }
            }

            return label;
        }

        static Color MakeReadableFill(Color color)
        {
            var fill = Color.Lerp(color, Color.white, 0.35f);
            fill.a = 1f;
            return fill;
        }

        static Material GetTextMaterial()
        {
            if (s_textMaterial != null)
            {
                return s_textMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            s_textMaterial = CreateTransparentUnlit(shader, "SpellFxLabel");
            RefreshFontTexture(GetFont());
            return s_textMaterial;
        }

        static Material CreateTransparentUnlit(Shader shader, string materialName)
        {
            var material = new Material(shader)
            {
                name = materialName,
            };
            material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        static Font GetFont()
        {
            if (s_font != null || s_fontResolved)
            {
                return s_font;
            }

            s_fontResolved = true;
            try
            {
                s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                s_font = null;
            }

            if (s_font == null)
            {
                try
                {
                    s_font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch
                {
                    s_font = null;
                }
            }

            if (s_font == null)
            {
                s_font = Font.CreateDynamicFontFromOSFont("Arial", 48);
            }

            if (s_font != null)
            {
                RefreshFontTexture(s_font);
            }

            return s_font;
        }

        static void RefreshFontTexture(Font font)
        {
            if (font == null || s_textMaterial == null)
            {
                return;
            }

            var texture = font.material != null ? font.material.mainTexture : null;
            if (texture != null)
            {
                s_textMaterial.SetTexture("_BaseMap", texture);
            }
        }
    }
}
