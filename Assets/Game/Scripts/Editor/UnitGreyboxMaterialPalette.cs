using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Persistent URP Lit materials for stylized unit prefabs.</summary>
    public static class UnitGreyboxMaterialPalette
    {
        public const string MaterialsPath = "Assets/Game/Art/Materials/Units";
        const string ReferenceMaterialPath = "Assets/Game/Art/Materials/EnvironmentGround.mat";

        public static Material HumanSkin { get; private set; }
        public static Material HumanSteel { get; private set; }
        public static Material HumanGold { get; private set; }
        public static Material HumanWood { get; private set; }
        public static Material HumanLeather { get; private set; }
        public static Material HumanCloth { get; private set; }
        public static Material HumanArcane { get; private set; }
        public static Material BugChitin { get; private set; }
        public static Material BugChitinDark { get; private set; }
        public static Material BugUnderbelly { get; private set; }
        public static Material BugBone { get; private set; }
        public static Material BugGlow { get; private set; }
        public static Material TeamAccent { get; private set; }

        public static void EnsureMaterials()
        {
            EnsureFolder(MaterialsPath);

            HumanSkin = GetOrCreate("UnitHumanSkin", new Color(0.82f, 0.72f, 0.62f), 0.28f);
            HumanSteel = GetOrCreate("UnitHumanSteel", new Color(0.72f, 0.76f, 0.82f), 0.62f, metallic: 0.75f);
            HumanGold = GetOrCreate("UnitHumanGold", new Color(0.82f, 0.68f, 0.28f), 0.55f, metallic: 0.65f);
            HumanWood = GetOrCreate("UnitHumanWood", new Color(0.42f, 0.28f, 0.16f), 0.18f);
            HumanLeather = GetOrCreate("UnitHumanLeather", new Color(0.38f, 0.26f, 0.18f), 0.2f);
            HumanCloth = GetOrCreate("UnitHumanCloth", new Color(0.28f, 0.3f, 0.38f), 0.12f);
            HumanArcane = GetOrCreate("UnitHumanArcane", new Color(0.35f, 0.55f, 0.95f), 0.7f);
            BugChitin = GetOrCreate("UnitBugChitin", new Color(0.42f, 0.28f, 0.52f), 0.4f);
            BugChitinDark = GetOrCreate("UnitBugChitinDark", new Color(0.18f, 0.12f, 0.2f), 0.3f);
            BugUnderbelly = GetOrCreate("UnitBugUnderbelly", new Color(0.28f, 0.2f, 0.16f), 0.22f);
            BugBone = GetOrCreate("UnitBugBone", new Color(0.78f, 0.72f, 0.58f), 0.35f);
            BugGlow = GetOrCreate("UnitBugGlow", new Color(0.35f, 0.95f, 0.4f), 0.75f);
            TeamAccent = GetOrCreate("UnitTeamAccent", Color.white, 0.45f);

            AssetDatabase.SaveAssets();
        }

        static Material GetOrCreate(string name, Color color, float smoothness, float metallic = 0f)
        {
            var path = $"{MaterialsPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = ResolveLitShader();
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Shader ResolveLitShader()
        {
            var reference = AssetDatabase.LoadAssetAtPath<Material>(ReferenceMaterialPath);
            if (reference != null && reference.shader != null)
            {
                return reference.shader;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Standard");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
