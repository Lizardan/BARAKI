using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Editor debug: draws lane path waypoints and segments.</summary>
    public sealed class LaneWaypointDebugOverlay : MonoBehaviour
    {
        public static bool IsVisible { get; set; }

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private Color _pointColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color _lineColor = new(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private float _pointRadius = 0.28f;
        [SerializeField] private float _yOffset = 0.25f;

        readonly System.Collections.Generic.List<Vector3> _waypointBuffer = new(64);

        void Awake()
        {
            if (_runtime == null)
            {
                _runtime = GetComponent<MatchRuntime>();
            }
        }

        void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!IsVisible)
            {
                return;
            }

            var controller = _runtime != null ? _runtime.Controller : null;
            if (controller == null || !controller.IsRunning || controller.Graph == null)
            {
                return;
            }

            foreach (var lane in controller.Graph.Lanes)
            {
                if (lane?.Path == null)
                {
                    continue;
                }

                if (lane.LaneId is not (GameIds.Lanes.Left or GameIds.Lanes.Center or GameIds.Lanes.Right))
                {
                    continue;
                }

                _waypointBuffer.Clear();
                LaneWaypointDebugRules.AppendWaypoints(lane.Path, _waypointBuffer);
                if (_waypointBuffer.Count < 2)
                {
                    continue;
                }

                Gizmos.color = _lineColor;
                for (var i = 0; i < _waypointBuffer.Count - 1; i++)
                {
                    var a = _waypointBuffer[i];
                    var b = _waypointBuffer[i + 1];
                    a.y = _yOffset;
                    b.y = _yOffset;
                    Gizmos.DrawLine(a, b);
                }

                Gizmos.color = _pointColor;
                for (var i = 0; i < _waypointBuffer.Count; i++)
                {
                    var point = _waypointBuffer[i];
                    point.y = _yOffset;
                    Gizmos.DrawSphere(point, _pointRadius);
                }
            }
#endif
        }
    }
}
