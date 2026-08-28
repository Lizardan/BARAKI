using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MatchContextStripController : MonoBehaviour
    {
        const string HiddenClass = "match-context-strip__mode--hidden";
        const string EmptyOverlayHiddenClass = "match-panel__empty-overlay--hidden";
        const string PanelActiveClass = "match-dock-panel__inner--active";
        const string ResearchHiddenClass = "match-context-strip__research-block--hidden";
        const string OwnerHiddenClass = "match-context-strip__owner--hidden";
        const string StatLineHiddenClass = "match-context-strip__stat-line--hidden";

        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private UnitVisualCatalog _visualCatalog;

        MatchRuntime _matchRuntime;
        MatchCombatPresenter _combatPresenter;
        MatchSelection _selection;
        VisualElement _contextPanelInner;
        VisualElement _selectionContext;
        VisualElement _emptyOverlay;
        VisualElement _portrait;
        VisualElement _researchBlock;
        Label _title;
        Label _owner;
        Label _hpStat;
        Label _stat1;
        Label _stat2;
        Label _stat3;
        Label _stat4;
        Label _stat5;
        Label _researchLabel;
        ProgressBar _researchProgress;
        readonly Label[] _researchQueueSlots = new Label[MatchResearchQueue.MaxQueueLength];

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _contextPanelInner = root.Q<VisualElement>("ContextStrip")?.Q(className: "match-dock-panel__inner");
            _selectionContext = root.Q<VisualElement>("SelectionContext");
            _emptyOverlay = root.Q<VisualElement>("ContextEmptyOverlay");
            _portrait = root.Q<VisualElement>("EntityPortrait");
            _title = root.Q<Label>("EntityTitle");
            _owner = root.Q<Label>("EntityOwner");
            _hpStat = root.Q<Label>("EntityHpStat");
            _stat1 = root.Q<Label>("EntityStat1");
            _stat2 = root.Q<Label>("EntityStat2");
            _stat3 = root.Q<Label>("EntityStat3");
            _stat4 = root.Q<Label>("EntityStat4");
            _stat5 = root.Q<Label>("EntityStat5");
            _researchBlock = root.Q<VisualElement>("ResearchBlock");
            _researchLabel = root.Q<Label>("ResearchLabel");
            _researchProgress = root.Q<ProgressBar>("ResearchProgress");
            for (var i = 0; i < _researchQueueSlots.Length; i++)
            {
                _researchQueueSlots[i] = root.Q<Label>($"ResearchQueueSlot{i}");
            }
            SetResearchVisible(false);
            SetOwnerVisible(false);
            ClearPortrait();
        }

        void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            ResolveVisualCatalog();
            SubscribeSelection();
        }

        void OnDisable()
        {
            UnsubscribeSelection();
        }

        void LateUpdate()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            if (_selection == null)
            {
                SubscribeSelection();
            }

            Refresh();
        }

        void ResolveVisualCatalog()
        {
            if (_visualCatalog != null)
            {
                return;
            }

            if (_combatPresenter == null)
            {
                _combatPresenter = MatchCombatPresenter.Current;
            }

            _visualCatalog = _combatPresenter != null ? _combatPresenter.VisualCatalog : null;
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

        void OnSelectionChanged(MatchPickTarget target) => Refresh();

        void Refresh()
        {
            var target = _selection != null ? _selection.Current : MatchPickTarget.None;
            if (!target.HasTarget)
            {
                _selectionContext?.AddToClassList(HiddenClass);
                SetPanelActive(false);
                SetEmptyOverlayVisible(true);
                SetResearchVisible(false);
                SetOwnerVisible(false);
                ClearPortrait();
                return;
            }

            SetPanelActive(true);
            SetEmptyOverlayVisible(false);
            _selectionContext?.RemoveFromClassList(HiddenClass);

            if (target.IsUnit)
            {
                PopulateUnit(target.EntityId);
                return;
            }

            PopulateBuilding(target.EntityId);
        }

        void PopulateUnit(int unitId)
        {
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                ClearStats();
                return;
            }

            var stats = unit.Stats;
            var isHero = unit.IsHero && unit.HeroSlot >= 1;
            _title.text = isHero
                ? MatchInspectorFormatting.FormatHeroName(unit.HeroSlot)
                : MatchInspectorFormatting.FormatUnitTitle(unit.Role, unit.BonusSlot);
            SetOwnerVisible(false);
            SetStatText(_hpStat, $"HP: {MatchInspectorFormatting.FormatHp(unit.CurrentHp, stats.MaxHp)}");
            SetStatText(_stat1, $"Урон: {MatchInspectorFormatting.FormatDamageRange(stats.DamageMin, stats.DamageMax)}");
            SetStatText(_stat2, $"Броня: {MatchInspectorFormatting.FormatStatValue(stats.Armor)}");
            SetStatText(_stat3, $"Дальность: {MatchInspectorFormatting.FormatStatValue(stats.AttackRange)}");
            SetStatText(_stat4, $"Скорость: {MatchInspectorFormatting.FormatStatValue(stats.MoveSpeed)}");
            var stat5Text = isHero
                ? FormatHeroProgress(unit)
                : stats.HasMana
                    ? $"Мана: {MatchInspectorFormatting.FormatHp(unit.CurrentMana, stats.MaxMana)}"
                    : string.Empty;
            SetStatText(_stat5, stat5Text);
            ApplyUnitPortrait(unit);
            SetResearchVisible(false);
        }

        string FormatHeroProgress(MatchUnitState unit)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            var hero = controller?.GetHeroRoster(unit.OwnerSlot)?.Get(unit.HeroSlot);
            if (hero == null)
            {
                return string.Empty;
            }

            return $"{MatchInspectorFormatting.FormatHeroLevel(hero.Level)} · "
                   + MatchInspectorFormatting.FormatHeroXp(hero.Xp, hero.Level);
        }

        void PopulateBuilding(int instanceId)
        {
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            var building = controller?.Buildings.GetByInstanceId(instanceId);
            if (building == null)
            {
                ClearStats();
                return;
            }

            _title.text = MatchInspectorFormatting.FormatBuildingName(building.BuildingId);
            SetOwnerVisible(false);
            SetStatText(_hpStat, $"HP: {MatchInspectorFormatting.FormatHp(building.CurrentHp, building.MaxHp)}");
            SetStatText(_stat1, building.IsRuins ? "Руины" : string.Empty);
            SetStatText(_stat2, string.Empty);
            SetStatText(_stat3, string.Empty);
            SetStatText(_stat4, string.Empty);
            SetStatText(_stat5, string.Empty);

            if (MatchInspectorFormatting.IsBarracksBuilding(building.BuildingId))
            {
                var barracks = controller.WaveScheduler.GetBarracks(building.OwnerSlot, building.BuildingId);
                if (barracks != null)
                {
                    SetStatText(_stat2, $"Уровень: {barracks.Level}");
                }
            }
            else if (MatchInspectorFormatting.IsMainBuilding(building.BuildingId)
                     && building.OwnerSlot < controller.Players.Count)
            {
                var player = controller.Players[building.OwnerSlot];
                SetStatText(_stat1, building.IsRuins
                    ? "Руины"
                    : $"Мана: {MatchInspectorFormatting.FormatHp(player.MainMana, player.MainManaMax)}");
                SetStatText(_stat2, $"Ур. {player.MainLevel}");
                SetStatText(_stat3, $"Passive: {player.PassiveGoldLevel}");
            }

            ClearPortrait();
            if (_portrait != null)
            {
                _portrait.style.backgroundColor = MatchPlayerColors.GetSlotColor(building.OwnerSlot);
            }

            if (MatchInspectorFormatting.IsResearchBuilding(building.BuildingId))
            {
                UpdateResearch(controller, building.InstanceId);
            }
            else
            {
                SetResearchVisible(false);
            }
        }

        void ApplyUnitPortrait(MatchUnitState unit)
        {
            ResolveVisualCatalog();
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            var raceId = GameIds.Races.Human;
            if (controller != null
                && unit.OwnerSlot >= 0
                && unit.OwnerSlot < controller.Players.Count)
            {
                raceId = controller.Players[unit.OwnerSlot].RaceId;
            }

            Texture2D portrait = null;
            if (_visualCatalog != null)
            {
                if (HumanBonusUnitRules.MatchesUnit(unit.BonusSlot, unit.Role, unit.HeroSlot))
                {
                    _visualCatalog.TryGetBonusPortrait(raceId, unit.BonusSlot, out portrait);
                }

                if (portrait == null)
                {
                    _visualCatalog.TryGetPortrait(raceId, unit.Role, unit.HeroSlot, out portrait);
                }
            }

            if (portrait != null && _portrait != null)
            {
                _portrait.style.backgroundImage = new StyleBackground(portrait);
                _portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                _portrait.style.backgroundColor = Color.clear;
                _portrait.style.borderTopColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
                _portrait.style.borderRightColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
                _portrait.style.borderBottomColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
                _portrait.style.borderLeftColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
                _portrait.style.borderTopWidth = 2f;
                _portrait.style.borderRightWidth = 2f;
                _portrait.style.borderBottomWidth = 2f;
                _portrait.style.borderLeftWidth = 2f;
                return;
            }

            ClearPortrait();
            if (_portrait != null)
            {
                _portrait.style.backgroundColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
            }
        }

        void ClearPortrait()
        {
            if (_portrait == null)
            {
                return;
            }

            _portrait.style.backgroundImage = new StyleBackground();
            _portrait.style.backgroundColor = new Color(34f / 255f, 34f / 255f, 36f / 255f);
            _portrait.style.borderTopWidth = 0f;
            _portrait.style.borderRightWidth = 0f;
            _portrait.style.borderBottomWidth = 0f;
            _portrait.style.borderLeftWidth = 0f;
        }

        void UpdateResearch(MatchController controller, int buildingInstanceId)
        {
            SetResearchVisible(true);
            UpdateResearchQueueSlots(controller, buildingInstanceId);
            if (controller != null && controller.TryGetResearch(buildingInstanceId, out var research))
            {
                _researchLabel.text = MatchInspectorFormatting.FormatResearchProgress(research.Progress01);
                _researchProgress.value = research.Progress01;
                return;
            }

            ClearResearchLabelAndBar();
        }

        void UpdateResearchQueueSlots(MatchController controller, int buildingInstanceId)
        {
            IReadOnlyList<BuildingResearchState> queue = null;
            var hasQueue = controller != null
                && controller.TryGetResearchQueue(buildingInstanceId, out queue);
            var passiveBase = 0;
            var barracksBase = 0;
            var mainLevelBase = 0;
            var meleeBase = 0;
            var rangedBase = 0;
            var hpArmorBase = 0;
            var building = controller?.Buildings.GetByInstanceId(buildingInstanceId);
            if (controller != null && building != null)
            {
                if (building.OwnerSlot >= 0 && building.OwnerSlot < controller.Players.Count)
                {
                    var player = controller.Players[building.OwnerSlot];
                    passiveBase = player.PassiveGoldLevel;
                    mainLevelBase = player.MainLevel;
                    meleeBase = player.MeleeDamageLevel;
                    rangedBase = player.RangedDamageLevel;
                    hpArmorBase = player.HpArmorLevel;
                }
                var barracks = controller.WaveScheduler.GetBarracks(building.OwnerSlot, building.BuildingId);
                if (barracks != null)
                {
                    barracksBase = barracks.Level;
                }
            }
            var passiveSeen = 0;
            var barracksSeen = 0;
            var mainSeen = 0;
            var meleeSeen = 0;
            var rangedSeen = 0;
            var hpArmorSeen = 0;
            for (var i = 0; i < _researchQueueSlots.Length; i++)
            {
                var slot = _researchQueueSlots[i];
                if (slot == null)
                {
                    continue;
                }
                if (!hasQueue || queue == null || i >= queue.Count)
                {
                    slot.text = string.Empty;
                    slot.EnableInClassList("match-context-strip__research-queue-slot--active", false);
                    continue;
                }
                var research = queue[i];
                var displayLevel = 0;
                if (research.UpgradeId == GameIds.Upgrades.MainPassiveGold)
                {
                    displayLevel = passiveBase + 1 + passiveSeen;
                    passiveSeen++;
                }
                else if (research.UpgradeId == GameIds.Upgrades.BarracksLevel)
                {
                    displayLevel = barracksBase + 1 + barracksSeen;
                    barracksSeen++;
                }
                else if (research.UpgradeId == GameIds.Upgrades.MainBuildingLevel)
                {
                    displayLevel = mainLevelBase + 1 + mainSeen;
                    mainSeen++;
                }
                else if (research.UpgradeId == GameIds.Upgrades.MeleeDamage)
                {
                    displayLevel = meleeBase + 1 + meleeSeen;
                    meleeSeen++;
                }
                else if (research.UpgradeId == GameIds.Upgrades.RangedDamage)
                {
                    displayLevel = rangedBase + 1 + rangedSeen;
                    rangedSeen++;
                }
                else if (research.UpgradeId == GameIds.Upgrades.Armor)
                {
                    displayLevel = hpArmorBase + 1 + hpArmorSeen;
                    hpArmorSeen++;
                }
                else if (HeroRules.TryParseHireUpgradeId(research.UpgradeId, out var heroSlot))
                {
                    displayLevel = heroSlot;
                }
                slot.text = MatchUpgradeLabelRules.FormatQueueSlotShort(research.UpgradeId, displayLevel);
                slot.EnableInClassList("match-context-strip__research-queue-slot--active", i == 0);
            }
        }

        void ClearResearch()
        {
            ClearResearchQueueSlots();
            ClearResearchLabelAndBar();
        }

        void ClearResearchQueueSlots()
        {
            for (var i = 0; i < _researchQueueSlots.Length; i++)
            {
                var slot = _researchQueueSlots[i];
                if (slot == null)
                {
                    continue;
                }
                slot.text = string.Empty;
                slot.EnableInClassList("match-context-strip__research-queue-slot--active", false);
            }
        }

        void ClearResearchLabelAndBar()
        {
            if (_researchLabel != null)
            {
                _researchLabel.text = MatchInspectorFormatting.FormatNoActiveResearch();
            }

            if (_researchProgress != null)
            {
                _researchProgress.value = 0f;
            }
        }

        void SetResearchVisible(bool visible)
        {
            _researchBlock?.EnableInClassList(ResearchHiddenClass, !visible);
            if (!visible)
            {
                ClearResearch();
            }
        }

        void SetOwnerVisible(bool visible)
        {
            if (_owner == null)
            {
                return;
            }

            _owner.text = string.Empty;
            _owner.EnableInClassList(OwnerHiddenClass, !visible);
        }

        void SetStatText(Label label, string text)
        {
            if (label == null)
            {
                return;
            }

            label.text = text;
            label.EnableInClassList(StatLineHiddenClass, string.IsNullOrEmpty(text));
        }

        void ClearStats()
        {
            _title.text = string.Empty;
            SetOwnerVisible(false);
            SetStatText(_hpStat, "HP: —");
            SetStatText(_stat1, string.Empty);
            SetStatText(_stat2, string.Empty);
            SetStatText(_stat3, string.Empty);
            SetStatText(_stat4, string.Empty);
            SetStatText(_stat5, string.Empty);
            ClearPortrait();
            SetResearchVisible(false);
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

        void SetPanelActive(bool active)
        {
            _contextPanelInner?.EnableInClassList(PanelActiveClass, active);
        }
    }
}

