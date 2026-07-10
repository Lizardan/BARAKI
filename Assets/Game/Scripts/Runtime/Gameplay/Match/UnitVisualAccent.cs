using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Shared name and runtime tint for the team-color accent mesh on greybox unit prefabs.</summary>
    public static class UnitVisualAccent
    {
        public const string TeamAccentTransformName = "TeamAccent";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void ApplyTeamColor(Transform visualRoot, Color slotColor)
        {
            var accent = visualRoot.Find(TeamAccentTransformName);
            if (accent == null)
            {
                accent = FindDeepChild(visualRoot, TeamAccentTransformName);
            }

            if (accent == null)
            {
                return;
            }

            var renderer = accent.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, slotColor);
            renderer.SetPropertyBlock(block);
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                var nested = FindDeepChild(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
