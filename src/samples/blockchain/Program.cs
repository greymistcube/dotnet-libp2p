using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nethermind.Libp2p.Stack;
using Nethermind.Libp2p.Core;
using Multiformats.Address;
using Blockchain.Protocols;

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

            var consoleInterface = new ConsoleInterface(new Chain(), new MemPool(), miner);
            ServiceProvider serviceProvider = new ServiceCollection()
                .AddLibp2p(builder => builder
                    .AddAppLayerProtocol<HandshakeProtocol>(new HandshakeProtocol(consoleInterface))
                    .AddAppLayerProtocol<BroadcastProtocol>(new BroadcastProtocol(consoleInterface))
                    .AddAppLayerProtocol<PingPongProtocol>(new PingPongProtocol(consoleInterface)))
                .AddLogging(builder =>
                    builder.SetMinimumLevel(args.Contains("--trace") ? LogLevel.Trace : LogLevel.Information)
                        .AddSimpleConsole(l =>
                        {
                            l.SingleLine = true;
                            l.TimestampFormat = "[HH:mm:ss.FFF]";
                        }))
                .BuildServiceProvider();

            ILogger logger = serviceProvider.GetService<ILoggerFactory>()!.CreateLogger("Chat");
            IPeerFactory peerFactory = serviceProvider.GetService<IPeerFactory>()!;

            CancellationTokenSource ts = new();

            Task transportTask = miner
                ? RunMiner(logger, peerFactory, args, ts.Token)
                : RunClient(logger, peerFactory, args, ts.Token);
            Task consoleTask = consoleInterface.StartAsync(ts.Token);

            await Task.WhenAny(transportTask, consoleTask);
        }

        public static async Task RunMiner(
            ILogger logger,
            IPeerFactory peerFactory,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Running as a miner");

            Identity optionalFixedIdentity = new(Enumerable.Repeat((byte)42, 32).ToArray());
            ILocalPeer peer = peerFactory.Create(optionalFixedIdentity);

            string addrTemplate = "/ip4/127.0.0.1/tcp/{0}";
            IListener listener = await peer.ListenAsync(
                string.Format(addrTemplate, args.Length > 0 && args[0] == "-sp" ? args[1] : "0"),
                cancellationToken);
            using (StreamWriter outputFile = new StreamWriter(ListenerAddressFileName, false))
            {
                outputFile.WriteLine(listener.Address.ToString());
            }
            logger.LogInformation("Listener started at {address}", listener.Address);

            listener.OnConnection += remotePeer =>
            {
                logger.LogInformation("A peer connected {remote}", remotePeer.Address);
                return Task.CompletedTask;
            };
            Console.CancelKeyPress += delegate { listener.DisconnectAsync(); };

            await listener;
        }

        public static async Task RunClient(
            ILogger logger,
            IPeerFactory peerFactory,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Running as a client");

            string addrTemplate = "/ip4/127.0.0.1/tcp/0";
            ILocalPeer localPeer = peerFactory.Create(localAddr: addrTemplate);

            Multiaddress remoteAddr;
            using (StreamReader inputFile = new StreamReader(ListenerAddressFileName))
            {
                remoteAddr = inputFile.ReadLine();
            }
            logger.LogInformation("Dialing {remote}", remoteAddr);
            IRemotePeer remotePeer = await localPeer.DialAsync(remoteAddr, cancellationToken);

            await remotePeer.DialAsync<HandshakeProtocol>(cancellationToken);
            await remotePeer.DisconnectAsync();
        }
    }
}
