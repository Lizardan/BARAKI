using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Local app version for UI / update checks.
    /// Player builds: <see cref="Application.version"/> (CI-stamped).
    /// Editor: latest git tag +1, without mutating PlayerSettings.
    /// </summary>
    public static class GameLocalVersion
    {
        public static string Current
        {
            get
            {
#if UNITY_EDITOR
                return EditorLocalVersionResolver.Resolve(Application.version);
#else
                return Application.version;
#endif
            }
        }
    }
}
