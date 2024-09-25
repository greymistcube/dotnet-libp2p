namespace blockchain
{
    public class Transaction
    {
        public Transaction(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public static Transaction Create() => new Transaction(Guid.NewGuid().ToString());

        public override string ToString() => Id;
    }
}
