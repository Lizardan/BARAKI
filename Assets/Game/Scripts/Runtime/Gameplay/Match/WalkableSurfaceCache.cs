using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Static walkable bake per map mode (player count). SourceParts geometry is deterministic for N=2/N=4.
    /// </summary>
    public static class WalkableSurfaceCache
    {
        static readonly Dictionary<int, WalkableSurface> s_byPlayerCount = new();

        public static WalkableSurface GetOrCreate(int playerCount)
        {
            if (playerCount < 2 || playerCount > 8)
            {
                throw new System.ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Player count must be 2..8.");
            }

            if (s_byPlayerCount.TryGetValue(playerCount, out var cached))
            {
                return cached;
            }

            var surface = Bake(playerCount);
            s_byPlayerCount[playerCount] = surface;
            return surface;
        }

        /// <summary>Edit Mode tests only.</summary>
        public static void Clear()
        {
            s_byPlayerCount.Clear();
        }

        public static bool HasCached(int playerCount) => s_byPlayerCount.ContainsKey(playerCount);

        static WalkableSurface Bake(int playerCount)
        {
            var root = new GameObject($"WalkableBake_N{playerCount}");
            try
            {
                var layout = MatchArenaGenerator.Generate(playerCount);
                var graph = LaneGraphBuilder.Build(layout);
                MatchArenaGreyboxBuilder.PopulateRoadPrefabContent(root.transform, layout, graph);

                var sourceParts = root.transform.Find(N4SourcePartsBuilder.RootName);
                if (sourceParts != null)
                {
                    return WalkableSurfaceBuilder.BuildFromSourceParts(sourceParts);
                }

                // Non 2/4 fallback: bake all road meshes under the temp root.
                return WalkableSurfaceBuilder.BuildFromSourceParts(root.transform);
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}
