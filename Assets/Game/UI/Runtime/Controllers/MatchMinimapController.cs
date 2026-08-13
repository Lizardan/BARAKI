using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Cameras;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Fog;
using Game.Gameplay.Match.Selection;
using Game.Gameplay.Networking;
using Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Controllers
{
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(1000)]
    public sealed class MatchMinimapController : MonoBehaviour
    {
        const string BlipClass = "match-minimap__blip";
        const string BlipBuildingClass = "match-minimap__blip--building";
        const string BlipBaseClass = "match-minimap__blip--base";
        const string BlipSelectedClass = "match-minimap__blip--selected";
        const string BlipRuinsClass = "match-minimap__blip--ruins";

        [SerializeField] private UIDocument _uiDocument;

        MatchRuntime _matchRuntime;
        MatchFogOfWar _fogOfWar;
        VisualElement _canvas;
        MatchMinimapGeometryElement _geometryElement;
        MatchMinimapViewportElement _viewportElement;
        MatchMinimapTopology _topology;
        MatchController _topologyController;
        readonly Dictionary<string, VisualElement> _blips = new();
        readonly Vector3[] _groundCorners = new Vector3[4];
        float _panelWidth = 350f;
        float _panelHeight = 350f;
        float _lastGeometryWidth;
        float _lastGeometryHeight;
        float _lastGeometryYaw = float.NaN;
        MatchMinimapTopology _lastDrawnTopology;
        bool _isDraggingMinimap;
        bool _heldPanInputLock;

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

                _viewportElement = new MatchMinimapViewportElement();
                _viewportElement.style.position = Position.Absolute;
                _viewportElement.style.left = 0;
                _viewportElement.style.right = 0;
                _viewportElement.style.top = 0;
                _viewportElement.style.bottom = 0;
                geometryLayer.Add(_viewportElement);
            }
        }

        void OnEnable()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

            if (_canvas == null)
            {
                return;
            }

            _canvas.RegisterCallback<PointerDownEvent>(OnMinimapPointerDown);
            _canvas.RegisterCallback<PointerMoveEvent>(OnMinimapPointerMove);
            _canvas.RegisterCallback<PointerUpEvent>(OnMinimapPointerUp);
            _canvas.RegisterCallback<PointerCaptureOutEvent>(OnMinimapPointerCaptureOut);
        }

        void OnDisable()
        {
            if (_canvas != null)
            {
                _canvas.UnregisterCallback<PointerDownEvent>(OnMinimapPointerDown);
                _canvas.UnregisterCallback<PointerMoveEvent>(OnMinimapPointerMove);
                _canvas.UnregisterCallback<PointerUpEvent>(OnMinimapPointerUp);
                _canvas.UnregisterCallback<PointerCaptureOutEvent>(OnMinimapPointerCaptureOut);
            }

            EndMinimapDrag();
        }

        void LateUpdate()
        {
            if (_matchRuntime == null)
            {
                _matchRuntime = MatchRuntime.Current;
            }

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
                _viewportElement?.ClearCorners();
                return;
            }

            if (controller != _topologyController)
            {
                _topologyController = controller;
                _topology = MatchMinimapTopologyBuilder.Build(controller.Layout, controller.Graph);
            }

            UpdateGeometry();
            UpdateViewportOverlay(controller.Layout.ArenaRadius);

            if (_fogOfWar == null && _matchRuntime != null)
            {
                _fogOfWar = _matchRuntime.FogOfWar;
            }

            var localSlot = MatchNetworkSession.LocalSlot >= 0
                ? MatchNetworkSession.LocalSlot
                : (GameSession.ActiveSetup?.LocalPlayerSlot ?? 0);

            var arenaRadius = controller.Layout.ArenaRadius;
            var selection = _matchRuntime.Selection;
            var activeKeys = new HashSet<string>();

            for (var slot = 0; slot < controller.Layout.Slots.Count; slot++)
            {
                var key = GetBaseKey(slot);
                activeKeys.Add(key);
                var blip = GetOrCreateBlip(key, BlipBaseClass);
                var slotLayout = controller.Layout.Slots[slot];
                PlaceBlip(
                    blip,
                    slotLayout.BasePosition,
                    arenaRadius,
                    9f,
                    MatchMinimapProjection.YawDegrees(slotLayout.BaseRotation));
                blip.style.backgroundColor = MatchPlayerColors.GetSlotColor(slot);
                blip.EnableInClassList(BlipSelectedClass, false);
            }

            foreach (var building in controller.Buildings.Buildings)
            {
                var key = GetBuildingKey(building.InstanceId);
                activeKeys.Add(key);
                var blip = GetOrCreateBlip(key, BlipBuildingClass);
                var yaw = 0f;
                if (building.OwnerSlot >= 0 && building.OwnerSlot < controller.Layout.Slots.Count)
                {
                    yaw = MatchMinimapProjection.YawDegrees(
                        controller.Layout.Slots[building.OwnerSlot].BaseRotation);
                }

                PlaceBlip(blip, building.WorldPosition, arenaRadius, 7f, yaw);
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

                if (!ShouldDrawUnitBlip(unit, localSlot, combat))
                {
                    continue;
                }

                var key = GetUnitKey(unit.UnitId);
                activeKeys.Add(key);
                var blip = GetOrCreateBlip(key, string.Empty);
                var unitPos = unit.WorldPosition;
                if (combat.TryGetUnitWorldPosition(unit, out var livePos))
                {
                    unitPos = livePos;
                }

                PlaceBlip(blip, unitPos, arenaRadius, 5f);
                blip.style.backgroundColor = MatchPlayerColors.GetSlotColor(unit.OwnerSlot);
                blip.EnableInClassList(
                    BlipSelectedClass,
                    selection != null
                    && selection.Current.IsUnit
                    && selection.Current.EntityId == unit.UnitId);
            }

            RemoveStaleBlips(activeKeys);
        }

        void OnMinimapPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !TryGetActiveArenaRadius(out _))
            {
                return;
            }

            _isDraggingMinimap = true;
            _canvas.CapturePointer(evt.pointerId);
            BeginPanInputLock();
            SnapCameraToPanelPosition(evt.localPosition);
            evt.StopPropagation();
        }

        void OnMinimapPointerMove(PointerMoveEvent evt)
        {
            if (!_isDraggingMinimap)
            {
                return;
            }

            SnapCameraToPanelPosition(evt.localPosition);
            evt.StopPropagation();
        }

        void OnMinimapPointerUp(PointerUpEvent evt)
        {
            if (!_isDraggingMinimap || evt.button != 0)
            {
                return;
            }

            SnapCameraToPanelPosition(evt.localPosition);
            if (_canvas.HasPointerCapture(evt.pointerId))
            {
                _canvas.ReleasePointer(evt.pointerId);
            }

            EndMinimapDrag();
            evt.StopPropagation();
        }

        void OnMinimapPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            EndMinimapDrag();
        }

        void SnapCameraToPanelPosition(Vector2 localPanelPosition)
        {
            if (!TryGetActiveArenaRadius(out var arenaRadius))
            {
                return;
            }

            var pan = GameplayCameraPanController.Current;
            if (pan == null)
            {
                return;
            }

            var world = MatchMinimapProjection.PanelToWorld(
                localPanelPosition,
                _panelWidth,
                _panelHeight,
                MatchMinimapProjection.MapHalfExtent(arenaRadius),
                pan.YawDegrees);
            pan.FocusOnMinimapPosition(world);
        }

        bool TryGetActiveArenaRadius(out float arenaRadius)
        {
            arenaRadius = MatchArenaGenerator.DefaultArenaRadius;
            var controller = _matchRuntime != null ? _matchRuntime.Controller : null;
            if (controller == null || !controller.IsRunning || controller.Layout == null)
            {
                return false;
            }

            arenaRadius = controller.Layout.ArenaRadius;
            return true;
        }

        void BeginPanInputLock()
        {
            var pan = GameplayCameraPanController.Current;
            if (pan == null || _heldPanInputLock)
            {
                return;
            }

            pan.SetPanInputLocked(true);
            _heldPanInputLock = true;
        }

        void EndMinimapDrag()
        {
            _isDraggingMinimap = false;
            if (!_heldPanInputLock)
            {
                return;
            }

            GameplayCameraPanController.Current?.SetPanInputLocked(false);
            _heldPanInputLock = false;
        }

        void UpdateViewportOverlay(float arenaRadius)
        {
            if (_viewportElement == null)
            {
                return;
            }

            var camera = CameraCache.Main;
            if (camera == null
                || !GameplayCameraGroundView.TryGetGroundFrustumCorners(camera, _groundCorners))
            {
                _viewportElement.ClearCorners();
                return;
            }

            // Exact frustum trapezoid (no AABB). Unclamped panel mapping avoids edge pull-in.
            var c0 = WorldToPanel(_groundCorners[0], arenaRadius);
            var c1 = WorldToPanel(_groundCorners[1], arenaRadius);
            var c2 = WorldToPanel(_groundCorners[2], arenaRadius);
            var c3 = WorldToPanel(_groundCorners[3], arenaRadius);
            _viewportElement.SetCorners(c0, c1, c2, c3);
        }

        Vector2 WorldToPanel(Vector3 worldPosition, float arenaRadius)
        {
            // Unclamped: far frustum corners past the arena must not be pulled onto the map
            // edge (that artificially inflates the viewport overlay).
            var yaw = GetViewYawDegrees();
            var normalized = MatchMinimapProjection.WorldToNormalizedUnclamped(
                worldPosition,
                MatchMinimapProjection.MapHalfExtent(arenaRadius),
                yaw);
            return MatchMinimapProjection.NormalizedToPanel(normalized, _panelWidth, _panelHeight);
        }

        static float GetViewYawDegrees() =>
            GameplayCameraPanController.Current != null
                ? GameplayCameraPanController.Current.YawDegrees
                : 0f;

        bool ShouldDrawUnitBlip(MatchUnitState unit, int localSlot, MatchCombatSystem combat)
        {
            if (_fogOfWar == null || !_fogOfWar.IsInitialized || _fogOfWar.FogDisabled)
            {
                return true;
            }

            if (unit.OwnerSlot == localSlot)
            {
                return true;
            }

            var position = unit.WorldPosition;
            if (combat != null && combat.TryGetUnitWorldPosition(unit, out var livePos))
            {
                position = livePos;
            }

            return _fogOfWar.IsRevealed(position);
        }

        void UpdateGeometry()
        {
            if (_geometryElement == null)
            {
                return;
            }

            var arenaRadius = _topologyController?.Layout?.ArenaRadius ?? MatchArenaGenerator.DefaultArenaRadius;
            var yaw = GetViewYawDegrees();
            var sizeChanged = !Mathf.Approximately(_lastGeometryWidth, _panelWidth)
                || !Mathf.Approximately(_lastGeometryHeight, _panelHeight);
            var yawChanged = float.IsNaN(_lastGeometryYaw) || !Mathf.Approximately(_lastGeometryYaw, yaw);
            if (!sizeChanged && !yawChanged && ReferenceEquals(_topology, _lastDrawnTopology))
            {
                return;
            }

            _lastGeometryWidth = _panelWidth;
            _lastGeometryHeight = _panelHeight;
            _lastGeometryYaw = yaw;
            _lastDrawnTopology = _topology;
            _geometryElement.SetDrawData(_topology, arenaRadius, _panelWidth, _panelHeight, yaw);
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

            blip.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f));
            _canvas.Add(blip);
            _blips[key] = blip;
            return blip;
        }

        void PlaceBlip(
            VisualElement blip,
            Vector3 worldPosition,
            float arenaRadius,
            float size,
            float worldYawDegrees = 0f)
        {
            var viewYaw = GetViewYawDegrees();
            var normalized = MatchMinimapProjection.WorldToNormalized(
                worldPosition,
                MatchMinimapProjection.MapHalfExtent(arenaRadius),
                viewYaw);
            var panelPosition = MatchMinimapProjection.NormalizedToPanel(normalized, _panelWidth, _panelHeight);
            blip.style.left = panelPosition.x - size * 0.5f;
            blip.style.top = panelPosition.y - size * 0.5f;
            blip.style.width = size;
            blip.style.height = size;
            blip.style.rotate = new Rotate(
                Angle.Degrees(MatchMinimapProjection.BlipRotateDegrees(worldYawDegrees, viewYaw)));
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
