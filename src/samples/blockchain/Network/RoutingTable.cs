using System.Collections.Immutable;
using Blockchain.Network.Protocols;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

namespace Blockchain.Network
{
    public class RoutingTable
    {
        private ILocalPeer _localPeer;

        // NOTE: For whatever reason, Multiaddress equality can't be used.
        private ImmutableDictionary<PeerId, Multiaddress> _peerListenerAddresses;
        private Multiaddress? _localListenerAddress;

        public event EventHandler<Multiaddress>? PeerAdded;

        public RoutingTable()
        {
            _peerListenerAddresses = ImmutableDictionary<PeerId, Multiaddress>.Empty;
            PeerAdded += async (sender, remotePeerListenerAddress) => await Connect(remotePeerListenerAddress);
        }

        public void Add(Multiaddress peerListenerAddress)
        {
            if (peerListenerAddress.GetPeerId()!.Equals(LocalListenerAddress.GetPeerId()!))
            {
                Console.WriteLine($"Given {peerListenerAddress} is the same as {nameof(LocalListenerAddress)}.");
            }
            else if (!_peerListenerAddresses.ContainsKey(peerListenerAddress.GetPeerId()!))
            {
                _peerListenerAddresses = _peerListenerAddresses
                    .Add(peerListenerAddress.GetPeerId()!, peerListenerAddress);
                PeerAdded?.Invoke(this, peerListenerAddress);
                Console.WriteLine($"Given {peerListenerAddress} is added to the table.");
            }
            else
            {
                Console.WriteLine($"Given {peerListenerAddress} is already in the table.");
            }
        }

        public void Remove(Multiaddress peerListenerAddress)
        {
            if (_peerListenerAddresses.ContainsKey(peerListenerAddress.GetPeerId()!))
            {
                _peerListenerAddresses = _peerListenerAddresses.Remove(peerListenerAddress.GetPeerId()!);
                Console.WriteLine($"Given {peerListenerAddress} is removed from the table.");
            }
            else
            {
                Console.WriteLine($"Given {peerListenerAddress} is not found in the table.");
            }
        }

        public async Task Connect(Multiaddress remotePeerListenerAddress)
        {
            await ExchangePeers(remotePeerListenerAddress);
            await Communicate(remotePeerListenerAddress);
        }

        public async Task ExchangePeers(Multiaddress remotePeerListenerAddress)
        {
            IRemotePeer remote = await LocalPeer.DialAsync(remotePeerListenerAddress, default);
            await remote.DialAsync<PeerExchangeProtocol>(default);
        }

        public async Task Communicate(Multiaddress remotePeerListenerAddress)
        {
            IRemotePeer remote = await LocalPeer.DialAsync(remotePeerListenerAddress, default);
            Task broadcastConnection = remote.DialAsync<BroadcastProtocol>(default);
            Task pingPongConnection = remote.DialAsync<PingPongProtocol>(default);
            await Task
                .WhenAny(broadcastConnection, pingPongConnection)
                .ContinueWith(t => Console.WriteLine($"Lost connection to {remotePeerListenerAddress}"))
                .ContinueWith(t => Remove(remotePeerListenerAddress));
        }

        public ImmutableDictionary<PeerId, Multiaddress> Peers => _peerListenerAddresses;

        public ILocalPeer LocalPeer
        {
            get
            {
                return _localPeer ??
                    throw new NullReferenceException($"{nameof(LocalPeer)} is not set.");
            }
            set
            {
                if (_localPeer is null)
                {
                    _localPeer = value;
                }
                else
                {
                    throw new InvalidOperationException($"{nameof(LocalPeer)} is already set.");
                }
            }
        }

        public Multiaddress LocalListenerAddress
        {
            get
            {
                return _localListenerAddress ??
                    throw new NullReferenceException($"{nameof(LocalListenerAddress)} is not set.");
            }
            set
            {
                if (_localListenerAddress is null)
                {
                    _localListenerAddress = value;
                }
                else
                {
                    throw new InvalidOperationException($"{nameof(LocalListenerAddress)} is already set.");
                }
            }
        }
    }
}
