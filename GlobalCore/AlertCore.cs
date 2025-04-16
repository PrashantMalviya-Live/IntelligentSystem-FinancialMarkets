using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
//using Google.Protobuf.WellKnownTypes;
using GlobalLayer;
using GrpcPeerLoggerService;
using System.Collections.Concurrent;
using Google.Protobuf.WellKnownTypes;

namespace GlobalCore
{
    public static class AlertCore
    {
        private static GrpcChannel grpcChannel { get; set; } = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });
        public static void PublishAlert(Alert alert)
        {
            try
            {
                var client = new ServiceAlertManager.ServiceAlertManagerClient(grpcChannel);
                var response = client.Alert(GetAlertMessage(alert));
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                Logger.LogWrite("Logger Service Failed");
            }
        }
        private static AlertMessage GetAlertMessage(Alert alert)
        {
            return new AlertMessage()
            {
                Id = alert.ID.ToString(),
                AlertTriggerId = alert.AlertTriggerID,
                InstrumentToken = alert.InstrumentToken,
                TradingSymbol = alert.TradingSymbol,
                UserId = alert.UserId,
                Price = Convert.ToDouble(alert.LastPrice),
                CandleTimeSpan = alert.CandleTimeSpan,
                Message = alert.Message,
                 
            };
        }
    }
}
