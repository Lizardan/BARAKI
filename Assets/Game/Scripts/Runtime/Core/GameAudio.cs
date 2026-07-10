namespace Game.Core
{
    /// <summary>
    /// Global audio preferences applied to <see cref="UnityEngine.AudioListener"/>.
    /// </summary>
    public static class GameAudio
    {
        public static bool SoundEnabled { get; set; } = true;

        public static float Volume { get; set; } = 0.75f;

        public static void Apply()
        {
            UnityEngine.AudioListener.volume = SoundEnabled ? Volume : 0f;
        }
    }
}
