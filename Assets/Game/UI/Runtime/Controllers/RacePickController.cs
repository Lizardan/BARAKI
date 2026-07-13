using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RacePickController : MonoBehaviour
    {
        private const string SelectedClass = "race-pick__race-btn--selected";
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
            SetPanInputLocked(false);
        }

        private void BeginRacePick()
        {
            var setup = GameSession.ActiveSetup ?? MatchSetup.Default;
            var localSlot = LocalMatchRegistry.LocalPlayerSlot ?? setup.LocalPlayerSlot;
            localSlot = Mathf.Clamp(localSlot, 0, setup.PlayerCount - 1);
            GameSession.UpdateActiveSetup(new MatchSetup(setup.PlayerCount, localSlot, setup.RaceIds));
            _session = new RacePickSession(setup.PlayerCount, localSlot);
            _selectedRaceId = null;

            _subtitleLabel.text =
                $"Слот {localSlot + 1} · игроков {setup.PlayerCount}";
            _slotsLabel.text = BuildSlotsHint(setup.PlayerCount, localSlot);
            _selectedLabel.text = "Раса не выбрана";
            UpdateRaceButtons();
            UpdateConfirmButton();
            SetPanInputLocked(true);
            Show();
        }

        private void SelectRace(string raceId)
        {
            _selectedRaceId = raceId;
            _selectedLabel.text = $"Выбрано: {RacePickRules.GetDisplayName(raceId)}";
            UpdateRaceButtons();
            UpdateConfirmButton();
        }

        private void OnConfirm()
        {
            if (string.IsNullOrEmpty(_selectedRaceId) || _session == null || _matchRuntime == null)
            {
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

        private void SetPanInputLocked(bool locked)
        {
            _panController?.SetPanInputLocked(locked);
        }

        private void UpdateRaceButtons()
        {
            _humanButton.EnableInClassList(SelectedClass, _selectedRaceId == GameIds.Races.Human);
            _bugButton.EnableInClassList(SelectedClass, _selectedRaceId == GameIds.Races.Bug);
        }

        private void UpdateConfirmButton()
        {
            var enabled = !string.IsNullOrEmpty(_selectedRaceId);
            _confirmButton.SetEnabled(enabled);
            _confirmButton.EnableInClassList(ConfirmDisabledClass, !enabled);
        }

        private static string BuildSlotsHint(int playerCount, int localSlot)
        {
            var lines = $"Ваш слот: {localSlot + 1} — случайно назначен, вы выбираете расу.\n";

            for (var slot = 0; slot < playerCount; slot++)
            {
                if (slot == localSlot)
                {
                    continue;
                }

                lines += $"Слот {slot + 1}: случайная раса после «Готов».\n";
            }

            return lines.TrimEnd();
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
