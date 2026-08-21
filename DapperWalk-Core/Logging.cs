using BepInEx.Logging;

namespace DapperWalk.Core {
    public static class Logging {
        private static readonly Dictionary<Type, ManualLogSource> loggers = [];

        public static ManualLogSource CreateLogger (Type type) {
            if (loggers.ContainsKey ( type )) {
                return loggers[type];
            }

            ManualLogSource logger = new ( type.FullName );
            Logger.Sources.Add ( logger );
            loggers.Add ( type, logger );
            return logger;
        }
    }
}
