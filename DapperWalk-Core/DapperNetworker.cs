using BepInEx.Logging;
using System.Reflection;
using UnityEngine;

namespace DapperWalk.Core {
    public abstract class DapperNetworker : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( DapperNetworker ) );

        private NetworkFunctionRepository? networkFunctionRepository;

        protected virtual void Awake () {
            networkFunctionRepository = Singleton.Summon<NetworkFunctionRepository> ();
            Type type = GetType ();
            var methods = type.GetMethods (BindingFlags.Instance | BindingFlags.NonPublic).Where ( m => m.GetCustomAttributes ( typeof ( NetworkFunction ), true ).Length > 0 );

            logger.LogDebug ( $"Network functions found for {type.FullName}: {methods.Count ()}" );
            foreach (MethodInfo method in methods) {
                var func = (Func<uint, object?, bool>) Delegate.CreateDelegate ( typeof ( Func<uint, object?, bool> ), this, method );
                networkFunctionRepository.Register ( func );
            }
        }
    }
}
