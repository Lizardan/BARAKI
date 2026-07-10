using System.IO;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Procedural greybox unit prefabs (Human/Bug × 6 roles) and <see cref="UnitVisualCatalog"/> asset.</summary>
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
                {
                    var frame = BuildHumanFrame(body);
                    AddHumanMeleeWeapons(body, frame.Torso);
                    AddTeamAccent(frame.Torso, human: true, new Vector3(0f, 0.18f, 0.2f));
                    break;
                }

                case UnitRole.Ranged:
                {
                    var frame = BuildHumanFrame(body);
                    AddHumanBowArc(body, frame.Torso);
                    AddPart(body, "Quiver", PrimitiveType.Cube, new Vector3(0f, 0.06f, -0.22f),
                        new Vector3(0.18f, 0.42f, 0.12f), UnitGreyboxMaterialPalette.HumanWood, frame.Torso);
                    AddTeamAccent(frame.Torso, human: true, new Vector3(0f, 0.18f, 0.2f));
                    break;
                }

                case UnitRole.Caster:
                {
                    const float robeHalf = 0.62f;
                    var robe = AddPart(body, "Robe", PrimitiveType.Cylinder, new Vector3(0f, robeHalf, 0f),
                        new Vector3(0.72f, robeHalf, 0.72f), UnitGreyboxMaterialPalette.HumanCloth);
                    var robeTop = Stack.Top(robeHalf, robeHalf);
                    var head = AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, robeTop + 0.14f, 0f),
                        new Vector3(0.28f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.HumanSkin);
                    var staffBase = robeTop * 0.35f;
                    AddPart(body, "Staff", PrimitiveType.Cylinder, new Vector3(0.22f, staffBase + 0.55f, 0f),
                        new Vector3(0.06f, 0.55f, 0.06f), UnitGreyboxMaterialPalette.HumanWood, robe.transform);
                    AddPart(body, "Orb", PrimitiveType.Sphere, new Vector3(0.22f, staffBase + 1.12f, 0f),
                        new Vector3(0.16f, 0.16f, 0.16f), UnitGreyboxMaterialPalette.HumanArcane, robe.transform);
                    AddTeamAccent(head.transform, human: true, new Vector3(0f, 0f, 0.16f));
                    break;
                }

                case UnitRole.Siege:
                {
                    const float wheelRadius = 0.14f;
                    const float chassisHalf = 0.22f;
                    var chassisY = wheelRadius + chassisHalf;
                    var chassis = AddPart(body, "Chassis", PrimitiveType.Cube, new Vector3(0f, chassisY, 0f),
                        new Vector3(1.2f, chassisHalf * 2f, 0.9f), UnitGreyboxMaterialPalette.HumanWood);
                    var chassisTop = Stack.Top(chassisY, chassisHalf);
                    AddPart(body, "Arm", PrimitiveType.Cube, new Vector3(0f, chassisTop + 0.06f, 0.18f),
                        new Vector3(0.14f, 0.14f, 0.52f), Quaternion.Euler(-28f, 0f, 0f),
                        UnitGreyboxMaterialPalette.HumanWood, chassis.transform);
                    AddPart(body, "Bucket", PrimitiveType.Cube, new Vector3(0f, chassisTop + 0.18f, 0.42f),
                        new Vector3(0.3f, 0.22f, 0.3f), UnitGreyboxMaterialPalette.HumanSteel, chassis.transform);
                    AddWheel(body, new Vector3(0.46f, wheelRadius, 0.34f));
                    AddWheel(body, new Vector3(-0.46f, wheelRadius, 0.34f));
                    AddWheel(body, new Vector3(0.46f, wheelRadius, -0.34f));
                    AddWheel(body, new Vector3(-0.46f, wheelRadius, -0.34f));
                    AddTeamAccent(chassis.transform, human: true, new Vector3(0f, 0.12f, 0.48f));
                    break;
                }

                case UnitRole.Flying:
                {
                    const float packHalf = 0.18f;
                    var pack = AddPart(body, "Pack", PrimitiveType.Cube, new Vector3(0f, packHalf, -0.06f),
                        new Vector3(0.42f, packHalf * 2f, 0.28f), UnitGreyboxMaterialPalette.HumanSteel);
                    var packTop = Stack.Top(packHalf, packHalf);
                    var torso = AddPart(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, packTop + 0.34f, 0f),
                        new Vector3(0.44f, 0.34f, 0.3f), UnitGreyboxMaterialPalette.HumanSkin, pack.transform);
                    AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f),
                        new Vector3(0.24f, 0.24f, 0.24f), UnitGreyboxMaterialPalette.HumanSteel, torso.transform);
                    AddPart(body, "WingL", PrimitiveType.Cube, new Vector3(-0.34f, 0.04f, 0f),
                        new Vector3(0.42f, 0.04f, 0.24f), Quaternion.Euler(0f, 0f, 6f),
                        UnitGreyboxMaterialPalette.HumanSteel, torso.transform);
                    AddPart(body, "WingR", PrimitiveType.Cube, new Vector3(0.34f, 0.04f, 0f),
                        new Vector3(0.42f, 0.04f, 0.24f), Quaternion.Euler(0f, 0f, -6f),
                        UnitGreyboxMaterialPalette.HumanSteel, torso.transform);
                    AddTeamAccent(torso.transform, human: true, new Vector3(0f, 0.12f, 0.18f));
                    break;
                }

                case UnitRole.Super:
                {
                    const float bootHalf = 0.12f;
                    AddPart(body, "BootL", PrimitiveType.Cube, new Vector3(-0.18f, bootHalf, 0.04f),
                        new Vector3(0.18f, bootHalf * 2f, 0.24f), UnitGreyboxMaterialPalette.HumanSteel);
                    AddPart(body, "BootR", PrimitiveType.Cube, new Vector3(0.18f, bootHalf, 0.04f),
                        new Vector3(0.18f, bootHalf * 2f, 0.24f), UnitGreyboxMaterialPalette.HumanSteel);
                    var bootTop = Stack.Top(bootHalf, bootHalf);
                    var torso = AddPart(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, bootTop + 0.52f, 0f),
                        new Vector3(0.88f, 0.52f, 0.58f), UnitGreyboxMaterialPalette.HumanSteel);
                    AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, bootTop + 1.06f, 0f),
                        new Vector3(0.34f, 0.34f, 0.34f), UnitGreyboxMaterialPalette.HumanSkin, torso.transform);
                    AddPart(body, "PauldronL", PrimitiveType.Cube, new Vector3(-0.42f, 0.22f, 0f),
                        new Vector3(0.28f, 0.2f, 0.36f), UnitGreyboxMaterialPalette.HumanSteel, torso.transform);
                    AddPart(body, "PauldronR", PrimitiveType.Cube, new Vector3(0.42f, 0.22f, 0f),
                        new Vector3(0.28f, 0.2f, 0.36f), UnitGreyboxMaterialPalette.HumanSteel, torso.transform);
                    AddPart(body, "Greatsword", PrimitiveType.Cube, new Vector3(0.34f, -0.02f, 0.12f),
                        new Vector3(0.08f, 0.88f, 0.14f), Quaternion.Euler(0f, 0f, -10f),
                        UnitGreyboxMaterialPalette.HumanSteel, torso.transform);
                    AddTeamAccent(torso.transform, human: true, new Vector3(0f, 0.18f, 0.3f));
                    break;
                }
            }

            return root;
        }

        static HumanFrame BuildHumanFrame(GameObject body)
        {
            const float legHalf = 0.36f;
            AddPart(body, "LegL", PrimitiveType.Capsule, new Vector3(-0.12f, legHalf, 0f),
                new Vector3(0.2f, legHalf, 0.2f), UnitGreyboxMaterialPalette.HumanSkin);
            AddPart(body, "LegR", PrimitiveType.Capsule, new Vector3(0.12f, legHalf, 0f),
                new Vector3(0.2f, legHalf, 0.2f), UnitGreyboxMaterialPalette.HumanSkin);

            var legTop = Stack.Top(legHalf, legHalf);
            const float pelvisHalf = 0.06f;
            AddPart(body, "Pelvis", PrimitiveType.Cube, new Vector3(0f, legTop + pelvisHalf, 0f),
                new Vector3(0.3f, pelvisHalf * 2f, 0.22f), UnitGreyboxMaterialPalette.HumanSkin);

            var pelvisTop = legTop + pelvisHalf * 2f;
            const float torsoHalf = 0.36f;
            var torso = AddPart(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, pelvisTop + torsoHalf, 0f),
                new Vector3(0.44f, torsoHalf, 0.3f), UnitGreyboxMaterialPalette.HumanSkin);

            var torsoTop = pelvisTop + torsoHalf * 2f;
            const float headRadius = 0.14f;
            AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, torsoTop + headRadius, 0f),
                new Vector3(headRadius * 2f, headRadius * 2f, headRadius * 2f), UnitGreyboxMaterialPalette.HumanSkin,
                torso.transform);

            return new HumanFrame { Torso = torso.transform };
        }

        static GameObject BuildBug(UnitRole role, string name)
        {
            var root = new GameObject(name);
            var body = CreateChild(root.transform, "Body");

            switch (role)
            {
                case UnitRole.Melee:
                {
                    const float shellRadius = 0.34f;
                    var shell = AddPart(body, "Shell", PrimitiveType.Sphere, new Vector3(0f, shellRadius, 0f),
                        new Vector3(shellRadius * 2f, shellRadius * 2f, shellRadius * 2f),
                        UnitGreyboxMaterialPalette.BugChitin);
                    var head = AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, shellRadius + 0.02f, shellRadius + 0.08f),
                        new Vector3(0.36f, 0.32f, 0.32f), UnitGreyboxMaterialPalette.BugChitinDark, shell.transform);
                    AddPart(body, "MandibleL", PrimitiveType.Cube, new Vector3(-0.08f, -0.04f, 0.18f),
                        new Vector3(0.06f, 0.06f, 0.16f), Quaternion.Euler(12f, -8f, 0f),
                        UnitGreyboxMaterialPalette.BugChitinDark, head.transform);
                    AddPart(body, "MandibleR", PrimitiveType.Cube, new Vector3(0.08f, -0.04f, 0.18f),
                        new Vector3(0.06f, 0.06f, 0.16f), Quaternion.Euler(12f, 8f, 0f),
                        UnitGreyboxMaterialPalette.BugChitinDark, head.transform);
                    AddBugLegs(shell.transform, shellRadius, 6);
                    AddTeamAccent(shell.transform, human: false, new Vector3(0f, 0.08f, shellRadius + 0.02f));
                    break;
                }

                case UnitRole.Ranged:
                {
                    const float abdomenRadius = 0.22f;
                    var abdomen = AddPart(body, "Abdomen", PrimitiveType.Sphere, new Vector3(0f, abdomenRadius, -0.08f),
                        new Vector3(abdomenRadius * 2f, abdomenRadius * 1.6f, abdomenRadius * 2f),
                        UnitGreyboxMaterialPalette.BugChitin);
                    var thorax = AddPart(body, "Thorax", PrimitiveType.Capsule, new Vector3(0f, abdomenRadius + 0.22f, 0.06f),
                        new Vector3(0.28f, 0.22f, 0.28f), UnitGreyboxMaterialPalette.BugChitinDark, abdomen.transform);
                    var head = AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.24f, 0.18f),
                        new Vector3(0.28f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.BugChitin, thorax.transform);
                    AddPart(body, "Spitter", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0.2f),
                        new Vector3(0.1f, 0.1f, 0.28f), Quaternion.Euler(72f, 0f, 0f),
                        UnitGreyboxMaterialPalette.BugChitinDark, head.transform);
                    AddBugLegs(abdomen.transform, abdomenRadius + 0.02f, 4, yOffset: 0f);
                    AddTeamAccent(thorax.transform, human: false, new Vector3(0f, 0.08f, 0.16f));
                    break;
                }

                case UnitRole.Caster:
                {
                    const float bodyHalf = 0.42f;
                    var core = AddPart(body, "Body", PrimitiveType.Capsule, new Vector3(0f, bodyHalf, 0f),
                        new Vector3(0.24f, bodyHalf, 0.24f), UnitGreyboxMaterialPalette.BugChitinDark);
                    var bodyTop = Stack.Top(bodyHalf, bodyHalf);
                    var head = AddPart(body, "Head", PrimitiveType.Sphere, new Vector3(0f, bodyTop + 0.14f, 0f),
                        new Vector3(0.28f, 0.28f, 0.28f), UnitGreyboxMaterialPalette.BugChitin, core.transform);
                    AddPart(body, "AntennaL", PrimitiveType.Cylinder, new Vector3(-0.06f, 0.18f, 0.02f),
                        new Vector3(0.04f, 0.14f, 0.04f), Quaternion.Euler(0f, 0f, 16f),
                        UnitGreyboxMaterialPalette.BugChitinDark, head.transform);
                    AddPart(body, "AntennaR", PrimitiveType.Cylinder, new Vector3(0.06f, 0.18f, 0.02f),
                        new Vector3(0.04f, 0.14f, 0.04f), Quaternion.Euler(0f, 0f, -16f),
                        UnitGreyboxMaterialPalette.BugChitinDark, head.transform);
                    AddPart(body, "Glow", PrimitiveType.Sphere, new Vector3(0f, 0.24f, 0.04f),
                        new Vector3(0.12f, 0.12f, 0.12f), UnitGreyboxMaterialPalette.BugGlow, head.transform);
                    AddTeamAccent(head.transform, human: false, new Vector3(0f, 0f, 0.16f));
                    break;
                }

                case UnitRole.Siege:
                {
                    const float bodyRadius = 0.42f;
                    var core = AddPart(body, "Body", PrimitiveType.Sphere, new Vector3(0f, bodyRadius, 0f),
                        new Vector3(bodyRadius * 2f, bodyRadius * 1.7f, bodyRadius * 2f),
                        UnitGreyboxMaterialPalette.BugChitin);
                    AddPart(body, "Plate", PrimitiveType.Cube, new Vector3(0f, 0.12f, bodyRadius - 0.04f),
                        new Vector3(0.42f, 0.1f, 0.12f), UnitGreyboxMaterialPalette.BugChitinDark, core.transform);
                    AddBugLegs(core.transform, bodyRadius, 8, yOffset: -bodyRadius + 0.06f);
                    AddTeamAccent(core.transform, human: false, new Vector3(0f, 0.16f, bodyRadius - 0.02f));
                    break;
                }

                case UnitRole.Flying:
                {
                    const float bodyHalfZ = 0.28f;
                    const float bodyHalfY = 0.22f;
                    var core = AddPart(body, "Body", PrimitiveType.Sphere, new Vector3(0f, bodyHalfY, 0f),
                        new Vector3(0.48f, bodyHalfY * 2f, bodyHalfZ * 2f), UnitGreyboxMaterialPalette.BugChitin);
                    AddPart(body, "WingCoverL", PrimitiveType.Cube, new Vector3(-0.28f, 0.02f, 0f),
                        new Vector3(0.32f, 0.05f, 0.42f), Quaternion.Euler(0f, 0f, 10f),
                        UnitGreyboxMaterialPalette.BugChitinDark, core.transform);
                    AddPart(body, "WingCoverR", PrimitiveType.Cube, new Vector3(0.28f, 0.02f, 0f),
                        new Vector3(0.32f, 0.05f, 0.42f), Quaternion.Euler(0f, 0f, -10f),
                        UnitGreyboxMaterialPalette.BugChitinDark, core.transform);
                    AddPart(body, "WingL", PrimitiveType.Cube, new Vector3(-0.34f, 0.04f, -0.06f),
                        new Vector3(0.36f, 0.03f, 0.22f), Quaternion.Euler(0f, 0f, 4f),
                        UnitGreyboxMaterialPalette.BugGlow, core.transform);
                    AddPart(body, "WingR", PrimitiveType.Cube, new Vector3(0.34f, 0.04f, -0.06f),
                        new Vector3(0.36f, 0.03f, 0.22f), Quaternion.Euler(0f, 0f, -4f),
                        UnitGreyboxMaterialPalette.BugGlow, core.transform);
                    AddTeamAccent(core.transform, human: false, new Vector3(0f, 0.08f, bodyHalfZ - 0.02f));
                    break;
                }

                case UnitRole.Super:
                {
                    const float rearRadius = 0.38f;
                    var rear = AddPart(body, "SegmentRear", PrimitiveType.Sphere, new Vector3(0f, rearRadius, -0.1f),
                        new Vector3(rearRadius * 2f, rearRadius * 1.5f, rearRadius * 2f),
                        UnitGreyboxMaterialPalette.BugChitin);
                    var front = AddPart(body, "SegmentFront", PrimitiveType.Sphere, new Vector3(0f, 0.08f, rearRadius + 0.06f),
                        new Vector3(0.62f, 0.52f, 0.62f), UnitGreyboxMaterialPalette.BugChitinDark, rear.transform);
                    AddPart(body, "HornL", PrimitiveType.Cube, new Vector3(-0.1f, 0.08f, 0.24f),
                        new Vector3(0.06f, 0.06f, 0.22f), Quaternion.Euler(18f, -8f, 0f),
                        UnitGreyboxMaterialPalette.BugChitinDark, front.transform);
                    AddPart(body, "HornR", PrimitiveType.Cube, new Vector3(0.1f, 0.08f, 0.24f),
                        new Vector3(0.06f, 0.06f, 0.22f), Quaternion.Euler(18f, 8f, 0f),
                        UnitGreyboxMaterialPalette.BugChitinDark, front.transform);
                    AddBugLegs(rear.transform, rearRadius, 6, yOffset: -rearRadius + 0.08f);
                    AddTeamAccent(front.transform, human: false, new Vector3(0f, 0.12f, 0.22f));
                    break;
                }
            }

            return root;
        }

        static void AddHumanMeleeWeapons(GameObject body, Transform torso)
        {
            AddPart(body, "Shield", PrimitiveType.Cube, new Vector3(-0.36f, 0.04f, 0.48f),
                new Vector3(0.08f, 0.56f, 0.44f), UnitGreyboxMaterialPalette.HumanSteel, torso);
            AddPart(body, "SwordGuard", PrimitiveType.Cube, new Vector3(0.3f, 0.02f, 0.36f),
                new Vector3(0.18f, 0.08f, 0.08f), UnitGreyboxMaterialPalette.HumanSteel, torso);
            AddPart(body, "SwordBlade", PrimitiveType.Cube, new Vector3(0.3f, 0.02f, 0.78f),
                new Vector3(0.1f, 0.12f, 0.72f), UnitGreyboxMaterialPalette.HumanSteel, torso);
        }

        static void AddHumanBowArc(GameObject body, Transform torso)
        {
            const float bowDepth = 0.54f;
            const float bowCenterY = 0.08f;
            const float bowRadius = 0.34f;
            const int segmentCount = 9;
            const float arcStartDeg = 38f;
            const float arcEndDeg = 142f;

            for (var i = 0; i < segmentCount; i++)
            {
                var t = segmentCount == 1 ? 0.5f : i / (float)(segmentCount - 1);
                var angleDeg = Mathf.Lerp(arcStartDeg, arcEndDeg, t);
                var angle = angleDeg * Mathf.Deg2Rad;
                var localPosition = new Vector3(
                    Mathf.Cos(angle) * bowRadius,
                    Mathf.Sin(angle) * bowRadius + bowCenterY,
                    bowDepth);
                var tangentDeg = angleDeg + 90f;
                AddPart(body, $"BowSeg{i + 1}", PrimitiveType.Cube, localPosition,
                    new Vector3(0.06f, 0.14f, 0.06f), Quaternion.Euler(0f, 0f, tangentDeg),
                    UnitGreyboxMaterialPalette.HumanWood, torso);
            }

            AddPart(body, "BowGrip", PrimitiveType.Cube, new Vector3(0f, bowCenterY - bowRadius + 0.04f, bowDepth),
                new Vector3(0.08f, 0.14f, 0.08f), UnitGreyboxMaterialPalette.HumanWood, torso);
            AddPart(body, "BowString", PrimitiveType.Cube, new Vector3(0f, bowCenterY, bowDepth - 0.14f),
                new Vector3(0.03f, bowRadius * 1.55f, 0.03f), UnitGreyboxMaterialPalette.HumanSteel, torso);
        }

        static void AddBugLegs(Transform attachBody, float attachRadius, int count, float yOffset = 0f)
        {
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var localPos = dir * (attachRadius + 0.08f) + new Vector3(0f, yOffset, 0f);
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                AddPart(attachBody.gameObject, $"Leg{i + 1}", PrimitiveType.Cube, localPos,
                    new Vector3(0.08f, 0.08f, 0.22f), rot, UnitGreyboxMaterialPalette.BugChitinDark, attachBody);
            }
        }

        static void AddWheel(GameObject body, Vector3 position)
        {
            AddPart(body, "Wheel", PrimitiveType.Cylinder, position,
                new Vector3(0.28f, 0.08f, 0.28f), UnitGreyboxMaterialPalette.HumanSteel);
        }

        static void AddTeamAccent(Transform parent, bool human, Vector3 localPosition)
        {
            var accent = AddPart(parent.gameObject, UnitVisualAccent.TeamAccentTransformName, PrimitiveType.Cube,
                localPosition, new Vector3(0.18f, human ? 0.16f : 0.14f, 0.04f),
                UnitGreyboxMaterialPalette.TeamAccent, parent);
            accent.name = UnitVisualAccent.TeamAccentTransformName;
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
            public Transform Torso;
        }

        static class Stack
        {
            public static float Top(float centerY, float halfExtent) => centerY + halfExtent;
        }
    }
}
