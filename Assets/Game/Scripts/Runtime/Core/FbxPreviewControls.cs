using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class FbxPreviewControls : MonoBehaviour
    {
        [System.Serializable]
        public class UnitRef
        {
            public Animator animator;
            public AnimationClip runClip;
            public AnimationClip attackClip;
        }

        public List<UnitRef> units = new List<UnitRef>();

        readonly Dictionary<Animator, AnimatorOverrideController> _attackOverrides = new Dictionary<Animator, AnimatorOverrideController>();
        readonly Dictionary<Animator, RuntimeAnimatorController> _baseControllers = new Dictionary<Animator, RuntimeAnimatorController>();

        public void PlayRun()
        {
            foreach (var u in units)
            {
                if (u == null || u.animator == null) continue;
                if (_baseControllers.TryGetValue(u.animator, out var baseController) && baseController != null)
                {
                    u.animator.runtimeAnimatorController = baseController;
                    _attackOverrides.Remove(u.animator);
                    _baseControllers.Remove(u.animator);
                    u.animator.Rebind();
                }
            }
        }

        public void PlayAttack()
        {
            foreach (var u in units)
            {
                if (u == null || u.animator == null || u.attackClip == null) continue;
                var baseController = u.animator.runtimeAnimatorController;
                if (!_baseControllers.ContainsKey(u.animator))
                {
                    _baseControllers[u.animator] = baseController;
                }

                if (!_attackOverrides.TryGetValue(u.animator, out var overrideController) || overrideController == null)
                {
                    overrideController = new AnimatorOverrideController(baseController);
                    var run = u.runClip != null ? u.runClip : FirstClip(baseController);
                    if (run != null)
                    {
                        overrideController[run] = u.attackClip;
                    }

                    _attackOverrides[u.animator] = overrideController;
                }

                u.animator.runtimeAnimatorController = overrideController;
                u.animator.Rebind();
            }
        }

        public void SetTeamColor(Color color)
        {
            var materials = new List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null) continue;
                if (IsTeamMaterial(r.sharedMaterial))
                    materials.Add(r.sharedMaterial);
            }
            foreach (var m in materials)
            {
                if (m.HasProperty("_BaseColor"))
                    m.color = color;
            }
        }

        static bool IsTeamMaterial(Material m)
        {
            var name = m.name.ToLowerInvariant();
            return name.Contains("team") || name.Contains("accent");
        }

        static AnimationClip FirstClip(RuntimeAnimatorController controller)
        {
            var clips = controller.animationClips;
            return clips != null && clips.Length > 0 ? clips[0] : null;
        }

        void OnGUI()
        {
            const float w = 220f;
            GUILayout.BeginArea(new Rect(16f, 16f, w, 280f));
            GUILayout.Box("Превью: управление", GUILayout.Width(w));

            if (GUILayout.Button("Атака (луп)", GUILayout.Width(w)))
                PlayAttack();
            if (GUILayout.Button("Бег (Walk, луп)", GUILayout.Width(w)))
                PlayRun();

            GUILayout.Space(12f);
            GUILayout.Box("Цвет команды", GUILayout.Width(w));
            if (GUILayout.Button("Зелёный", GUILayout.Width(w)))
                SetTeamColor(new Color(0.2f, 1f, 0.3f));
            if (GUILayout.Button("Красный", GUILayout.Width(w)))
                SetTeamColor(new Color(1f, 0.25f, 0.25f));
            if (GUILayout.Button("Синий", GUILayout.Width(w)))
                SetTeamColor(new Color(0.25f, 0.5f, 1f));

            GUILayout.EndArea();
        }
    }
}
