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
    }
}
