using BepInEx.Logging;
using UnityEngine;

namespace DapperWalk.Core {
    public static class Singleton {
        private const string DONT_DESTROY_ON_LOAD_SCENE = "DontDestroyOnLoad";

        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( Singleton ) );
        private static readonly Dictionary<Type, MonoBehaviour> singletons = [];

        public static T Summon<T> (bool persistent = false) where T : MonoBehaviour {
            Type type = typeof ( T );
            MonoBehaviour instance;

            if (singletons.ContainsKey ( type ) && singletons[type] != null) {
                instance = (T) singletons[typeof ( T )];
            } else {
                logger.LogDebug ( $"Creating singleton: {type.Name}" );

                GameObject go = new ( type.Name );
                instance = go.AddComponent<T> ();
                singletons[type] = instance;
            }

            if (persistent && instance.gameObject.scene.name != DONT_DESTROY_ON_LOAD_SCENE) {
                logger.LogDebug ($"Setting singleton to persistent: {type.Name}");
                GameObject.DontDestroyOnLoad ( instance );
            }
            
            return (T) instance;
        }
    }
}
