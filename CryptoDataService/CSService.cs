using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LocalDBData;
using GlobalLayer;
namespace CryptoDataService
{
    public class CSService : BackgroundService
    {
        private readonly ILogger<CSService> _logger;
        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        public CSService(ILogger<CSService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogWrite("Starting Crypto Data Service");
            await StartDBUpdate();
            Logger.LogWrite("Market Crypto Service Started");
            manualReset.Wait();
        }

        internal async Task StartDBUpdate()
        {


#if MARKET || AWSMARKET
            await CryptoData.PublishData();
            System.Threading.Thread.Sleep(new TimeSpan(7, 30, 1));
#elif local || BACKTEST
            CryptoTickDataStreamer dataStreamer = new CryptoTickDataStreamer();
            Task<long> done = dataStreamer.BeginStreaming();
#endif
        }
    }
}
