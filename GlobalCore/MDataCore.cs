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
    public static class MDataCore
    {
        private static GrpcChannel grpcChannel { get; set; } = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });
        public static void PublishMData(MData mdata)
        {
            try
            {
                var client = new ServiceDataManager.ServiceDataManagerClient(grpcChannel);
                var response = client.Data(GetMDataMessage(mdata));
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                Logger.LogWrite("Logger Service Failed");
            }
        }
        private static DataMessage GetMDataMessage(MData mdata)
        {
            return new DataMessage()
            {
                //Id = mdata.ID.ToString(),
                AlgoId = mdata.AlgoID,
                InstrumentToken = mdata.InstrumentToken,
                BaseInstrumentToken = mdata.BaseInstrumentToken,
                //TradingSymbol = mdata.TradingSymbol,
                //UserId = mdata.UserId,
                Price = Convert.ToDouble(mdata.LastPrice),
                CandleTimeSpan = mdata.CandleTimeSpan,

                //Send any data here with * seperated properties and : seperated key value pairs
                Message = mdata.Message,
            };
        }
    }
}
