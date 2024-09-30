namespace blockchain
{
    public class Block
    {
        public Block(int index, IReadOnlyList<Transaction> transactions)
        {
            Index = index;
            Transactions = transactions;
        }

        public Block(string serialized)
        {
            var components = serialized.Split(",");
            Index = int.Parse(components[0]);
            Transactions = components.Skip(1).Select(s => new Transaction(s)).ToList();
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
