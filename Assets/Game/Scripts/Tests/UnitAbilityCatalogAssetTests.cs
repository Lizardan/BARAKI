using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    public sealed class UnitAbilityCatalogAssetTests
    {
        const string CatalogPath = "Assets/Game/ScriptableObjects/Catalogs/UnitAbilityCatalog.asset";

        [Test]
        public void CatalogAsset_ExistsAndHasAbilities()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitAbilityCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, CatalogPath);
            Assert.Greater(catalog.Abilities.Count, 0);
        }
    }
}
