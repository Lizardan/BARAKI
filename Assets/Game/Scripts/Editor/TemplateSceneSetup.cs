using System.IO;
using Game.Gameplay;
using Game.Gameplay.Cameras;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using Game.Gameplay.Match.Selection;
using Game.UI.Controllers;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Idempotent setup for Game.unity (Cinemachine, level backdrop).
    /// Maintainer-only: call <see cref="ConfigureGameScene"/> from Editor code if the scene needs repair — no menu item.
    /// </summary>
    public static class TemplateSceneSetup
    {
        private const string GameScenePath = "Assets/Game/Scenes/Game.unity";
        private const string CameraRigPrefabPath = "Assets/Game/Prefabs/Cameras/GameCameraRig.prefab";
        private const string GroundMaterialPath = "Assets/Game/Art/Materials/EnvironmentGround.mat";
        private const string PanelSettingsPath = "Assets/Game/Settings/UI/DefaultPanelSettings.asset";
        private const string MatchHudUxmlPath = "Assets/Game/UI/Runtime/UXML/MatchHud.uxml";
        private const string RacePickUxmlPath = "Assets/Game/UI/Runtime/UXML/RacePick.uxml";

        public static void ConfigureGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            EnsureMatchPickableLayer();
            RaceContentBuilder.EnsureContent();
            UnitVisualPrefabBuilder.EnsureContent();
            EnsureFolder("Assets/Game/Prefabs/Effects");
            EnsureFolder("Assets/Game/Prefabs/Cameras");

            var systems = EnsureGroup("--- SYSTEMS ---");
            var cameras = EnsureGroup("--- CAMERAS ---");
            var level = EnsureGroup("--- LEVEL ---");
            var ui = EnsureGroup("--- UI ---");
            EnsureGroup("--- DYNAMIC ---");

            var mainCamera = FindOrCreateMainCamera(cameras.transform);
            var cameraTarget = EnsureCameraTarget(cameras.transform);
            var virtualCamera = EnsureGameCamera(cameras.transform, cameraTarget.transform);
            EnsureGameplayCameraPanController(cameras.transform, virtualCamera);
            EnsureGameplayCameraBinder(cameras.transform, virtualCamera, cameraTarget);

            ReparentIfExists("Directional Light", level.transform);
            EnsureLevelEnvironment(level.transform);
            EnsureGameplayReveal(level.transform);
            EnsureMatchRuntime(systems.transform);
            EnsureMatchArenaGreybox(level.transform);
            EnsureMatchHud(ui.transform);
            EnsureRacePickUi(ui.transform);

            EnsureCameraRigPrefab(cameras);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Template] Game scene configured: Cinemachine and level backdrop.");
        }

        private static void EnsureLevelEnvironment(Transform levelParent)
        {
            EnsureFolder("Assets/Game/Art/Materials");

            var environment = levelParent.Find("Environment");
            if (environment == null)
            {
                environment = new GameObject("Environment").transform;
                environment.SetParent(levelParent, false);
            }

            var groundMaterial = EnsureLitMaterial(
                GroundMaterialPath,
                new Color(0.28f, 0.48f, 0.22f),
                0.28f);

            var groundScale = MatchArenaGenerator.DefaultGroundPlaneScale;
            EnsureScaledPrimitive(
                environment,
                "Ground",
                PrimitiveType.Plane,
                groundMaterial,
                new Vector3(0f, 0f, 0f),
                new Vector3(groundScale, 1f, groundScale));

            var monolith = environment.Find("Monolith");
            if (monolith != null)
            {
                Object.DestroyImmediate(monolith.gameObject);
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 0.22f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.72f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.52f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.28f, 0.16f);
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0f;
        }

        private static void EnsureGameplayReveal(Transform levelParent)
        {
            var light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                return;
            }

            var reveal = light.GetComponent<GameplayReveal>();
            if (reveal == null)
            {
                reveal = light.gameObject.AddComponent<GameplayReveal>();
            }

            var so = new SerializedObject(reveal);
            so.FindProperty("_keyLight").objectReferenceValue = light;
            so.FindProperty("_menuIntensity").floatValue = 0.22f;
            so.FindProperty("_playIntensity").floatValue = 1.15f;
            so.FindProperty("_duration").floatValue = 0.65f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureLitMaterial(
            string assetPath,
            Color baseColor,
            float smoothness,
            Color? emission = null)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureScaledPrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Material material,
            Vector3 position,
            Vector3 scale)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.identity;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                var folder = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folder))
                {
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }

        private static GameObject EnsureGroup(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing;
            }

            return new GameObject(name);
        }

        private static GameObject FindOrCreateMainCamera(Transform parent)
        {
            var mainCameraObject = GameObject.FindWithTag("MainCamera");
            if (mainCameraObject == null)
            {
                mainCameraObject = new GameObject("Main Camera");
                mainCameraObject.tag = "MainCamera";
                var camera = mainCameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                mainCameraObject.AddComponent<AudioListener>();
            }

            if (mainCameraObject.GetComponent<CinemachineBrain>() == null)
            {
                mainCameraObject.AddComponent<CinemachineBrain>();
            }

            mainCameraObject.transform.SetParent(parent, false);
            mainCameraObject.transform.position = new Vector3(0f, 1f, -10f);
            mainCameraObject.transform.rotation = Quaternion.identity;
            return mainCameraObject;
        }

        private static GameplayCameraTarget EnsureCameraTarget(Transform parent)
        {
            var existing = parent.Find("CameraTarget");
            GameObject go;
            GameplayCameraTarget target;
            if (existing != null && existing.TryGetComponent<GameplayCameraTarget>(out target))
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("CameraTarget");
                go.transform.SetParent(parent, false);
                target = go.AddComponent<GameplayCameraTarget>();
            }

            go.transform.position = Vector3.zero;
            EnsureComponent<GameplayCameraPanController>(go);
            return target;
        }

        private static CinemachineCamera EnsureGameCamera(Transform parent, Transform followTarget)
        {
            var existing = parent.Find("GameCamera");
            CinemachineCamera vcam;
            if (existing != null)
            {
                vcam = existing.GetComponent<CinemachineCamera>();
                if (vcam == null)
                {
                    vcam = existing.gameObject.AddComponent<CinemachineCamera>();
                }
            }
            else
            {
                var go = new GameObject("GameCamera");
                go.transform.SetParent(parent, false);
                vcam = go.AddComponent<CinemachineCamera>();
            }

            vcam.Priority = 10;
            vcam.Lens.FieldOfView = GameplayCameraSettings.DefaultFieldOfViewDegrees;

            var follow = vcam.GetComponent<CinemachineFollow>();
            if (follow == null)
            {
                follow = vcam.gameObject.AddComponent<CinemachineFollow>();
            }

            follow.FollowOffset = GameplayCameraSettings.IsometricFollowOffset;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = Vector3.zero;

            if (vcam.GetComponent<CinemachineHardLookAt>() == null)
            {
                vcam.gameObject.AddComponent<CinemachineHardLookAt>();
            }

            vcam.Target.TrackingTarget = followTarget;
            vcam.Target.LookAtTarget = followTarget;
            return vcam;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var existing))
            {
                return existing;
            }

            return go.AddComponent<T>();
        }

        private static void EnsureGameplayCameraPanController(Transform camerasParent, CinemachineCamera virtualCamera)
        {
            var target = camerasParent.Find("CameraTarget");
            if (target == null || virtualCamera == null)
            {
                return;
            }

            var panController = target.GetComponent<GameplayCameraPanController>();
            if (panController == null)
            {
                return;
            }

            var follow = virtualCamera.GetComponent<CinemachineFollow>();
            var so = new SerializedObject(panController);
            so.FindProperty("_cinemachineFollow").objectReferenceValue = follow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureGameplayCameraBinder(
            Transform parent,
            CinemachineCamera virtualCamera,
            GameplayCameraTarget cameraTarget)
        {
            var binderObject = parent.Find("CameraBinder");
            GameplayCameraBinder binder;
            if (binderObject == null)
            {
                binderObject = new GameObject("CameraBinder").transform;
                binderObject.SetParent(parent, false);
                binder = binderObject.gameObject.AddComponent<GameplayCameraBinder>();
            }
            else
            {
                binder = binderObject.GetComponent<GameplayCameraBinder>();
                if (binder == null)
                {
                    binder = binderObject.gameObject.AddComponent<GameplayCameraBinder>();
                }
            }

            var so = new SerializedObject(binder);
            so.FindProperty("_virtualCamera").objectReferenceValue = virtualCamera;
            so.FindProperty("_defaultTarget").objectReferenceValue = cameraTarget;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReparentIfExists(string objectName, Transform parent)
        {
            var go = GameObject.Find(objectName);
            if (go != null)
            {
                go.transform.SetParent(parent, true);
            }
        }

        private static void EnsureMatchRuntime(Transform systemsParent)
        {
            var existing = systemsParent.Find("MatchRuntime");
            GameObject runtimeObject;
            if (existing != null)
            {
                runtimeObject = existing.gameObject;
            }
            else
            {
                runtimeObject = new GameObject("MatchRuntime");
                runtimeObject.transform.SetParent(systemsParent, false);
            }

            if (runtimeObject.GetComponent<MatchRuntime>() == null)
            {
                runtimeObject.AddComponent<MatchRuntime>();
            }

            if (runtimeObject.GetComponent<MatchCombatPresenter>() == null)
            {
                runtimeObject.AddComponent<MatchCombatPresenter>();
            }

            if (runtimeObject.GetComponent<MatchSelectionBridge>() == null)
            {
                runtimeObject.AddComponent<MatchSelectionBridge>();
            }

            if (runtimeObject.GetComponent<MatchBuildingPickPresenter>() == null)
            {
                runtimeObject.AddComponent<MatchBuildingPickPresenter>();
            }

            if (runtimeObject.GetComponent<MatchSelectionGroundRingPresenter>() == null)
            {
                runtimeObject.AddComponent<MatchSelectionGroundRingPresenter>();
            }

            if (runtimeObject.GetComponent<MatchSelectionValidityPresenter>() == null)
            {
                runtimeObject.AddComponent<MatchSelectionValidityPresenter>();
            }

            if (runtimeObject.GetComponent<BarracksSpawnDebugOverlay>() == null)
            {
                runtimeObject.AddComponent<BarracksSpawnDebugOverlay>();
            }

            if (runtimeObject.GetComponent<LaneWaypointDebugOverlay>() == null)
            {
                runtimeObject.AddComponent<LaneWaypointDebugOverlay>();
            }

            var runtime = runtimeObject.GetComponent<MatchRuntime>();
            var runtimeSo = new SerializedObject(runtime);
            runtimeSo.FindProperty("_raceCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RaceCatalog>(RaceContentBuilder.CatalogPath);
            runtimeSo.ApplyModifiedPropertiesWithoutUndo();

            var presenter = runtimeObject.GetComponent<MatchCombatPresenter>();
            var presenterSo = new SerializedObject(presenter);
            presenterSo.FindProperty("_runtime").objectReferenceValue = runtime;
            presenterSo.FindProperty("_visualCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(UnitVisualPrefabBuilder.CatalogPath);
            presenterSo.ApplyModifiedPropertiesWithoutUndo();

            var buildingPick = runtimeObject.GetComponent<MatchBuildingPickPresenter>();
            var buildingPickSo = new SerializedObject(buildingPick);
            buildingPickSo.FindProperty("_runtime").objectReferenceValue = runtime;
            buildingPickSo.ApplyModifiedPropertiesWithoutUndo();

            var groundRing = runtimeObject.GetComponent<MatchSelectionGroundRingPresenter>();
            var groundRingSo = new SerializedObject(groundRing);
            groundRingSo.FindProperty("_runtime").objectReferenceValue = runtime;
            groundRingSo.FindProperty("_combatPresenter").objectReferenceValue = presenter;
            groundRingSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureMatchHud(Transform uiParent)
        {
            var existing = uiParent.Find("MatchHud");
            GameObject hudObject;
            if (existing != null)
            {
                hudObject = existing.gameObject;
            }
            else
            {
                hudObject = new GameObject("MatchHud");
                hudObject.transform.SetParent(uiParent, false);
            }

            var uiDocument = hudObject.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = hudObject.AddComponent<UnityEngine.UIElements.UIDocument>();
            }

            uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(PanelSettingsPath);
            uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(MatchHudUxmlPath);
            uiDocument.sortingOrder = 100;

            if (hudObject.GetComponent<MatchHudController>() == null)
            {
                hudObject.AddComponent<MatchHudController>();
            }

            if (hudObject.GetComponent<MatchMinimapController>() == null)
            {
                hudObject.AddComponent<MatchMinimapController>();
            }

            if (hudObject.GetComponent<MatchContextStripController>() == null)
            {
                hudObject.AddComponent<MatchContextStripController>();
            }

            if (hudObject.GetComponent<MatchInspectorController>() == null)
            {
                hudObject.AddComponent<MatchInspectorController>();
            }

            if (hudObject.GetComponent<MatchSelectionUiGate>() == null)
            {
                hudObject.AddComponent<MatchSelectionUiGate>();
            }

            if (hudObject.GetComponent<MatchDebugHudController>() == null)
            {
                hudObject.AddComponent<MatchDebugHudController>();
            }

            var controller = hudObject.GetComponent<MatchHudController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_uiDocument").objectReferenceValue = uiDocument;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            WireUiDocument(hudObject.GetComponent<MatchMinimapController>(), uiDocument);
            WireUiDocument(hudObject.GetComponent<MatchContextStripController>(), uiDocument);
            WireUiDocument(hudObject.GetComponent<MatchInspectorController>(), uiDocument);
            WireUiDocument(hudObject.GetComponent<MatchSelectionUiGate>(), uiDocument);
            WireUiDocument(hudObject.GetComponent<MatchDebugHudController>(), uiDocument);
        }

        static void WireUiDocument(MonoBehaviour component, UnityEngine.UIElements.UIDocument uiDocument)
        {
            if (component == null || uiDocument == null)
            {
                return;
            }

            var so = new SerializedObject(component);
            so.FindProperty("_uiDocument").objectReferenceValue = uiDocument;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureRacePickUi(Transform uiParent)
        {
            var existing = uiParent.Find("RacePick");
            GameObject pickObject;
            if (existing != null)
            {
                pickObject = existing.gameObject;
            }
            else
            {
                pickObject = new GameObject("RacePick");
                pickObject.transform.SetParent(uiParent, false);
            }

            var uiDocument = pickObject.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = pickObject.AddComponent<UnityEngine.UIElements.UIDocument>();
            }

            uiDocument.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(PanelSettingsPath);
            uiDocument.visualTreeAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(RacePickUxmlPath);
            uiDocument.sortingOrder = 200;

            if (pickObject.GetComponent<RacePickController>() == null)
            {
                pickObject.AddComponent<RacePickController>();
            }

            var controller = pickObject.GetComponent<RacePickController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_uiDocument").objectReferenceValue = uiDocument;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureMatchArenaGreybox(Transform levelParent)
        {
            var existing = levelParent.Find("MatchArena");
            GameObject arenaObject;
            if (existing != null)
            {
                arenaObject = existing.gameObject;
            }
            else
            {
                arenaObject = new GameObject("MatchArena");
                arenaObject.transform.SetParent(levelParent, false);
            }

            var greybox = arenaObject.GetComponent<MatchArenaGreybox>();
            if (greybox == null)
            {
                greybox = arenaObject.AddComponent<MatchArenaGreybox>();
            }

            var so = new SerializedObject(greybox);
            so.FindProperty("_playerCount").intValue = 4;
            so.FindProperty("_arenaRadius").floatValue = MatchArenaGenerator.DefaultArenaRadius;
            so.FindProperty("_mainToTowerDistance").floatValue = MatchArenaGenerator.DefaultMainToTowerDistance;
            so.FindProperty("_centerArenaRadius").floatValue = LaneGraphBuilder.DefaultCenterArenaRadius;
            so.FindProperty("_buildOnAwake").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureCameraRigPrefab(GameObject camerasGroup)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CameraRigPrefabPath) != null)
            {
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(camerasGroup, CameraRigPrefabPath);
        }

        static void EnsureMatchPickableLayer()
        {
            const string layerName = MatchPickLayers.PickableLayerName;
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                return;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            var layers = tagManager.FindProperty("layers");
            for (var i = 8; i < 32; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    return;
                }
            }

            for (var i = 8; i < 32; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
        }
    }
}
