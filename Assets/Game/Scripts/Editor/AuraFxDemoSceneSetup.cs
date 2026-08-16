using Game.Gameplay.Combat;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds a standalone preview scene with Human aura bearers + CFXR loops
    /// (King / Paladin / Priest / Titan / Siege BONUS).
    /// </summary>
    public static class AuraFxDemoSceneSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/AuraFxDemo.unity";

        [MenuItem("BARAKI/FX/Build Aura Demo Scene")]
        public static void Build()
        {
            MatchFxSetup.EnsureContent();
            UnitVisualPrefabBuilder.EnsureHumanPrefabFolders();

            EnsureFolder("Assets/Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "AuraFxDemo";

            var catalog = AssetDatabase.LoadAssetAtPath<MatchFxCatalog>(MatchFxSetup.CatalogPath);
            if (catalog == null
                || catalog.AuraShinyLoop == null
                || catalog.AuraRunicLoop == null)
            {
                throw new System.InvalidOperationException(
                    "MatchFxCatalog missing CFXR aura prefabs. Run BARAKI/FX/Setup Scene first.");
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.1f;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 14f, -16f);
                camera.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 2.5f);
            var groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                groundRenderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(0.22f, 0.28f, 0.2f),
                };
            }

            var root = new GameObject("--- AURA BEARERS ---").transform;
            PlaceBearer(
                root,
                "King · Damage Aura · Runic crimson",
                UnitVisualPrefabBuilder.HumanHero1Path,
                AbilityIds.AuraDamagePercent,
                new Vector3(-10f, 0f, 0f),
                catalog);
            PlaceBearer(
                root,
                "Paladin · Haste Aura · Runic green",
                UnitVisualPrefabBuilder.HumanHero2Path,
                AbilityIds.AuraAttackSpeedPercent,
                new Vector3(-5f, 0f, 0f),
                catalog);
            PlaceBearer(
                root,
                "Priest · Armor Aura · Runic silver-blue",
                UnitVisualPrefabBuilder.HumanHero3Path,
                AbilityIds.AuraArmorPercent,
                new Vector3(0f, 0f, 0f),
                catalog);
            PlaceBearer(
                root,
                "Titan · MaxHp Aura · Runic orange + body Rays",
                UnitVisualPrefabBuilder.HumanTitanPath,
                AbilityIds.AuraMaxHpPercent,
                new Vector3(6f, 0f, 0f),
                catalog,
                attachBodyRays: true);
            PlaceBearer(
                root,
                "Siege BONUS · Regen Aura · Runic holy yellow",
                UnitVisualPrefabBuilder.HumanSiegeBonusPath,
                AbilityIds.AuraHpRegen,
                new Vector3(12f, 0f, 0f),
                catalog);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AuraFxDemo] Saved {ScenePath}. Open and press Play to preview loops.");
        }

        [MenuItem("BARAKI/FX/Open Aura Demo Scene")]
        public static void Open()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Build();
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void PlaceBearer(
            Transform parent,
            string label,
            string prefabPath,
            int abilityId,
            Vector3 position,
            MatchFxCatalog catalog,
            bool attachBodyRays = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException($"Missing unit prefab: {prefabPath}");
            }

            var unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            unit.name = label;
            unit.transform.SetParent(parent, false);
            unit.transform.position = position;
            unit.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            if (attachBodyRays && catalog.AuraRunicLoop != null)
            {
                AuraFxVisuals.AttachBodyRays(
                    unit.transform,
                    catalog.AuraRunicLoop,
                    AbilityFxColors.AuraMaxHp,
                    UnitGreyboxVisuals.TitanBodyRaysLocalScale);
            }

            var kind = PassiveAuraFxRules.ResolveKind(abilityId);
            var tint = PassiveAuraFxRules.ResolveTint(abilityId);
            var prefabFx = catalog.GetPassiveAuraPrefab(kind);
            AuraFxVisuals.Attach(unit.transform, prefabFx, kind, tint, HeroAbilityRules.AuraRadius);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(unit.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            var text = labelGo.AddComponent<TextMesh>();
            text.text = label.Replace(" · ", "\n");
            text.fontSize = 28;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
