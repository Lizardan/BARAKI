using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    public readonly struct UnitCombatStats
    {
        public UnitCombatStats(
            UnitRole role,
            float maxHp,
            float armor,
            float damageMin,
            float damageMax,
            float attackSpeed,
            float attackRange,
            float moveSpeed,
            int goldBounty,
            float maxMana = 0f)
        {
            Role = role;
            MaxHp = maxHp;
            Armor = armor;
            DamageMin = damageMin;
            DamageMax = damageMax;
            AttackSpeed = attackSpeed;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            GoldBounty = goldBounty;
            MaxMana = maxMana;
        }

        public UnitRole Role { get; }
        public float MaxHp { get; }
        public float Armor { get; }
        public float DamageMin { get; }
        public float DamageMax { get; }
        public float AttackSpeed { get; }
        public float AttackRange { get; }
        public float MoveSpeed { get; }
        public int GoldBounty { get; }
        public float MaxMana { get; }
        public bool HasMana => MaxMana > 0f;

        public static UnitCombatStats FromDefinition(UnitDefinition definition)
        {
            if (definition == null)
            {
                throw new System.ArgumentNullException(nameof(definition));
            }

            return new UnitCombatStats(
                definition.Role,
                definition.MaxHp,
                definition.Armor,
                definition.DamageMin,
                definition.DamageMax,
                definition.AttackSpeed,
                definition.AttackRange,
                definition.MoveSpeed,
                definition.GoldBounty,
                ResolveMaxMana(definition));
        }

        static float ResolveMaxMana(UnitDefinition definition)
        {
            if (definition.MaxMana > 0f)
            {
                return definition.MaxMana;
            }

            return definition.Role == UnitRole.Caster ? 200f : 0f;
        }
    }
}
