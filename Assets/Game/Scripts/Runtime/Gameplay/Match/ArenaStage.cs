using UnityEngine;

namespace Game.Gameplay.Match
{
    /// <summary>
    /// Круг-арена далеко от игровой карты на той же сцене. Строится в рантайме,
    /// чтобы не зависеть от правок сцены: платформа-цилиндр + бортик + разметка центра.
    /// </summary>
    public static class ArenaStage
    {
        static GameObject _root;

        public static Vector3 Center => ArenaRules.Center;

        public static void EnsureBuilt()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("--- ARENA ---");
            var center = ArenaRules.Center;
            var diameter = ArenaRules.Radius * 2f;

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "ArenaPlatform";
            platform.transform.SetParent(_root.transform, false);
            platform.transform.position = new Vector3(
                center.x,
                center.y - ArenaRules.PlatformThickness * 0.5f,
                center.z);
            platform.transform.localScale = new Vector3(
                diameter,
                ArenaRules.PlatformThickness,
                diameter);
            ApplyMaterial(platform, "ArenaPlatform", new Color(0.30f, 0.29f, 0.26f));

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "ArenaRim";
            rim.transform.SetParent(_root.transform, false);
            rim.transform.position = new Vector3(
                center.x,
                center.y - ArenaRules.PlatformThickness * 0.5f,
                center.z);
            rim.transform.localScale = new Vector3(
                diameter + 2.4f,
                ArenaRules.PlatformThickness * 0.6f,
                diameter + 2.4f);
            ApplyMaterial(rim, "ArenaRim", new Color(0.18f, 0.17f, 0.15f));

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "ArenaInner";
            inner.transform.SetParent(_root.transform, false);
            inner.transform.position = new Vector3(
                center.x,
                center.y + 0.01f,
                center.z);
            inner.transform.localScale = new Vector3(diameter * 0.55f, 0.02f, diameter * 0.55f);
            ApplyMaterial(inner, "ArenaInner", new Color(0.42f, 0.34f, 0.20f));

            foreach (var collider in _root.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.Destroy(collider);
            }
        }

        static void ApplyMaterial(GameObject target, string materialName, Color color)
        {
            if (!target.TryGetComponent<Renderer>(out var renderer))
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
            };
            material.SetFloat("_Smoothness", 0.15f);
            renderer.sharedMaterial = material;
        }
    }
}
