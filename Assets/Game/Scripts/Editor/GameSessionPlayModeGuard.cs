#if UNITY_EDITOR
using Game.Core;
using Game.Gameplay.Networking;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Clears static session state when leaving Play Mode (Domain Reload off safe).
    /// </summary>
    [InitializeOnLoad]
    public static class GameSessionPlayModeGuard
    {
        static GameSessionPlayModeGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                GameSession.Reset();
                GameChatService.ResetSessionState();
            }
        }
    }
}
#endif
