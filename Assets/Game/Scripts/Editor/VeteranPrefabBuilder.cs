using System.IO;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds veteran champion prefabs (PRE-006b, bonus slots 7–10) from the base hero/titan
    /// prefabs by attaching the TT banner to the model's back:
    /// <c>Prefabs/Races/Humans/BonusHeroes/{Hero1..3,Titan}/Human_*_BONUS.prefab</c>.
    /// Balance and abilities are then synced by Sync Balance / Seed Unit Abilities.
    /// </summary>
    public static class VeteranPrefabBuilder
    {
        const string BannerFbxPath =
            "Assets/ToonyTinyPeople/TT_RTS/TT_RTS_Standard/models/extras/banners/TT_RTS_Banner_plain.FBX";

        [MenuItem("BARAKI/Units/Build Veteran Prefabs")]
        public static void BuildAll()
        {
            string root = UnitVisualPrefabBuilder.HumanBonusHeroesPath;
            ContentAssetPaths.EnsureFolder(root);
            ContentAssetPaths.EnsureFolder(root + "/Hero1");
            ContentAssetPaths.EnsureFolder(root + "/Hero2");
            ContentAssetPaths.EnsureFolder(root + "/Hero3");
            ContentAssetPaths.EnsureFolder(root + "/Titan");
            // Newly created folders are not visible to SaveAsPrefabAsset until imported.
            AssetDatabase.Refresh();

            Build(
                UnitVisualPrefabBuilder.HumanHero1Path,
                UnitVisualPrefabBuilder.HumanHero1BonusPath,
                UnitVisualPrefabBuilder.HumanBonusHeroesPath + "/Hero1");
            Build(
                UnitVisualPrefabBuilder.HumanHero2Path,
                UnitVisualPrefabBuilder.HumanHero2BonusPath,
                UnitVisualPrefabBuilder.HumanBonusHeroesPath + "/Hero2");
            Build(
                UnitVisualPrefabBuilder.HumanHero3Path,
                UnitVisualPrefabBuilder.HumanHero3BonusPath,
                UnitVisualPrefabBuilder.HumanBonusHeroesPath + "/Hero3");
            Build(
                UnitVisualPrefabBuilder.HumanTitanPath,
                UnitVisualPrefabBuilder.HumanTitanBonusPath,
                UnitVisualPrefabBuilder.HumanBonusHeroesPath + "/Titan");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnitVisualPrefabBuilder.EnsureContent();
            Debug.Log("VeteranPrefabBuilder: built 4 veteran prefab(s).");
        }

        static void Build(string sourcePath, string destinationPath, string folder)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"VeteranPrefabBuilder: missing source prefab '{sourcePath}'.");
                return;
            }

            // A hand-tuned banner transform (position/rotation/scale) and the synced
            // balance/abilities survive rebuilds.
            Transform authoredBanner = null;
            UnitCombatSettings capturedSettings = null;
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
            if (existingPrefab != null)
            {
                authoredBanner = existingPrefab.transform.Find("VeteranBanner");
                var existingSettings = existingPrefab.GetComponentInChildren<UnitCombatSettings>(true);
                if (existingSettings != null)
                {
                    var temp = new GameObject("VeteranBalanceCapture");
                    temp.hideFlags = HideFlags.HideAndDontSave;
                    capturedSettings = temp.AddComponent<UnitCombatSettings>();
                    capturedSettings.CopyFrom(existingSettings);
                }
            }

            ContentAssetPaths.EnsureFolder(folder);
            var banner = AssetDatabase.LoadAssetAtPath<GameObject>(BannerFbxPath);
            if (banner == null)
            {
                Debug.LogError($"VeteranPrefabBuilder: missing banner model '{BannerFbxPath}'.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = Path.GetFileNameWithoutExtension(destinationPath);

                var bannerInstance = (GameObject)PrefabUtility.InstantiatePrefab(banner, root.transform);
                bannerInstance.name = "VeteranBanner";
                if (authoredBanner != null)
                {
                    bannerInstance.transform.localPosition = authoredBanner.localPosition;
                    bannerInstance.transform.localRotation = authoredBanner.localRotation;
                    bannerInstance.transform.localScale = authoredBanner.localScale;
                }
                else
                {
                    // TT biped carries a baked yaw: model forward is +Z, cape/back is -Z.
                    // Hang the banner off the back (-Z). The banner FBX is authored much larger
                    // than TT unit models — fit it to the body height.
                    var body = ComputeBodyBounds(root.transform);
                    var bannerBounds = ComputeBodyBounds(bannerInstance.transform);
                    var bannerHeight = Mathf.Max(0.01f, bannerBounds.size.y);
                    var scale = 0.6f * body.size.y / bannerHeight;
                    bannerInstance.transform.localScale = new Vector3(scale, scale, scale);
                    var bannerDepth = ComputeLocalBounds(bannerInstance.transform).size.z;
                    bannerInstance.transform.localPosition = new Vector3(
                        body.center.x,
                        body.min.y + bannerHeight * scale * 0.5f,
                        body.center.z - body.extents.z - bannerDepth * 0.35f);
                    bannerInstance.transform.localRotation = Quaternion.identity;
                }

                bannerInstance.transform.SetSiblingIndex(root.transform.childCount - 1);

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

        static Bounds ComputeLocalBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            var bounds = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            for (var i = 0; i < renderers.Length; i++)
            {
                bounds.Encapsulate(root.InverseTransformPoint(renderers[i].bounds.min));
                bounds.Encapsulate(root.InverseTransformPoint(renderers[i].bounds.max));
            }

            return bounds;
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
