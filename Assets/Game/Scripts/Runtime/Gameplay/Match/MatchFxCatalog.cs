using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Prefab catalog for combat and building FX (ToonyTinyPeople TT_RTS + CFXR auras).</summary>
    [CreateAssetMenu(fileName = "MatchFxCatalog", menuName = "BARAKI/Match Fx Catalog")]
    public sealed class MatchFxCatalog : ScriptableObject
    {
        [Header("Combat FX")]
        [SerializeField] private GameObject _blood;
        [SerializeField] private GameObject _machineDestroyed;
        [SerializeField] private GameObject _buildingDestroyed;
        [SerializeField] private GameObject _buildingBurning;
        [SerializeField] private GameObject _buildingImpact;

        [Header("Aura FX")]
        [SerializeField] private GameObject _auraShinyLoop;
        [SerializeField] private GameObject _auraRunicLoop;

        public GameObject Blood => _blood;
        public GameObject MachineDestroyed => _machineDestroyed;
        public GameObject BuildingDestroyed => _buildingDestroyed;
        public GameObject BuildingBurning => _buildingBurning;
        public GameObject BuildingImpact => _buildingImpact;
        public GameObject AuraShinyLoop => _auraShinyLoop;
        public GameObject AuraRunicLoop => _auraRunicLoop;

        public GameObject GetPassiveAuraPrefab(PassiveAuraFxKind kind) =>
            kind == PassiveAuraFxKind.Shiny ? _auraShinyLoop : _auraRunicLoop;

        public void EditorAssign(
            GameObject blood,
            GameObject machineDestroyed,
            GameObject buildingDestroyed,
            GameObject buildingBurning,
            GameObject buildingImpact,
            GameObject auraShinyLoop = null,
            GameObject auraRunicLoop = null)
        {
            _blood = blood;
            _machineDestroyed = machineDestroyed;
            _buildingDestroyed = buildingDestroyed;
            _buildingBurning = buildingBurning;
            _buildingImpact = buildingImpact;
            if (auraShinyLoop != null)
            {
                _auraShinyLoop = auraShinyLoop;
            }

            if (auraRunicLoop != null)
            {
                _auraRunicLoop = auraRunicLoop;
            }
        }
    }
}
