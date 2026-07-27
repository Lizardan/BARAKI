using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Keeps the OS cursor inside the game window (<see cref="CursorLockMode.Confined"/>)
    /// on every non-Bootstrap scene. Re-applies after focus changes — Unity often clears
    /// <see cref="Cursor.lockState"/>.
    /// </summary>
    public sealed class GameCursorConfineDriver : MonoBehaviour
    {
        static GameCursorConfineDriver s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInstance()
        {
            if (s_instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(GameCursorConfineDriver));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<GameCursorConfineDriver>();
        }

        private void OnEnable()
        {
            Application.focusChanged += OnFocusChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyForActiveScene();
        }

        private void OnDisable()
        {
            Application.focusChanged -= OnFocusChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void LateUpdate()
        {
            // Unity / UI Toolkit can clear Confined; restore so the cursor cannot leave the window.
            ApplyForActiveScene();
        }

        void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyForActiveScene();
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyForScene(scene.name);

        static void ApplyForActiveScene() =>
            ApplyForScene(SceneManager.GetActiveScene().name);

        static void ApplyForScene(string sceneName)
        {
            Cursor.visible = true;
            var desired = GameDisplayRules.ResolveCursorLockMode(sceneName);
            if (Cursor.lockState != desired)
            {
                Cursor.lockState = desired;
            }
        }
    }
}
