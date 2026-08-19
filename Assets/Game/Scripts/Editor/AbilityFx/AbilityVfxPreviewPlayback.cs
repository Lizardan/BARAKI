using System.Collections.Generic;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Editor
{
    /// <summary>
    /// ParticleSystem can pause+Simulate. Visual Effect Graph in a PreviewRenderUtility
    /// must stay unpaused and get Play after Reinit. Do not switch the preview camera to
    /// CameraType.Game or call PreviewRenderUtility.Render(true) from the FX grid:
    /// the preview scene has no LightmapSettings manager (fatal editor crash).
    /// </summary>
    public static class AbilityVfxPreviewPlayback
    {
        public const float OneShotLoopSeconds = 0.9f;
        /// <summary>Tight ortho so a ~1 unit slash arc fills the picker cell.</summary>
        public const float SlashOrthoSize = 0.48f;

        const int CullNone = 0;
        const string CullingProperty = "m_Infos.m_CullingFlags";

        static readonly Dictionary<VisualEffectAsset, int> OriginalCulling = new();
        static int s_cullingUsers;

        public static void RetainCullingScope() => s_cullingUsers++;

        public static void ReleaseCullingScope()
        {
            s_cullingUsers = Mathf.Max(0, s_cullingUsers - 1);
            if (s_cullingUsers == 0)
            {
                RestoreCulling();
            }
        }

        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
        }

        public static void Render(PreviewRenderUtility preview, RenderTexture target)
        {
            if (preview == null || preview.camera == null || target == null)
            {
                return;
            }

            preview.camera.targetTexture = target;
            VFXManager.PrepareCamera(preview.camera);
            preview.camera.Render();
        }

        public static void PauseParticlesKeepVfxPlaying(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                systems[i]?.Pause(true);
            }

            var effects = root.GetComponentsInChildren<VisualEffect>(true);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                {
                    effects[i].pause = false;
                    effects[i].allowInstancing = false;
                }
            }
        }

        public static void PrepareInstance(GameObject root, Color tint = default)
        {
            if (root == null)
            {
                return;
            }

            OverrideCulling(root);
            PauseParticlesKeepVfxPlaying(root);
            PlayVisualEffects(root, tint);
        }

        /// <summary>
        /// Slash pack often authors additive FirstColor as black; without a viewer tint
        /// the mesh is invisible. Picker copies SecondColor so the arc still reads.
        /// Call after every Reinit — property overrides do not survive it.
        /// </summary>
        public static void LiftDarkSlashMeshColor(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var effects = root.GetComponentsInChildren<VisualEffect>(true);
            for (var i = 0; i < effects.Length; i++)
            {
                var vfx = effects[i];
                if (vfx == null || !vfx.HasVector4("FirstColor") || !vfx.HasVector4("SecondColor"))
                {
                    continue;
                }

                var first = (Color)vfx.GetVector4("FirstColor");
                if (first.r + first.g + first.b >= 0.2f)
                {
                    continue;
                }

                vfx.SetVector4("FirstColor", vfx.GetVector4("SecondColor"));
            }
        }

        public static void PlayVisualEffects(GameObject root, Color tint = default)
        {
            if (root == null)
            {
                return;
            }

            var effects = root.GetComponentsInChildren<VisualEffect>(true);
            for (var i = 0; i < effects.Length; i++)
            {
                var vfx = effects[i];
                if (vfx == null)
                {
                    continue;
                }

                vfx.pause = false;
                vfx.allowInstancing = false;
                vfx.enabled = false;
                vfx.enabled = true;
                if (vfx.visualEffectAsset != null)
                {
                    vfx.visualEffectAsset.PrewarmComputeShaders();
                }

                vfx.Reinit();
                vfx.Play();
            }

            ApplyPreviewColors(root, tint);
        }

        public static void SimulateVisualEffects(
            GameObject root,
            float seconds,
            bool restart,
            Color tint = default)
        {
            if (root == null)
            {
                return;
            }

            if (restart)
            {
                PlayVisualEffects(root, tint);
            }

            var time = Mathf.Max(0f, seconds);
            var effects = root.GetComponentsInChildren<VisualEffect>(true);
            for (var i = 0; i < effects.Length; i++)
            {
                var vfx = effects[i];
                if (vfx == null)
                {
                    continue;
                }

                vfx.pause = false;
                var steps = (uint)Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(time, 0.016f) * 60f));
                var stepDt = time / steps * Mathf.Max(0.01f, vfx.playRate);
                vfx.Simulate(stepDt, steps);
            }
        }

        static void ApplyPreviewColors(GameObject root, Color tint)
        {
            if (tint.a > 0.01f)
            {
                AbilityVfxTint.ApplyVisualEffects(root, tint);
                return;
            }

            LiftDarkSlashMeshColor(root);
        }

        public static bool HasVisualEffect(GameObject root) =>
            root != null && root.GetComponentInChildren<VisualEffect>(true) != null;

        public static int CountAliveParticles(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            var count = 0;
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    count += systems[i].particleCount;
                }
            }

            return count;
        }

        public static void OverrideCulling(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var effects = root.GetComponentsInChildren<VisualEffect>(true);
            for (var i = 0; i < effects.Length; i++)
            {
                var asset = effects[i] != null ? effects[i].visualEffectAsset : null;
                if (asset == null)
                {
                    continue;
                }

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(CullingProperty);
                if (prop == null)
                {
                    continue;
                }

                if (!OriginalCulling.ContainsKey(asset))
                {
                    OriginalCulling[asset] = prop.intValue;
                }

                if (prop.intValue == CullNone)
                {
                    continue;
                }

                prop.intValue = CullNone;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.ClearDirty(asset);
            }
        }

        static void RestoreCulling()
        {
            foreach (var pair in OriginalCulling)
            {
                var asset = pair.Key;
                if (asset == null)
                {
                    continue;
                }

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(CullingProperty);
                if (prop == null || prop.intValue == pair.Value)
                {
                    continue;
                }

                prop.intValue = pair.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.ClearDirty(asset);
            }

            OriginalCulling.Clear();
        }
    }
}
