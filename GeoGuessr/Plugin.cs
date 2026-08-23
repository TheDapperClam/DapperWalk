using BepInEx;
using DapperWalk.Core;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace DapperWalk.GeoGuessr;

[BepInPlugin ( MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION )]
public class Plugin : DapperWalkPlugin {
    private const string WORLD_SCENE_NAME = "WorldScene";

    public override void Load () {
        Init ( Assembly.GetExecutingAssembly (), MyPluginInfo.PLUGIN_GUID );

        SceneWatcher sceneWatcher = Singleton.Summon<SceneWatcher> ( true );
        sceneWatcher.ActiveSceneChanged += OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged ( Scene scene ) {
        if (scene.name == WORLD_SCENE_NAME) {
            Singleton.Summon<GeoGuessrController> ();
        }
    }
}
