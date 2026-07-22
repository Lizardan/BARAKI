using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>World-space UVs for road meshes under <c>_SourceParts</c> (matches grass tiling density).</summary>
    public static class RoadMeshUv
    {
        public const float WorldUnitsPerRepeat = 8.75f;

        public static Vector2 FromWorld(float x, float z) =>
            new Vector2(x / WorldUnitsPerRepeat, z / WorldUnitsPerRepeat);

        public static Vector2 FromWorld(Vector3 world) => FromWorld(world.x, world.z);
    }
}
