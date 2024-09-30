using System.Text;
using Nethermind.Libp2p.Core;

namespace blockchain
{
    public class ConsoleInterface
    {
        private static ConsoleReader _consoleReader = new ConsoleReader();

        public List<Func<byte[], Task>> _sendMessageAsyncs = new List<Func<byte[], Task>>();

        public async Task StartAsync(
            Chain chain,
            bool miner,
            CancellationToken cancellationToken = default)
        {
            while (true)
            {
                Console.Write("> ");
                var input = await _consoleReader.ReadLineAsync();
                if (input == "block")
                {
                    if (miner)
                    {
                        Block block = chain.Mine(new List<Transaction>());
                        Console.WriteLine($"Created block: {block}");
                        chain.Append(block);
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
                    if (miner)
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
