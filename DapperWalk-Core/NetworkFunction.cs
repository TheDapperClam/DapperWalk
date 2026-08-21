namespace DapperWalk.Core {
    [AttributeUsage(AttributeTargets.Method)]
    public class NetworkFunction : Attribute {
        public readonly Type PayloadType;

        public NetworkFunction ( Type payloadType ) {
            PayloadType = payloadType;
        }
    }
}
