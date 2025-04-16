using FirebaseAdmin.Messaging;
using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcPeerLoggerService
{
    public static class ClientDataRepository
    {
        public static List<DataMessage> Datas { get; set; } = new List<DataMessage>();

        public static void AddData(DataMessage data)
        {
            Datas.Add(data);
        }

        public static void AddData(int algoId, uint baseInstrumentToken, uint instrumentToken,
            string tradingSymbol, int candleTimeSpan, string userId, string message, decimal price)
        {
            DataMessage dataMessage = new DataMessage
            {
                AlgoId = algoId,
                BaseInstrumentToken = baseInstrumentToken,
                CandleTimeSpan = candleTimeSpan,
                Message = message,
                InstrumentToken = instrumentToken,
                Price = Convert.ToDouble(price),
            };
            AddData(dataMessage);
        }

        public static void ClearLog()
        {
            Datas.Clear();
        }
    }

    public interface IClientDataRepository
    {
        public List<DataMessage> datas { get; set; }
        public void AddData(DataMessage data);
        public void ClearData();
    }
}
