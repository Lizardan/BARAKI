using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Data
{
    /// <summary>
    /// Serialized ability definition: identity, unlock rules, numeric tuning and the behaviour that
    /// resolves casts. Kits reference defs by asset; new abilities need no runtime code beyond a behaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitAbilityDef", menuName = "BARAKI/Ability Def")]
    public sealed class UnitAbilityDef : ScriptableObject
    {
        [SerializeField] private int _abilityId;
        [SerializeField] private string _displayName = "";
        [SerializeField] [TextArea(2, 6)] private string _description = "";
        [SerializeField] private AbilityKind _kind;
        [SerializeField] private AbilityUnlock _unlock;
        [SerializeField] private int _unlockValue = 1;
        [SerializeField] private float _damage;
        [SerializeField] private float _heal;
        [SerializeField] private float _healPerSecond;
        [SerializeField] private float _radius;
        [SerializeField] private float _castRange;
        [SerializeField] private float _cooldownSeconds;
        [SerializeField] private float _durationSeconds;
        [SerializeField] private float _percent;
        [SerializeField] private float _manaCost;
        [SerializeField] private float _stunSeconds;
        [SerializeField] private float _flatBonus;
        [SerializeField] private float _secondaryRadius;
        [SerializeField] private float _secondaryHeal;
        [SerializeField] private AbilityFx _fx = new() { Kind = FxKind.Ring };
        [SerializeField] private UnitAbilityBehaviour _behaviour;

        public int AbilityId => _abilityId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public AbilityKind Kind => _kind;
        public AbilityUnlock Unlock => _unlock;
        public int UnlockValue => _unlockValue;
        public float Damage => _damage;
        public float Heal => _heal;
        public float HealPerSecond => _healPerSecond;
        public float Radius => _radius;
        public float CastRange => _castRange;
        public float CooldownSeconds => _cooldownSeconds;
        public float DurationSeconds => _durationSeconds;
        public float Percent => _percent;
        public float ManaCost => _manaCost;
        public float StunSeconds => _stunSeconds;
        public float FlatBonus => _flatBonus;
        public float SecondaryRadius => _secondaryRadius;
        public float SecondaryHeal => _secondaryHeal;
        public AbilityFx Fx => _fx;
        public UnitAbilityBehaviour Behaviour => _behaviour;

        public bool IsActive => _kind == AbilityKind.Active;
        public bool IsPassiveAura => _kind == AbilityKind.Passive;

        /// <summary>Appends behaviour-specific params to the description tooltip.</summary>
        public string DescribeParams() => Behaviour != null ? Behaviour.DescribeParams() : string.Empty;

        public void Configure(
            int abilityId,
            string displayName,
            string description,
            AbilityKind kind,
            AbilityUnlock unlock,
            int unlockValue,
            UnitAbilityBehaviour behaviour,
            AbilityFx fx = default,
            float damage = 0f,
            float heal = 0f,
            float healPerSecond = 0f,
            float radius = 0f,
            float castRange = 0f,
            float cooldownSeconds = 0f,
            float durationSeconds = 0f,
            float percent = 0f,
            float manaCost = 0f,
            float stunSeconds = 0f,
            float flatBonus = 0f,
            float secondaryRadius = 0f,
            float secondaryHeal = 0f)
        {
            _abilityId = abilityId;
            _displayName = displayName;
            _description = description;
            _kind = kind;
            _unlock = unlock;
            _unlockValue = unlockValue;
            _behaviour = behaviour;
            _fx = fx.Kind == FxKind.Plus && fx.Color == default(Color)
                ? new AbilityFx { Kind = FxKind.Ring }
                : fx;
            _damage = damage;
            _heal = heal;
            _healPerSecond = healPerSecond;
            _radius = radius;
            _castRange = castRange;
            _cooldownSeconds = cooldownSeconds;
            _durationSeconds = durationSeconds;
            _percent = percent;
            _manaCost = manaCost;
            _stunSeconds = stunSeconds;
            _flatBonus = flatBonus;
            _secondaryRadius = secondaryRadius;
            _secondaryHeal = secondaryHeal;
        }

        public static UnitAbilityDef Create(
            int abilityId,
            string displayName,
            string description,
            AbilityKind kind,
            AbilityUnlock unlock,
            int unlockValue,
            UnitAbilityBehaviour behaviour,
            AbilityFx fx = default,
            float damage = 0f,
            float heal = 0f,
            float healPerSecond = 0f,
            float radius = 0f,
            float castRange = 0f,
            float cooldownSeconds = 0f,
            float durationSeconds = 0f,
            float percent = 0f,
            float manaCost = 0f,
            float stunSeconds = 0f,
            float flatBonus = 0f,
            float secondaryRadius = 0f,
            float secondaryHeal = 0f)
        {
            var def = CreateInstance<UnitAbilityDef>();
            def.Configure(
                abilityId,
                displayName,
                description,
                kind,
                unlock,
                unlockValue,
                behaviour,
                fx,
                damage,
                heal,
                healPerSecond,
                radius,
                castRange,
                cooldownSeconds,
                durationSeconds,
                percent,
                manaCost,
                stunSeconds,
                flatBonus,
                secondaryRadius,
                secondaryHeal);
            return def;
        }
    }
}
