using System.IO;
using Game.Core;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Bakes 128px unit prefab thumbnails into <see cref="UnitVisualCatalog"/> portraits.</summary>
    public static class UnitPortraitBaker
    {
        public const string PortraitFolder = ContentAssetPaths.PortraitRoot;
        public const int Size = 128;
        const int RenderSize = 256;
        const int PaddingPx = 2;
        const float YawDegrees = 145f;
        static readonly Color KeyColor = new(1f, 0f, 1f, 1f);
        static readonly Color32 BackdropColor = new(0x52, 0x52, 0x52, 255);

        public static void BakeIntoCatalog(UnitVisualCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitUnits);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitHeroes);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitBonusUnits);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.HumanPortraitBonusHeroes);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessPortraitUnits);
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.FacelessPortraitHeroes);
            BakeRace(catalog, GameIds.Races.Human);
            BakeRace(catalog, GameIds.Races.Faceless);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void BakeRace(UnitVisualCatalog catalog, string raceId)
        {
            var so = new SerializedObject(catalog);
            var set = FindRaceVisuals(so.FindProperty("_races"), raceId);
            if (set == null)
            {
                return;
            }

            var unitPortraitFolder = raceId == GameIds.Races.Faceless
                ? ContentAssetPaths.FacelessPortraitUnits
                : ContentAssetPaths.HumanPortraitUnits;
            var heroPortraitFolder = raceId == GameIds.Races.Faceless
                ? ContentAssetPaths.FacelessPortraitHeroes
                : ContentAssetPaths.HumanPortraitHeroes;

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

                var path = $"{unitPortraitFolder}/{roles[i]}.png";
                var texture = RenderPrefabThumbnail(prefab, path);
                set.FindPropertyRelative(portraitProps[i]).objectReferenceValue = texture;
            }

            BakeChampion(catalog, set, raceId, UnitRole.Hero, 1, "_hero1Portrait",
                $"{heroPortraitFolder}/Hero1.png");
            BakeChampion(catalog, set, raceId, UnitRole.Hero, 2, "_hero2Portrait",
                $"{heroPortraitFolder}/Hero2.png");
            BakeChampion(catalog, set, raceId, UnitRole.Hero, 3, "_hero3Portrait",
                $"{heroPortraitFolder}/Hero3.png");
            BakeChampion(catalog, set, raceId, UnitRole.Titan, 0, "_titanPortrait",
                $"{heroPortraitFolder}/Titan.png");

            if (raceId != GameIds.Races.Human)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            BakeBonusPortraits(catalog, set, raceId);

            BakeChampion(catalog, set, raceId, UnitRole.Hero, 1, "_hero1BonusPortrait",
                $"{ContentAssetPaths.HumanPortraitBonusHeroes}/Hero1.png", BonusKitRules.BonusSlotForHeroSlot(1));
            BakeChampion(catalog, set, raceId, UnitRole.Hero, 2, "_hero2BonusPortrait",
                $"{ContentAssetPaths.HumanPortraitBonusHeroes}/Hero2.png", BonusKitRules.BonusSlotForHeroSlot(2));
            BakeChampion(catalog, set, raceId, UnitRole.Hero, 3, "_hero3BonusPortrait",
                $"{ContentAssetPaths.HumanPortraitBonusHeroes}/Hero3.png", BonusKitRules.BonusSlotForHeroSlot(3));
            BakeChampion(catalog, set, raceId, UnitRole.Titan, 0, "_titanBonusPortrait",
                $"{ContentAssetPaths.HumanPortraitBonusHeroes}/Titan.png", BonusKitRules.TitanBonusSlot);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BakeBonusPortraits(
            UnitVisualCatalog catalog,
            SerializedProperty set,
            string raceId)
        {
            var portraitProps = new[]
            {
                "_meleeBonusPortrait",
                "_rangedBonusPortrait",
                "_casterBonusPortrait",
                "_siegeBonusPortrait",
                "_flyingBonusPortrait",
                "_superBonusPortrait",
            };
            var roles = new[]
            {
                UnitRole.Melee,
                UnitRole.Ranged,
                UnitRole.Caster,
                UnitRole.Siege,
                UnitRole.Flying,
                UnitRole.Super,
            };

            for (var i = 0; i < roles.Length; i++)
            {
                var bonusSlot = i + 1;
                if (!catalog.TryGetPrefab(raceId, roles[i], heroSlot: 0, bonusSlot, out var prefab)
                    || prefab == null)
                {
                    continue;
                }

                var path = $"{ContentAssetPaths.HumanPortraitBonusUnits}/{roles[i]}.png";
                var texture = RenderPrefabThumbnail(prefab, path);
                set.FindPropertyRelative(portraitProps[i]).objectReferenceValue = texture;
            }
        }

        static SerializedProperty FindRaceVisuals(SerializedProperty races, string raceId)
        {
            for (var i = 0; i < races.arraySize; i++)
            {
                var entry = races.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_raceId").stringValue == raceId)
                {
                    return entry.FindPropertyRelative("_visuals");
                }
            }

            return null;
        }

        static void BakeChampion(
            UnitVisualCatalog catalog,
            SerializedProperty set,
            string raceId,
            UnitRole role,
            int heroSlot,
            string portraitProperty,
            string assetPath,
            int bonusSlot = 0)
        {
            if (bonusSlot > 0 && !catalog.HasBonusPrefab(raceId, bonusSlot))
            {
                // Veteran prefab not built yet — keep the previous portrait instead of re-baking the base.
                return;
            }

            if (!catalog.TryGetPrefab(raceId, role, heroSlot, bonusSlot, out var prefab) || prefab == null)
            {
                return;
            }

            var texture = RenderPrefabThumbnail(prefab, assetPath);
            set.FindPropertyRelative(portraitProperty).objectReferenceValue = texture;
        }

        static Texture2D RenderPrefabThumbnail(GameObject prefab, string assetPath)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                preview.lights[1].intensity = 0.55f;

                var instance = preview.InstantiatePrefabInScene(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.Euler(0f, YawDegrees, 0f);

                var bounds = CalculateBounds(instance);
                var captured = CapturePreview(preview, bounds);
                Object.DestroyImmediate(instance);

                var fitted = FitToPortrait(captured);
                Object.DestroyImmediate(captured);

                var png = fitted.EncodeToPNG();
                Object.DestroyImmediate(fitted);
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

        static Texture2D CapturePreview(PreviewRenderUtility preview, Bounds bounds)
        {
            var rect = new Rect(0f, 0f, RenderSize, RenderSize);
            preview.BeginStaticPreview(rect);

            var camera = preview.camera;
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = KeyColor;
            camera.allowMSAA = false;
            camera.aspect = 1f;

            var center = bounds.center;
            var radius = Mathf.Max(bounds.extents.magnitude, 0.35f);
            camera.transform.position =
                center + new Vector3(0.35f, 0.45f, -1f).normalized * (radius * 2.5f);
            camera.transform.LookAt(center);
            FrameOrthographic(camera, bounds);

            preview.Render(true);
            return preview.EndStaticPreview();
        }

        static void FrameOrthographic(Camera camera, Bounds bounds)
        {
            GetCameraLocalExtents(camera, bounds, out var minX, out var maxX, out var minY, out var maxY);
            var cx = (minX + maxX) * 0.5f;
            var cy = (minY + maxY) * 0.5f;
            camera.transform.position += camera.transform.right * cx + camera.transform.up * cy;

            var half = Mathf.Max(maxX - minX, maxY - minY) * 0.5f;
            camera.orthographicSize = Mathf.Max(half * 1.08f, 0.05f);
        }

        static void GetCameraLocalExtents(
            Camera camera,
            Bounds bounds,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = minY = float.PositiveInfinity;
            maxX = maxY = float.NegativeInfinity;
            var min = bounds.min;
            var max = bounds.max;
            var corners = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (var i = 0; i < corners.Length; i++)
            {
                var local = camera.transform.InverseTransformPoint(corners[i]);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }
        }

        static Texture2D FitToPortrait(Texture2D source)
        {
            var pixels = source.GetPixels32();
            var width = source.width;
            var height = source.height;
            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;

            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    if (IsKey(pixels[row + x]))
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            var dest = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var destPixels = new Color32[Size * Size];
            for (var i = 0; i < destPixels.Length; i++)
            {
                destPixels[i] = BackdropColor;
            }

            if (maxX >= minX)
            {
                var contentW = maxX - minX + 1;
                var contentH = maxY - minY + 1;
                var inner = Size - PaddingPx * 2;
                var scale = inner / (float)Mathf.Max(contentW, contentH);
                var destW = Mathf.Max(1, Mathf.RoundToInt(contentW * scale));
                var destH = Mathf.Max(1, Mathf.RoundToInt(contentH * scale));
                var ox = (Size - destW) / 2;
                var oy = (Size - destH) / 2;

                for (var y = 0; y < destH; y++)
                {
                    var srcY = minY + Mathf.Clamp(y * contentH / destH, 0, contentH - 1);
                    var srcRow = srcY * width;
                    var destRow = (oy + y) * Size + ox;
                    for (var x = 0; x < destW; x++)
                    {
                        var srcX = minX + Mathf.Clamp(x * contentW / destW, 0, contentW - 1);
                        var pixel = pixels[srcRow + srcX];
                        destPixels[destRow + x] = IsKey(pixel) ? BackdropColor : pixel;
                    }
                }
            }

            dest.SetPixels32(destPixels);
            dest.Apply(false, false);
            return dest;
        }

        static bool IsKey(Color32 pixel) => pixel.r >= 240 && pixel.g <= 20 && pixel.b >= 240;

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
