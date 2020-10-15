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
    public static class OrderCore
    {
        private static GrpcChannel grpcChannel { get; set; } = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });
        public static void PublishOrder(Order order)
        {
            try
            {
                var client = new ServiceOrderAlerter.ServiceOrderAlerterClient(grpcChannel);
                var response = client.Publish(GetOrderMessage(order));
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                Logger.LogWrite("Logger Service Failed");
            }
        }
        private static OrderMessage GetOrderMessage(Order order)
        {
            return new OrderMessage()
            {
                Orderid = order.OrderId,
                InstrumentToken = order.InstrumentToken,
                TradingSymbol = order.Tradingsymbol,
                TransactionType = order.TransactionType,
                Price = Convert.ToDouble(order.Price),
                Quantity = order.Quantity,
                TriggerPrice = Convert.ToDouble(order.TriggerPrice),
                Status = order.Status,
                StatusMessage = order.StatusMessage,
                Algorithm = System.Enum.GetName(typeof(GlobalLayer.AlgoIndex), order.AlgoIndex),
                AlgoInstance = order.AlgoInstance,
                OrderType = order.OrderType,
                OrderTime = Timestamp.FromDateTime(DateTime.SpecifyKind(order.OrderTimestamp.Value, DateTimeKind.Utc))
            };
        }
    }
}
