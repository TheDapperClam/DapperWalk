using BepInEx.Logging;
using UnityEngine;

namespace DapperWalk.Core {
    public static class Singleton {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( Singleton ) );
        private static readonly Dictionary<Type, MonoBehaviour> singletons = [];

        public static T Summon<T> () where T : MonoBehaviour {
            Type type = typeof ( T );

            if (singletons.ContainsKey ( type )) {
                return (T) singletons[typeof ( T )];
            }

            logger.LogDebug ( $"Creating singleton: {type.Name}" );

            GameObject go = new ( type.Name );
            MonoBehaviour instance = go.AddComponent<T> ();
            singletons.Add ( type, instance );
            GameObject.DontDestroyOnLoad ( go );

            return (T) instance;
        }
    }
}
