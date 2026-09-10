using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Vfx;
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
            float auraRadius,
            float visualScale = 1f)
        {
            if (parent == null || prefab == null)
            {
                return null;
            }

            var fx = Object.Instantiate(prefab, parent);
            fx.name = kind == PassiveAuraFxKind.Shiny ? "AuraFx_Shiny" : "AuraFx_Runic";
            fx.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one
                * PassiveAuraFxRules.ResolveScale(kind, auraRadius)
                * Mathf.Max(0.01f, visualScale);
            StripNamedChildren(fx, RaysChildName);
            ApplyTint(fx, tint);
            SoftenLoop(fx);
            RestartLoops(fx);
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
            float presenterScale)
        {
            if (parent == null || runicPrefab == null)
            {
                return null;
            }

            var fx = Object.Instantiate(runicPrefab, parent);
            fx.name = "TitanBodyRays";
            fx.transform.localRotation = Quaternion.identity;
            SetBodyRaysPlacement(fx, presenterScale);
            KeepOnlyNamedChildren(fx, RaysChildName);
            MuteRootParticlesKeepChildren(fx);
            ApplyTint(fx, tint);
            SoftenLoop(fx);
            RestartLoops(fx);
            return fx;
        }

        public static void SetBodyRaysPlacement(GameObject fx, float presenterScale)
        {
            if (fx == null)
            {
                return;
            }

            fx.transform.localPosition = new Vector3(
                0f,
                UnitGreyboxVisuals.ResolveTitanBodyRaysHeight(presenterScale),
                0f);
            fx.transform.localScale = Vector3.one
                * UnitGreyboxVisuals.ResolveTitanBodyRaysScale(presenterScale);
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

        public static void ApplyTint(GameObject fx, Color tint) => AbilityVfxTint.Apply(fx, tint);

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

        static void MuteRootParticlesKeepChildren(GameObject fx)
        {
            // Do not ParticleSystem.Stop(withChildren: true) — that also kills the Rays child.
            DisableCfxrAutoClear(fx);
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var emission = ps.emission;
                emission.enabled = false;
                ps.Clear(withChildren: false);
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

        static void DisableCfxrAutoClear(GameObject fx)
        {
            var cfxr = fx.GetComponent("CFXR_Effect");
            if (cfxr == null)
            {
                return;
            }

            var field = cfxr.GetType().GetField("clearBehavior");
            if (field != null)
            {
                field.SetValue(cfxr, 0);
            }
        }

        static void RestartLoops(GameObject fx)
        {
            if (fx == null)
            {
                return;
            }

            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null || ps.gameObject == fx)
                {
                    continue;
                }

                ps.Play(withChildren: false);
            }
        }

        /// <summary>Keep value/alpha from the CFXR authoring; take hue/sat from the aura tint.</summary>
        public static Color Retint(Color source, Color tint) => AbilityVfxTint.Retint(source, tint);

        /// <summary>
        /// Preview-scene safety: CFXR must not Destroy/Disable the instance, shake the editor
        /// camera, or play audio. Lights are optionally softened like a match aura loop.
        /// </summary>
        public static void PrepareEditorPreview(GameObject fx, bool softenLights = true)
        {
            if (fx == null)
            {
                return;
            }

            var behaviours = fx.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour.GetType().Name != "CFXR_Effect")
                {
                    continue;
                }

                var type = behaviour.GetType();
                type.GetField("clearBehavior")?.SetValue(behaviour, 0);
                AbilityFxCameraShakeGuard.DisableCameraShake(behaviour);
            }

            MuteAudio(fx);
            if (softenLights)
            {
                SoftenLights(fx);
            }
        }

        static void SoftenLoop(GameObject fx)
        {
            AbilityFxCameraShakeGuard.Strip(fx);
            SoftenLights(fx);
            MuteAudio(fx);
        }

        static void SoftenLights(GameObject fx)
        {
            var lights = fx.GetComponentsInChildren<Light>(true);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity *= 0.55f;
                }
            }
        }

        static void MuteAudio(GameObject fx)
        {
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

            // Immediate: delayed Destroy leaves CFXR children alive for a frame and can
            // trip clearBehavior (disable the whole aura FX).
            Object.DestroyImmediate(target);
        }
    }
}
