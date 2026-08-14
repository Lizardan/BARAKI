using System;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Data
{
    [Serializable]
    public sealed class UnitAbilitySlot
    {
        [SerializeField] private AbilityType _type;
        [SerializeField] private AbilityKind _kind;
        [SerializeField] private AbilityUnlock _unlock;
        [SerializeField] private int _unlockValue = 1;
        [SerializeField] private string _displayName = "";
        [SerializeField] [TextArea(2, 6)] private string _description = "";
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

        public AbilityType Type => _type;
        public AbilityKind Kind => _kind;
        public AbilityUnlock Unlock => _unlock;
        public int UnlockValue => _unlockValue;
        public string DisplayName => _displayName;
        public string Description => _description;
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

        public HeroAbilityType AsHeroAbility() => (HeroAbilityType)(int)_type;

        public UnitAbilitySlot Clone()
        {
            return (UnitAbilitySlot)MemberwiseClone();
        }

        public static UnitAbilitySlot Create(
            AbilityType type,
            AbilityKind kind,
            AbilityUnlock unlock,
            int unlockValue,
            string displayName,
            string description,
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
            return new UnitAbilitySlot
            {
                _type = type,
                _kind = kind,
                _unlock = unlock,
                _unlockValue = unlockValue,
                _displayName = displayName,
                _description = description,
                _damage = damage,
                _heal = heal,
                _healPerSecond = healPerSecond,
                _radius = radius,
                _castRange = castRange,
                _cooldownSeconds = cooldownSeconds,
                _durationSeconds = durationSeconds,
                _percent = percent,
                _manaCost = manaCost,
                _stunSeconds = stunSeconds,
                _flatBonus = flatBonus,
                _secondaryRadius = secondaryRadius,
                _secondaryHeal = secondaryHeal,
            };
        }
    }
}
