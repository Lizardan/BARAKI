using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Mdx
{
    /// <summary>
    /// Editor-preview helper for the MdxPreview scene. On Start it finds every <see cref="MdxInstance"/>
    /// in the scene, loops all sequences and auto-plays a "Stand" sequence, then builds a small uGUI
    /// toolbar (Canvas is created at runtime, so nothing needs to be wired up in the scene).
    /// Buttons are common to all models: Stand, Attack, Walk, Death, Team color.
    /// </summary>
    public sealed class MdxPreviewUi : MonoBehaviour
    {
        static readonly int[] TeamColorCycle = { 0, 6, 1 };
        static readonly string[] TeamColorNames = { "Красный", "Зелёный", "Синий" };

        readonly List<MdxInstance> _instances = new();
        int _colorIndex;

        Text _colorLabel;

        void Start()
        {
            foreach (var inst in FindObjectsByType<MdxInstance>())
            {
                _instances.Add(inst);
            }

            foreach (var inst in _instances)
            {
                inst.SequenceLoopMode = 2;
                var stand = FindSequence(inst, "Stand");
                if (stand >= 0)
                {
                    inst.SetSequence(stand);
                }
            }

            BuildUi();
        }

        public void PlayStand()
        {
            PlayAll("Stand", "Portrait");
        }

        public void PlayAttack()
        {
            PlayAll("Attack");
        }

        public void PlayWalk()
        {
            PlayAll("Walk", "Run");
        }

        public void PlayDeath()
        {
            PlayAll("Death");
        }

        public void CycleTeamColor()
        {
            _colorIndex = (_colorIndex + 1) % TeamColorCycle.Length;

            foreach (var inst in _instances)
            {
                inst.TeamColor = TeamColorCycle[_colorIndex];
            }

            if (_colorLabel != null)
            {
                _colorLabel.text = "Цвет: " + TeamColorNames[_colorIndex];
            }
        }

        void PlayAll(params string[] keywords)
        {
            foreach (var inst in _instances)
            {
                var index = FindSequence(inst, keywords);
                if (index >= 0)
                {
                    inst.SetSequence(index);
                }
            }
        }

        static int FindSequence(MdxInstance inst, params string[] keywords)
        {
            for (var i = 0; i < inst.SequenceCount; i++)
            {
                var name = inst.GetSequenceName(i);
                foreach (var keyword in keywords)
                {
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("PreviewUiCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            var panel = new GameObject("Toolbar", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            panel.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(panel.transform, "Стоит", PlayStand);
            CreateButton(panel.transform, "Атака", PlayAttack);
            CreateButton(panel.transform, "Идёт", PlayWalk);
            CreateButton(panel.transform, "Смерть", PlayDeath);
            _colorLabel = CreateButton(panel.transform, "Цвет: " + TeamColorNames[_colorIndex], CycleTeamColor);
        }

        static Text CreateButton(Transform parent, string content, UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = 160;
            layout.preferredHeight = 48;

            var image = go.GetComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = label.GetComponent<Text>();
            text.text = content;
            text.font = FontAsset();
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(onClick);
            return text;
        }

        static Sprite _whiteSprite;

        static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.hideFlags = HideFlags.DontSave;
                _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
                _whiteSprite.hideFlags = HideFlags.DontSave;
            }

            return _whiteSprite;
        }

        static Font _font;

        static Font FontAsset()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }
    }
}
