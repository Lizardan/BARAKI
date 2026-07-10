using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Procedural greybox for bases, lanes, and center arena ring in Game.unity.</summary>
    public sealed class MatchArenaGreybox : MonoBehaviour
    {
        [SerializeField] private int _playerCount = 4;
        [SerializeField] private float _arenaRadius = MatchArenaGenerator.DefaultArenaRadius;
        [SerializeField] private float _mainToTowerDistance = MatchArenaGenerator.DefaultMainToTowerDistance;
        [SerializeField] private float _centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius;
        [SerializeField] private bool _buildOnAwake = true;

        private Transform _visualRoot;

        public int PlayerCount => _playerCount;
        public MatchArenaLayout Layout { get; private set; }
        public LaneGraph Graph { get; private set; }

        public void Configure(int playerCount, float centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius)
        {
            _playerCount = Mathf.Clamp(playerCount, 2, 8);
            _centerArenaRadius = Mathf.Max(5f, centerArenaRadius);
            Rebuild();
        }

        private void Awake()
        {
            if (_buildOnAwake)
            {
                Rebuild();
            }
        }

        public void Rebuild()
        {
            ClearVisuals();
            Layout = MatchArenaGenerator.Generate(_playerCount, _arenaRadius, _mainToTowerDistance);
            Graph = LaneGraphBuilder.Build(Layout, _centerArenaRadius);
            _visualRoot = new GameObject("GreyboxVisual").transform;
            _visualRoot.SetParent(transform, false);
            MatchArenaGreyboxBuilder.Populate(_visualRoot, Layout, Graph);
        }

        public void ClearVisuals()
        {
            if (_visualRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_visualRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(_visualRoot.gameObject);
                }

                _visualRoot = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _playerCount = Mathf.Clamp(_playerCount, 2, 8);
            _arenaRadius = Mathf.Max(20f, _arenaRadius);
            _centerArenaRadius = Mathf.Max(5f, _centerArenaRadius);
        }
#endif
    }
}
