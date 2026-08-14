using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(UnitAbilitySlot))]
    public sealed class UnitAbilitySlotDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = VisibleLineCount(property);
            return EditorGUIUtility.singleLineHeight * lines
                   + EditorGUIUtility.standardVerticalSpacing * (lines - 1)
                   + 8f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var y = position.y;
            var line = EditorGUIUtility.singleLineHeight;
            var gap = EditorGUIUtility.standardVerticalSpacing;

            var typeProp = property.FindPropertyRelative("_type");
            Draw(ref y, position, typeProp);
            Draw(ref y, position, property.FindPropertyRelative("_kind"));
            Draw(ref y, position, property.FindPropertyRelative("_unlock"));
            Draw(ref y, position, property.FindPropertyRelative("_unlockValue"));
            Draw(ref y, position, property.FindPropertyRelative("_displayName"));

            var description = property.FindPropertyRelative("_description");
            var descHeight = EditorGUI.GetPropertyHeight(description, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, descHeight), description, true);
            y += descHeight + gap;

            var type = (AbilityType)typeProp.intValue;
            if (Shows(type, "_damage"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_damage"));
            }

            if (Shows(type, "_heal"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_heal"));
            }

            if (Shows(type, "_healPerSecond"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_healPerSecond"));
            }

            if (Shows(type, "_radius"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_radius"));
            }

            if (Shows(type, "_castRange"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_castRange"));
            }

            if (Shows(type, "_cooldownSeconds"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_cooldownSeconds"));
            }

            if (Shows(type, "_durationSeconds"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_durationSeconds"));
            }

            if (Shows(type, "_percent"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_percent"));
            }

            if (Shows(type, "_manaCost"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_manaCost"));
            }

            if (Shows(type, "_stunSeconds"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_stunSeconds"));
            }

            if (Shows(type, "_flatBonus"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_flatBonus"));
            }

            if (Shows(type, "_secondaryRadius"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_secondaryRadius"));
            }

            if (Shows(type, "_secondaryHeal"))
            {
                Draw(ref y, position, property.FindPropertyRelative("_secondaryHeal"));
            }

            EditorGUI.EndProperty();
        }

        static void Draw(ref float y, Rect position, SerializedProperty property)
        {
            var height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), property);
            y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        static int VisibleLineCount(SerializedProperty property)
        {
            var type = (AbilityType)property.FindPropertyRelative("_type").intValue;
            var count = 6;
            var description = property.FindPropertyRelative("_description");
            count += Mathf.CeilToInt(EditorGUI.GetPropertyHeight(description, true) / EditorGUIUtility.singleLineHeight) - 1;
            if (Shows(type, "_damage")) count++;
            if (Shows(type, "_heal")) count++;
            if (Shows(type, "_healPerSecond")) count++;
            if (Shows(type, "_radius")) count++;
            if (Shows(type, "_castRange")) count++;
            if (Shows(type, "_cooldownSeconds")) count++;
            if (Shows(type, "_durationSeconds")) count++;
            if (Shows(type, "_percent")) count++;
            if (Shows(type, "_manaCost")) count++;
            if (Shows(type, "_stunSeconds")) count++;
            if (Shows(type, "_flatBonus")) count++;
            if (Shows(type, "_secondaryRadius")) count++;
            if (Shows(type, "_secondaryHeal")) count++;
            return count;
        }

        static bool Shows(AbilityType type, string field)
        {
            return field switch
            {
                "_damage" => type is AbilityType.Strike
                    or AbilityType.Ultimate
                    or AbilityType.Smite
                    or AbilityType.Consecration
                    or AbilityType.HolyNova
                    or AbilityType.Slam
                    or AbilityType.Stomp
                    or AbilityType.Frost,
                "_heal" => type is AbilityType.Heal
                    or AbilityType.HolyNova
                    or AbilityType.CasterHeal
                    or AbilityType.Revive,
                "_healPerSecond" => type == AbilityType.GreaterHeal,
                "_radius" => type is not (AbilityType.CasterHeal
                    or AbilityType.AuraDamagePercent
                    or AbilityType.AuraAttackSpeedPercent
                    or AbilityType.AuraArmorPercent
                    or AbilityType.AuraMaxHpPercent
                    or AbilityType.None),
                "_castRange" => type is AbilityType.HolyNova
                    or AbilityType.CasterHeal
                    or AbilityType.Frost
                    or AbilityType.Resurrect,
                "_cooldownSeconds" => type is not (AbilityType.AuraDamagePercent
                    or AbilityType.AuraAttackSpeedPercent
                    or AbilityType.AuraArmorPercent
                    or AbilityType.AuraMaxHpPercent
                    or AbilityType.None),
                "_durationSeconds" => type is AbilityType.Ultimate
                    or AbilityType.Shield
                    or AbilityType.Rally
                    or AbilityType.GreaterHeal
                    or AbilityType.Resurrect,
                "_percent" => type is AbilityType.Ultimate
                    or AbilityType.AuraDamagePercent
                    or AbilityType.AuraAttackSpeedPercent
                    or AbilityType.AuraArmorPercent
                    or AbilityType.AuraMaxHpPercent,
                "_manaCost" => type is AbilityType.CasterHeal or AbilityType.Frost or AbilityType.Resurrect,
                "_stunSeconds" => type is AbilityType.Consecration
                    or AbilityType.Stomp
                    or AbilityType.Frost,
                "_flatBonus" => type is AbilityType.Shield or AbilityType.Rally,
                "_secondaryRadius" or "_secondaryHeal" => type == AbilityType.Revive,
                _ => false,
            };
        }
    }
}
