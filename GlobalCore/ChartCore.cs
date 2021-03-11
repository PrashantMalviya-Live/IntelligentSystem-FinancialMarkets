using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Google.Protobuf.WellKnownTypes;
using GlobalLayer;
using GrpcPeerLoggerService;
using System.Collections.Concurrent;

namespace GlobalCore
{
    public static class ChartCore
    {
        private static GrpcChannel grpcChannel { get; set; } = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });
        public static void Draw(ChartData cData)
        {
            try
            {
                var client = new Charter.CharterClient(grpcChannel);
                var response = client.DrawChart(GetChartData(cData));
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                Logger.LogWrite("Logger Service Failed");
            }
        }
        private static CData GetChartData(ChartData cdata)
        {
            return new CData()
            {
                AlgoId = cdata.AlgoId.ToString(),
                D = Convert.ToDouble(cdata.d),
                Arg = cdata.Arg,
                Xlabel = cdata.xLabel,
                Ylabel = cdata.yLabel,
                T = Timestamp.FromDateTime(DateTime.SpecifyKind(cdata.T, DateTimeKind.Utc))
            };
        }
    }
}
