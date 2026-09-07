using Game.Core;
using Game.Gameplay.Combat;

namespace Game.Gameplay.Data
{
    /// <summary>
    /// Lightweight combat identity of a unit — race + role (+ hero/bonus slot).
    /// Behavior that differs per race (melee-vs-ranged delivery, min attack range,
    /// which target roles are attackable, vfx flavour, …) is expressed HERE once,
    /// instead of spreading <c>if (raceId == X)</c> checks through the combat loop.
    /// <para>
    /// The <see cref="UnitRole"/> enum itself is unchanged (it's baked into snapshots,
    /// the wire contract, catalogs and UI) — these rules layer race-specific behaviour
    /// on top of it. Build one via <c>UnitCombatIdentityFactory</c> in Game.Gameplay.Combat.
    /// </para>
    /// </summary>
    public readonly struct UnitCombatIdentity
    {
        public string RaceId { get; }

        /// <summary>Base combat role (same enum the snapshot/wire uses).</summary>
        public UnitRole Role { get; }

        public bool IsHero { get; }

        /// <summary>1-based hero slot; 0 for non-heroes/titan.</summary>
        public int HeroSlot { get; }

        /// <summary>0 = base variant; otherwise a Human bonus/veteran slot.</summary>
        public int BonusSlot { get; }

        public UnitCombatIdentity(
            string raceId,
            UnitRole role,
            bool isHero = false,
            int heroSlot = 0,
            int bonusSlot = 0)
        {
            RaceId = raceId;
            Role = role;
            IsHero = isHero;
            HeroSlot = heroSlot;
            BonusSlot = bonusSlot;
        }

        /// <summary>True when this unit belongs to the Faceless (Древние) race.</summary>
        public bool IsFaceless => RaceId == GameIds.Races.Faceless;

        // ---- Delivery type (melee strike vs projectile) -------------------------------

        /// <summary>True when this unit auto-attacks with a melee strike (no projectile).</summary>
        public bool UsesMeleeStrike
        {
            get
            {
                if (IsHero)
                {
                    return HeroSlot != HeroAbilityRules.PriestSlot;
                }

                return IsFacelessMeleeDelivery
                    || CombatAttackRules.UsesMeleeStrike(Role);
            }
        }

        /// <summary>True when this unit auto-attacks with a ranged projectile.</summary>
        public bool UsesProjectile
        {
            get
            {
                if (IsHero && HeroSlot == HeroAbilityRules.PriestSlot)
                {
                    return true;
                }

                return !IsFacelessMeleeDelivery
                    && CombatAttackRules.UsesProjectile(Role);
            }
        }

        // Faceless (Древние) turn their Super (крип) and Flying into melee fighters:
        // strike in close range with no minimum-arc-band, no projectile, and Flying can
        // still hit flying targets. Everything else keeps the Discrete role behaviour.
        bool IsFacelessMeleeDelivery =>
            IsFaceless && (Role is UnitRole.Super or UnitRole.Flying);

        /// <summary>
        /// Minimum fireable distance (0 for typical melee). Faceless Super drops the
        /// artillery dead-zone so it can fight point-blank.
        /// </summary>
        public float GetMinAttackRange() =>
            IsFaceless && Role == UnitRole.Super
                ? 0f
                : CombatRules.GetMinAttackRange(Role);

        /// <summary>
        /// Can this unit attack a target of <paramref name="targetRole"/>? Faceless Flying
        /// stays able to hit flying targets even though it fights in melee.
        /// </summary>
        public bool CanAttackTarget(UnitRole targetRole)
        {
            if (targetRole != UnitRole.Flying)
            {
                return true;
            }

            if (Role == UnitRole.Flying && IsFaceless)
            {
                return true;
            }

            return CombatRules.CanAttackTarget(Role, targetRole);
        }
    }
}
