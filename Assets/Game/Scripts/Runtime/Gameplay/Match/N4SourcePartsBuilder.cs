using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Procedural N=4 road mesh: flat <c>_SourceParts</c> with a single unioned <c>RoadSurface</c>.</summary>
    public static class N4SourcePartsBuilder
    {
        public const string RootName = "_SourceParts";
        public const int PartCount = N4RoadReferenceSpec.SourcePartsCount;

        public static Transform Populate(Transform parent, MatchArenaLayout layout, Material roadMaterial)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var footprints = RoadFootprintFactory.BuildN4(layout);
            RoadSurfaceMeshBuilder.Create(root, footprints, roadMaterial);
            return root;
        }
    }
}
