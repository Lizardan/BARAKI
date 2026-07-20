using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Data;
using Game.Gameplay.Match;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
namespace Game.Editor
{
    /// <summary>
    /// Builds Human animated unit materials, AnimatorControllers and Units/Human prefabs
    /// from <c>Art/Models/Human/*_Animated.fbx</c> (converted WC3 MDX).
    /// </summary>
    public static class HumanAnimatedUnitSetup
    {
        public const string ModelRoot = "Assets/Game/Art/Models/Human";
        public const string TextureFolder = ModelRoot + "/Textures";
        public const string MaterialFolder = ModelRoot + "/Materials";
        public const string AnimatorFolder = ModelRoot + "/Animators";
        public const string GeosetMapPath = ModelRoot + "/geoset_textures.json";
        public const string TeamAccentMaterialPath =
            "Assets/Game/Art/Materials/Units/UnitTeamAccent.mat";
        public const string TeamColorDiffuseShaderPath =
            "Assets/Game/Art/Materials/Units/TeamColorDiffuse.shader";
        public const string CasterCloakMaterialPath =
            MaterialFolder + "/Mat_Arthas_TeamCloak.mat";

        static readonly string[] UnitNames =
        {
            "Human_Melee",
            "Human_Ranged",
            "Human_Caster",
            "Human_Siege",
            "Human_Flying",
            "Human_Super",
        };

        [MenuItem("BARAKI/Units/Rebuild Human Animated Prefabs")]
        public static void RebuildFromMenu()
        {
            RebuildAll();
        }

        public static void RebuildAll()
        {
            EnsureFolders();
            CreateMaterialsFromTextures();
            var geosetMap = LoadGeosetMap();

            foreach (var unit in UnitNames)
            {
                ConfigureFbxImporter(unit);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var unit in UnitNames)
            {
                var controller = CreateAnimatorController(unit);
                BuildPrefab(unit, controller, geosetMap);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("HumanAnimatedUnitSetup: rebuilt " + UnitNames.Length + " human unit prefabs.");
        }

        static void EnsureFolders()
        {
            EnsureFolder(ModelRoot);
            EnsureFolder(TextureFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(AnimatorFolder);
            EnsureFolder(UnitVisualPrefabBuilder.HumanPath);
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

        static void CreateMaterialsFromTextures()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader not found.");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
            {
                var texPath = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                {
                    continue;
                }

                var matPath = MaterialFolder + "/Mat_" + tex.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader) { name = "Mat_" + tex.name };
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                mat.shader = shader;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(mat);
            }

            EnsureCasterCloakMaterial();
        }

        /// <summary>
        /// Arthas atlas paints the mage cloak gold; replace that pigment with slot team color.
        /// </summary>
        static Material EnsureCasterCloakMaterial()
        {
            var teamShader = AssetDatabase.LoadAssetAtPath<Shader>(TeamColorDiffuseShaderPath)
                ?? Shader.Find("Game/Units/TeamColorDiffuse");
            if (teamShader == null)
            {
                throw new InvalidOperationException("Missing " + TeamColorDiffuseShaderPath);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/Arthas.png");
            if (tex == null)
            {
                throw new InvalidOperationException("Missing Arthas.png for caster cloak.");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(CasterCloakMaterialPath);
            if (mat == null)
            {
                mat = new Material(teamShader) { name = "Mat_Arthas_TeamCloak" };
                AssetDatabase.CreateAsset(mat, CasterCloakMaterialPath);
            }

            mat.shader = teamShader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ConfigureFbxImporter(string unit)
        {
            var fbxPath = ModelRoot + "/" + unit + "_Animated.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Missing FBX: " + fbxPath);
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            var defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0)
            {
                defaults = importer.clipAnimations;
            }

            var configured = new List<ModelImporterClipAnimation>();
            foreach (var src in defaults)
            {
                var clip = src;
                var name = CleanClipName(clip.name);
                if (string.Equals(name, "all sequences", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                clip.name = name;
                var loop = IsLoopClip(name);
                clip.loopTime = loop;
                clip.loop = loop;
                configured.Add(clip);
            }

            importer.clipAnimations = configured.ToArray();
            importer.SaveAndReimport();
        }

        static string CleanClipName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            // Blender may prefix with "Armature|"
            var pipe = raw.LastIndexOf('|');
            return pipe >= 0 ? raw.Substring(pipe + 1).Trim() : raw.Trim();
        }

        static bool IsLoopClip(string name)
        {
            var n = name.ToLowerInvariant();
            if (n.Contains("attack") || n.Contains("death") || n.Contains("dissipate")
                || n.Contains("decay") || n.Contains("spell"))
            {
                return false;
            }

            return n.Contains("stand") || n.Contains("walk") || n.Contains("sleep")
                || n.Contains("portrait");
        }

        static AnimatorController CreateAnimatorController(string unit)
        {
            var fbxPath = ModelRoot + "/" + unit + "_Animated.fbx";
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__", StringComparison.Ordinal))
                .ToArray();

            var stand = PickClip(clips, preferExact: new[] { "Stand", "stand" },
                preferContains: new[] { "Stand 2", "Stand -1", "Stand - 2", "Stand Portrait", "stand" },
                exclude: new[] { "ready", "victory", "defend", "walk", "attack", "death" });
            var walk = PickClip(clips, preferExact: new[] { "Walk", "walk" },
                preferContains: new[] { "Walk", "walk" },
                exclude: new[] { "defend", "attack", "death", "stand" });
            var attack = PickClip(clips, preferExact: new[] { "Attack", "attack" },
                preferContains: new[] { "Attack -1", "Attack - 1", "Attack 2", "Attack" },
                exclude: new[] { "defend", "slam", "spin", "walk" });
            var death = PickClip(clips, preferExact: new[] { "Death", "death" },
                preferContains: new[] { "Death", "death" },
                exclude: Array.Empty<string>());

            if (stand == null || walk == null || attack == null || death == null)
            {
                var names = string.Join(", ", clips.Select(c => c.name));
                throw new InvalidOperationException(
                    unit + " missing combat clips. stand=" + (stand != null) +
                    " walk=" + (walk != null) + " attack=" + (attack != null) +
                    " death=" + (death != null) + " available=[" + names + "]");
            }

            if (unit == "Human_Flying")
            {
                // WC3 airship Stand/Walk use a different root pose than Attack — that yaws the
                // ship ~20–30° and drops it under the road when Attack returns to Stand.
                stand = CreateFlyingStableClip(stand, attack, "Stand");
                walk = CreateFlyingStableClip(walk, attack, "Walk");
            }

            var controllerPath = AnimatorFolder + "/" + unit + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter(UnitCombatAnimatorDriver.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(UnitCombatAnimatorDriver.AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(UnitCombatAnimatorDriver.DeathParam, AnimatorControllerParameterType.Trigger);

            var root = controller.layers[0].stateMachine;
            var standState = root.AddState("Stand");
            standState.motion = stand;
            var walkState = root.AddState("Walk");
            walkState.motion = walk;
            var attackState = root.AddState("Attack");
            attackState.motion = attack;
            var deathState = root.AddState("Death");
            deathState.motion = death;
            root.defaultState = standState;

            var toWalk = standState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, UnitCombatAnimatorDriver.SpeedParam);

            var toStand = walkState.AddTransition(standState);
            toStand.hasExitTime = false;
            toStand.duration = 0.15f;
            toStand.AddCondition(AnimatorConditionMode.Less, 0.1f, UnitCombatAnimatorDriver.SpeedParam);

            var anyAttack = root.AddAnyStateTransition(attackState);
            anyAttack.hasExitTime = false;
            anyAttack.duration = 0.05f;
            anyAttack.canTransitionToSelf = false;
            anyAttack.AddCondition(AnimatorConditionMode.If, 0f, UnitCombatAnimatorDriver.AttackParam);

            var attackToStand = attackState.AddTransition(standState);
            attackToStand.hasExitTime = true;
            attackToStand.exitTime = 0.9f;
            attackToStand.duration = 0.1f;

            var anyDeath = root.AddAnyStateTransition(deathState);
            anyDeath.hasExitTime = false;
            anyDeath.duration = 0.05f;
            anyDeath.canTransitionToSelf = false;
            anyDeath.AddCondition(AnimatorConditionMode.If, 0f, UnitCombatAnimatorDriver.DeathParam);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Locks the airship body chain TR to the Attack pose so Walk/Stand keep facing and altitude.
        /// </summary>
        static AnimationClip CreateFlyingStableClip(
            AnimationClip source,
            AnimationClip reference,
            string label)
        {
            const string BodyChainRoot = "airplan Nodes/bone_new0.005";
            EnsureFolder(AnimatorFolder);
            var outPath = AnimatorFolder + "/Human_Flying_" + label + "_Stable.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath) != null)
            {
                AssetDatabase.DeleteAsset(outPath);
            }

            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = "Human_Flying_" + label + "_Stable";
            AssetDatabase.CreateAsset(copy, outPath);
            copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);

            var refBindings = AnimationUtility.GetCurveBindings(reference)
                .ToDictionary(b => b.path + "|" + b.propertyName, b => b);

            foreach (var bind in AnimationUtility.GetCurveBindings(copy))
            {
                if (!IsFlyingBodyChainPath(bind.path, BodyChainRoot))
                {
                    continue;
                }

                if (!IsTransformBinding(bind.propertyName))
                {
                    continue;
                }

                if (!refBindings.TryGetValue(bind.path + "|" + bind.propertyName, out var refBind))
                {
                    continue;
                }

                var refCurve = AnimationUtility.GetEditorCurve(reference, refBind);
                if (refCurve == null || refCurve.keys.Length == 0)
                {
                    continue;
                }

                var value = refCurve.Evaluate(0f);
                var flat = AnimationCurve.Constant(0f, Mathf.Max(copy.length, 0.01f), value);
                AnimationUtility.SetEditorCurve(copy, bind, flat);
            }

            EditorUtility.SetDirty(copy);
            return copy;
        }

        static bool IsFlyingBodyChainPath(string path, string chainRoot) =>
            path == chainRoot
            || path.StartsWith(chainRoot + "/", StringComparison.Ordinal);

        static bool IsTransformBinding(string propertyName) =>
            propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)
            || propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)
            || propertyName.StartsWith("localEulerAnglesRaw.", StringComparison.Ordinal);

        static AnimationClip PickClip(
            AnimationClip[] clips,
            string[] preferExact,
            string[] preferContains,
            string[] exclude)
        {
            bool Excluded(string name)
            {
                var lower = name.ToLowerInvariant();
                foreach (var ex in exclude)
                {
                    if (lower.Contains(ex.ToLowerInvariant()))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var exact in preferExact)
            {
                foreach (var clip in clips)
                {
                    if (string.Equals(clip.name, exact, StringComparison.OrdinalIgnoreCase)
                        && !Excluded(clip.name))
                    {
                        return clip;
                    }
                }
            }

            foreach (var token in preferContains)
            {
                foreach (var clip in clips)
                {
                    if (clip.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                        && !Excluded(clip.name))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        static void BuildPrefab(
            string unit,
            AnimatorController controller,
            Dictionary<string, List<GeosetInfo>> geosetMap)
        {
            var fbxPath = ModelRoot + "/" + unit + "_Animated.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                throw new InvalidOperationException("Missing model " + fbxPath);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            instance.name = unit;
            instance.transform.localPosition = Vector3.zero;
            var role = unit switch
            {
                "Human_Melee" => UnitRole.Melee,
                "Human_Ranged" => UnitRole.Ranged,
                "Human_Caster" => UnitRole.Caster,
                "Human_Siege" => UnitRole.Siege,
                "Human_Flying" => UnitRole.Flying,
                "Human_Super" => UnitRole.Super,
                _ => UnitRole.Melee,
            };
            instance.transform.localRotation = Quaternion.Euler(
                UnitGreyboxVisuals.GetAnimatedHumanModelEuler(role));
            instance.transform.localScale = Vector3.one;

            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);

            // Hide death/gutz geosets before material/accent assignment so fallback
            // never promotes hidden meshes into visible TeamAccent parts.
            DisableHiddenGeosets(instance, unit, geosetMap);
            ApplyMaterialsAndAccents(instance, unit, geosetMap);
            NormalizePrefabScale(instance, unit);
            if (unit == "Human_Flying")
            {
                // Measure after Attack pose — bind/Stand poses hang below the pivot.
                var attackClip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(c => string.Equals(c.name, "Attack", StringComparison.OrdinalIgnoreCase));
                if (attackClip != null)
                {
                    attackClip.SampleAnimation(instance, 0f);
                }

                LiftPrefabAboveGround(instance, clearance: 0.35f);
            }

            var prefabPath = UnitVisualPrefabBuilder.HumanPath + "/" + unit + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
        }

        /// <summary>
        /// WC3 flying meshes hang below the pivot; shift children so the rest pose sits above y=0.
        /// Runtime hover is applied separately via <see cref="UnitGreyboxVisuals.FlyingHoverHeight"/>.
        /// </summary>
        static void LiftPrefabAboveGround(GameObject root, float clearance)
        {
            var bounds = MeasureActiveRendererWorldBounds(root);
            if (!bounds.HasValue)
            {
                return;
            }

            var liftWorld = -bounds.Value.min.y + clearance;
            if (liftWorld <= 0.001f)
            {
                return;
            }

            var scaleY = Mathf.Max(1e-4f, root.transform.lossyScale.y);
            var liftLocal = liftWorld / scaleY;
            foreach (Transform child in root.transform)
            {
                child.localPosition += Vector3.up * liftLocal;
            }
        }

        static Bounds? MeasureActiveRendererWorldBounds(GameObject root)
        {
            Bounds? combined = null;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.gameObject.activeSelf || smr.sharedMesh == null)
                {
                    continue;
                }

                var b = smr.bounds;
                combined = combined.HasValue
                    ? Encapsulate(combined.Value, b)
                    : b;
            }

            return combined;
        }

        static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        /// <summary>
        /// WC3 meshes are authored at ~100–1000 unit height. Normalize body height (tallest active
        /// skinned mesh) to match greybox after Scale × AnimatedHumanScaleFactor — not the combined
        /// AABB of all geosets, which inflates Melee/Flying death leftovers and makes sizes drift.
        /// </summary>
        static void NormalizePrefabScale(GameObject root, string unit)
        {
            const float BugMeleeLocalHeight = 1.134f;
            var targetLocalHeight =
                BugMeleeLocalHeight
                * UnitGreyboxVisuals.Scale
                / (UnitGreyboxVisuals.Scale * UnitGreyboxVisuals.AnimatedHumanScaleFactor);

            if (unit is "Human_Melee" or "Human_Caster" or "Human_Siege" or "Human_Super")
            {
                var roleScale = unit switch
                {
                    "Human_Melee" => UnitGreyboxVisuals.AnimatedHumanMeleeScaleFactor,
                    "Human_Caster" => UnitGreyboxVisuals.AnimatedHumanLargeRoleScaleFactor,
                    "Human_Siege" => UnitGreyboxVisuals.AnimatedHumanSiegeScaleFactor,
                    "Human_Super" => UnitGreyboxVisuals.AnimatedHumanSuperScaleFactor,
                    _ => 1f,
                };
                targetLocalHeight *= roleScale;
            }

            var bodyHeight = MeasureActiveBodyHeight(root);
            if (bodyHeight < 0.01f)
            {
                return;
            }

            var factor = targetLocalHeight / bodyHeight;
            root.transform.localScale = Vector3.one * factor;
        }

        static float MeasureActiveBodyHeight(GameObject root)
        {
            var best = 0f;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.gameObject.activeSelf || smr.sharedMesh == null)
                {
                    continue;
                }

                // localBounds tracks the posed mesh extent; more stable than world AABB of all geosets.
                var height = smr.localBounds.size.y;
                if (height > best)
                {
                    best = height;
                }
            }

            return best;
        }

        static void ApplyMaterialsAndAccents(
            GameObject root,
            string unit,
            Dictionary<string, List<GeosetInfo>> geosetMap)
        {
            var accentMat = AssetDatabase.LoadAssetAtPath<Material>(TeamAccentMaterialPath);
            if (accentMat == null)
            {
                throw new InvalidOperationException("Missing " + TeamAccentMaterialPath);
            }

            geosetMap.TryGetValue(unit, out var infos);
            var byIndex = infos != null
                ? infos.ToDictionary(g => g.index, g => g)
                : new Dictionary<int, GeosetInfo>();

            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                var geosetIndex = ResolveGeosetIndex(renderer);
                byIndex.TryGetValue(geosetIndex, out var info);

                var pureTeamColor = info != null
                    && (info.replaceableId == 1 || info.texture == "TeamColor");
                // Small WC3 team-underlay pieces stay solid accents so slot color stays readable.
                var solidUnderlayAccent = info != null
                    && info.hasTeamUnderlay
                    && info.verts > 0f
                    && info.verts <= 90f;
                var forcedAccent = ShouldForceTeamAccent(unit, geosetIndex);
                var casterCloak = unit == "Human_Caster" && geosetIndex == 0;

                if (pureTeamColor || solidUnderlayAccent || forcedAccent)
                {
                    renderer.sharedMaterial = accentMat;
                    if (!UnitVisualAccent.IsAccentName(renderer.gameObject.name))
                    {
                        renderer.gameObject.name =
                            UnitVisualAccent.TeamAccentTransformName + "_" + geosetIndex;
                    }

                    continue;
                }

                var texName = info?.texture;
                Material bodyMat = null;
                if (casterCloak)
                {
                    bodyMat = EnsureCasterCloakMaterial();
                }
                else if (!string.IsNullOrEmpty(texName)
                    && texName != "TeamGlow"
                    && texName != "Missing"
                    && texName != "TeamColor")
                {
                    bodyMat = AssetDatabase.LoadAssetAtPath<Material>(
                        MaterialFolder + "/Mat_" + texName + ".mat");
                }

                if (bodyMat == null)
                {
                    bodyMat = AssetDatabase.LoadAssetAtPath<Material>(
                        MaterialFolder + "/Mat_Footman.mat");
                }

                if (bodyMat != null)
                {
                    renderer.sharedMaterial = bodyMat;
                }

                if (casterCloak || (info != null && info.hasTeamUnderlay))
                {
                    renderer.gameObject.name =
                        UnitVisualAccent.TeamTintTransformName + "_" + geosetIndex;
                }
                else if (UnitVisualAccent.IsTeamColorTarget(renderer.gameObject.name))
                {
                    renderer.gameObject.name = "Body_" + geosetIndex;
                }
            }

            // If MDX had no team layers at all, keep a small accent marker for slot tint tests.
            if (UnitVisualAccent.CountAccents(root.transform) == 0)
            {
                EnsureFallbackTeamAccentMarker(root, accentMat);
            }
        }

        /// <summary>Solid team-color geosets that are not tagged TeamColor in the MDX.</summary>
        static bool ShouldForceTeamAccent(string unit, int geosetIndex) =>
            unit == "Human_Super" && geosetIndex == 3; // hood / cowl

        static void EnsureFallbackTeamAccentMarker(GameObject root, Material accentMat)
        {
            // Visible cloth/plume accents when the MDX has no TeamColor geosets (e.g. GodsPaladin).
            AddAccentPrimitive(root, accentMat, UnitVisualAccent.TeamAccentTransformName + "_Cape",
                PrimitiveType.Cube, new Vector3(0f, 0.85f, -0.28f), new Vector3(0.55f, 0.75f, 0.08f));
            AddAccentPrimitive(root, accentMat, UnitVisualAccent.TeamAccentTransformName + "_Plume",
                PrimitiveType.Sphere, new Vector3(0f, 1.55f, 0.05f), Vector3.one * 0.22f);
            AddAccentPrimitive(root, accentMat, UnitVisualAccent.TeamAccentTransformName + "_Trim",
                PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0.2f), new Vector3(0.35f, 0.08f, 0.35f));
        }

        static void AddAccentPrimitive(
            GameObject root,
            Material accentMat,
            string name,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale)
        {
            var accent = GameObject.CreatePrimitive(primitive);
            accent.name = name;
            accent.transform.SetParent(root.transform, false);
            accent.transform.localPosition = localPosition;
            accent.transform.localScale = localScale;
            var renderer = accent.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = accentMat;
            }

            var collider = accent.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        static void DisableHiddenGeosets(
            GameObject root,
            string unit,
            Dictionary<string, List<GeosetInfo>> geosetMap)
        {
            if (!geosetMap.TryGetValue(unit, out var infos) || infos == null)
            {
                return;
            }

            var byIndex = new Dictionary<int, SkinnedMeshRenderer>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var index = ResolveGeosetIndex(renderer);
                if (index >= 0)
                {
                    byIndex[index] = renderer;
                }
            }

            foreach (var info in infos)
            {
                if (!info.hiddenInStand)
                {
                    continue;
                }

                if (byIndex.TryGetValue(info.index, out var renderer))
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Blender exports geosets as "0 Model", "10 Model" — parse numeric prefix.</summary>
        static int ResolveGeosetIndex(SkinnedMeshRenderer renderer)
        {
            var name = renderer.gameObject.name;
            var i = 0;
            while (i < name.Length && char.IsDigit(name[i]))
            {
                i++;
            }

            if (i > 0 && int.TryParse(name.Substring(0, i), out var index))
            {
                return index;
            }

            return -1;
        }

        static bool IsAccentName(string name) => UnitVisualAccent.IsAccentName(name);

        static Dictionary<string, List<GeosetInfo>> LoadGeosetMap()
        {
            var abs = Path.Combine(Application.dataPath, "Game/Art/Models/Human/geoset_textures.json");
            var text = File.ReadAllText(abs);
            return ParseGeosetJson(text);
        }

        static Dictionary<string, List<GeosetInfo>> ParseGeosetJson(string text)
        {
            var map = new Dictionary<string, List<GeosetInfo>>(StringComparer.Ordinal);
            // Expect object of arrays. Use Newtonsoft if available; else lightweight scan.
            foreach (var unit in UnitNames)
            {
                var keyIndex = text.IndexOf("\"" + unit + "\"", StringComparison.Ordinal);
                if (keyIndex < 0)
                {
                    continue;
                }

                var arrayStart = text.IndexOf('[', keyIndex);
                var arrayEnd = text.IndexOf(']', arrayStart);
                if (arrayStart < 0 || arrayEnd < 0)
                {
                    continue;
                }

                var arrayJson = text.Substring(arrayStart, arrayEnd - arrayStart + 1);
                var wrapped = "{\"items\":" + arrayJson + "}";
                var file = JsonUtility.FromJson<GeosetArrayWrapper>(wrapped);
                map[unit] = file.items != null ? file.items.ToList() : new List<GeosetInfo>();
            }

            return map;
        }

        [Serializable]
        class GeosetArrayWrapper
        {
            public GeosetInfo[] items;
        }

        [Serializable]
        public class GeosetInfo
        {
            public int index;
            public int materialId;
            public string texture;
            public int replaceableId;
            public bool hasTeamUnderlay;
            public float verts;
            public bool hiddenInStand;
        }
    }
}
