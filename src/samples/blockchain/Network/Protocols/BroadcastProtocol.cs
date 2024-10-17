using System.Threading.Channels;
using System.Buffers;
using Nethermind.Libp2p.Core;

namespace Blockchain.Network.Protocols
{
    internal class BroadcastProtocol : IProtocol
    {
        private readonly ConsoleColor protocolConsoleColor = ConsoleColor.DarkBlue;
        private readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;
        private readonly Transport _transport;

        public string Id => "/broadcast/1.0.0";

        public BroadcastProtocol(Transport transport)
        {
            _transport = transport;
        }

        public async Task DialAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            // Initial listener exchange.
            WriteLineToConsole($"Connected to remote peer {context.RemotePeer.Address} as {context.LocalPeer.Address} as dialer.");
            Channel<byte[]> broadcastRequests = Channel.CreateUnbounded<byte[]>();
            EventHandler<byte[]> eventHandler = (object? sender, byte[] bytes) => broadcastRequests.Writer.TryWrite(bytes);

            try
            {
                _transport.MessageToBroadcast += eventHandler;
                await SendMessage(broadcastRequests, channel, context);
            }
            finally
            {
                _transport.MessageToBroadcast -= eventHandler;
            }
        }

        public async Task ListenAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            // Initial listener exchange.
            WriteLineToConsole($"Connected to remote peer {context.RemotePeer.Address} as {context.LocalPeer.Address} as listener.");
            await ReceiveMessage(channel, context);
        }

        private async Task ReceiveMessage(IChannel channel, IPeerContext context)
        {
            while(true)
            {
                ReadOnlySequence<byte> read = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                WriteLineToConsole($"Reaceived a message of length {read.Length} from {context.RemotePeer.Address}");
                _transport.ReceiveBroadcastMessage(context.RemotePeer.Address, read.ToArray());
            }
        }

        private async Task SendMessage(Channel<byte[]> broadcastRequests, IChannel channel, IPeerContext context)
        {
            while(true)
            {
                byte[] bytes = await broadcastRequests.Reader.ReadAsync();
                await channel.WriteAsync(new ReadOnlySequence<byte>(bytes));
                WriteLineToConsole($"Sent a message of length {bytes.Length} to {context.RemotePeer.Address}");
            }
        }

        private void WriteLineToConsole(string message)
        {
            Console.ForegroundColor = protocolConsoleColor;
            Console.WriteLine(message);
            Console.ForegroundColor = defaultConsoleColor;
        }
    }
}
