using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using GlobalLayer;

namespace GrpcLoggerService
{
    public class Program
    {
        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();

            //CreateHostBuilder(args).Build().Run();
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
                //.UseUrls(GlobalLayer.APIPORT.RSICross)
                .UseConfiguration(config)
                .UseStartup<Startup>()
                .Build();

            if (isService)
            {
                GlobalLayer.Logger.LogWrite("Starting GRPC Service");
                host.RunAsService();
                GlobalLayer.Logger.LogWrite("GRPC Service Started");
                manualReset.Wait();

            }
            else
            {
                host.Run();
            }

        }

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
