using BepInEx.Logging;
using DapperWalk.Core;
using System.Collections.Generic;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class Spectator : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( Spectator ) );

        private readonly List<Renderer> savedPlayerRenderers = [];

        public bool IsSpectating { get; private set; }

        private PlayerCharacter localPlayer;
        private MinigameContext minigameContext;
        private PlayerCharacterService playerCharacterService;
        private Vector3 savedCameraPosition;
        private Quaternion savedCameraRotation;

        public void SpectateStart () {
            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();

            if (IsSpectating || localPlayer == null) {
                return;
            }

            IsSpectating = true;
            savedCameraPosition = Camera.main.transform.localPosition;
            savedCameraRotation = Camera.main.transform.localRotation;

            if (minigameContext.PointToFind.OwnerNetId != localPlayer.netId) {
                SetAllPlayerVisibility ( false );
                SetPlayerDeafen ( true );
            }
        }

        public void SpectateStop () {
            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();

            if (!IsSpectating || localPlayer == null) {
                return;
            }

            IsSpectating = false;
            Camera.main.transform.localPosition = savedCameraPosition;
            Camera.main.transform.localRotation = savedCameraRotation;
            SetAllPlayerVisibility ( true );
            SetPlayerDeafen ( false );
        }

        private void Awake () {
            minigameContext = Singleton.Summon<MinigameContext> ();
            playerCharacterService = Singleton.Summon<PlayerCharacterService> ();
        }

        private void LateUpdate () {
            if (IsSpectating) {
                Camera.main.transform.position = minigameContext.PointToFind.GetSpectatePosition ();
            }
        }

        private void SetAllPlayerVisibility ( bool visible ) {
            logger.LogDebug ( $"Setting all player visibility: {visible}" );

            if (visible) {
                if (savedPlayerRenderers.Count > 0) {
                    foreach (Renderer renderer in savedPlayerRenderers) {
                        renderer.enabled = true;
                    }
                    savedPlayerRenderers.Clear ();
                }
            } else {
                savedPlayerRenderers.Clear ();
                foreach (PlayerCharacter pc in playerCharacterService.GetPlayerCharacters ()) {
                    Renderer[] renderers = pc.GetComponentsInChildren<Renderer> ( false );

                    foreach (Renderer renderer in renderers) {
                        if (renderer.enabled) {
                            savedPlayerRenderers.Add ( renderer );
                            renderer.enabled = false;
                        }
                    }
                }
            }
        }

        private void SetPlayerDeafen ( bool deaf ) {
            logger.LogDebug ( $"Setting player deafen: {deaf}" );
            FindAnyObjectByType<AudioListener> ().enabled = !deaf;
        }
    }
}
