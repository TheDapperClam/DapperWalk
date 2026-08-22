namespace DapperWalk.Core {
    public enum NetAuthority {
        Any,
        Server
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class NetworkFunction ( Type payloadType, NetAuthority receiverAuthority = NetAuthority.Any, NetAuthority senderAuthority = NetAuthority.Server ) : Attribute {
        public readonly Type PayloadType = payloadType;
        public readonly NetAuthority ReceiverAuthority = receiverAuthority;
        public readonly NetAuthority SenderAuthority = senderAuthority;
    }
}
