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
        /// <summary>Set when the check finished without a usable remote manifest (network/API/parse).</summary>
        public static bool CheckFailed { get; private set; }
        public static string LastError { get; private set; }

        public static async UniTask RefreshAsync()
        {
            CheckCompleted = false;
            CheckFailed = false;
            LastError = null;
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
                    manifestJson = await GetTextAsync(overrideManifest.Trim(), githubApi: false);
                }
                else
                {
                    var releaseJson = await GetTextAsync(ResolveLatestApiUrl(), githubApi: true);
                    if (GitHubReleaseUpdateRules.LooksLikeHtmlErrorPage(releaseJson))
                    {
                        throw new InvalidOperationException(
                            "GitHub API returned an HTML error page (temporary outage).");
                    }

                    // Prefer constructed download URL from tag — more reliable than scraping
                    // browser_download_url around GitHub's huge uploader objects.
                    if (!GitHubReleaseUpdateRules.TryGetTagName(releaseJson, out var tag))
                    {
                        throw new InvalidOperationException("Latest release JSON missing tag_name.");
                    }

                    var manifestUrl = GitHubReleaseUpdateRules.ReleaseDownloadUrl(
                        tag,
                        GitHubReleaseUpdateRules.ManifestAssetName);

                    if (GitHubReleaseUpdateRules.TryGetAssetDownloadUrl(
                            releaseJson,
                            GitHubReleaseUpdateRules.ManifestAssetName,
                            out var assetUrl)
                        && !string.IsNullOrWhiteSpace(assetUrl))
                    {
                        manifestUrl = assetUrl;
                    }

                    manifestJson = await GetTextAsync(manifestUrl, githubApi: false);
                }

                if (GitHubReleaseUpdateRules.LooksLikeHtmlErrorPage(manifestJson))
                {
                    throw new InvalidOperationException(
                        "version.json download returned an HTML error page.");
                }

                if (!GameUpdateManifestParser.TryParse(manifestJson, out var manifest))
                {
                    throw new InvalidOperationException("Invalid version.json from release.");
                }

                RemoteManifest = manifest;
                UpdateRequired = GameUpdateVersionRules.IsUpdateRequired(
                    Application.version,
                    manifest.EffectiveMinVersion);
            }
            catch (Exception ex)
            {
                CheckFailed = true;
                LastError = ex.Message;
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
                SetDownloadHeaders(req);
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

        static async UniTask<string> GetTextAsync(string url, bool githubApi)
        {
            using var req = UnityWebRequest.Get(url);
            if (githubApi)
            {
                SetGitHubApiHeaders(req);
            }
            else
            {
                SetDownloadHeaders(req);
            }

            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"GET {url} failed: {req.responseCode} {req.error}");
            }

            return req.downloadHandler.text;
        }

        static void SetGitHubApiHeaders(UnityWebRequest req)
        {
            req.SetRequestHeader("User-Agent", "BARAKI-GameUpdate");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
        }

        static void SetDownloadHeaders(UnityWebRequest req)
        {
            // Asset URLs are CDN/browser downloads — do not send GitHub API Accept.
            req.SetRequestHeader("User-Agent", "BARAKI-GameUpdate");
            req.SetRequestHeader("Accept", "*/*");
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
