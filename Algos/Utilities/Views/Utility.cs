using Global.Web;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Google.Protobuf.WellKnownTypes;
namespace Algorithms.Utilities.Views
{
    public class ViewUtility
    {
        public static Order GetOrder(DataRow drOrders)
        {
            return new Order
            {
                OrderId = Convert.ToString(drOrders["OrderId"]),
                InstrumentToken = Convert.ToUInt32(drOrders["InstrumentToken"]),
                Tradingsymbol = (string)drOrders["TradingSymbol"],
                TransactionType = (string)drOrders["TransactionType"],
                AveragePrice = Convert.ToDecimal(drOrders["AveragePrice"]),
                Quantity = (int)drOrders["Quantity"],
                TriggerPrice = Convert.ToDecimal(drOrders["TriggerPrice"]),
                Status = (string)drOrders["Status"],
                StatusMessage = Convert.ToString(drOrders["StatusMessage"]),
                OrderType = Convert.ToString(drOrders["OrderType"]),
                OrderTimestamp = Convert.ToDateTime(drOrders["OrderTimeStamp"]),
                AlgoIndex = Convert.ToInt32(drOrders["AlgoIndex"]),
                AlgoInstance = Convert.ToInt32(drOrders["AlgoInstance"])
            };
        }
        public static OrderView GetOrderView(Order order)
        {
            return new OrderView
            {
                orderid = order.OrderId,
                instrumenttoken = order.InstrumentToken,
                tradingsymbol = order.Tradingsymbol.Trim(' '),
                transactiontype = order.TransactionType,
                status = order.Status,
                statusmessage = order.StatusMessage,
                price = order.OrderType == Constants.ORDER_TYPE_SLM ? order.TriggerPrice : order.AveragePrice,
                quantity = order.Quantity,
                triggerprice = order.TriggerPrice,
                algorithm = Convert.ToString((AlgoIndex)order.AlgoIndex),
                algoinstance = order.AlgoInstance,
                ordertime = Timestamp.FromDateTime(DateTime.SpecifyKind(order.OrderTimestamp.GetValueOrDefault(DateTime.Now),DateTimeKind.Utc)),
                ordertype = Convert.ToString(order.OrderType)
            };
        }
    }
}
