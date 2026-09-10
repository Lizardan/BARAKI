using Game.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class UnitCombatSettingsTests
    {
        [Test]
        public void AssignSource_UnitDefinition_SetsUnitAndClearsHero()
        {
            var gameObject = new GameObject("UnitCombatSettingsTests");
            var unit = ScriptableObject.CreateInstance<UnitDefinition>();
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            try
            {
                var settings = gameObject.AddComponent<UnitCombatSettings>();
                settings.AssignSource(hero);
                settings.AssignSource(unit);

                Assert.AreSame(unit, settings.UnitDefinition);
                Assert.IsNull(settings.HeroDefinition);
                Assert.IsTrue(settings.HasStatsSource);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(unit);
                Object.DestroyImmediate(hero);
            }
        }

        [Test]
        public void AssignSource_HeroDefinition_SetsHeroAndClearsUnit()
        {
            var gameObject = new GameObject("UnitCombatSettingsTests");
            var unit = ScriptableObject.CreateInstance<UnitDefinition>();
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            try
            {
                var settings = gameObject.AddComponent<UnitCombatSettings>();
                settings.AssignSource(unit);
                settings.AssignSource(hero);

                Assert.AreSame(hero, settings.HeroDefinition);
                Assert.IsNull(settings.UnitDefinition);
                Assert.IsTrue(settings.HasStatsSource);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(unit);
                Object.DestroyImmediate(hero);
            }
        }

        [Test]
        public void ReplaceAbilities_KeepsSourceRef()
        {
            var gameObject = new GameObject("UnitCombatSettingsTests");
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            var ability = ScriptableObject.CreateInstance<UnitAbilityDef>();
            try
            {
                var settings = gameObject.AddComponent<UnitCombatSettings>();
                settings.AssignSource(hero);
                settings.ReplaceAbilities(new[] { ability });

                Assert.AreSame(hero, settings.HeroDefinition);
                Assert.AreEqual(1, settings.Abilities.Length);
                Assert.AreSame(ability, settings.Abilities[0]);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void CopyFromSettings_CopiesSourceRefsAndAbilities()
        {
            var sourceObject = new GameObject("UnitCombatSettingsSource");
            var destinationObject = new GameObject("UnitCombatSettingsDestination");
            var hero = ScriptableObject.CreateInstance<HeroDefinition>();
            var ability = ScriptableObject.CreateInstance<UnitAbilityDef>();
            try
            {
                var source = sourceObject.AddComponent<UnitCombatSettings>();
                source.AssignSource(hero);
                source.ReplaceAbilities(new[] { ability });

                var destination = destinationObject.AddComponent<UnitCombatSettings>();
                destination.CopyFrom(source);

                Assert.AreSame(hero, destination.HeroDefinition);
                Assert.IsNull(destination.UnitDefinition);
                Assert.AreEqual(1, destination.Abilities.Length);
                Assert.AreSame(ability, destination.Abilities[0]);
            }
            finally
            {
                Object.DestroyImmediate(sourceObject);
                Object.DestroyImmediate(destinationObject);
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(ability);
            }
        }
    }
}