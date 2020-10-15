using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrpcPeerLoggerService
{
    public static class ClientOrderRepository
    {
        public static List<OrderMessage> Orders { get; set; } = new List<OrderMessage>();

        public static void AddOrder(OrderMessage order)
        {
            Orders.Add(order);
        }

        public static void AddOrder(string orderid, string algorithm, int algoInstance, uint instrumentToken,
           DateTime orderTimeStamp, string orderType, decimal price, int quantity, string status,
           string statusMessage, string tradingSymbol, decimal triggerPrice, string transactionType)
        {
            OrderMessage orderMessage = new OrderMessage
            {
                Orderid = orderid,
                Algorithm = algorithm,
                AlgoInstance = algoInstance,
                InstrumentToken = instrumentToken,
                OrderTime = Timestamp.FromDateTime(orderTimeStamp),
                OrderType = orderType,
                Price = Convert.ToDouble(price),
                Quantity = quantity,
                Status = status,
                StatusMessage = statusMessage,
                TradingSymbol = tradingSymbol,
                TransactionType = transactionType,
                TriggerPrice = Convert.ToDouble(triggerPrice)
            };
            AddOrder(orderMessage);
        }

        public static void ClearLog()
        {
            Orders.Clear();
        }
    }

    public interface IClientOrderRepository
    {
        public List<OrderMessage> orders { get; set; }
        public void AddOrder(OrderMessage order);
        public void ClearOrder();
    }
}
