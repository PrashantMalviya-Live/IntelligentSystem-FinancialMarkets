using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcLoggerService
{
    public static class DataRepository //: ILogRepository
    {
        public static List<DataMessage> Datas { get; set; } = new List<DataMessage>();

        public static void AddData(DataMessage data)
        {
            Datas.Add(data);
        }

        public static void AddData(int algoId, uint baseInstrumentToken, uint instrumentToken,
            string tradingSymbol, int candleTimeSpan, string userId, string message, decimal price)
        {
            DataMessage data = new DataMessage
            { 
                AlgoId = algoId,
                BaseInstrumentToken = baseInstrumentToken,
                CandleTimeSpan = candleTimeSpan,
                Message = message,
                InstrumentToken = instrumentToken,
                Price = Convert.ToDouble(price),
            };
            AddData(data);
        }

        public static void ClearData()
        {
            Datas.Clear();
        }
    }

    public interface IDataRepository
    {
        public List<DataMessage> Datas { get; set; }
        public void AddData(DataMessage data);
        public void ClearData();
    }
}
