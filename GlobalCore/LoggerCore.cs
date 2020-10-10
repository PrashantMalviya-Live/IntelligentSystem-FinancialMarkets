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
namespace GlobalCore
{
    public static class LoggerCore
    {
        //private static readonly IHttpClientFactory _clientFactory;

        private static GrpcChannel grpcChannel { get; set; } = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });

        public async static Task<bool> PublishLog(LogData log)
        {
            var client = new ServiceLogger.ServiceLoggerClient(grpcChannel);
            var response = await client.LogAsync(GetLogMessage(log));

            return response.Status_;
        }
        public async static Task<bool> PublishLog(int algoInstance, LogLevel logLevel, DateTime logTime, string message, string sourceMethod)
        {
            LogData log = new LogData
            {
                AlgoInstance = algoInstance,
                Level = logLevel,
                LogTime = logTime,
                Message = message,
                SourceMethod = sourceMethod
            };

            Task<bool> status = PublishLog(log);
            
            await status;
            
            return status.Result;
        }
        private static LogMessage GetLogMessage(LogData logData)
        {
            return new LogMessage()
            {
                AlgoId = 126,
                AlgoInstance = logData.AlgoInstance,
                LogLevel = System.Enum.GetName(typeof(GlobalLayer.LogLevel), logData.Level),
                LogTime = Timestamp.FromDateTime(logData.LogTime),
                Message = logData.Message,
                MessengerMethod = logData.Message
            };
        }
    }
}
