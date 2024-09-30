namespace blockchain
{
    public class MemPool
    {
        private Dictionary<string, Transaction> _transactions;

        public MemPool()
        {
            _transactions = new Dictionary<string, Transaction>();
        }

        public bool Add(Transaction transaction) =>
            _transactions.TryAdd(transaction.Id, transaction);

        public List<Transaction> Dump()
        {
            List<Transaction> dump = _transactions.Select(pair => pair.Value).ToList();
            _transactions.Clear();
            return dump;
        }
    }
}
