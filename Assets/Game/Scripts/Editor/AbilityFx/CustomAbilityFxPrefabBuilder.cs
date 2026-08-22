using Game.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Bakes project-authored ability VFX into <c>Assets/Game/Prefabs/Fx/Custom</c>
    /// (BARAKI Studio palette chip «Кастом»).
    /// </summary>
    public static class CustomAbilityFxPrefabBuilder
    {
        const float SkyBeamDuration = 1.15f;
        const float SkyBeamHeight = 40f;
        const float SkyBeamDiameter = 0.85f;
        const float SkyBeamCoreDiameter = 0.28f;
        const float SkyBeamImpactRadius = 1.8f;
        const float FadeOutNormalized = 0.72f;
        const int RingSegments = 8;
        const float RingSegmentSize = 0.55f;
        const float RingThickness = 0.07f;

        [MenuItem("BARAKI/Abilities/Rebuild Custom FX Prefabs", false, 25)]
        public static void RebuildMenu()
        {
            var prefab = EnsureSkyBeam();
            if (prefab == null)
            {
                Debug.LogError("CustomAbilityFxPrefabBuilder: failed to bake SkyBeam.");
                return;
            }

            Debug.Log($"CustomAbilityFxPrefabBuilder: {AssetDatabase.GetAssetPath(prefab)}");
        }

        public static GameObject EnsureSkyBeam()
        {
            ContentAssetPaths.EnsureFolder("Assets/Game/Prefabs");
            ContentAssetPaths.EnsureFolder("Assets/Game/Prefabs/Fx");
            ContentAssetPaths.EnsureFolder(ContentAssetPaths.CustomAbilityFxPrefabs);

            var material = EnsureMaterial();
            var cylinder = CreatePrimitiveMesh(PrimitiveType.Cylinder);
            var cube = CreatePrimitiveMesh(PrimitiveType.Cube);
            if (material == null || cylinder == null || cube == null)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssetPaths.SkyBeamPrefab);
            }

            var root = new GameObject("SkyBeam");
            var divine = AbilityFxColors.DivineSmite;

            var beamColor = divine;
            beamColor.a = 0.92f;
            AddMeshBurst(
                root.transform,
                "Beam",
                cylinder,
                material,
                beamColor,
                new Vector3(0f, SkyBeamHeight * 0.5f, 0f),
                new Vector3(SkyBeamDiameter, SkyBeamHeight * 0.5f, SkyBeamDiameter),
                count: 1);

            var coreColor = Color.Lerp(divine, Color.white, 0.55f);
            coreColor.a = 1f;
            AddMeshBurst(
                root.transform,
                "Core",
                cylinder,
                material,
                coreColor,
                new Vector3(0f, SkyBeamHeight * 0.49f, 0f),
                new Vector3(SkyBeamCoreDiameter, SkyBeamHeight * 0.49f, SkyBeamCoreDiameter),
                count: 1);

            var diskColor = divine;
            diskColor.a *= 0.4f;
            var diskDiameter = SkyBeamImpactRadius * 2.4f;
            AddMeshBurst(
                root.transform,
                "ImpactDisk",
                cylinder,
                material,
                diskColor,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(diskDiameter, 0.05f, diskDiameter),
                count: 1);

            var ringColor = divine;
            ringColor.a *= 0.9f;
            AddRing(root.transform, cube, material, ringColor);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ContentAssetPaths.SkyBeamPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static Material EnsureMaterial()
        {
            const string path = ContentAssetPaths.CustomAbilityFxPrefabs + "/SkyBeam.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = "SkyBeam",
            };
            material.SetFloat("_Surface", 1f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Mesh CreatePrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>() != null
                ? go.GetComponent<MeshFilter>().sharedMesh
                : null;
            Object.DestroyImmediate(go);
            return mesh;
        }

        static void AddMeshBurst(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            Color color,
            Vector3 localPosition,
            Vector3 startSize,
            int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureOneShot(ps, color, startSize, count);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void AddRing(Transform parent, Mesh cube, Material material, Color color)
        {
            var go = new GameObject("ImpactRing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureOneShot(
                ps,
                color,
                new Vector3(RingSegmentSize, RingThickness, RingSegmentSize),
                RingSegments);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = SkyBeamImpactRadius;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.alignToDirection = true;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = cube;
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void ConfigureOneShot(
            ParticleSystem ps,
            Color color,
            Vector3 startSize,
            int count)
        {
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.duration = SkyBeamDuration;
            main.startLifetime = SkyBeamDuration;
            main.startSpeed = 0f;
            main.startDelay = 0f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = count;
            main.startSize3D = true;
            main.startSizeX = startSize.x;
            main.startSizeY = startSize.y;
            main.startSizeZ = startSize.z;
            main.startColor = color;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = false;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, FadeOutNormalized),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;
        }
    }
}
