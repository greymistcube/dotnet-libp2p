using System.Text;
using Nethermind.Libp2p.Core;

namespace blockchain
{
    public class ConsoleInterface
    {
        private static ConsoleReader _consoleReader = new ConsoleReader();

        public List<Func<byte[], Task>> _sendMessageAsyncs = new List<Func<byte[], Task>>();

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

        public void AddSendAsync(Func<byte[], Task> sendMessageAsync) =>
            _sendMessageAsyncs.Add(sendMessageAsync);

        public void ReceiveMessage(byte[] bytes)
        {
            if (bytes[0] == (byte)MessageType.Block)
            {
                var block = new Block(Encoding.UTF8.GetString(bytes.Skip(1).ToArray()));
                Console.WriteLine($"Received block: {block}");
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
        }

        public async Task BroadcastMessage(byte[] bytes, CancellationToken cancellationToken = default)
        {
            foreach (var sendMessageAsync in _sendMessageAsyncs)
            {
                await sendMessageAsync(bytes);
            }
        }
    }
}
