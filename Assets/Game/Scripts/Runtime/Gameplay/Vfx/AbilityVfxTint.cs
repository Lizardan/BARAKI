using UnityEngine;
using UnityEngine.VFX;

namespace Game.Gameplay.Vfx
{
    /// <summary>
    /// Applies an authored RGB tint to ParticleSystem, Visual Effect Graph and lights.
    /// Slash pack graphs expose <c>FirstColor</c> / <c>SecondColor</c> / <c>ThirdColor</c>.
    /// </summary>
    public static class AbilityVfxTint
    {
        public static void Apply(GameObject fx, Color tint)
        {
            if (fx == null || tint.a <= 0f)
            {
                return;
            }

            ApplyParticles(fx, tint);
            ApplyVisualEffects(fx, tint);
            ApplyLights(fx, tint);
        }

        public static void ApplyParticles(GameObject fx, Color tint)
        {
            if (fx == null)
            {
                return;
            }

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                var main = ps.main;
                main.startColor = RetintGradient(main.startColor, tint);

                var colorOverLifetime = ps.colorOverLifetime;
                if (colorOverLifetime.enabled)
                {
                    colorOverLifetime.color = RetintGradient(colorOverLifetime.color, tint);
                }
            }
        }

        public static void ApplyVisualEffects(GameObject fx, Color tint)
        {
            if (fx == null)
            {
                return;
            }

            var effects = fx.GetComponentsInChildren<VisualEffect>(true);
            if (effects == null || effects.Length == 0)
            {
                return;
            }

            var first = Darken(tint, 0.35f);
            var third = Brighten(tint, 0.28f);
            for (var i = 0; i < effects.Length; i++)
            {
                var vfx = effects[i];
                if (vfx == null)
                {
                    continue;
                }

                TrySetColor(vfx, "FirstColor", first);
                TrySetColor(vfx, "SecondColor", tint);
                TrySetColor(vfx, "ThirdColor", third);
                TrySetColor(vfx, "Fresnel Color", third);
                TrySetColor(vfx, "FresnelColor", third);
            }
        }

        /// <summary>Keep value/alpha from the authored effect; take hue/sat from the tint.</summary>
        public static Color Retint(Color source, Color tint)
        {
            Color.RGBToHSV(source, out _, out var sourceSat, out var sourceVal);
            Color.RGBToHSV(tint, out var tintHue, out var tintSat, out _);
            var sat = Mathf.Lerp(sourceSat, Mathf.Max(sourceSat, tintSat), 0.85f);
            var result = Color.HSVToRGB(tintHue, sat, Mathf.Max(0.15f, sourceVal));
            result.a = source.a;
            return result;
        }

        static void ApplyLights(GameObject fx, Color tint)
        {
            var lights = fx.GetComponentsInChildren<Light>(true);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null)
                {
                    continue;
                }

                light.color = Color.Lerp(light.color, tint, 0.85f);
            }
        }

        static void TrySetColor(VisualEffect vfx, string name, Color color)
        {
            if (!vfx.HasVector4(name))
            {
                return;
            }

            vfx.SetVector4(name, (Vector4)(Color)color);
        }

        static ParticleSystem.MinMaxGradient RetintGradient(
            ParticleSystem.MinMaxGradient gradient,
            Color tint)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Retint(gradient.color, tint));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        Retint(gradient.colorMin, tint),
                        Retint(gradient.colorMax, tint));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(RetintGradientKeys(gradient.gradient, tint));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        RetintGradientKeys(gradient.gradientMin, tint),
                        RetintGradientKeys(gradient.gradientMax, tint));
                default:
                    return gradient;
            }
        }

        static Gradient RetintGradientKeys(Gradient source, Color tint)
        {
            if (source == null)
            {
                return source;
            }

            var colorKeys = source.colorKeys;
            for (var i = 0; i < colorKeys.Length; i++)
            {
                colorKeys[i].color = Retint(colorKeys[i].color, tint);
            }

            var result = new Gradient();
            result.SetKeys(colorKeys, source.alphaKeys);
            return result;
        }

        static Color Darken(Color color, float amount)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            return Color.HSVToRGB(h, s, Mathf.Max(0.05f, v * (1f - amount)));
        }

        static Color Brighten(Color color, float amount)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            return Color.HSVToRGB(h, Mathf.Max(0f, s * (1f - amount * 0.35f)), Mathf.Min(1f, v + amount));
        }
    }
}
