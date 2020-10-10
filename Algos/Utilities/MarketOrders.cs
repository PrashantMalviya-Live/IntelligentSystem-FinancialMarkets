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
            bool buyOrder, int quantity, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = null)
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
                order = GetOrder(orderId, algoInstance, orderType == Constants.ORDER_TYPE_SLM).Result;


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

            Order order = new Order (){
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
                TransactionType = buyOrder ? "buy": "sell",
                Status = orderType == Constants.ORDER_TYPE_MARKET? "Complete": "Trigger Pending",
                Variety = "regular",
                Tag = "Test"
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
            UpdateOrderDetails(algoInstance, order);

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
        public async static Task<Order> ModifyOrder(int algoInstance, decimal stoploss, string orderId, DateTime currentTime)
        {
            Order order = null;
            try
            {
#if market
            
                Dictionary<string, dynamic> orderStatus = ZObjects.kite.ModifyOrder(orderId, TriggerPrice: stoploss);
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    //order = new Order(orderStatus["data"]);
                    //string orderId = orderStatus["data"]["order_id"];
                    order = GetOrder(orderId, algoInstance, true).Result;

                    //orderId = orderStatus["data"]["order_id"];
                    //orderTimestamp = Utils.StringToDate(orderStatus["data"]["order_timestamp"]);
                }
                else
                {
                    //throw new Exception(string.Format("Modify Order status null for order id:{0}", orderId));
                    await LoggerCore.PublishLog(algoInstance, LogLevel.Error, currentTime, string.Format("Modify Order status null for order id:{0}", orderId), "ModifyOrder");
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
                order.AveragePrice = currentPrice;
                order.Price = currentPrice;
                order.TriggerPrice = currentPrice;
                order.ExchangeTimestamp = currentTime;
                order.OrderTimestamp = currentTime;

            //Order order = slOrder;

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

            UpdateOrderDetails(algoInstance, order);
            }
            catch (Exception ex)
            {
                await LoggerCore.PublishLog(algoInstance, LogLevel.Error, currentTime, "Order modification failed. Trade will continue.", "ModifyOrder");
            }
            return order;
        }

        public async static Task<Order> GetOrder(string orderId, int algoInstance, bool slOrder = false)
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
                    await LoggerCore.PublishLog(algoInstance, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
                    //throw new Exception("order did not execute properly");
                }
                if (counter > 3000)
                {
                    //_stopTrade = true;
                    Logger.LogWrite("order did not execute properly. Waited for 10 minutes");
                    await LoggerCore.PublishLog(algoInstance, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order did not go through. Waited for 10 minutes", "GetOrder");
                    throw new Exception("order did not execute properly. Waited for 10 minutes");
                }
                counter++;
            }
            return oh;
        }

        //private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        //{
        //    DataLogic dl = new DataLogic();
        //    dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.VolumeThreshold, tradedLot, triggerID);
        //}

        public async static void UpdateOrderDetails(int algoInstance, Order order, int strategyId = 0)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateOrder(algoInstance, order, strategyId);

            await LoggerCore.PublishLog(algoInstance, LogLevel.Info, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order detailed updated in the database", "UpdateOrderDetails");
        }
    }
}
