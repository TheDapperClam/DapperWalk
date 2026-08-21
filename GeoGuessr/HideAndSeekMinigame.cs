using BepInEx.Configuration;
using BepInEx.Logging;
using DapperWalk.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class HideAndSeekMinigame : MonoBehaviour, IMinigame {
        public enum MinigameEndArgs {
            WinnerNetId = 0
        }

        public enum MinigameStartArgs {
            HiderNetId = 0,
            HideTime = 1,
            SeekTime = 2
        }

        public enum MinigameState {
            Stopped = 0,
            Hide,
            Seek
        }

        private const uint DEFAULT_HIDE_TIME = 60;
        private const uint DEFAULT_SEEK_TIME = 120;
        private const KeyCode DEFAULT_SPECTATE_KEY = KeyCode.LeftAlt;
        private const string UI_PARENT_NAME = "Will Fade With Ending";

        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( HideAndSeekMinigame ) );

        private ConfigService configService;
        private MinigameState currentMinigameState;
        private float hideEndTime;
        private ConfigEntry<uint> hideTimeConfigEntry;
        private PlayerCharacter hider;
        private bool isSpectating;
        private GameStartBlind gameStartBlind;
        private PlayerCharacter localPlayer;
        private NetworkFunctionRepository networkFunctionRepository;
        private NetworkService networkService;
        private PlayerCharacterService playerCharacterService;
        private float seekEndTime;
        private ConfigEntry<uint> seekTimeConfigEntry;
        private ConfigEntry<KeyCode> spectateKeyConfigEntry;
        private Vector3 startingCameraPosition;
        private Quaternion startingCameraRotation;
        private TextMeshProUGUI timerText;

        [HarmonyPrefix]
        [HarmonyPatch ( typeof ( PlayerActions ), "ActionPickUpPlayer" )]
        private static void ActionPickUpPlayerPatch (PlayerCharacter playerCharacter) {
            HideAndSeekMinigame geoGuessrMinigame = Singleton.Summon<HideAndSeekMinigame> ();
            geoGuessrMinigame.OnPlayerPickedUp ( playerCharacter );
        }

        public void StartMinigame () {
            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();
            if (currentMinigameState != MinigameState.Stopped || localPlayer == null || !localPlayer.isServer) {
                return;
            }

            PlayerCharacter[] pcs = playerCharacterService.GetPlayerCharacters ();
            int hiderIndex = Random.RandomRangeInt ( 0, pcs.Length );

            logger.LogDebug ( "Picking player to hide" );
            PlayerCharacter newHider;
            do {
                newHider = pcs[hiderIndex];
            } while (hider == newHider && pcs.Length > 1);

            networkService.Send ( localPlayer, networkFunctionRepository.GetFunctionName( RelayStartMinigame ), [newHider.netId.ToString (), hideTimeConfigEntry.Value.ToString (),
                seekTimeConfigEntry.Value.ToString ()] );
        }

        private void Awake () {
            configService = Singleton.Summon<ConfigService> ();
            networkFunctionRepository = Singleton.Summon<NetworkFunctionRepository> ();
            networkService = Singleton.Summon<NetworkService> ();
            playerCharacterService = Singleton.Summon<PlayerCharacterService> ();

            hideTimeConfigEntry = configService.Get ( "General", "HideTime", DEFAULT_HIDE_TIME );
            seekTimeConfigEntry = configService.Get ( "General", "SeekTime", DEFAULT_SEEK_TIME );
            spectateKeyConfigEntry = configService.Get ( "Input", "SpectateKey", DEFAULT_SPECTATE_KEY );

            networkFunctionRepository.Register ( RelayEndHide );
            networkFunctionRepository.Register ( RelayEndMinigame);
            networkFunctionRepository.Register ( RelayStartMinigame );
        }

        private void OnPlayerPickedUp ( PlayerCharacter playerCharacter ) {
            if (currentMinigameState != MinigameState.Seek || playerCharacter.netId != hider.netId) {
                return;
            }
            networkService.Send ( localPlayer, networkFunctionRepository.GetFunctionName ( RelayEndMinigame ), [localPlayer.netId.ToString ()] );
        }

        private void ProcessHideState () {
            if (currentMinigameState != MinigameState.Hide) {
                return;
            }

            float remainingTime = Mathf.Max ( hideEndTime - Time.time, 0f );

            SetTimerText ( remainingTime );
            if (remainingTime > 0f) {
                if (localPlayer != hider) {
                    gameStartBlind._transitionStartTime = Time.time;
                    gameStartBlind.gameObject.SetActive ( true );
                }
            } else if (localPlayer.isServer) {
                networkService.Send ( localPlayer, networkFunctionRepository.GetFunctionName ( RelayEndHide ) );
            }
        }

        private void ProcessSeekState () {
            if (currentMinigameState != MinigameState.Seek) {
                return;
            }

            float remainingTime = Mathf.Max ( seekEndTime - Time.time, 0f );

            SetTimerText ( remainingTime );
            if (remainingTime > 0f) {
                if (localPlayer != hider) {
                    if (Input.GetKeyDown ( spectateKeyConfigEntry.Value )) {
                        SpectateStart ();
                    } else if (Input.GetKeyUp ( spectateKeyConfigEntry.Value)) {
                        SpectateStop ();
                    }
                }
            } else if (localPlayer.isServer) {
                networkService.Send ( localPlayer, networkFunctionRepository.GetFunctionName ( RelayEndMinigame ), [hider.netId.ToString ()] );
            }
        }

        private bool RelayEndHide ( uint senderNetId, string[] args ) {
            if (currentMinigameState != MinigameState.Hide) {
                return false;
            }

            currentMinigameState = MinigameState.Seek;
            SetPlayerFrozen ( localPlayer == hider );
            logger.LogInfo ( "Hiding has ended" );
            return false;
        }

        private bool RelayEndMinigame ( uint senderNetId, string[] args ) {
            if (!uint.TryParse ( args[(int) MinigameEndArgs.WinnerNetId], out uint winnerNetId )) {
                return true;
            }
            if (currentMinigameState != MinigameState.Seek) {
                return false;
            }

            PlayerCharacter winner = playerCharacterService.GetPlayerCharacter ( winnerNetId );
            currentMinigameState = MinigameState.Stopped;
            SpectateStop ();
            SetPlayerFrozen ( false );
            logger.LogInfo ( $"Minigame has ended with winner: {winner.name}" );
            return false;
        }

        private bool RelayStartMinigame ( uint senderNetId, string[] args ) {
            if (!uint.TryParse ( args[(int) MinigameStartArgs.HiderNetId], out uint hiderNetId ) ||
                !float.TryParse ( args[(int) MinigameStartArgs.HideTime], out float hideTime) ||
                !float.TryParse(args[(int) MinigameStartArgs.SeekTime], out float huntTime)) {
                return true;
            }
            if (currentMinigameState != MinigameState.Stopped) {
                return false;
            }

            if (timerText == null) {
                RectTransform uiParent = GameObject.Find ( UI_PARENT_NAME ).GetComponent<RectTransform> ();
                GameObject timerTextObject = new ();
                timerTextObject.transform.parent = uiParent;
                timerText = timerTextObject.AddComponent<TextMeshProUGUI> ();

                RectTransform timerTextRectTransform = timerTextObject.GetComponent<RectTransform> ();
                timerTextRectTransform.anchorMin = Vector2.zero;
                timerTextRectTransform.anchorMax = Vector2.one;
                timerTextRectTransform.anchoredPosition = Vector2.down * 64f;
                timerText.alignment = TextAlignmentOptions.Center;
            }

            currentMinigameState = MinigameState.Hide;
            hideEndTime = Time.time + hideTime;
            seekEndTime = hideEndTime + huntTime;
            hider = playerCharacterService.GetPlayerCharacter ( hiderNetId );
            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();
            gameStartBlind ??= FindAnyObjectByType<GameStartBlind> (FindObjectsInactive.Include);

            SetPlayerFrozen ( localPlayer != hider );
            logger.LogInfo ( $"Minigame has started: {hider.gameObject.name}" );
            return false;
        }

        private void SetPlayerFrozen ( bool frozen ) {
            Rigidbody rb = localPlayer.GetComponent<Rigidbody> ();

            if (rb.isKinematic != frozen) {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = frozen;
            }
        }

        private void SetTimerText ( float time ) {
            timerText.text = Mathf.CeilToInt ( time ).ToString ();
            timerText.enabled = time > 0f;
        }

        private void SpectateStart () {
            if (isSpectating || localPlayer == hider) {
                return;
            }

            isSpectating = true;
            SetPlayerFrozen ( true );

            startingCameraPosition = Camera.main.transform.localPosition;
            startingCameraRotation = Camera.main.transform.localRotation;
            Camera.main.transform.position = hider.cameraTransform.position;
        }

        private void SpectateStop () {
            if (!isSpectating || localPlayer == null) {
                return;
            }

            isSpectating = false;
            SetPlayerFrozen ( false );

            Camera.main.transform.localPosition = startingCameraPosition;
            Camera.main.transform.localRotation = startingCameraRotation;
        }

        private void Update () { 
            if (currentMinigameState != MinigameState.Stopped && localPlayer == null) {
                // TODO: Can't start GeoGuessr after reaching this point (session was ended without restarting game)
                currentMinigameState = MinigameState.Stopped;
            }
            ProcessHideState ();
            ProcessSeekState ();
        }
    }
}
