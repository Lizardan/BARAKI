using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Resource catalog of OccaSoftware nature prefabs for arena décor.</summary>
    [CreateAssetMenu(
        fileName = "MatchArenaEnvironmentCatalog",
        menuName = "Game/Match/Environment Catalog")]
    public sealed class MatchArenaEnvironmentCatalog : ScriptableObject
    {
        [SerializeField] private GameObject[] _trees = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _pines = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _rocks = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _cliffs = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _flowers = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _mountains = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _pathPieces = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _boats = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _bridges = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _lanterns = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _crates = System.Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _benches = System.Array.Empty<GameObject>();

        public MatchArenaEnvironmentPrefabSet ToPrefabSet() => new()
        {
            Trees = _trees,
            Pines = _pines,
            Rocks = _rocks,
            Cliffs = _cliffs,
            Flowers = _flowers,
            Mountains = _mountains,
            PathPieces = _pathPieces,
            Boats = _boats,
            Bridges = _bridges,
            Lanterns = _lanterns,
            Crates = _crates,
            Benches = _benches,
        };

#if UNITY_EDITOR
        public void EditorAssign(
            GameObject[] trees,
            GameObject[] pines,
            GameObject[] rocks,
            GameObject[] cliffs,
            GameObject[] flowers,
            GameObject[] mountains,
            GameObject[] pathPieces,
            GameObject[] boats = null,
            GameObject[] bridges = null,
            GameObject[] lanterns = null,
            GameObject[] crates = null,
            GameObject[] benches = null)
        {
            _trees = trees ?? System.Array.Empty<GameObject>();
            _pines = pines ?? System.Array.Empty<GameObject>();
            _rocks = rocks ?? System.Array.Empty<GameObject>();
            _cliffs = cliffs ?? System.Array.Empty<GameObject>();
            _flowers = flowers ?? System.Array.Empty<GameObject>();
            _mountains = mountains ?? System.Array.Empty<GameObject>();
            _pathPieces = pathPieces ?? System.Array.Empty<GameObject>();
            _boats = boats ?? System.Array.Empty<GameObject>();
            _bridges = bridges ?? System.Array.Empty<GameObject>();
            _lanterns = lanterns ?? System.Array.Empty<GameObject>();
            _crates = crates ?? System.Array.Empty<GameObject>();
            _benches = benches ?? System.Array.Empty<GameObject>();
        }
#endif
    }
}
