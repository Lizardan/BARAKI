using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using Game.Gameplay.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RacePickController : MonoBehaviour
    {
        private const string SelectedClass = "race-pick__race-btn--selected";
        private const string LockedClass = "race-pick__race-btn--locked";
        private const string HiddenClass = "race-pick--hidden";
        private const string ConfirmDisabledClass = "race-pick__confirm--disabled";

        [SerializeField] private UIDocument _uiDocument;

        private MatchRuntime _matchRuntime;
        private GameplayCameraPanController _panController;
        private VisualElement _screen;
        private Label _subtitleLabel;
        private Label _slotsLabel;
        private Label _selectedLabel;
        private Button _humanButton;
        private Button _bugButton;
        private Button _confirmButton;
        private RacePickSession _session;
        private string _selectedRaceId;
        private bool _localPickSubmitted;
        private bool _networkPickSubscribed;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _screen = root.Q<VisualElement>("RacePickScreen");
            _subtitleLabel = root.Q<Label>("SubtitleLabel");
            _slotsLabel = root.Q<Label>("SlotsLabel");
            _selectedLabel = root.Q<Label>("SelectedLabel");
            _humanButton = root.Q<Button>("HumanButton");
            _bugButton = root.Q<Button>("BugButton");
            _confirmButton = root.Q<Button>("ConfirmButton");

            _humanButton.clicked += () => SelectRace(GameIds.Races.Human);
            _bugButton.clicked += () => SelectRace(GameIds.Races.Bug);
            _confirmButton.clicked += OnConfirm;
            RefreshRaceAvailability();
        }

        private void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }

            if (_panController == null)
            {
                _panController = FindAnyObjectByType<GameplayCameraPanController>();
            }

            if (!GameSession.IsPlaying || _matchRuntime == null || _matchRuntime.IsMatchStarted)
            {
                SetPanInputLocked(false);
                Hide();
                return;
            }

            BeginRacePick();
        }

        private void OnDisable()
        {
            UnsubscribeNetworkPick();
            SetPanInputLocked(false);
        }

        private void BeginRacePick()
        {
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            var localSlot = ResolveLocalSlot(setup);
            GameSession.UpdateActiveSetup(new MatchSetup(setup.PlayerCount, localSlot, setup.RaceIds));
            _session = new RacePickSession(setup.PlayerCount, localSlot);
            _selectedRaceId = null;
            _localPickSubmitted = false;

            if (MatchNetworkSession.IsNetworked)
            {
                MatchNetworkSession.EnsureRacePickSession(setup.PlayerCount);
                SubscribeNetworkPick();
            }
            else
            {
                UnsubscribeNetworkPick();
            }

            RefreshRacePickUi();
            SetPanInputLocked(true);
            Show();
        }

        private void SelectRace(string raceId)
        {
            if (_localPickSubmitted || _matchRuntime == null || _matchRuntime.IsMatchStarted)
            {
                return;
            }

            if (!RacePickRules.IsSelectable(raceId))
            {
                return;
            }

            _selectedRaceId = raceId;
            RefreshRacePickUi();
        }

        private void OnConfirm()
        {
            if (string.IsNullOrEmpty(_selectedRaceId) || _session == null || _matchRuntime == null
                || _localPickSubmitted)
            {
                return;
            }

            if (MatchNetworkSession.IsNetworked)
            {
                MatchNetworkSession.RequestRacePick(_selectedRaceId);
                _localPickSubmitted = true;
                RefreshRacePickUi();
                return;
            }

            _session.SetLocalPick(_selectedRaceId);
            _session.ConfirmOfflinePick();
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            GameSession.UpdateActiveSetup(new MatchSetup(
                setup.PlayerCount,
                _session.LocalPlayerSlot,
                _session.ToRaceIdsArray()));
            SetPanInputLocked(false);
            _matchRuntime.StartMatch(_session.ToRaceIdsArray(), _session.LocalPlayerSlot);
            Hide();
        }

        private void OnNetworkPickChanged()
        {
            if (_matchRuntime != null
                && (_matchRuntime.IsMatchStarted || MatchNetworkSession.NetworkMatchSimStarted))
            {
                SetPanInputLocked(false);
                Hide();
                return;
            }

            RefreshRacePickUi();
        }

        private void RefreshRacePickUi()
        {
            if (_session == null)
            {
                return;
            }

            _subtitleLabel.text =
                $"Слот {_session.LocalPlayerSlot + 1} · игроков {_session.PlayerCount}";
            _slotsLabel.text = BuildSlotsHint(_session.PlayerCount, _session.LocalPlayerSlot);
            _selectedLabel.text = string.IsNullOrEmpty(_selectedRaceId)
                ? "Раса не выбрана"
                : $"Выбрано: {RacePickRules.GetDisplayName(_selectedRaceId)}";
            UpdateRaceButtons();
            UpdateConfirmButton();
        }

        private void SubscribeNetworkPick()
        {
            if (_networkPickSubscribed)
            {
                return;
            }

            MatchNetworkSession.NetworkRacePickChanged += OnNetworkPickChanged;
            _networkPickSubscribed = true;
        }

        private void UnsubscribeNetworkPick()
        {
            if (!_networkPickSubscribed)
            {
                return;
            }

            MatchNetworkSession.NetworkRacePickChanged -= OnNetworkPickChanged;
            _networkPickSubscribed = false;
        }

        private void SetPanInputLocked(bool locked)
        {
            _panController?.SetPanInputLocked(locked);
        }

        private void UpdateRaceButtons()
        {
            var canSelect = !_localPickSubmitted
                && (_matchRuntime == null || !_matchRuntime.IsMatchStarted);
            _humanButton.SetEnabled(canSelect && RacePickRules.IsSelectable(GameIds.Races.Human));
            _bugButton.SetEnabled(canSelect && RacePickRules.IsSelectable(GameIds.Races.Bug));
            _humanButton.EnableInClassList(SelectedClass, _selectedRaceId == GameIds.Races.Human);
            _bugButton.EnableInClassList(SelectedClass, _selectedRaceId == GameIds.Races.Bug);
            RefreshRaceAvailability();
        }

        void RefreshRaceAvailability()
        {
            if (_bugButton == null || _humanButton == null)
            {
                return;
            }

            _bugButton.EnableInClassList(LockedClass, !RacePickRules.IsSelectable(GameIds.Races.Bug));
            _humanButton.EnableInClassList(LockedClass, !RacePickRules.IsSelectable(GameIds.Races.Human));
            if (!RacePickRules.IsSelectable(GameIds.Races.Bug))
            {
                _bugButton.tooltip = "Раса недоступна в текущем playtest";
            }
        }

        private void UpdateConfirmButton()
        {
            var enabled = !string.IsNullOrEmpty(_selectedRaceId)
                && !_localPickSubmitted
                && (_matchRuntime == null || !_matchRuntime.IsMatchStarted);
            _confirmButton.SetEnabled(enabled);
            _confirmButton.EnableInClassList(ConfirmDisabledClass, !enabled);
            if (_localPickSubmitted && MatchNetworkSession.IsNetworked)
            {
                _confirmButton.text = "ЖДЁМ ОСТАЛЬНЫХ";
            }
            else
            {
                _confirmButton.text = "ГОТОВ";
            }
        }

        private string BuildSlotsHint(int playerCount, int localSlot)
        {
            if (MatchNetworkSession.IsNetworked)
            {
                var lines = $"Ваш слот: {localSlot + 1} — выберите расу и нажмите «Готов».\n";

                for (var slot = 0; slot < playerCount; slot++)
                {
                    if (slot == localSlot)
                    {
                        continue;
                    }

                    lines += MatchNetworkSession.HasRacePick(slot)
                        ? $"Слот {slot + 1}: выбрал расу.\n"
                        : $"Слот {slot + 1}: выбирает…\n";
                }

                return lines.TrimEnd();
            }

            var offlineLines = $"Ваш слот: {localSlot + 1} — случайно назначен, вы выбираете расу.\n";

            for (var slot = 0; slot < playerCount; slot++)
            {
                if (slot == localSlot)
                {
                    continue;
                }

                offlineLines += $"Слот {slot + 1}: случайная раса после «Готов».\n";
            }

            return offlineLines.TrimEnd();
        }

        private static int ResolveLocalSlot(MatchSetup setup)
        {
            if (MatchNetworkSession.IsNetworked)
            {
                var slot = MatchNetworkSession.LocalSlot;
                return Mathf.Clamp(slot < 0 ? setup.LocalPlayerSlot : slot, 0, setup.PlayerCount - 1);
            }

            return Mathf.Clamp(LocalMatchRegistry.LocalPlayerSlot ?? setup.LocalPlayerSlot, 0, setup.PlayerCount - 1);
        }

        private void Show()
        {
            _screen?.RemoveFromClassList(HiddenClass);
        }

        private void Hide()
        {
            _screen?.AddToClassList(HiddenClass);
        }
    }
}
