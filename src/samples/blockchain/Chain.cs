namespace blockchain
{
    public class Chain
    {
        private List<Block> _blocks;

        public Chain()
        {
            _blocks = new List<Block>();
        }

        public IReadOnlyList<Block> Blocks => _blocks;

        public void Append(Block block)
        {
            if (_blocks.Count() != block.Index)
            {
                throw new ArgumentException(
                    $"Given {nameof(block)} must have index {_blocks.Count()}: {block.Index}",
                    nameof(block));
            }

            _blocks.Add(block);
        }
    }
}
