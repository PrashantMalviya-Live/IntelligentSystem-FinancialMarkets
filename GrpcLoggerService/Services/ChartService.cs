using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using System.Diagnostics.Eventing.Reader;
using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using System.Timers;
using GrpcPeerLoggerService;

namespace GrpcLoggerService
{
    public class ChartService : Charter.CharterBase
    {
        public static CData CurrentChartMessage { get; set; }

        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        private bool _firstTime;
        public ChartService()
        {
            _firstTime = true;
        }
        public override async Task DrawChart(IAsyncStreamReader<CStatus> requestStream, 
            IServerStreamWriter<CData> responseStream, ServerCallContext context)
        {
            while (true)
            {
                try
                {
                    if(_firstTime)
                    {
                        manualReset.Set();
                        _firstTime = false;
                        foreach (CData message in ChartRepository.Charts)
                        {
                           await responseStream.WriteAsync(message);
                        }
                        manualReset.Reset();
                    }
                    manualReset.Wait();
                    await responseStream.WriteAsync(CurrentChartMessage);
                    ChartRepository.AddChart(CurrentChartMessage);
                    manualReset.Reset();
                }
                catch (Exception ex)
                {
                    break;
                }
            }
        }
    }
}
