using EscapeGame.Session;

namespace EscapeGame.Networking
{
    // Unity transport assembly is selected only at composition; the session adapter can be tested without Unity.
    internal sealed class HttpSessionGatewayFactory : ISessionGatewayFactory
    {
        private readonly int heartbeatRequestTimeoutMs;
        internal HttpSessionGatewayFactory(int heartbeatRequestTimeoutMs = 2000)
        {
            this.heartbeatRequestTimeoutMs = heartbeatRequestTimeoutMs;
        }
        public ISessionGateway Create(string address) => new HttpSessionGateway(
            new SessionApiClient(new ProtobufGameApiClient(address, heartbeatRequestTimeoutMs)));
    }
}
