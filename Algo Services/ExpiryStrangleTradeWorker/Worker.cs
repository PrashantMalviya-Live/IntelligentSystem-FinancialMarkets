using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Global;
using AdvanceAlgos.Algorithms;
using ZeroMQMessaging;
using ZeroMQ;

namespace ExpiryStrangleTradeWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                //Subscribe to ZeroMQ for ticks
                //Call Algo and send ticks
                ZMQClient();
            }
        }
        public void ZMQClient()
        {
            ExpiryTrade expiryTrade = new ExpiryTrade();
            using (var context = new ZContext())
            using (var subscriber = new ZSocket(context, ZSocketType.SUB))
            {
                subscriber.Connect("tcp://127.0.0.1:5555");
                subscriber.Subscribe("");
                while (true)
                {
                    using (ZMessage message = subscriber.ReceiveMessage())
                    {
                        // Read envelope with address
                        byte[] tickData = message[0].Read();
                        Tick[] ticks = TickDataSchema.ParseTicks(tickData);
                        expiryTrade.OnNext(ticks);
                    }
                }
            }
        }
    }
}
