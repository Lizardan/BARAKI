using Game.Gameplay.Combat;
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

        [SerializeField] private UIDocument _uiDocument;

        MatchRuntime _matchRuntime;
        MatchSelection _selection;
        VisualElement _contextPanelInner;
        VisualElement _unitContext;
        VisualElement _researchContext;
        VisualElement _emptyOverlay;
        VisualElement _unitPortrait;
        Label _hpStat;
        Label _damageStat;
        Label _armorStat;
        Label _rangeStat;
        Label _speedStat;
        Label _manaStat;
        Label _researchLabel;
        ProgressBar _researchProgress;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _contextPanelInner = root.Q<VisualElement>("ContextStrip")?.Q(className: "match-dock-panel__inner");
            _unitContext = root.Q<VisualElement>("UnitContext");
            _researchContext = root.Q<VisualElement>("ResearchContext");
            _emptyOverlay = root.Q<VisualElement>("ContextEmptyOverlay");
            _unitPortrait = root.Q<VisualElement>("UnitPortrait");
            _hpStat = root.Q<Label>("UnitHpStat");
            _damageStat = root.Q<Label>("UnitDamageStat");
            _armorStat = root.Q<Label>("UnitArmorStat");
            _rangeStat = root.Q<Label>("UnitRangeStat");
            _speedStat = root.Q<Label>("UnitSpeedStat");
            _manaStat = root.Q<Label>("UnitManaStat");
            _researchLabel = root.Q<Label>("ResearchLabel");
            _researchProgress = root.Q<ProgressBar>("ResearchProgress");
        }

        void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }

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

            Refresh();
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
                _unitContext?.AddToClassList(HiddenClass);
                _researchContext?.AddToClassList(HiddenClass);
                SetPanelActive(false);
                SetEmptyOverlayVisible(true);
                return;
            }

            SetPanelActive(true);
            SetEmptyOverlayVisible(false);

            if (target.IsUnit)
            {
                ShowUnitMode();
                PopulateUnit(target.EntityId);
                return;
            }

            ShowResearchMode();
        }

        void ShowUnitMode()
        {
            _unitContext?.RemoveFromClassList(HiddenClass);
            _researchContext?.AddToClassList(HiddenClass);
        }

        void ShowResearchMode()
        {
            _unitContext?.AddToClassList(HiddenClass);
            _researchContext?.RemoveFromClassList(HiddenClass);
            _researchLabel.text = MatchInspectorFormatting.FormatNoActiveResearch();
            _researchProgress.value = 0f;
        }

        void ClearUnitStats()
        {
            _hpStat.text = "HP: —";
            _damageStat.text = "Урон: —";
            _armorStat.text = "Броня: —";
            _rangeStat.text = "Дальность: —";
            _speedStat.text = "Скорость: —";
            _manaStat.text = "Мана: —";
            _unitPortrait.style.backgroundColor = new Color(0.2f, 0.2f, 0.25f);
        }

        void PopulateUnit(int unitId)
        {
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                ClearUnitStats();
                return;
            }

            var stats = unit.Stats;
            _hpStat.text = $"HP: {MatchInspectorFormatting.FormatHp(unit.CurrentHp, stats.MaxHp)}";
            _damageStat.text =
                $"Урон: {MatchInspectorFormatting.FormatDamageRange(stats.DamageMin, stats.DamageMax)}";
            _armorStat.text = $"Броня: {MatchInspectorFormatting.FormatStatValue(stats.Armor)}";
            _rangeStat.text = $"Дальность: {MatchInspectorFormatting.FormatStatValue(stats.AttackRange)}";
            _speedStat.text = $"Скорость: {MatchInspectorFormatting.FormatStatValue(stats.MoveSpeed)}";
            _manaStat.text = stats.HasMana
                ? $"Мана: {MatchInspectorFormatting.FormatHp(unit.CurrentMana, stats.MaxMana)}"
                : "Мана: —";
            _manaStat.style.display = stats.HasMana ? DisplayStyle.Flex : DisplayStyle.None;
            _unitPortrait.style.backgroundColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
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
