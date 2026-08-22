using System.Collections.Generic;
using Game.Gameplay.Data;
using Game.Gameplay.Vfx;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Aura stat boosted by a passive ability.</summary>
    public enum AuraStat
    {
        Damage = 0,
        AttackSpeed = 1,
        Armor = 2,
        MaxHp = 3,
        /// <summary>Flat HP/s regeneration (def <see cref="UnitAbilityDef.FlatBonus"/>), not a percent.</summary>
        HpRegen = 4,
    }

    /// <summary>
    /// VFX definition for an ability cast: color for label/tint + optional prefab
    /// (CFXR / Adjustable Slash / Hyper Casual / Custom). If <see cref="VfxPrefab"/> is null, only
    /// the spell label is shown. Authored visuals on def assets survive <c>Build Ability Defs</c>.
    /// </summary>
    [System.Serializable]
    public struct AbilityFx
    {
        public Color Color;
        public GameObject VfxPrefab;
        public AbilityVfxAnchor Anchor;
        public AbilityAnimKind AnimKind;
        /// <summary>Animator state name (Attack / Cast / Stand / Death / …). Empty = <see cref="AbilityAnimRules.ResolveKind"/>.</summary>
        public string AnimState;
        /// <summary>BlendTree child index when <see cref="AnimState"/> is set. Ignored if state is empty.</summary>
        public int AnimVariant;
        /// <summary>Visual-only VFX multiplier. <c>0</c> = unset → 1× (combat radius unchanged).</summary>
        public float Scale;
        /// <summary>Local euler degrees on spawn. <c>(0,0,0)</c> = unset → prefab rotation.</summary>
        public Vector3 Euler;

        /// <summary>
        /// Keeps an already-authored color/prefab/anchor/anim when rebuilding defs from
        /// <c>AbilityKitDefaults</c>. Empty authored fields fall back to <paramref name="defaults"/>.
        /// </summary>
        public AbilityFx WithPreservedAuthored(AbilityFx defaults)
        {
            var state = !string.IsNullOrEmpty(AnimState) ? AnimState : defaults.AnimState;
            return new AbilityFx
            {
                Color = Color.a > 0.01f ? Color : defaults.Color,
                VfxPrefab = VfxPrefab != null ? VfxPrefab : defaults.VfxPrefab,
                Anchor = Anchor != AbilityVfxAnchor.Unspecified ? Anchor : defaults.Anchor,
                AnimKind = AnimKind != AbilityAnimKind.Unspecified ? AnimKind : defaults.AnimKind,
                AnimState = state,
                AnimVariant = !string.IsNullOrEmpty(AnimState) ? AnimVariant : defaults.AnimVariant,
                Scale = Scale > 0.001f ? Scale : defaults.Scale,
                Euler = Euler.sqrMagnitude > 0.0001f ? Euler : defaults.Euler,
            };
        }

        /// <summary>Authored visual scale, or 1 when unset.</summary>
        public static float ResolveScale(float scale) => scale > 0.001f ? scale : 1f;

        /// <summary>Packs a color into a 32-bit RGBA int for snapshot transport.</summary>
        public static int ToRgbaInt(Color color)
        {
            var c = (Color32)color;
            return (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
        }

        /// <summary>Unpacks a color from <see cref="ToRgbaInt"/>.</summary>
        public static Color FromRgbaInt(int packed) =>
            new Color32(
                (byte)((packed >> 24) & 0xFF),
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)(packed & 0xFF));
    }

    /// <summary>Everything a behaviour needs to resolve and apply a single cast.</summary>
    public readonly struct UnitAbilityContext
    {
        public UnitAbilityContext(
            IUnitAbilityHost host,
            MatchUnitState caster,
            UnitAbilityDef def,
            int slotIndex,
            int magicLevel)
        {
            Host = host;
            Caster = caster;
            Def = def;
            SlotIndex = slotIndex;
            MagicLevel = magicLevel;
        }

        public IUnitAbilityHost Host { get; }
        public MatchUnitState Caster { get; }
        public UnitAbilityDef Def { get; }
        public int SlotIndex { get; }
        public int MagicLevel { get; }
    }

    /// <summary>Combat-system surface exposed to behaviours (implemented by <see cref="MatchCombatSystem"/>).</summary>
    public interface IUnitAbilityHost
    {
        IReadOnlyList<MatchUnitState> Units { get; }
        IReadOnlyList<CombatCorpseState> Corpses { get; }

        float GetEffectiveMaxHp(MatchUnitState unit);
        void ApplyDamage(MatchUnitState attacker, MatchUnitState target, float rawDamage, int killerOwnerSlot);
        MatchUnitState FindMostInjuredAlly(IReadOnlyList<MatchUnitState> allies);
        MatchUnitState ResurrectUnit(CombatCorpseState corpse);
        bool HasActiveHealZone(int casterUnitId);
        void ReplaceHealZone(HeroHealZoneState zone);
        void ArmSlotCooldown(MatchUnitState unit, int slotIndex, float seconds);
        void EmitCast(AbilityCastEvent cast);
        int GetMagicLevel(int ownerSlot);
    }

    /// <summary>
    /// Behaviour asset bound to a <see cref="UnitAbilityDef"/>. Tuning lives on the def
    /// (<see cref="UnitAbilityContext.Def"/>); behaviours carry only structural config.
    /// </summary>
    public abstract class UnitAbilityBehaviour : ScriptableObject
    {
        /// <summary>Attempts to cast. Returns true only when the cast fired.</summary>
        public abstract bool TryCast(in UnitAbilityContext ctx);

        /// <summary>Passive aura contribution for <paramref name="stat"/>. Default: none.</summary>
        public virtual float QueryAura(in UnitAbilityContext ctx, AuraStat stat) => 0f;

        /// <summary>Extra tooltip line describing the behaviour-specific effect (default: none).</summary>
        public virtual string DescribeParams() => string.Empty;
    }
}
