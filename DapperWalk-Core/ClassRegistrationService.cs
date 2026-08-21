using BepInEx.Logging;
using Il2CppInterop.Runtime.Injection;
using System.Reflection;
using UnityEngine;

namespace DapperWalk.Core {
    public class ClassRegistrationService {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( ClassRegistrationService ) );

        public void Register (Assembly assembly) {
            List<Type> types = [.. assembly.GetTypes ().Where ( t => t.IsAssignableTo ( typeof ( MonoBehaviour ) ) )];

            foreach (Type t in types) {
                logger.LogDebug ( $"Registering type: {t.Name}" );
                ClassInjector.RegisterTypeInIl2Cpp ( t );
            }
        }
    }
}
