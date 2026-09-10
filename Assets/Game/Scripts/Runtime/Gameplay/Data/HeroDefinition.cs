using UnityEngine;

namespace Game.Gameplay.Data
{
    [CreateAssetMenu(fileName = "HeroDefinition", menuName = "Game/Hero Definition")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _raceId;
        [SerializeField] private int _slot = 1;
        [SerializeField] private string _idleMoraleId;
        [SerializeField] private float _maxHp = 600f;
        [SerializeField] private float _armor = 4f;
        [SerializeField] private float _damageMin = 35f;
        [SerializeField] private float _damageMax = 45f;
        [SerializeField] private float _attackSpeed = 1f;
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private int _goldBounty = 80;

        /// <summary>
        /// Authoritative uniform visual scale. 0 means "keep the prefab's baked root scale"
        /// (pre-migration assets); &gt; 0 overrides it everywhere the hero/titan is presented
        /// (<see cref="Game.Gameplay.Match.MatchCombatPresenter"/>,
        /// <see cref="Game.Gameplay.Match.ArenaDuelPresenter"/>).
        /// </summary>
        [SerializeField, Min(0f)] private float _visualScale;

        public string Id => _id;
        public string RaceId => _raceId;
        public int Slot => _slot;
        public string IdleMoraleId => _idleMoraleId;
        public float MaxHp => _maxHp;
        public float Armor => _armor;
        public float DamageMin => _damageMin;
        public float DamageMax => _damageMax;
        public float AttackSpeed => _attackSpeed;
        public float AttackRange => _attackRange;
        public float MoveSpeed => _moveSpeed;
        public int GoldBounty => _goldBounty;
        public float VisualScale => _visualScale;

        /// <summary>Editor migration helper — bakes the authored prefab scale into the asset.</summary>
        public void SetVisualScale(float scale) => _visualScale = Mathf.Max(0f, scale);
    }
}
