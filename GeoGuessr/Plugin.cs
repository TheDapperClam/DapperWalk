using BepInEx;
using DapperWalk.Core;
using System.Reflection;

namespace DapperWalk.GeoGuessr;

[BepInPlugin ( MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION )]
public class Plugin : DapperWalkPlugin {
    public override void Load () {
        Init ( Assembly.GetExecutingAssembly (), MyPluginInfo.PLUGIN_GUID );
        Singleton.Summon<GeoGuessrController> ();
    }
}
