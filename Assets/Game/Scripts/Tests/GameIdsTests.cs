using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class GameIdsTests
    {
        [Test]
        public void AllIds_AreUniqueNonEmptyStrings()
        {
            var ids = CollectIds(typeof(GameIds));
            Assert.IsNotEmpty(ids);
            var set = new HashSet<string>(ids);
            Assert.AreEqual(ids.Count, set.Count, "Duplicate GameIds detected.");
        }

        [Test]
        public void CoreIds_MatchGdd()
        {
            Assert.AreEqual("RACE_HUMAN", GameIds.Races.Human);
            Assert.AreEqual("BUILDING_BARRACKS_CENTER", GameIds.Buildings.BarracksCenter);
            Assert.AreEqual("LANE_CENTER", GameIds.Lanes.Center);
            Assert.AreEqual("SPELL_BUG_2", GameIds.Spells.BugEgg);
        }

        static List<string> CollectIds(System.Type root)
        {
            var result = new List<string>();
            CollectIdsRecursive(root, result);
            return result;
        }

        static void CollectIdsRecursive(System.Type type, List<string> result)
        {
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
                CollectIdsRecursive(nested, result);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                var value = (string)field.GetValue(null);
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Empty id: {type.Name}.{field.Name}");
                result.Add(value);
            }
        }
    }
}
