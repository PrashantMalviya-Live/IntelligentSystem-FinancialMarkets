using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcLoggerService
{
    public static class AlertRepository //: ILogRepository
    {
        public static List<AlertMessage> Alerts { get; set; } = new List<AlertMessage>();

        public static void AddAlert(AlertMessage alert)
        {
            Alerts.Add(alert);
        }

        public static void AddAlert(string id, int alertTriggerId, uint instrumentToken, 
            string tradingSymbol, int candleTimeSpan, string userId, string message, decimal price)
        {
            AlertMessage alert = new AlertMessage
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
            AddAlert(alert);
        }

        public static void ClearAlert()
        {
            Alerts.Clear();
        }
    }

    public interface IAlertRepository
    {
        public List<AlertMessage> Alerts { get; set; }
        public void AddAlert(AlertMessage alert);
        public void ClearAlert();
    }
}
