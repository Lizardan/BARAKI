using UnityEngine;

namespace Game.Gameplay.Cameras
{
    /// <summary>
    /// Local preferred screen edge for the player's base. Used on match start
    /// and kept in sync with the in-match camera pad.
    /// </summary>
    public static class GameplayCameraPreferences
    {
        public const string PrefsKey = "baraki.camera.baseScreenEdge";

        static CameraBaseScreenEdge s_edge = CameraBaseScreenEdge.Bottom;
        static bool s_isLoaded;

        public static CameraBaseScreenEdge PreferredBaseScreenEdge
        {
            get
            {
                EnsureLoaded();
                return s_edge;
            }
            set
            {
                var edge = GameplayCameraSettings.ClampScreenEdge(value);
                EnsureLoaded();
                if (s_edge == edge)
                {
                    return;
                }

                s_edge = edge;
                PlayerPrefs.SetInt(PrefsKey, (int)edge);
                PlayerPrefs.Save();
            }
        }

        static void EnsureLoaded()
        {
            if (s_isLoaded)
            {
                return;
            }

            s_edge = GameplayCameraSettings.ClampScreenEdge(
                (CameraBaseScreenEdge)PlayerPrefs.GetInt(
                    PrefsKey,
                    (int)CameraBaseScreenEdge.Bottom));
            s_isLoaded = true;
        }
    }
}
