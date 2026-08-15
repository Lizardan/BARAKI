using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// Passive army aura: while the owning champion is alive and the slot is unlocked,
    /// the strongest matching stat percent across living owner units applies (see <see cref="QueryAura"/>).
    /// Never casts.
    /// </summary>
    public sealed class AuraBehaviour : UnitAbilityBehaviour
    {
        [SerializeField] private AuraStat _stat;

        public void Configure(AuraStat stat)
        {
            _stat = stat;
        }

        public AuraStat Stat => _stat;

        public override bool TryCast(in UnitAbilityContext ctx) => false;

        public override float QueryAura(in UnitAbilityContext ctx, AuraStat stat)
        {
            return stat == _stat ? ctx.Def.Percent : 0f;
        }

        public override string DescribeParams() => _stat switch
        {
            AuraStat.Damage => "Пассивно: +урон армии владельца",
            AuraStat.AttackSpeed => "Пассивно: +скорость атаки армии владельца",
            AuraStat.Armor => "Пассивно: +броня армии владельца",
            AuraStat.MaxHp => "Пассивно: +запас здоровья армии владельца",
            _ => string.Empty,
        };
    }
}
