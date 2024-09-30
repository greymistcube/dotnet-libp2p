// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Text;
using Nethermind.Libp2p.Core;

namespace blockchain
{
    internal class ChatProtocol : SymmetricProtocol, IProtocol
    {
        private static readonly ConsoleReader Reader = new();
        private readonly ConsoleColor defautConsoleColor = Console.ForegroundColor;
        private readonly ConsoleInterface _consoleInterface;

        public string Id => "/chat/1.0.0";

        public ChatProtocol(ConsoleInterface consoleInterface)
        {
            _consoleInterface = consoleInterface;
        }

        protected override async Task ConnectAsync(
            IChannel channel,
            IChannelFactory? channelFactory,
            IPeerContext context,
            bool isListener)
        {
            _consoleInterface.AddSendAsync(bytes => SendMessage(channel, context, bytes));
            while (true)
            {
                ReadOnlySequence<byte> read = await channel.ReadAsync(0, ReadBlockingMode.WaitAny).OrThrow();
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"Reaceived a message of length {read.Length}");
                Console.ForegroundColor = defautConsoleColor;
                _consoleInterface.ReceiveMessage(read.ToArray());
            }
        }

        public async Task SendMessage(IChannel channel, IPeerContext context, byte[] bytes)
        {
            await channel.WriteAsync(new ReadOnlySequence<byte>(bytes));
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"Sent a message of length {bytes.Length}");
            Console.ForegroundColor = defautConsoleColor;
        }
    }
}
