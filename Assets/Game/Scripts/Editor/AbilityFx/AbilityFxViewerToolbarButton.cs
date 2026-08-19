using UnityEditor.Toolbars;

namespace Game.Editor
{
    /// <summary>Main-toolbar factory: Unity 6 requires <see cref="MainToolbarElementAttribute"/> on a static method.</summary>
    public static class AbilityFxViewerToolbarButton
    {
        [MainToolbarElement("BARAKI/Ability FX", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateButton()
        {
            return new MainToolbarButton(
                new MainToolbarContent("Ability FX", "Вьювер способностей и VFX"),
                AbilityFxViewerWindow.Open);
        }
    }
}
