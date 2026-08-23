using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    /// <summary>
    /// Mode Select map thumbnails from the same <see cref="MatchMinimapTopology"/>
    /// the in-match minimap paints.
    /// </summary>
    public static class ModeMapThumbnailBuilder
    {
        public const float PreviewSize = ModeMapThumbnailElement.AuthoredSize;

        public static VisualElement BuildPreview(int playerCount) =>
            BuildPreview(playerCount, ModeMapThumbnailElement.AuthoredSize);

        public static VisualElement BuildPreview(int playerCount, float panelSize) =>
            BuildPreview(playerCount, panelSize, fillHost: false);

        public static VisualElement BuildFillPreview(int playerCount) =>
            BuildPreview(playerCount, ModeMapThumbnailElement.AuthoredSize, fillHost: true);

        public static VisualElement BuildPreview(int playerCount, float panelSize, bool fillHost)
        {
            var n = Mathf.Clamp(playerCount, MatchModeRules.MinPlayers, MatchModeRules.MaxPlayers);
            var layout = MatchArenaGenerator.Generate(n);
            var graph = LaneGraphBuilder.Build(layout);
            var topology = MatchMinimapTopologyBuilder.Build(layout, graph);
            var element = new ModeMapThumbnailElement(panelSize, fillHost);
            var home = GameplayCameraSettings.GetPlayerBaseFocusPosition(layout, 0);
            element.SetTopology(topology, layout.ArenaRadius, viewYawDegrees: 0f, home);
            if (fillHost)
            {
                element.ApplyCameraEdge(GameplayCameraPreferences.PreferredBaseScreenEdge);
            }

            return element;
        }

        public static Button BuildModeButton(int playerCount)
        {
            var button = new Button { name = $"Mode_N{playerCount}" };
            button.AddToClassList("mm-mode");
            button.Add(BuildPreview(playerCount));

            var label = new Label(MatchModeRules.GetModeTitle(playerCount));
            label.AddToClassList("mm-mode__label");
            button.Add(label);

            var selectable = MatchModeRules.IsModeSelectable(playerCount);
            button.SetEnabled(selectable);
            if (!selectable)
            {
                button.AddToClassList("mm-mode--disabled");
            }

            return button;
        }
    }
}
