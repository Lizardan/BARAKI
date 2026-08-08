using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Mdx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Audits every MDX source under Assets/NewModels/MODEL_PREFAB/Sources and reports which
    /// models are "scene-ready": they have a run sequence (Walk/Run) AND an attack sequence AND
    /// every model texture resolves to a real local file (DDS/BLP) or is a replaceable
    /// (TeamColor/TeamGlow) slot. Models whose textures would fall back to white.png are listed
    /// with their missing texture names. Uses the same resolution rules as MdxPrefabBuilder.
    /// </summary>
    public static class MdxModelAudit
    {
        public const string SourceAssetFolder = "Assets/NewModels/MODEL_PREFAB/Sources";
        public const string SourceFolder = "Assets/NewModels/SOURSE";

        public sealed class Record
        {
            public string Name;
            public readonly List<string> SequenceNames = new();
            public readonly List<string> TextureRefs = new();
            public readonly List<string> MissingTextures = new();

            public bool HasRun => SequenceNames.Any(n =>
                n.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0);
            public bool HasAttack => SequenceNames.Any(n =>
                n.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0);
            public bool AllTexturesResolved => MissingTextures.Count == 0;
            public bool SceneReady => HasRun && HasAttack && AllTexturesResolved;
        }

        [MenuItem("BARAKI/Units/Audit MDX Models (scene-ready)")]
        public static void AuditFromMenu()
        {
            var records = Audit();
            var ready = records.Where(r => r.SceneReady).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var noRun = records.Count(r => !r.HasRun);
            var noAttack = records.Count(r => !r.HasAttack);
            var missingTex = records.Count(r => !r.AllTexturesResolved);

            Debug.Log($"MDX audit: {records.Count} total; {ready.Count} scene-ready "
                + $"(run+attack+all textures); {noRun} no-run, {noAttack} no-attack, "
                + $"{missingTex} with missing textures.");

            Debug.LogFormat("Scene-ready models ({0}):\n{1}", ready.Count,
                string.Join(", ", ready.Select(r => r.Name)));

            var missingGroup = records
                .SelectMany(r => r.MissingTextures.Select(m => new { r.Name, m }))
                .GroupBy(x => x.m, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Missing texture -> models that reference it:");
            if (missingGroup.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                foreach (var g in missingGroup)
                {
                    var distinctNames = g.Select(x => x.Name).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                    var shown = string.Join(", ", distinctNames.Take(8));
                    if (distinctNames.Count > 8)
                    {
                        shown += "... (+" + (distinctNames.Count - 8) + ")";
                    }

                    sb.AppendLine("  " + g.Key + "  ->  " + shown);
                }
            }

            var file = Path.Combine(Application.dataPath, "..", "Temp", "mtexaudit.txt");
            var full = new StringBuilder();
            full.AppendLine("=== Scene-ready models (" + ready.Count + ") ===");
            foreach (var r in ready)
            {
                full.AppendLine("  " + r.Name);
            }

            full.AppendLine();
            full.Append(sb.ToString());
            File.WriteAllText(file, full.ToString());
            Debug.Log("Audit report written to " + Path.GetFullPath(file));
            EditorUtility.RevealInFinder(Path.GetFullPath(file));
        }

        public static List<Record> Audit()
        {
            var blpIndex = BuildBlpIndex();
            var records = new List<Record>();

            var files = Directory.GetFiles(ProjectPathToDisk(SourceAssetFolder), "*.mdx.bytes", SearchOption.TopDirectoryOnly);
            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(file).Replace(".mdx.bytes", "");
                var rec = new Record { Name = name };
                try
                {
                    var model = MdxModel.Load(File.ReadAllBytes(file));
                    foreach (var s in model.Sequences)
                    {
                   rec.SequenceNames.Add(s.Name);
                    }

                    foreach (var t in model.Textures)
                    {
                        rec.TextureRefs.Add(t.Path);
                        if (ResolveReplaceableId(t) != 0)
                        {
                            continue;
                        }

                        if (!FindTexture(t.Path, blpIndex))
                        {
                            rec.MissingTextures.Add(Path.GetFileName(t.Path).Replace('\\', '/'));
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MDX audit] failed to parse " + name + " -> " + e.Message);
                }

                records.Add(rec);
            }

            return records;
        }

        static int ResolveReplaceableId(Game.Mdx.Texture texture)
        {
            if (texture.ReplaceableId == 1 || texture.ReplaceableId == 2)
            {
                return (int)texture.ReplaceableId;
            }

            var path = texture.Path;
            if (path.StartsWith("ReplaceableTextures\\TeamColor", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (path.StartsWith("ReplaceableTextures\\TeamGlow", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return (int)texture.ReplaceableId;
        }

        static bool FindTexture(string mdxTexturePath, Dictionary<string, string> blpIndex)
        {
            var clean = mdxTexturePath.Replace('\\', '/');
            var baseName = Path.GetFileName(clean);
            var key = Path.GetFileNameWithoutExtension(clean);

            if (blpIndex.TryGetValue(key + ".dds", out _)) return true;
            if (blpIndex.TryGetValue(baseName, out _)) return true;
            if (blpIndex.TryGetValue(key + ".blp", out _)) return true;

            foreach (var entry in blpIndex)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(entry.Key), key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static Dictionary<string, string> BuildBlpIndex()
        {
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var root = ProjectPathToDisk(SourceFolder);
            foreach (var file in Directory.GetFiles(root, "*.blp", SearchOption.AllDirectories))
            {
                index[Path.GetFileName(file)] = file;
            }

            foreach (var file in Directory.GetFiles(root, "*.dds", SearchOption.AllDirectories))
            {
                index[Path.GetFileName(file)] = file;
            }

            return index;
        }

        static string ProjectPathToDisk(string assetPath)
        {
            var normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, "..", normalized);
        }
    }
}