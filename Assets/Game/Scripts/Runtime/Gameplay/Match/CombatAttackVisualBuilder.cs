using Game.Gameplay.Combat;
using Game.Gameplay.Data;
using UnityEngine;

namespace Game.Gameplay.Match
{
    public static class CombatAttackVisualBuilder
    {
        static readonly Color HumanArrowColor = new(0.45f, 0.28f, 0.12f);
        static readonly Color HumanSpellColor = new(0.9f, 0.15f, 0.1f);

        const float ProjectileScale = 2f;

        static Material _fireMaterial;
        static Material _arrowShaftMaterial;
        static Material _arrowHeadMaterial;
        static GameObject _boltPrefab;

        public static GameObject CreateProjectileVisual(CombatProjectileState projectile, Transform parent)
        {
            GameObject visual;
            if (projectile.IsBuildingAttack)
            {
                visual = CreateBuildingShot(projectile);
            }
            else if (projectile.AttackerRole == UnitRole.Caster)
            {
                visual = CreateFireball(projectile);
            }
            else if (projectile.AttackerRole is UnitRole.Ranged or UnitRole.Flying)
            {
                visual = CreateArrow(projectile);
            }
            else if (projectile.AttackerRole == UnitRole.Super)
            {
                visual = CreateBolt(projectile);
            }
            else
            {
                visual = CreateRoleCube(projectile, 0.2f * ProjectileScale, HumanSpellColor);
            }

            visual.transform.SetParent(parent, false);
            return visual;
        }

        static Material LoadMaterial(string path)
        {
            var material = Resources.Load<Material>(path);
            return material != null ? material : null;
        }

        static GameObject CreateBuildingShot(CombatProjectileState projectile)
        {
            var visual = CreatePrimitiveRoot(PrimitiveType.Cube, $"BuildingShot_{projectile.ProjectileId}");
            visual.transform.localScale = Vector3.one * TowerCombatRules.ProjectileCubeScale;
            ApplyColor(visual, MatchPlayerColors.GetSlotColor(projectile.AttackerOwnerSlot));
            return visual;
        }

        static GameObject CreateRoleCube(CombatProjectileState projectile, float scale, Color color)
        {
            var visual = CreatePrimitiveRoot(PrimitiveType.Cube, $"Projectile_{projectile.ProjectileId}");
            visual.transform.localScale = Vector3.one * scale;
            ApplyColor(visual, color);
            return visual;
        }

        static GameObject CreateArrow(CombatProjectileState projectile)
        {
            var root = new GameObject($"Projectile_{projectile.ProjectileId}");
            root.transform.localScale = Vector3.one;

            if (_arrowShaftMaterial == null) _arrowShaftMaterial = LoadMaterial("Art/ProjectileArrowShaft");
            if (_arrowHeadMaterial == null) _arrowHeadMaterial = LoadMaterial("Art/ProjectileArrowHead");

            var shaft = CreatePrimitiveRoot(PrimitiveType.Cube, "Shaft");
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localScale = new Vector3(0.05f, 0.05f, 0.5f);
            ApplyMaterial(shaft, _arrowShaftMaterial);

            var head = CreatePrimitiveRoot(PrimitiveType.Cube, "Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.26f);
            head.transform.localScale = new Vector3(0.12f, 0.12f, 0.14f);
            ApplyMaterial(head, _arrowHeadMaterial);

            return root;
        }

        /// <summary>Ballista bolt — uses the actual bolt model from the TT ballista pack.</summary>
        static GameObject CreateBolt(CombatProjectileState projectile)
        {
            var root = new GameObject($"Projectile_{projectile.ProjectileId}");
            root.transform.localScale = Vector3.one;

            if (_boltPrefab == null) _boltPrefab = LoadBoltPrefab();
            if (_boltPrefab != null)
            {
                var bolt = Object.Instantiate(_boltPrefab, root.transform, false);
                bolt.name = "Bolt";
                return root;
            }

            // Fallback (no Resources): thick arrow-shaped bolt.
            if (_arrowShaftMaterial == null) _arrowShaftMaterial = LoadMaterial("Art/ProjectileArrowShaft");
            if (_arrowHeadMaterial == null) _arrowHeadMaterial = LoadMaterial("Art/ProjectileArrowHead");

            var shaft = CreatePrimitiveRoot(PrimitiveType.Cube, "Shaft");
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localScale = new Vector3(0.09f, 0.09f, 0.75f);
            ApplyMaterial(shaft, _arrowShaftMaterial);

            var head = CreatePrimitiveRoot(PrimitiveType.Cube, "Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            head.transform.localScale = new Vector3(0.16f, 0.16f, 0.18f);
            ApplyMaterial(head, _arrowHeadMaterial);

            return root;
        }

        static GameObject LoadBoltPrefab() => Resources.Load<GameObject>("Art/ProjectileBolt");

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

        static void ApplyColor(GameObject visual, Color color)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_BaseColor"), color);
            renderer.SetPropertyBlock(block);
        }

        static void ApplyMaterial(GameObject visual, Material material)
        {
            if (material == null) return;
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = material;
        }

        /// <summary>Building shots = owner-colored cubes; unit shots keep race/role styling.</summary>
        public static void ResolveVisualStyle(
            CombatProjectileState projectile,
            out PrimitiveType primitive,
            out Vector3 localScale,
            out Color color)
        {
            if (projectile.IsBuildingAttack)
            {
                primitive = PrimitiveType.Cube;
                localScale = Vector3.one * TowerCombatRules.ProjectileCubeScale;
                color = MatchPlayerColors.GetSlotColor(projectile.AttackerOwnerSlot);
                return;
            }

            var isRanged = projectile.AttackerRole is UnitRole.Ranged or UnitRole.Flying;
            var isCaster = projectile.AttackerRole == UnitRole.Caster;

            if (isRanged)
            {
                primitive = PrimitiveType.Cube;
                localScale = new Vector3(0.08f, 0.08f, 0.55f) * ProjectileScale;
                color = HumanArrowColor;
                return;
            }

            primitive = PrimitiveType.Cube;
            localScale = Vector3.one * ((isCaster ? 0.24f : 0.2f) * ProjectileScale);
            color = HumanSpellColor;
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
