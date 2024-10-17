using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethermind.Libp2p.Stack;
using Nethermind.Libp2p.Core;
using Multiformats.Address;
using Blockchain.Network;
using Blockchain.Network.Protocols;

namespace Blockchain
{
    public static class Program
    {
        public const string ListenerAddressFileName = "listener.txt";

        public static async Task Main(string[] args)
        {
            bool miner;
            switch (args[0])
            {
                case "-m":
                    miner = true;
                    break;
                case "-c":
                    miner = false;
                    break;
                default:
                    throw new ArgumentException($"The first argument should be either -m or -c: {args[0]}");
            }

            var routingTable = new RoutingTable();
            var transport = new Transport(routingTable);
            var consoleInterface = new ConsoleInterface(transport, new Chain(), new MemPool(), miner);

            ServiceProvider serviceProvider = new ServiceCollection()
                .AddLibp2p(builder => builder
                    .AddAppLayerProtocol<PeerExchangeProtocol>(new PeerExchangeProtocol(routingTable))
                    .AddAppLayerProtocol<BroadcastProtocol>(new BroadcastProtocol(transport))
                    .AddAppLayerProtocol<PingPongProtocol>(new PingPongProtocol(transport)))
                .AddLogging(builder =>
                    builder.SetMinimumLevel(args.Contains("--trace") ? LogLevel.Trace : LogLevel.Information)
                        .AddSimpleConsole(l =>
                        {
                            l.SingleLine = true;
                            l.TimestampFormat = "[HH:mm:ss.FFF]";
                        }))
                .BuildServiceProvider();

            CancellationTokenSource ts = new();

            ILogger logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger("Chat");
            IPeerFactory peerFactory = serviceProvider.GetService<IPeerFactory>()!;
            string addrTemplate = "/ip4/127.0.0.1/tcp/0";
            ILocalPeer localPeer = peerFactory.Create(localAddr: addrTemplate);
            logger.LogInformation("Local peer created at {address}", localPeer.Address);
            IListener listener = await localPeer.ListenAsync(addrTemplate, ts.Token);
            logger.LogInformation("Listener started at {address}", listener.Address);
            if (miner)
            {
                using (StreamWriter outputFile = new StreamWriter(ListenerAddressFileName, false))
                {
                    outputFile.WriteLine(listener.Address.ToString());
                }
            }

            listener.OnConnection += remotePeer =>
            {
                logger.LogInformation("A peer connected {remote}", remotePeer.Address);
                return Task.CompletedTask;
            };
            Console.CancelKeyPress += delegate { listener.DisconnectAsync(); };

            routingTable.LocalPeer = localPeer;
            routingTable.LocalListenerAddress = listener.Address;

            // NOTE: Not sure which order these should be in.
            Task transportTask = miner
                ? RunMiner(logger, listener, ts.Token)
                : RunClient(logger, listener, routingTable, ts.Token);
            Task consoleTask = consoleInterface.StartAsync(ts.Token);

            await Task.WhenAny(transportTask, consoleTask);
        }

        public static async Task RunMiner(
            ILogger logger,
            IListener listener,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Running as a miner");
            await listener;
        }

        public static async Task RunClient(
            ILogger logger,
            IListener listener,
            RoutingTable routingTable,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Running as a client");

            Multiaddress remoteListenerAddress;
            using (StreamReader inputFile = new StreamReader(ListenerAddressFileName))
            {
                remoteListenerAddress = inputFile.ReadLine();
            }
            logger.LogInformation("Starting with seed peer {remote}", remoteListenerAddress);

            routingTable.Add(remoteListenerAddress);
            await listener;
        }
    }
}
