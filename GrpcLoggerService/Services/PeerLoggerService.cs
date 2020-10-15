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
    public class PeerLoggerService : ServiceLogger.ServiceLoggerBase
    {
        private GrpcLoggerService.LogMessage MessageConverter(LogMessage logMessage)
        {
            return new GrpcLoggerService.LogMessage
            {
                AlgoId = logMessage.AlgoId,
                AlgoInstance = logMessage.AlgoInstance,
                LogLevel = logMessage.LogLevel,
                LogTime = logMessage.LogTime,
                Message = logMessage.Message,
                MessengerMethod = logMessage.MessengerMethod
            };
        }

        public override Task<Status> Log(LogMessage request, ServerCallContext context)
        {
            try
            {
                LoggerService.CurrentLogMessage = MessageConverter(request);
                ClientLoggerRepository.AddLog(request);
                LoggerService.manualReset.Set();
                return Task.FromResult(new Status { Status_ = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new Status { Status_ = false });
            }
        }
    }
}
