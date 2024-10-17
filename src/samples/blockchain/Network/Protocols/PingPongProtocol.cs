using System.Buffers;
using System.Threading.Channels;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

namespace Blockchain.Network.Protocols
{
    internal class PingPongProtocol : IProtocol
    {
        private readonly ConsoleColor protocolConsoleColor = ConsoleColor.DarkGreen;
        private readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;
        private readonly Transport _transport;

        public string Id => "/ping-pong/1.0.0";

        public PingPongProtocol(Transport transport)
        {
            _transport = transport;
        }

        /// <summary>
        /// Responsible for sending requests and receiving replys.
        /// </summary>
        public async Task DialAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            WriteLineToConsole($"Connected to remote peer {context.RemotePeer.Address} as {context.LocalPeer.Address} as dialer.");
            Channel<(Multiaddress, byte[], Channel<byte[]>)> requestRequests = Channel.CreateUnbounded<(Multiaddress, byte[], Channel<byte[]>)>();
            EventHandler<(Multiaddress, byte[], Channel<byte[]>)> eventHandler = (object? sender, (Multiaddress, byte[], Channel<byte[]>) triple) => requestRequests.Writer.TryWrite(triple);

            try
            {
                _transport.MessageToSend += eventHandler;
                await SendAndReceiveMessage(requestRequests, channel, context);
            }
            finally
            {
                _transport.MessageToSend -= eventHandler;
            }
        }

        /// <summary>
        /// Responsible for receiving requests and sending replys.
        /// </summary>
        public async Task ListenAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            WriteLineToConsole($"Connected to remote peer {context.RemotePeer.Address} as {context.LocalPeer.Address} as listener.");
            await ReceiveAndSendMessage(channel, context);
        }

        private async Task SendAndReceiveMessage(
            Channel<(Multiaddress, byte[], Channel<byte[]>)> requestRequests,
            IChannel channel,
            IPeerContext context)
        {
            while(true)
            {
                (Multiaddress remoteAddress, byte[] message, Channel<byte[]> localChannel) = await requestRequests.Reader.ReadAsync();
                if (remoteAddress.Equals(context.RemotePeer.Address))
                {
                    await channel.WriteAsync(new ReadOnlySequence<byte>(message));
                    WriteLineToConsole($"Sent a message of length {message.Length} to {context.RemotePeer.Address}");

                    // FIXME: This does not guarantee that the message read is the reply for the message sent.
                    ReadOnlySequence<byte> read = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                    WriteLineToConsole($"Reaceived a message of length {read.Length} from {context.RemotePeer.Address}");
                    await localChannel.Writer.WriteAsync(read.ToArray());
                }
                else
                {
                    WriteLineToConsole($"Ignoring message to send since target peer {remoteAddress} is not the same as the context's peer {context.RemotePeer.Address}");
                }
            }
        }

        private async Task ReceiveAndSendMessage(IChannel channel, IPeerContext context)
        {
            while(true)
            {
                ReadOnlySequence<byte> read = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                WriteLineToConsole($"Reaceived a message of length {read.Length} from {context.RemotePeer.Address}");

                Channel<byte[]> outboundReplyChannel = Channel.CreateUnbounded<byte[]>();
                _transport.ReceiveRequestMessage(
                    context.RemotePeer.Address,
                    read.ToArray(),
                    outboundReplyChannel);
                byte[] replyMessage = await outboundReplyChannel.Reader.ReadAsync(default);
                await channel.WriteAsync(new ReadOnlySequence<byte>(replyMessage));
                WriteLineToConsole($"Sent a message of length {replyMessage.Length} to {context.RemotePeer.Address}");
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
