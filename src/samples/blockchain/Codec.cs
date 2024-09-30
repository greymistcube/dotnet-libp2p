using System.Text;

namespace blockchain
{
    public static class Codec
    {
        public static byte[] Encode(Block block) =>
            new byte[] { (byte)MessageType.Block }
                .Concat(Encoding.UTF8.GetBytes(block.ToString()))
                .ToArray();

        public static byte[] Encode(Transaction transaction) =>
            new byte[] { (byte)MessageType.Transaction }
                .Concat(Encoding.UTF8.GetBytes(transaction.ToString()))
                .ToArray();

        public static byte[] Encode(Chain chain) =>
            new byte[] { (byte)MessageType.Blocks }
                .Concat(Encoding.UTF8.GetBytes(chain.ToString()))
                .ToArray();
    }
}
