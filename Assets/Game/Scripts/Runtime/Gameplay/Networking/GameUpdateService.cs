using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Gameplay.Networking
{
    /// <summary>
    /// Force-update via GitHub Releases (no Cloudflare R2 / no credit card).
    /// Flow: GET /releases/latest → version.json asset → compare → download zip → ApplyUpdate.bat.
    /// </summary>
    public static class GameUpdateService
    {
        public const string ManifestUrlEnv = "BARAKI_UPDATE_URL";
        public const string GitHubRepoEnv = "BARAKI_GITHUB_REPO";

        public static GameUpdateManifest RemoteManifest { get; private set; }
        public static bool UpdateRequired { get; private set; }
        public static bool CheckCompleted { get; private set; }

        public static async UniTask RefreshAsync()
        {
            CheckCompleted = false;
            UpdateRequired = false;
            RemoteManifest = null;

            if (Application.isEditor)
            {
                CheckCompleted = true;
                UpdateRequired = false;
                await UniTask.Yield();
                return;
            }

            try
            {
                var overrideManifest = Environment.GetEnvironmentVariable(ManifestUrlEnv);
                string manifestJson;
                if (!string.IsNullOrWhiteSpace(overrideManifest))
                {
                    manifestJson = await GetTextAsync(overrideManifest.Trim());
                }
                else
                {
                    var releaseJson = await GetTextAsync(ResolveLatestApiUrl());
                    if (!GitHubReleaseUpdateRules.TryGetAssetDownloadUrl(
                            releaseJson,
                            GitHubReleaseUpdateRules.ManifestAssetName,
                            out var manifestUrl))
                    {
                        // Fallback: build download URL from tag if version.json naming is stable.
                        if (!GitHubReleaseUpdateRules.TryGetTagName(releaseJson, out var tag))
                        {
                            Debug.LogWarning("GameUpdateService: latest release missing tag/version.json");
                            CheckCompleted = true;
                            return;
                        }

                        manifestUrl = GitHubReleaseUpdateRules.ReleaseDownloadUrl(
                            tag,
                            GitHubReleaseUpdateRules.ManifestAssetName);
                    }

                    manifestJson = await GetTextAsync(manifestUrl);
                }

                if (!GameUpdateManifestParser.TryParse(manifestJson, out var manifest))
                {
                    Debug.LogWarning("GameUpdateService: invalid version.json");
                    CheckCompleted = true;
                    return;
                }

                RemoteManifest = manifest;
                UpdateRequired = GameUpdateVersionRules.IsUpdateRequired(
                    Application.version,
                    manifest.EffectiveMinVersion);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"GameUpdateService.RefreshAsync: {ex.Message}");
            }
            finally
            {
                CheckCompleted = true;
            }
        }

        public static async UniTask DownloadAndApplyAsync()
        {
            if (RemoteManifest == null || string.IsNullOrWhiteSpace(RemoteManifest.url))
            {
                throw new InvalidOperationException("No remote manifest/url.");
            }

            var stagingRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BARAKI",
                "staging");
            Directory.CreateDirectory(stagingRoot);
            var zipPath = Path.Combine(stagingRoot, "baraki-windows.zip");

            using (var req = UnityWebRequest.Get(RemoteManifest.url))
            {
                req.downloadHandler = new DownloadHandlerFile(zipPath);
                SetGitHubHeaders(req);
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException("Download failed: " + req.error);
                }
            }

            if (!string.IsNullOrWhiteSpace(RemoteManifest.sha256))
            {
                var hash = ComputeSha256Hex(zipPath);
                if (!string.Equals(hash, RemoteManifest.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SHA256 mismatch for downloaded build.");
                }
            }

            var installDir = Path.GetDirectoryName(Application.dataPath);
            var exeName = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            var helperPath = Path.Combine(installDir ?? ".", "ApplyUpdate.bat");
            if (!File.Exists(helperPath))
            {
                throw new InvalidOperationException("ApplyUpdate.bat missing next to game.");
            }

            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var args =
                $"\"{zipPath}\" \"{installDir}\" \"{exeName}\" {pid}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = installDir,
            });

            Application.Quit();
            await UniTask.CompletedTask;
        }

        static string ResolveLatestApiUrl()
        {
            var repo = Environment.GetEnvironmentVariable(GitHubRepoEnv);
            if (!string.IsNullOrWhiteSpace(repo))
            {
                var parts = repo.Trim().Split('/');
                if (parts.Length == 2)
                {
                    return GitHubReleaseUpdateRules.LatestReleaseApiUrl(parts[0], parts[1]);
                }
            }

            return GitHubReleaseUpdateRules.LatestReleaseApiUrl();
        }

        static async UniTask<string> GetTextAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            SetGitHubHeaders(req);
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"GET {url} failed: {req.responseCode} {req.error}");
            }

            return req.downloadHandler.text;
        }

        static void SetGitHubHeaders(UnityWebRequest req)
        {
            // GitHub API requires a User-Agent.
            req.SetRequestHeader("User-Agent", "BARAKI-GameUpdate");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
        }

        static string ComputeSha256Hex(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
