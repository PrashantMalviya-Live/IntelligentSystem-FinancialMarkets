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
    public class PeerDataService : ServiceDataManager.ServiceDataManagerBase
    {
        private GrpcLoggerService.DataMessage MessageConverter(DataMessage dataMessage)
        {
            return new GrpcLoggerService.DataMessage
            {
                AlgoId = dataMessage.AlgoId,
                BaseInstrumentToken = dataMessage.BaseInstrumentToken,
                CandleTimeSpan = dataMessage.CandleTimeSpan,
                Message = dataMessage.Message,
                InstrumentToken = dataMessage.InstrumentToken,
                Price = Convert.ToDouble(dataMessage.Price),
            };
        }

        public override Task<DataStatus> Data(DataMessage request, ServerCallContext context)
        {
            try
            {
                DataService.CurrentDataMessage = MessageConverter(request);
                ClientDataRepository.AddData(request);
                DataService.manualReset.Set();
                return Task.FromResult(new DataStatus { Status = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new DataStatus { Status = false });
            }
        }
    }
}
