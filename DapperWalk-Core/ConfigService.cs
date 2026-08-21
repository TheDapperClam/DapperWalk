using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace DapperWalk.Core {
    public class ConfigService : MonoBehaviour {
        private static readonly ManualLogSource logger = Logging.CreateLogger ( typeof ( ConfigService ) );

        private readonly Dictionary<string, ConfigEntryBase> configEntries = [];

        private ConfigFile? config;

        public ConfigEntry<T>? Get<T> ( string section, string setting, T defaultValue ) {
            string key = BuildKey ( section, setting );
            ConfigEntry<T>? entry = configEntries.ContainsKey ( key ) ? (ConfigEntry<T>) configEntries[key] : CreateEntry ( section, setting, defaultValue );

            if (entry != null) {
                logger.LogDebug ( $"Getting config entry {key}: {entry.Value}" );
            }
            return entry;
        }

        public void Init ( ConfigFile config ) {
            this.config = config;
 
        }

        private string BuildKey (string section, string setting) {
            return $"{section}.{setting}";
        }

        private ConfigEntry<T>? CreateEntry<T> (string section, string setting, T value) {
            if (config == null) {
                return null;
            }

            string key = BuildKey ( section, setting );
            ConfigEntry<T> entry = config.Bind ( section, setting, value );
            configEntries.Add ( key, entry );
            return entry;
        }
    }
}
