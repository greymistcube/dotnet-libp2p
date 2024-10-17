using System.Threading.Channels;
using Multiformats.Address;

namespace Blockchain.Network
{
    public class Transport
    {
        private RoutingTable _routingTable;

        // Used by protocols.
        public event EventHandler<byte[]>? MessageToBroadcast;
        public event EventHandler<(Multiaddress, byte[], Channel<byte[]>)>? MessageToSend;

        // Used by application.
        public event EventHandler<(Multiaddress, byte[])>? BroadcastMessageReceived;
        public event EventHandler<(Multiaddress, byte[], Channel<byte[]>)>? RequestMessageReceived;

        public Transport(RoutingTable routingTable)
        {
            // NOTE: Strictly speaking, this is not needed.
            _routingTable = routingTable;
        }

        public void BroadcastMessage(byte[] message)
        {
            MessageToBroadcast?.Invoke(this, message);
        }

        public async Task<byte[]> SendAndRecieveMessage(Multiaddress peer, byte[] message)
        {
            Channel<byte[]> inboundReplyChannel = Channel.CreateUnbounded<byte[]>();
            MessageToSend?.Invoke(this, (peer, message, inboundReplyChannel));
            return await inboundReplyChannel.Reader.ReadAsync();
        }

        public void ReceiveBroadcastMessage(Multiaddress peer, byte[] message)
        {
            BroadcastMessageReceived?.Invoke(this, (peer, message));
        }

        public void ReceiveRequestMessage(Multiaddress peer, byte[] message, Channel<byte[]> outboundReplyChannel)
        {
            RequestMessageReceived?.Invoke(this, (peer, message, outboundReplyChannel));
        }
    }
}
