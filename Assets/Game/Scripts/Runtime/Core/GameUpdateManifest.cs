using System;

namespace Game.Core
{
    [Serializable]
    public sealed class GameUpdateManifest
    {
        public string version;
        public string minVersion;
        public string url;
        public string sha256;
        public string releasedAt;

        public string EffectiveMinVersion =>
            string.IsNullOrWhiteSpace(minVersion) ? version : minVersion;
    }

    public static class GameUpdateManifestParser
    {
        public static bool TryParse(string json, out GameUpdateManifest manifest)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                manifest = UnityEngine.JsonUtility.FromJson<GameUpdateManifest>(json);
                return manifest != null && !string.IsNullOrWhiteSpace(manifest.version);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
