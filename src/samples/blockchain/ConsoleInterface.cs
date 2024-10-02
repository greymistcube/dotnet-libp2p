using System.Text;
using Multiformats.Address;
using Nethermind.Libp2p.Core;

namespace Blockchain
{
    public class ConsoleInterface
    {
        private static ConsoleReader _consoleReader = new ConsoleReader();

        public Dictionary<Multiaddress, Func<byte[], Task>> _sendMessageAsyncs = new Dictionary<Multiaddress, Func<byte[], Task>>();

        private Chain _chain;
        private MemPool _memPool;
        private bool _miner;

        public ConsoleInterface(
            Chain chain,
            MemPool mempool,
            bool miner)
        {
            _chain = chain;
            _memPool = mempool;
            _miner = miner;
        }

        public async Task StartAsync(
            CancellationToken cancellationToken = default)
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
                        byte[] bytes = Codec.Encode(block);
                        await BroadcastMessage(bytes, cancellationToken);
                    }
                    else
                    {
                        Console.WriteLine("Cannot create a block with a non-miner node.");
                    }
                }
                else if (input == "transaction")
                {
                    if (_miner)
                    {
                        Console.WriteLine("Cannot create a transaction with a miner node.");
                    }
                    else
                    {
                        Transaction transaction = new Transaction(Guid.NewGuid().ToString());
                        Console.WriteLine($"Created transaction: {transaction}");
                        byte[] bytes = Codec.Encode(transaction);
                        await BroadcastMessage(bytes, cancellationToken);
                    }
                }
                else if (input == "sync")
                {
                    if (_miner)
                    {
                        Console.WriteLine("Cannot sync chain as a miner node.");

                    }
                    else
                    {
                        byte[] bytes = { (byte)MessageType.GetBlocks };
                        await BroadcastMessage(bytes, cancellationToken);
                    }
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

        public void AddSendAsync(Multiaddress multiaddress, Func<byte[], Task> sendMessageAsync) =>
            _sendMessageAsyncs.Add(multiaddress, sendMessageAsync);

        public async Task ReceiveMessage(byte[] bytes, IPeerContext context)
        {
            if (bytes[0] == (byte)MessageType.Block)
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
            else if (bytes[0] == (byte)MessageType.Transaction)
            {
                var transaction = new Transaction(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received transaction: {transaction}");
                if (_miner)
                {
                    _memPool.Add(transaction);
                }
            }
            else if (bytes[0] == (byte)MessageType.GetBlocks)
            {
                Console.WriteLine($"Received get blocks request.");
                await SendMessage(context.RemotePeer.Address, Codec.Encode(_chain));
            }
            else if (bytes[0] == (byte)MessageType.Blocks)
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
        }

        public async Task BroadcastMessage(byte[] bytes, CancellationToken cancellationToken = default)
        {
            foreach ((_, var sendMessageAsync) in _sendMessageAsyncs)
            {
                await sendMessageAsync(bytes);
            }
        }

        public async Task SendMessage(Multiaddress multiaddress, byte[] bytes, CancellationToken cancellationToken = default)
        {
            await _sendMessageAsyncs[multiaddress](bytes);
        }
    }
}
