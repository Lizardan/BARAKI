using Game.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Readable inspector for <see cref="UnitAbilityDef"/>. By default only non-zero tuning rows are
    /// shown (zero values are hidden to reduce clutter); "Показать все" reveals them.
    /// Tuning is regenerated from <c>AbilityKitDefaults</c> on Build Ability Defs.
    /// </summary>
    [CustomEditor(typeof(UnitAbilityDef))]
    public sealed class UnitAbilityDefEditor : UnityEditor.Editor
    {
        static readonly string[] NumberLabels =
        {
            "Damage", "Heal", "Heal/sec", "Radius", "Cast range", "Cooldown (s)", "Duration (s)",
            "Percent", "Mana cost", "Stun (s)", "Flat bonus", "Secondary radius", "Secondary heal",
        };

        SerializedProperty _id;
        SerializedProperty _name;
        SerializedProperty _description;
        SerializedProperty _kind;
        SerializedProperty _unlock;
        SerializedProperty _unlockValue;
        SerializedProperty _fx;
        SerializedProperty _behaviour;
        SerializedProperty[] _numbers;

        bool _showAll;

        void OnEnable()
        {
            _id = serializedObject.FindProperty("_abilityId");
            _name = serializedObject.FindProperty("_displayName");
            _description = serializedObject.FindProperty("_description");
            _kind = serializedObject.FindProperty("_kind");
            _unlock = serializedObject.FindProperty("_unlock");
            _unlockValue = serializedObject.FindProperty("_unlockValue");
            _fx = serializedObject.FindProperty("_fx");
            _behaviour = serializedObject.FindProperty("_behaviour");
            _numbers = new[]
            {
                serializedObject.FindProperty("_damage"),
                serializedObject.FindProperty("_heal"),
                serializedObject.FindProperty("_healPerSecond"),
                serializedObject.FindProperty("_radius"),
                serializedObject.FindProperty("_castRange"),
                serializedObject.FindProperty("_cooldownSeconds"),
                serializedObject.FindProperty("_durationSeconds"),
                serializedObject.FindProperty("_percent"),
                serializedObject.FindProperty("_manaCost"),
                serializedObject.FindProperty("_stunSeconds"),
                serializedObject.FindProperty("_flatBonus"),
                serializedObject.FindProperty("_secondaryRadius"),
                serializedObject.FindProperty("_secondaryHeal"),
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField($"Ability def — id {_id.intValue}", TitleStyle());
            EditorGUILayout.PropertyField(_name, new GUIContent("Name"));
            EditorGUILayout.LabelField("Description", FieldLabelStyle());
            _description.stringValue = EditorGUILayout.TextArea(_description.stringValue, TextAreaStyle(), GUILayout.MinHeight(48f));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_kind, new GUIContent("Kind"));
            EditorGUILayout.PropertyField(_unlock, new GUIContent("Unlock"));
            EditorGUILayout.PropertyField(_unlockValue, new GUIContent("Unlock value"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Параметры", EditorStyles.boldLabel);
            _showAll = EditorGUILayout.ToggleLeft("Показать все (включая нулевые)", _showAll);
            for (var i = 0; i < _numbers.Length; i++)
            {
                if (_showAll || !Mathf.Approximately(_numbers[i].floatValue, 0f))
                {
                    EditorGUILayout.PropertyField(_numbers[i], new GUIContent(NumberLabels[i]));
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(_fx, new GUIContent("FX"));
            EditorGUILayout.PropertyField(_behaviour, new GUIContent("Behaviour"), true);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Тюнинг пересобирается из AbilityKitDefaults (Build Ability Defs). " +
                "Цвет, префаб, якорь, клип, масштаб и поворот сохраняются. Визуал правится в BARAKI Studio.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        static GUIStyle TitleStyle() => new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };

        static GUIStyle FieldLabelStyle() => new GUIStyle(EditorStyles.label) { fontSize = 12 };

        static GUIStyle TextAreaStyle() => new GUIStyle(EditorStyles.textArea) { fontSize = 13, wordWrap = true };
    }
}
