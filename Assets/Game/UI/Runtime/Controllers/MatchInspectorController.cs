using System;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchInspectorController : MonoBehaviour
    {
        const int CommandSlotCount = 12;
        const string EmptyOverlayHiddenClass = "match-panel__empty-overlay--hidden";
        const string InspectorEmptyClass = "match-inspector--empty";
        const string PanelActiveClass = "match-dock-panel__inner--active";
        const string CommandGridHiddenClass = "match-command-grid--hidden";
        const string UnitInfoHiddenClass = "match-unit-info--hidden";

        [SerializeField] private UIDocument _uiDocument;

        MatchRuntime _matchRuntime;
        MatchSelection _selection;
        int _localPlayerSlot = MatchSetup.DefaultLocalPlayerSlot;
        int _selectedBuildingInstanceId = -1;
        Label _title;
        Label _owner;
        Label _hp;
        Label _meta;
        Label _badge;
        Label _readonly;
        VisualElement _commandGrid;
        VisualElement _unitInfoPanel;
        VisualElement _emptyOverlay;
        VisualElement _panelBody;
        readonly Button[] _commandSlots = new Button[CommandSlotCount];
        readonly Action[] _commandActions = new Action[CommandSlotCount];

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _title = root.Q<Label>("InspectorTitle");
            _owner = root.Q<Label>("InspectorOwner");
            _hp = root.Q<Label>("InspectorHp");
            _meta = root.Q<Label>("InspectorMeta");
            _badge = root.Q<Label>("InspectorBadge");
            _readonly = root.Q<Label>("InspectorReadonly");
            _commandGrid = root.Q<VisualElement>("CommandGrid");
            _unitInfoPanel = root.Q<VisualElement>("UnitInfoPanel");
            _emptyOverlay = root.Q<VisualElement>("InspectorEmptyOverlay");
            _panelBody = root.Q<VisualElement>("InspectorPanelBody");

            for (var i = 0; i < CommandSlotCount; i++)
            {
                var index = i;
                var button = root.Q<Button>($"CommandSlot{i}");
                _commandSlots[i] = button;
                if (button != null)
                {
                    button.clicked += () => OnCommandClicked(index);
                }
            }
        }

        void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }

            _localPlayerSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            SubscribeSelection();
        }

        void OnDisable()
        {
            UnsubscribeSelection();
        }

        void LateUpdate()
        {
            _localPlayerSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            if (_selection == null)
            {
                SubscribeSelection();
            }

            RefreshLiveStats();
        }

        void SubscribeSelection()
        {
            UnsubscribeSelection();
            _selection = _matchRuntime != null ? _matchRuntime.Selection : null;
            if (_selection != null)
            {
                _selection.Changed += OnSelectionChanged;
                OnSelectionChanged(_selection.Current);
            }
        }

        void UnsubscribeSelection()
        {
            if (_selection != null)
            {
                _selection.Changed -= OnSelectionChanged;
                _selection = null;
            }
        }

        void OnSelectionChanged(MatchPickTarget target) => Refresh(target);

        void RefreshLiveStats()
        {
            if (_selection == null || !_selection.Current.HasTarget)
            {
                return;
            }

            Refresh(_selection.Current);
        }

        void Refresh(MatchPickTarget target)
        {
            ClearCommands();
            _selectedBuildingInstanceId = -1;

            if (!target.HasTarget)
            {
                ClearInspectorLabels();
                SetInspectorEmpty(true);
                SetPanelActive(false);
                SetEmptyOverlayVisible(true);
                ShowCommandGrid(false);
                ShowUnitInfo(false);
                return;
            }

            SetInspectorEmpty(false);
            SetPanelActive(true);
            SetEmptyOverlayVisible(false);

            if (target.IsUnit)
            {
                ShowCommandGrid(false);
                ShowUnitInfo(true);
                RefreshUnit(target.EntityId);
                return;
            }

            RefreshBuilding(target.EntityId);
        }

        void RefreshUnit(int unitId)
        {
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                _title.text = "Юнит";
                _owner.text = string.Empty;
                _hp.text = "HP: —";
                _meta.text = string.Empty;
                _badge.text = string.Empty;
                _readonly.text = string.Empty;
                return;
            }

            _title.text = MatchInspectorFormatting.FormatRole(unit.Role);
            _owner.text = string.Empty;
            _hp.text = $"HP: {MatchInspectorFormatting.FormatHp(unit.CurrentHp, unit.Stats.MaxHp)}";
            _meta.text = string.Empty;
            _badge.text = string.Empty;
            _readonly.text = string.Empty;
        }

        void RefreshBuilding(int instanceId)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            var building = controller?.Buildings.GetByInstanceId(instanceId);
            if (building == null)
            {
                ShowCommandGrid(false);
                ShowUnitInfo(true);
                _title.text = "Здание";
                _owner.text = string.Empty;
                _hp.text = "HP: —";
                _meta.text = string.Empty;
                _badge.text = string.Empty;
                _readonly.text = string.Empty;
                return;
            }

            var canControl = building.IsIntact && building.OwnerSlot == _localPlayerSlot;
            if (canControl)
            {
                _selectedBuildingInstanceId = building.InstanceId;
                ShowUnitInfo(false);
                ShowCommandGrid(true);
                PopulateBuildingCommands(building);
                return;
            }

            ShowCommandGrid(false);
            ShowUnitInfo(true);
            _title.text = MatchInspectorFormatting.FormatBuildingName(building.BuildingId);
            _owner.text = string.Empty;
            _hp.text = $"HP: {MatchInspectorFormatting.FormatHp(building.CurrentHp, building.MaxHp)}";
            _meta.text = string.Empty;
            _badge.text = building.IsRuins ? "Руины" : string.Empty;
            _readonly.text = string.Empty;
        }

        void PopulateBuildingCommands(BuildingState building)
        {
            if (MatchInspectorFormatting.IsMainBuilding(building.BuildingId))
            {
                SetCommand(0, "Ур. здания", enabled: false, null);
                SetCommand(1, "Passive Gold\n200g", enabled: true, () => StartResearch(GameIds.Upgrades.MainPassiveGold));
                SetCommand(2, "Magic", enabled: false, null);
                SetCommand(3, "Герой", enabled: false, null);
                return;
            }

            if (MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId))
            {
                var controller = _matchRuntime.Controller;
                var barracks = controller.WaveScheduler.GetBarracks(building.OwnerSlot, building.BuildingId);
                var cost = 0;
                var canUpgrade = barracks != null
                    && MatchEconomyRules.TryGetBarracksLevelUpgrade(barracks.Level, out cost, out _);
                var label = canUpgrade ? $"Ур. казарм\n{cost}g" : "Макс. ур.";
                SetCommand(0, label, canUpgrade, () => StartResearch(GameIds.Upgrades.BarracksLevel));
                SetCommand(1, "Статы", enabled: false, null);
                SetCommand(2, "Герой", enabled: false, null);
                return;
            }

            if (MatchInspectorFormatting.IsTowerBuilding(building.BuildingId))
            {
                SetCommand(0, "Цель", enabled: false, null);
                SetCommand(1, "Апгрейд", enabled: false, null);
            }
        }

        void StartResearch(string upgradeId)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null || _selectedBuildingInstanceId < 0)
            {
                return;
            }

            controller.TryStartResearch(_localPlayerSlot, _selectedBuildingInstanceId, upgradeId);
        }

        void SetCommand(int index, string label, bool enabled, Action action)
        {
            if (index < 0 || index >= CommandSlotCount || _commandSlots[index] == null)
            {
                return;
            }

            _commandSlots[index].text = label;
            _commandSlots[index].SetEnabled(enabled && action != null);
            _commandActions[index] = action;
        }

        void OnCommandClicked(int index)
        {
            _commandActions[index]?.Invoke();
        }

        void ClearCommands()
        {
            for (var i = 0; i < CommandSlotCount; i++)
            {
                _commandActions[i] = null;
                if (_commandSlots[i] == null)
                {
                    continue;
                }

                _commandSlots[i].text = string.Empty;
                _commandSlots[i].SetEnabled(false);
            }
        }

        MatchUnitState FindUnit(int unitId)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null)
            {
                return null;
            }

            foreach (var unit in controller.Combat.Units)
            {
                if (unit.UnitId == unitId)
                {
                    return unit;
                }
            }

            return null;
        }

        void ShowCommandGrid(bool visible)
        {
            _commandGrid?.EnableInClassList(CommandGridHiddenClass, !visible);
        }

        void ShowUnitInfo(bool visible)
        {
            _unitInfoPanel?.EnableInClassList(UnitInfoHiddenClass, !visible);
        }

        void SetEmptyOverlayVisible(bool visible)
        {
            _emptyOverlay?.EnableInClassList(EmptyOverlayHiddenClass, !visible);
        }

        void SetInspectorEmpty(bool isEmpty)
        {
            _panelBody?.EnableInClassList(InspectorEmptyClass, isEmpty);
        }

        void SetPanelActive(bool active)
        {
            _panelBody?.EnableInClassList(PanelActiveClass, active);
        }

        void ClearInspectorLabels()
        {
            _title.text = string.Empty;
            _owner.text = string.Empty;
            _hp.text = string.Empty;
            _meta.text = string.Empty;
            _badge.text = string.Empty;
            _readonly.text = string.Empty;
        }
    }
}
