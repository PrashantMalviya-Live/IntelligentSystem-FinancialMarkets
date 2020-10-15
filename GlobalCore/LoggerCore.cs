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
    public static class LoggerCore
    {
        //private static readonly IHttpClientFactory _clientFactory;
        private static bool loggerThreadStarted = false;
        private static BlockingCollection<LogData> _pendingLogs = new BlockingCollection<LogData>(100);
        private static void StartLoggerThread()
        {
            // A simple blocking consumer with no cancellation.
            Task.Run(() =>
            {
                while (!_pendingLogs.IsCompleted)
                {

                    LogData log = null;
                    // Blocks if dataItems.Count == 0.
                    // IOE means that Take() was called on a completed collection.
                    // Some other thread can call CompleteAdding after we pass the
                    // IsCompleted check but before we call Take.
                    // In this example, we can simply catch the exception since the
                    // loop will break on the next iteration.
                    try
                    {
                        log = _pendingLogs.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (log != null)
                    {
                        SendLogToGrpcServer(log);
                    }
                }
                //Console.WriteLine("\r\nNo more logs expected.");
            });
        }
        private static void ProducerThread(LogData log)
        {
            // A simple blocking producer with no cancellation.
            //Task.Run(() =>
            //{
            //    while (true)
            //    {
                    //Data data = GetData();
                    // Blocks if numbers.Count == dataItems.BoundedCapacity
                    _pendingLogs.Add(log);
                //}
                // Let consumer know we are done.
                _pendingLogs.CompleteAdding();
            //});
        }
        private static GrpcChannel grpcChannel { get; set; } = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(new HttpClientHandler())
        });

        private static void SendLogToGrpcServer(LogData log)
        {
            var client = new ServiceLogger.ServiceLoggerClient(grpcChannel);
            var response = client.Log(GetLogMessage(log));

            //return response.Status_;
        }
        public static void PublishLog(int algoInstance, AlgoIndex algoIndex, LogLevel logLevel, 
            DateTime logTime, string message, string sourceMethod)
        {
            try
            {
                if(!loggerThreadStarted)
                {
                    loggerThreadStarted = true;
                    StartLoggerThread();
                }

                LogData log = new LogData
                {
                    AlgoIndex = System.Enum.GetName(typeof(AlgoIndex), algoIndex),
                    AlgoInstance = algoInstance,
                    Level = logLevel,
                    LogTime = DateTime.SpecifyKind(logTime, DateTimeKind.Utc),
                    Message = message,
                    SourceMethod = sourceMethod
                };
                // Blocks if numbers.Count == dataItems.BoundedCapacity
                _pendingLogs.Add(log);
                // Let consumer know we are done.
                //_pendingLogs.CompleteAdding();
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                Logger.LogWrite("Logger Service Failed");
                //_pendingLogs.CompleteAdding();
            }
        }
        private static LogMessage GetLogMessage(LogData logData)
        {
            return new LogMessage()
            {
                AlgoId = logData.AlgoIndex,
                AlgoInstance = logData.AlgoInstance,
                //LogLevel = System.Enum.GetName(typeof(GlobalLayer.LogLevel), logData.Level),
                LogLevel = ((int)logData.Level).ToString(),
                LogTime = Timestamp.FromDateTime(logData.LogTime),
                Message = logData.Message,
                MessengerMethod = logData.SourceMethod
            };
        }
    }
}
