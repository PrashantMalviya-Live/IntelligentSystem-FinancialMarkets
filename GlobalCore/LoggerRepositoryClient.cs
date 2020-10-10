using Global;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrpcPeerLoggerService;
namespace GlobalCore
{
    public static class ClientLoggerRepository //: ILogRepository
    {
        public static List<LogMessage> Logs { get; set; } = new List<LogMessage>();

        public static void AddLog(LogMessage log)
        {
            Logs.Add(log);
        }

        public static void AddLog(int algoInstance, LogLevel logLevel, DateTime logTime,
            string message, string source)
        {
            LogMessage logMessage = new LogMessage
            {
                AlgoInstance = algoInstance,
                LogLevel = Convert.ToString(logLevel),
                LogTime = Timestamp.FromDateTime(logTime),
                Message = message,
                MessengerMethod = source
            };
            AddLog(logMessage);
        }

        public static void ClearLog()
        {
            Logs.Clear();
        }
    }

    public interface IClientLogRepository
    {
        public List<LogMessage> Logs { get; set; }
        public void AddLog(LogMessage log);
        public void ClearLog();
    }
}
