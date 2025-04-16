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
    public static class ClientAlertRepository
    {
        public static List<AlertMessage> Alerts { get; set; } = new List<AlertMessage>();

        public static void AddAlert(AlertMessage alert)
        {
            Alerts.Add(alert);
        }

        public static void AddAlert(string id, int alertTriggerId, uint instrumentToken,
            string tradingSymbol, int candleTimeSpan, string userId, string message, decimal price)
        {
            AlertMessage alertMessage = new AlertMessage
            {
                AlertTriggerId = alertTriggerId,
                UserId = userId,
                CandleTimeSpan = candleTimeSpan,
                Id = id,
                Message = message,
                InstrumentToken = instrumentToken,
                Price = Convert.ToDouble(price),
                TradingSymbol = tradingSymbol

            };
            AddAlert(alertMessage);
        }

        public static void ClearLog()
        {
            Alerts.Clear();
        }
    }

    public interface IClientAlertRepository
    {
        public List<AlertMessage> alerts { get; set; }
        public void AddAlert(AlertMessage alert);
        public void ClearAlert();
    }
}
