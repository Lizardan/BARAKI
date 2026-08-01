using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Ring (N=3,5..8) road mesh: one unioned road surface under <c>_SourceParts</c>.</summary>
    public static class N3SourcePartsBuilder
    {
        public const string RootName = N4SourcePartsBuilder.RootName;
        public const int PartCount = 1;

        public static Transform Populate(Transform parent, MatchArenaLayout layout, Material roadMaterial)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var footprints = layout.PlayerCount == 3
                ? RoadFootprintFactory.BuildN3(layout)
                : RoadFootprintFactory.BuildRing(layout);
            RoadSurfaceMeshBuilder.Create(root, footprints, roadMaterial);
            return root;
        }
    }
}
