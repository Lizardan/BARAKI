using System.IO;
using Game.Core;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Bakes 128px unit prefab thumbnails into <see cref="UnitVisualCatalog"/> portraits.</summary>
    public static class UnitPortraitBaker
    {
        public const string PortraitFolder = "Assets/Game/Art/UI/UnitPortraits";
        public const int Size = 128;

        public static void BakeIntoCatalog(UnitVisualCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            EnsureFolder(PortraitFolder);
            BakeRace(catalog, "Human", GameIds.Races.Human, "_human");
            BakeRace(catalog, "Bug", GameIds.Races.Bug, "_bug");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void BakeRace(UnitVisualCatalog catalog, string prefix, string raceId, string setPropertyName)
        {
            var so = new SerializedObject(catalog);
            var set = so.FindProperty(setPropertyName);
            var roles = new[]
            {
                UnitRole.Melee,
                UnitRole.Ranged,
                UnitRole.Caster,
                UnitRole.Siege,
                UnitRole.Flying,
                UnitRole.Super,
            };
            var portraitProps = new[]
            {
                "_meleePortrait",
                "_rangedPortrait",
                "_casterPortrait",
                "_siegePortrait",
                "_flyingPortrait",
                "_superPortrait",
            };

            for (var i = 0; i < roles.Length; i++)
            {
                if (!catalog.TryGetPrefab(raceId, roles[i], out var prefab) || prefab == null)
                {
                    continue;
                }

                var path = $"{PortraitFolder}/{prefix}_{roles[i]}.png";
                var texture = RenderPrefabThumbnail(prefab, path);
                set.FindPropertyRelative(portraitProps[i]).objectReferenceValue = texture;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Texture2D RenderPrefabThumbnail(GameObject prefab, string assetPath)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.cameraFieldOfView = 30f;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 50f;
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                preview.lights[1].intensity = 0.55f;

                var instance = preview.InstantiatePrefabInScene(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

                var bounds = CalculateBounds(instance);
                var center = bounds.center;
                var radius = Mathf.Max(bounds.extents.magnitude, 0.35f);
                var distance = radius / Mathf.Sin(preview.cameraFieldOfView * 0.5f * Mathf.Deg2Rad);
                preview.camera.transform.position = center + new Vector3(0.35f, 0.45f, -1f).normalized * distance;
                preview.camera.transform.LookAt(center);

                var rect = new Rect(0f, 0f, Size, Size);
                preview.BeginStaticPreview(rect);
                preview.Render(true);
                var result = preview.EndStaticPreview();

                Object.DestroyImmediate(instance);

                var png = result.EncodeToPNG();
                Object.DestroyImmediate(result);
                File.WriteAllBytes(Path.GetFullPath(assetPath), png);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            finally
            {
                preview.Cleanup();
            }
        }

        static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
