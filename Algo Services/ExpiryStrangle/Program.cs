using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ExpiryStrangle
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var isService = !(Debugger.IsAttached || args.Contains("--console"));
            var pathToContentRoot = Directory.GetCurrentDirectory();
            var webHostArgs = args.Where(arg => arg != "--console").ToArray();
            if (isService)
            {
                var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
                pathToContentRoot = Path.GetDirectoryName(pathToExe);
            }

            var config = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("hostsettings.json", optional: true)
               .AddCommandLine(args)
               .Build();

            var host = WebHost.CreateDefaultBuilder(args)
                .UseContentRoot(pathToContentRoot)
                .UseUrls(GlobalLayer.APIPORT.ExpiryStrangle)
                .UseConfiguration(config)
                .UseStartup<Startup>()
                .Build();

            //var cfg = new IgniteClientConfiguration
            //{ Endpoints = new string[] { "127.0.0.1:10900" } };

            //IIgniteClient igniteClient = Ignition.StartClient(cfg);
            ////{
            //    ICacheClient<uint, Tick> cache = igniteClient.GetOrCreateCache<uint, Tick>(Constants.IGNITE_CACHENAME);

            ////    PutGet(cache);
            ////}

            //GlobalObjects.IgniteClient = igniteClient;
            //GlobalObjects.ICacheClient = cache;
            //GlobalObjects.IMessaging = cache..


            //Ignition.ClientMode = true;
            //IIgnite ignite = Ignition.Start(new IgniteConfiguration
            //{
            //    DiscoverySpi = new TcpDiscoverySpi
            //    {
            //        IpFinder = new TcpDiscoveryStaticIpFinder
            //        {
            //            Endpoints = new[] { "192.168.1.3:47500..47509" }
            //        },
            //        SocketTimeout = TimeSpan.FromSeconds(15.5)
            //    },
            //    CommunicationSpi = new TcpCommunicationSpi
            //    {
            //        SlowClientQueueLimit = 1000,
            //    },
            //    PeerAssemblyLoadingMode = PeerAssemblyLoadingMode.CurrentAppDomain,
            //    IncludedEventTypes = EventType.CacheAll,
            //    JvmOptions = new[] { "-Xms2g", "-Xmx5g" },
            //    DataStorageConfiguration = new DataStorageConfiguration
            //    {
            //        DefaultDataRegionConfiguration = new DataRegionConfiguration { Name = "defaultRegion", PersistenceEnabled = false },
            //        DataRegionConfigurations = new DataRegionConfiguration[] { new DataRegionConfiguration { Name = "inMemoryRegion" } }
            //    },
            //    ClientConnectorConfiguration = new ClientConnectorConfiguration { Host = "192.168.1.3", Port = 10900, PortRange = 50, MaxOpenCursorsPerConnection = 50 }

            //});


            //// Clean up caches on all nodes before run.
            //var cache = ignite.GetOrCreateCache<TickKey, Tick>(Constants.IGNITE_CACHENAME);
            //cache.Clear();

            ////var ldr = ignite.GetDataStreamer<TickKey, Tick>(Constants.IGNITE_CACHENAME);

            ////Local listner
            ////var imsg = ignite.GetCluster().ForLocal().GetMessaging();

            //// remote too
            //var imsg = ignite.GetMessaging();

            //GlobalObjects.Ignite = ignite;
            //GlobalObjects.ICache = cache;
            ////GlobalObjects.IDataStreamer = ldr;
            //GlobalObjects.IMessaging = imsg;


            //var marketCache = ignite.GetOrCreateCache<uint, Tick>(Constants.IGNITE_MARKETDATA_CACHE);
            //marketCache.Clear();
            //var mdr = ignite.GetDataStreamer<uint, Tick>(Constants.IGNITE_MARKETDATA_CACHE);
            //GlobalObjects.IMarketDataCache = marketCache;
            //GlobalObjects.IMarketDataStreamer = mdr;

            //mdr.PerNodeParallelOperations = 1;
            //mdr.PerNodeBufferSize = 0;
            //mdr.AutoFlushFrequency = 1;
            //mdr.PerThreadBufferSize = 0;


            //var host = WebHost.CreateDefaultBuilder(webHostArgs)
            //    .UseContentRoot(pathToContentRoot)
            //    .UseStartup<Startup>()
            //    .Build();
            if (isService)
            {
                host.RunAsService();
            }
            else
            {
                host.Run();
            }
        }
        
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
