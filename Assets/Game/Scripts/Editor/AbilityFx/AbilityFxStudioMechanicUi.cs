using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Mechanic card + top-down schematic in BARAKI Studio settings.</summary>
    static class AbilityFxStudioMechanicUi
    {
        public const float SchematicWidth = 128f;
        public const float SchematicHeight = 100f;

        static GUIStyle s_chipTitle;
        static GUIStyle s_chipMotion;
        static GUIStyle s_desc;
        static GUIStyle s_hint;
        static GUIStyle s_mapLabel;
        static Texture2D s_disc;
        static Texture2D s_ring;

        public static void DrawSchematic(AbilityFxMechanic mechanic)
        {
            var rect = GUILayoutUtility.GetRect(
                SchematicWidth,
                SchematicHeight,
                GUILayout.Width(SchematicWidth),
                GUILayout.Height(SchematicHeight));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureTextures();
            EditorGUI.DrawRect(rect, new Color(0.09f, 0.10f, 0.12f, 1f));
            DrawBorder(rect, new Color(1f, 1f, 1f, 0.08f));

            var floorY = rect.y + rect.height * 0.70f;
            EditorGUI.DrawRect(
                new Rect(rect.x + 10f, floorY, rect.width - 20f, 1f),
                new Color(1f, 1f, 1f, 0.16f));

            var caster = new Vector2(rect.x + rect.width * 0.32f, floorY - 16f);
            var target = new Vector2(rect.x + rect.width * 0.70f, floorY - 16f);
            var aoeColor = mechanic.ChipColor;

            DrawAoe(mechanic, caster, target, floorY, aoeColor);
            DrawUnit(caster, new Color(0.45f, 0.78f, 1f, 1f), 8f);
            DrawUnit(target, new Color(1f, 0.58f, 0.38f, 1f), 8f);

            s_mapLabel ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 9,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) },
            };
            GUI.Label(new Rect(caster.x - 16f, floorY + 4f, 32f, 16f), "я", s_mapLabel);
            GUI.Label(new Rect(target.x - 20f, floorY + 4f, 40f, 16f), "цель", s_mapLabel);
        }

        public static void DrawBody(string kitLine, string description, AbilityFxMechanic mechanic)
        {
            EnsureStyles();
            EditorGUILayout.BeginHorizontal();
            DrawChip(mechanic);
            GUILayout.Space(8f);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(kitLine, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(description, s_desc);
            }

            var prev = GUI.color;
            GUI.color = Color.Lerp(mechanic.ChipColor, Color.white, 0.35f);
            EditorGUILayout.LabelField(mechanic.FxHint, s_hint);
            GUI.color = prev;
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        static void DrawChip(AbilityFxMechanic mechanic)
        {
            var title = mechanic.Title ?? "";
            var motion = mechanic.Motion ?? "";
            var width = Mathf.Clamp(
                Mathf.Max(s_chipTitle.CalcSize(new GUIContent(title)).x,
                    s_chipMotion.CalcSize(new GUIContent(motion)).x) + 16f,
                148f,
                240f);
            var rect = GUILayoutUtility.GetRect(width, 32f, GUILayout.Width(width), GUILayout.Height(32f));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, Color.Lerp(mechanic.ChipColor, new Color(0.12f, 0.12f, 0.14f), 0.62f));
            DrawBorder(rect, mechanic.ChipColor * 0.85f);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 1f, rect.width - 12f, 16f), title, s_chipTitle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 15f, rect.width - 12f, 15f), motion, s_chipMotion);
        }

        static void DrawAoe(
            AbilityFxMechanic mechanic,
            Vector2 caster,
            Vector2 target,
            float floorY,
            Color color)
        {
            switch (mechanic.Shape)
            {
                case AbilityFxMechanicShape.AuraAroundSelf:
                    DrawRing(caster, 28f, color, fill: 0.16f);
                    DrawRing(caster, 22f, color, fill: 0f);
                    DrawFollowMarks(caster + new Vector2(22f, -10f), color);
                    break;
                case AbilityFxMechanicShape.AreaOnGround:
                {
                    var center = mechanic.RingHost == AbilityVfxAnchor.Target ? target : caster;
                    DrawGroundBlob(new Vector2(center.x, floorY - 4f), 34f, 12f, color);
                    break;
                }
                case AbilityFxMechanicShape.BurstAroundSelf:
                    DrawRing(caster, 26f, color, fill: 0.28f);
                    break;
                case AbilityFxMechanicShape.BurstAroundTarget:
                    DrawRing(target, 26f, color, fill: 0.28f);
                    break;
                case AbilityFxMechanicShape.BurstAtImpact:
                    DrawGroundBlob(new Vector2(target.x, floorY - 4f), 30f, 11f, color);
                    break;
                case AbilityFxMechanicShape.PointOnSelf:
                    DrawPoint(caster, color);
                    break;
                default:
                    DrawPoint(target, color);
                    break;
            }
        }

        static void DrawUnit(Vector2 center, Color color, float radius)
        {
            DrawSprite(DiscRect(center, radius + 1.5f), s_disc, new Color(0f, 0f, 0f, 0.45f));
            DrawSprite(DiscRect(center, radius), s_disc, color);
        }

        static void DrawRing(Vector2 center, float radius, Color color, float fill)
        {
            if (fill > 0.01f)
            {
                var fillColor = color;
                fillColor.a = fill;
                DrawSprite(DiscRect(center, radius), s_disc, fillColor);
            }

            DrawSprite(DiscRect(center, radius), s_ring, color);
        }

        static void DrawGroundBlob(Vector2 center, float width, float height, Color color)
        {
            var fill = color;
            fill.a = 0.32f;
            var rect = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            DrawSprite(rect, s_disc, fill);
            DrawSprite(rect, s_ring, color);
        }

        static void DrawPoint(Vector2 center, Color color)
        {
            DrawSprite(DiscRect(center, 5f), s_disc, color);
            DrawSprite(DiscRect(center, 11f), s_ring, color);
        }

        static void DrawFollowMarks(Vector2 origin, Color color)
        {
            for (var i = 0; i < 3; i++)
            {
                var x = origin.x + i * 6f;
                var rect = new Rect(x, origin.y, 4f, 2f);
                EditorGUI.DrawRect(rect, color);
            }
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
            s_chipTitle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            s_chipMotion ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(1f, 1f, 1f, 0.72f) },
            };
            s_desc ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 11,
                wordWrap = true,
            };
            s_hint ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                wordWrap = true,
                fontStyle = FontStyle.Italic,
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
