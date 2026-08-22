using BepInEx.Configuration;
using BepInEx.Logging;
using DapperWalk.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace DapperWalk.GeoGuessr {
    public class GeoGuessrMinigame : DapperNetworker, IMinigame {
        public enum MinigameState {
            Stopped = 0,
            Start = 1,
            ChoosePoints = 2,
            FindPoints = 4,
            Reveal = 8
        }

        private const float DEFAULT_CHOOSE_POINT_TIME = 300f;
        private const KeyCode DEFAULT_CONFIRM_KEY = KeyCode.G;
        private const float DEFAULT_FIND_POINT_TIME = 360f;
        private const float DEFAULT_REVEAL_TIME = 15f;
        private const KeyCode DEFAULT_SPECTATE_KEY = KeyCode.LeftAlt;
        private const float START_TIME = 10f;

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

        private ConfigEntry<float> choosePointTimeConfigEntry;
        private ConfigService configService;
        private ConfigEntry<KeyCode> confirmKeyConfigEntry;
        private Confetti confetti;
        private MinigameState currentMinigameState;
        private Dictionary<uint, GeoGuessrPoint> findPlayerPointsDictionary = [];
        private ConfigEntry<float> findPointTimeConfigEntry;
        private Dictionary<uint, GeoGuessrPoint> guessedPlayerPointsDictionary = [];
        private float hideMinigameTextTime = float.MaxValue;
        private PlayerCharacter localPlayer;
        private MinigameContext minigameContext;
        private HudText minigameStatusText;
        private HudTimer minigameTimer;
        private NetworkService networkService;
        private int participatingPlayerCount;
        private Dictionary<uint, Beacon> playerBeacons = [];
        private PlayerCharacterService playerCharacterService;
        private Dictionary<uint, Color> playerColors = [];
        private Dictionary<uint, ScoreEntry> playerScores = [];
        private Dictionary<uint, System.Numerics.Vector3> playerStartingPoints = [];
        private Queue<uint> remainingPlayersQueue = [];
        private ConfigEntry<float> revealTimeConfigEntry;
        private bool skipConfirmInput;
        private ConfigEntry<KeyCode> spectateKeyConfigEntry;
        private Spectator spectator;

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

            StartData data = new ( START_TIME );
            List<PlayerCharacter> pcs = networkService.Send ( NetRelayStartMinigame, data ).Successful;
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
            minigameContext = Singleton.Summon<MinigameContext> ();
            networkService = Singleton.Summon<NetworkService> ();
            playerCharacterService = Singleton.Summon<PlayerCharacterService> ();
            spectator = Singleton.Summon<Spectator> ();

            choosePointTimeConfigEntry = configService.Get ( "General", "ChooseSpotTime", DEFAULT_CHOOSE_POINT_TIME );
            confirmKeyConfigEntry = configService.Get ( "Input", "ConfirmKey", DEFAULT_CONFIRM_KEY );
            findPointTimeConfigEntry = configService.Get ( "General", "FindSpotTime", DEFAULT_FIND_POINT_TIME );
            revealTimeConfigEntry = configService.Get ( "General", "RevealTime", DEFAULT_REVEAL_TIME );
            spectateKeyConfigEntry = configService.Get ( "Input", "SpectateKey", DEFAULT_SPECTATE_KEY );
        }

        private bool CanPlayerSetPoint ( uint callerNetId, uint targetNetId ) {
            PlayerCharacter[] pcs = playerCharacterService.GetPlayerCharacters ();

            return callerNetId == targetNetId &&
                (currentMinigameState & (MinigameState.ChoosePoints | MinigameState.FindPoints)) != 0 &&
                (callerNetId != minigameContext.PointToFind.OwnerNetId || Mathf.Max ( pcs.Length, participatingPlayerCount ) < MinimumPlayerCount);
        }

        private void ChoosePoints () {
            if (!localPlayer.isServer) {
                return;
            }

            ChoosePointData data = new ( choosePointTimeConfigEntry.Value );
            networkService.Send ( NetRelayChoosePoints, data );
        }

        private void EndReveal () {
            if (!localPlayer.isServer) {
                return;
            }
            if (IsMinigameOver ()) {
                networkService.Send ( NetRelayEndMinigame );
            } else {
                FindNextPlayerPoint ();
            }
        }

        private void FindNextPlayerPoint () {
            if (!localPlayer.isServer || remainingPlayersQueue.Count <= 0 || findPlayerPointsDictionary.Count <= 0) {
                return;
            }

            FindPointData data = new ( findPlayerPointsDictionary[remainingPlayersQueue.Dequeue ()], findPointTimeConfigEntry.Value );
            networkService.Send ( NetRelayFindPlayerPoint, data );
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

        private bool HasActiveBeacon () {
            return localPlayer != null &&
                playerBeacons.ContainsKey ( localPlayer.netId ) &&
                playerBeacons[localPlayer.netId].gameObject.activeSelf;
        }

        private void HideBeacons () {
            foreach (var beaconPair in playerBeacons) {
                beaconPair.Value.gameObject.SetActive ( false );
            }
        }

        private bool IsMinigameOver () {
            return currentMinigameState != MinigameState.Stopped && remainingPlayersQueue.Count <= 0;
        }

        [NetworkFunction ( payloadType: typeof ( ChoosePointData ) )]
        private bool NetRelayChoosePoints ( uint senderNetId, object payload ) {
            logger.LogInfo ( "Time for players to choose their GeoGuessr points" );

            ChoosePointData data = (ChoosePointData) payload;
            localPlayer ??= playerCharacterService.GetLocalPlayerCharacter ();
            currentMinigameState = MinigameState.ChoosePoints;
            minigameContext.Reset ();
            skipConfirmInput = true;
            findPlayerPointsDictionary.Clear ();
            guessedPlayerPointsDictionary.Clear ();

            SetMinigameText ( $"Set / Update Spot: {confirmKeyConfigEntry.Value}\nPick a spot for other players to try and find." );
            SetMinigameTimer ( data.Time, () => {
                if (!HasActiveBeacon ()) {
                    SetPlayerPoint ();
                }
            } );
            return false;
        }

        [NetworkFunction ( payloadType: typeof ( void ) )]
        private bool NetRelayEndMinigame ( uint senderNetId, object payload ) {
            logger.LogInfo ( "GeoGuessr has ended" );

            currentMinigameState = MinigameState.Stopped;
            SetMinigameText ( null );
            HideBeacons ();

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( FindPointData ) )]
        private bool NetRelayFindPlayerPoint ( uint senderNetId, object payload ) {
            FindPointData data = (FindPointData) payload;
            currentMinigameState = MinigameState.FindPoints;
            minigameContext.PointToFind = data.Point;
            skipConfirmInput = true;

            logger.LogInfo ( $"Time to find {minigameContext.PointToFind.OwnerNetId}'s GeoGuessr point" );

            PlayerCharacter pc = playerCharacterService.GetPlayerCharacter ( minigameContext.PointToFind.OwnerNetId );
            StringBuilder sb = new ();
            sb.AppendLine ( $"View Spot: {spectateKeyConfigEntry.Value}" );
            if (minigameContext.PointToFind.OwnerNetId == localPlayer.netId) {
                sb.Append ( $"Everyone else is looking for your spot." );
            } else {
                sb.Append ( $"Set / Update Guess: {confirmKeyConfigEntry.Value}" );
                sb.Append ( $"Find <color=#{ColorUtility.ToHtmlStringRGB ( GetPlayerColor ( pc.netId ) )}>{pc.playerNetworking.moderationNameSanitized}'s</color> spot." );
            }

            HideBeacons ();
            SetMinigameText ( sb.ToString () );
            SetMinigameTimer ( data.Time, () => {
                if (!HasActiveBeacon ()) {
                    SetPlayerPoint ();
                }
            } );

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( Dictionary<uint, System.Numerics.Vector3> ) )]
        private bool NetRelayResetPlayer ( uint senderNetId, object payload ) {
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
            logger.LogInfo ( "Revealing point" );

            RevealData data = (RevealData) payload;
            currentMinigameState = MinigameState.Reveal;

            foreach (var guessedPointPair in data.GuessedPoints) {
                uint netId = guessedPointPair.Key;
                Vector3 position = guessedPointPair.Value.Position.ToUnity ();
                PlaceBeacon ( netId, position, Vector3.one * 0.5f );
            }
            PlaceBeacon ( data.TargetPoint.OwnerNetId, data.TargetPoint.Position.ToUnity (), Vector3.one * 1.5f );

            spectator.SpectateStop ();
            SetMinigameText ( $"{(data.WasLastRound ? "Final Results" : "Round Results")}\n{data.Score}" );
            SetMinigameTimer ( data.Time, EndReveal );

            if (data.WasLastRound) {
                PlayerCharacter winner = playerCharacterService.GetPlayerCharacter ( data.WinnerNetId );
                PlayConfetti ( winner.cameraTransform.position );
            }

            return false;
        }

        [NetworkFunction ( payloadType: typeof ( GeoGuessrPoint ), receiverAuthority: NetAuthority.Server, senderAuthority: NetAuthority.Any )]
        private bool NetRelaySetPlayerGeoGuessrPoint ( uint senderNetId, object payload ) {
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
                                float dist = Vector3.Distance ( position, minigameContext.PointToFind.GetPosition () );
                                float addedScore = dist;
                                ScoreEntry updatedScore = playerScores[netId];
                                updatedScore.Score += addedScore;
                                playerScores[netId] = updatedScore;

                                logger.LogDebug ( $"{netId}'s distance from point was: {dist}" );
                            }
                            guessedPlayerPointsDictionary.Clear ();

                            bool wasLastRound = IsMinigameOver ();
                            uint winnerNetId = SortScores ( playerScores ).First ().NetId;
                            RevealData data = new ( guessedPlayerPointsDictionary, FormatScores ( playerScores ), minigameContext.PointToFind, revealTimeConfigEntry.Value,
                                wasLastRound, winnerNetId );
                            networkService.Send ( NetRelayReveal, data );
                        }
                        break;
                }
            }
            return false;
        }

        [NetworkFunction(payloadType: typeof(StartData))]
        private bool NetRelayStartMinigame (uint senderNetId, object payload) {
            StartData data = (StartData) payload;
            currentMinigameState = MinigameState.Start;

            SetMinigameText ("Starting GeoGuessr");
            SetMinigameTimer ( data.Time, ChoosePoints );
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
            if (minigameStatusText == null) {
                GameObject textObject = new ( "GeoGuessr Text" );
                minigameStatusText = textObject.AddComponent<HudText> ();
                minigameStatusText.Text.alignment = TextAlignmentOptions.BottomLeft;

                RectTransform rectTransform = minigameStatusText.GetComponent<RectTransform> ();
                rectTransform.anchoredPosition = new Vector2 ( 150f, 50f );
            }
            minigameStatusText.Text.text = text;
            minigameStatusText.gameObject.SetActive ( !string.IsNullOrEmpty ( text ) );
            hideMinigameTextTime = hideAfterSeconds <= 0f ? float.MaxValue : Time.time + hideAfterSeconds;
        }

        private void SetMinigameTimer ( float time, Action? callback = null ) {
            if (minigameTimer == null) {
                GameObject timerObject = new ("GeoGuessr Timer");
                minigameTimer = timerObject.AddComponent<HudTimer> ();
                minigameTimer.Text.alignment = TextAlignmentOptions.Center;

                RectTransform rectTransform = minigameTimer.GetComponent<RectTransform> ();
                rectTransform.anchoredPosition = new Vector2 ( 0f, -64f );
            }
            minigameTimer.Play ( time, callback );
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
                        spectator.SpectateStart ();
                    } else if (Input.GetKeyUp ( spectateKeyConfigEntry.Value ) || Cursor.lockState != CursorLockMode.Locked) {
                        spectator.SpectateStop ();
                    }
                    break;
            }
            skipConfirmInput = false;
        }
    }
}
