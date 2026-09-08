using System.IO;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds Faceless bonus / veteran prefabs (slots 1–10) by cloning the base unit / hero / titan
    /// prefab and reusing the SAME model, then attaching the blue <see cref="BonusFlameMarker"/>
    /// above the head — the glowing "enhanced variant" cue (per design the Faceless bonus/veteran
    /// units never get a separate mesh, only the head glow). Structure mirrors Humans'
    /// <c>VeteranPrefabBuilder</c> (BonusUnits / BonusHeroes) so the two races stay uniform.
    /// Balance and readable spells are seeded afterwards by Sync Balance / Seed Unit Abilities.
    /// </summary>
    public static class FacelessBonusPrefabBuilder
    {
        [MenuItem("BARAKI/Faceless/Build Bonus Prefabs")]
        public static void BuildAll()
        {
            UnitVisualPrefabBuilder.EnsureFacelessBonusFolders();
            // Newly created folders are not visible to SaveAsPrefabAsset until imported.
            AssetDatabase.Refresh();

            Build(UnitVisualPrefabBuilder.FacelessMeleePath,  UnitVisualPrefabBuilder.FacelessMeleeBonusPath,  UnitRole.Melee,  0, BonusKitRules.BonusSlotForRole(UnitRole.Melee));
            Build(UnitVisualPrefabBuilder.FacelessRangedPath, UnitVisualPrefabBuilder.FacelessRangedBonusPath, UnitRole.Ranged, 0, BonusKitRules.BonusSlotForRole(UnitRole.Ranged));
            Build(UnitVisualPrefabBuilder.FacelessCasterPath, UnitVisualPrefabBuilder.FacelessCasterBonusPath, UnitRole.Caster, 0, BonusKitRules.BonusSlotForRole(UnitRole.Caster));
            Build(UnitVisualPrefabBuilder.FacelessSiegePath,  UnitVisualPrefabBuilder.FacelessSiegeBonusPath,  UnitRole.Siege,  0, BonusKitRules.BonusSlotForRole(UnitRole.Siege));
            Build(UnitVisualPrefabBuilder.FacelessFlyingPath, UnitVisualPrefabBuilder.FacelessFlyingBonusPath, UnitRole.Flying, 0, BonusKitRules.BonusSlotForRole(UnitRole.Flying));
            Build(UnitVisualPrefabBuilder.FacelessSuperPath,  UnitVisualPrefabBuilder.FacelessSuperBonusPath,  UnitRole.Super,  0, BonusKitRules.BonusSlotForRole(UnitRole.Super));
            Build(UnitVisualPrefabBuilder.FacelessHero1Path,  UnitVisualPrefabBuilder.FacelessHero1BonusPath,  UnitRole.Hero,   1, BonusKitRules.Hero1BonusSlot);
            Build(UnitVisualPrefabBuilder.FacelessHero2Path,  UnitVisualPrefabBuilder.FacelessHero2BonusPath,  UnitRole.Hero,   2, BonusKitRules.Hero2BonusSlot);
            Build(UnitVisualPrefabBuilder.FacelessHero3Path,  UnitVisualPrefabBuilder.FacelessHero3BonusPath,  UnitRole.Hero,   3, BonusKitRules.Hero3BonusSlot);
            Build(UnitVisualPrefabBuilder.FacelessTitanPath,  UnitVisualPrefabBuilder.FacelessTitanBonusPath,  UnitRole.Titan,  0, BonusKitRules.TitanBonusSlot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnitVisualPrefabBuilder.UpdateFacelessCatalog();
            Debug.Log("FacelessBonusPrefabBuilder: built bonus / veteran prefab(s).");
        }

        static void Build(string sourcePath, string destinationPath, UnitRole role, int heroSlot, int bonusSlot)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"FacelessBonusPrefabBuilder: missing source prefab '{sourcePath}'.");
                return;
            }

            // A hand-tuned UnitCombatSettings (balance + readable spells) survives rebuilds.
            UnitCombatSettings capturedSettings = null;
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
            if (existingPrefab != null)
            {
                var existingSettings = existingPrefab.GetComponentInChildren<UnitCombatSettings>(true);
                if (existingSettings != null)
                {
                    var temp = new GameObject("FacelessBalanceCapture");
                    temp.hideFlags = HideFlags.HideAndDontSave;
                    capturedSettings = temp.AddComponent<UnitCombatSettings>();
                    capturedSettings.CopyFrom(existingSettings);
                }
            }

            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = Path.GetFileNameWithoutExtension(destinationPath);

                AddBonusFlame(root.transform);

                var settings = root.GetComponentInChildren<UnitCombatSettings>(true);
                if (capturedSettings != null)
                {
                    if (settings == null)
                    {
                        settings = root.AddComponent<UnitCombatSettings>();
                    }

                    settings.CopyFrom(capturedSettings);
                    Object.DestroyImmediate(capturedSettings.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Attaches the blue glow marker above the head of the (cloned) base model. Idempotent:
        /// re-running the builder never stacks a second marker. The marker self-builds its sprite at
        /// runtime, so baking just the component is enough — the prefab reuses the regular mesh.
        /// </summary>
        static void AddBonusFlame(Transform root)
        {
            if (root.GetComponentInChildren<BonusFlameMarker>(true) != null)
            {
                return;
            }

            var body = ComputeBodyBounds(root);
            var flame = new GameObject("BonusFlame");
            flame.transform.SetParent(root, false);
            flame.transform.localPosition = new Vector3(0f, body.max.y + 0.15f, 0f);
            flame.AddComponent<BonusFlameMarker>();
        }

        static Bounds ComputeBodyBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            var bounds = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            for (var i = 0; i < renderers.Length; i++)
            {
                bounds.Encapsulate(root.InverseTransformPoint(renderers[i].bounds.center));
                bounds.Encapsulate(root.InverseTransformPoint(renderers[i].bounds.min));
                bounds.Encapsulate(root.InverseTransformPoint(renderers[i].bounds.max));
            }

            return bounds;
        }
    }
}
