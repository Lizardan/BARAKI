using Game.Core;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Editor debug: draws barracks unit spawn points and random spawn areas.</summary>
    public sealed class BarracksSpawnDebugOverlay : MonoBehaviour
    {
        public static bool IsVisible { get; set; }

        [SerializeField] private MatchRuntime _runtime;
        [SerializeField] private Color _pointColor = new(0.2f, 1f, 0.35f, 1f);
        [SerializeField] private Color _areaColor = new(0.2f, 1f, 0.35f, 0.85f);
        [SerializeField] private float _pointRadius = 0.35f;
        [SerializeField] private float _yOffset = 0.2f;

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

                var region = BarracksSpawnDebugRules.BuildRegion(lane.Path);
                var spawnPoint = region.SpawnPoint;
                spawnPoint.y = _yOffset;
                var areaCenter = region.AreaCenter;
                areaCenter.y = _yOffset;

                Gizmos.color = _pointColor;
                Gizmos.DrawSphere(spawnPoint, _pointRadius);

                region.GetAreaCorners(out var fl, out var fr, out var br, out var bl);
                fl.y = _yOffset;
                fr.y = _yOffset;
                br.y = _yOffset;
                bl.y = _yOffset;

                Gizmos.color = _areaColor;
                Gizmos.DrawLine(fl, fr);
                Gizmos.DrawLine(fr, br);
                Gizmos.DrawLine(br, bl);
                Gizmos.DrawLine(bl, fl);
                Gizmos.DrawLine(fl, br);
                Gizmos.DrawLine(fr, bl);
                Gizmos.DrawLine(areaCenter, spawnPoint);
            }
#endif
        }
    }
}
