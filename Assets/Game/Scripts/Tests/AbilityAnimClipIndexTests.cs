using Game.Core;
using Game.Editor;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AbilityAnimClipIndexTests
    {
        UnitVisualCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            Assert.IsNotNull(_catalog);
        }

        [Test]
        public void Collect_HumanMelee_AttackHasTwoBlendChildren()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            var clips = AbilityAnimClipIndex.Collect(prefab);
            var attack = CountState(clips, UnitCombatAnimatorDriver.AttackState);
            Assert.AreEqual(2, attack);
            Assert.Greater(clips.Count, 2);
            Assert.GreaterOrEqual(CountState(clips, UnitCombatAnimatorDriver.StandState), 1);
            Assert.GreaterOrEqual(CountState(clips, UnitCombatAnimatorDriver.DeathState), 1);
        }

        [Test]
        public void Collect_HumanCaster_AttackIsSingleClip()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Caster, out var prefab));
            var clips = AbilityAnimClipIndex.Collect(prefab);
            var attack = FindAll(clips, UnitCombatAnimatorDriver.AttackState);
            Assert.AreEqual(1, attack.Count);
            Assert.AreEqual("staff_04_attack_B", attack[0].ClipName);
            Assert.AreEqual(-1, attack[0].Variant);

            var cast = FindAll(clips, UnitCombatAnimatorDriver.CastState);
            Assert.AreEqual(2, cast.Count);
        }

        [Test]
        public void IndexOf_FindsBlendChild()
        {
            Assert.IsTrue(_catalog.TryGetPrefab(GameIds.Races.Human, UnitRole.Melee, out var prefab));
            var clips = AbilityAnimClipIndex.Collect(prefab);
            var first = AbilityAnimClipIndex.IndexOf(clips, UnitCombatAnimatorDriver.AttackState, 0);
            var second = AbilityAnimClipIndex.IndexOf(clips, UnitCombatAnimatorDriver.AttackState, 1);
            Assert.GreaterOrEqual(first, 0);
            Assert.GreaterOrEqual(second, 0);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(0, clips[first].Variant);
            Assert.AreEqual(1, clips[second].Variant);
        }

        static int CountState(System.Collections.Generic.List<AbilityAnimClipIndex.Entry> clips, string state)
        {
            var count = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].StateName == state)
                {
                    count++;
                }
            }

            return count;
        }

        static System.Collections.Generic.List<AbilityAnimClipIndex.Entry> FindAll(
            System.Collections.Generic.List<AbilityAnimClipIndex.Entry> clips,
            string state)
        {
            var list = new System.Collections.Generic.List<AbilityAnimClipIndex.Entry>();
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i].StateName == state)
                {
                    list.Add(clips[i]);
                }
            }

            return list;
        }
    }
}
