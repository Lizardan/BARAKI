using System;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchInspectorController : MonoBehaviour
    {
        const int CommandSlotCount = 12;
        /// <summary>3-col grid: rows 0–1 upgrades; rows 2–3 manual calls (see <see cref="MatchBarracksCallSlotRules"/>).</summary>
        const string EmptyOverlayHiddenClass = "match-panel__empty-overlay--hidden";
        const string InspectorEmptyClass = "match-inspector--empty";
        const string PanelActiveClass = "match-dock-panel__inner--active";
        const string CommandGridHiddenClass = "match-command-grid--hidden";
        const string UnitInfoHiddenClass = "match-unit-info--hidden";
        const string TooltipHiddenClass = "match-command-tooltip--hidden";

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
        VisualElement _commandTooltip;
        Label _commandTooltipLabel;
        VisualElement _hudRoot;
        readonly Button[] _commandSlots = new Button[CommandSlotCount];
        readonly Action[] _commandActions = new Action[CommandSlotCount];
        readonly string[] _commandTooltips = new string[CommandSlotCount];
        readonly MatchCommandRegenFrameElement[] _callRegenFrames = new MatchCommandRegenFrameElement[CommandSlotCount];
        readonly UnitRole?[] _callSlotRoles = new UnitRole?[CommandSlotCount];
        int _hoveredCommandIndex = -1;
        string _commandsFingerprint;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _hudRoot = root.Q<VisualElement>("MatchHudRoot") ?? root;
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
            _commandTooltip = root.Q<VisualElement>("CommandTooltip");
            _commandTooltipLabel = root.Q<Label>("CommandTooltipLabel");

            for (var i = 0; i < CommandSlotCount; i++)
            {
                var index = i;
                var button = root.Q<Button>($"CommandSlot{i}");
                _commandSlots[i] = button;
                if (button != null)
                {
                    button.clicked += () => OnCommandClicked(index);
                    button.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(index));
                    button.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
                    EnsureCallRegenFrame(button, index);
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
            UpdateCallRegenFrames();
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

        void OnSelectionChanged(MatchPickTarget target) => Refresh(target, forceCommands: true);

        void RefreshLiveStats()
        {
            if (_selection == null || !_selection.Current.HasTarget)
            {
                return;
            }

            Refresh(_selection.Current, forceCommands: false);
        }

        void Refresh(MatchPickTarget target, bool forceCommands)
        {
            if (!target.HasTarget)
            {
                ClearCommands();
                _selectedBuildingInstanceId = -1;
                _commandsFingerprint = null;
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
                if (forceCommands || _selectedBuildingInstanceId != -1)
                {
                    ClearCommands();
                    _selectedBuildingInstanceId = -1;
                    _commandsFingerprint = null;
                }

                ShowCommandGrid(false);
                ShowUnitInfo(true);
                RefreshUnit(target.EntityId);
                return;
            }

            RefreshBuilding(target.EntityId, forceCommands);
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

        void RefreshBuilding(int instanceId, bool forceCommands)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            var building = controller?.Buildings.GetByInstanceId(instanceId);
            if (building == null)
            {
                if (forceCommands)
                {
                    ClearCommands();
                    _commandsFingerprint = null;
                }

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
                MaybeRepopulateCommands(building, forceCommands);
                return;
            }

            if (forceCommands || _selectedBuildingInstanceId != -1)
            {
                ClearCommands();
                _selectedBuildingInstanceId = -1;
                _commandsFingerprint = null;
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

        void MaybeRepopulateCommands(BuildingState building, bool forceCommands)
        {
            var fingerprint = BuildCommandsFingerprint(building);
            if (!forceCommands && fingerprint == _commandsFingerprint)
            {
                return;
            }

            var keepHover = _hoveredCommandIndex;
            ClearCommands(hideTooltip: false);
            PopulateBuildingCommands(building);
            _commandsFingerprint = fingerprint;
            if (keepHover >= 0)
            {
                ShowTooltip(keepHover);
            }
        }

        string BuildCommandsFingerprint(BuildingState building)
        {
            var controller = _matchRuntime?.Controller;
            var player = FindLocalPlayer(controller);
            var gold = player?.Gold ?? 0;
            var passive = player?.PassiveGoldLevel ?? 0;
            var mainLevel = player?.MainLevel ?? 0;
            var queueCount = controller?.Research.GetCount(building.InstanceId) ?? 0;
            var barracksLevel = 0;
            var chargesKey = 0;
            if (controller != null && MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId))
            {
                var barracks = controller.WaveScheduler
                    .GetBarracks(building.OwnerSlot, building.BuildingId);
                barracksLevel = barracks?.Level ?? 0;
                if (barracks != null)
                {
                    chargesKey =
                        barracks.CallCharges.GetCharges(UnitRole.Melee) * 100000
                        + barracks.CallCharges.GetCharges(UnitRole.Ranged) * 10000
                        + barracks.CallCharges.GetCharges(UnitRole.Caster) * 1000
                        + barracks.CallCharges.GetCharges(UnitRole.Siege) * 100
                        + barracks.CallCharges.GetCharges(UnitRole.Flying) * 10
                        + barracks.CallCharges.GetCharges(UnitRole.Super);
                }
            }

            var heroKey = 0;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            if (roster != null)
            {
                for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
                {
                    heroKey = (heroKey * 10) + (int)roster.Get(slot).State;
                }
            }

            return $"{building.InstanceId}:{gold}:{passive}:{mainLevel}:{queueCount}:{barracksLevel}:{heroKey}:{chargesKey}";
        }

        void PopulateBuildingCommands(BuildingState building)
        {
            var controller = _matchRuntime?.Controller;
            var player = FindLocalPlayer(controller);
            var queueFull = controller != null
                && !controller.Research.HasSpace(building.InstanceId);

            if (MatchInspectorFormatting.IsMainBuilding(building.BuildingId))
            {
                SetCommand(0, "Ур. здания", enabled: false, null, "Уровень главного здания (скоро).");

                var queuedPassive = controller?.Research.CountUpgrade(
                    building.InstanceId,
                    GameIds.Upgrades.MainPassiveGold) ?? 0;
                var nextPassive = MatchUpgradeLabelRules.GetNextLevel(
                    player?.PassiveGoldLevel ?? 0,
                    queuedPassive);
                var canPassive = player != null
                    && !queueFull
                    && MatchEconomyRules.CanPurchasePassiveGold(player.PassiveGoldLevel + queuedPassive, player.MainLevel)
                    && player.Gold >= MatchEconomyRules.PassiveGoldUpgradeCost;
                SetCommand(
                    1,
                    MatchUpgradeLabelRules.FormatPassiveGoldButton(
                        nextPassive,
                        MatchEconomyRules.PassiveGoldUpgradeCost),
                    canPassive,
                    () => StartResearch(GameIds.Upgrades.MainPassiveGold),
                    MatchUpgradeLabelRules.FormatPassiveGoldTooltip(
                        nextPassive,
                        MatchEconomyRules.PassiveGoldUpgradeCost,
                        MatchEconomyRules.PassiveGoldUpgradeSeconds,
                        MatchEconomyRules.PassiveGoldTickIntervalSeconds));

                SetCommand(2, "Magic", enabled: false, null, "Расовая магия (скоро).");
                var nextSlot = PeekNextHireSlot();
                var canHire = nextSlot > 0 && !queueFull && CanHireNextHero();
                SetCommand(
                    3,
                    nextSlot > 0
                        ? MatchUpgradeLabelRules.FormatHeroHireButton(nextSlot, HeroRules.HireGold)
                        : $"Герой\n{HeroRules.HireGold}g",
                    canHire,
                    HireNextHero,
                    nextSlot > 0
                        ? MatchUpgradeLabelRules.FormatHeroHireTooltip(
                            nextSlot,
                            HeroRules.HireGold,
                            HeroRules.HireResearchSeconds)
                        : "Нет доступного слота героя");
                return;
            }

            if (MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId))
            {
                var barracks = controller?.WaveScheduler.GetBarracks(building.OwnerSlot, building.BuildingId);
                var queuedLevels = controller?.Research.CountUpgrade(
                    building.InstanceId,
                    GameIds.Upgrades.BarracksLevel) ?? 0;
                var projectedLevel = (barracks?.Level ?? 0) + queuedLevels;
                var cost = 0;
                var duration = 0f;
                var hasUpgradeStep = barracks != null
                    && MatchEconomyRules.TryGetBarracksLevelUpgrade(projectedLevel, out cost, out duration);
                var canUpgrade = hasUpgradeStep
                    && !queueFull
                    && player != null
                    && player.Gold >= cost;
                var nextLevel = MatchUpgradeLabelRules.GetNextLevel(barracks?.Level ?? 0, queuedLevels);
                SetCommand(
                    0,
                    hasUpgradeStep
                        ? MatchUpgradeLabelRules.FormatBarracksLevelButton(nextLevel, cost)
                        : "Макс. ур.",
                    canUpgrade,
                    () => StartResearch(GameIds.Upgrades.BarracksLevel),
                    hasUpgradeStep
                        ? MatchUpgradeLabelRules.FormatBarracksLevelTooltip(nextLevel, cost, duration)
                        : "Казармы уже максимального уровня");

                var canDeploy = CanDeployAnyHero();
                SetCommand(
                    1,
                    $"Герой\n{HeroRules.DeployGold}g",
                    canDeploy,
                    DeployReadyHero,
                    $"Выпуск героя в lane\n{HeroRules.DeployGold}g · мгновенно");

                if (barracks != null && building.IsIntact && !barracks.IsRuins)
                {
                    PopulateManualCallCommands(barracks, player);
                }

                return;
            }

            if (MatchInspectorFormatting.IsTowerBuilding(building.BuildingId))
            {
                SetCommand(0, "Апгрейд", enabled: false, null, "Расовые апгрейды башни (скоро).");
            }
        }

        void PopulateManualCallCommands(
            BarracksWaveState barracks,
            MatchPlayerState player)
        {
            var roles = new[]
            {
                UnitRole.Melee,
                UnitRole.Ranged,
                UnitRole.Caster,
                UnitRole.Siege,
                UnitRole.Flying,
                UnitRole.Super,
            };

            for (var i = 0; i < roles.Length; i++)
            {
                var role = roles[i];
                if (!MatchBarracksCallSlotRules.TryGetCommandSlot(role, out var slotIndex))
                {
                    continue;
                }

                var max = barracks.CallCharges.GetMaxCharges(role);
                if (max <= 0)
                {
                    continue;
                }

                var charges = barracks.CallCharges.GetCharges(role);
                var goldCost = BarracksManualCallRules.GetGoldCost(role);
                var canCall = player != null
                              && BarracksManualCallRules.CanCall(
                                  player.Gold >= goldCost,
                                  charges,
                                  barracksIntact: true,
                                  notEliminated: !player.IsEliminated);
                var roleLabel = FormatCallRole(role);
                var capturedRole = role;
                SetCommand(
                    slotIndex,
                    $"{roleLabel}\n{goldCost}g {charges}/{max}",
                    canCall,
                    () => RequestManualCall(capturedRole),
                    $"{roleLabel}: {goldCost}g · заряд {charges}/{max}\nВосстановление заряда: {BarracksManualCallRules.RegenSeconds:0}с");
                _callSlotRoles[slotIndex] = role;
            }
        }

        void EnsureCallRegenFrame(Button button, int index)
        {
            if (button == null || _callRegenFrames[index] != null)
            {
                return;
            }

            var frame = new MatchCommandRegenFrameElement();
            frame.style.display = DisplayStyle.None;
            button.Add(frame);
            _callRegenFrames[index] = frame;
        }

        void UpdateCallRegenFrames()
        {
            var controller = _matchRuntime?.Controller;
            BarracksWaveState barracks = null;
            if (controller != null && _selectedBuildingInstanceId >= 0)
            {
                var building = controller.Buildings.GetByInstanceId(_selectedBuildingInstanceId);
                if (building != null && MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId))
                {
                    barracks = controller.WaveScheduler.GetBarracks(building.OwnerSlot, building.BuildingId);
                }
            }

            for (var i = 0; i < CommandSlotCount; i++)
            {
                var frame = _callRegenFrames[i];
                if (frame == null)
                {
                    continue;
                }

                var role = _callSlotRoles[i];
                if (barracks == null || !role.HasValue)
                {
                    frame.SetRegenerating(false, 0f);
                    continue;
                }

                if (!barracks.CallCharges.TryGetNextRegenRemaining(role.Value, out var remaining))
                {
                    frame.SetRegenerating(false, 0f);
                    continue;
                }

                var fill = MatchCommandRegenFrameRules.GetFill01(
                    remaining,
                    BarracksManualCallRules.RegenSeconds);
                frame.SetRegenerating(true, fill);

                if (_commandTooltips[i] != null && _callSlotRoles[i].HasValue)
                {
                    var roleLabel = FormatCallRole(_callSlotRoles[i].Value);
                    var charges = barracks.CallCharges.GetCharges(_callSlotRoles[i].Value);
                    var max = barracks.CallCharges.GetMaxCharges(_callSlotRoles[i].Value);
                    var goldCost = BarracksManualCallRules.GetGoldCost(_callSlotRoles[i].Value);
                    _commandTooltips[i] =
                        $"{roleLabel}: {goldCost}g · заряд {charges}/{max}\n" +
                        $"Заряд через: {remaining:0}с / {BarracksManualCallRules.RegenSeconds:0}с";
                    if (_hoveredCommandIndex == i)
                    {
                        ShowTooltip(i);
                    }
                }
            }
        }

        static string FormatCallRole(UnitRole role) => role switch
        {
            UnitRole.Melee => "Ближ",
            UnitRole.Ranged => "Дальн",
            UnitRole.Caster => "Маг",
            UnitRole.Siege => "Осада",
            UnitRole.Flying => "Лёт",
            UnitRole.Super => "Супер",
            _ => role.ToString(),
        };

        void RequestManualCall(UnitRole role)
        {
            if (_selectedBuildingInstanceId < 0)
            {
                return;
            }

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestManualCall(_selectedBuildingInstanceId, role);
                return;
            }

            _matchRuntime?.Controller?.TryManualCallUnit(
                _localPlayerSlot,
                _selectedBuildingInstanceId,
                role);
        }

        void StartResearch(string upgradeId)
        {
            if (_selectedBuildingInstanceId < 0)
            {
                return;
            }

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestStartResearch(_selectedBuildingInstanceId, upgradeId);
                return;
            }

            _matchRuntime?.Controller?.TryStartResearch(
                _localPlayerSlot,
                _selectedBuildingInstanceId,
                upgradeId);
        }

        bool CanHireNextHero() => PeekNextHireSlot() > 0;

        void HireNextHero()
        {
            var slot = PeekNextHireSlot();
            if (slot <= 0)
            {
                return;
            }

            StartResearch(HeroRules.BuildHireUpgradeId(slot));
        }

        int PeekNextHireSlot()
        {
            var controller = _matchRuntime?.Controller;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            var player = FindLocalPlayer(controller);
            if (roster == null || player == null || _selectedBuildingInstanceId < 0)
            {
                return -1;
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                var hireId = HeroRules.BuildHireUpgradeId(slot);
                if (controller.Research.CountUpgrade(_selectedBuildingInstanceId, hireId) > 0)
                {
                    continue;
                }

                if (HeroRules.CanHire(roster.Get(slot).State, slot, player.MainLevel, player.Gold))
                {
                    return slot;
                }
            }

            return -1;
        }

        bool CanDeployAnyHero()
        {
            var controller = _matchRuntime?.Controller;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            var player = FindLocalPlayer(controller);
            if (roster == null || player == null)
            {
                return false;
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                if (HeroRules.CanDeploy(roster.Get(slot).State, roster.Get(slot).DeathCooldownRemaining, player.Gold, true))
                {
                    return true;
                }
            }

            return false;
        }

        void DeployReadyHero()
        {
            var controller = _matchRuntime?.Controller;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            var player = FindLocalPlayer(controller);
            if (roster == null || player == null || _selectedBuildingInstanceId < 0)
            {
                return;
            }

            for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
            {
                var state = roster.Get(slot);
                if (!HeroRules.CanDeploy(state.State, state.DeathCooldownRemaining, player.Gold, true))
                {
                    continue;
                }

                if (MatchNetworkCommands.IsAvailable)
                {
                    MatchNetworkCommands.RequestDeployHero(_selectedBuildingInstanceId, slot);
                }
                else
                {
                    controller.TryDeployHero(_localPlayerSlot, _selectedBuildingInstanceId, slot);
                }

                return;
            }
        }

        static MatchPlayerState FindLocalPlayer(MatchController controller)
        {
            if (controller == null)
            {
                return null;
            }

            var localSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            foreach (var player in controller.Players)
            {
                if (player.SlotIndex == localSlot)
                {
                    return player;
                }
            }

            return null;
        }

        void SetCommand(int index, string label, bool enabled, Action action, string tooltip = null)
        {
            if (index < 0 || index >= CommandSlotCount || _commandSlots[index] == null)
            {
                return;
            }

            _commandSlots[index].text = label;
            _commandSlots[index].SetEnabled(enabled && action != null);
            _commandActions[index] = action;
            _commandTooltips[index] = tooltip ?? string.Empty;
        }

        void ShowTooltip(int index)
        {
            if (_commandTooltip == null
                || _commandTooltipLabel == null
                || index < 0
                || index >= CommandSlotCount
                || string.IsNullOrEmpty(_commandTooltips[index])
                || _commandSlots[index] == null)
            {
                HideTooltip();
                return;
            }

            _hoveredCommandIndex = index;
            _commandTooltipLabel.text = _commandTooltips[index];
            _commandTooltip.RemoveFromClassList(TooltipHiddenClass);
            _commandTooltip.BringToFront();
            _commandTooltip.schedule.Execute(() => PositionTooltip(index));
        }

        void PositionTooltip(int index)
        {
            if (_commandTooltip == null || _commandSlots[index] == null)
            {
                return;
            }

            var buttonBound = _commandSlots[index].worldBound;
            var size = _commandTooltip.worldBound.size;
            if (size.x < 1f || size.y < 1f)
            {
                size = new Vector2(180f, 48f);
            }

            var panelWidth = _hudRoot != null ? _hudRoot.worldBound.width : 0f;
            var topLeft = MatchCommandTooltipRules.GetTooltipTopLeft(buttonBound, size, panelWidth);
            var rootBound = _hudRoot != null ? _hudRoot.worldBound : default;
            _commandTooltip.style.left = topLeft.x - rootBound.x;
            _commandTooltip.style.top = topLeft.y - rootBound.y;
        }

        void HideTooltip()
        {
            _hoveredCommandIndex = -1;
            _commandTooltip?.AddToClassList(TooltipHiddenClass);
            if (_commandTooltipLabel != null)
            {
                _commandTooltipLabel.text = string.Empty;
            }
        }

        void OnCommandClicked(int index)
        {
            _commandActions[index]?.Invoke();
        }

        void ClearCommands(bool hideTooltip = true)
        {
            if (hideTooltip)
            {
                HideTooltip();
            }

            for (var i = 0; i < CommandSlotCount; i++)
            {
                _commandActions[i] = null;
                _commandTooltips[i] = string.Empty;
                _callSlotRoles[i] = null;
                _callRegenFrames[i]?.SetRegenerating(false, 0f);
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
