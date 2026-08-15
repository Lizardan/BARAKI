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
        /// <summary>3-col grid: upgrade/titan, calls, heroes (see <see cref="MatchBarracksCallSlotRules"/>).</summary>
        const string EmptyOverlayHiddenClass = "match-panel__empty-overlay--hidden";
        const string InspectorEmptyClass = "match-inspector--empty";
        const string PanelActiveClass = "match-dock-panel__inner--active";
        const string CommandGridHiddenClass = "match-command-grid--hidden";
        const string UnitInfoHiddenClass = "match-unit-info--hidden";
        const string TooltipHiddenClass = "match-command-tooltip--hidden";
        const string ExtraAbilityMenuHiddenClass = "match-extra-ability--hidden";
        const string ExtraAbilityLockedClass = "match-extra-ability__btn--locked";
        const string ExtraAbilityPickedClass = "match-extra-ability__btn--picked";

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
        VisualElement _extraAbilityMenu;
        Button _extraAbilityCloseButton;
        readonly Button[] _extraAbilityButtons = new Button[MainExtraAbilityRules.AbilityCount];
        readonly Button[] _commandSlots = new Button[CommandSlotCount];
        readonly Action[] _commandActions = new Action[CommandSlotCount];
        readonly string[] _commandTooltips = new string[CommandSlotCount];
        readonly MatchCommandRegenFrameElement[] _callRegenFrames = new MatchCommandRegenFrameElement[CommandSlotCount];
        readonly VisualElement[] _commandPortraits = new VisualElement[CommandSlotCount];
        readonly Label[] _chargeBadges = new Label[CommandSlotCount];
        readonly UnitRole?[] _callSlotRoles = new UnitRole?[CommandSlotCount];
        readonly int?[] _heroDeploySlots = new int?[CommandSlotCount];
        readonly bool[] _titanDeploySlots = new bool[CommandSlotCount];
        int _hoveredCommandIndex = -1;
        string _commandsFingerprint;
        UnitVisualCatalog _visualCatalog;
        MatchCombatPresenter _combatPresenter;

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
            BindExtraAbilityMenu(root);

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
                    EnsureCommandPortrait(button, index);
                    EnsureCallRegenFrame(button, index);
                    EnsureChargeBadge(button, index);
                }
            }
        }

        void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            _localPlayerSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            ResolveVisualCatalog();
            SubscribeSelection();
            if (_extraAbilityCloseButton != null)
            {
                _extraAbilityCloseButton.clicked += CloseExtraAbilityMenu;
            }
        }

        void OnDisable()
        {
            UnsubscribeSelection();
            if (_extraAbilityCloseButton != null)
            {
                _extraAbilityCloseButton.clicked -= CloseExtraAbilityMenu;
            }

            CloseExtraAbilityMenu();
        }

        void BindExtraAbilityMenu(VisualElement root)
        {
            _extraAbilityMenu = root.Q<VisualElement>("ExtraAbilityMenu");
            _extraAbilityCloseButton = root.Q<Button>("ExtraAbilityCloseButton");
            for (var i = 0; i < MainExtraAbilityRules.AbilityCount; i++)
            {
                var abilityId = i + 1;
                var button = root.Q<Button>($"ExtraAbilitySlot{abilityId}");
                _extraAbilityButtons[i] = button;
                if (button != null)
                {
                    button.clicked += () => PickMainExtraAbility(abilityId);
                }
            }

            CloseExtraAbilityMenu();
        }

        void LateUpdate()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            _localPlayerSlot = (GameSession.ActiveSetup ?? MatchSetup.Default).LocalPlayerSlot;
            ResolveVisualCatalog();
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
                CloseExtraAbilityMenu();
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
            var heroCdKey = 0;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            if (roster != null)
            {
                for (var slot = 1; slot <= HeroRules.MaxHeroSlots; slot++)
                {
                    var hero = roster.Get(slot);
                    heroKey = (heroKey * 10) + (int)hero.State;
                    var cd = MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId)
                        ? MatchUpgradeLabelRules.CeilRemainingSeconds(
                            hero.GetDeathCooldown(building.InstanceId))
                        : 0;
                    heroCdKey = (heroCdKey * 1000) + cd;
                }
            }

            var titanKey = 0;
            var titanCd = 0;
            var titan = controller?.GetTitanState(_localPlayerSlot);
            if (titan != null)
            {
                titanKey = (int)titan.State;
                if (MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId))
                {
                    titanCd = MatchUpgradeLabelRules.CeilRemainingSeconds(
                        titan.GetDeathCooldown(building.InstanceId));
                }
            }

            return $"{building.InstanceId}:{gold}:{passive}:{mainLevel}:{queueCount}:{barracksLevel}:{heroKey}:{heroCdKey}:{titanKey}:{titanCd}:{chargesKey}:{player?.MeleeDamageLevel ?? 0}:{player?.RangedDamageLevel ?? 0}:{player?.HpArmorLevel ?? 0}:{player?.MagicLevel ?? 0}:{player?.DivineBlessingComplete}:{player?.MainExtraAbilityId ?? 0}";
        }

        void PopulateBuildingCommands(BuildingState building)
        {
            var controller = _matchRuntime?.Controller;
            var player = FindLocalPlayer(controller);
            var queueFull = controller != null
                && !controller.Research.HasSpace(building.InstanceId);

            if (MatchInspectorFormatting.IsMainBuilding(building.BuildingId))
            {
                var queuedMainLevel = controller?.Research.CountUpgrade(
                    building.InstanceId,
                    GameIds.Upgrades.MainBuildingLevel) ?? 0;
                var projectedMainLevel = (player?.MainLevel ?? 0) + queuedMainLevel;
                var hasMainStep = MatchEconomyRules.TryGetMainLevelUpgrade(
                    projectedMainLevel,
                    out var mainCost,
                    out var mainDuration);
                var canMain = hasMainStep
                    && !queueFull
                    && player != null
                    && player.Gold >= mainCost;
                var nextMainLevel = MatchUpgradeLabelRules.GetNextLevel(
                    player?.MainLevel ?? 0,
                    queuedMainLevel);
                SetCommand(
                    0,
                    hasMainStep
                        ? MatchUpgradeLabelRules.FormatMainLevelButton(nextMainLevel, mainCost)
                        : "Макс. ур.",
                    canMain,
                    () => StartResearch(GameIds.Upgrades.MainBuildingLevel),
                    hasMainStep
                        ? MatchUpgradeLabelRules.FormatMainLevelTooltip(nextMainLevel, mainCost, mainDuration)
                        : "Главное здание максимального уровня");

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

                PopulateStatTrackCommand(controller, player, building, queueFull, 2, GameIds.Upgrades.MeleeDamage);
                PopulateStatTrackCommand(controller, player, building, queueFull, 3, GameIds.Upgrades.RangedDamage);
                PopulateStatTrackCommand(controller, player, building, queueFull, 4, GameIds.Upgrades.Armor);

                var queuedMagic = controller?.Research.CountUpgrade(
                    building.InstanceId,
                    GameIds.Upgrades.MainMagic) ?? 0;
                var projectedMagicLevel = (player?.MagicLevel ?? 0) + queuedMagic;
                var magicCost = 0;
                var magicDuration = 0f;
                var hasMagicStep = player != null
                    && MatchEconomyRules.CanPurchaseMagic(projectedMagicLevel, player.MainLevel)
                    && MatchEconomyRules.TryGetMagicUpgrade(projectedMagicLevel, out magicCost, out magicDuration);
                var canMagic = hasMagicStep && !queueFull && player != null && player.Gold >= magicCost;
                var nextMagicLevel = MatchUpgradeLabelRules.GetNextLevel(
                    player?.MagicLevel ?? 0,
                    queuedMagic);
                SetCommand(
                    5,
                    hasMagicStep
                        ? MatchUpgradeLabelRules.FormatMagicButton(nextMagicLevel, magicCost)
                        : "Макс. магия",
                    canMagic,
                    () => StartResearch(GameIds.Upgrades.MainMagic),
                    hasMagicStep
                        ? MatchUpgradeLabelRules.FormatMagicTooltip(nextMagicLevel, magicCost, magicDuration)
                        : "Магия — требуется уровень главного здания");

                PopulateHeroHireCommands(player, building, queueFull);
                PopulateDivineBlessingCommand(player, building, queueFull);
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
                    MatchBarracksCallSlotRules.BarracksUpgradeSlot,
                    hasUpgradeStep
                        ? MatchUpgradeLabelRules.FormatBarracksLevelButton(nextLevel, cost)
                        : "Макс. ур.",
                    canUpgrade,
                    () => StartResearch(GameIds.Upgrades.BarracksLevel),
                    hasUpgradeStep
                        ? MatchUpgradeLabelRules.FormatBarracksLevelTooltip(nextLevel, cost, duration)
                        : "Казармы уже максимального уровня");

                PopulateHeroDeployCommands(player);
                PopulateTitanCommand();

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

        void PopulateStatTrackCommand(
            MatchController controller,
            MatchPlayerState player,
            BuildingState building,
            bool queueFull,
            int slot,
            string trackId)
        {
            var currentLevel = GetPlayerStatLevel(player, trackId);
            var queued = controller?.Research.CountUpgrade(building.InstanceId, trackId) ?? 0;
            var nextLevel = MatchUpgradeLabelRules.GetNextLevel(currentLevel, queued);
            var cost = 0;
            var duration = 0f;
            var hasStep = player != null
                && MatchEconomyRules.TryGetStatTrackUpgrade(
                    trackId,
                    currentLevel + queued,
                    out cost,
                    out duration);
            var canBuy = hasStep
                && !queueFull
                && player != null
                && MatchEconomyRules.CanPurchaseStatTrack(trackId, currentLevel + queued, player.MainLevel)
                && player.Gold >= cost;
            SetCommand(
                slot,
                hasStep
                    ? MatchUpgradeLabelRules.FormatStatTrackButton(trackId, nextLevel, cost)
                    : $"Макс. {MatchUpgradeLabelRules.GetStatTrackTitle(trackId)}",
                canBuy,
                () => StartResearch(trackId),
                hasStep
                    ? MatchUpgradeLabelRules.FormatStatTrackTooltip(trackId, nextLevel, cost, duration)
                    : $"{MatchUpgradeLabelRules.GetStatTrackTitle(trackId)} — максимальный уровень");
        }

        static int GetPlayerStatLevel(MatchPlayerState player, string trackId)
        {
            if (player == null)
            {
                return 0;
            }

            if (trackId == GameIds.Upgrades.MeleeDamage)
            {
                return player.MeleeDamageLevel;
            }

            if (trackId == GameIds.Upgrades.RangedDamage)
            {
                return player.RangedDamageLevel;
            }

            if (trackId == GameIds.Upgrades.Armor)
            {
                return player.HpArmorLevel;
            }

            return 0;
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
                    $"{roleLabel}: {goldCost}g · заряд {charges}/{max}\nВосстановление заряда: {BarracksManualCallRules.RegenSeconds:0}с",
                    ResolveUnitPortrait(role),
                    charges);
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

        void EnsureChargeBadge(Button button, int index)
        {
            if (button == null || _chargeBadges[index] != null)
            {
                return;
            }

            var badge = new Label();
            badge.AddToClassList("match-command-charge");
            badge.pickingMode = PickingMode.Ignore;
            badge.style.display = DisplayStyle.None;
            button.Add(badge);
            _chargeBadges[index] = badge;
        }

        void ApplyChargeBadge(int index, int? charges)
        {
            var badge = _chargeBadges[index];
            if (badge == null)
            {
                return;
            }

            if (!charges.HasValue)
            {
                badge.text = string.Empty;
                badge.style.display = DisplayStyle.None;
                return;
            }

            badge.text = charges.Value.ToString();
            badge.style.display = DisplayStyle.Flex;
        }

        void EnsureCommandPortrait(Button button, int index)
        {
            if (button == null || _commandPortraits[index] != null)
            {
                return;
            }

            var portrait = new VisualElement();
            portrait.AddToClassList("match-command-portrait");
            portrait.pickingMode = PickingMode.Ignore;
            portrait.style.display = DisplayStyle.None;
            button.Insert(0, portrait);
            _commandPortraits[index] = portrait;
        }

        void ApplyCommandPortrait(int index, Texture2D portrait)
        {
            var element = _commandPortraits[index];
            if (element == null)
            {
                return;
            }

            if (portrait == null)
            {
                element.style.backgroundImage = new StyleBackground();
                element.style.display = DisplayStyle.None;
                return;
            }

            element.style.backgroundImage = new StyleBackground(portrait);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top);
            element.style.display = DisplayStyle.Flex;
        }

        void ResolveVisualCatalog()
        {
            if (_visualCatalog != null)
            {
                return;
            }

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller != null && controller.UnitVisualCatalog != null)
            {
                _visualCatalog = controller.UnitVisualCatalog;
                return;
            }

            if (_combatPresenter == null)
            {
                _combatPresenter = MatchCombatPresenter.Current;
            }

            _visualCatalog = _combatPresenter != null ? _combatPresenter.VisualCatalog : null;
        }

        Texture2D ResolveUnitPortrait(UnitRole role, int heroSlot = 0)
        {
            ResolveVisualCatalog();
            var player = FindLocalPlayer(_matchRuntime?.Controller);
            var raceId = player != null ? player.RaceId : GameIds.Races.Human;
            if (_visualCatalog != null
                && _visualCatalog.TryGetPortrait(raceId, role, heroSlot, out var portrait)
                && portrait != null)
            {
                return portrait;
            }

            return null;
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
                if (barracks != null && role.HasValue)
                {
                    UpdateManualCallRegenFrame(i, frame, barracks, role.Value);
                    continue;
                }

                if (TryUpdateChampionDeployRegenFrame(i, frame, controller))
                {
                    continue;
                }

                frame.SetRegenerating(false, 0f);
            }
        }

        void UpdateManualCallRegenFrame(
            int index,
            MatchCommandRegenFrameElement frame,
            BarracksWaveState barracks,
            UnitRole role)
        {
            if (!barracks.CallCharges.TryGetNextRegenRemaining(role, out var remaining))
            {
                frame.SetRegenerating(false, 0f);
                ApplyChargeBadge(index, barracks.CallCharges.GetCharges(role));
                return;
            }

            var fill = MatchCommandRegenFrameRules.GetFill01(
                remaining,
                BarracksManualCallRules.RegenSeconds);
            frame.SetRegenerating(true, fill);

            if (_commandTooltips[index] == null)
            {
                return;
            }

            var roleLabel = FormatCallRole(role);
            var charges = barracks.CallCharges.GetCharges(role);
            var max = barracks.CallCharges.GetMaxCharges(role);
            var goldCost = BarracksManualCallRules.GetGoldCost(role);
            _commandTooltips[index] =
                $"{roleLabel}: {goldCost}g · заряд {charges}/{max}\n" +
                $"Заряд через: {remaining:0}с / {BarracksManualCallRules.RegenSeconds:0}с";
            ApplyChargeBadge(index, charges);
            if (_hoveredCommandIndex == index)
            {
                ShowTooltip(index);
            }
        }

        bool TryUpdateChampionDeployRegenFrame(
            int index,
            MatchCommandRegenFrameElement frame,
            MatchController controller)
        {
            if (controller == null || _selectedBuildingInstanceId < 0)
            {
                return false;
            }

            float remaining;
            float duration;
            if (_heroDeploySlots[index].HasValue)
            {
                var hero = controller.GetHeroRoster(_localPlayerSlot)?.Get(_heroDeploySlots[index].Value);
                if (hero == null)
                {
                    return false;
                }

                remaining = hero.GetDeathCooldown(_selectedBuildingInstanceId);
                duration = HeroRules.DeathCooldownSeconds;
                if (remaining > 0f)
                {
                    var seconds = MatchUpgradeLabelRules.CeilRemainingSeconds(remaining);
                    var heroName = MatchInspectorFormatting.FormatHeroName(_heroDeploySlots[index].Value);
                    _commandTooltips[index] =
                        MatchUpgradeLabelRules.FormatHeroDeployCooldownTooltip(heroName, seconds);
                    if (_hoveredCommandIndex == index)
                    {
                        ShowTooltip(index);
                    }
                }
            }
            else if (_titanDeploySlots[index])
            {
                var titan = controller.GetTitanState(_localPlayerSlot);
                if (titan == null)
                {
                    return false;
                }

                remaining = titan.GetDeathCooldown(_selectedBuildingInstanceId);
                duration = TitanRules.DeathCooldownSeconds;
                if (remaining > 0f)
                {
                    var seconds = MatchUpgradeLabelRules.CeilRemainingSeconds(remaining);
                    _commandTooltips[index] =
                        MatchUpgradeLabelRules.FormatTitanDeployCooldownTooltip(seconds);
                    if (_hoveredCommandIndex == index)
                    {
                        ShowTooltip(index);
                    }
                }
            }
            else
            {
                return false;
            }

            if (remaining <= 0f)
            {
                frame.SetRegenerating(false, 0f);
                return true;
            }

            frame.SetRegenerating(
                true,
                MatchCommandRegenFrameRules.GetFill01(remaining, duration));
            return true;
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

        void PopulateHeroHireCommands(MatchPlayerState player, BuildingState building, bool queueFull)
        {
            var controller = _matchRuntime?.Controller;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            if (roster == null || player == null)
            {
                return;
            }

            for (var heroSlot = 1; heroSlot <= HeroRules.MaxHeroSlots; heroSlot++)
            {
                if (!MatchMainHireSlotRules.TryGetHeroHireSlot(heroSlot, out var commandIndex))
                {
                    continue;
                }

                var state = roster.Get(heroSlot);
                if (!HeroRules.ShouldShowHire(state.State, heroSlot, player.MainLevel))
                {
                    continue;
                }

                var hireId = HeroRules.BuildHireUpgradeId(heroSlot);
                var queued = controller.Research.CountUpgrade(building.InstanceId, hireId) > 0;
                var canHire = !queued
                    && !queueFull
                    && HeroRules.CanHire(state.State, heroSlot, player.MainLevel, player.Gold);
                var capturedSlot = heroSlot;
                SetCommand(
                    commandIndex,
                    MatchUpgradeLabelRules.FormatHeroHireButton(heroSlot, HeroRules.HireGold),
                    canHire,
                    () => HireHero(capturedSlot),
                    MatchUpgradeLabelRules.FormatHeroHireTooltip(
                        heroSlot,
                        HeroRules.HireGold,
                        HeroRules.HireResearchSeconds),
                    ResolveUnitPortrait(UnitRole.Hero, heroSlot));
            }
        }

        void HireHero(int heroSlot)
        {
            if (!HeroRules.IsValidHeroSlot(heroSlot))
            {
                return;
            }

            StartResearch(HeroRules.BuildHireUpgradeId(heroSlot));
        }

        void PopulateDivineBlessingCommand(MatchPlayerState player, BuildingState building, bool queueFull)
        {
            var slot = MainExtraAbilityRules.CommandSlotIndex;
            if (player == null)
            {
                return;
            }

            if (player.MainExtraAbilityId != MainExtraAbilityRules.None)
            {
                SetCommand(
                    slot,
                    MatchUpgradeLabelRules.FormatExtraAbilityPickedButton(player.MainExtraAbilityId),
                    enabled: false,
                    action: null,
                    MatchUpgradeLabelRules.FormatExtraAbilityPickedTooltip(player.MainExtraAbilityId));
                return;
            }

            if (player.DivineBlessingComplete)
            {
                SetCommand(
                    slot,
                    MatchUpgradeLabelRules.FormatExtraAbilityMenuButton(),
                    enabled: true,
                    OpenExtraAbilityMenu,
                    MatchUpgradeLabelRules.FormatExtraAbilityMenuTooltip());
                return;
            }

            var controller = _matchRuntime?.Controller;
            var queued = controller?.Research.CountUpgrade(
                building.InstanceId,
                GameIds.Upgrades.DivineBlessing) ?? 0;
            var canStart = queued == 0
                && !queueFull
                && MatchEconomyRules.TryGetDivineBlessingUpgrade(
                    player.MainLevel,
                    player.DivineBlessingComplete,
                    out var cost,
                    out var duration)
                && player.Gold >= cost;

            SetCommand(
                slot,
                MatchUpgradeLabelRules.FormatDivineBlessingButton(
                    MatchEconomyRules.DivineBlessingCost),
                canStart,
                () => StartResearch(GameIds.Upgrades.DivineBlessing),
                MatchUpgradeLabelRules.FormatDivineBlessingTooltip(
                    MatchEconomyRules.DivineBlessingCost,
                    MatchEconomyRules.DivineBlessingSeconds));
        }

        void OpenExtraAbilityMenu()
        {
            if (_extraAbilityMenu == null)
            {
                return;
            }

            RefreshExtraAbilityMenu();
            _extraAbilityMenu.RemoveFromClassList(ExtraAbilityMenuHiddenClass);
            _extraAbilityMenu.BringToFront();
        }

        void CloseExtraAbilityMenu()
        {
            _extraAbilityMenu?.AddToClassList(ExtraAbilityMenuHiddenClass);
        }

        void RefreshExtraAbilityMenu()
        {
            var player = FindLocalPlayer(_matchRuntime?.Controller);
            for (var i = 0; i < MainExtraAbilityRules.AbilityCount; i++)
            {
                var abilityId = i + 1;
                var button = _extraAbilityButtons[i];
                if (button == null)
                {
                    continue;
                }

                var unlocked = player != null && MainExtraAbilityRules.IsUnlocked(abilityId, player);
                var picked = player != null && player.MainExtraAbilityId == abilityId;
                button.text =
                    $"{MainExtraAbilityRules.GetDisplayName(abilityId)}\n{MainExtraAbilityRules.GetGateDescription(abilityId)}";
                button.SetEnabled(unlocked && player != null && MainExtraAbilityRules.CanPick(player, abilityId));
                button.EnableInClassList(ExtraAbilityLockedClass, !unlocked);
                button.EnableInClassList(ExtraAbilityPickedClass, picked);
            }
        }

        void PickMainExtraAbility(int abilityId)
        {
            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestPickMainExtraAbility(abilityId);
            }
            else
            {
                _matchRuntime?.Controller?.TryPickMainExtraAbility(_localPlayerSlot, abilityId);
            }

            CloseExtraAbilityMenu();
        }

        void PopulateHeroDeployCommands(MatchPlayerState player)
        {
            var controller = _matchRuntime?.Controller;
            var roster = controller?.GetHeroRoster(_localPlayerSlot);
            if (roster == null || _selectedBuildingInstanceId < 0)
            {
                return;
            }

            for (var heroSlot = 1; heroSlot <= HeroRules.MaxHeroSlots; heroSlot++)
            {
                if (!MatchBarracksCallSlotRules.TryGetHeroDeploySlot(heroSlot, out var commandIndex))
                {
                    continue;
                }

                var state = roster.Get(heroSlot);
                if (!HeroRules.ShouldShowDeploy(state.State))
                {
                    continue;
                }

                var cooldown = state.GetDeathCooldown(_selectedBuildingInstanceId);
                var cooldownSeconds = MatchUpgradeLabelRules.CeilRemainingSeconds(cooldown);
                var canDeploy = player != null
                    && HeroRules.CanDeploy(
                        state.State,
                        cooldown,
                        player.Gold,
                        barracksIntact: true);
                var heroName = MatchInspectorFormatting.FormatHeroName(heroSlot);
                var capturedSlot = heroSlot;
                if (cooldownSeconds > 0)
                {
                    SetCommand(
                        commandIndex,
                        MatchUpgradeLabelRules.FormatHeroDeployCooldownButton(heroSlot, cooldownSeconds),
                        enabled: false,
                        () => DeployHero(capturedSlot),
                        MatchUpgradeLabelRules.FormatHeroDeployCooldownTooltip(heroName, cooldownSeconds),
                        ResolveUnitPortrait(UnitRole.Hero, heroSlot));
                }
                else
                {
                    SetCommand(
                        commandIndex,
                        MatchUpgradeLabelRules.FormatHeroDeployButton(heroSlot, HeroRules.DeployGold),
                        canDeploy,
                        () => DeployHero(capturedSlot),
                        MatchUpgradeLabelRules.FormatHeroDeployTooltip(heroName, HeroRules.DeployGold),
                        ResolveUnitPortrait(UnitRole.Hero, heroSlot));
                }

                _heroDeploySlots[commandIndex] = heroSlot;
            }
        }

        void DeployHero(int heroSlot)
        {
            var controller = _matchRuntime?.Controller;
            if (controller == null || _selectedBuildingInstanceId < 0)
            {
                return;
            }

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestDeployHero(_selectedBuildingInstanceId, heroSlot);
            }
            else
            {
                controller.TryDeployHero(_localPlayerSlot, _selectedBuildingInstanceId, heroSlot);
            }
        }

        void PopulateTitanCommand()
        {
            var controller = _matchRuntime?.Controller;
            var titan = controller?.GetTitanState(_localPlayerSlot);
            if (titan == null
                || !TitanRules.ShouldShowDeploy(titan.State)
                || !MatchBarracksCallSlotRules.TryGetTitanDeploySlot(out var commandIndex))
            {
                return;
            }

            var cooldown = titan.GetDeathCooldown(_selectedBuildingInstanceId);
            var cooldownSeconds = MatchUpgradeLabelRules.CeilRemainingSeconds(cooldown);
            if (cooldownSeconds > 0)
            {
                SetCommand(
                    commandIndex,
                    MatchUpgradeLabelRules.FormatTitanDeployCooldownButton(cooldownSeconds),
                    enabled: false,
                    DeployTitan,
                    MatchUpgradeLabelRules.FormatTitanDeployCooldownTooltip(cooldownSeconds),
                    ResolveUnitPortrait(UnitRole.Titan));
            }
            else
            {
                SetCommand(
                    commandIndex,
                    MatchUpgradeLabelRules.FormatTitanDeployButton(TitanRules.DeployGold),
                    CanDeployTitan(),
                    DeployTitan,
                    MatchUpgradeLabelRules.FormatTitanDeployTooltip(TitanRules.DeployGold),
                    ResolveUnitPortrait(UnitRole.Titan));
            }

            _titanDeploySlots[commandIndex] = true;
        }

        bool CanDeployTitan()
        {
            var controller = _matchRuntime?.Controller;
            var titan = controller?.GetTitanState(_localPlayerSlot);
            var player = FindLocalPlayer(controller);
            if (titan == null || player == null || _selectedBuildingInstanceId < 0)
            {
                return false;
            }

            var building = controller.Buildings.GetByInstanceId(_selectedBuildingInstanceId);
            if (building == null || !building.IsIntact || building.OwnerSlot != _localPlayerSlot)
            {
                return false;
            }

            return TitanRules.CanDeploy(
                titan.State,
                titan.GetDeathCooldown(_selectedBuildingInstanceId),
                player.Gold,
                barracksIntact: true);
        }

        void DeployTitan()
        {
            var controller = _matchRuntime?.Controller;
            if (controller == null || _selectedBuildingInstanceId < 0)
            {
                return;
            }

            if (MatchNetworkCommands.IsAvailable)
            {
                MatchNetworkCommands.RequestDeployTitan(_selectedBuildingInstanceId);
            }
            else
            {
                controller.TryDeployTitan(_localPlayerSlot, _selectedBuildingInstanceId);
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

        void SetCommand(
            int index,
            string label,
            bool enabled,
            Action action,
            string tooltip = null,
            Texture2D portrait = null,
            int? charges = null)
        {
            if (index < 0 || index >= CommandSlotCount || _commandSlots[index] == null)
            {
                return;
            }

            var hasPortrait = portrait != null;
            _commandSlots[index].text = hasPortrait ? string.Empty : (label ?? string.Empty);
            _commandSlots[index].SetEnabled(enabled && action != null);
            _commandActions[index] = action;
            _commandTooltips[index] = string.IsNullOrEmpty(tooltip) ? (label ?? string.Empty) : tooltip;
            ApplyCommandPortrait(index, portrait);
            ApplyChargeBadge(index, charges);
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
                _heroDeploySlots[i] = null;
                _titanDeploySlots[i] = false;
                _callRegenFrames[i]?.SetRegenerating(false, 0f);
                ApplyCommandPortrait(i, null);
                ApplyChargeBadge(i, null);
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
