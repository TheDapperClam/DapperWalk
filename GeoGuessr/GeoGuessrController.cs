using BepInEx.Configuration;
using DapperWalk.Core;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class GeoGuessrController : MonoBehaviour {
        private const KeyCode DEFAULT_START_HUNT_KEY = KeyCode.G;

        private ConfigService configService;
        private IMinigame minigame;
        private ConfigEntry<KeyCode> startMinigameKeyConfig;

        private void Awake () {
            configService = Singleton.Summon<ConfigService> ();
            minigame = (IMinigame) Singleton.Summon<GeoGuessrMinigame> ();

            startMinigameKeyConfig = configService.Get ( "Input", "StartMinigame", DEFAULT_START_HUNT_KEY );
        }

        private void Update () {
            if (Input.GetKeyDown ( startMinigameKeyConfig.Value ) && Cursor.lockState == CursorLockMode.Locked) {
                minigame.StartMinigame ();
            }
        }
    }
}
