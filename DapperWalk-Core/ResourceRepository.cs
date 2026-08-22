using BepInEx.Logging;
using UnityEngine;

namespace DapperWalk.Core {
    public class ResourceRepository : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( ResourceRepository ) );

        private readonly Dictionary<string, UnityEngine.Object> loadedResources = [];

        public T Get<T> ( string name ) where T : UnityEngine.Object {
            if (loadedResources.ContainsKey ( name )) {
                return (T) loadedResources[name];
            }
            logger.LogDebug ( $"Loading resource: {name}" );

            T resource = Resources.FindObjectsOfTypeAll<T> ().First ( r => r.name == name ) ?? throw new NullReferenceException ( $"Resource not found: {name}" );
            loadedResources.Add ( name, resource );
            return resource;
        }
    }
}
