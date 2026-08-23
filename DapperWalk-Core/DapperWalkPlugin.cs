using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DapperWalk.Core {
    public abstract class DapperWalkPlugin<T> : BasePlugin where T : MonoBehaviour {
        private const string WORLD_SCENE_NAME = "WorldScene";

        protected virtual void Init (Assembly pluginAssembly, string pluginGuid) {
            ClassRegistrationService classRegistrationService = new ();
            Assembly coreAssembly = Assembly.GetExecutingAssembly ();
            classRegistrationService.Register ( coreAssembly );
            classRegistrationService.Register ( pluginAssembly );

            ConfigService configService = Singleton.Summon<ConfigService> ( true );
            configService.Init ( Config );

            Harmony harmony = new ( pluginGuid );
            harmony.PatchAll ( coreAssembly );
            harmony.PatchAll ( pluginAssembly );

            SceneWatcher sceneWatcher = Singleton.Summon<SceneWatcher> ( true );
            sceneWatcher.ActiveSceneChanged += OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged ( Scene scene ) {
            if (scene.name == WORLD_SCENE_NAME) {
                Singleton.Summon<T> ();
            }
        }
    }
}
