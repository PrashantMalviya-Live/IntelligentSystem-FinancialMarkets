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
    public class LoggerService : Logger.LoggerBase, ILoggerService
    {
        public static LogMessage CurrentLogMessage { get; set; }

        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        private bool _firstTime;
        public LoggerService ()
        {
            _firstTime = true;
        }
        public override async Task Log(IAsyncStreamReader<Status> requestStream, 
            IServerStreamWriter<LogMessage> responseStream, ServerCallContext context)
        {
            while (true)
            {
                try
                {
                    if(_firstTime)
                    {
                        manualReset.Set();
                        _firstTime = false;
                        foreach (LogMessage message in LoggerRepository.Logs)
                        {
                           await responseStream.WriteAsync(message);
                        }
                        manualReset.Reset();
                    }
                    manualReset.Wait();
                    await responseStream.WriteAsync(CurrentLogMessage);
                    LoggerRepository.AddLog(CurrentLogMessage);
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
