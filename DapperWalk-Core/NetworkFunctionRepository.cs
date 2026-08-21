using BepInEx.Logging;
using UnityEngine;

namespace DapperWalk.Core {
    public class NetworkFunctionRepository : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( NetworkFunctionRepository ) );

        [Obsolete ( "Deprecated in favor of using single payload string instead of args array" )]
        private readonly Dictionary<string, Func<uint, string[], bool>> networkFunctions = [];
        private readonly Dictionary<string, Func<uint, object?, bool>> registeredNetworkFunctions = [];

        [Obsolete ( "Deprecated in favor of using single payload string instead of args array" )]
        public string GetFunctionName ( Func<uint, string[], bool> function ) {
            string name = function.Target.GetType ().Name + "." + function.Method.Name;
            return name;
        }

        public Func<uint, object?, bool>? Get ( string function ) {
            if (!registeredNetworkFunctions.ContainsKey ( function )) {
                return null;
            }
            return registeredNetworkFunctions[function];
        }

        [Obsolete("Deprecated in favor of using single payload string instead of args array")]
        public bool Process ( uint senderNetId, string function, string[] args ) {
            if (!networkFunctions.ContainsKey ( function )) {
                return true;
            }
            return networkFunctions[function].Invoke ( senderNetId, args );
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

        [Obsolete ( "Deprecated in favor of using single payload string instead of args array" )]
        public void Register (Func<uint, string[], bool> function) {
            string name = GetFunctionName ( function );

            logger.LogDebug ( $"Registering network function: {name}" );
            networkFunctions[name] = function;
        }

        private string GetFunctionKey ( Func<uint, object?, bool> function ) {
            return function.Method.Name;
        }
    }
}
