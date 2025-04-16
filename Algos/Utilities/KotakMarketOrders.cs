using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using DataAccess;
using BrokerConnectWrapper;
using GlobalCore;

namespace Algorithms.Utilities
{
    public class KotakMarketOrders
    {

        /// <summary>
        /// Place order and update database
        /// </summary>
        /// <param name="algoInstance"></param>
        /// <param name="tradingSymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <param name="orderType"></param>
        /// <returns></returns>
//        public static Order PlaceOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
//            bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = "", 
//            string product = Constants.PRODUCT_MIS, int disclosedQuantity = 0, decimal triggerPrice = 0)
//        {
//            decimal currentPrice = instrument_currentPrice;

//#if market
//            Dictionary<string, dynamic> orderStatus = null;
//            orderStatus = ZObjects.kotak.PlaceOrder(instrument_Token, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL,
//                quantity, Price: currentPrice, Product: product, OrderType: orderType, Validity: "GFD", disclosedQuantity, triggerPrice, Tag);
//            //}

//            KotakOrder korder = null;
//            Order order = null;
//            if (orderStatus != null && orderStatus["Success"] != null)
//            {
//                korder = new KotakOrder(orderStatus["Success"]["NSE"], disclosedQuantity, instrument_Token, "Complete", "Ordered", product,
//                    "GFD", Constants.VARIETY_REGULAR, orderType, tradingSymbol, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, algoInstance, (int) algoIndex);

//                //string orderId = Convert.ToString(orderStatus["Success"]["NSE"]["orderId"]);

//                order = new Order(korder);

//                //order = GetOrder(orderId, algoInstance, algoIndex,
//                //    orderType == Constants.ORDER_TYPE_SLM ? Constants.ORDER_STATUS_TRIGGER_PENDING : orderType == Constants.ORDER_TYPE_LIMIT ? Constants.ORDER_STATUS_OPEN : Constants.ORDER_STATUS_COMPLETE);

//                //orderTimestamp = Utils.StringToDate(orderStatus["data"]["order_timestamp"]);
//            }
//            else
//            {
//                throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
//            }
//#elif local

//            ///TEMP, REMOVE Later
//            if (currentPrice == 0)
//            {
//                DataLogic dl = new DataLogic();
//                currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
//                //order.AveragePrice = currentPrice;
//            }

//            Order order = new Order()
//            {
//                AlgoInstance = algoInstance,
//                OrderId = Guid.NewGuid().ToString(),
//                AveragePrice = currentPrice,
//                ExchangeTimestamp = tickTime,
//                OrderType = orderType,
//                Price = currentPrice,
//                Product = Constants.PRODUCT_NRML,
//                CancelledQuantity = 0,
//                FilledQuantity = quantity,
//                InstrumentToken = instrument_Token,
//                OrderTimestamp = tickTime,
//                Quantity = quantity,
//                Validity = Constants.VALIDITY_DAY,
//                TriggerPrice = currentPrice,
//                Tradingsymbol = tradingSymbol,
//                TransactionType = buyOrder ? "buy" : "sell",
//                Status = orderType == Constants.ORDER_TYPE_MARKET ? "Complete" : "Trigger Pending",
//                Variety = "regular",
//                 //if(Tag != null)
//                 //{
//                 //   order.Tag = Tag;
//                 //}
//                Tag = "Test",
//                AlgoIndex = (int)algoIndex,
//                StatusMessage = "Ordered",
//            };

//#endif
//            //ShortTrade trade = new ShortTrade();
//            //trade.InstrumentToken = instrument_Token;
//            //trade.TradingSymbol = tradingSymbol;
//            //trade.AveragePrice = order.AveragePrice;
//            //trade.ExchangeTimestamp = tickTime;// DateTime.Now;
//            //trade.TradeTime = orderTimestamp ?? tickTime ?? DateTime.Now;
//            //trade.Quantity = quantity;
//            //trade.OrderId = order.OrderId;
//            //trade.TransactionType = buyOrder ? "Buy" : "Sell";
//            //trade.TriggerID = Convert.ToInt32(AlgoIndex.VolumeThreshold);
//            //trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
//            //trade.InstrumentType = instrumenttype;

//            //UpdateTradeDetails(strategyID: 0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

//            if (Tag != null)
//            {
//                order.Tag = Tag;
//            }
//            UpdateOrderDetails(algoInstance, algoIndex, order);

//            return order;
//        }


        //public static Order GetOrder(string orderId, int algoInstance, AlgoIndex algoIndex, string status) // bool slOrder = false)
        //{
        //    Order oh = null;
        //    int counter = 0;

        //    while (true)
        //    {
        //        try
        //        {
        //            System.Threading.Thread.Sleep(400);

        //            List<Order> orderInfo = ZObjects.kotak.GetOrderHistory(orderId);


        //            oh = orderInfo[orderInfo.Count - 1];

        //            if (oh.Status == status)
        //            {
        //                //order.AveragePrice = oh.AveragePrice;
        //                break;
        //            }
        //            if (oh.Status == Constants.ORDER_STATUS_COMPLETE)
        //            {
        //                //order.AveragePrice = oh.AveragePrice;
        //                break;
        //            }
        //            //else if (oh.Status == Constants.ORDER_STATUS_TRIGGER_PENDING)
        //            //{
        //            //    //order.TriggerPrice = oh.TriggerPrice;
        //            //    break;
        //            //}
        //            //else 
        //            else if (oh.Status == Constants.ORDER_STATUS_REJECTED)
        //            {
        //                //_stopTrade = true;
        //                Logger.LogWrite("Order Rejected");
        //                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
        //                break;
        //                //throw new Exception("order did not execute properly");
        //            }
        //            if (counter > 150)
        //            {
        //                //_stopTrade = true;
        //                Logger.LogWrite("order did not execute properly. Waited for 1 minutes");
        //                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order did not go through. Waited for 10 minutes", "GetOrder");
        //                break;
        //                //throw new Exception("order did not execute properly. Waited for 10 minutes");
        //            }
        //            counter++;
        //        }
        //        catch (Exception ex)
        //        {
        //            Logger.LogWrite(ex.Message);
        //        }
        //    }
        //    oh.AlgoInstance = algoInstance;
        //    oh.AlgoIndex = Convert.ToInt32(algoIndex);
        //    return oh;
        //}

        //        //private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        //        //{
        //        //    DataLogic dl = new DataLogic();
        //        //    dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.VolumeThreshold, tradedLot, triggerID);
        //        //}

        public async static void UpdateOrderDetails(int algoInstance, AlgoIndex algoIndex, Order order, int strategyId = 0)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateOrder(algoInstance, order, strategyId);

            //LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order detailed updated in the database", "UpdateOrderDetails");
        }
    }
}
