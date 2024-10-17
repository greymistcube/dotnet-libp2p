using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Blockchain.Network;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

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
            _transport.BroadcastMessageReceived += (sender, pair) => ProcessReceivedBroadcastMessage(pair.Item1, pair.Item2);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                Console.Write("> ");
                var input = await _consoleReader.ReadLineAsync();
                if (input == "block")
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
                    if (_miner)
                    {
                        Console.WriteLine("Cannot create a transaction with a miner node.");
                    }
                    else
                    {
                        Transaction transaction = new Transaction(Guid.NewGuid().ToString());
                        Console.WriteLine($"Created transaction: {transaction}");
                        byte[] message = Codec.Encode(transaction);
                        _transport.BroadcastMessage(message);
                    }
                }
                else if (input == "sync")
                {
                    Console.WriteLine("Sync command is not supported.");
                    /*
                    // NOTE: This should normally be initiated with polling with
                    // some way of retrieving a target remote to sync.
                    if (_miner)
                    {
                        Console.WriteLine("Cannot sync chain as a miner node.");

                    }
                    else
                    {
                        byte[] bytes = { (byte)MessageType.GetBlocks };
                        if (_toSendMessageTask is { } toTask)
                        {
                            await toTask(bytes);
                        }
                        else
                        {
                            throw new NullReferenceException();
                        }
                    }
                    */
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

        public void ProcessReceivedBroadcastMessage(Multiaddress remote, byte[] message)
        {
            byte messageType = message[0];
            if (messageType == (byte)MessageType.Block)
            {
                // FIXME: Sync logic should be added here.
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
                    Console.WriteLine($"Ignoring block as the index does not match {_chain.Blocks.Count}.");
                }
            }
            else if (messageType == (byte)MessageType.Transaction)
            {
                var transaction = new Transaction(Encoding.UTF8.GetString(message.Skip(1).ToArray()));
                Console.WriteLine($"Received transaction: {transaction}");
                if (_miner)
                {
                    _memPool.Add(transaction);
                }
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {messageType}");
            }
        }

        public void ProcessReceivedRequestMessage(Multiaddress remote, byte[] message, Channel<byte> replyChannel)
        {

        }

        public async Task SyncBlockChain(Multiaddress remote)
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

                Console.WriteLine($"Appended {appended} blocks to current chain.");
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {reply[0]}");
            }
        }

        public async Task ReceiveBroadcastMessage(byte[] bytes, IPeerContext context)
        {
            byte messageType = bytes[0];
            if (messageType == (byte)MessageType.Block)
            {
                var block = new Block(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received block: {block}");
                if (block.Index == _chain.Blocks.Count)
                {
                    _chain.Append(block);
                    Console.WriteLine($"Appended block to current chain.");
                }
                else
                {
                    Console.WriteLine($"Ignoring block as the index does not match {_chain.Blocks.Count}.");
                }
            }
            else if (messageType == (byte)MessageType.Transaction)
            {
                var transaction = new Transaction(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received transaction: {transaction}");
                if (_miner)
                {
                    _memPool.Add(transaction);
                }
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {messageType}");
            }
        }

        public async Task ReceivePingPongMessage(
            byte[] bytes,
            Func<byte[], Task> toSendReplyMessageTask,
            CancellationToken cancellationToken = default)
        {
            byte messageType = bytes[0];
            if (messageType == (byte)MessageType.GetBlocks)
            {
                Console.WriteLine($"Received get blocks.");
                await toSendReplyMessageTask(Codec.Encode(_chain));
            }
            else if (messageType == (byte)MessageType.Blocks)
            {
                var chain = new Chain(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
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

                Console.WriteLine($"Appended {appended} blocks to current chain.");
            }
            else
            {
                Console.WriteLine($"Received a message of unknown type: {messageType}");
            }
        }

        public void SetToSendMessageTask(Func<byte[], Task> toSendMessageTask)
        {
            _toSendMessageTask = toSendMessageTask;
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
