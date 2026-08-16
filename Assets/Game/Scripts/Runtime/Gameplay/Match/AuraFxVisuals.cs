using System.Collections.Generic;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>Spawns / tints CFXR looping aura prefabs under unit roots.</summary>
    public static class AuraFxVisuals
    {
        public const string RaysChildName = "Rays";

        public static GameObject Attach(
            Transform parent,
            GameObject prefab,
            PassiveAuraFxKind kind,
            Color tint,
            float auraRadius)
        {
            if (parent == null || prefab == null)
            {
                return null;
            }

            var fx = Object.Instantiate(prefab, parent);
            fx.name = kind == PassiveAuraFxKind.Shiny ? "AuraFx_Shiny" : "AuraFx_Runic";
            fx.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one * PassiveAuraFxRules.ResolveScale(kind, auraRadius);
            StripNamedChildren(fx, RaysChildName);
            ApplyTint(fx, tint);
            SoftenLoop(fx);
            return fx;
        }

        /// <summary>
        /// Permanent body FX for Titan: only the CFXR Runic <c>Rays</c> child, tinted and scaled
        /// to sit inside the model (separate from the MaxHp army aura ring).
        /// </summary>
        public static GameObject AttachBodyRays(
            Transform parent,
            GameObject runicPrefab,
            Color tint,
            float localScale)
        {
            if (parent == null || runicPrefab == null)
            {
                return null;
            }

            var fx = Object.Instantiate(runicPrefab, parent);
            fx.name = "TitanBodyRays";
            fx.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one * Mathf.Max(0.05f, localScale);
            KeepOnlyNamedChildren(fx, RaysChildName);
            DisableRootParticleAndLight(fx);
            ApplyTint(fx, tint);
            SoftenLoop(fx);
            return fx;
        }

        public static void StripNamedChildren(GameObject root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return;
            }

            var doomed = new List<GameObject>();
            CollectNamed(root.transform, childName, doomed);
            for (var i = 0; i < doomed.Count; i++)
            {
                DestroyGo(doomed[i]);
            }
        }

        public static void KeepOnlyNamedChildren(GameObject root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return;
            }

            var doomed = new List<GameObject>();
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i).gameObject;
                if (!string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    doomed.Add(child);
                }
            }

            for (var i = 0; i < doomed.Count; i++)
            {
                DestroyGo(doomed[i]);
            }
        }

        public static void ApplyTint(GameObject fx, Color tint)
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

        static void CollectNamed(Transform root, string childName, List<GameObject> into)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    into.Add(child.gameObject);
                }
                else
                {
                    CollectNamed(child, childName, into);
                }
            }
        }

        static void DisableRootParticleAndLight(GameObject fx)
        {
            // CFXR_Effect requires a ParticleSystem on the same GameObject — disable, don't destroy.
            var cfxr = fx.GetComponent("CFXR_Effect");
            if (cfxr != null)
            {
                if (cfxr is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }

            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var emission = ps.emission;
                emission.enabled = false;
                var renderer = fx.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            var light = fx.GetComponent<Light>();
            if (light != null)
            {
                light.enabled = false;
            }
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

        /// <summary>Keep value/alpha from the CFXR authoring; take hue/sat from the aura tint.</summary>
        public static Color Retint(Color source, Color tint)
        {
            Color.RGBToHSV(source, out _, out var sourceSat, out var sourceVal);
            Color.RGBToHSV(tint, out var tintHue, out var tintSat, out _);
            var sat = Mathf.Lerp(sourceSat, Mathf.Max(sourceSat, tintSat), 0.85f);
            var result = Color.HSVToRGB(tintHue, sat, Mathf.Max(0.15f, sourceVal));
            result.a = source.a;
            return result;
        }

        static void SoftenLoop(GameObject fx)
        {
            var lights = fx.GetComponentsInChildren<Light>(true);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity *= 0.55f;
                }
            }

            var audio = fx.GetComponentsInChildren<AudioSource>(true);
            for (var i = 0; i < audio.Length; i++)
            {
                if (audio[i] != null)
                {
                    audio[i].mute = true;
                    audio[i].enabled = false;
                }
            }
        }

        static void DestroyGo(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
