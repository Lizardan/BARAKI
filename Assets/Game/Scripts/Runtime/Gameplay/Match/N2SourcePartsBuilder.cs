using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Duel (N=2) road mesh: same square layout as N=4, emitted as one unioned <c>RoadSurface</c>.
    /// </summary>
    public static class N2SourcePartsBuilder
    {
        public const string RootName = "_SourceParts";
        public const int PartCount = N2RoadReferenceSpec.SourcePartsCount;

        public static Transform Populate(Transform parent, MatchArenaLayout layout, Material roadMaterial)
        {
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var footprints = RoadFootprintFactory.BuildN2(layout);
            RoadSurfaceMeshBuilder.Create(root, footprints, roadMaterial);
            return root;
        }
    }
}
