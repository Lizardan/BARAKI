using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Cached access to the main camera, avoiding per-frame Camera.main calls.
    /// Refreshed automatically when the cached camera is destroyed.
    /// </summary>
    public static class CameraCache
    {
        static Camera _main;

        public static Camera Main
        {
            get
            {
                if (_main != null) return _main;
                _main = Camera.main;
                return _main;
            }
        }

        public static void Clear() => _main = null;
    }
}
