using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Runtime tint for all team-color accent meshes on unit prefabs.</summary>
    public static class UnitVisualAccent
    {
        public const string TeamAccentTransformName = "TeamAccent";
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void ApplyTeamColor(Transform visualRoot, Color slotColor)
        {
            if (visualRoot == null)
            {
                return;
            }

            ApplyRecursive(visualRoot, slotColor);
        }

        public static int CountAccents(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                return 0;
            }

            var count = 0;
            CountRecursive(visualRoot, ref count);
            return count;
        }

        static void ApplyRecursive(Transform node, Color slotColor)
        {
            if (IsAccentName(node.name))
            {
                var renderer = node.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, slotColor);
                    renderer.SetPropertyBlock(block);
                }
            }

            for (var i = 0; i < node.childCount; i++)
            {
                ApplyRecursive(node.GetChild(i), slotColor);
            }
        }

        static void CountRecursive(Transform node, ref int count)
        {
            if (IsAccentName(node.name) && node.GetComponent<Renderer>() != null)
            {
                count++;
            }

            for (var i = 0; i < node.childCount; i++)
            {
                CountRecursive(node.GetChild(i), ref count);
            }
        }

        static bool IsAccentName(string name) =>
            name == TeamAccentTransformName
            || name.StartsWith(TeamAccentTransformName + "_", System.StringComparison.Ordinal);
    }
}
