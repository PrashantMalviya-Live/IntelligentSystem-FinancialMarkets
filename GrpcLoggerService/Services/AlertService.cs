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
    public class AlertService : AlertManager.AlertManagerBase, IAlertService
    {
        public static AlertMessage CurrentAlertMessage { get; set; }

        public static ManualResetEventSlim manualReset = new ManualResetEventSlim();
        private bool _firstTime;
        public AlertService()
        {
            _firstTime = true;
        }
        public override async Task Alert(IAsyncStreamReader<AlertStatus> requestStream,
            IServerStreamWriter<AlertMessage> responseStream, ServerCallContext context)
        {
            while (true)
            {
                try
                {
                    if (_firstTime)
                    {
                        manualReset.Set();
                        _firstTime = false;
                        foreach (AlertMessage message in AlertRepository.Alerts)
                        {
                            await responseStream.WriteAsync(message);
                        }
                        manualReset.Reset();
                    }
                    manualReset.Wait();
                    await responseStream.WriteAsync(CurrentAlertMessage);
                    AlertRepository.AddAlert(CurrentAlertMessage);
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
