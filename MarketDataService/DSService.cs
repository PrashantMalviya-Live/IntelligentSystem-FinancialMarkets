using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LocalDBData;
namespace MarketDataService
{
    public class DSService : BackgroundService
    {
        private readonly ILogger<DSService> _logger;

        public DSService(ILogger<DSService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            //    await Task.Delay(1000, stoppingToken);
            //}

            if (Debugger.IsAttached)
            {
                Task<long> status = new DSService(null).StartDBUpdate();
                if (status != null)
                {
                    status.Wait();
                }
                else
                {
                    System.Threading.Thread.Sleep(new TimeSpan(7, 30, 0));
                }
            }
        }

        internal Task<long> StartDBUpdate()
        {
            //string appSetting = ConfigurationManager.AppSettings["executionmode"];
            //if (appSetting == "market")
            //{
#if market
            MarketData.PublishData();
            System.Threading.Thread.Sleep(new TimeSpan(7, 30, 1));
            return null;
#elif local
            //}
            //else if (appSetting == "local")
            //{
            TickDataStreamer dataStreamer = new TickDataStreamer();
            Task<long> done = dataStreamer.BeginStreaming();
            return done;

#endif

            //}
            //return null;
        }
    }
}
