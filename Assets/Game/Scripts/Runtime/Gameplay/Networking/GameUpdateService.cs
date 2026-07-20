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
    /// Force-update via GitHub Release tags (no version.json / no API rate limits).
    /// Flow: GET /releases/latest → follow redirect → tag → compare → download BARAKI-{tag}.zip.
    /// Optional override: BARAKI_UPDATE_URL pointing at a version.json for local QA.
    /// </summary>
    public static class GameUpdateService
    {
        public const string ManifestUrlEnv = "BARAKI_UPDATE_URL";
        public const string GitHubRepoEnv = "BARAKI_GITHUB_REPO";

        public static GameUpdateManifest RemoteManifest { get; private set; }
        public static bool UpdateRequired { get; private set; }
        public static bool CheckCompleted { get; private set; }
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
                if (!string.IsNullOrWhiteSpace(overrideManifest))
                {
                    await RefreshFromManifestUrlAsync(overrideManifest.Trim());
                }
                else
                {
                    await RefreshFromLatestTagAsync();
                }
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

        public static async UniTask DownloadAndApplyAsync(IProgress<float> progress = null)
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
                req.redirectLimit = 8;
                var operation = req.SendWebRequest();
                while (!operation.isDone)
                {
                    progress?.Report(Mathf.Clamp01(req.downloadProgress));
                    await UniTask.Yield();
                }

                progress?.Report(1f);
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

        static async UniTask RefreshFromLatestTagAsync()
        {
            var latestPageUrl = ResolveLatestReleasePageUrl();
            var tag = await ResolveLatestTagAsync(latestPageUrl);
            var version = tag.TrimStart('v', 'V');
            var zipUrl = ResolveZipUrl(tag);

            RemoteManifest = new GameUpdateManifest
            {
                version = version,
                minVersion = version,
                url = zipUrl,
            };

            UpdateRequired = GameUpdateVersionRules.IsUpdateRequired(
                GameLocalVersion.Current,
                RemoteManifest.EffectiveMinVersion);
        }

        static async UniTask RefreshFromManifestUrlAsync(string manifestUrl)
        {
            var manifestJson = await GetTextAsync(manifestUrl);
            EnsureUsableBody(manifestJson, "version.json");

            if (!GameUpdateManifestParser.TryParse(manifestJson, out var manifest))
            {
                throw new InvalidOperationException("Invalid version.json from override URL.");
            }

            RemoteManifest = manifest;
            UpdateRequired = GameUpdateVersionRules.IsUpdateRequired(
                GameLocalVersion.Current,
                manifest.EffectiveMinVersion);
        }

        static async UniTask<string> ResolveLatestTagAsync(string latestPageUrl)
        {
            // Follow /releases/latest → /releases/tag/vX.Y.Z without using api.github.com.
            using var req = UnityWebRequest.Head(latestPageUrl);
            SetDownloadHeaders(req);
            req.redirectLimit = 8;
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Some hosts/Unity builds are flaky with HEAD — fall back to GET.
                return await ResolveLatestTagViaGetAsync(latestPageUrl);
            }

            if (GitHubReleaseUpdateRules.TryParseTagFromReleaseUrl(req.url, out var tag))
            {
                return tag;
            }

            return await ResolveLatestTagViaGetAsync(latestPageUrl);
        }

        static async UniTask<string> ResolveLatestTagViaGetAsync(string latestPageUrl)
        {
            using var req = UnityWebRequest.Get(latestPageUrl);
            SetDownloadHeaders(req);
            req.redirectLimit = 8;
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"GET {latestPageUrl} failed: {req.responseCode} {req.error}");
            }

            if (!GitHubReleaseUpdateRules.TryParseTagFromReleaseUrl(req.url, out var tag))
            {
                throw new InvalidOperationException(
                    "Could not parse release tag from GitHub latest URL: " + req.url);
            }

            return tag;
        }

        static string ResolveLatestReleasePageUrl()
        {
            var repo = Environment.GetEnvironmentVariable(GitHubRepoEnv);
            if (!string.IsNullOrWhiteSpace(repo))
            {
                var parts = repo.Trim().Split('/');
                if (parts.Length == 2)
                {
                    return GitHubReleaseUpdateRules.LatestReleasePageUrl(parts[0], parts[1]);
                }
            }

            return GitHubReleaseUpdateRules.LatestReleasePageUrl();
        }

        static string ResolveZipUrl(string tag)
        {
            var repo = Environment.GetEnvironmentVariable(GitHubRepoEnv);
            if (!string.IsNullOrWhiteSpace(repo))
            {
                var parts = repo.Trim().Split('/');
                if (parts.Length == 2)
                {
                    return GitHubReleaseUpdateRules.ReleaseZipDownloadUrl(tag, parts[0], parts[1]);
                }
            }

            return GitHubReleaseUpdateRules.ReleaseZipDownloadUrl(tag);
        }

        static void EnsureUsableBody(string body, string label)
        {
            if (GitHubReleaseUpdateRules.LooksLikeHtmlErrorPage(body))
            {
                throw new InvalidOperationException(
                    $"{label} download returned an HTML error page.");
            }

            if (GitHubReleaseUpdateRules.LooksLikeGitHubApiError(body))
            {
                throw new InvalidOperationException(
                    $"{label} download returned a GitHub API error (often rate limit).");
            }
        }

        static async UniTask<string> GetTextAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            SetDownloadHeaders(req);
            req.redirectLimit = 8;
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"GET {url} failed: {req.responseCode} {req.error}");
            }

            return req.downloadHandler.text;
        }

        static void SetDownloadHeaders(UnityWebRequest req)
        {
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
