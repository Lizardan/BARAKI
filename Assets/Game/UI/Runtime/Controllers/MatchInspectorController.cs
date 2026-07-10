using System.Collections.Generic;
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
        const string EmptyOverlayHiddenClass = "match-panel__empty-overlay--hidden";
        const string InspectorEmptyClass = "match-inspector--empty";
        const string PanelActiveClass = "match-dock-panel__inner--active";

        [SerializeField] private UIDocument _uiDocument;

        MatchRuntime _matchRuntime;
        MatchSelection _selection;
        int _localPlayerSlot = MatchSetup.DefaultLocalPlayerSlot;
        Label _title;
        Label _owner;
        Label _hp;
        Label _meta;
        Label _badge;
        Label _readonly;
        ScrollView _actions;
        VisualElement _emptyOverlay;
        VisualElement _panelBody;
        readonly List<Button> _actionButtons = new();

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
            _actions = root.Q<ScrollView>("InspectorActions");
            _emptyOverlay = root.Q<VisualElement>("InspectorEmptyOverlay");
            _panelBody = root.Q<VisualElement>("InspectorPanelBody");
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
            ClearActions();

            if (!target.HasTarget)
            {
                ClearInspectorLabels();
                SetInspectorEmpty(true);
                SetPanelActive(false);
                SetEmptyOverlayVisible(true);
                return;
            }

            SetInspectorEmpty(false);
            SetPanelActive(true);
            SetEmptyOverlayVisible(false);

            if (target.IsUnit)
            {
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
            _owner.text = MatchInspectorFormatting.FormatOwnerLabel(unit.OwnerSlot);
            _hp.text = $"HP: {MatchInspectorFormatting.FormatHp(unit.CurrentHp, unit.Stats.MaxHp)}";
            _meta.text = $"Lane: {unit.LaneId}";
            _badge.text = string.Empty;
            _readonly.text = MatchInspectorFormatting.FormatReadonlyHint();
        }

        void RefreshBuilding(int instanceId)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            var building = controller?.Buildings.GetByInstanceId(instanceId);
            if (building == null)
            {
                _title.text = "Здание";
                _owner.text = string.Empty;
                _hp.text = "HP: —";
                _meta.text = string.Empty;
                _badge.text = string.Empty;
                _readonly.text = string.Empty;
                return;
            }

            _title.text = MatchInspectorFormatting.FormatBuildingName(building.BuildingId);
            _owner.text = MatchInspectorFormatting.FormatOwnerLabel(building.OwnerSlot);
            _hp.text = $"HP: {MatchInspectorFormatting.FormatHp(building.CurrentHp, building.MaxHp)}";
            _meta.text = building.BuildingId;
            _badge.text = building.IsRuins ? "Руины" : string.Empty;

            var canControl = building.IsIntact && building.OwnerSlot == _localPlayerSlot;
            _readonly.text = canControl ? string.Empty : MatchInspectorFormatting.FormatReadonlyHint();
            if (canControl)
            {
                PopulateBuildingActions(building.BuildingId);
            }
        }

        void PopulateBuildingActions(string buildingId)
        {
            if (MatchInspectorFormatting.IsMainBuilding(buildingId))
            {
                AddStubAction("Upgrade Level");
                AddStubAction("Passive Gold");
                AddStubAction("Magic");
                AddStubAction("Hire Hero");
                return;
            }

            if (MatchInspectorFormatting.IsBarracksBuilding(buildingId))
            {
                AddStubAction("Upgrade L2");
                AddStubAction("Upgrade L3");
                AddStubAction("Upgrade L4");
                AddStubAction("Stat Research");
                AddStubAction("Deploy Hero");
                return;
            }

            if (MatchInspectorFormatting.IsTowerBuilding(buildingId))
            {
                AddStubAction("Target Mode");
                AddStubAction("Tower Upgrade");
            }
        }

        void AddStubAction(string label)
        {
            var button = new Button(() => Debug.Log($"[MatchInspector stub] {label}"))
            {
                text = label,
            };
            button.AddToClassList("fantasy-btn");
            button.AddToClassList("fantasy-btn--secondary");
            button.AddToClassList("match-inspector__action-btn");
            _actions.Add(button);
            _actionButtons.Add(button);
        }

        void ClearActions()
        {
            foreach (var button in _actionButtons)
            {
                button.RemoveFromHierarchy();
            }

            _actionButtons.Clear();
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
