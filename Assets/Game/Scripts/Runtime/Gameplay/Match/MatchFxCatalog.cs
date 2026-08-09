using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Prefab catalog for combat and building FX (ToonyTinyPeople TT_RTS).</summary>
    [CreateAssetMenu(fileName = "MatchFxCatalog", menuName = "BARAKI/Match Fx Catalog")]
    public sealed class MatchFxCatalog : ScriptableObject
    {
        [SerializeField] private GameObject _blood;
        [SerializeField] private GameObject _machineDestroyed;
        [SerializeField] private GameObject _buildingDestroyed;
        [SerializeField] private GameObject _buildingBurning;
        [SerializeField] private GameObject _buildingImpact;

        public GameObject Blood => _blood;
        public GameObject MachineDestroyed => _machineDestroyed;
        public GameObject BuildingDestroyed => _buildingDestroyed;
        public GameObject BuildingBurning => _buildingBurning;
        public GameObject BuildingImpact => _buildingImpact;

        public void EditorAssign(
            GameObject blood,
            GameObject machineDestroyed,
            GameObject buildingDestroyed,
            GameObject buildingBurning,
            GameObject buildingImpact)
        {
            _blood = blood;
            _machineDestroyed = machineDestroyed;
            _buildingDestroyed = buildingDestroyed;
            _buildingBurning = buildingBurning;
            _buildingImpact = buildingImpact;
        }
    }
}
