using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using DataAccess;
using ZConnectWrapper;
using GlobalCore;

namespace Algorithms.Utilities
{
    public class MarketOrders
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
        public static Order PlaceOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
            bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = null)
        {
            decimal currentPrice = instrument_currentPrice;

#if market

            Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_NRML,
                                      OrderType: orderType, Validity: Constants.VALIDITY_DAY, TriggerPrice: currentPrice);

            Order order = null;

            if (orderStatus != null && orderStatus["data"]["order_id"] != null)
            {
                //order = new Order(orderStatus["data"]);
                string orderId = orderStatus["data"]["order_id"];
                order = GetOrder(orderId, algoInstance, algoIndex, orderType == Constants.ORDER_TYPE_SLM).Result;


                //orderTimestamp = Utils.StringToDate(orderStatus["data"]["order_timestamp"]);
            }
            else
            {
                throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
            }
#elif local

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
                //order.AveragePrice = currentPrice;
            }

            Order order = new Order()
            {
                AlgoInstance = algoInstance,
                OrderId = Guid.NewGuid().ToString(),
                AveragePrice = currentPrice,
                ExchangeTimestamp = tickTime,
                OrderType = orderType,
                Price = currentPrice,
                Product = Constants.PRODUCT_NRML,
                CancelledQuantity = 0,
                FilledQuantity = quantity,
                InstrumentToken = instrument_Token,
                OrderTimestamp = tickTime,
                Quantity = quantity,
                Validity = Constants.VALIDITY_DAY,
                TriggerPrice = currentPrice,
                Tradingsymbol = tradingSymbol,
                TransactionType = buyOrder ? "buy" : "sell",
                Status = orderType == Constants.ORDER_TYPE_MARKET ? "Complete" : "Trigger Pending",
                Variety = "regular",
                Tag = "Test",
                AlgoIndex = (int)algoIndex,
                StatusMessage = "Ordered",
            };

            
            
#endif
            //ShortTrade trade = new ShortTrade();
            //trade.InstrumentToken = instrument_Token;
            //trade.TradingSymbol = tradingSymbol;
            //trade.AveragePrice = order.AveragePrice;
            //trade.ExchangeTimestamp = tickTime;// DateTime.Now;
            //trade.TradeTime = orderTimestamp ?? tickTime ?? DateTime.Now;
            //trade.Quantity = quantity;
            //trade.OrderId = order.OrderId;
            //trade.TransactionType = buyOrder ? "Buy" : "Sell";
            //trade.TriggerID = Convert.ToInt32(AlgoIndex.VolumeThreshold);
            //trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            //trade.InstrumentType = instrumenttype;

            //UpdateTradeDetails(strategyID: 0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

            if(Tag != null)
            {
                order.Tag = Tag;
            }
            UpdateOrderDetails(algoInstance, algoIndex, order);

            return order;
        }

        /// <summary>
        /// Modify existing order. This is used to change the SL of existing order
        /// </summary>
        /// <param name="instrument_tradingsymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <returns></returns>
        public async static Task<Order> ModifyOrder(int algoInstance, AlgoIndex algoIndex, decimal stoploss, Order slOrder, DateTime currentTime)
        {
            string orderId = slOrder.OrderId;
            Order order = null;
            try
            {
#if market
                Dictionary<string, dynamic> orderStatus;
                if (stoploss == 0)
                {
                    orderStatus = ZObjects.kite.ModifyOrder(orderId, OrderType:Constants.ORDER_TYPE_MARKET);
                }
                else
                {
                    orderStatus = ZObjects.kite.ModifyOrder(orderId, TriggerPrice: stoploss);
                }
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    //order = new Order(orderStatus["data"]);
                    //string orderId = orderStatus["data"]["order_id"];
                    order = GetOrder(orderId, algoInstance, algoIndex, true).Result;

                    //orderId = orderStatus["data"]["order_id"];
                    //orderTimestamp = Utils.StringToDate(orderStatus["data"]["order_timestamp"]);
                }
                else
                {
                    //throw new Exception(string.Format("Modify Order status null for order id:{0}", orderId));
                    LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, string.Format("Modify Order status null for order id:{0}", orderId), "ModifyOrder");
                }
           

#elif local
                decimal currentPrice = stoploss;
                // CurrentPostion = sl;
                ///TEMP, REMOVE Later
                //if (currentPrice == 0)
                //{
                //    DataLogic dl = new DataLogic();
                //    currentPrice = dl.RetrieveLastPrice(instrumentToken, slOrder.OrderTimestamp, false);
                //}
                slOrder.AveragePrice = currentPrice;
                slOrder.Price = currentPrice;
                slOrder.TriggerPrice = currentPrice;
                slOrder.ExchangeTimestamp = currentTime;
                slOrder.OrderTimestamp = currentTime;

            order = slOrder;

#endif


            //ShortTrade trade = new ShortTrade();
            //trade.InstrumentToken = instrument_Token;
            //trade.TradingSymbol = tradingSymbol;
            //trade.AveragePrice = order.AveragePrice;
            //trade.ExchangeTimestamp = tickTime;// DateTime.Now;
            //trade.TradeTime = orderTimestamp ?? tickTime ?? DateTime.Now;
            //trade.Quantity = quantity;
            //trade.OrderId = order.OrderId;
            //trade.TransactionType = buyOrder ? "Buy" : "Sell";
            //trade.TriggerID = Convert.ToInt32(AlgoIndex.VolumeThreshold);
            //trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            //trade.InstrumentType = instrumenttype;

            //UpdateTradeDetails(strategyID: 0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

            UpdateOrderDetails(algoInstance, algoIndex, order);
            }
            catch (Exception ex)
            {
                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, "Order modification failed. Trade will continue.", "ModifyOrder");
            }
            return order;
        }

        /// <summary>
        /// Modify existing order. This is used to change the SL of existing order
        /// </summary>
        /// <param name="instrument_tradingsymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <returns></returns>
        public async static Task CancelOrder(int algoInstance, AlgoIndex algoIndex, Order order, DateTime currentTime)
        {
            try
            {
#if market
                string orderId = order.OrderId;
                Dictionary<string, dynamic> orderStatus;
                    orderStatus = ZObjects.kite.CancelOrder(orderId);
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    order = GetOrder(orderId, algoInstance, algoIndex, true).Result;
                }
#endif
#if local
                order.Status = Constants.ORDER_STATUS_CANCELLED;
#endif
                UpdateOrderDetails(algoInstance, algoIndex, order);
            }
            catch (Exception ex)
            {
                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, "Order cancellation failed. Trade will continue.", "CancelOrder");
            }
        }


        public async static Task<Order> GetOrder(string orderId, int algoInstance, AlgoIndex algoIndex, bool slOrder = false)
        {
            Order oh = null;
            int counter = 0;

            while (true)
            {
                System.Threading.Thread.Sleep(200);
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);

                oh = orderInfo[orderInfo.Count - 1];

                if (oh.Status.ToLower() == "complete" && !slOrder)
                {
                    //order.AveragePrice = oh.AveragePrice;
                    break;
                }
                else if (oh.Status.ToLower() == "trigger pending" && slOrder)
                {
                    //order.TriggerPrice = oh.TriggerPrice;
                    break;
                }
                else if (oh.Status.ToLower() == "rejected")
                {
                    //_stopTrade = true;
                    Logger.LogWrite("order did not execute properly");
                    LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
                    //throw new Exception("order did not execute properly");
                }
                if (counter > 3000)
                {
                    //_stopTrade = true;
                    Logger.LogWrite("order did not execute properly. Waited for 10 minutes");
                    LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order did not go through. Waited for 10 minutes", "GetOrder");
                    throw new Exception("order did not execute properly. Waited for 10 minutes");
                }
                counter++;
            }
            oh.AlgoInstance = algoInstance;
            oh.AlgoIndex = Convert.ToInt32(algoIndex);
            return oh;
        }

        //private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        //{
        //    DataLogic dl = new DataLogic();
        //    dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.VolumeThreshold, tradedLot, triggerID);
        //}

        public async static void UpdateOrderDetails(int algoInstance, AlgoIndex algoIndex, Order order, int strategyId = 0)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateOrder(algoInstance, order, strategyId);

            LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order detailed updated in the database", "UpdateOrderDetails");
        }
    }
}
