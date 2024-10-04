using System.Buffers;
using Nethermind.Libp2p.Core;

namespace Blockchain.Protocols
{
    internal class PingPongProtocol : SymmetricProtocol, IProtocol
    {
        private readonly ConsoleColor protocolConsoleColor = ConsoleColor.DarkGreen;
        private readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;
        private readonly ConsoleInterface _consoleInterface;

        public string Id => "/ping-pong/1.0.0";

        public PingPongProtocol(ConsoleInterface consoleInterface)
        {
            _consoleInterface = consoleInterface;
        }

        protected override async Task ConnectAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context,
            bool isListener)
        {
            _consoleInterface.SetToSendMessageTask(
                bytes => SendRequestMessage(bytes, channel, context));
            Task receiveTask = ReceiveMessage(channel, context);
            await Task.WhenAny(receiveTask);
        }

        private async Task SendRequestMessage(
            byte[] bytes,
            IChannel channel,
            IPeerContext context)
        {
            await channel.WriteAsync(new ReadOnlySequence<byte>(bytes));
            Console.ForegroundColor = protocolConsoleColor;
            Console.WriteLine($"Sent a message of length {bytes.Length} to {context.RemotePeer.Address}");
            Console.ForegroundColor = defaultConsoleColor;
        }

        private async Task ReceiveMessage(IChannel channel, IPeerContext context)
        {
            while(true)
            {
                ReadOnlySequence<byte> read = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                Console.ForegroundColor = protocolConsoleColor;
                Console.WriteLine($"Reaceived a message of length {read.Length} from {context.RemotePeer.Address}");
                Console.ForegroundColor = defaultConsoleColor;
                await _consoleInterface.RecievePingPongMessage(
                    read.ToArray(),
                    bytes => SendReplyMessage(bytes, channel, context));
            }
        }

        private async Task SendReplyMessage(byte[] bytes, IChannel channel, IPeerContext context)
        {
            await channel.WriteAsync(new ReadOnlySequence<byte>(bytes));
            Console.ForegroundColor = protocolConsoleColor;
            Console.WriteLine($"Sent a message of length {bytes.Length} to {context.RemotePeer.Address}");
            Console.ForegroundColor = defaultConsoleColor;
        }
    }
}
