using Game.Editor;
using Game.Gameplay.Vfx;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    public sealed class AbilityVfxPrefabIndexTests
    {
        [Test]
        public void Classify_SlashPack_IsHit()
        {
            Assert.AreEqual(
                AbilityVfxKind.Hit,
                AbilityVfxPrefabIndex.Classify("Assets/Adjustable Slash VFX Pack/Prefabs/Slash_12.prefab"));
        }

        [Test]
        public void Classify_CfxrImpact_IsHit()
        {
            Assert.AreEqual(
                AbilityVfxKind.Hit,
                AbilityVfxPrefabIndex.Classify(
                    "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab"));
        }

        [Test]
        public void Classify_HyperCasualArea_IsAura()
        {
            Assert.AreEqual(
                AbilityVfxKind.Aura,
                AbilityVfxPrefabIndex.Classify(
                    "Assets/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_heal_green.prefab"));
        }

        [Test]
        public void Classify_MagicAuraLoop_IsAura()
        {
            Assert.AreEqual(
                AbilityVfxKind.Aura,
                AbilityVfxPrefabIndex.Classify(
                    "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab"));
        }

        [Test]
        public void Classify_SoulsEscape_IsCast()
        {
            Assert.AreEqual(
                AbilityVfxKind.Cast,
                AbilityVfxPrefabIndex.Classify(
                    "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Eerie/CFXR2 Souls Escape.prefab"));
        }

        [Test]
        public void Scan_FindsPackPrefabsWhenImported()
        {
            var entries = AbilityVfxPrefabIndex.Scan();
            if (!AssetDatabase.IsValidFolder(AbilityVfxPrefabIndex.CfxrPrefabs))
            {
                Assert.Ignore("CFXR pack is not in the project.");
            }

            Assert.Greater(entries.Count, 0);
            Assert.IsTrue(entries.Exists(e => e.Kind == AbilityVfxKind.Hit && e.Prefab != null));
            Assert.IsTrue(entries.Exists(e => e.Kind == AbilityVfxKind.Aura && e.Prefab != null));
            Assert.IsTrue(entries.Exists(e => e.Kind == AbilityVfxKind.Cast && e.Prefab != null));
            if (AssetDatabase.IsValidFolder(AbilityVfxPrefabIndex.SlashPrefabs))
            {
                Assert.IsTrue(entries.Exists(e => e.DisplayName == "Slash_1"));
                Assert.IsFalse(entries.Exists(e => e.DisplayName == "SlashMesh"));
            }
        }
    }
}
