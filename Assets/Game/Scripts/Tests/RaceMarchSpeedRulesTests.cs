using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    public sealed class RaceMarchSpeedRulesTests
    {
        [Test]
        public void GetMarchSpeed_Human_ReturnsBaseSpeed()
        {
            var race = UnityEngine.ScriptableObject.CreateInstance<RaceDefinition>();
            Assert.AreEqual(RaceMarchSpeedRules.BaseMarchSpeed, RaceMarchSpeedRules.GetMarchSpeed(race));
            UnityEngine.Object.DestroyImmediate(race);
        }

        [Test]
        public void GetMarchSpeed_Bug_AppliesFrenzyBonus()
        {
            var bug = CreateRaceWithPassives(GameIds.Passives.BugFrenzy);
            var expected = RaceMarchSpeedRules.BaseMarchSpeed * RaceMarchSpeedRules.BugFrenzyMoveMultiplier;
            Assert.AreEqual(expected, RaceMarchSpeedRules.GetMarchSpeed(bug), 0.001f);
            UnityEngine.Object.DestroyImmediate(bug);
        }

        [Test]
        public void GetMarchSpeed_UnitOverride_TakesPrecedenceOverRace()
        {
            var race = CreateRaceWithPassives(GameIds.Passives.BugFrenzy);
            var unit = CreateUnitWithMarchOverride(7f);
            Assert.AreEqual(7f, RaceMarchSpeedRules.GetMarchSpeed(race, unit), 0.001f);
            UnityEngine.Object.DestroyImmediate(race);
            UnityEngine.Object.DestroyImmediate(unit);
        }

        static RaceDefinition CreateRaceWithPassives(params string[] positiveIds)
        {
            var race = UnityEngine.ScriptableObject.CreateInstance<RaceDefinition>();
            var so = new UnityEditor.SerializedObject(race);
            var list = so.FindProperty("_positivePassiveIds");
            list.ClearArray();
            for (var i = 0; i < positiveIds.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).stringValue = positiveIds[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return race;
        }

        static UnitDefinition CreateUnitWithMarchOverride(float overrideSpeed)
        {
            var unit = UnityEngine.ScriptableObject.CreateInstance<UnitDefinition>();
            var so = new UnityEditor.SerializedObject(unit);
            so.FindProperty("_marchSpeedOverride").floatValue = overrideSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }
    }
}
