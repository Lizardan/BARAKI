using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public sealed class SquadSpawnRulesTests
    {
        [Test]
        public void GetSpawnDelaySeconds_StaggersByInterval()
        {
            Assert.AreEqual(0.1f, SquadSpawnRules.UnitSpawnIntervalSeconds);
            Assert.AreEqual(0f, SquadSpawnRules.GetSpawnDelaySeconds(0));
            Assert.AreEqual(0.1f, SquadSpawnRules.GetSpawnDelaySeconds(1));
            Assert.AreEqual(0.3f, SquadSpawnRules.GetSpawnDelaySeconds(3));
        }

        [Test]
        public void BuildEntries_SkipsZeroCounts()
        {
            var squad = ScriptableObject.CreateInstance<SquadCompositionDefinition>();
            var entries = SquadSpawnRules.BuildEntries(squad);
            Assert.AreEqual(0, entries.Count);
            Object.DestroyImmediate(squad);
        }

        [Test]
        public void BuildEntries_ReturnsNonZeroRoles()
        {
            var squad = ScriptableObject.CreateInstance<SquadCompositionDefinition>();
            using (var serialized = new SerializedObject(squad))
            {
                serialized.FindProperty("_meleeCount").intValue = 2;
                serialized.FindProperty("_rangedCount").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var entries = SquadSpawnRules.BuildEntries(squad);
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(UnitRole.Melee, entries[0].Role);
            Assert.AreEqual(2, entries[0].Count);
            Assert.AreEqual(UnitRole.Ranged, entries[1].Role);
            Object.DestroyImmediate(squad);
        }

        [Test]
        public void BuildSpawnPlan_L1_MeleeFrontRow_RangedThenCasterBehind()
        {
            var squad = ScriptableObject.CreateInstance<SquadCompositionDefinition>();
            using (var serialized = new SerializedObject(squad))
            {
                serialized.FindProperty("_meleeCount").intValue = 2;
                serialized.FindProperty("_rangedCount").intValue = 1;
                serialized.FindProperty("_casterCount").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var plan = SquadSpawnRules.BuildSpawnPlan(squad);
            Assert.AreEqual(4, plan.Count);

            Assert.AreEqual(UnitRole.Melee, plan[0].Role);
            Assert.AreEqual(0, plan[0].RowIndex);
            Assert.AreEqual(UnitRole.Melee, plan[1].Role);
            Assert.AreEqual(0, plan[1].RowIndex);
            Assert.AreEqual(2, plan[1].CountInRow);

            Assert.AreEqual(UnitRole.Ranged, plan[2].Role);
            Assert.AreEqual(1, plan[2].RowIndex);

            Assert.AreEqual(UnitRole.Caster, plan[3].Role);
            Assert.AreEqual(2, plan[3].RowIndex);

            Object.DestroyImmediate(squad);
        }

        [Test]
        public void GetSpawnDistanceForRow_FrontRowIsFurthestAlongLane()
        {
            var random = new System.Random(123);
            var front = CombatFormationRules.GetSpawnDistanceForRow(0, rearmostRowIndex: 2, random);
            var mid = CombatFormationRules.GetSpawnDistanceForRow(1, 2, new System.Random(123));
            var rear = CombatFormationRules.GetSpawnDistanceForRow(2, 2, new System.Random(123));

            Assert.Greater(front, mid);
            Assert.Greater(mid, rear);
        }
    }
}
