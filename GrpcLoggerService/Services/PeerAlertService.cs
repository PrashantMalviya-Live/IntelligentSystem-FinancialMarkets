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
    public class PeerAlertService : ServiceAlertManager.ServiceAlertManagerBase
    {
        private GrpcLoggerService.AlertMessage MessageConverter(AlertMessage alertMessage)
        {
            return new GrpcLoggerService.AlertMessage
            {
                AlertTriggerId = alertMessage.AlertTriggerId,
                CandleTimeSpan = alertMessage.CandleTimeSpan,
                Id = alertMessage.Id,
                Message = alertMessage.Message,
                UserId = alertMessage.UserId,
                InstrumentToken = alertMessage.InstrumentToken,
                TradingSymbol = alertMessage.TradingSymbol,
                Price = Convert.ToDouble(alertMessage.Price),
            };
        }

        public override Task<AlertStatus> Alert(AlertMessage request, ServerCallContext context)
        {
            try
            {
                AlertService.CurrentAlertMessage = MessageConverter(request);
                ClientAlertRepository.AddAlert(request);
                AlertService.manualReset.Set();
                return Task.FromResult(new AlertStatus { Status = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new AlertStatus { Status = false });
            }
        }
    }
}
