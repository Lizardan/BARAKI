using System;
using System.IO;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Stylized low-poly unit prefabs (Human × 6 roles) and <see cref="UnitVisualCatalog"/>.</summary>
    public static class UnitVisualPrefabBuilder
    {
        public const string RootPath = "Assets/Game/Prefabs/Units";
        public const string HumanPath = RootPath + "/Human";
        public const string CatalogPath = "Assets/Game/ScriptableObjects/UnitVisualCatalog.asset";
        public const string HumanMeleePath = HumanPath + "/Human_Melee.prefab";
        public const string HumanRangedPath = HumanPath + "/Human_Ranged.prefab";
        public const string HumanCasterPath = HumanPath + "/Human_Caster.prefab";
        public const string HumanSiegePath = HumanPath + "/Human_Siege.prefab";
        public const string HumanFlyingPath = HumanPath + "/Human_Flying.prefab";
        public const string HumanSuperPath = HumanPath + "/Human_Super.prefab";
        public const string UnitTeamAccentMaterialPath =
            "Assets/Game/Art/Materials/Units/UnitTeamAccent.mat";

        static readonly string[] HumanAnimatedPrefabPaths =
        {
            HumanMeleePath,
            HumanRangedPath,
            HumanCasterPath,
            HumanSiegePath,
            HumanFlyingPath,
            HumanSuperPath,
        };

        public static void EnsureContent()
        {
            UnitGreyboxMaterialPalette.EnsureMaterials();
            EnsureFolder(RootPath);
            EnsureFolder(HumanPath);
            EnsureFolder("Assets/Game/ScriptableObjects");

            var humanPrefabs = LoadAnimatedHumanPrefabs();
            UpdateCatalogFromPrefabs(humanPrefabs);
            UnitPortraitBaker.BakeIntoCatalog(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// All Human combat roles live under Units/Human (animated FBX + Animator).
        /// Rebuild via menu BARAKI/Units/Rebuild Human Animated Prefabs when models change.
        /// </summary>
        static GameObject[] LoadAnimatedHumanPrefabs()
        {
            var prefabs = new GameObject[HumanAnimatedPrefabPaths.Length];
            for (var i = 0; i < HumanAnimatedPrefabPaths.Length; i++)
            {
                var path = HumanAnimatedPrefabPaths[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Missing animated human unit prefab at '{path}'. " +
                        "Run BARAKI/Units/Rebuild Human Animated Prefabs first.");
                }

                prefabs[i] = prefab;
            }

            return prefabs;
        }

        static GameObject BuildHuman(UnitRole role, string name)
        {
            var root = new GameObject(name);
            var body = CreateChild(root.transform, "Body");

            switch (role)
            {
                case UnitRole.Melee:
                    BuildHumanMelee(body);
                    break;
                case UnitRole.Ranged:
                    BuildHumanRanged(body);
                    break;
                case UnitRole.Caster:
                    BuildHumanCaster(body);
                    break;
                case UnitRole.Siege:
                    BuildHumanKnight(body);
                    break;
                case UnitRole.Flying:
                    BuildHumanGriffin(body);
                    break;
                case UnitRole.Super:
                    BuildHumanChampion(body);
                    break;
            }

            return root;
        }

        static void BuildHumanMelee(GameObject body)
        {
            var frame = BuildHumanSoldierFrame(body, armor: true, scale: 1f);
            AddPart(body, "Helmet", PrimitiveType.Sphere, frame.HeadCenter + new Vector3(0f, 0.06f, 0f),
                new Vector3(0.34f, 0.28f, 0.34f), UnitGreyboxMaterialPalette.HumanSteel);
            AddTeamAccent(body.transform, "TeamAccent_Plume", PrimitiveType.Cube,
                frame.HeadCenter + new Vector3(0f, 0.28f, 0f), new Vector3(0.06f, 0.28f, 0.1f));
            AddHumanKiteShield(body, frame.TorsoCenter + new Vector3(-0.42f, 0.02f, 0.12f), 1f);
            AddHumanSword(body, frame.TorsoCenter + new Vector3(0.38f, 0f, 0.2f), 1f);
            AddTeamAccent(body.transform, "TeamAccent_Tabard", PrimitiveType.Cube,
                frame.TorsoCenter + new Vector3(0f, -0.05f, 0.18f), new Vector3(0.28f, 0.35f, 0.04f));
        }

        static void BuildHumanRanged(GameObject body)
        {
            var frame = BuildHumanSoldierFrame(body, armor: false, scale: 0.95f);
            AddPart(body, "Hood", PrimitiveType.Sphere, frame.HeadCenter + new Vector3(0f, 0.04f, -0.02f),
                new Vector3(0.36f, 0.32f, 0.38f), UnitGreyboxMaterialPalette.HumanCloth);
            AddTeamAccent(body.transform, "TeamAccent_Tunic", PrimitiveType.Cube,
                frame.TorsoCenter + new Vector3(0f, 0f, 0.16f), new Vector3(0.36f, 0.5f, 0.05f));
            AddPart(body, "Quiver", PrimitiveType.Cube, frame.TorsoCenter + new Vector3(-0.22f, 0.05f, -0.2f),
                new Vector3(0.12f, 0.45f, 0.12f), UnitGreyboxMaterialPalette.HumanLeather);
            AddHumanBow(body, frame.TorsoCenter);
        }

        static void BuildHumanCaster(GameObject body)
        {
            const float robeHalf = 0.7f;
            AddPart(body, "Robe", PrimitiveType.Cylinder, new Vector3(0f, robeHalf, 0f),
                new Vector3(0.85f, robeHalf, 0.85f), UnitGreyboxMaterialPalette.HumanCloth);
            AddTeamAccent(body.transform, "TeamAccent_Trim", PrimitiveType.Cylinder,
                new Vector3(0f, robeHalf + 0.55f, 0f), new Vector3(0.92f, 0.08f, 0.92f));
            var headY = robeHalf * 2f + 0.14f;
            AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, headY, 0f),
                new Vector3(0.3f, 0.3f, 0.3f), UnitGreyboxMaterialPalette.HumanSkin);
            AddPart(body, "Hood", PrimitiveType.Sphere, new Vector3(0f, headY + 0.04f, -0.04f),
                new Vector3(0.38f, 0.34f, 0.4f), UnitGreyboxMaterialPalette.HumanCloth);
            AddPart(body, "Staff", PrimitiveType.Cylinder, new Vector3(0.32f, 0.75f, 0.1f),
                new Vector3(0.07f, 0.75f, 0.07f), UnitGreyboxMaterialPalette.HumanWood);
            AddPart(body, "Crystal", PrimitiveType.Sphere, new Vector3(0.32f, 1.6f, 0.1f),
                new Vector3(0.22f, 0.22f, 0.22f), UnitGreyboxMaterialPalette.HumanArcane);
            AddTeamAccent(body.transform, "TeamAccent_OrbGlow", PrimitiveType.Sphere,
                new Vector3(0.32f, 1.6f, 0.1f), new Vector3(0.12f, 0.12f, 0.12f));
        }

        static void BuildHumanKnight(GameObject body)
        {
            AddPart(body, "HorseBody", PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.55f, 0.7f, 1.15f), UnitGreyboxMaterialPalette.HumanLeather);
            AddPart(body, "HorseNeck", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0.55f),
                new Vector3(0.28f, 0.35f, 0.35f), Quaternion.Euler(-35f, 0f, 0f),
                UnitGreyboxMaterialPalette.HumanLeather);
            AddPart(body, "HorseHead", PrimitiveType.Sphere, new Vector3(0f, 1.1f, 0.85f),
                new Vector3(0.28f, 0.24f, 0.36f), UnitGreyboxMaterialPalette.HumanLeather);
            AddHorseLegs(body);
            AddTeamAccent(body.transform, "TeamAccent_Barding", PrimitiveType.Cube,
                new Vector3(0f, 0.6f, 0f), new Vector3(0.62f, 0.35f, 0.9f));

            AddPart(body, "RiderTorso", PrimitiveType.Capsule, new Vector3(0f, 1.15f, -0.05f),
                new Vector3(0.4f, 0.35f, 0.28f), UnitGreyboxMaterialPalette.HumanSteel);
            AddPart(body, "RiderHead", PrimitiveType.Sphere, new Vector3(0f, 1.57f, -0.05f),
                new Vector3(0.26f, 0.26f, 0.26f), UnitGreyboxMaterialPalette.HumanSkin);
            AddPart(body, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 1.65f, -0.05f),
                new Vector3(0.3f, 0.24f, 0.3f), UnitGreyboxMaterialPalette.HumanSteel);
            AddTeamAccent(body.transform, "TeamAccent_Shield", PrimitiveType.Cube,
                new Vector3(-0.32f, 1.2f, 0.1f), new Vector3(0.32f, 0.4f, 0.06f));
            AddPart(body, "Lance", PrimitiveType.Cylinder, new Vector3(0.28f, 1.3f, 0.5f),
                new Vector3(0.05f, 0.05f, 1.1f), Quaternion.Euler(12f, 0f, 0f),
                UnitGreyboxMaterialPalette.HumanWood);
            AddPart(body, "LanceTip", PrimitiveType.Cube, new Vector3(0.28f, 1.37f, 1.1f),
                new Vector3(0.06f, 0.06f, 0.14f), UnitGreyboxMaterialPalette.HumanSteel);
        }

        static void BuildHumanGriffin(GameObject body)
        {
            var torsoY = 0.7f;
            AddPart(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, torsoY, 0f),
                new Vector3(0.55f, 0.45f, 0.85f), UnitGreyboxMaterialPalette.HumanLeather);
            AddPart(body, "LionHind", PrimitiveType.Sphere, new Vector3(0f, torsoY - 0.05f, -0.4f),
                new Vector3(0.55f, 0.45f, 0.55f), UnitGreyboxMaterialPalette.HumanGold);
            AddPart(body, "EagleHead", PrimitiveType.Sphere, new Vector3(0f, torsoY + 0.25f, 0.5f),
                new Vector3(0.35f, 0.32f, 0.4f), UnitGreyboxMaterialPalette.HumanSkin);
            AddPart(body, "Beak", PrimitiveType.Cube, new Vector3(0f, torsoY + 0.23f, 0.78f),
                new Vector3(0.1f, 0.08f, 0.2f), UnitGreyboxMaterialPalette.HumanGold);
            AddPart(body, "WingL", PrimitiveType.Cube, new Vector3(-0.55f, torsoY + 0.15f, 0f),
                new Vector3(0.85f, 0.06f, 0.45f), Quaternion.Euler(0f, 0f, 18f),
                UnitGreyboxMaterialPalette.HumanLeather);
            AddPart(body, "WingR", PrimitiveType.Cube, new Vector3(0.55f, torsoY + 0.15f, 0f),
                new Vector3(0.85f, 0.06f, 0.45f), Quaternion.Euler(0f, 0f, -18f),
                UnitGreyboxMaterialPalette.HumanLeather);
            AddTeamAccent(body.transform, "TeamAccent_Collar", PrimitiveType.Cylinder,
                new Vector3(0f, torsoY + 0.2f, 0.25f), new Vector3(0.5f, 0.08f, 0.5f));
            AddPart(body, "TalonL", PrimitiveType.Cube, new Vector3(-0.18f, torsoY - 0.45f, 0.15f),
                new Vector3(0.12f, 0.2f, 0.2f), UnitGreyboxMaterialPalette.HumanGold);
            AddPart(body, "TalonR", PrimitiveType.Cube, new Vector3(0.18f, torsoY - 0.45f, 0.15f),
                new Vector3(0.12f, 0.2f, 0.2f), UnitGreyboxMaterialPalette.HumanGold);
        }

        static void BuildHumanChampion(GameObject body)
        {
            var frame = BuildHumanSoldierFrame(body, armor: true, scale: 1.35f);
            AddPart(body, "CrownHelm", PrimitiveType.Cylinder, frame.HeadCenter + new Vector3(0f, 0.1f, 0f),
                new Vector3(0.4f, 0.12f, 0.4f), UnitGreyboxMaterialPalette.HumanGold);
            AddPart(body, "PauldronL", PrimitiveType.Cube, frame.TorsoCenter + new Vector3(-0.42f, 0.25f, 0f),
                new Vector3(0.32f, 0.22f, 0.38f), UnitGreyboxMaterialPalette.HumanSteel);
            AddPart(body, "PauldronR", PrimitiveType.Cube, frame.TorsoCenter + new Vector3(0.42f, 0.25f, 0f),
                new Vector3(0.32f, 0.22f, 0.38f), UnitGreyboxMaterialPalette.HumanSteel);
            AddHumanKiteShield(body, frame.TorsoCenter + new Vector3(-0.55f, 0f, 0.15f), 1.35f);
            AddPart(body, "HammerHaft", PrimitiveType.Cylinder, frame.TorsoCenter + new Vector3(0.48f, 0.1f, 0.15f),
                new Vector3(0.08f, 0.55f, 0.08f), Quaternion.Euler(0f, 0f, -25f),
                UnitGreyboxMaterialPalette.HumanWood);
            var hammerHead = frame.TorsoCenter + new Vector3(0.48f, 0.75f, 0.15f);
            AddPart(body, "HammerHead", PrimitiveType.Cube, hammerHead,
                new Vector3(0.35f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.HumanSteel);
            AddPart(body, "HammerCrystal", PrimitiveType.Sphere, hammerHead,
                new Vector3(0.14f, 0.14f, 0.14f), UnitGreyboxMaterialPalette.HumanArcane);
            AddTeamAccent(body.transform, "TeamAccent_Cape", PrimitiveType.Cube,
                frame.TorsoCenter + new Vector3(0f, 0.05f, -0.22f), new Vector3(0.45f, 0.7f, 0.06f));
        }

        static HumanFrame BuildHumanSoldierFrame(GameObject body, bool armor, float scale)
        {
            var s = scale;
            const float legHalf = 0.32f;
            AddPart(body, "LegL", PrimitiveType.Capsule, new Vector3(-0.12f * s, legHalf * s, 0f),
                new Vector3(0.18f * s, legHalf * s, 0.18f * s),
                armor ? UnitGreyboxMaterialPalette.HumanSteel : UnitGreyboxMaterialPalette.HumanLeather);
            AddPart(body, "LegR", PrimitiveType.Capsule, new Vector3(0.12f * s, legHalf * s, 0f),
                new Vector3(0.18f * s, legHalf * s, 0.18f * s),
                armor ? UnitGreyboxMaterialPalette.HumanSteel : UnitGreyboxMaterialPalette.HumanLeather);

            var legTop = Stack.Top(legHalf * s, legHalf * s);
            const float pelvisHalf = 0.07f;
            AddPart(body, "Pelvis", PrimitiveType.Cube, new Vector3(0f, legTop + pelvisHalf * s, 0f),
                new Vector3(0.32f * s, pelvisHalf * 2f * s, 0.24f * s), UnitGreyboxMaterialPalette.HumanLeather);

            var pelvisTop = legTop + pelvisHalf * 2f * s;
            const float torsoHalf = 0.34f;
            var torsoMat = armor ? UnitGreyboxMaterialPalette.HumanSteel : UnitGreyboxMaterialPalette.HumanCloth;
            var torsoCenter = new Vector3(0f, pelvisTop + torsoHalf * s, 0f);
            AddPart(body, "Torso", PrimitiveType.Capsule, torsoCenter,
                new Vector3(0.48f * s, torsoHalf * s, 0.32f * s), torsoMat);

            var torsoTop = pelvisTop + torsoHalf * 2f * s;
            const float headRadius = 0.13f;
            var headCenter = new Vector3(0f, torsoTop + headRadius * s, 0f);
            AddPart(body, "Head", PrimitiveType.Sphere, headCenter,
                new Vector3(headRadius * 2f * s, headRadius * 2f * s, headRadius * 2f * s),
                UnitGreyboxMaterialPalette.HumanSkin);

            AddPart(body, "ArmL", PrimitiveType.Capsule, torsoCenter + new Vector3(-0.32f * s, 0.05f * s, 0f),
                new Vector3(0.14f * s, 0.28f * s, 0.14f * s),
                armor ? UnitGreyboxMaterialPalette.HumanSteel : UnitGreyboxMaterialPalette.HumanLeather);
            AddPart(body, "ArmR", PrimitiveType.Capsule, torsoCenter + new Vector3(0.32f * s, 0.05f * s, 0f),
                new Vector3(0.14f * s, 0.28f * s, 0.14f * s),
                armor ? UnitGreyboxMaterialPalette.HumanSteel : UnitGreyboxMaterialPalette.HumanLeather);

            return new HumanFrame { TorsoCenter = torsoCenter, HeadCenter = headCenter, Scale = s };
        }

        static void AddHumanKiteShield(GameObject body, Vector3 localPos, float scale)
        {
            AddPart(body, "Shield", PrimitiveType.Cube, localPos,
                new Vector3(0.42f * scale, 0.62f * scale, 0.08f * scale),
                Quaternion.Euler(0f, 25f, 0f), UnitGreyboxMaterialPalette.HumanSteel);
            AddPart(body, "ShieldRim", PrimitiveType.Cube, localPos + new Vector3(0f, 0f, 0.02f),
                new Vector3(0.48f * scale, 0.68f * scale, 0.05f * scale),
                Quaternion.Euler(0f, 25f, 0f), UnitGreyboxMaterialPalette.HumanGold);
            AddTeamAccent(body.transform, "TeamAccent_ShieldFace", PrimitiveType.Cube,
                localPos + new Vector3(0.02f, 0f, 0.06f),
                new Vector3(0.34f * scale, 0.52f * scale, 0.04f * scale));
        }

        static void AddHumanSword(GameObject body, Vector3 localPos, float scale)
        {
            AddPart(body, "SwordGuard", PrimitiveType.Cube, localPos,
                new Vector3(0.22f * scale, 0.08f * scale, 0.08f * scale), UnitGreyboxMaterialPalette.HumanGold);
            AddPart(body, "SwordBlade", PrimitiveType.Cube, localPos + new Vector3(0f, 0f, 0.42f * scale),
                new Vector3(0.08f * scale, 0.1f * scale, 0.75f * scale), UnitGreyboxMaterialPalette.HumanSteel);
            AddPart(body, "SwordHilt", PrimitiveType.Cylinder, localPos + new Vector3(0f, 0f, -0.12f * scale),
                new Vector3(0.05f * scale, 0.12f * scale, 0.05f * scale), UnitGreyboxMaterialPalette.HumanLeather);
        }

        static void AddHumanBow(GameObject body, Vector3 torsoCenter)
        {
            const float bowDepth = 0.42f;
            const float bowRadius = 0.36f;
            const int segmentCount = 7;
            for (var i = 0; i < segmentCount; i++)
            {
                var t = i / (float)(segmentCount - 1);
                var angleDeg = Mathf.Lerp(40f, 140f, t);
                var angle = angleDeg * Mathf.Deg2Rad;
                var localPosition = torsoCenter + new Vector3(Mathf.Cos(angle) * bowRadius, Mathf.Sin(angle) * bowRadius, bowDepth);
                AddPart(body, $"BowSeg{i + 1}", PrimitiveType.Cube, localPosition,
                    new Vector3(0.05f, 0.14f, 0.05f), Quaternion.Euler(0f, 0f, angleDeg + 90f),
                    UnitGreyboxMaterialPalette.HumanWood);
            }

            AddPart(body, "BowString", PrimitiveType.Cube,
                torsoCenter + new Vector3(0f, bowRadius * 0.55f, bowDepth - 0.12f),
                new Vector3(0.025f, bowRadius * 1.4f, 0.025f), UnitGreyboxMaterialPalette.HumanLeather);
        }

        static void AddHorseLegs(GameObject body)
        {
            var positions = new[]
            {
                new Vector3(-0.18f, 0.28f, 0.35f),
                new Vector3(0.18f, 0.28f, 0.35f),
                new Vector3(-0.18f, 0.28f, -0.35f),
                new Vector3(0.18f, 0.28f, -0.35f),
            };
            for (var i = 0; i < positions.Length; i++)
            {
                AddPart(body, $"HorseLeg{i + 1}", PrimitiveType.Capsule, positions[i],
                    new Vector3(0.12f, 0.28f, 0.12f), UnitGreyboxMaterialPalette.HumanLeather);
            }
        }

        static void AddTeamAccent(Transform parent, string accentName, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale)
        {
            var accent = AddPart(parent.gameObject, accentName, primitive, localPosition, localScale,
                UnitGreyboxMaterialPalette.TeamAccent, parent);
            accent.name = accentName;
        }

        static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child.gameObject;
        }

        static GameObject AddPart(
            GameObject root,
            string partName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform parent = null)
        {
            return AddPart(root, partName, primitive, localPosition, localScale, Quaternion.identity, material, parent);
        }

        static GameObject AddPart(
            GameObject root,
            string partName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material,
            Transform parent = null)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            var attachParent = parent != null ? parent : root.transform;
            part.transform.SetParent(attachParent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void UpdateCatalogFromPrefabs(GameObject[] humanPrefabs)
        {
            var catalog = LoadOrCreateCatalog();
            var so = new SerializedObject(catalog);

            AssignSet(so.FindProperty("_human"), humanPrefabs);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void AssignSet(SerializedProperty setProperty, GameObject[] prefabs)
        {
            setProperty.FindPropertyRelative("_melee").objectReferenceValue = prefabs[0];
            setProperty.FindPropertyRelative("_ranged").objectReferenceValue = prefabs[1];
            setProperty.FindPropertyRelative("_caster").objectReferenceValue = prefabs[2];
            setProperty.FindPropertyRelative("_siege").objectReferenceValue = prefabs[3];
            setProperty.FindPropertyRelative("_flying").objectReferenceValue = prefabs[4];
            setProperty.FindPropertyRelative("_super").objectReferenceValue = prefabs[5];
        }

        static UnitVisualCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<UnitVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
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

        struct HumanFrame
        {
            public Vector3 TorsoCenter;
            public Vector3 HeadCenter;
            public float Scale;
        }

        static class Stack
        {
            public static float Top(float centerY, float halfExtent) => centerY + halfExtent;
        }
    }
}
