using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Billboard HP and optional mana strips above a greybox unit (Dota-style).</summary>
    public sealed class UnitWorldStatusBars : MonoBehaviour
    {
        const float SizeMultiplier = 1.5f;
        const float BarWidth = 1.1f * SizeMultiplier;
        const float BarHeight = 0.12f * SizeMultiplier;
        const float BarDepth = 0.02f;
        const float BarSpacing = 0.05f * SizeMultiplier;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static Material s_unlitMaterial;
        static MaterialPropertyBlock s_colorBlock;

        Transform _healthFill;
        Transform _manaFill;
        float _lastHealthFill = -1f;
        float _lastManaFill = -1f;

        public static UnitWorldStatusBars Create(Transform parent, float heightAboveBody, bool showManaBar)
        {
            var root = new GameObject("StatusBars");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, heightAboveBody, 0f);

            var bars = root.AddComponent<UnitWorldStatusBars>();
            bars.BuildVisuals(showManaBar);
            return bars;
        }

        void BuildVisuals(bool showManaBar)
        {
            var healthY = showManaBar ? BarSpacing * 0.5f + BarHeight * 0.5f : 0f;
            _healthFill = CreateBar("HealthBar", healthY, new Color(0.25f, 0.9f, 0.3f), out _);

            if (showManaBar)
            {
                var manaY = -BarSpacing * 0.5f - BarHeight * 0.5f;
                _manaFill = CreateBar("ManaBar", manaY, new Color(0.25f, 0.45f, 0.95f), out _);
            }
        }

        Transform CreateBar(string barName, float localY, Color fillColor, out Transform fillTransform)
        {
            var barRoot = new GameObject(barName);
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = new Vector3(0f, localY, 0f);

            var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Background";
            background.transform.SetParent(barRoot.transform, false);
            background.transform.localScale = new Vector3(BarWidth, BarHeight, BarDepth);
            DestroyCollider(background);
            SetUnlitColor(background, new Color(0.06f, 0.06f, 0.06f, 0.95f));

            var fillObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillObject.name = "Fill";
            fillObject.transform.SetParent(barRoot.transform, false);
            fillObject.transform.localScale = new Vector3(BarWidth, BarHeight, BarDepth * 0.5f);
            fillObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            DestroyCollider(fillObject);

            var fillRenderer = fillObject.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                SetRendererColor(fillRenderer, fillColor);
            }

            fillTransform = fillObject.transform;
            return fillTransform;
        }

        public void SetHealth(float normalized)
        {
            SetBarFill(_healthFill, ref _lastHealthFill, normalized, true);
        }

        public void SetMana(float normalized)
        {
            if (_manaFill == null)
            {
                return;
            }

            SetBarFill(_manaFill, ref _lastManaFill, normalized, false);
        }

        void SetBarFill(Transform fill, ref float lastFill, float normalized, bool isHealth)
        {
            normalized = Mathf.Clamp01(normalized);
            if (Mathf.Approximately(normalized, lastFill) || fill == null)
            {
                return;
            }

            lastFill = normalized;
            fill.localScale = new Vector3(BarWidth * normalized, BarHeight, BarDepth * 0.5f);
            fill.localPosition = new Vector3((normalized - 1f) * BarWidth * 0.5f, 0f, -0.01f);

            var renderer = fill.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var color = isHealth
                ? Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.25f, 0.9f, 0.3f), normalized)
                : Color.Lerp(new Color(0.15f, 0.15f, 0.45f), new Color(0.25f, 0.45f, 0.95f), normalized);
            SetRendererColor(renderer, color);
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var toCamera = transform.position - camera.transform.position;
            if (toCamera.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        static void DestroyCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        static void SetUnlitColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                SetRendererColor(renderer, color);
            }
        }

        static void SetRendererColor(Renderer renderer, Color color)
        {
            var material = GetUnlitMaterial();
            if (material == null)
            {
                renderer.enabled = false;
                return;
            }

            renderer.sharedMaterial = material;
            s_colorBlock ??= new MaterialPropertyBlock();
            s_colorBlock.Clear();
            s_colorBlock.SetColor(BaseColorId, color);
            s_colorBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(s_colorBlock);
        }

        static Material GetUnlitMaterial()
        {
            if (s_unlitMaterial != null)
            {
                return s_unlitMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            s_unlitMaterial = new Material(shader)
            {
                name = "UnitWorldStatusBars",
            };
            return s_unlitMaterial;
        }
    }
}
