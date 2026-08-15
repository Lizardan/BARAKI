using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitCombatSettingsTests
    {
        [Test]
        public void CopyFromDefinition_PreservesAbilities()
        {
            var gameObject = new GameObject("UnitCombatSettingsTests");
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            var ability = ScriptableObject.CreateInstance<UnitAbilityDef>();
            try
            {
                var settings = gameObject.AddComponent<UnitCombatSettings>();
                settings.ReplaceAbilities(new[] { ability });

                settings.CopyFrom(definition);

                Assert.AreEqual(1, settings.Abilities.Length);
                Assert.AreSame(ability, settings.Abilities[0]);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void CopyFromSettings_CopiesStatsAndAbilities()
        {
            var sourceObject = new GameObject("UnitCombatSettingsSource");
            var destinationObject = new GameObject("UnitCombatSettingsDestination");
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            var ability = ScriptableObject.CreateInstance<UnitAbilityDef>();
            try
            {
                var source = sourceObject.AddComponent<UnitCombatSettings>();
                source.CopyFrom(definition);
                source.ReplaceAbilities(new[] { ability });

                var destination = destinationObject.AddComponent<UnitCombatSettings>();
                destination.CopyFrom(source);

                Assert.AreEqual(source.MaxHp, destination.MaxHp);
                Assert.AreEqual(source.DamageMin, destination.DamageMin);
                Assert.AreEqual(1, destination.Abilities.Length);
                Assert.AreSame(ability, destination.Abilities[0]);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(destinationObject);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(ability);
            }
        }
    }
}
