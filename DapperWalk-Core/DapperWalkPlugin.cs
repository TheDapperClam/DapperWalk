using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Reflection;

namespace DapperWalk.Core {
    public abstract class DapperWalkPlugin : BasePlugin {
        protected void Init (Assembly pluginAssembly, string pluginGuid) {
            ClassRegistrationService classRegistrationService = new ();
            Assembly coreAssembly = Assembly.GetExecutingAssembly ();
            classRegistrationService.Register ( coreAssembly );
            classRegistrationService.Register ( pluginAssembly );

            ConfigService configService = Singleton.Summon<ConfigService> ( true );
            configService.Init ( Config );

            Harmony harmony = new ( pluginGuid );
            harmony.PatchAll ( coreAssembly );
            harmony.PatchAll ( pluginAssembly );
        }
    }
}
