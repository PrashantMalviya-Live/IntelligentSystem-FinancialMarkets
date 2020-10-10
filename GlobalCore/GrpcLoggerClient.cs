using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Threading;


namespace Global
{
    public class GrpcLoggerClient
    {
        public void CreateClient()
        {
            //Best practice is to reuse the channel once created. Client objects are light weight so can be created again and again.
            // The port number(5001) must match the port of the gRPC server.
            using var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
            {
                HttpHandler = new GrpcWebHandler(new HttpClientHandler())
            });
            var client = new Logger.LoggerClient(channel);
            //var reply = await client.Log();


            AsyncDuplexStreamingCall<Status, LogMessage> streamingCall = client.Log();

            await foreach (var message in streamingCall.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine(message.AlgoId);
            }


            //var response = streamingCall.ResponseStream;

            ////LogMessage status = response.Current;
            ////Console.WriteLine(status.AlgoId);

            //while (await response.MoveNext(CancellationToken.None))
            //{
            //    LogMessage status = response.Current;
            //    Console.WriteLine(status.AlgoId);
            //}

            //new LogMessage
            //{
            //    AlgoId = 1,
            //    AlgoInstance = 2,
            //    LogLevel = "information",
            //    LogTime = Timestamp.FromDateTime(DateTime.UtcNow),
            //    Message = "Test Message",
            //    MessengerMethod = "Dummy"
            //});

            Console.WriteLine("Log sent");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
