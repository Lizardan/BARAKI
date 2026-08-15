using Game.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Match
{
    /// <summary>Procedural greybox for bases, lanes, and center arena ring in Game.unity.</summary>
    public sealed class MatchArenaGreybox : MonoBehaviour
    {
        public static MatchArenaGreybox Current { get; private set; }

        [SerializeField] private int _playerCount = 4;
        [SerializeField] private float _arenaRadius = MatchArenaGenerator.DefaultArenaRadius;
        [SerializeField] private float _mainToTowerDistance = MatchArenaGenerator.DefaultMainToTowerDistance;
        [SerializeField] private float _centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius;
        [SerializeField] private bool _buildOnAwake = true;

        private Transform _visualRoot;

        public int PlayerCount => _playerCount;
        public MatchArenaLayout Layout { get; private set; }
        public LaneGraph Graph { get; private set; }

        /// <summary>
        /// Building marker under <c>GreyboxVisual/Bases/Player_{slot}/{buildingId}</c>
        /// (also accepts a slot parented directly under GreyboxVisual).
        /// </summary>
        public static Transform FindBuildingVisual(BuildingState building)
        {
            if (building == null)
            {
                return null;
            }

            var greybox = Current != null ? Current : Object.FindFirstObjectByType<MatchArenaGreybox>();
            return greybox != null
                ? greybox.FindBuildingVisual(building.OwnerSlot, building.BuildingId)
                : null;
        }

        public Transform FindBuildingVisual(int ownerSlot, string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                return null;
            }

            var searchRoot = _visualRoot != null ? _visualRoot : transform.Find("GreyboxVisual");
            if (searchRoot == null)
            {
                searchRoot = transform;
            }

            var slotRoot = FindNamedChild(searchRoot, $"Player_{ownerSlot}");
            return slotRoot != null ? slotRoot.Find(buildingId) : null;
        }

        static Transform FindNamedChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamedChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public void Configure(int playerCount, float centerArenaRadius = LaneGraphBuilder.DefaultCenterArenaRadius)
        {
            _playerCount = Mathf.Clamp(playerCount, MatchModeRules.MinPlayers, MatchModeRules.MaxPlayers);
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

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void Rebuild()
        {
            ClearVisuals();
            Layout = MatchArenaGenerator.Generate(_playerCount, _arenaRadius, _mainToTowerDistance);
            Graph = LaneGraphBuilder.Build(Layout, _centerArenaRadius);

            // Dedicated Server Optimizations strip shaders; Null gfx has no materials.
            if (!ShouldBuildVisuals())
            {
                return;
            }

            _visualRoot = new GameObject("GreyboxVisual").transform;
            _visualRoot.SetParent(transform, false);
            MatchArenaGreyboxBuilder.Populate(_visualRoot, Layout, Graph);

            MatchArenaEnvironmentDecorator.Populate(
                transform,
                Layout,
                WalkableSurfaceCache.GetOrCreate(_playerCount),
                MatchArenaEnvironmentDecorator.LoadPrefabSetOrEmpty());
        }

        /// <summary>Layout/graph stay available on server; mesh materials do not.</summary>
        public static bool ShouldBuildVisuals()
        {
#if UNITY_EDITOR
            // Dedicated Server build target defines UNITY_SERVER in Editor — still draw greybox in Play Mode.
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
#elif UNITY_SERVER || BARAKI_DEDICATED_SERVER
            return false;
#else
            return SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
#endif
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

            MatchArenaEnvironmentDecorator.ClearDecor(transform);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _playerCount = Mathf.Clamp(_playerCount, MatchModeRules.MinPlayers, MatchModeRules.MaxPlayers);
            _arenaRadius = Mathf.Max(20f, _arenaRadius);
            _centerArenaRadius = Mathf.Max(5f, _centerArenaRadius);
        }
#endif
    }
}
