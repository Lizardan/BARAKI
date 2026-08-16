using System.Collections.Generic;
using Game.Gameplay.Data;
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

    /// <summary>How a cast is presented (ring / burst / plus / combinations).</summary>
    public enum FxKind
    {
        Plus = 0,
        Ring = 1,
        Burst = 2,
        RingPlus = 3,
        RingBurst = 4,
        /// <summary>Vertical beam from sky into the impact point (main Divine Blessing smites).</summary>
        SkyBeam = 5,
    }

    /// <summary>VFX hint for a cast; zero duration/height means "use the kind default".</summary>
    [System.Serializable]
    public struct AbilityFx
    {
        public FxKind Kind;
        public Color Color;
        public float DurationSeconds;
        public float BurstHeight;

        public static AbilityFx Ring(Color color) =>
            new() { Kind = FxKind.Ring, Color = color };

        public static AbilityFx Ring(Color color, float duration) =>
            new() { Kind = FxKind.Ring, Color = color, DurationSeconds = duration };

        public static AbilityFx RingPlus(Color color, float duration = 0f) =>
            new() { Kind = FxKind.RingPlus, Color = color, DurationSeconds = duration };

        public static AbilityFx RingBurst(Color color, float ringDuration = 0f, float burstHeight = 0f) =>
            new() { Kind = FxKind.RingBurst, Color = color, DurationSeconds = ringDuration, BurstHeight = burstHeight };

        public static AbilityFx Burst(Color color) =>
            new() { Kind = FxKind.Burst, Color = color };

        public static AbilityFx Plus(Color color) =>
            new() { Kind = FxKind.Plus, Color = color };

        public static AbilityFx SkyBeam(Color color, float duration = 1.1f, float height = 36f) =>
            new() { Kind = FxKind.SkyBeam, Color = color, DurationSeconds = duration, BurstHeight = height };

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
