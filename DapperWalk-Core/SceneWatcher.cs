using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DapperWalk.Core {
    public class SceneWatcher : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( SceneWatcher ) );

        public delegate void ActiveSceneChangedEventHandler (Scene scene);
        public event ActiveSceneChangedEventHandler? ActiveSceneChanged;

        private Scene previousScene;

        private void Update () {
            Scene currentScene = SceneManager.GetActiveScene ();
            bool sceneChanged = currentScene.name != previousScene.name;
            previousScene = currentScene;

            if (sceneChanged) {
                logger.LogInfo ( $"Active scene changed: {currentScene.name}" );
                ActiveSceneChanged?.Invoke ( currentScene );
            }
        }
    }
}
