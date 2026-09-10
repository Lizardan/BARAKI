using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Dev-helper: builds a read-only showcase scene with every visual prefab of both races
    /// placed side by side at their exact in-game scale (definition VisualScale, no presenter
    /// multiply) with proper per-role lift and labels. Run from the target scene via
    /// <c>BARAKI/Dev/Build Unit Scale Showcase</c>; re-running rebuilds from scratch.
    /// Idle animation plays once the scene runs in play mode (animators start in the default state).
    /// </summary>
    public static class UnitScaleShowcaseBuilder
    {
        const string RootName = "UnitScaleShowcase";

        static readonly UnitRole[] BaseUnitRoles =
        {
            UnitRole.Melee,
            UnitRole.Ranged,
            UnitRole.Caster,
            UnitRole.Siege,
            UnitRole.Flying,
            UnitRole.Super,
        };

        static readonly string[] HeroFields = { "_hero1", "_hero2", "_hero3" };

        const float UnitsZ = 0f;
        const float HeroesZ = -45f;
        const float TitansZ = -75f;
        const float RaceSpacing = 34f;
        const float CellBaseSpacing = 3.4f;

        [MenuItem("BARAKI/Dev/Build Unit Scale Showcase")]
        public static void Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);

            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("UnitScaleShowcaseBuilder: visual catalog not found.");
                return;
            }

            var races = new SerializedObject(catalog).FindProperty("_races");
            var raceStartX = new List<float>();
            var raceX = -RaceSpacing * 0.5f;
            var count = 0;

            for (var r = 0; r < races.arraySize; r++)
            {
                var visuals = races.GetArrayElementAtIndex(r).FindPropertyRelative("_visuals");

                raceStartX.Add(raceX);

                var unitX = raceX;
                foreach (var role in BaseUnitRoles)
                {
                    Place(visuals, FieldForUnitRole(role), role, root.transform, ref unitX, UnitsZ, ref count);
                    Place(visuals, FieldForUnitRole(role) + "Bonus", role, root.transform, ref unitX, UnitsZ, ref count);
                    if (role == UnitRole.Melee)
                    {
                        Place(visuals, "_meleeServant", role, root.transform, ref unitX, UnitsZ, ref count);
                    }

                    unitX += CellBaseSpacing;
                }

                var heroX = raceX;
                foreach (var field in HeroFields)
                {
                    Place(visuals, field, UnitRole.Hero, root.transform, ref heroX, HeroesZ, ref count);
                    heroX += CellBaseSpacing;
                }

                var titanX = raceX;
                Place(visuals, "_titan", UnitRole.Titan, root.transform, ref titanX, TitansZ, ref count);

                raceX += RaceSpacing;
            }

            var centerX = raceStartX.Count > 0
                ? raceStartX[0] + raceX * 0.5f - RaceSpacing * 0.5f
                : 0f;
            var lookTarget = new Vector3(centerX, 7f, (UnitsZ + TitansZ) * 0.5f);
            var camera = EnsureCamera(centerX, lookTarget);
            EnsureLight(camera.transform);
            Selection.activeGameObject = root;

            Debug.Log(
                $"UnitScaleShowcaseBuilder: built {count} prefab(s) next to each other at in-game scale.");
        }

        static void Place(
            SerializedProperty visuals,
            string field,
            UnitRole role,
            Transform parent,
            ref float x,
            float z,
            ref int count)
        {
            var prefab = visuals.FindPropertyRelative(field).objectReferenceValue as GameObject;
            if (prefab == null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = prefab.name;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale =
                Vector3.one * UnitGreyboxVisuals.ResolveAuthorVisualScale(prefab, instance);

            var offset = UnitGreyboxVisuals.GetModelLocalOffset(role);
            instance.transform.localPosition = new Vector3(x, offset.y, z);
            AddLabel(instance.transform, role == UnitRole.Melee ? prefab.name : $"{role}: {prefab.name}");

            x += CellBaseSpacing + instance.transform.localScale.x * 1.6f;
            count++;
        }

        static void AddLabel(Transform host, string text)
        {
            var label = new GameObject("ScaleLabel");
            label.transform.SetParent(host, false);
            label.transform.localPosition = new Vector3(0f, -0.35f, 1.1f);

            var textMesh = label.AddComponent<TextMesh>();
            textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.fontSize = 64;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.04f;
            textMesh.color = new Color(1f, 1f, 0.85f, 0.9f);
            textMesh.text = text;
        }

        static Camera EnsureCamera(float centerX, Vector3 lookTarget)
        {
            var go = new GameObject("ShowcaseCamera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 55;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(centerX + 4f, 40f, -100f);
            camera.transform.LookAt(lookTarget);
            return camera;
        }

        static void EnsureLight(Transform nearCamera)
        {
            var go = new GameObject("ShowcaseLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.98f, 0.94f);
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.transform.position = nearCamera.position + Vector3.up;
            light.transform.SetParent(nearCamera, false);
        }

        static string FieldForUnitRole(UnitRole role) => role switch
        {
            UnitRole.Melee => "_melee",
            UnitRole.Ranged => "_ranged",
            UnitRole.Caster => "_caster",
            UnitRole.Siege => "_siege",
            UnitRole.Flying => "_flying",
            UnitRole.Super => "_super",
            _ => "_melee",
        };
    }
}