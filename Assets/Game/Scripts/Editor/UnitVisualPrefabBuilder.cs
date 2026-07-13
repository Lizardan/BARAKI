using System.IO;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Stylized low-poly unit prefabs (Human/Bug × 6 roles) and <see cref="UnitVisualCatalog"/>.</summary>
    public static class UnitVisualPrefabBuilder
    {
        public const string RootPath = "Assets/Game/Prefabs/Units";
        public const string HumanPath = RootPath + "/Human";
        public const string BugPath = RootPath + "/Bug";
        public const string CatalogPath = "Assets/Game/ScriptableObjects/UnitVisualCatalog.asset";

        public static void EnsureContent()
        {
            UnitGreyboxMaterialPalette.EnsureMaterials();
            EnsureFolder(RootPath);
            EnsureFolder(HumanPath);
            EnsureFolder(BugPath);
            EnsureFolder("Assets/Game/ScriptableObjects");

            var humanPrefabs = CreateRacePrefabs(HumanPath, "Human", isHuman: true);
            var bugPrefabs = CreateRacePrefabs(BugPath, "Bug", isHuman: false);
            UpdateCatalogFromPrefabs(humanPrefabs, bugPrefabs);
            UnitPortraitBaker.BakeIntoCatalog(AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(CatalogPath));
            AssetDatabase.SaveAssets();
        }

        static GameObject[] CreateRacePrefabs(string folder, string prefix, bool isHuman)
        {
            var roles = new[]
            {
                UnitRole.Melee,
                UnitRole.Ranged,
                UnitRole.Caster,
                UnitRole.Siege,
                UnitRole.Flying,
                UnitRole.Super,
            };

            var prefabs = new GameObject[roles.Length];
            for (var i = 0; i < roles.Length; i++)
            {
                var role = roles[i];
                var name = $"{prefix}_{role}";
                var path = $"{folder}/{name}.prefab";
                var root = isHuman ? BuildHuman(role, name) : BuildBug(role, name);
                prefabs[i] = SavePrefab(root, path);
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

        static GameObject BuildBug(UnitRole role, string name)
        {
            var root = new GameObject(name);
            var body = CreateChild(root.transform, "Body");

            switch (role)
            {
                case UnitRole.Melee:
                    BuildBugMelee(body);
                    break;
                case UnitRole.Ranged:
                    BuildBugRanged(body);
                    break;
                case UnitRole.Caster:
                    BuildBugCaster(body);
                    break;
                case UnitRole.Siege:
                    BuildBugScorpion(body);
                    break;
                case UnitRole.Flying:
                    BuildBugWasp(body);
                    break;
                case UnitRole.Super:
                    BuildBugSpider(body);
                    break;
            }

            return root;
        }

        static void BuildBugMelee(GameObject body)
        {
            AddPart(body, "Pelvis", PrimitiveType.Sphere, new Vector3(0f, 0.35f, 0f),
                new Vector3(0.45f, 0.35f, 0.4f), UnitGreyboxMaterialPalette.BugUnderbelly);
            AddTeamAccent(body.transform, "TeamAccent_Carapace", PrimitiveType.Sphere,
                new Vector3(0f, 0.47f, -0.05f), new Vector3(0.55f, 0.4f, 0.5f));
            AddPart(body, "Thorax", PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0.05f),
                new Vector3(0.4f, 0.32f, 0.35f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddTeamAccent(body.transform, "TeamAccent_Chest", PrimitiveType.Cube,
                new Vector3(0f, 0.6f, 0.23f), new Vector3(0.35f, 0.35f, 0.08f));
            AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0.2f),
                new Vector3(0.38f, 0.34f, 0.38f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddPart(body, "HornL", PrimitiveType.Cube, new Vector3(-0.1f, 0.97f, 0.3f),
                new Vector3(0.08f, 0.28f, 0.08f), Quaternion.Euler(-25f, -10f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "HornR", PrimitiveType.Cube, new Vector3(0.1f, 0.97f, 0.3f),
                new Vector3(0.08f, 0.28f, 0.08f), Quaternion.Euler(-25f, 10f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "EyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.89f, 0.36f),
                new Vector3(0.1f, 0.1f, 0.1f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "EyeR", PrimitiveType.Sphere, new Vector3(0.1f, 0.89f, 0.36f),
                new Vector3(0.1f, 0.1f, 0.1f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "ClawL", PrimitiveType.Cube, new Vector3(-0.38f, 0.5f, 0.25f),
                new Vector3(0.12f, 0.12f, 0.35f), Quaternion.Euler(15f, -20f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "ClawR", PrimitiveType.Cube, new Vector3(0.38f, 0.5f, 0.25f),
                new Vector3(0.12f, 0.12f, 0.35f), Quaternion.Euler(15f, 20f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddBipedBugLegs(body);
        }

        static void BuildBugRanged(GameObject body)
        {
            // Upright spit-beetle (compact biped) — not a horizontal worm.
            AddPart(body, "Pelvis", PrimitiveType.Sphere, new Vector3(0f, 0.32f, -0.05f),
                new Vector3(0.4f, 0.32f, 0.36f), UnitGreyboxMaterialPalette.BugUnderbelly);
            AddTeamAccent(body.transform, "TeamAccent_Carapace", PrimitiveType.Sphere,
                new Vector3(0f, 0.42f, -0.08f), new Vector3(0.48f, 0.28f, 0.42f));

            AddPart(body, "Thorax", PrimitiveType.Capsule, new Vector3(0f, 0.58f, 0.02f),
                new Vector3(0.36f, 0.26f, 0.3f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddTeamAccent(body.transform, "TeamAccent_Chest", PrimitiveType.Cube,
                new Vector3(0f, 0.62f, 0.18f), new Vector3(0.28f, 0.26f, 0.06f));

            // Glow spit sac on the back (reads as ranged ammo, not a tail segment)
            AddPart(body, "SpitSac", PrimitiveType.Sphere, new Vector3(0f, 0.62f, -0.28f),
                new Vector3(0.28f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.BugGlow);

            AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.88f, 0.12f),
                new Vector3(0.3f, 0.28f, 0.3f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddPart(body, "EyeL", PrimitiveType.Sphere, new Vector3(-0.09f, 0.92f, 0.24f),
                new Vector3(0.09f, 0.09f, 0.09f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "EyeR", PrimitiveType.Sphere, new Vector3(0.09f, 0.92f, 0.24f),
                new Vector3(0.09f, 0.09f, 0.09f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "AntennaL", PrimitiveType.Cube, new Vector3(-0.08f, 1.08f, 0.12f),
                new Vector3(0.04f, 0.2f, 0.04f), Quaternion.Euler(10f, -15f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "AntennaR", PrimitiveType.Cube, new Vector3(0.08f, 1.08f, 0.12f),
                new Vector3(0.04f, 0.2f, 0.04f), Quaternion.Euler(10f, 15f, 0f),
                UnitGreyboxMaterialPalette.BugBone);

            // Short forward spit nozzle from the mouth
            AddPart(body, "SpitBarrel", PrimitiveType.Cylinder, new Vector3(0f, 0.82f, 0.38f),
                new Vector3(0.08f, 0.14f, 0.08f), Quaternion.Euler(90f, 0f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "SpitGlow", PrimitiveType.Sphere, new Vector3(0f, 0.82f, 0.55f),
                new Vector3(0.14f, 0.14f, 0.16f), UnitGreyboxMaterialPalette.BugGlow);

            AddPart(body, "LegL", PrimitiveType.Capsule, new Vector3(-0.12f, 0.16f, 0f),
                new Vector3(0.1f, 0.16f, 0.1f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "LegR", PrimitiveType.Capsule, new Vector3(0.12f, 0.16f, 0f),
                new Vector3(0.1f, 0.16f, 0.1f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "FootL", PrimitiveType.Cube, new Vector3(-0.12f, 0.04f, 0.06f),
                new Vector3(0.12f, 0.05f, 0.16f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "FootR", PrimitiveType.Cube, new Vector3(0.12f, 0.04f, 0.06f),
                new Vector3(0.12f, 0.05f, 0.16f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "ArmL", PrimitiveType.Cube, new Vector3(-0.26f, 0.55f, 0.12f),
                new Vector3(0.08f, 0.08f, 0.24f), Quaternion.Euler(8f, -12f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "ArmR", PrimitiveType.Cube, new Vector3(0.26f, 0.55f, 0.12f),
                new Vector3(0.08f, 0.08f, 0.24f), Quaternion.Euler(8f, 12f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
        }

        static void BuildBugCaster(GameObject body)
        {
            AddPart(body, "Core", PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0f),
                new Vector3(0.45f, 0.5f, 0.45f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddTeamAccent(body.transform, "TeamAccent_Shell", PrimitiveType.Sphere,
                new Vector3(0f, 0.8f, 0f), new Vector3(0.48f, 0.45f, 0.48f));
            AddPart(body, "SpikeL", PrimitiveType.Cube, new Vector3(-0.22f, 1.0f, 0f),
                new Vector3(0.1f, 0.35f, 0.1f), Quaternion.Euler(0f, 0f, 25f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "SpikeR", PrimitiveType.Cube, new Vector3(0.22f, 1.0f, 0f),
                new Vector3(0.1f, 0.35f, 0.1f), Quaternion.Euler(0f, 0f, -25f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "EnergyOrb", PrimitiveType.Sphere, new Vector3(0.35f, 0.7f, 0.2f),
                new Vector3(0.22f, 0.22f, 0.22f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "Eye", PrimitiveType.Sphere, new Vector3(0f, 0.83f, 0.22f),
                new Vector3(0.14f, 0.14f, 0.14f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "HoverRing", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f),
                new Vector3(0.55f, 0.03f, 0.55f), UnitGreyboxMaterialPalette.BugGlow);
        }

        static void BuildBugScorpion(GameObject body)
        {
            AddPart(body, "Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0.4f, -0.15f),
                new Vector3(0.9f, 0.55f, 1.0f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddTeamAccent(body.transform, "TeamAccent_Armor", PrimitiveType.Cube,
                new Vector3(0f, 0.6f, -0.15f), new Vector3(0.75f, 0.2f, 0.85f));
            AddPart(body, "Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0.45f),
                new Vector3(0.55f, 0.4f, 0.55f), UnitGreyboxMaterialPalette.BugUnderbelly);
            AddPart(body, "PincerL", PrimitiveType.Cube, new Vector3(-0.4f, 0.55f, 0.8f),
                new Vector3(0.18f, 0.14f, 0.45f), Quaternion.Euler(0f, -25f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "PincerR", PrimitiveType.Cube, new Vector3(0.4f, 0.55f, 0.8f),
                new Vector3(0.18f, 0.14f, 0.45f), Quaternion.Euler(0f, 25f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "Tail1", PrimitiveType.Capsule, new Vector3(0f, 0.7f, -0.55f),
                new Vector3(0.2f, 0.28f, 0.2f), Quaternion.Euler(-40f, 0f, 0f),
                UnitGreyboxMaterialPalette.BugChitin);
            AddTeamAccent(body.transform, "TeamAccent_Tail", PrimitiveType.Sphere,
                new Vector3(0f, 0.85f, -0.55f), new Vector3(0.18f, 0.2f, 0.18f));
            AddPart(body, "Tail2", PrimitiveType.Capsule, new Vector3(0f, 1.05f, -0.35f),
                new Vector3(0.16f, 0.22f, 0.16f), Quaternion.Euler(-10f, 0f, 0f),
                UnitGreyboxMaterialPalette.BugChitinDark);
            AddPart(body, "Stinger", PrimitiveType.Sphere, new Vector3(0f, 1.3f, -0.2f),
                new Vector3(0.18f, 0.18f, 0.28f), UnitGreyboxMaterialPalette.BugGlow);
            AddBugLegs(body, new Vector3(0f, 0.4f, -0.15f), 0.45f, 8, yOffset: -0.25f);
        }

        static void BuildBugWasp(GameObject body)
        {
            AddPart(body, "Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0f),
                new Vector3(0.4f, 0.35f, 0.45f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddTeamAccent(body.transform, "TeamAccent_Thorax", PrimitiveType.Sphere,
                new Vector3(0f, 0.75f, 0f), new Vector3(0.42f, 0.3f, 0.47f));
            AddPart(body, "Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0.55f, -0.4f),
                new Vector3(0.35f, 0.3f, 0.5f), UnitGreyboxMaterialPalette.BugUnderbelly);
            AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0.3f),
                new Vector3(0.28f, 0.26f, 0.28f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddPart(body, "EyeL", PrimitiveType.Sphere, new Vector3(-0.08f, 0.78f, 0.4f),
                new Vector3(0.1f, 0.1f, 0.1f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "EyeR", PrimitiveType.Sphere, new Vector3(0.08f, 0.78f, 0.4f),
                new Vector3(0.1f, 0.1f, 0.1f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "WingFL", PrimitiveType.Cube, new Vector3(-0.35f, 0.85f, 0.05f),
                new Vector3(0.55f, 0.02f, 0.28f), Quaternion.Euler(10f, 15f, 25f),
                UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "WingFR", PrimitiveType.Cube, new Vector3(0.35f, 0.85f, 0.05f),
                new Vector3(0.55f, 0.02f, 0.28f), Quaternion.Euler(10f, -15f, -25f),
                UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "WingBL", PrimitiveType.Cube, new Vector3(-0.3f, 0.8f, -0.1f),
                new Vector3(0.45f, 0.02f, 0.22f), Quaternion.Euler(-5f, 20f, 30f),
                UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "WingBR", PrimitiveType.Cube, new Vector3(0.3f, 0.8f, -0.1f),
                new Vector3(0.45f, 0.02f, 0.22f), Quaternion.Euler(-5f, -20f, -30f),
                UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "LegL", PrimitiveType.Cube, new Vector3(-0.15f, 0.35f, 0.1f),
                new Vector3(0.06f, 0.35f, 0.06f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "LegR", PrimitiveType.Cube, new Vector3(0.15f, 0.35f, 0.1f),
                new Vector3(0.06f, 0.35f, 0.06f), UnitGreyboxMaterialPalette.BugBone);
        }

        static void BuildBugSpider(GameObject body)
        {
            AddPart(body, "Body", PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f),
                new Vector3(1.35f, 0.7f, 1.2f), UnitGreyboxMaterialPalette.BugChitinDark);
            AddTeamAccent(body.transform, "TeamAccent_Armor", PrimitiveType.Sphere,
                new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 0.52f, 1.25f));
            AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0.65f),
                new Vector3(0.55f, 0.4f, 0.5f), UnitGreyboxMaterialPalette.BugUnderbelly);
            AddPart(body, "FangL", PrimitiveType.Cube, new Vector3(-0.12f, 0.35f, 0.85f),
                new Vector3(0.1f, 0.1f, 0.25f), Quaternion.Euler(20f, -10f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "FangR", PrimitiveType.Cube, new Vector3(0.12f, 0.35f, 0.85f),
                new Vector3(0.1f, 0.1f, 0.25f), Quaternion.Euler(20f, 10f, 0f),
                UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "SacL", PrimitiveType.Sphere, new Vector3(-0.55f, 0.55f, 0f),
                new Vector3(0.28f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.BugGlow);
            AddPart(body, "SacR", PrimitiveType.Sphere, new Vector3(0.55f, 0.55f, 0f),
                new Vector3(0.28f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.BugGlow);
            AddBugLegs(body, new Vector3(0f, 0.55f, 0f), 0.65f, 8, yOffset: -0.35f, legLength: 0.55f);
        }

        static void AddBipedBugLegs(GameObject body)
        {
            AddPart(body, "LegL", PrimitiveType.Capsule, new Vector3(-0.15f, 0.2f, 0f),
                new Vector3(0.14f, 0.22f, 0.14f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "LegR", PrimitiveType.Capsule, new Vector3(0.15f, 0.2f, 0f),
                new Vector3(0.14f, 0.22f, 0.14f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "FootL", PrimitiveType.Cube, new Vector3(-0.15f, 0.05f, 0.06f),
                new Vector3(0.14f, 0.06f, 0.2f), UnitGreyboxMaterialPalette.BugBone);
            AddPart(body, "FootR", PrimitiveType.Cube, new Vector3(0.15f, 0.05f, 0.06f),
                new Vector3(0.14f, 0.06f, 0.2f), UnitGreyboxMaterialPalette.BugBone);
        }

        static void AddBugLegs(GameObject body, Vector3 center, float attachRadius, int count, float yOffset = 0f, float legLength = 0.28f)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count + 0.2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var localPos = center + dir * (attachRadius + 0.05f) + new Vector3(0f, yOffset, 0f);
                var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(25f, 0f, 0f);
                AddPart(body, $"Leg{i + 1}", PrimitiveType.Cube, localPos,
                    new Vector3(0.08f, 0.08f, legLength), rot, UnitGreyboxMaterialPalette.BugBone);
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
                Object.DestroyImmediate(collider);
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
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void UpdateCatalogFromPrefabs(GameObject[] humanPrefabs, GameObject[] bugPrefabs)
        {
            var catalog = LoadOrCreateCatalog();
            var so = new SerializedObject(catalog);

            AssignSet(so.FindProperty("_human"), humanPrefabs);
            AssignSet(so.FindProperty("_bug"), bugPrefabs);
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
