using Game.Core;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchDebugHudController : MonoBehaviour
    {
        const string HiddenClass = "match-hud__debug--hidden";
        const string SpawnPointsOnLabel = "Spawn points: ON";
        const string SpawnPointsOffLabel = "Spawn points: OFF";
        const string WaypointsOnLabel = "Waypoints: ON";
        const string WaypointsOffLabel = "Waypoints: OFF";

        [SerializeField] UIDocument _uiDocument;

        MatchRuntime _matchRuntime;
        VisualElement _panel;
        Button _addGoldButton;
        Button _addPassiveButton;
        Button _skipResearchButton;
        Button _winButton;
        Button _fireAllBarracksButton;
        Button _spawnPointsButton;
        Button _waypointsButton;
        int _localPlayerSlot = MatchSetup.DefaultLocalPlayerSlot;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _panel = root.Q<VisualElement>("DebugHudPanel");
            _addGoldButton = root.Q<Button>("DebugAddGoldButton");
            _addPassiveButton = root.Q<Button>("DebugAddPassiveButton");
            _skipResearchButton = root.Q<Button>("DebugSkipResearchButton");
            _winButton = root.Q<Button>("DebugWinButton");
            _fireAllBarracksButton = root.Q<Button>("DebugFireAllBarracksButton");
            _spawnPointsButton = root.Q<Button>("DebugSpawnPointsButton");
            _waypointsButton = root.Q<Button>("DebugWaypointsButton");
            RefreshSpawnPointsButtonLabel();
            RefreshWaypointsButtonLabel();

#if UNITY_EDITOR
            ShowPanel();
#else
            HidePanel();
#endif
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (_addGoldButton != null)
            {
                _addGoldButton.clicked += OnAddGold;
            }

            if (_addPassiveButton != null)
            {
                _addPassiveButton.clicked += OnAddPassive;
            }

            if (_skipResearchButton != null)
            {
                _skipResearchButton.clicked += OnSkipResearch;
            }

            if (_winButton != null)
            {
                _winButton.clicked += OnWinLocal;
            }

            if (_fireAllBarracksButton != null)
            {
                _fireAllBarracksButton.clicked += OnFireAllBarracks;
            }

            if (_spawnPointsButton != null)
            {
                _spawnPointsButton.clicked += OnToggleSpawnPoints;
            }

            if (_waypointsButton != null)
            {
                _waypointsButton.clicked += OnToggleWaypoints;
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            if (_addGoldButton != null)
            {
                _addGoldButton.clicked -= OnAddGold;
            }

            if (_addPassiveButton != null)
            {
                _addPassiveButton.clicked -= OnAddPassive;
            }

            if (_skipResearchButton != null)
            {
                _skipResearchButton.clicked -= OnSkipResearch;
            }

            if (_winButton != null)
            {
                _winButton.clicked -= OnWinLocal;
            }

            if (_fireAllBarracksButton != null)
            {
                _fireAllBarracksButton.clicked -= OnFireAllBarracks;
            }

            if (_spawnPointsButton != null)
            {
                _spawnPointsButton.clicked -= OnToggleSpawnPoints;
            }

            if (_waypointsButton != null)
            {
                _waypointsButton.clicked -= OnToggleWaypoints;
            }
#endif
        }

        void Start()
        {
            _matchRuntime = FindAnyObjectByType<MatchRuntime>();
        }

        void LateUpdate()
        {
#if UNITY_EDITOR
            _localPlayerSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }
#else
            HidePanel();
#endif
        }

        void ShowPanel()
        {
            _panel?.RemoveFromClassList(HiddenClass);
        }

        void HidePanel()
        {
            _panel?.AddToClassList(HiddenClass);
        }

        bool TryGetMutableController(out MatchController controller)
        {
            controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null || !controller.IsRunning)
            {
                return false;
            }

            if (_matchRuntime != null
                && !MatchTickAuthority.ShouldTickSimulation(_matchRuntime.TickMode))
            {
                return false;
            }

            return true;
        }

        void OnAddGold()
        {
            if (!TryGetMutableController(out var controller))
            {
                return;
            }

            if (_localPlayerSlot < 0 || _localPlayerSlot >= controller.Players.Count)
            {
                return;
            }

            controller.Players[_localPlayerSlot].Gold += 1000;
        }

        void OnAddPassive()
        {
            if (!TryGetMutableController(out var controller))
            {
                return;
            }

            controller.DebugBumpPassiveGold(_localPlayerSlot);
        }

        void OnSkipResearch()
        {
            if (!TryGetMutableController(out var controller))
            {
                return;
            }

            controller.DebugCompleteResearchForOwner(_localPlayerSlot);
        }

        void OnWinLocal()
        {
            if (!TryGetMutableController(out var controller))
            {
                return;
            }

            if (controller.Phase is MatchPhase.Lobby or MatchPhase.End)
            {
                return;
            }

            controller.EndMatch(_localPlayerSlot);
        }

        void OnFireAllBarracks()
        {
            if (!TryGetMutableController(out var controller))
            {
                return;
            }

            controller.DebugFireAllBarracksWaves();
        }

        void OnToggleSpawnPoints()
        {
            BarracksSpawnDebugOverlay.IsVisible = !BarracksSpawnDebugOverlay.IsVisible;
            RefreshSpawnPointsButtonLabel();
        }

        void OnToggleWaypoints()
        {
            LaneWaypointDebugOverlay.IsVisible = !LaneWaypointDebugOverlay.IsVisible;
            RefreshWaypointsButtonLabel();
        }

        void RefreshSpawnPointsButtonLabel()
        {
            if (_spawnPointsButton == null)
            {
                return;
            }

            _spawnPointsButton.text = BarracksSpawnDebugOverlay.IsVisible
                ? SpawnPointsOnLabel
                : SpawnPointsOffLabel;
        }

        void RefreshWaypointsButtonLabel()
        {
            if (_waypointsButton == null)
            {
                return;
            }

            _waypointsButton.text = LaneWaypointDebugOverlay.IsVisible
                ? WaypointsOnLabel
                : WaypointsOffLabel;
        }
    }
}
