using BepInEx.Configuration;
using BepInEx.Logging;
using DapperWalk.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class GeoGuessrMinigame : DapperNetworker, IMinigame {
        public enum MinigameState {
            Stopped = 0,
            ChoosePoints,
            FindPoints,
            Reveal
        }

        private const KeyCode DEFAULT_CONFIRM_KEY = KeyCode.G;
        private const float DEFAULT_REVEAL_TIME = 10f;
        private const KeyCode DEFAULT_SPECTATE_KEY = KeyCode.LeftAlt;
        private const string TEXT_FONT_NAME = "Courier Prime Sans Bold SDF";

        private const string UI_PARENT_NAME = "Will Fade With Ending";

        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( GeoGuessrMinigame ) );
        private static readonly ColorPicker colorPicker = new ( [
            Color.red,
            Color.violet,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.orange,
            Color.violetRed,
            Color.blueViolet,
            Color.cyan,
            Color.yellowGreen,
            Color.yellowNice,
            Color.orangeRed
        ] );

        public int MinimumPlayerCount { get; set; } = 2;

        private ConfigService configService;
        private ConfigEntry<KeyCode> confirmKeyConfigEntry;
        private Confetti confetti;
        private MinigameState currentMinigameState;
        private GeoGuessrPoint currentPlayerPoint;
        private Dictionary<uint, GeoGuessrPoint> findPlayerPointsDictionary = [];
        private Dictionary<uint, GeoGuessrPoint> guessedPlayerPointsDictionary = [];
        private float hideMinigameTextTime = float.MaxValue;
        private bool isSpectating;
        private PlayerCharacter localPlayer;
        private TextMeshProUGUI minigameText;
        private NetworkService networkService;
        private int participatingPlayerCount;
        private Dictionary<uint, Beacon> playerBeacons = [];
        private PlayerCharacterService playerCharacterService;
        private Dictionary<uint, Color> playerColors = [];
        private Dictionary<uint, ScoreEntry> playerScores = [];
        private Dictionary<uint, System.Numerics.Vector3> playerStartingPoints = [];
        private Queue<uint> remainingPlayersQueue = [];
        private float revealEndTime;
        private ConfigEntry<float> revealTimeConfigEntry;
        private List<Renderer> savedPlayerRenderers = [];
        private bool skipConfirmInput;
        private ConfigEntry<KeyCode> spectateKeyConfigEntry;
        private Vector3 startingCameraPosition;
        private Quaternion startingCameraRotation;

        public void StartMinigame () {
            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();
            if (currentMinigameState != MinigameState.Stopped || localPlayer == null) {
                return;
            }
            if (!localPlayer.isServer) {
                SetMinigameText ( "Only the host can start GeoGuessr", 5f );
                return;
            }

            PlayerCharacter[] connectedPlayers = playerCharacterService.GetPlayerCharacters ();

            if (connectedPlayers.Length < MinimumPlayerCount) {
                SetMinigameText ( $"There are currently {connectedPlayers.Length} player(s), at least {MinimumPlayerCount} players are required to play GeoGuessr", 5f );
                return;
            }

            List<PlayerCharacter> pcs = networkService.Send ( NetRelayChoosePoints ).Successful;
            participatingPlayerCount = pcs.Count;

            logger.LogInfo ( $"Starting GeoGuessr with {participatingPlayerCount} players" );

            // Remove missing player scores
            playerScores.Clear ();
            foreach (PlayerCharacter pc in pcs) {
                playerScores[pc.netId] = new ( pc.playerNetworking.moderationNameSanitized, pc.netId );
                playerStartingPoints[pc.netId] = pc.transform.position.ToNumerics ();
                remainingPlayersQueue.Enqueue ( pc.netId );
            }
        }

        protected override void Awake () {
            base.Awake ();

            configService = Singleton.Summon<ConfigService> ();
            networkService = Singleton.Summon<NetworkService> ();
            playerCharacterService = Singleton.Summon<PlayerCharacterService> ();

            confirmKeyConfigEntry = configService.Get ( "Input", "ConfirmKey", DEFAULT_CONFIRM_KEY );
            revealTimeConfigEntry = configService.Get ( "General", "RevealTime", DEFAULT_REVEAL_TIME );
            spectateKeyConfigEntry = configService.Get ( "Input", "SpectateKey", DEFAULT_SPECTATE_KEY );
        }

        private bool CanPlayerSetPoint ( uint callerNetId, uint targetNetId ) {
            PlayerCharacter[] pcs = playerCharacterService.GetPlayerCharacters ();

            return callerNetId == targetNetId &&
                (!isSpectating || callerNetId != localPlayer.netId) &&
                (currentMinigameState & (MinigameState.ChoosePoints | MinigameState.FindPoints)) != 0 &&
                (callerNetId != currentPlayerPoint.OwnerNetId || Mathf.Max ( pcs.Length, participatingPlayerCount ) < MinimumPlayerCount);
        }

        private void FindNextPlayerPoint () {
            if (!localPlayer.isServer || remainingPlayersQueue.Count <= 0 || findPlayerPointsDictionary.Count <= 0) {
                return;
            }
            networkService.Send ( NetRelayFindPlayerPoint, findPlayerPointsDictionary[remainingPlayersQueue.Dequeue ()] );
        }

        private string FormatScores ( Dictionary<uint, ScoreEntry> scores ) {
            StringBuilder sb = new ();
            int placement = 0;

            foreach (ScoreEntry entry in SortScores ( scores )) {
                placement++;
                sb.AppendLine ( $"{placement}. <color=#{ColorUtility.ToHtmlStringRGB ( GetPlayerColor ( entry.NetId ) )}>{entry.Name}</color>: {entry.Score.ToString ( "n2" )}" );
            }
            return sb.ToString ();
        }

        private Color GetPlayerColor ( uint netId ) {
            if (playerColors.ContainsKey ( netId )) {
                return playerColors[netId];
            }

            Color color = colorPicker.Pick ();
            playerColors.Add ( netId, color );
            return color;
        }

        private void HideBeacons () {
            foreach (var beaconPair in playerBeacons) {
                beaconPair.Value.gameObject.SetActive ( false );
            }
        }

        private bool IsMinigameOver () {
            return currentMinigameState != MinigameState.Stopped && remainingPlayersQueue.Count <= 0;
        }

        private void LateUpdate () {
            if (isSpectating) {
                Camera.main.transform.position = currentPlayerPoint.GetSpectatePosition ();
            }
        }

        [NetworkFunction ( payloadType: typeof ( void ) )]
        private bool NetRelayChoosePoints ( uint senderNetId, object payload ) {
            if (!ValidateServer ( senderNetId )) {
                return false;
            }

            logger.LogInfo ( "Time for players to choose their GeoGuessr points" );

            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();
            currentMinigameState = MinigameState.ChoosePoints;
            currentPlayerPoint = default;
            skipConfirmInput = true;
            findPlayerPointsDictionary.Clear ();
            guessedPlayerPointsDictionary.Clear ();
            SetMinigameText ( $"Set / Update Spot: {confirmKeyConfigEntry.Value}\nPick a spot for other players to try and find." );

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( void ) )]
        private bool NetRelayEndMinigame ( uint senderNetId, object payload ) {
            if (!ValidateServer ( senderNetId )) {
                return false;
            }

            logger.LogInfo ( "GeoGuessr has ended" );

            currentMinigameState = MinigameState.Stopped;
            SetMinigameText ( null );
            HideBeacons ();

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( GeoGuessrPoint ) )]
        private bool NetRelayFindPlayerPoint ( uint senderNetId, object payload ) {
            if (!ValidateServer ( senderNetId )) {
                return false;
            }

            logger.LogInfo ( $"Time to find {currentPlayerPoint.OwnerNetId}'s GeoGuessr point" );

            currentMinigameState = MinigameState.FindPoints;
            currentPlayerPoint = (GeoGuessrPoint) payload;
            skipConfirmInput = true;

            PlayerCharacter pc = playerCharacterService.GetPlayerCharacter ( currentPlayerPoint.OwnerNetId );
            StringBuilder sb = new ();
            sb.AppendLine ( $"View Spot: {spectateKeyConfigEntry.Value}" );
            if (currentPlayerPoint.OwnerNetId == localPlayer.netId) {
                sb.Append ( $"Everyone else is looking for your spot." );
            } else {
                sb.Append ( $"Set / Update Guess: {confirmKeyConfigEntry.Value}" );
                sb.Append ( $"Find <color=#{ColorUtility.ToHtmlStringRGB ( GetPlayerColor ( pc.netId ) )}>{pc.playerNetworking.moderationNameSanitized}'s</color> spot." );
            }

            HideBeacons ();
            SetMinigameText ( sb.ToString () );

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( Dictionary<uint, System.Numerics.Vector3> ) )]
        private bool NetRelayResetPlayer ( uint senderNetId, object payload ) {
            if (!ValidateServer ( senderNetId )) {
                return false;
            }

            logger.LogInfo ( "Resetting players" );

            var startingPositions = (Dictionary<uint, System.Numerics.Vector3>) payload;

            foreach (var playerStartingPosition in startingPositions) {
                uint netId = playerStartingPosition.Key;

                if (netId == localPlayer.netId) {
                    Vector3 position = playerStartingPosition.Value.ToUnity ();
                    localPlayer.rb.linearVelocity = Vector3.zero;
                    localPlayer.rb.angularVelocity = Vector3.zero;
                    localPlayer.rb.position = position;
                    break;
                }
            }
            return false;
        }

        [NetworkFunction ( payloadType: typeof ( RevealData ) )]
        private bool NetRelayReveal ( uint senderNetId, object payload ) {
            if (!ValidateServer ( senderNetId )) {
                return false;
            }
            logger.LogInfo ( "Revealing point" );

            RevealData data = (RevealData) payload;

            foreach (var guessedPointPair in data.GuessedPoints) {
                uint netId = guessedPointPair.Key;
                Vector3 position = guessedPointPair.Value.Position.ToUnity ();
                PlaceBeacon ( netId, position, Vector3.one * 0.5f );
            }
            PlaceBeacon ( data.TargetPoint.OwnerNetId, data.TargetPoint.Position.ToUnity (), Vector3.one * 1.5f );

            SpectateStop ();
            SetMinigameText ( $"{(data.WasLastRound ? "Final Results" : "Round Results")}\n{data.Score}" );
            currentMinigameState = MinigameState.Reveal;
            revealEndTime = Time.time + revealTimeConfigEntry.Value;

            if (data.WasLastRound) {
                PlayerCharacter winner = playerCharacterService.GetPlayerCharacter ( data.WinnerNetId );
                PlayConfetti ( winner.cameraTransform.position );
            }

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( GeoGuessrPoint ) )]
        private bool NetRelaySetPlayerGeoGuessrPoint ( uint senderNetId, object payload ) {
            if (!localPlayer.isServer) {
                return false;
            }

            GeoGuessrPoint point = (GeoGuessrPoint) payload;

            if (CanPlayerSetPoint ( senderNetId, point.OwnerNetId )) {
                PlayerCharacter pc = playerCharacterService.GetPlayerCharacter ( senderNetId );

                logger.LogDebug ( $"Setting {pc.playerNetworking.moderationNameSanitized}'s GeoGuessr point: {point.Position}" );

                switch (currentMinigameState) {
                    case MinigameState.ChoosePoints:
                        findPlayerPointsDictionary[senderNetId] = point;

                        if (findPlayerPointsDictionary.Count >= participatingPlayerCount) {
                            networkService.Send ( NetRelayResetPlayer, playerStartingPoints );
                            FindNextPlayerPoint ();
                        }
                        break;
                    case MinigameState.FindPoints:
                        guessedPlayerPointsDictionary[senderNetId] = point;

                        if (guessedPlayerPointsDictionary.Count >= participatingPlayerCount - 1) {
                            foreach (var guess in guessedPlayerPointsDictionary) {
                                uint netId = guess.Key;
                                Vector3 position = guess.Value.GetPosition ();
                                float dist = Vector3.Distance ( position, currentPlayerPoint.GetPosition () );
                                float addedScore = dist;
                                ScoreEntry updatedScore = playerScores[netId];
                                updatedScore.Score += addedScore;
                                playerScores[netId] = updatedScore;

                                logger.LogDebug ( $"{netId}'s distance from point was: {dist}" );
                            }
                            guessedPlayerPointsDictionary.Clear ();

                            bool wasLastRound = IsMinigameOver ();
                            uint winnerNetId = SortScores ( playerScores ).First ().NetId;
                            RevealData data = new ( guessedPlayerPointsDictionary, FormatScores ( playerScores ), currentPlayerPoint, wasLastRound, winnerNetId );
                            networkService.Send ( NetRelayReveal, data );
                        }
                        break;
                }
            }
            return false;
        }

        private Beacon PlaceBeacon ( uint netId, Vector3 position, Vector3 scale ) {
            Beacon beacon;

            if (playerBeacons.ContainsKey ( netId )) {
                beacon = playerBeacons[netId];
            } else {
                GameObject beaconObj = new ( "GeoGuessr Beacon" );
                beacon = beaconObj.AddComponent<Beacon> ();
                beacon.Color = GetPlayerColor ( netId );
                playerBeacons[netId] = beacon;
            }

            beacon.transform.position = position;
            beacon.transform.localScale = scale;
            beacon.gameObject.SetActive ( true );

            return beacon;
        }

        private void PlayConfetti ( Vector3 position ) {
            if (confetti == null) {
                GameObject confettiObj = new ( "GeoGuessr Confetti" );
                confetti = confettiObj.AddComponent<Confetti> ();
            }
            confetti.transform.position = position;
            confetti.Play ();
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

        private void SetPlayerPoint () {
            if (!CanPlayerSetPoint ( localPlayer.netId, localPlayer.netId )) {
                return;
            }

            GeoGuessrPoint point = new ( localPlayer );
            PlaceBeacon ( localPlayer.netId, point.GetPosition (), Vector3.one );

            logger.LogDebug ( $"Sending GeoGuessr point to server: {point.GetPosition ()}" );
            networkService.Send ( NetRelaySetPlayerGeoGuessrPoint, point );
        }

        private void SetMinigameText ( string text, float hideAfterSeconds = 0f ) {
            if (minigameText == null) {
                GameObject uiParent = GameObject.Find ( UI_PARENT_NAME );
                GameObject textObject = new ( "GeoGuessr Text" );
                textObject.transform.parent = uiParent.transform;

                TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TMP_FontAsset> ().First ( f => f.name == TEXT_FONT_NAME );
                minigameText = textObject.AddComponent<TextMeshProUGUI> ();
                minigameText.alignment = TextAlignmentOptions.BottomLeft;
                minigameText.font = font;

                RectTransform rectTransform = minigameText.GetComponent<RectTransform> ();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.anchoredPosition = new Vector2 ( 150f, 50f );
            }
            minigameText.text = text;
            minigameText.enabled = !string.IsNullOrEmpty ( text );
            hideMinigameTextTime = hideAfterSeconds <= 0f ? float.MaxValue : Time.time + hideAfterSeconds;
        }

        private void SpectateStart () {
            if (isSpectating || localPlayer == null) {
                return;
            }

            isSpectating = true;
            startingCameraPosition = Camera.main.transform.localPosition;
            startingCameraRotation = Camera.main.transform.localRotation;

            if (currentPlayerPoint.OwnerNetId != localPlayer.netId) {
                SetAllPlayerVisibility ( false );
                SetPlayerDeafen ( true );
            }
        }

        private void SpectateStop () {
            if (!isSpectating || localPlayer == null) {
                return;
            }

            isSpectating = false;
            Camera.main.transform.localPosition = startingCameraPosition;
            Camera.main.transform.localRotation = startingCameraRotation;
            SetAllPlayerVisibility ( true );
            SetPlayerDeafen ( false );
        }

        private ScoreEntry[] SortScores ( Dictionary<uint, ScoreEntry> scores ) {
            return [.. scores.Values.OrderBy ( e => e.Score )];
        }

        private void Update () {
            if (Time.time >= hideMinigameTextTime) {
                SetMinigameText ( null );
            }
            if (Input.GetKeyDown ( confirmKeyConfigEntry.Value ) && Cursor.lockState == CursorLockMode.Locked && !skipConfirmInput) {
                SetPlayerPoint ();
            }
            switch (currentMinigameState) {
                case MinigameState.FindPoints:
                    if (Input.GetKeyDown ( spectateKeyConfigEntry.Value ) && Cursor.lockState == CursorLockMode.Locked) {
                        SpectateStart ();
                    } else if (Input.GetKeyUp ( spectateKeyConfigEntry.Value ) || Cursor.lockState != CursorLockMode.Locked) {
                        SpectateStop ();
                    }
                    break;
                case MinigameState.Reveal:
                    if (!localPlayer.isServer || Time.time < revealEndTime) {
                        break;
                    }
                    if (IsMinigameOver ()) {
                        networkService.Send ( NetRelayEndMinigame );
                    } else {
                        FindNextPlayerPoint ();
                    }
                    break;
            }
            skipConfirmInput = false;
        }

        private bool ValidateServer ( uint netId ) {
            // TODO: Server validation doesn't work
            return true;
            //PlayerCharacter pc = playerCharacterService.GetPlayerCharacter ( netId );
            //return pc.isServer;
        }
    }
}
