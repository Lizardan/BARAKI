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
    public sealed class MatchMinimapController : MonoBehaviour
    {
        const string BlipClass = "match-minimap__blip";
        const string BlipBuildingClass = "match-minimap__blip--building";
        const string BlipBaseClass = "match-minimap__blip--base";
        const string BlipSelectedClass = "match-minimap__blip--selected";
        const string BlipRuinsClass = "match-minimap__blip--ruins";

        [SerializeField] private UIDocument _uiDocument;

        MatchRuntime _matchRuntime;
        VisualElement _canvas;
        MatchMinimapGeometryElement _geometryElement;
        MatchMinimapTopology _topology;
        MatchController _topologyController;
        readonly Dictionary<string, VisualElement> _blips = new();
        float _panelWidth = 350f;
        float _panelHeight = 350f;
        float _lastGeometryWidth;
        float _lastGeometryHeight;
        MatchMinimapTopology _lastDrawnTopology;

        void Awake()
        {
            if (_uiDocument == null)
            {
                TryGetComponent(out _uiDocument);
            }

            var root = _uiDocument.rootVisualElement;
            _canvas = root.Q<VisualElement>("MinimapCanvas");
            var geometryLayer = root.Q<VisualElement>("MinimapGeometryLayer");
            if (geometryLayer != null)
            {
                geometryLayer.Clear();
                _geometryElement = new MatchMinimapGeometryElement();
                _geometryElement.style.position = Position.Absolute;
                _geometryElement.style.left = 0;
                _geometryElement.style.right = 0;
                _geometryElement.style.top = 0;
                _geometryElement.style.bottom = 0;
                geometryLayer.Add(_geometryElement);
            }
        }

        void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = FindAnyObjectByType<MatchRuntime>();
            }
        }

        void LateUpdate()
        {
            if (_canvas == null)
            {
                return;
            }

            _panelWidth = _canvas.resolvedStyle.width > 1f ? _canvas.resolvedStyle.width : _panelWidth;
            _panelHeight = _canvas.resolvedStyle.height > 1f ? _canvas.resolvedStyle.height : _panelHeight;

            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null || !controller.IsRunning || controller.Layout == null)
            {
                ClearBlips();
                _topology = null;
                _topologyController = null;
                UpdateGeometry();
                return;
            }

            if (controller != _topologyController)
            {
                _topologyController = controller;
                _topology = MatchMinimapTopologyBuilder.Build(controller.Layout, controller.Graph);
            }

            UpdateGeometry();

            var arenaRadius = controller.Layout.ArenaRadius;
            var selection = _matchRuntime.Selection;
            var activeKeys = new HashSet<string>();

            for (var slot = 0; slot < controller.Layout.Slots.Count; slot++)
            {
                var key = GetBaseKey(slot);
                activeKeys.Add(key);
                var blip = GetOrCreateBlip(key, BlipBaseClass);
                var position = controller.Layout.Slots[slot].BasePosition;
                PlaceBlip(blip, position, arenaRadius, 9f);
                blip.style.backgroundColor = MatchPlayerColors.GetSlotColor(slot);
                blip.EnableInClassList(BlipSelectedClass, false);
            }

            foreach (var building in controller.Buildings.Buildings)
            {
                var key = GetBuildingKey(building.InstanceId);
                activeKeys.Add(key);
                var blip = GetOrCreateBlip(key, BlipBuildingClass);
                PlaceBlip(blip, building.WorldPosition, arenaRadius, 7f);
                blip.style.backgroundColor = building.IsRuins
                    ? new Color(0.45f, 0.22f, 0.22f)
                    : new Color(0.55f, 0.55f, 0.55f);
                blip.EnableInClassList(BlipRuinsClass, building.IsRuins);
                blip.EnableInClassList(
                    BlipSelectedClass,
                    selection != null
                    && selection.Current.IsBuilding
                    && selection.Current.EntityId == building.InstanceId);
            }

            var combat = controller.Combat;
            foreach (var unit in combat.Units)
            {
                if (!unit.IsAlive)
                {
                    continue;
                }

                var key = GetUnitKey(unit.UnitId);
                activeKeys.Add(key);
                var blip = GetOrCreateBlip(key, string.Empty);
                PlaceBlip(blip, unit.WorldPosition, arenaRadius, 5f);
                blip.style.backgroundColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
                blip.EnableInClassList(
                    BlipSelectedClass,
                    selection != null
                    && selection.Current.IsUnit
                    && selection.Current.EntityId == unit.UnitId);
            }

            RemoveStaleBlips(activeKeys);
        }

        void UpdateGeometry()
        {
            if (_geometryElement == null)
            {
                return;
            }

            var arenaRadius = _topologyController?.Layout?.ArenaRadius ?? MatchArenaGenerator.DefaultArenaRadius;
            var sizeChanged = !Mathf.Approximately(_lastGeometryWidth, _panelWidth)
                || !Mathf.Approximately(_lastGeometryHeight, _panelHeight);
            if (!sizeChanged && ReferenceEquals(_topology, _lastDrawnTopology))
            {
                return;
            }

            _lastGeometryWidth = _panelWidth;
            _lastGeometryHeight = _panelHeight;
            _lastDrawnTopology = _topology;
            _geometryElement.SetDrawData(_topology, arenaRadius, _panelWidth, _panelHeight);
        }

        VisualElement GetOrCreateBlip(string key, string extraClass)
        {
            if (_blips.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var blip = new VisualElement { pickingMode = PickingMode.Ignore };
            blip.AddToClassList(BlipClass);
            if (!string.IsNullOrEmpty(extraClass))
            {
                blip.AddToClassList(extraClass);
            }

            _canvas.Add(blip);
            _blips[key] = blip;
            return blip;
        }

        void PlaceBlip(VisualElement blip, Vector3 worldPosition, float arenaRadius, float size)
        {
            var normalized = MatchMinimapProjection.WorldToNormalized(worldPosition, arenaRadius);
            var panelPosition = MatchMinimapProjection.NormalizedToPanel(normalized, _panelWidth, _panelHeight);
            blip.style.left = panelPosition.x - size * 0.5f;
            blip.style.top = panelPosition.y - size * 0.5f;
            blip.style.width = size;
            blip.style.height = size;
        }

        void RemoveStaleBlips(HashSet<string> activeKeys)
        {
            var stale = new List<string>();
            foreach (var pair in _blips)
            {
                if (!activeKeys.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var key in stale)
            {
                _blips[key].RemoveFromHierarchy();
                _blips.Remove(key);
            }
        }

        void ClearBlips()
        {
            foreach (var blip in _blips.Values)
            {
                blip.RemoveFromHierarchy();
            }

            _blips.Clear();
        }

        static string GetBaseKey(int slot) => $"base:{slot}";
        static string GetUnitKey(int unitId) => $"unit:{unitId}";
        static string GetBuildingKey(int instanceId) => $"building:{instanceId}";
    }
}
