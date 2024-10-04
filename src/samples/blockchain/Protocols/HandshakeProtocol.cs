using Nethermind.Libp2p.Core;

namespace Blockchain.Protocols
{
    internal class HandshakeProtocol : IProtocol
    {
        private readonly ConsoleColor protocolConsoleColor = ConsoleColor.DarkYellow;
        private readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;
        private readonly ConsoleInterface _consoleInterface;

        public string Id => "/handshake/1.0.0";

        public HandshakeProtocol(ConsoleInterface consoleInterface)
        {
            _consoleInterface = consoleInterface;
        }

        public async Task DialAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            Console.WriteLine($"Connected to remote peer {context.RemotePeer.Address}.");
            Task broadcastProtocol = ((IRemotePeer)context.RemotePeer).DialAsync<BroadcastProtocol>();
            Task chatProtocol = ((IRemotePeer)context.RemotePeer).DialAsync<PingPongProtocol>();
            await Task.WhenAll(broadcastProtocol, chatProtocol);
        }

        public async Task ListenAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            Console.ForegroundColor = protocolConsoleColor;
            Console.WriteLine($"Remote peer {context.RemotePeer.Address} has connected.");
            Console.ForegroundColor = defaultConsoleColor;

            while (true)
            {
                await Task.Delay(1000);
            }
        }
    }
}
