using System;
using System.Collections.Generic;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Scans Slash / CFXR / Hyper Casual prefab packs and classifies them for the FX viewer.</summary>
    public static class AbilityVfxPrefabIndex
    {
        public const string SlashPrefabs = "Assets/Adjustable Slash VFX Pack/Prefabs";
        public const string CfxrPrefabs = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs";
        public const string HyperCasualPrefabs = "Assets/Lana Studio/Hyper Casual FX/Prefabs";

        public readonly struct Entry
        {
            public Entry(string path, string displayName, GameObject prefab, AbilityVfxKind kind)
            {
                Path = path;
                DisplayName = displayName;
                Prefab = prefab;
                Kind = kind;
            }

            public string Path { get; }
            public string DisplayName { get; }
            public GameObject Prefab { get; }
            public AbilityVfxKind Kind { get; }
        }

        public static List<Entry> Scan()
        {
            var list = new List<Entry>();
            CollectFolder(SlashPrefabs, list);
            CollectFolder(CfxrPrefabs, list);
            CollectFolder(HyperCasualPrefabs, list);
            list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return list;
        }

        public static AbilityVfxKind Classify(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return AbilityVfxKind.Cast;
            }

            var path = assetPath.Replace('\\', '/');
            var file = System.IO.Path.GetFileNameWithoutExtension(path);

            if (path.IndexOf("Adjustable Slash VFX Pack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AbilityVfxKind.Hit;
            }

            if (path.IndexOf("Hyper Casual FX/Prefabs/Area", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("Hyper Casual FX/Prefabs/Shine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AbilityVfxKind.Aura;
            }

            if (path.IndexOf("Hyper Casual FX/Prefabs/Flash", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AbilityVfxKind.Hit;
            }

            if (ContainsIgnoreCase(file, "Magic Aura")
                || ContainsIgnoreCase(file, "Shiny Item")
                || (ContainsIgnoreCase(file, "LightGlow") && ContainsIgnoreCase(file, "Loop")))
            {
                return AbilityVfxKind.Aura;
            }

            if (path.IndexOf("/Impacts/", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("Sword Trails", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("/Explosions/", StringComparison.OrdinalIgnoreCase) >= 0
                || ContainsIgnoreCase(file, "Sword Hit")
                || ContainsIgnoreCase(file, "Ground Hit")
                || ContainsIgnoreCase(file, "Firewall")
                || file.IndexOf("Hit ", StringComparison.OrdinalIgnoreCase) >= 0
                || file.StartsWith("CFXR Hit", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("CFXR3 Hit", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("CFXR2 Hit", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("CFXR4 Sword Hit", StringComparison.OrdinalIgnoreCase))
            {
                return AbilityVfxKind.Hit;
            }

            return AbilityVfxKind.Cast;
        }

        static void CollectFolder(string folder, List<Entry> into)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || path.EndsWith("SlashMesh.prefab", StringComparison.Ordinal))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                into.Add(new Entry(path, prefab.name, prefab, Classify(path)));
            }
        }

        static bool ContainsIgnoreCase(string value, string fragment) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
