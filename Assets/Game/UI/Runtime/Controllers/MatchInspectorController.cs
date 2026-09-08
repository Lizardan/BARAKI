using System;
using System.Collections.Generic;
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
        const string TargetingTooltipHiddenClass = "match-targeting-tooltip--hidden";
        const string CommandSlotLockedClass = "match-command-grid__slot--locked";
        const string UnitAbilityBarName = "UnitAbilityBar";
        const string UnitAbilityBarHiddenClass = "match-unit-abilities--hidden";
        static readonly Color LockedAbilityColor = new Color(0.32f, 0.33f, 0.30f, 1f);
        const float TargetingTooltipOffsetX = 18f;
        const float TargetingTooltipOffsetY = 18f;

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
        VisualElement _targetingTooltip;
        Label _targetingTooltipLabel;
        readonly Button[] _extraAbilityButtons = new Button[MainExtraAbilityRules.AbilityCount];
        readonly string[] _extraAbilityTooltips = new string[MainExtraAbilityRules.AbilityCount];
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
        int _hoveredExtraAbilityIndex = -1;
        string _commandsFingerprint;
        UnitVisualCatalog _visualCatalog;
        MatchCombatPresenter _combatPresenter;
        VisualElement _unitAbilityBar;
        readonly List<AbilitySlotView> _abilitySlots = new List<AbilitySlotView>();
        string _abilityBarFingerprint;
        MatchUnitState _abilityBarUnit;

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
            _unitAbilityBar = root.Q<VisualElement>(UnitAbilityBarName);
            _emptyOverlay = root.Q<VisualElement>("InspectorEmptyOverlay");
            _panelBody = root.Q<VisualElement>("InspectorPanelBody");
            _commandTooltip = root.Q<VisualElement>("CommandTooltip");
            _commandTooltipLabel = root.Q<Label>("CommandTooltipLabel");
            _targetingTooltip = root.Q<VisualElement>("TargetingTooltip");
            _targetingTooltipLabel = root.Q<Label>("TargetingTooltipLabel");
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
            HideTargetingTooltip();
        }

        void BindExtraAbilityMenu(VisualElement root)
        {
            _extraAbilityMenu = root.Q<VisualElement>("ExtraAbilityMenu");
            _extraAbilityCloseButton = root.Q<Button>("ExtraAbilityCloseButton");
            for (var i = 0; i < MainExtraAbilityRules.AbilityCount; i++)
            {
                var abilityId = i + 1;
                var index = i;
                var button = root.Q<Button>($"ExtraAbilitySlot{abilityId}");
                _extraAbilityButtons[i] = button;
                if (button != null)
                {
                    button.clicked += () => PickMainExtraAbility(abilityId);
                    button.RegisterCallback<PointerEnterEvent>(_ => ShowExtraAbilityTooltip(index));
                    button.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
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
            UpdateTargetingTooltip();
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

            _title.text = MatchInspectorFormatting.FormatUnitTitle(unit.Role, unit.BonusSlot);
            _owner.text = string.Empty;
            _hp.text = $"HP: {MatchInspectorFormatting.FormatHp(unit.CurrentHp, unit.Stats.MaxHp)}";
            _meta.text = string.Empty;
            _badge.text = string.Empty;
            _readonly.text = string.Empty;
            RefreshUnitAbilities(unit);
        }

        /// <summary>
        /// Renders the selected unit's ability kit as square icons. Unlocked abilities show their
        /// authored colour; locked ones (not yet learned / below required hero or magic level) are
        /// greyed out. While an ability is on cooldown the square shows a sweeping overlay plus a
        /// countdown of the remaining seconds. Runs every frame (cheap: only the kit shape rebuilds
        /// when the ability set changes).
        /// </summary>
        void RefreshUnitAbilities(MatchUnitState unit)
        {
            if (_unitAbilityBar == null)
            {
                return;
            }

            _abilityBarUnit = unit;

            var abilities = unit?.Abilities;
            if (abilities == null || abilities.Length == 0)
            {
                if (_abilityBarFingerprint != null)
                {
                    ClearAbilitySlots();
                    _abilityBarFingerprint = null;
                }

                _unitAbilityBar.EnableInClassList(UnitAbilityBarHiddenClass, true);
                return;
            }

            _unitAbilityBar.EnableInClassList(UnitAbilityBarHiddenClass, false);

            var combat = _matchRuntime != null ? _matchRuntime.Controller?.Combat : null;
            var fingerprint = string.Empty;
            for (var i = 0; i < abilities.Length; i++)
            {
                fingerprint += (abilities[i]?.AbilityId ?? 0).ToString();
                fingerprint += ",";
            }

            if (!string.Equals(fingerprint, _abilityBarFingerprint, StringComparison.Ordinal))
            {
                RebuildAbilitySlots(abilities);
                _abilityBarFingerprint = fingerprint;
            }

            for (var i = 0; i < abilities.Length; i++)
            {
                var def = abilities[i];
                if (def == null)
                {
                    continue;
                }

                var view = _abilitySlots[i];
                var unlocked = IsAbilityUnlocked(combat, unit, def);
                var remaining = (unit.AbilityCooldownRemaining != null && i < unit.AbilityCooldownRemaining.Length)
                    ? unit.AbilityCooldownRemaining[i]
                    : 0f;
                var total = def.CooldownSeconds;

                view.Fill.style.backgroundColor = unlocked ? def.Fx.Color : LockedAbilityColor;
                view.Root.EnableInClassList("match-ability-slot--locked", !unlocked);

                if (remaining > 0.001f && total > 0.001f)
                {
                    var fraction = Mathf.Clamp01(remaining / total);
                    view.CooldownOverlay.style.height = Length.Percent(fraction * 100f);
                    view.CooldownOverlay.visible = true;
                    view.CdText.text = Mathf.CeilToInt(remaining).ToString();
                    view.CdText.visible = true;
                }
                else
                {
                    view.CooldownOverlay.visible = false;
                    view.CdText.visible = false;
                }
            }
        }

        static bool IsAbilityUnlocked(MatchCombatSystem combat, MatchUnitState unit, UnitAbilityDef def)
        {
            switch (def.Unlock)
            {
                case AbilityUnlock.Always:
                    return true;
                case AbilityUnlock.HeroLevel:
                    return unit.Level >= def.UnlockValue;
                case AbilityUnlock.MagicLevel:
                    return combat != null && combat.GetMagicLevel(unit.OwnerSlot) >= def.UnlockValue;
                default:
                    return false;
            }
        }

        void RebuildAbilitySlots(UnitAbilityDef[] abilities)
        {
            ClearAbilitySlots();
            for (var i = 0; i < abilities.Length; i++)
            {
                var def = abilities[i];

                var root = new VisualElement { name = $"AbilitySlot{i}" };
                root.AddToClassList("match-ability-slot");

                var icon = new VisualElement();
                icon.AddToClassList("match-ability-slot__icon");

                var fill = new VisualElement();
                fill.AddToClassList("match-ability-slot__fill");

                var overlay = new VisualElement();
                overlay.AddToClassList("match-ability-slot__cooldown");
                overlay.visible = false;

                var cdText = new Label();
                cdText.AddToClassList("match-ability-slot__cd-text");
                cdText.visible = false;

                icon.Add(fill);
                icon.Add(overlay);
                icon.Add(cdText);

                var label = new Label(def != null ? def.DisplayName : string.Empty);
                label.AddToClassList("match-ability-slot__label");

                root.Add(icon);
                root.Add(label);
                _unitAbilityBar.Add(root);

                var view = new AbilitySlotView
                {
                    Root = root,
                    Icon = icon,
                    Fill = fill,
                    CooldownOverlay = overlay,
                    CdText = cdText,
                    Label = label,
                    Def = def,
                    SlotIndex = i,
                };
                _abilitySlots.Add(view);

                // Styled hover tooltip (reuses the shared CommandTooltip) — see ShowAbilityTooltip.
                root.RegisterCallback<PointerEnterEvent>(_ => ShowAbilityTooltip(view));
                root.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
        }

        void ClearAbilitySlots()
        {
            _unitAbilityBar?.Clear();
            _abilitySlots.Clear();
        }

        sealed class AbilitySlotView
        {
            public VisualElement Root;
            public VisualElement Icon;
            public VisualElement Fill;
            public VisualElement CooldownOverlay;
            public Label CdText;
            public Label Label;
            public UnitAbilityDef Def;
            public int SlotIndex;
        }

        /// <summary>
        /// Styled hover tooltip for an ability square — name + description, and, when the ability is
        /// locked, the reason it is locked (hero/magic level requirement). Reuses the shared
        /// <c>CommandTooltip</c> element so it matches the rest of the HUD.
        /// </summary>
        void ShowAbilityTooltip(AbilitySlotView view)
        {
            if (view == null || view.Def == null || _commandTooltip == null || _commandTooltipLabel == null)
            {
                HideTooltip();
                return;
            }

            // Keep the command-slot hover state clean so it does not re-show on the next refresh.
            _hoveredCommandIndex = -1;
            _hoveredExtraAbilityIndex = -1;

            var def = view.Def;
            var combat = _matchRuntime != null ? _matchRuntime.Controller?.Combat : null;
            var unit = _abilityBarUnit;
            var unlocked = unit != null && IsAbilityUnlocked(combat, unit, def);

            var text = def.DisplayName;
            if (!string.IsNullOrEmpty(def.Description))
            {
                text += "\n" + def.Description;
            }

            if (!unlocked)
            {
                text += "\n" + AbilityUnlockRequirementText(def, combat);
            }
            else if (def.ManaCost > 0f)
            {
                text += "\nМана: " + def.ManaCost.ToString("0");
                if (def.CooldownSeconds > 0f)
                {
                    text += "   Кулдаун: " + def.CooldownSeconds.ToString("0") + " с";
                }
            }

            _commandTooltipLabel.text = text;
            _commandTooltip.RemoveFromClassList(TooltipHiddenClass);
            _commandTooltip.BringToFront();
            _commandTooltip.schedule.Execute(() => PositionTooltip(view.Root));
        }

        string AbilityUnlockRequirementText(UnitAbilityDef def, MatchCombatSystem combat)
        {
            switch (def.Unlock)
            {
                case AbilityUnlock.HeroLevel:
                    return $"Закрыто — требуется уровень героя: {def.UnlockValue}";
                case AbilityUnlock.MagicLevel:
                    var lvl = (combat != null && _abilityBarUnit != null)
                        ? combat.GetMagicLevel(_abilityBarUnit.OwnerSlot)
                        : 0;
                    return $"Закрыто — требуется магия ур.: {def.UnlockValue} (сейчас {lvl})";
                default:
                    return "Закрыто";
            }
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

            return $"{building.InstanceId}:{gold}:{passive}:{mainLevel}:{queueCount}:{barracksLevel}:{heroKey}:{heroCdKey}:{titanKey}:{titanCd}:{chargesKey}:{player?.MeleeDamageLevel ?? 0}:{player?.RangedDamageLevel ?? 0}:{player?.HpArmorLevel ?? 0}:{player?.MagicLevel ?? 0}:{player?.DivineBlessingComplete}:{player?.MainExtraAbilityId ?? 0}:{player?.MainMana ?? 0f:0}:{player?.MainExtraAbilityCooldownRemaining ?? 0f:0}:{MatchSelectionBridge.Current?.IsMainExtraCastPending}";
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
                PopulateBuildingAbilityCommands(player);
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
                PopulateTowerTrackCommands(controller, player, building);
                return;
            }
        }

        void PopulateTowerTrackCommands(
            MatchController controller,
            MatchPlayerState player,
            BuildingState building)
        {
            var queueLimit = controller != null
                ? controller.GetResearchQueueLimit(building.BuildingId)
                : 1;
            var queueFull = controller != null
                && controller.Research.GetCount(building.InstanceId) >= queueLimit;

            for (var trackIndex = 0; trackIndex < TowerTrackRules.TrackCount; trackIndex++)
            {
                PopulateTowerTrackCommand(controller, player, building, queueFull, trackIndex);
            }
        }

        void PopulateTowerTrackCommand(
            MatchController controller,
            MatchPlayerState player,
            BuildingState building,
            bool queueFull,
            int trackIndex)
        {
            const int uiSlotOffset = 3;
            var raceId = player?.RaceId ?? GameIds.Races.Human;
            var trackIds = raceId == GameIds.Races.Faceless
                ? FacelessTowerTrackRules.TrackIds
                : TowerTrackRules.TrackIds;
            var trackId = trackIds[trackIndex];
            var currentLevel = player?.GetTowerTrackLevel(trackIndex) ?? 0;
            var queued = controller?.Research.CountUpgrade(building.InstanceId, trackId) ?? 0;
            var nextLevel = MatchUpgradeLabelRules.GetNextLevel(currentLevel, queued);
            var cost = 0;
            var duration = 0f;
            var hasStep = player != null
                && MatchEconomyRules.TryGetTowerTrackUpgrade(
                    trackId,
                    currentLevel + queued,
                    out cost,
                    out duration);
            var canBuy = hasStep
                && !queueFull
                && player != null
                && player.Gold >= cost;
            SetCommand(
                uiSlotOffset + trackIndex,
                hasStep
                    ? MatchUpgradeLabelRules.FormatTowerTrackButton(trackIndex, nextLevel, cost, raceId)
                    : $"Макс. {MatchUpgradeLabelRules.GetTowerTrackTitle(trackIndex, raceId)}",
                canBuy,
                () => StartResearch(trackId),
                hasStep
                    ? MatchUpgradeLabelRules.FormatTowerTrackTooltip(trackIndex, nextLevel, cost, duration, raceId)
                    : $"{MatchUpgradeLabelRules.GetTowerTrackTitle(trackIndex, raceId)} — максимальный уровень");
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
            var bonusSlot = player != null
                ? HumanBonusUnitRules.EffectiveBonusSlotForRole(player.BonusPickSlot, role)
                : 0;
            if (bonusSlot == 0 && player != null)
            {
                if (role == UnitRole.Hero && heroSlot >= 1)
                {
                    bonusSlot = HumanBonusUnitRules.EffectiveBonusSlotForHero(player.BonusPickSlot, heroSlot);
                }
                else if (role == UnitRole.Titan)
                {
                    bonusSlot = HumanBonusUnitRules.EffectiveBonusSlotForTitan(player.BonusPickSlot);
                }
            }

            if (bonusSlot > 0
                && _visualCatalog != null
                && _visualCatalog.TryGetBonusPortrait(raceId, bonusSlot, out var bonusPortrait)
                && bonusPortrait != null)
            {
                return bonusPortrait;
            }

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

        void PopulateBuildingAbilityCommands(MatchPlayerState player)
        {
            if (player == null)
            {
                return;
            }

            PopulateBuildingAbilityCommand(player, BuildingAbilityRules.IceRingId);
            PopulateBuildingAbilityCommand(player, BuildingAbilityRules.WaveOfLightId);
        }

        void PopulateBuildingAbilityCommand(MatchPlayerState player, int abilityId)
        {
            var slot = abilityId == BuildingAbilityRules.IceRingId
                ? BuildingAbilityRules.IceRingSlotIndex
                : BuildingAbilityRules.WaveOfLightSlotIndex;
            var requiredLevel = BuildingAbilityRules.GetRequiredMainLevel(abilityId);
            if (!BuildingAbilityRules.IsUnlocked(abilityId, player.MainLevel))
            {
                // Keep the button enabled for hover tooltips; click is gated below.
                SetCommand(
                    slot,
                    MatchUpgradeLabelRules.FormatBuildingAbilityLockedButton(abilityId, requiredLevel),
                    enabled: true,
                    action: () => { },
                    MatchUpgradeLabelRules.FormatBuildingAbilityTooltip(abilityId));
                SetCommandLocked(slot, locked: true);
                return;
            }

            var canCast = BuildingAbilityRules.CanCast(player, abilityId);
            var bridge = MatchSelectionBridge.Current;
            var pending = bridge != null
                && bridge.IsBuildingAbilityPending
                && bridge.PendingBuildingAbilityId == abilityId;
            SetCommand(
                slot,
                MatchUpgradeLabelRules.FormatBuildingAbilityButton(
                    abilityId,
                    player.MainMana,
                    player.MainManaMax,
                    BuildingAbilityRules.GetCooldownRemaining(player, abilityId)),
                enabled: canCast || pending,
                action: () => ToggleBuildingAbilityCast(abilityId),
                MatchUpgradeLabelRules.FormatBuildingAbilityTooltip(abilityId));
            SetCommandLocked(slot, locked: false);
        }

        void ToggleBuildingAbilityCast(int abilityId)
        {
            var bridge = MatchSelectionBridge.Current;
            if (bridge == null)
            {
                return;
            }

            if (bridge.IsBuildingAbilityPending && bridge.PendingBuildingAbilityId == abilityId)
            {
                bridge.CancelBuildingAbilityTargeting();
                return;
            }

            var player = FindLocalPlayer(_matchRuntime?.Controller);
            if (player == null || !BuildingAbilityRules.CanCast(player, abilityId))
            {
                return;
            }

            if (abilityId == BuildingAbilityRules.WaveOfLightId)
            {
                // Instant radial wave centered on the base (host resolves the center).
                if (MatchNetworkCommands.IsAvailable)
                {
                    MatchNetworkCommands.RequestCastBuildingAbility(abilityId, UnityEngine.Vector3.zero);
                }
                else
                {
                    _matchRuntime.Controller.TryCastBuildingAbility(
                        player.SlotIndex,
                        abilityId,
                        UnityEngine.Vector3.zero);
                }

                return;
            }

            bridge.BeginBuildingAbilityTargeting(abilityId);
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
                var canCast = MainExtraAbilityRules.CanCast(player);
                var pending = MatchSelectionBridge.Current != null
                    && MatchSelectionBridge.Current.IsMainExtraCastPending;
                SetCommand(
                    slot,
                    MatchUpgradeLabelRules.FormatExtraAbilityCastButton(
                        player.MainExtraAbilityId,
                        player.MainMana,
                        player.MainManaMax,
                        player.MainExtraAbilityCooldownRemaining),
                    enabled: canCast || pending,
                    action: ToggleMainExtraAbilityCast,
                    MatchUpgradeLabelRules.FormatExtraAbilityCastTooltip(
                        player.MainExtraAbilityId,
                        player.MainMana,
                        player.MainManaMax,
                        player.MainExtraAbilityCooldownRemaining));
                SetCommandLocked(slot, locked: false);
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
                SetCommandLocked(slot, locked: false);
                return;
            }

            var controller = _matchRuntime?.Controller;
            var queued = controller?.Research.CountUpgrade(
                building.InstanceId,
                GameIds.Upgrades.DivineBlessing) ?? 0;
            var meetsLevel = MatchEconomyRules.CanPurchaseDivineBlessing(
                player.MainLevel,
                player.DivineBlessingComplete);
            var canStart = queued == 0
                && !queueFull
                && meetsLevel
                && player.Gold >= MatchEconomyRules.DivineBlessingCost;
            var lockedByLevel = !meetsLevel;

            // Keep the button visible from main L1 (locked label); UI Toolkit disables
            // pointer events on SetEnabled(false), so locked-by-level stays enabled for
            // hover tooltips and the click is gated below.
            SetCommand(
                slot,
                lockedByLevel
                    ? MatchUpgradeLabelRules.FormatDivineBlessingLockedButton(
                        MatchEconomyRules.DivineBlessingRequiredMainLevel)
                    : MatchUpgradeLabelRules.FormatDivineBlessingButton(
                        MatchEconomyRules.DivineBlessingCost),
                enabled: canStart || lockedByLevel,
                () =>
                {
                    if (!meetsLevel || queued > 0 || queueFull
                        || player.Gold < MatchEconomyRules.DivineBlessingCost)
                    {
                        return;
                    }

                    StartResearch(GameIds.Upgrades.DivineBlessing);
                },
                MatchUpgradeLabelRules.FormatDivineBlessingTooltip(
                    MatchEconomyRules.DivineBlessingCost,
                    MatchEconomyRules.DivineBlessingSeconds,
                    player.MainLevel));
            SetCommandLocked(slot, lockedByLevel || !canStart);
        }

        void SetCommandLocked(int index, bool locked)
        {
            if (index < 0 || index >= CommandSlotCount || _commandSlots[index] == null)
            {
                return;
            }

            _commandSlots[index].EnableInClassList(CommandSlotLockedClass, locked);
        }

        void ToggleMainExtraAbilityCast()
        {
            var bridge = MatchSelectionBridge.Current;
            if (bridge == null)
            {
                return;
            }

            if (bridge.IsMainExtraCastPending)
            {
                bridge.CancelMainExtraAbilityTargeting();
                return;
            }

            var player = FindLocalPlayer(_matchRuntime?.Controller);
            if (player == null || !MainExtraAbilityRules.CanCast(player))
            {
                return;
            }

            bridge.BeginMainExtraAbilityTargeting();
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
                var implemented = MainExtraAbilityRules.IsImplemented(abilityId);
                button.text = implemented
                    ? MainExtraAbilityRules.GetDisplayName(abilityId)
                    : "Скоро";
                _extraAbilityTooltips[i] = MainExtraAbilityRules.GetMenuTooltip(abilityId);
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
            _hoveredExtraAbilityIndex = -1;
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
            _commandTooltip.schedule.Execute(() => PositionTooltip(_commandSlots[index]));
        }

        void ShowExtraAbilityTooltip(int index)
        {
            _hoveredCommandIndex = -1;
            if (_commandTooltip == null
                || _commandTooltipLabel == null
                || index < 0
                || index >= MainExtraAbilityRules.AbilityCount
                || string.IsNullOrEmpty(_extraAbilityTooltips[index])
                || _extraAbilityButtons[index] == null)
            {
                HideTooltip();
                return;
            }

            _hoveredExtraAbilityIndex = index;
            _commandTooltipLabel.text = _extraAbilityTooltips[index];
            _commandTooltip.RemoveFromClassList(TooltipHiddenClass);
            _commandTooltip.BringToFront();
            _commandTooltip.schedule.Execute(() => PositionTooltip(_extraAbilityButtons[index]));
        }

        void PositionTooltip(VisualElement anchor)
        {
            if (_commandTooltip == null || anchor == null)
            {
                return;
            }

            var buttonBound = anchor.worldBound;
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

        void PositionTooltip(int index)
        {
            if (index < 0 || index >= CommandSlotCount)
            {
                return;
            }

            PositionTooltip(_commandSlots[index]);
        }

        void HideTooltip()
        {
            _hoveredCommandIndex = -1;
            _hoveredExtraAbilityIndex = -1;
            _commandTooltip?.AddToClassList(TooltipHiddenClass);
            if (_commandTooltipLabel != null)
            {
                _commandTooltipLabel.text = string.Empty;
            }
        }

        void UpdateTargetingTooltip()
        {
            if (_targetingTooltip == null || _targetingTooltipLabel == null)
            {
                return;
            }

            var bridge = MatchSelectionBridge.Current;
            if (bridge == null || !bridge.IsMainExtraCastPending)
            {
                HideTargetingTooltip();
                return;
            }

            var name = MatchInspectorFormatting.FormatPickTargetName(
                _matchRuntime?.Controller,
                bridge.HoverTarget);
            if (string.IsNullOrEmpty(name))
            {
                HideTargetingTooltip();
                return;
            }

            _targetingTooltipLabel.text = name;
            _targetingTooltip.RemoveFromClassList(TargetingTooltipHiddenClass);
            _targetingTooltip.BringToFront();
            PositionTargetingTooltipNearPointer();
        }

        void PositionTargetingTooltipNearPointer()
        {
            if (_targetingTooltip == null || _hudRoot == null || _targetingTooltip.panel == null)
            {
                return;
            }

            if (!MatchSelectionUiPointer.TryGetScreenPosition(out var screenPosition))
            {
                return;
            }

            // Input System is bottom-left; UI Toolkit ScreenToPanel expects top-left.
            var panelPos = MatchSelectionUiPointer.ScreenToPanelPosition(
                _targetingTooltip.panel,
                screenPosition);
            var rootBound = _hudRoot.worldBound;
            _targetingTooltip.style.left = panelPos.x - rootBound.x + TargetingTooltipOffsetX;
            _targetingTooltip.style.top = panelPos.y - rootBound.y + TargetingTooltipOffsetY;
        }

        void HideTargetingTooltip()
        {
            _targetingTooltip?.AddToClassList(TargetingTooltipHiddenClass);
            if (_targetingTooltipLabel != null)
            {
                _targetingTooltipLabel.text = string.Empty;
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
                _commandSlots[i].EnableInClassList(CommandSlotLockedClass, false);
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
