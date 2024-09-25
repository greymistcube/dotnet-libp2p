namespace blockchain
{
    public class Block
    {
        public Block(int index, IReadOnlyList<Transaction> transactions)
        {
            Index = index;
            Transactions = transactions;
        }

        public int Index { get; }

        public IReadOnlyList<Transaction> Transactions { get; }

        public override string ToString()
        {
            var str = $"{Index}";
            foreach (var transaction in Transactions)
            {
                str = str += $",{transaction}";
            }

            return str;
        }
    }
}
