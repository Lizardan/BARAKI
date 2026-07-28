using System.Text;

namespace Game.Core
{
    /// <summary>GitHub PAT XOR-embedded at CI/player build time (see GitHubPlaytestEmbedded.Data.cs).</summary>
    internal static partial class GitHubPlaytestEmbedded
    {
        public static bool TryGetToken(out string token)
        {
            token = null;
            if (Payload == null || Payload.Length == 0 || Key == null || Key.Length == 0)
            {
                return false;
            }

            var decoded = GitHubPlaytestStampRules.Xor(Payload, Key);
            var text = Encoding.UTF8.GetString(decoded);
            token = GitHubPlaytestStampRules.NormalizeToken(text);
            return token != null;
        }
    }
}
