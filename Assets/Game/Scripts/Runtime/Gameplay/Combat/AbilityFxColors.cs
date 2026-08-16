using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Shared FX colors per ability family (mirrors the removed presenter color constants).</summary>
    public static class AbilityFxColors
    {
        public static readonly Color Heal = new(0.25f, 1f, 0.4f);
        public static readonly Color Frost = new(0.35f, 0.65f, 1f);
        public static readonly Color Resurrect = new(1f, 0.85f, 0.25f);
        public static readonly Color Strike = new(1f, 0.55f, 0.2f);
        public static readonly Color Ultimate = new(1f, 0.3f, 0.2f);
        public static readonly Color Paladin = new(1f, 0.84f, 0.28f);
        public static readonly Color Priest = new(0.78f, 0.92f, 1f);
        public static readonly Color DivineSmite = new(1f, 0.92f, 0.45f);

        public static readonly Color AuraDamage = new(0.9f, 0.2f, 0.15f);
        /// <summary>Paladin haste — green runic ring (not gold sparkle).</summary>
        public static readonly Color AuraAttackSpeed = new(0.3f, 0.92f, 0.35f);
        public static readonly Color AuraArmor = new(0.55f, 0.72f, 0.95f);
        public static readonly Color AuraMaxHp = new(0.95f, 0.5f, 0.15f);
        /// <summary>Siege / paladin-style regen — holy yellow light (not green mend).</summary>
        public static readonly Color AuraHpRegen = new(1f, 0.88f, 0.35f);
    }
}
