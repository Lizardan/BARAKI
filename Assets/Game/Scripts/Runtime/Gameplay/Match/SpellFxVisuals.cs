using Game.Core;
using Game.Gameplay.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Match
{
    /// <summary>Floating spell VFX: rises + fades out, then self-destructs.</summary>
    public sealed class SpellFxRiseFade : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Renderer[] _renderers;
        Color[] _baseColors;
        TextMesh _label;
        Color _labelColor;
        float _duration;
        float _riseSpeed;
        float _fadeOutStart;
        float _elapsed;
        bool _billboard;

        /// <summary>Static fade-out for cube/cylinder primitives (MaterialPropertyBlock colors).</summary>
        public void Configure(Renderer[] renderers, Color color, float duration, float riseSpeed, bool billboard)
        {
            _renderers = renderers;
            _baseColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                _baseColors[i] = color;
            }

            _duration = Mathf.Max(0.05f, duration);
            _riseSpeed = riseSpeed;
            _billboard = billboard;
            _fadeOutStart = _duration * 0.55f;
        }

        /// <summary>Fade-out for a legacy TextMesh label (vertex colors, not property blocks).</summary>
        public void ConfigureLabel(TextMesh label, Color color, float duration, float riseSpeed)
        {
            _label = label;
            _labelColor = color;
            _duration = Mathf.Max(0.05f, duration);
            _riseSpeed = riseSpeed;
            _billboard = true;
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
            if (!_billboard)
            {
                return;
            }

            var camera = CameraCache.Main;
            if (camera == null)
            {
                return;
            }

            // Only tip on X; yaw locked to gameplay camera compass (same as HP bars).
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
            if (_label != null)
            {
                var c = _labelColor;
                c.a = Mathf.Max(0f, c.a * alpha);
                _label.color = c;
                return;
            }

            if (_renderers == null)
            {
                return;
            }

            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var color = _baseColors[i];
                color.a = Mathf.Max(0f, color.a * alpha);
                block.SetColor(BaseColorId, color);
                block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }
    }

    /// <summary>Builds spell FX from Unity primitives + legacy TextMesh (URP Unlit materials).</summary>
    public static class SpellFxFactory
    {
        const float PlusArmLength = 0.55f;
        const float PlusArmThickness = 0.14f;
        const float RingSegmentSize = 0.55f;
        const float RingThickness = 0.07f;

        static Material s_transparentMaterial;
        static Material s_textMaterial;
        static Font s_font;
        static bool s_fontResolved;

        /// <summary>Green/yellow "+" above the target's HP bar, rises then fades.</summary>
        public static GameObject CreatePlus(
            Transform parent,
            Vector3 position,
            Color color,
            float duration = 1.1f,
            float riseSpeed = 0.9f)
        {
            var root = new GameObject("SpellPlus");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var armH = CreatePrimitiveCube(root.transform, "ArmH", new Vector3(PlusArmLength, PlusArmThickness, 0.04f), color);
            var armV = CreatePrimitiveCube(root.transform, "ArmV", new Vector3(PlusArmThickness, PlusArmLength, 0.04f), color);
            var component = root.AddComponent<SpellFxRiseFade>();
            component.Configure(
                new[] { armH.GetComponent<Renderer>(), armV.GetComponent<Renderer>() },
                color,
                duration,
                riseSpeed,
                billboard: true);
            return root;
        }

        /// <summary>Blue AoE disc + octagonal rim laid on the ground (Frost), fades without rising.</summary>
        public static GameObject CreateRing(
            Transform parent,
            Vector3 position,
            float radius,
            Color color,
            float duration = 0.9f)
        {
            var root = new GameObject("FrostRing");
            root.transform.SetParent(parent, false);
            root.transform.position = position + Vector3.up * 0.02f;

            var diskColor = color;
            diskColor.a *= 0.28f;
            var disk = CreatePrimitiveCylinder(root.transform, "Disk", radius * 2f, 0.08f, diskColor);

            var rimColor = color;
            rimColor.a *= 0.85f;
            const int segmentCount = 8;
            var renderers = new Renderer[segmentCount + 1];
            renderers[0] = disk.GetComponent<Renderer>();
            for (var i = 0; i < segmentCount; i++)
            {
                var angle = i * 360f / segmentCount * Mathf.Deg2Rad;
                var segment = CreatePrimitiveCube(
                    root.transform,
                    "Rim",
                    new Vector3(RingSegmentSize, RingThickness, RingSegmentSize),
                    rimColor);
                segment.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                segment.localRotation = Quaternion.Euler(0f, i * 360f / segmentCount, 0f);
                renderers[i + 1] = segment.GetComponent<Renderer>();
            }

            var component = root.AddComponent<SpellFxRiseFade>();
            component.Configure(renderers, rimColor, duration, riseSpeed: 0f, billboard: false);
            return root;
        }

        /// <summary>Spell name label above the caster's bars, rises then fades.</summary>
        public static GameObject CreateLabel(
            Transform parent,
            Vector3 position,
            string text,
            Color color,
            float duration = 1.4f,
            float riseSpeed = 0.7f)
        {
            var root = new GameObject("SpellName");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * 0.25f;

            var font = GetFont();
            var label = root.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 24;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            label.characterSize = 1f;
            if (font != null)
            {
                label.font = font;
            }

            var material = GetTextMaterial();
            if (material != null)
            {
                label.GetComponent<Renderer>().sharedMaterial = material;
            }

            var component = root.AddComponent<SpellFxRiseFade>();
            component.ConfigureLabel(label, color, duration, riseSpeed);
            return root;
        }

        static Transform CreatePrimitiveCube(Transform parent, string name, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            DestroyCollider(go);
            ApplyColor(go.GetComponent<Renderer>(), color);
            return go.transform;
        }

        static Transform CreatePrimitiveCylinder(Transform parent, string name, float diameter, float height, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(diameter, height, diameter);
            DestroyCollider(go);
            ApplyColor(go.GetComponent<Renderer>(), color);
            return go.transform;
        }

        static void ApplyColor(Renderer renderer, Color color)
        {
            var material = GetTransparentMaterial();
            if (material == null)
            {
                renderer.enabled = false;
                return;
            }

            renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_BaseColor"), color);
            block.SetColor(Shader.PropertyToID("_Color"), color);
            renderer.SetPropertyBlock(block);
        }

        static void DestroyCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        static Material GetTransparentMaterial()
        {
            if (s_transparentMaterial != null)
            {
                return s_transparentMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            s_transparentMaterial = CreateTransparentUnlit(shader, "SpellFxPrimitives");
            return s_transparentMaterial;
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
