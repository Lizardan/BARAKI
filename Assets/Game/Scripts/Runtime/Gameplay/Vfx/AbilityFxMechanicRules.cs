using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using UnityEngine;

namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Combat shape for BARAKI Studio: aura that follows the bearer versus a stamped
    /// ground area versus a one-shot burst versus a single-unit point.
    /// </summary>
    public enum AbilityFxMechanicShape
    {
        PointOnSelf = 0,
        PointOnTarget = 1,
        AuraAroundSelf = 2,
        AreaOnGround = 3,
        BurstAroundSelf = 4,
        BurstAroundTarget = 5,
        BurstAtImpact = 6,
    }

    /// <summary>Resolved combat mechanic used to author VFX (not the spawn anchor).</summary>
    public readonly struct AbilityFxMechanic
    {
        public AbilityFxMechanic(
            AbilityFxMechanicShape shape,
            float radius,
            float secondaryRadius,
            float reach,
            AbilityVfxAnchor ringHost,
            string title,
            string motion,
            string fxHint,
            Color chipColor)
        {
            Shape = shape;
            Radius = radius;
            SecondaryRadius = secondaryRadius;
            Reach = reach;
            RingHost = ringHost;
            Title = title;
            Motion = motion;
            FxHint = fxHint;
            ChipColor = chipColor;
        }

        public AbilityFxMechanicShape Shape { get; }
        public float Radius { get; }
        public float SecondaryRadius { get; }
        public float Reach { get; }
        public AbilityVfxAnchor RingHost { get; }
        public string Title { get; }
        public string Motion { get; }
        public string FxHint { get; }
        public Color ChipColor { get; }

        public bool FollowsHost => Shape == AbilityFxMechanicShape.AuraAroundSelf;

        public bool ShowRing => Shape is AbilityFxMechanicShape.AuraAroundSelf
            or AbilityFxMechanicShape.AreaOnGround
            or AbilityFxMechanicShape.BurstAroundSelf
            or AbilityFxMechanicShape.BurstAroundTarget
            or AbilityFxMechanicShape.BurstAtImpact;

        public bool FlatOnGround => Shape is AbilityFxMechanicShape.AreaOnGround
            or AbilityFxMechanicShape.BurstAtImpact;

        public string RadiusLabel
        {
            get
            {
                if (ShowRing)
                {
                    return SecondaryRadius > 0.01f
                        ? $"круг {FormatMeters(Radius)} · хил {FormatMeters(SecondaryRadius)}"
                        : $"круг {FormatMeters(Radius)}";
                }

                return Reach > 0.01f
                    ? $"досягаемость {FormatMeters(Reach)}"
                    : "без круга";
            }
        }

        static string FormatMeters(float value) =>
            Mathf.Abs(value - Mathf.Round(value)) < 0.05f
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#");
    }

    /// <summary>Maps ability ids to combat shape, radius and Studio copy.</summary>
    public static class AbilityFxMechanicRules
    {
        public static AbilityFxMechanic Resolve(
            int abilityId,
            float defRadius = 0f,
            float secondaryRadius = 0f,
            float castRange = 0f)
        {
            var shape = ResolveShape(abilityId);
            var radius = ResolveRadius(abilityId, defRadius);
            var secondary = abilityId == AbilityIds.Revive
                ? (secondaryRadius > 0f ? secondaryRadius : HeroAbilityRules.ReviveHealRadius)
                : secondaryRadius;
            if (abilityId != AbilityIds.Revive)
            {
                secondary = 0f;
            }

            var reach = ResolveReach(abilityId, shape, radius, castRange);
            var ringHost = ResolveRingHost(abilityId, shape);
            return new AbilityFxMechanic(
                shape,
                radius,
                secondary,
                reach,
                ringHost,
                TitleOf(shape, abilityId),
                MotionOf(shape),
                HintOf(shape),
                ColorOf(shape));
        }

        public static AbilityFxMechanicShape ResolveShape(int abilityId) => abilityId switch
        {
            AbilityIds.AuraDamagePercent
                or AbilityIds.AuraAttackSpeedPercent
                or AbilityIds.AuraArmorPercent
                or AbilityIds.AuraMaxHpPercent
                or AbilityIds.GreaterColossus
                or AbilityIds.AuraHpRegen => AbilityFxMechanicShape.AuraAroundSelf,

            AbilityIds.Frost
                or AbilityIds.Consecration
                or AbilityIds.Sanctuary
                or AbilityIds.MainIceRing
                or AbilityIds.GreaterHeal => AbilityFxMechanicShape.AreaOnGround,

            AbilityIds.Strike
                or AbilityIds.Ultimate
                or AbilityIds.KingsCommand
                or AbilityIds.Aegis
                or AbilityIds.Slam
                or AbilityIds.Stomp
                or AbilityIds.Heal
                or AbilityIds.Shield
                or AbilityIds.Rally
                or AbilityIds.Revive
                or AbilityIds.MeleeCleave
                or AbilityIds.MainWaveOfLight => AbilityFxMechanicShape.BurstAroundSelf,

            AbilityIds.HolyNova => AbilityFxMechanicShape.BurstAroundTarget,

            AbilityIds.SuperCatapult => AbilityFxMechanicShape.BurstAtImpact,

            AbilityIds.FlyingSpawn
                or AbilityIds.RaiseDrowned => AbilityFxMechanicShape.PointOnSelf,

            AbilityIds.VoidDrain
                or AbilityIds.AreaOfMiss => AbilityFxMechanicShape.AreaOnGround,

            AbilityIds.AncientMantle => AbilityFxMechanicShape.BurstAroundSelf,

            AbilityIds.FeastZone
                or AbilityIds.AuraOfHunger => AbilityFxMechanicShape.AuraAroundSelf,

            _ => AbilityFxMechanicShape.PointOnTarget,
        };

        public static float ResolveRadius(int abilityId, float defRadius)
        {
            if (defRadius > 0f)
            {
                return defRadius;
            }

            return abilityId switch
            {
                AbilityIds.AuraDamagePercent
                    or AbilityIds.AuraAttackSpeedPercent
                    or AbilityIds.AuraArmorPercent
                    or AbilityIds.AuraMaxHpPercent
                    or AbilityIds.GreaterColossus
                    or                 AbilityIds.AuraHpRegen
                    or AbilityIds.AuraOfHunger => HeroAbilityRules.AuraRadius,
                AbilityIds.Strike => HeroAbilityRules.StrikeRadius,
                AbilityIds.Ultimate => HeroAbilityRules.UltimateRadius,
                AbilityIds.KingsCommand => HeroAbilityRules.UltimateRadius,
                AbilityIds.Frost => CasterSpellRules.FrostRadius,
                AbilityIds.Heal => HeroAbilityRules.HealRadius,
                AbilityIds.Shield => HeroAbilityRules.ShieldRadius,
                AbilityIds.Aegis => HeroAbilityRules.ShieldRadius,
                AbilityIds.Consecration => HeroAbilityRules.ConsecrationRadius,
                AbilityIds.HolyNova => HeroAbilityRules.NovaRadius,
                AbilityIds.GreaterHeal => HeroAbilityRules.GreaterHealRadius,
                AbilityIds.Revive => HeroAbilityRules.ReviveRadius,
                AbilityIds.Slam => HeroAbilityRules.SlamRadius,
                AbilityIds.Stomp => HeroAbilityRules.StompRadius,
                AbilityIds.Rally => HeroAbilityRules.RallyRadius,
                AbilityIds.Sanctuary => HeroAbilityRules.GreaterHealRadius,
                AbilityIds.MeleeCleave => HumanBonusUnitRules.MeleeAoeRadius,
                AbilityIds.SuperCatapult => HumanBonusUnitRules.CatapultAoeRadius,
                AbilityIds.MainBuildingSmite => 2.2f,
                AbilityIds.MainIceRing => BuildingAbilityRules.IceRingRadius,
                // Wave radius is dynamic (base→barracks × factor); Studio shows a placeholder.
                AbilityIds.MainWaveOfLight => 20f,
                _ => 0f,
            };
        }

        /// <summary>
        /// Studio ring in world metres — same units as combat <c>Radius</c> and unit presenter scale.
        /// </summary>
        public static float PreviewRingRadius(float combatRadius) =>
            combatRadius > 0.01f ? combatRadius : 0f;

        static float ResolveReach(
            int abilityId,
            AbilityFxMechanicShape shape,
            float radius,
            float castRange)
        {
            if (shape is AbilityFxMechanicShape.PointOnSelf or AbilityFxMechanicShape.PointOnTarget)
            {
                if (castRange > 0f)
                {
                    return castRange;
                }

                return abilityId switch
                {
                    AbilityIds.Smite => radius > 0f ? radius : HeroAbilityRules.SmiteRadius,
                    AbilityIds.CasterHeal or AbilityIds.Resurrect =>
                        CasterSpellRules.CastRange,
                    AbilityIds.HolyNova => HeroAbilityRules.NovaCastRange,
                    AbilityIds.MainBuildingSmite or AbilityIds.MainUnitSmite => 0f,
                    _ => radius,
                };
            }

            return 0f;
        }

        static AbilityVfxAnchor ResolveRingHost(int abilityId, AbilityFxMechanicShape shape) =>
            shape switch
            {
                AbilityFxMechanicShape.BurstAroundTarget => AbilityVfxAnchor.Target,
                AbilityFxMechanicShape.BurstAtImpact => AbilityVfxAnchor.Impact,
                AbilityFxMechanicShape.AreaOnGround when abilityId == AbilityIds.Frost =>
                    AbilityVfxAnchor.Target,
                AbilityFxMechanicShape.AreaOnGround => AbilityVfxAnchor.Ground,
                AbilityFxMechanicShape.AuraAroundSelf
                    or AbilityFxMechanicShape.BurstAroundSelf => AbilityVfxAnchor.Caster,
                _ => AbilityVfxAnchor.Unspecified,
            };

        static string TitleOf(AbilityFxMechanicShape shape, int abilityId) => shape switch
        {
            AbilityFxMechanicShape.AuraAroundSelf => "Аура вокруг себя",
            AbilityFxMechanicShape.AreaOnGround when abilityId == AbilityIds.Frost =>
                "Область на земле у врагов",
            AbilityFxMechanicShape.AreaOnGround => "Область на земле",
            AbilityFxMechanicShape.BurstAroundSelf => "Вспышка вокруг себя",
            AbilityFxMechanicShape.BurstAroundTarget => "Область у цели",
            AbilityFxMechanicShape.BurstAtImpact => "Вспышка в точке удара",
            AbilityFxMechanicShape.PointOnSelf => "Точка на себе",
            _ => "Точка на цели",
        };

        static string MotionOf(AbilityFxMechanicShape shape) => shape switch
        {
            AbilityFxMechanicShape.AuraAroundSelf => "едет с носителем",
            AbilityFxMechanicShape.AreaOnGround => "остаётся на земле",
            AbilityFxMechanicShape.BurstAroundSelf => "разовая вспышка у себя",
            AbilityFxMechanicShape.BurstAroundTarget => "разовая вспышка у цели",
            AbilityFxMechanicShape.BurstAtImpact => "разовая вспышка, не едет",
            AbilityFxMechanicShape.PointOnSelf => "без круга — на себе",
            _ => "без круга — один юнит",
        };

        static string HintOf(AbilityFxMechanicShape shape) => shape switch
        {
            AbilityFxMechanicShape.AuraAroundSelf =>
                "Бери loop / magic aura: кольцо живёт на носителе и едет с ним.",
            AbilityFxMechanicShape.AreaOnGround =>
                "Бери ground ring / лёд / consecration: круг на земле в кадр каста, не следует.",
            AbilityFxMechanicShape.BurstAroundSelf =>
                "Бери удар / nova вокруг кастера: разовый круг у себя.",
            AbilityFxMechanicShape.BurstAroundTarget =>
                "Бери вспышку вокруг цели или якоря, не на земле кастера.",
            AbilityFxMechanicShape.BurstAtImpact =>
                "Бери splash / explosion там, куда прилетело.",
            AbilityFxMechanicShape.PointOnSelf =>
                "Бери эффект на себе, без AoE-круга.",
            _ =>
                "Бери hit / heal на одном юните, без AoE-круга.",
        };

        static Color ColorOf(AbilityFxMechanicShape shape) => shape switch
        {
            AbilityFxMechanicShape.AuraAroundSelf => new Color(0.72f, 0.46f, 0.95f, 1f),
            AbilityFxMechanicShape.AreaOnGround => new Color(0.28f, 0.78f, 0.72f, 1f),
            AbilityFxMechanicShape.BurstAroundSelf => new Color(0.95f, 0.74f, 0.32f, 1f),
            AbilityFxMechanicShape.BurstAroundTarget => new Color(0.95f, 0.56f, 0.30f, 1f),
            AbilityFxMechanicShape.BurstAtImpact => new Color(0.95f, 0.40f, 0.34f, 1f),
            _ => new Color(0.58f, 0.66f, 0.76f, 1f),
        };
    }
}
