using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Detailed read-only summary of the kit's shared def references: per-ability description,
    /// tuning numbers, unlock rule, behaviour and FX hint. Order in the list = AI cast priority.
    /// </summary>
    [CustomEditor(typeof(UnitAbilityKit))]
    public sealed class UnitAbilityKitEditor : UnityEditor.Editor
    {
        static readonly Color ActiveColor = new(0.35f, 0.8f, 0.45f);
        static readonly Color PassiveColor = new(0.55f, 0.65f, 1f);
        static readonly Color MissingColor = new(1f, 0.5f, 0.3f);

        public override void OnInspectorGUI()
        {
            var kit = (UnitAbilityKit)target;
            if (kit.Abilities.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No abilities assigned. Run BARAKI/Abilities/Build Ability Defs, " +
                    "then BARAKI/Units/Seed Ability Kits.",
                    MessageType.Info);
                base.OnInspectorGUI();
                return;
            }

            EditorGUILayout.LabelField($"Ability kit — {kit.Abilities.Length} шт.", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Порядок списка = приоритет каста AI (слева кастуется первым).", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            for (var i = 0; i < kit.Abilities.Length; i++)
            {
                var def = kit.Abilities[i];
                if (def == null)
                {
                    EditorGUILayout.HelpBox(
                        $"{i}. <null> — ссылка потеряна. Перезапусти BARAKI/Units/Seed Ability Kits.",
                        MessageType.Warning);
                    continue;
                }

                DrawAbility(def, i);
            }
        }

        static void DrawAbility(UnitAbilityDef def, int index)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            DrawColorSwatch(def.Fx.Color, 14f);
            var kindColor = def.IsPassiveAura ? PassiveColor : ActiveColor;
            EditorGUILayout.LabelField($"{index}. {def.DisplayName}", Bold(kindColor));
            EditorGUILayout.LabelField(
                def.IsActive ? "Активная" : "Пассивная",
                Mini(def.IsActive ? ActiveColor : new Color(0.65f, 0.65f, 0.65f)),
                GUILayout.Width(78f));

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(def.Description))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(def.Description, MessageType.None);
            }

            var paramsText = BuildParamsText(def);
            if (!string.IsNullOrEmpty(paramsText))
            {
                EditorGUILayout.LabelField(paramsText, EditorStyles.miniLabel);
            }

            if (def.IsPassiveAura && def.Behaviour is AuraBehaviour aura)
            {
                EditorGUILayout.LabelField($"Аура армии: {StatName(aura.Stat)} +{FormatPercent(def.Percent)}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(2);
            var meta = $"id {def.AbilityId}  ·  {UnlockText(def)}";
            if (def.Behaviour != null)
            {
                meta += $"  ·  {def.Behaviour.GetType().Name}";
            }

            meta += $"  ·  FX: {def.Fx.Kind}";
            EditorGUILayout.LabelField(meta, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        static string BuildParamsText(UnitAbilityDef def)
        {
            var parts = new List<string>();
            Add("Урон", def.Damage);
            Add("Лечение", def.Heal);
            Add("Лечение/с", def.HealPerSecond);
            Add("Радиус", def.Radius);
            Add("Дальность", def.CastRange);
            Add("КД", def.CooldownSeconds);
            Add("Длит.", def.DurationSeconds);
            Add("%", def.Percent);
            Add("Мана", def.ManaCost);
            Add("Оглушение", def.StunSeconds);
            Add("Бонус", def.FlatBonus);
            Add("Радиус²", def.SecondaryRadius);
            Add("Лечение²", def.SecondaryHeal);
            return parts.Count > 0 ? "Параметры: " + string.Join("   ·   ", parts) : string.Empty;

            void Add(string label, float value)
            {
                if (value > 0f)
                {
                    parts.Add($"{label} {FormatNumber(value)}");
                }
            }
        }

        static string UnlockText(UnitAbilityDef def) => def.Unlock switch
        {
            AbilityUnlock.Always => "всегда",
            AbilityUnlock.HeroLevel => $"уровень героя {def.UnlockValue}+",
            AbilityUnlock.MagicLevel => $"магия {def.UnlockValue}+",
            _ => "?",
        };

        static string StatName(AuraStat stat) => stat switch
        {
            AuraStat.Damage => "урон",
            AuraStat.AttackSpeed => "скорость атаки",
            AuraStat.Armor => "броня",
            AuraStat.MaxHp => "запас здоровья",
            _ => "?",
        };

        static string FormatNumber(float value) =>
            Mathf.Abs(value % 1f) < 0.001f ? value.ToString("0") : value.ToString("0.#");

        static string FormatPercent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        static GUIStyle Bold(Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            style.normal.textColor = color;
            return style;
        }

        static GUIStyle Mini(Color color)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = color;
            return style;
        }

        static void DrawColorSwatch(Color color, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (color.a <= 0f)
            {
                color = new Color(0.35f, 0.35f, 0.35f);
            }

            EditorGUI.DrawRect(rect, color);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        }
    }
}
