using UnityEditor.Toolbars;

namespace Game.Editor
{
    /// <summary>Main-toolbar factory: Unity 6 requires <see cref="MainToolbarElementAttribute"/> on a static method.</summary>
    public static class AbilityFxStudioToolbarButton
    {
        [MainToolbarElement("BARAKI Studio", defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement CreateButton()
        {
            return new MainToolbarButton(
                new MainToolbarContent("BARAKI Studio", "Студия способностей и VFX"),
                AbilityFxStudioWindow.Open);
        }
    }
}
