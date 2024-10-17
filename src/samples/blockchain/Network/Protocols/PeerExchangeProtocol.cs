using System.Buffers;
using System.Text;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

namespace Blockchain.Network.Protocols
{
    internal class PeerExchangeProtocol : IProtocol
    {
        private readonly ConsoleColor protocolConsoleColor = ConsoleColor.DarkCyan;
        private readonly ConsoleColor defaultConsoleColor = Console.ForegroundColor;
        private readonly RoutingTable _routingTable;

        public string Id => "/peer-exchange/1.0.0";

        public PeerExchangeProtocol(RoutingTable routingTable)
        {
            _routingTable = routingTable;
        }

        public async Task DialAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            // Initial listener exchange.
            WriteLineToConsole($"Connected to remote peer {context.RemotePeer.Address} as {context.LocalPeer.Address}.");
            await ReceiveListenerAsync(channel, context);
            await SendListenerAsync(channel, context);

            // Peers exchange.
            await ReceivePeersAsync(channel, context);
            await SendPeersAsync(channel, context);
            WriteLineToConsole($"Done exchanging peers with {context.RemotePeer.Address}");
        }

        public async Task ListenAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context)
        {
            // Initial listener exchange.
            WriteLineToConsole($"Remote peer {context.RemotePeer.Address} has cconnected.");
            await SendListenerAsync(channel, context);
            await ReceiveListenerAsync(channel, context);

            // Peers exchange.
            await SendPeersAsync(channel, context);
            await ReceivePeersAsync(channel, context);
            WriteLineToConsole($"Done exchanging peers with {context.RemotePeer.Address}");
        }

        private async Task SendListenerAsync(
            IChannel channel,
            IPeerContext context)
        {
            await channel.WriteAsync(new ReadOnlySequence<byte>(new byte[] { (byte)MessageType.Signal }));
            await channel.WriteAsync(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(_routingTable.LocalListenerAddress.ToString())));
            await channel.WriteAsync(new ReadOnlySequence<byte>(new byte[] { (byte)MessageType.Signal }));

            WriteLineToConsole($"Sent listening peer {_routingTable.LocalListenerAddress} to peer {context.RemotePeer.Address}.");
        }

        private async Task ReceiveListenerAsync(
            IChannel channel,
            IPeerContext context)
        {
            var buffer = (await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow()).ToArray();
            if (buffer.Length != 1 || buffer[0] != (byte)MessageType.Signal)
            {
                throw new ArgumentException("Received invalid data.");
            }

            buffer = (await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow()).ToArray();
            Multiaddress remote = Encoding.UTF8.GetString(buffer);
            WriteLineToConsole($"Received {remote} peer from peer {context.RemotePeer.Address}.");
            _routingTable.Add(remote);

            buffer = (await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow()).ToArray();
            if (buffer.Length != 1 || buffer[0] != (byte)MessageType.Signal)
            {
                throw new ArgumentException("Received invalid data.");
            }
        }

        private async Task SendPeersAsync(
            IChannel channel,
            IPeerContext context)
        {
            await channel.WriteAsync(new ReadOnlySequence<byte>(new byte[] { (byte)MessageType.Signal }));
            foreach (var peer in _routingTable.Peers.Values)
            {
                await channel.WriteAsync(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(peer.ToString())));
            }

            await channel.WriteAsync(new ReadOnlySequence<byte>(new byte[] { (byte)MessageType.Signal }));
            WriteLineToConsole($"Sent {_routingTable.Peers.Count} peers to peer {context.RemotePeer.Address}.");
        }

        private async Task ReceivePeersAsync(
            IChannel channel,
            IPeerContext context)
        {
            var buffer = (await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow()).ToArray();
            if (buffer.Length != 1 || buffer[0] != (byte)MessageType.Signal)
            {
                throw new ArgumentException("Received invalid data.");
            }

            while (true)
            {
                buffer = (await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow()).ToArray();
                if (buffer.Length == 1 && buffer[0] == (byte)MessageType.Signal)
                {
                    break;
                }
                else
                {
                    Multiaddress remote = Encoding.UTF8.GetString(buffer);
                    WriteLineToConsole($"Received {remote} peer from peer {context.RemotePeer.Address}.");
                    _routingTable.Add(remote);
                }
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
