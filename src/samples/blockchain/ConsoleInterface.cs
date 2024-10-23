using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Blockchain.Network;
using Multiformats.Address;
using Nethermind.Libp2p.Core;
using Nethermind.Libp2p.Protocols.Pubsub.Dto;

namespace Blockchain
{
    public class ConsoleInterface
    {
        private static ConsoleReader _consoleReader = new ConsoleReader();
        private Func<byte[], Task>? _toSendMessageTask = null;
        public event EventHandler<byte[]>? MessageToBroadcast;

        private Transport _transport;
        private Chain _chain;
        private MemPool _memPool;
        private bool _miner;
        private Multiaddress? _localListenerAddress;

        public ConsoleInterface(
            Transport transport,
            Chain chain,
            MemPool mempool,
            bool miner)
        {
            _transport = transport;
            _chain = chain;
            _memPool = mempool;
            _miner = miner;
            _localListenerAddress = null;
            _transport.BroadcastMessageReceived += async (sender, pair) => await ProcessReceivedBroadcastMessage(pair.Item1, pair.Item2);
            _transport.RequestMessageReceived += async (sender, triple) => await ProcessReceivedRequestMessage(triple.Item1, triple.Item2, triple.Item3);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                Console.Write("> ");
                var input = await _consoleReader.ReadLineAsync();
                if (input == "help")
                {
                    Console.WriteLine("List of commands: block, tx, pool, chain, table, exit");
                }
                else if (input == "block")
                {
                    if (_miner)
                    {
                        Block block = _chain.Mine(_memPool.Dump());
                        Console.WriteLine($"Created block: {block}");
                        _chain.Append(block);
                        byte[] message = Codec.Encode(block);
                        _transport.BroadcastMessage(message);
                    }
                    else
                    {
                        Console.WriteLine("Cannot create a block with a non-miner node.");
                    }
                }
                else if (input == "tx")
                {
                    Transaction transaction = new Transaction(Guid.NewGuid().ToString());
                    Console.WriteLine($"Created transaction: {transaction}");
                    _memPool.Add(transaction);
                    byte[] message = Codec.Encode(transaction);
                    _transport.BroadcastMessage(message);
                }
                else if (input == "pool")
                {
                    Console.WriteLine($"Number of transactions in mem-pool: {_memPool.Count}");
                }
                else if (input == "chain")
                {
                    Console.WriteLine($"Number of blocks in chain: {_chain.Blocks.Count}");
                }
                else if (input == "table")
                {
                    Console.WriteLine($"Number of peers: {_transport.RoutingTable.Peers.Count}");
                }
                else if (input == "exit")
                {
                    Console.WriteLine("Terminating process.");
                    return;
                }
                else
                {
                    Console.WriteLine($"Unknown command: {input}");
                }
            }
        }

        public async Task ProcessReceivedBroadcastMessage(Multiaddress remote, byte[] message)
        {
            byte messageType = message[0];
            if (messageType == (byte)MessageType.Block)
            {
                var block = new Block(Encoding.UTF8.GetString(message.Skip(1).ToArray()));
                Console.WriteLine($"Received block: {block}");
                if (block.Index == _chain.Blocks.Count)
                {
                    _chain.Append(block);
                    Console.WriteLine($"Appended block to current chain.");

                    foreach (var tx in block.Transactions)
                    {
                        _memPool.Remove(tx);
                    }
                }
                else
                {
                    // NOTE: This only works since remote will always be a single miner.
                    // Otherwise, a broadcast message needs to include the identity of the original creator.
                    Console.WriteLine($"Ignoring block as the index does not match {_chain.Blocks.Count}.");
                    Console.WriteLine($"Trying to sync chain...");
                    await SyncChain(remote);
                    Console.WriteLine($"Sync complete.");
                }
            }
            else if (messageType == (byte)MessageType.Transaction)
            {
                var transaction = new Transaction(Encoding.UTF8.GetString(message.Skip(1).ToArray()));
                Console.WriteLine($"Received transaction: {transaction}");
                _memPool.Add(transaction);
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {messageType}");
            }
        }

        public async Task ProcessReceivedRequestMessage(Multiaddress remote, byte[] message, Channel<byte[]> replyChannel)
        {
            if (message[0] == (byte)MessageType.GetBlocks)
            {
                byte[] reply = Codec.Encode(_chain);
                await replyChannel.Writer.WriteAsync(reply);
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {message[0]}");
            }
        }

        public async Task SyncChain(Multiaddress remote)
        {
            byte[] message = { (byte)MessageType.GetBlocks };
            byte[] reply = await _transport.SendAndRecieveMessage(remote, message);

            if (reply[0] == (byte)MessageType.Blocks)
            {
                var chain = new Chain(Encoding.UTF8.GetString(reply.Skip(1).ToArray()));
                Console.WriteLine($"Received {chain.Blocks.Count} blocks.");

                var appended = 0;
                foreach (var block in chain.Blocks)
                {
                    if (block.Index >= _chain.Blocks.Count)
                    {
                        _chain.Append(block);
                        appended++;
                    }
                }

                foreach (var block in chain.Blocks)
                {
                    foreach (var transaction in block.Transactions)
                    {
                        _memPool.Remove(transaction);
                    }
                }

                Console.WriteLine($"Appended {appended} blocks to current chain.");
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {reply[0]}");
            }
        }
    }
}
