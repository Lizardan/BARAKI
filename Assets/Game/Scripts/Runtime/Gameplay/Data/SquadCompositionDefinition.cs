using UnityEngine;

namespace Game.Gameplay.Data
{
    [CreateAssetMenu(fileName = "SquadComposition", menuName = "Game/Squad Composition")]
    public sealed class SquadCompositionDefinition : ScriptableObject
    {
        [SerializeField] private int _barracksLevel = 1;
        [SerializeField] private int _meleeCount;
        [SerializeField] private int _rangedCount;
        [SerializeField] private int _casterCount;
        [SerializeField] private int _siegeCount;
        [SerializeField] private int _flyingCount;
        [SerializeField] private int _superCount;

        public int BarracksLevel => _barracksLevel;
        public int MeleeCount => _meleeCount;
        public int RangedCount => _rangedCount;
        public int CasterCount => _casterCount;
        public int SiegeCount => _siegeCount;
        public int FlyingCount => _flyingCount;
        public int SuperCount => _superCount;
        public int TotalUnits => _meleeCount + _rangedCount + _casterCount + _siegeCount + _flyingCount + _superCount;
    }
}
