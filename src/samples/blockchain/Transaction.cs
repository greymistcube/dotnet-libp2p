namespace blockchain
{
    public class Transaction : IEquatable<Transaction>
    {
        public Transaction(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public static Transaction Create() => new Transaction(Guid.NewGuid().ToString());

        public override string ToString() => Id;

        public override int GetHashCode() => Id.GetHashCode();

        public bool Equals(Transaction? other) => other is Transaction transaction && Id == transaction.Id;

        public override bool Equals(object? obj) => obj is Transaction transaction && Equals(transaction);
    }
}
