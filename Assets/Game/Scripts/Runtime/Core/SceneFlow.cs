using UnityEngine.SceneManagement;

namespace Game.Core
{
    public static class SceneFlow
    {
        public static bool IsLoaded(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }
    }
}
