using Grpc.Net.Client.Web;
using Grpc.Net.Client;
using Grpc.Core;
using Azure.Core;
using GrpcProtos;
namespace StockMarketAlertsApp.Helper
{
    public class GrpcHelper

    { 
        //public delegate string LoggerMessageDelegate(LogMessage logMessage);
        //public static event LoggerMessageDelegate LoggerMessageEvent;

        //public delegate string OrderAlerterDelegate(OrderMessage orderMessage);
        //public static event OrderAlerterDelegate OrderAlerterEvent;

        //public static async Task GRPCLoggerAsync()
        //{
        //    var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        //    {
        //        HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        //    });

        //    var client = new Logger.LoggerClient(channel);


        //    AsyncDuplexStreamingCall<GrpcProtos.Status, LogMessage> streamingCall = client.Log();

        //    await foreach (var message in streamingCall.ResponseStream.ReadAllAsync())
        //    {
        //        Console.WriteLine(message.Message);
        //        LoggerMessageEvent(message);
        //    }
        //    //need thread blocking
        //}

        //public static async Task GRPCOrderAltererAsync()
        //{
        //    var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        //    {
        //        HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        //    });

        //    var client =  new OrderAlerter.OrderAlerterClient(channel);


        //    AsyncDuplexStreamingCall<GrpcProtos.PublishStatus, OrderMessage> streamingCall = client.Publish();

        //    await foreach (var message in streamingCall.ResponseStream.ReadAllAsync())
        //    {
        //        Console.WriteLine(message.Orderid);
        //        OrderAlerterEvent(message);
        //    }

        //    //need thread blocking
        //}

    }
}
