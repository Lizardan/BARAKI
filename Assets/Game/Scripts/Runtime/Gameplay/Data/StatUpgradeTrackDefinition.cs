using UnityEngine;

namespace Game.Gameplay.Data
{
    [CreateAssetMenu(fileName = "StatUpgradeTrack", menuName = "Game/Stat Upgrade Track")]
    public sealed class StatUpgradeTrackDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private float _effectPerLevel = 0.03f;
        [SerializeField] private int _maxLevel = 9;
        [SerializeField] private int[] _costsGold;
        [SerializeField] private float[] _researchTimeSec;

        public string Id => _id;
        public float EffectPerLevel => _effectPerLevel;
        public int MaxLevel => _maxLevel;
        public int[] CostsGold => _costsGold;
        public float[] ResearchTimeSec => _researchTimeSec;
    }
}
