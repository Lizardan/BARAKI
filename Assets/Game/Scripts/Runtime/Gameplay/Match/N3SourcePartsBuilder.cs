using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Three-player road mesh: one unioned road surface under <c>_SourceParts</c>.</summary>
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

            var footprints = RoadFootprintFactory.BuildN3(layout);
            RoadSurfaceMeshBuilder.Create(root, footprints, roadMaterial);
            return root;
        }
    }
}
