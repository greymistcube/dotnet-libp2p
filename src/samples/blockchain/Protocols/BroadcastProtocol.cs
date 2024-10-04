using System.Threading.Channels;
using System.Buffers;
using Nethermind.Libp2p.Core;

namespace Blockchain.Protocols
{
    internal class BroadcastProtocol : SymmetricProtocol, IProtocol
    {
        private readonly ConsoleColor protocolConsoleColor = ConsoleColor.DarkBlue;
        private readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;
        private readonly ConsoleInterface _consoleInterface;

        public string Id => "/broadcast/1.0.0";

        public BroadcastProtocol(ConsoleInterface consoleInterface)
        {
            _consoleInterface = consoleInterface;
        }

        protected override async Task ConnectAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context,
            bool isListener)
        {
            Channel<byte[]> broadcastRequests = Channel.CreateUnbounded<byte[]>();
            EventHandler<byte[]> eventHandler = (object? sender, byte[] bytes) => broadcastRequests.Writer.TryWrite(bytes);
            try
            {
                _consoleInterface.MessageToBroadcast += eventHandler;
                Task receiveTask = ReceiveMessage(channel, context);
                Task sendTask = SendMessage(broadcastRequests, channel, context);

                await Task.WhenAny(receiveTask, sendTask);
            }
            finally
            {
                _consoleInterface.MessageToBroadcast -= eventHandler;
            }
        }

        private async Task ReceiveMessage(IChannel channel, IPeerContext context)
        {
            while(true)
            {
                ReadOnlySequence<byte> read = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                Console.ForegroundColor = protocolConsoleColor;
                Console.WriteLine($"Reaceived a message of length {read.Length} from {context.RemotePeer.Address}");
                Console.ForegroundColor = defaultConsoleColor;
                await _consoleInterface.ReceiveBroadcastMessage(read.ToArray(), context);
            }
        }

        private async Task SendMessage(Channel<byte[]> broadcastRequests, IChannel channel, IPeerContext context)
        {
            while(true)
            {
                byte[] bytes = await broadcastRequests.Reader.ReadAsync();
                await channel.WriteAsync(new ReadOnlySequence<byte>(bytes));
                Console.ForegroundColor = protocolConsoleColor;
                Console.WriteLine($"Sent a message of length {bytes.Length} to {context.RemotePeer.Address}");
                Console.ForegroundColor = defaultConsoleColor;
            }
        }
    }
}
