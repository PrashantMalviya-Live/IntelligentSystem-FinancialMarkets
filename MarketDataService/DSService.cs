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
using DBAccess;

namespace MarketDataService
{
    public class DSService : BackgroundService
    {
        private readonly ILogger<DSService> _logger;
        
        private readonly IRDSDAO _idAO;
        private readonly ITimeStreamDAO _iTimeStreamdAO;
        public DSService(IRDSDAO idAO, ITimeStreamDAO iTimeStreamdAO)
        {
            _idAO = idAO;
            _iTimeStreamdAO = iTimeStreamdAO;   
        }

        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        public DSService(ILogger<DSService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogWrite("Starting Market Data Service");
            StartDBUpdate();
            Logger.LogWrite("Market Data Service Started");
            manualReset.Wait();
        }

        internal void StartDBUpdate()
        {


#if market
            MarketData market = new MarketData(_idAO, _iTimeStreamdAO);
            market.PublishData();
            System.Threading.Thread.Sleep(new TimeSpan(7, 30, 1));
#elif local || BACKTEST
            TickDataStreamer dataStreamer = new TickDataStreamer();
            Task<long> done = dataStreamer.BeginStreaming();
#endif
        }
    }
}
