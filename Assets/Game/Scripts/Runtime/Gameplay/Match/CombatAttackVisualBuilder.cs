using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class CombatAttackVisualBuilder
    {
        static readonly Color HumanSpellColor = new(0.9f, 0.15f, 0.1f);

        const float ProjectileScale = 2f;
        /// <summary>
        /// Archer/flying/tower arrows: <c>ProjectileBolt</c> (Bolt_lvl1 ×0.5), root ×0.8625 (+15% vs prior 0.75).
        /// Ballista / main / barracks: <c>ProjectileBoltLvl3</c> at 1:1 with unit Bolt_lvl3.
        /// </summary>
        const float BoltScaleBallista = 1f;
        const float BoltScaleArrow = 0.75f * 1.15f;
        const float FireballDiameter = 0.55f;

        static Material _fireMaterial;
        static GameObject _boltPrefab;
        static GameObject _boltLvl3Prefab;
        static GameObject _catapultRockPrefab;

        public static GameObject CreateProjectileVisual(CombatProjectileState projectile, Transform parent)
        {
            GameObject visual;
            if (UsesCatapultRockVisual(projectile))
            {
                visual = CreateBolt(projectile, scale: 1f, ResolveCatapultRockPrefab());
            }
            else if (UsesFireballVisual(projectile.AttackerRole))
            {
                visual = CreateFireball(projectile);
            }
            else
            {
                visual = CreateBolt(projectile, ResolveBoltScale(projectile), ResolveBoltPrefab(projectile));
            }

            visual.transform.SetParent(parent, false);
            return visual;
        }

        static bool UsesFireballVisual(UnitRole role) =>
            role is UnitRole.Caster or UnitRole.Hero;

        /// <summary>Bonus Super catapult: splash shots use the rock mesh (parabolic alone is not enough —
        /// EditMode projectile helpers often set IsParabolic for all roles).</summary>
        static bool UsesCatapultRockVisual(CombatProjectileState projectile) =>
            projectile != null
            && projectile.AppliesSplashAoe
            && projectile.AttackerRole == UnitRole.Super
            && !projectile.IsBuildingAttack;

        static bool UsesBallistaBolt(CombatProjectileState projectile)
        {
            if (projectile.IsBuildingAttack)
            {
                return BuildingRules.IsMain(projectile.SourceBuildingId)
                    || BuildingRules.IsBarracks(projectile.SourceBuildingId);
            }

            return projectile.AttackerRole == UnitRole.Super;
        }

        static float ResolveBoltScale(CombatProjectileState projectile) =>
            UsesBallistaBolt(projectile) ? BoltScaleBallista : BoltScaleArrow;

        static GameObject ResolveBoltPrefab(CombatProjectileState projectile)
        {
            if (UsesBallistaBolt(projectile))
            {
                if (_boltLvl3Prefab == null) _boltLvl3Prefab = LoadBoltLvl3Prefab();
                return _boltLvl3Prefab != null ? _boltLvl3Prefab : LoadBoltPrefabCached();
            }

            return LoadBoltPrefabCached();
        }

        static Material LoadMaterial(string path) => Resources.Load<Material>(path);

        static GameObject CreateBolt(CombatProjectileState projectile, float scale, GameObject boltPrefab)
        {
            var root = new GameObject($"Projectile_{projectile.ProjectileId}");
            root.transform.localScale = Vector3.one * scale;

            if (boltPrefab != null)
            {
                var bolt = Object.Instantiate(boltPrefab, root.transform, false);
                bolt.name = "Bolt";
            }
            else
            {
                CreateRoleCube(root.transform, 0.15f * ProjectileScale, HumanSpellColor);
            }

            var teamColor = root.GetComponentInChildren<TtUnitTeamColor>(true);
            if (teamColor != null)
            {
                teamColor.ApplyTeamColor(MatchPlayerColors.GetSlotColor(projectile.AttackerOwnerSlot));
            }

            return root;
        }

        static GameObject LoadBoltPrefabCached()
        {
            if (_boltPrefab == null) _boltPrefab = Resources.Load<GameObject>("Art/ProjectileBolt");
            return _boltPrefab;
        }

        static GameObject LoadBoltLvl3Prefab() => Resources.Load<GameObject>("Art/ProjectileBoltLvl3");

        static GameObject ResolveCatapultRockPrefab()
        {
            if (_catapultRockPrefab == null)
            {
                _catapultRockPrefab = Resources.Load<GameObject>("Art/ProjectileCatapultRock");
            }

            return _catapultRockPrefab != null ? _catapultRockPrefab : LoadBoltPrefabCached();
        }

        static void CreateRoleCube(Transform parent, float scale, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BoltFallback";
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = Vector3.one * scale;

            var collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(collider);
                }
                else
                {
                    Object.DestroyImmediate(collider);
                }
            }

            var renderer = cube.GetComponent<Renderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_BaseColor"), color);
            renderer.SetPropertyBlock(block);
        }

        static GameObject CreateFireball(CombatProjectileState projectile)
        {
            var root = new GameObject($"Projectile_{projectile.ProjectileId}");
            root.transform.localScale = Vector3.one;

            if (_fireMaterial == null) _fireMaterial = LoadMaterial("Art/ProjectileFire");

            var core = CreatePrimitiveRoot(PrimitiveType.Sphere, "Core");
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * FireballDiameter;
            ApplyMaterial(core, _fireMaterial);

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = FireballDiameter * (0.22f / 0.48f);
            trail.endWidth = 0f;
            trail.material = _fireMaterial;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0f), new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            return root;
        }

        static GameObject CreatePrimitiveRoot(PrimitiveType primitive, string name)
        {
            var visual = GameObject.CreatePrimitive(primitive);
            visual.name = name;
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(collider);
                }
                else
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return visual;
        }

        static void ApplyMaterial(GameObject visual, Material material)
        {
            if (material == null) return;
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = material;
        }

        public static void UpdateProjectileTransform(Transform visual, CombatProjectileState projectile)
        {
            var progress = projectile.Progress;
            var position = CombatProjectileTrajectory.Evaluate(
                projectile.StartPosition,
                projectile.TargetPosition,
                progress,
                projectile.IsParabolic);
            visual.position = position;

            var nextProgress = Mathf.Min(1f, progress + 0.04f);
            var nextPosition = CombatProjectileTrajectory.Evaluate(
                projectile.StartPosition,
                projectile.TargetPosition,
                nextProgress,
                projectile.IsParabolic);
            var direction = nextPosition - position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = projectile.TargetPosition - projectile.StartPosition;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                visual.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}
