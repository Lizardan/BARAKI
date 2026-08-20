using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Mechanic card + 3/4-view schematic in BARAKI Studio settings.</summary>
    static class AbilityFxStudioMechanicUi
    {
        public const float SchematicWidth = 168f;
        public const float SchematicHeight = 192f;
        const float CaptionHeight = 24f;

        static readonly Color CasterColor = new(0.45f, 0.82f, 1f, 1f);
        static readonly Color TargetColor = new(1f, 0.55f, 0.36f, 1f);

        static GUIStyle s_chipTitle;
        static GUIStyle s_desc;
        static GUIStyle s_mapLabel;
        static GUIStyle s_caption;
        static Texture2D s_disc;
        static Texture2D s_ring;

        public static void DrawSchematic(AbilityFxMechanic mechanic) =>
            DrawSchematic(mechanic, SchematicHeight, SchematicWidth);

        public static void DrawSchematic(AbilityFxMechanic mechanic, float height, float width = 0f)
        {
            var rect = width > 0f
                ? GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height))
                : GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureTextures();
            EnsureStyles();
            var arena = new Rect(rect.x, rect.y, rect.width, rect.height - CaptionHeight);
            var caption = new Rect(rect.x, arena.yMax, rect.width, CaptionHeight);
            DrawArena(arena, mechanic);
            DrawCaption(caption, mechanic);
        }

        public static void DrawMechanicLine(AbilityFxMechanic mechanic)
        {
            EnsureStyles();
            var text = mechanic.ShowRing
                ? $"{mechanic.Title}  ·  {mechanic.RadiusLabel}"
                : $"{mechanic.Title}  ·  {mechanic.RadiusLabel}";
            var rect = GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(true), GUILayout.Height(26f));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, Color.Lerp(mechanic.ChipColor, new Color(0.12f, 0.12f, 0.14f), 0.72f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), mechanic.ChipColor);
            GUI.Label(
                new Rect(rect.x + 8f, rect.y, rect.width - 10f, rect.height),
                text,
                s_chipTitle);
        }

        public static void DrawDescription(string description)
        {
            EnsureStyles();
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            EditorGUILayout.LabelField(description, s_desc);
        }

        static void DrawArena(Rect rect, AbilityFxMechanic mechanic)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.11f, 1f));
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.y, rect.width, rect.height * 0.42f),
                new Color(0.11f, 0.12f, 0.15f, 1f));

            var ground = new Rect(
                rect.x + 7f,
                rect.y + rect.height * 0.48f,
                rect.width - 14f,
                rect.height * 0.32f);
            DrawSprite(ground, s_disc, new Color(0.16f, 0.17f, 0.21f, 1f));
            DrawSprite(
                new Rect(ground.x - 2f, ground.y + 3f, ground.width * 0.58f, ground.height - 4f),
                s_disc,
                new Color(CasterColor.r, CasterColor.g, CasterColor.b, 0.14f));
            DrawSprite(
                new Rect(ground.xMax - ground.width * 0.58f + 2f, ground.y + 3f, ground.width * 0.58f, ground.height - 4f),
                s_disc,
                new Color(TargetColor.r, TargetColor.g, TargetColor.b, 0.14f));
            DrawSprite(
                new Rect(ground.x + 8f, ground.y + 5f, ground.width - 16f, ground.height - 12f),
                s_disc,
                new Color(0.12f, 0.13f, 0.16f, 0.85f));

            var floorY = ground.y + ground.height * 0.42f;
            var k = Mathf.Clamp(rect.height / 118f, 0.85f, 1.45f);
            var caster = new Vector2(rect.x + rect.width * 0.30f, floorY);
            var target = new Vector2(rect.x + rect.width * 0.70f, floorY);
            var aoe = Color.Lerp(mechanic.ChipColor, Color.white, 0.08f);

            DrawAoe(mechanic, caster, target, floorY, aoe, k);
            if (mechanic.Shape == AbilityFxMechanicShape.BurstAtImpact)
            {
                DrawFlightTick(caster, target, aoe, k);
            }

            DrawPawn(caster, CasterColor, k, 1f);
            DrawPawn(target, TargetColor, k, -1f);
            DrawNamePill(new Vector2(caster.x, ground.yMax + 2f), "я", CasterColor);
            DrawNamePill(new Vector2(target.x, ground.yMax + 2f), "цель", TargetColor);
            DrawBorder(rect, new Color(1f, 1f, 1f, 0.12f));
        }

        static void DrawCaption(Rect rect, AbilityFxMechanic mechanic)
        {
            EditorGUI.DrawRect(rect, Color.Lerp(mechanic.ChipColor, new Color(0.10f, 0.10f, 0.12f), 0.78f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.08f));
            GUI.Label(
                new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height),
                mechanic.Motion,
                s_caption);
        }

        static void DrawPawn(Vector2 feet, Color color, float k, float face)
        {
            var r = 6.6f * k;
            var body = new Vector2(feet.x, feet.y - r * 1.15f);
            var head = new Vector2(feet.x, feet.y - r * 2.42f);
            DrawSprite(
                new Rect(feet.x - r * 1.55f, feet.y - r * 0.18f, r * 3.1f, r * 0.55f),
                s_disc,
                new Color(0f, 0f, 0f, 0.5f));
            DrawSprite(
                new Rect(body.x - r * 0.62f, body.y - r * 1.05f, r * 1.24f, r * 2.15f),
                s_disc,
                color);
            DrawSprite(
                new Rect(body.x - r * 0.38f, body.y - r * 0.55f, r * 0.76f, r * 0.9f),
                s_disc,
                Color.Lerp(color, Color.black, 0.22f));
            DrawSprite(DiscRect(head, r * 0.58f), s_disc, Color.Lerp(color, Color.white, 0.2f));
            DrawSprite(
                DiscRect(head + new Vector2(-r * 0.16f, -r * 0.18f), r * 0.18f),
                s_disc,
                new Color(1f, 1f, 1f, 0.4f));
            var eye = head + new Vector2(face * r * 0.22f, r * 0.04f);
            DrawSprite(DiscRect(eye, r * 0.11f), s_disc, new Color(0.08f, 0.08f, 0.1f, 0.85f));
        }

        static void DrawNamePill(Vector2 center, string text, Color tint)
        {
            var size = s_mapLabel.CalcSize(new GUIContent(text));
            var rect = new Rect(center.x - size.x * 0.5f - 6f, center.y, size.x + 12f, 16f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.58f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), tint);
            var prev = GUI.color;
            GUI.color = Color.Lerp(tint, Color.white, 0.28f);
            GUI.Label(rect, text, s_mapLabel);
            GUI.color = prev;
        }

        static void DrawAoe(
            AbilityFxMechanic mechanic,
            Vector2 caster,
            Vector2 target,
            float floorY,
            Color color,
            float k)
        {
            switch (mechanic.Shape)
            {
                case AbilityFxMechanicShape.AuraAroundSelf:
                    DrawGroundRing(new Vector2(caster.x, floorY), 30f * k, 11f * k, color, 0.28f);
                    break;
                case AbilityFxMechanicShape.AreaOnGround:
                {
                    var host = mechanic.RingHost == AbilityVfxAnchor.Target ? target : caster;
                    DrawGroundRing(new Vector2(host.x, floorY), 36f * k, 13f * k, color, 0.4f);
                    break;
                }
                case AbilityFxMechanicShape.BurstAroundSelf:
                    DrawGroundRing(new Vector2(caster.x, floorY), 28f * k, 10f * k, color, 0.48f);
                    break;
                case AbilityFxMechanicShape.BurstAroundTarget:
                    DrawGroundRing(new Vector2(target.x, floorY), 28f * k, 10f * k, color, 0.48f);
                    break;
                case AbilityFxMechanicShape.BurstAtImpact:
                    DrawGroundRing(new Vector2(target.x, floorY), 32f * k, 11f * k, color, 0.42f);
                    break;
                case AbilityFxMechanicShape.PointOnSelf:
                    DrawPoint(caster + new Vector2(0f, -16f * k), color, k);
                    break;
                default:
                    DrawPoint(target + new Vector2(0f, -16f * k), color, k);
                    break;
            }
        }

        static void DrawFlightTick(Vector2 caster, Vector2 target, Color color, float k)
        {
            var mid = Vector2.Lerp(caster, target, 0.48f) + new Vector2(0f, -18f * k);
            DrawSprite(DiscRect(mid, 3.2f * k), s_disc, color);
            DrawSprite(DiscRect(Vector2.Lerp(caster, mid, 0.55f) + new Vector2(0f, -8f * k), 2f * k), s_disc, color);
        }

        static void DrawGroundRing(Vector2 center, float rx, float ry, Color color, float fill)
        {
            var rect = new Rect(center.x - rx, center.y - ry, rx * 2f, ry * 2f);
            if (fill > 0.01f)
            {
                var fillColor = color;
                fillColor.a = fill;
                DrawSprite(rect, s_disc, fillColor);
            }

            DrawSprite(rect, s_ring, color);
        }

        static void DrawPoint(Vector2 center, Color color, float k)
        {
            DrawSprite(DiscRect(center, 9f * k), s_ring, color);
            DrawSprite(DiscRect(center, 3.6f * k), s_disc, color);
        }

        static Rect DiscRect(Vector2 center, float radius) =>
            new(center.x - radius, center.y - radius, radius * 2f, radius * 2f);

        static void DrawSprite(Rect rect, Texture2D texture, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        static void EnsureStyles()
        {
            s_chipTitle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            s_desc ??= new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                wordWrap = true,
            };
            s_mapLabel ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
            };
            s_caption ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) },
            };
        }

        static void EnsureTextures()
        {
            s_disc ??= MakeRadial(64, 0f, 1f);
            s_ring ??= MakeRadial(64, 0.78f, 1f);
        }

        static Texture2D MakeRadial(int size, float inner, float outer)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var cx = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (x - cx) / cx;
                    var ny = (y - cx) / cx;
                    var d = Mathf.Sqrt(nx * nx + ny * ny);
                    var alpha = Mathf.Clamp01(Mathf.InverseLerp(outer + 0.04f, outer, d))
                        * Mathf.Clamp01(Mathf.InverseLerp(inner - 0.04f, inner, d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply(false, true);
            return tex;
        }
    }
}
