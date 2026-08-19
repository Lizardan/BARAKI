using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Layer-0 states of a unit AnimatorController, expanded so each BlendTree child is a pickable clip.
    /// </summary>
    public static class AbilityAnimClipIndex
    {
        public readonly struct Entry
        {
            public Entry(string stateName, int variant, string clipName, string displayName)
            {
                StateName = stateName;
                Variant = variant;
                ClipName = clipName;
                DisplayName = displayName;
            }

            public string StateName { get; }

            /// <summary>BlendTree child index, or −1 when the state is a single clip.</summary>
            public int Variant { get; }

            public string ClipName { get; }
            public string DisplayName { get; }

            public bool Matches(string stateName, int variant)
            {
                if (StateName != stateName)
                {
                    return false;
                }

                return Variant < 0 || variant < 0 || Variant == variant;
            }
        }

        public static List<Entry> Collect(GameObject prefab)
        {
            if (prefab == null)
            {
                return new List<Entry>();
            }

            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return new List<Entry>();
            }

            return Collect(animator.runtimeAnimatorController as AnimatorController);
        }

        public static List<Entry> Collect(AnimatorController controller)
        {
            var list = new List<Entry>();
            if (controller == null || controller.layers == null || controller.layers.Length == 0)
            {
                return list;
            }

            var states = controller.layers[0].stateMachine.states;
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                if (state == null)
                {
                    continue;
                }

                AppendMotion(list, state.name, state.motion, variant: -1);
            }

            return list;
        }

        static void AppendMotion(List<Entry> list, string stateName, Motion motion, int variant)
        {
            if (motion == null)
            {
                list.Add(new Entry(stateName, variant, stateName, stateName));
                return;
            }

            if (motion is AnimationClip clip)
            {
                var name = string.IsNullOrEmpty(clip.name) ? stateName : clip.name;
                list.Add(new Entry(stateName, variant, name, $"{stateName}  ·  {name}"));
                return;
            }

            if (motion is BlendTree tree)
            {
                var children = tree.children;
                for (var i = 0; i < children.Length; i++)
                {
                    AppendMotion(list, stateName, children[i].motion, i);
                }

                return;
            }

            list.Add(new Entry(stateName, variant, motion.name, $"{stateName}  ·  {motion.name}"));
        }

        public static int IndexOf(IReadOnlyList<Entry> clips, string stateName, int variant)
        {
            if (clips == null || string.IsNullOrEmpty(stateName))
            {
                return -1;
            }

            var fallback = -1;
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].StateName != stateName)
                {
                    continue;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }

                if (clips[i].Matches(stateName, variant))
                {
                    return i;
                }
            }

            return fallback;
        }

        public static int IndexOfState(IReadOnlyList<Entry> clips, string stateName)
        {
            if (clips == null || string.IsNullOrEmpty(stateName))
            {
                return -1;
            }

            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].StateName == stateName)
                {
                    return i;
                }
            }

            return -1;
        }

        public static string DefaultState(AbilityAnimKind kind) =>
            kind switch
            {
                AbilityAnimKind.Attack => UnitCombatAnimatorDriver.AttackState,
                AbilityAnimKind.Cast => UnitCombatAnimatorDriver.CastState,
                _ => UnitCombatAnimatorDriver.StandState,
            };
    }
}
