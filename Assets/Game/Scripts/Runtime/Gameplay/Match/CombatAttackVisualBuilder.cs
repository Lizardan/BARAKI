using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class CombatAttackVisualBuilder
    {
        static readonly Color HumanSpellColor = new(0.9f, 0.15f, 0.1f);

        const float ProjectileScale = 2f;
        /// <summary>Bolt prefab already carries a 0.5 normalized scale; these multipliers keep small shots compact and big shots prominent.</summary>
        const float BoltScaleLarge = 1f;
        const float BoltScaleSmall = 0.75f;

        static Material _fireMaterial;
        static GameObject _boltPrefab;

        public static GameObject CreateProjectileVisual(CombatProjectileState projectile, Transform parent)
        {
            var visual = projectile.AttackerRole == UnitRole.Caster
                ? CreateFireball(projectile)
                : CreateBolt(projectile, ResolveBoltScale(projectile));

            visual.transform.SetParent(parent, false);
            return visual;
        }

        static float ResolveBoltScale(CombatProjectileState projectile)
        {
            if (projectile.IsBuildingAttack)
            {
                return BuildingRules.IsMain(projectile.SourceBuildingId) ? BoltScaleLarge : BoltScaleSmall;
            }

            return projectile.AttackerRole == UnitRole.Super ? BoltScaleLarge : BoltScaleSmall;
        }

        static Material LoadMaterial(string path) => Resources.Load<Material>(path);

        static GameObject CreateBolt(CombatProjectileState projectile, float scale)
        {
            var root = new GameObject($"Projectile_{projectile.ProjectileId}");
            root.transform.localScale = Vector3.one * scale;

            if (_boltPrefab == null) _boltPrefab = LoadBoltPrefab();
            if (_boltPrefab != null)
            {
                var bolt = Object.Instantiate(_boltPrefab, root.transform, false);
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

        static GameObject LoadBoltPrefab() => Resources.Load<GameObject>("Art/ProjectileBolt");

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
            core.transform.localScale = Vector3.one * (0.24f * ProjectileScale);
            ApplyMaterial(core, _fireMaterial);

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.22f;
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
