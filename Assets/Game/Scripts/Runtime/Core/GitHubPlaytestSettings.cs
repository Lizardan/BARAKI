using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(
        fileName = "GitHubPlaytestSettings",
        menuName = "Game/Debug/GitHub Playtest Settings")]
    public sealed class GitHubPlaytestSettings : ScriptableObject
    {
        private const string ResourcesPath = "Debug/GitHubPlaytestSettings";

        [SerializeField] private string _personalAccessToken = string.Empty;
        [SerializeField] private string _repository = GitHubPlaytestStampRules.DefaultRepository;

        public string PersonalAccessToken =>
            _personalAccessToken != null ? _personalAccessToken.Trim() : string.Empty;

        public string Repository
        {
            get
            {
                var repo = _repository != null ? _repository.Trim() : string.Empty;
                return string.IsNullOrEmpty(repo) ? GitHubPlaytestStampRules.DefaultRepository : repo;
            }
        }

        public static GitHubPlaytestSettings Load() =>
            Resources.Load<GitHubPlaytestSettings>(ResourcesPath);

        public static bool TryResolveCredentials(out string token, out string repository, out string source)
        {
            repository = GitHubPlaytestStampRules.DefaultRepository;

            if (GitHubPlaytestEmbedded.TryGetToken(out token))
            {
                var settings = Load();
                if (settings != null)
                {
                    repository = settings.Repository;
                }

                source = "Embedded";
                return true;
            }

            var local = Load();
            if (local != null && !string.IsNullOrWhiteSpace(local.PersonalAccessToken))
            {
                token = local.PersonalAccessToken;
                repository = local.Repository;
                source = "Resources";
                return GitHubPlaytestStampRules.NormalizeToken(token) != null;
            }

            token = string.Empty;
            source = local == null ? "missing-embedded-and-resources" : "empty-token";
            return false;
        }
    }
}
