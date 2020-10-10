using Global;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GlobalCore
{
    public static class LoggerRepository //: ILogRepository
    {
        public static List<LogData> Logs { get; set; } = new List<LogData>();

        public static void AddLog(LogData log)
        {
            Logs.Add(log);
        }

        public static void AddLog(int algoInstance, LogLevel logLevel, DateTime logTime,
            string message, string source)
        {
            LogData logData = new LogData
            {
                AlgoInstance = algoInstance,
                Level = logLevel,
                LogTime = logTime,
                Message = message,
                SourceMethod = source
            };
            AddLog(logData);
        }

        public static void ClearLog()
        {
            Logs.Clear();
        }
    }

    public interface ILogRepository
    {
        public List<LogData> Logs { get; set; }
        public void AddLog(LogData log);
        public void ClearLog();
    }
}
