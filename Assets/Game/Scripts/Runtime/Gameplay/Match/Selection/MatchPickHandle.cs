using UnityEngine;

namespace Game.Gameplay.Match.Selection
{
    public sealed class MatchPickHandle : MonoBehaviour
    {
        [SerializeField] private MatchPickTargetKind _kind;
        [SerializeField] private int _entityId;

        public MatchPickTarget Target => _kind switch
        {
            MatchPickTargetKind.Unit => MatchPickTarget.Unit(_entityId),
            MatchPickTargetKind.Building => MatchPickTarget.Building(_entityId),
            _ => MatchPickTarget.None,
        };

        public void ConfigureUnit(int unitId)
        {
            _kind = MatchPickTargetKind.Unit;
            _entityId = unitId;
        }

        public void ConfigureBuilding(int buildingInstanceId)
        {
            _kind = MatchPickTargetKind.Building;
            _entityId = buildingInstanceId;
        }
    }
}
