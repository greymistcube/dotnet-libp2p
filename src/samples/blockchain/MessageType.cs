namespace Blockchain
{
    public enum MessageType : byte
    {
        Block = 0x01,
        Transaction = 0x02,

        GetBlocks = 0x11,
        Blocks = 0x12,
    }
}
