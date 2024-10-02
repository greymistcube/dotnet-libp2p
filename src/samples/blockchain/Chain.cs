namespace Blockchain
{
    public class Chain
    {
        private List<Block> _blocks;

        public Chain()
        {
            _blocks = new List<Block>();
        }

        public Chain(string serialized)
        {
            _blocks = serialized.Split(":").Select(s => new Block(s)).ToList();
        }

        public IReadOnlyList<Block> Blocks => _blocks;

        public void Append(Block block)
        {
            if (_blocks.Count != block.Index)
            {
                throw new ArgumentException(
                    $"Given {nameof(block)} must have index {_blocks.Count}: {block.Index}",
                    nameof(block));
            }

            _blocks.Add(block);
        }

        public Block Mine(List<Transaction> transactions) =>
            new Block(_blocks.Count, transactions);

        public override string ToString() => string.Join(":", _blocks.Select(block => block.ToString()));
    }
}
