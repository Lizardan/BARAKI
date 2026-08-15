using Game.Gameplay.Data;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Combined read-only viewer for the unit prefab: balance stats card first, then the ability
    /// kit cards (only for units that have abilities). Editing lives on the definition / def assets —
    /// see <see cref="UnitInspectorDrawer"/>.
    /// </summary>
    [CustomEditor(typeof(UnitCombatSettings))]
    public sealed class UnitCombatSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (UnitCombatSettings)target;

            UnitInspectorDrawer.DrawBalanceCard(settings, settings.transform.root.gameObject);

            if (settings.Abilities.Length > 0)
            {
                UnitInspectorDrawer.DrawAbilityCards(settings);
            }
        }
    }
}
