using BepInEx.Logging;
using UnityEngine;

namespace DapperWalk.Core {
    public class NetworkFunctionRepository : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( NetworkFunctionRepository ) );

        private readonly Dictionary<string, Func<uint, object?, bool>> registeredNetworkFunctions = [];

        public Func<uint, object?, bool>? Get ( string function ) {
            if (!registeredNetworkFunctions.ContainsKey ( function )) {
                return null;
            }
            return registeredNetworkFunctions[function];
        }

        public void Register ( Func<uint, object?, bool> function ) {
            logger.LogDebug ( $"Registering network function: {function.Method.Name}" );

            string key = GetFunctionKey ( function );
            if (key != null) {
                registeredNetworkFunctions.Add ( key, function );
            } else {
                logger.LogError ( $"Attempted to register function with null key: {function}" );
            }
        }

        private string GetFunctionKey ( Func<uint, object?, bool> function ) {
            return function.Method.Name;
        }
    }
}
