using UnityEngine;

namespace Game.Gameplay.Data
{
    /// <summary>
    /// Balance settings stored directly on the unit prefab. When present at match runtime,
    /// these values override the race's <see cref="UnitDefinition"/> so balance can be
    /// tuned by editing the prefab alone. Populated by the editor menu
    /// BARAKI/Units/Sync Balance to Prefabs.
    /// </summary>
    public sealed class UnitBalanceSettings : MonoBehaviour
    {
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private float _armor;
        [SerializeField] private float _damageMin = 8f;
        [SerializeField] private float _damageMax = 10f;
        [SerializeField] private float _attackSpeed = 1f;
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private int _goldBounty = 8;
        [SerializeField] private float _maxMana;
        [SerializeField] private float _marchSpeedOverride;

        public float MaxHp => _maxHp;
        public float Armor => _armor;
        public float DamageMin => _damageMin;
        public float DamageMax => _damageMax;
        public float AttackSpeed => _attackSpeed;
        public float AttackRange => _attackRange;
        public float MoveSpeed => _moveSpeed;
        public int GoldBounty => _goldBounty;
        public float MaxMana => _maxMana;
        public float MarchSpeedOverride => _marchSpeedOverride;

        public void CopyFrom(UnitDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _maxHp = definition.MaxHp;
            _armor = definition.Armor;
            _damageMin = definition.DamageMin;
            _damageMax = definition.DamageMax;
            _attackSpeed = definition.AttackSpeed;
            _attackRange = definition.AttackRange;
            _moveSpeed = definition.MoveSpeed;
            _goldBounty = definition.GoldBounty;
            _maxMana = definition.MaxMana;
            _marchSpeedOverride = definition.MarchSpeedOverride;
        }

        public void CopyFrom(HeroDefinition definition, float hpArmorDamageMultiplier = 1f)
        {
            if (definition == null)
            {
                return;
            }

            var scale = hpArmorDamageMultiplier > 0f ? hpArmorDamageMultiplier : 1f;
            _maxHp = definition.MaxHp * scale;
            _armor = definition.Armor * scale;
            _damageMin = definition.DamageMin * scale;
            _damageMax = definition.DamageMax * scale;
            _attackSpeed = definition.AttackSpeed;
            _attackRange = definition.AttackRange;
            _moveSpeed = definition.MoveSpeed;
            _goldBounty = definition.GoldBounty;
            _maxMana = 0f;
            _marchSpeedOverride = 0f;
        }

        public void CopyFrom(UnitBalanceSettings other)
        {
            if (other == null)
            {
                return;
            }

            _maxHp = other._maxHp;
            _armor = other._armor;
            _damageMin = other._damageMin;
            _damageMax = other._damageMax;
            _attackSpeed = other._attackSpeed;
            _attackRange = other._attackRange;
            _moveSpeed = other._moveSpeed;
            _goldBounty = other._goldBounty;
            _maxMana = other._maxMana;
            _marchSpeedOverride = other._marchSpeedOverride;
        }
    }
}
