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
using GrpcLoggerService;

namespace GrpcPeerLoggerService
{
    public class PeerChartService : ServiceCharter.ServiceCharterBase
    {
        private GrpcLoggerService.CData MessageConverter(CData chartMessage)
        {
            return new GrpcLoggerService.CData
            {
                AlgoId = chartMessage.AlgoId,
                AlgoInstance = chartMessage.AlgoInstance,
                ChartdataId = chartMessage.ChartdataId,
                ChartId = chartMessage.ChartId,
                T = chartMessage.T,
                D = chartMessage.D,
                InstrumentToken = chartMessage.InstrumentToken,
                Arg = chartMessage.Arg,
                Arg2 = chartMessage.Arg2
            };
        }

        public override Task<CStatus> DrawChart(CData request, ServerCallContext context)
        {
            try
            {
                ChartService.CurrentChartMessage = MessageConverter(request);
                ClientChartRepository.AddChart(request);
                ChartService.manualReset.Set();
                return Task.FromResult(new CStatus { Status = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new CStatus { Status = false });
            }
        }
    }
}
