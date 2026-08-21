using BepInEx.Logging;
using HarmonyLib;
using System.Text.Json;
using UnityEngine;

namespace DapperWalk.Core {
    [HarmonyPatch]
    public class NetworkService : MonoBehaviour {
        public enum MessageArtifacts {
            Prefix = 0,
            Function = 1,
            Payload = 2
        }

        private const string MESSAGE_PREFIX = "/NetMsg";
        private const int MESSAGE_MAX_ARTIFACTS = 3;
        private const char MESSAGE_SEPARATOR = ' ';

        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( NetworkService ) );
        private static readonly JsonSerializerOptions jsonOptions = new () {
            IncludeFields = true
        };

        private NetworkFunctionRepository networkFunctionRepository;
        private PlayerCharacterService playerCharacterService;

        [HarmonyPrefix]
        [HarmonyPatch ( typeof ( TextChatSource ), "AddMessage" )]
        private static bool MessageReceived ( ref TextChatMessage message ) {
            NetworkService networkService = Singleton.Summon<NetworkService> ();
            return networkService.ProcessMessage ( message );
        }

        public NetworkSendResult Send ( Func<uint, object?, bool> function, object? payload = null ) {
            string? json = payload != null ? JsonSerializer.Serialize ( payload, jsonOptions ) : null;
            string message = $"{MESSAGE_PREFIX + MESSAGE_SEPARATOR + function.Method.Name + MESSAGE_SEPARATOR + json}";
            NetworkSendResult result = new ();

            logger.LogDebug ( $"Sending net message: {message}" );
            foreach (PlayerCharacter pc in playerCharacterService.GetPlayerCharacters ()) {
                pc.texter.CompleteInput ( message, out bool success );
                if (success) {
                    logger.LogDebug ( $"Successfully sent message to player: {pc.playerNetworking.moderationNameSanitized}" );
                    result.Successful.Add ( pc );
                } else {
                    logger.LogDebug ( $"Failed to send message to player: {pc.playerNetworking.moderationNameSanitized}" );
                    result.Failed.Add ( pc );
                }
            }
            return result;
        }

        [Obsolete ( "Deprecated in favor of not having caller sending the player character" )]
        public bool Send ( PlayerCharacter pc, Func<uint, object?, bool> function, object? payload = null ) {
            string? json = payload != null ? JsonSerializer.Serialize ( payload, jsonOptions ) : null;
            string message = $"{MESSAGE_PREFIX + MESSAGE_SEPARATOR + function.Method.Name + MESSAGE_SEPARATOR + json}";

            logger.LogDebug ($"Sending net message: {message}");
            pc.texter.CompleteInput ( message, out bool success );

            return success;
        }

        [Obsolete ( "Deprecated in favor of using single payload string instead of args array" )]
        public bool Send ( PlayerCharacter pc, string function, string[] args = null) {
            string message = $"{MESSAGE_PREFIX + MESSAGE_SEPARATOR + function}" +
                (args != null && args.Length > 0 ? $"{MESSAGE_SEPARATOR + string.Join ( MESSAGE_SEPARATOR, args )}" : "");
            pc.texter.CompleteInput ( message, out bool success );

            return success;
        }

        private void Awake () {
            networkFunctionRepository = Singleton.Summon<NetworkFunctionRepository> ();
            playerCharacterService = Singleton.Summon<PlayerCharacterService> ();
        }

        private bool ProcessMessage ( TextChatMessage message ) {
            string[] artifacts = message.message.Split ( MESSAGE_SEPARATOR, MESSAGE_MAX_ARTIFACTS, StringSplitOptions.RemoveEmptyEntries );

            if (artifacts.Length < (int) MessageArtifacts.Payload || artifacts[(int) MessageArtifacts.Prefix] != MESSAGE_PREFIX) {
                return true;
            }

            logger.LogDebug ( $"Processing net message artifacts: {string.Join ( ',', artifacts )}" );

            string functionKey = artifacts[(int) MessageArtifacts.Function];
            var registeredFunction = networkFunctionRepository.Get ( functionKey );

            if (registeredFunction == null) {
                logger.LogDebug ( $"Registered function not found: {functionKey}" );
                return true;
            }

            string? payload = artifacts.Length > (int) MessageArtifacts.Payload ? artifacts[(int) MessageArtifacts.Payload] : null;
            object[] attributes = registeredFunction.Method.GetCustomAttributes ( typeof ( NetworkFunction ), true );
            object? deserializedPayload = null;

            if (attributes.Length > 0 && payload != null) {
                NetworkFunction netFuncAttr = (NetworkFunction) attributes.First ();
                try {
                    deserializedPayload = JsonSerializer.Deserialize ( payload, netFuncAttr.PayloadType, jsonOptions );
                } catch {
                    logger.LogError ( $"Invalid payload received for network function: {functionKey}" );
                    return false;
                }
            }
            return registeredFunction.Invoke ( message.sendingPlayer.netId, deserializedPayload );
        }
    }
}
