using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlobalLayer;
using DataAccess;
using BrokerConnectWrapper;
using GlobalCore;
using System.Net.Http;
using System.Net.Http.Headers;

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
        public static Order PlaceZerodhaOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
            bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = null, 
            string product = Constants.PRODUCT_MIS, int disclosedQuantity = 0, decimal triggerPrice = 0)
        {
            decimal currentPrice = instrument_currentPrice;

#if market
            Dictionary<string, dynamic> orderStatus = null;
            // string product = algoIndex == AlgoIndex.ExpiryTrade ? Constants.PRODUCT_MIS : Constants.PRODUCT_NRML;
            if (orderType == Constants.ORDER_TYPE_SLM)
            {
                orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity,
                                      Product: product, OrderType: orderType, Validity: Constants.VALIDITY_DAY,
                                      TriggerPrice: currentPrice);
            }
            else if (orderType == Constants.ORDER_TYPE_SL)
            {
                orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity,
                                      Price: currentPrice, Product: product, OrderType: orderType, Validity: Constants.VALIDITY_DAY,
                                      TriggerPrice: currentPrice);
            }
            else
            {
                orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
                                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity,
                                          Price: currentPrice, Product: product,
                                          OrderType: orderType, Validity: Constants.VALIDITY_DAY);
            }

            Order order = null;

            if (orderStatus != null && orderStatus["data"]["order_id"] != null)
            {
                //order = new Order(orderStatus["data"]);
                string orderId = orderStatus["data"]["order_id"];
                order = GetOrder(orderId, algoInstance, algoIndex,
                    orderType == Constants.ORDER_TYPE_SLM ? Constants.ORDER_STATUS_TRIGGER_PENDING : orderType == Constants.ORDER_TYPE_LIMIT ? Constants.ORDER_STATUS_OPEN : Constants.ORDER_STATUS_COMPLETE);

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
                 //if(Tag != null)
                 //{
                 //   order.Tag = Tag;
                 //}
                //Tag = "Test",
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

            if (Tag != null)
            {
                order.Tag = Tag;
            }
            UpdateOrderDetails(algoInstance, algoIndex, order);

            return order;
        }

        public static Order PlaceOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
           bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = "",
           string product = Constants.PRODUCT_NRML, int disclosedQuantity = 0, decimal triggerPrice = 0, int broker = Constants.KOTAK, string HsServerId="", 
           HttpClient httpClient = null, User user = null)
        {
            Order order = null;
            switch (broker)
            {
                case Constants.ZERODHA :
                    order = PlaceZerodhaOrder(algoInstance, tradingSymbol, instrumenttype, instrument_currentPrice, instrument_Token,
                        buyOrder, quantity, algoIndex, tickTime, orderType, Tag, product, disclosedQuantity, triggerPrice);
                    break;

                case Constants.KOTAK:
                    order = PlaceKotakOrder(algoInstance, tradingSymbol, instrumenttype, instrument_currentPrice, instrument_Token, 
                        buyOrder, quantity, algoIndex, tickTime, orderType, Tag, product, disclosedQuantity, triggerPrice, HsServerId, httpClient, user);
                    break;
            }
            return order;
        }
        //public static string PlaceKotakOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
        //  bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = "",
        //  string product = Constants.PRODUCT_MIS, int disclosedQuantity = 0, decimal triggerPrice = 0, int broker = Constants.ZERODHA)
        //{
        //    return PlaceInstantKotakOrder(algoInstance, tradingSymbol, instrumenttype, instrument_currentPrice, instrument_Token,
        //                buyOrder, quantity, algoIndex, tickTime, orderType, Tag, product, disclosedQuantity, triggerPrice);
        //}
        public static Order GetOrderDetails(int algoInstance, string tradingSymbol, AlgoIndex algoIndex, string orderId, 
            DateTime tickTime, decimal currentPrice, uint instrumentToken, bool buyOrder, string orderType, int quantity, 
            string Tag="", HttpClient httpClient = null)
        {
#if market
            Order order = GetKotakOrder(orderId, algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, tradingSymbol, httpClient);
#elif local
            if (currentPrice == 0)
            {
                currentPrice = 10;
                // DataLogic dl = new DataLogic();
                // currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
                //order.AveragePrice = currentPrice;
            }
            orderId = Guid.NewGuid().ToString();
            //System.Threading.Thread.Sleep(5000);
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
                InstrumentToken = instrumentToken,
                OrderTimestamp = tickTime,
                Quantity = quantity,
                Validity = Constants.VALIDITY_DAY,
                TriggerPrice = currentPrice,
                Tradingsymbol = tradingSymbol,
                TransactionType = buyOrder ? "buy" : "sell",
                Status = orderType == Constants.ORDER_TYPE_MARKET ? "Complete" : "Trigger Pending",
                Variety = "regular",
                //if(Tag != null)
                //{
                //   order.Tag = Tag;
                //}
                //Tag = "Test",
                AlgoIndex = (int)algoIndex,
                StatusMessage = "Ordered",
            };
#endif
#if BACKTEST
            Tag = "BackTest";
            if (Tag != null)
            {
                order.Tag = Tag;
            }
#endif
            UpdateOrderDetails(algoInstance, algoIndex, order);

            return order;
        }
        /// <summary>
        /// Returns order id of the placed order
        /// </summary>
        /// <param name="algoInstance"></param>
        /// <param name="tradingSymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="algoIndex"></param>
        /// <param name="tickTime"></param>
        /// <param name="orderType"></param>
        /// <param name="Tag"></param>
        /// <param name="product"></param>
        /// <param name="disclosedQuantity"></param>
        /// <param name="triggerPrice"></param>
        /// <returns></returns>
        public static string PlaceInstantKotakOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
          bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = "",
          string product = Constants.PRODUCT_MIS, int disclosedQuantity = 0, decimal triggerPrice = 0, string HsServerid="", HttpClient httpClient = null)
        {
            decimal currentPrice = instrument_currentPrice;
            string orderId;
#if market
            //Dictionary<string, dynamic> orderStatus = null;
            ZObjects.kotak ??= new KotakConnect.Kotak();
            Dictionary<string, dynamic> orderStatus = ZObjects.kotak.PlaceOrder(instrument_Token, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL,
                quantity, Price: currentPrice, Product: product, OrderType: orderType, Validity: "GFD", disclosedQuantity, triggerPrice, Tag, tradingSymbol, HsServerid, httpClient: httpClient);
            //}

            //<table summary="Response Details" border="1">
            //<tr><td> Status Code </td><td> Description </td><td> Response Headers </td></tr>
            //<tr><td> 200 </td><td> Order modified successfully </td><td>  -  </td></tr>
            //<tr><td> 400 </td><td> Invalid or missing input parameters </td><td>  -  </td></tr>
            //<tr><td> 403 </td><td> Invalid session, please re-login to continue </td><td>  -  </td></tr>
            //<tr><td> 429 </td><td> Too many requests to the API </td><td>  -  </td></tr>
            //<tr><td> 500 </td><td> Unexpected error </td><td>  -  </td></tr>
            //<tr><td> 502 </td><td> Not able to communicate with OMS </td><td>  -  </td></tr>
            //<tr><td> 503 </td><td> Trade API service is unavailable </td><td>  -  </td></tr>
            //<tr><td> 504 </td><td> Gateway timeout, trade API is unreachable </td><td>  -  </td></tr>
            //</table>
            if (orderStatus != null && orderStatus.ContainsKey("Success") && orderStatus["Success"] != null)
            {
                Dictionary<string, dynamic> data = orderStatus["Success"]["NSE"];

                orderId = Convert.ToString(data["orderId"]);

               // order = GetKotakOrder(orderId, algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, tradingSymbol);
            }
            else
            {
                throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
            }
#elif local

            ///TEMP, REMOVE Later

            //if (currentPrice == 0)
            //{
            //    currentPrice = 10;
            //   // DataLogic dl = new DataLogic();
            //   // currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
            //    //order.AveragePrice = currentPrice;
            //}
            orderId = Guid.NewGuid().ToString();
            //System.Threading.Thread.Sleep(5000);
            //Order order = new Order()
            //{
            //    AlgoInstance = algoInstance,
            //    OrderId = Guid.NewGuid().ToString(),
            //    AveragePrice = currentPrice,
            //    ExchangeTimestamp = tickTime,
            //    OrderType = orderType,
            //    Price = currentPrice,
            //    Product = Constants.PRODUCT_NRML,
            //    CancelledQuantity = 0,
            //    FilledQuantity = quantity,
            //    InstrumentToken = instrument_Token,
            //    OrderTimestamp = tickTime,
            //    Quantity = quantity,
            //    Validity = Constants.VALIDITY_DAY,
            //    TriggerPrice = currentPrice,
            //    Tradingsymbol = tradingSymbol,
            //    TransactionType = buyOrder ? "buy" : "sell",
            //    Status = orderType == Constants.ORDER_TYPE_MARKET ? "Complete" : "Trigger Pending",
            //    Variety = "regular",
            //     //if(Tag != null)
            //     //{
            //     //   order.Tag = Tag;
            //     //}
            //    Tag = "Test",
            //    AlgoIndex = (int)algoIndex,
            //    StatusMessage = "Ordered",
            //};

#endif
            //if (Tag != null)
            //{
            //    order.Tag = Tag;
            //}
            //UpdateOrderDetails(algoInstance, algoIndex, order);

            return orderId;
        }
        public static async Task<Order> PlaceKotakOrderAsync(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
           bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = "",
           string product = Constants.PRODUCT_MIS, int disclosedQuantity = 0, decimal triggerPrice = 0, HttpClient httpClient = null)
        {
            decimal currentPrice = instrument_currentPrice;

#if market
            ZObjects.kotak ??= new KotakConnect.Kotak();
            Dictionary<string, dynamic> orderStatus =  ZObjects.kotak.PlaceOrder(instrument_Token, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL,
                quantity, Price: currentPrice, Product: product, OrderType: orderType, Validity: "GFD", disclosedQuantity, triggerPrice, Tag, tradingSymbol, httpClient: httpClient);

            //orderStatusTask.Wait();

            //Dictionary<string, dynamic> orderStatus = orderStatusTask.Result;

            //}

            //<table summary="Response Details" border="1">
            //<tr><td> Status Code </td><td> Description </td><td> Response Headers </td></tr>
            //<tr><td> 200 </td><td> Order modified successfully </td><td>  -  </td></tr>
            //<tr><td> 400 </td><td> Invalid or missing input parameters </td><td>  -  </td></tr>
            //<tr><td> 403 </td><td> Invalid session, please re-login to continue </td><td>  -  </td></tr>
            //<tr><td> 429 </td><td> Too many requests to the API </td><td>  -  </td></tr>
            //<tr><td> 500 </td><td> Unexpected error </td><td>  -  </td></tr>
            //<tr><td> 502 </td><td> Not able to communicate with OMS </td><td>  -  </td></tr>
            //<tr><td> 503 </td><td> Trade API service is unavailable </td><td>  -  </td></tr>
            //<tr><td> 504 </td><td> Gateway timeout, trade API is unreachable </td><td>  -  </td></tr>
            //</table>
            KotakOrder korder = null;
            Order order = null;
            if (orderStatus != null && orderStatus.ContainsKey("Success") && orderStatus["Success"] != null)
            {
                Dictionary<string, dynamic> data = orderStatus["Success"]["NSE"];

                string orderId = Convert.ToString(data["orderId"]);

                order = GetKotakOrder(orderId, algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, tradingSymbol, httpClient: httpClient);

                //korder = new KotakOrder(orderStatus["Success"]["NSE"], disclosedQuantity, instrument_Token, "Complete", "Ordered",  product,
                //    "GFD", Constants.VARIETY_REGULAR, orderType, tradingSymbol, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, algoInstance, (int)algoIndex);

                //order = new Order(korder);
            }
            else
            {
                throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
            }
#elif local

            ///TEMP, REMOVE Later
            
            if (currentPrice == 0)
            {
                currentPrice = 10;
               // DataLogic dl = new DataLogic();
               // currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
                //order.AveragePrice = currentPrice;
            }
            //System.Threading.Thread.Sleep(5000);
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
                 //if(Tag != null)
                 //{
                 //   order.Tag = Tag;
                 //}
                Tag = "Test",
                AlgoIndex = (int)algoIndex,
                StatusMessage = "Ordered",
            };

#endif
            if (Tag != null)
            {
                order.Tag = Tag;
            }
            UpdateOrderDetails(algoInstance, algoIndex, order);
            return order;
        }

        public static Order PlaceKotakOrder(int algoInstance, string tradingSymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
           bool buyOrder, int quantity, AlgoIndex algoIndex, DateTime? tickTime = null, string orderType = Constants.ORDER_TYPE_MARKET, string Tag = "",
           string product = Constants.PRODUCT_NRML, int disclosedQuantity = 0, decimal triggerPrice = 0, string HsServerId ="", HttpClient httpClient = null, User user = null)
        {
            decimal currentPrice = instrument_currentPrice;

#if market
            ZObjects.kotak ??= new KotakConnect.Kotak();

            Dictionary<string, dynamic> orderStatus = ZObjects.kotak.PlaceOrder(instrument_Token, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL,
                quantity, Price: currentPrice, Product: product, OrderType: orderType, Validity: "GFD", disclosedQuantity, triggerPrice, Tag: Tag, TradingSymbol: tradingSymbol, 
                HsServerId: HsServerId, httpClient: httpClient, user: user);

            //orderStatusTask.Wait();
            //Dictionary<string, dynamic> orderStatus = orderStatusTask.Result;
            //}

            //<table summary="Response Details" border="1">
            //<tr><td> Status Code </td><td> Description </td><td> Response Headers </td></tr>
            //<tr><td> 200 </td><td> Order modified successfully </td><td>  -  </td></tr>
            //<tr><td> 400 </td><td> Invalid or missing input parameters </td><td>  -  </td></tr>
            //<tr><td> 403 </td><td> Invalid session, please re-login to continue </td><td>  -  </td></tr>
            //<tr><td> 429 </td><td> Too many requests to the API </td><td>  -  </td></tr>
            //<tr><td> 500 </td><td> Unexpected error </td><td>  -  </td></tr>
            //<tr><td> 502 </td><td> Not able to communicate with OMS </td><td>  -  </td></tr>
            //<tr><td> 503 </td><td> Trade API service is unavailable </td><td>  -  </td></tr>
            //<tr><td> 504 </td><td> Gateway timeout, trade API is unreachable </td><td>  -  </td></tr>
            //</table>
            KotakOrder korder = null;
            Order order = null;
            if (orderStatus != null && orderStatus.ContainsKey("stCode") && orderStatus["stCode"] == 200)
            {
                string orderId = Convert.ToString(orderStatus["nOrdNo"]);

                order = GetKotakOrder(orderId, algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, tradingSymbol, httpClient: httpClient, user: user);

                //korder = new KotakOrder(orderStatus["Success"]["NSE"], disclosedQuantity, instrument_Token, "Complete", "Ordered",  product,
                //    "GFD", Constants.VARIETY_REGULAR, orderType, tradingSymbol, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, algoInstance, (int)algoIndex);

                //order = new Order(korder);
            }
            else
            {
                throw new Exception(orderStatus["fault"]["message"]);//   string.Format("Place Order status null for algo instance:{0}", algoInstance));
                //throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
            }
#elif local

            ///TEMP, REMOVE Later

            if (currentPrice == 0)
            {
                currentPrice = 10;
                //DataLogic dl = new DataLogic();
                //currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
               //order.AveragePrice = currentPrice;
            }
            //System.Threading.Thread.Sleep(5000);
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
                 //if(Tag != null)
                 //{
                 //   order.Tag = Tag;
                 //}
                Tag = Tag,
                AlgoIndex = (int)algoIndex,
                StatusMessage = "Ordered",
            };

#endif
#if BACKTEST
            //Tag = "BackTest";
#endif
#if local
            //Tag = "PaperTrade";
#endif
            if (Tag != "")
            {
                order.Tag = Tag;
            }

            UpdateOrderDetails(algoInstance, algoIndex, order);
            return order;
        }

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
        //                korder = new KotakOrder(orderStatus["Success"]["NSE"], disclosedQuantity, instrument_Token, product,
        //                    "GFD", Constants.VARIETY_REGULAR, orderType, tradingSymbol, buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, algoInstance, (int)algoIndex);

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
        public static Order ModifyKotakOrder(int algoInstance, AlgoIndex algoIndex, decimal stoploss, Order slOrder, DateTime currentTime,int quantity, decimal currentmarketPrice = 0)
        {
            string orderId = slOrder.OrderId;
            Order order = null;
            try
            {
#if market
                Dictionary<string, dynamic> orderStatus;
                //if (stoploss == 0)
                //{
                //    //orderStatus = ZObjects.kite.ModifyOrder(orderId, OrderType:Constants.ORDER_TYPE_MARKET);
                //}
                //else
                //{
                //orderStatus = ZObjects.kite.ModifyOrder(orderId, TriggerPrice: stoploss);
                ZObjects.kotak ??= new KotakConnect.Kotak();
                orderStatus = ZObjects.kotak.ModifyOrder(orderId, Price: stoploss, Quantity:quantity, TriggerPrice: stoploss);

                //}
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    //order = new Order(orderStatus["data"]);
                    //string orderId = orderStatus["data"]["order_id"];
                    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_TRIGGER_PENDING);

                    //orderId = orderStatus["data"]["order_id"];
                    //orderTimestamp = Utils.StringToDate(orderStatus["data"]["order_timestamp"]);
                }
                else
                {
                    //throw new Exception(string.Format("Modify Order status null for order id:{0}", orderId));
                    LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, string.Format("Modify Order status null for order id:{0}", orderId), "ModifyOrder");
                }
           

#elif local
                decimal currentPrice = stoploss == 0 ? currentmarketPrice : stoploss;
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
        public static Order ModifyOrder(int algoInstance, AlgoIndex algoIndex, decimal stoploss, Order slOrder, DateTime currentTime, int quantity=0, decimal currentmarketPrice = 0)
        {
            string orderId = slOrder.OrderId;
            Order order = null;
            try
            {
#if market
                Dictionary<string, dynamic> orderStatus;
                //if (stoploss == 0)
                //{
                //    //orderStatus = ZObjects.kite.ModifyOrder(orderId, OrderType:Constants.ORDER_TYPE_MARKET);
                //}
                //else
                //{
                //orderStatus = ZObjects.kite.ModifyOrder(orderId, TriggerPrice: stoploss);
                ZObjects.kotak ??= new KotakConnect.Kotak();
                orderStatus = ZObjects.kotak.ModifyOrder(orderId, Price: stoploss, Quantity: quantity, TriggerPrice: stoploss);

                //}
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    //order = new Order(orderStatus["data"]);
                    //string orderId = orderStatus["data"]["order_id"];
                    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_TRIGGER_PENDING);

                    //orderId = orderStatus["data"]["order_id"];
                    //orderTimestamp = Utils.StringToDate(orderStatus["data"]["order_timestamp"]);
                }
                else
                {
                    //throw new Exception(string.Format("Modify Order status null for order id:{0}", orderId));
                    LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, string.Format("Modify Order status null for order id:{0}", orderId), "ModifyOrder");
                }


#elif local
                decimal currentPrice = stoploss == 0 ? currentmarketPrice : stoploss;
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
        /// Modify existing order. This is used to change the order product type
        /// </summary>
        /// <param name="instrument_tradingsymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <returns></returns>
        public async static Task<Order> ModifyOrder(int algoInstance, AlgoIndex algoIndex, Order order, string product, DateTime currentTime)
        {
            string orderId = order.OrderId;
            try
            {
#if market
                Dictionary<string, dynamic> orderStatus;
                orderStatus = ZObjects.kite.ModifyOrder(OrderId: orderId, Product: product);
                
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_TRIGGER_PENDING);
                }
                else
                {
                    LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, 
                        string.Format("Modify Order status null for order id:{0}", orderId), "ModifyOrder");
                }

#elif local
                order.Product = Constants.PRODUCT_MIS;
#endif
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
        public async static Task<Order> CancelOrder(int algoInstance, AlgoIndex algoIndex, Order order, DateTime currentTime)
        {
            try
            {
#if market
                string orderId = order.OrderId;
                Dictionary<string, dynamic> orderStatus;
                    orderStatus = ZObjects.kite.CancelOrder(orderId);
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_CANCELLED);
                }
#endif
#if local
                order.Status = Constants.ORDER_STATUS_CANCELLED;
                order.OrderTimestamp = currentTime;
#endif
                UpdateOrderDetails(algoInstance, algoIndex, order);
                return order;
            }
            catch (Exception ex)
            {
                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, "Order cancellation failed. Trade will continue.", "CancelOrder");
                return null;
            }
        }

        public async static Task<Order> CancelOrder(int algoInstance, AlgoIndex algoIndex, Order order, DateTime currentTime, int broker = Constants.ZERODHA)
        {
            switch (broker)
            {
                case Constants.ZERODHA:
                    order = CancelZerodhaOrder(algoInstance, algoIndex, order, currentTime);
                    break;

                case Constants.KOTAK:
                    order = CancelKotakOrder(algoInstance, algoIndex, order, currentTime);
                    break;
            }
            return order;
        }

        public static Order CancelZerodhaOrder(int algoInstance, AlgoIndex algoIndex, Order order, DateTime currentTime)
        {
            try
            {
#if market
                string orderId = order.OrderId;
                Dictionary<string, dynamic> orderStatus;
                    orderStatus = ZObjects.kite.CancelOrder(orderId);
                if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                {
                    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_CANCELLED);
                }
#endif
#if local
                order.Status = Constants.ORDER_STATUS_CANCELLED;
                order.OrderTimestamp = currentTime;
#endif
                UpdateOrderDetails(algoInstance, algoIndex, order);
                return order;
            }
            catch (Exception ex)
            {
                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, "Order cancellation failed. Trade will continue.", "CancelOrder");
                return null;
            }
        }

        public static Order CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, Order order, DateTime currentTime, string tag = "", string product = Constants.PRODUCT_MIS,
            HttpClient httpClient=null)
        {
            try
            {
#if market
                string orderId = order.OrderId;
                Dictionary<string, dynamic> orderStatus;
                ZObjects.kotak ??= new KotakConnect.Kotak();
                orderStatus = ZObjects.kotak.CancelOrder(orderId, product, httpClient: httpClient);
                //if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                //{
                //    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_CANCELLED);
                //}
                if (orderStatus != null && orderStatus.ContainsKey("Success") && orderStatus["Success"] != null)
                {
                    Dictionary<string, dynamic> data = orderStatus["Success"]["NSE"];
                    orderId = Convert.ToString(data["orderId"]);

                    order = GetKotakOrder(orderId, algoInstance, algoIndex, Constants.KORDER_STATUS_CANCELLED, order.Tradingsymbol, httpClient);
                }
                else
                {
                    throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
                }

#endif
#if local
                order.Status = Constants.ORDER_STATUS_CANCELLED;
                order.OrderTimestamp = currentTime;
                order.Tag = tag;
#endif
                UpdateOrderDetails(algoInstance, algoIndex, order);
                return order;
            }
            catch (Exception ex)
            {
                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, "Order cancellation failed. Trade will continue.", "CancelOrder");
                return null;
            }
        }

        public static Order ModifyKotakOrder(int algoInstance, AlgoIndex algoIndex, decimal stoploss, Order order, DateTime currentTime, int quantity, string product = Constants.PRODUCT_MIS,
            HttpClient httpClient = null)
        {
            try
            {
#if market
                string orderId = order.OrderId;
                Dictionary<string, dynamic> orderStatus;
                ZObjects.kotak ??= new KotakConnect.Kotak();
                orderStatus = ZObjects.kotak.ModifyOrder(orderId, Price:stoploss, TriggerPrice:stoploss, Quantity:quantity, Product:product, httpClient: httpClient);
                //if (orderStatus != null && orderStatus["data"]["order_id"] != null)
                //{
                //    order = GetOrder(orderId, algoInstance, algoIndex, Constants.ORDER_STATUS_CANCELLED);
                //}
                if (orderStatus != null && orderStatus.ContainsKey("Success") && orderStatus["Success"] != null)
                {
                    Dictionary<string, dynamic> data = orderStatus["Success"]["NSE"];
                    orderId = Convert.ToString(data["orderId"]);
                    //orderId = Convert.ToString(data["orderId"]);

                    order = GetKotakOrder(orderId, algoInstance, algoIndex, Constants.KORDER_STATUS_SLM, order.Tradingsymbol, httpClient);
                }
                else
                {
                    throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
                }

#endif
#if local
                order.AveragePrice = stoploss;
                order.TriggerPrice = stoploss;
                order.Status = Constants.KORDER_STATUS_MODIFIED;
                order.OrderTimestamp = currentTime;
#endif
                UpdateOrderDetails(algoInstance, algoIndex, order);
                return order;
            }
            catch (Exception ex)
            {
                LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, currentTime, "Order cancellation failed. Trade will continue.", "CancelOrder");
                return null;
            }
        }

        public static Order GetKotakOrder(string orderId, int algoInstance, AlgoIndex algoIndex, string status, string tradingSymbol, 
            HttpClient httpClient = null, User user = null)
        {
            Order oh = null;
            int counter = 0;
            bool executioncompleted = false;
            while (true)
            {
                try
                {
                    ZObjects.kotak ??= new KotakConnect.Kotak();
                    List<KotakNeoOrder> orderInfo = ZObjects.kotak.GetOrderHistory(orderId, httpClient, user: user);
                    foreach (var kOrder in orderInfo)
                    {
                        kOrder.Tradingsymbol = tradingSymbol;
                        kOrder.OrderType = "Market";
                        oh = new Order(kOrder);

                        if (oh.Status == status)
                        {
                            executioncompleted = true;
                            //order.AveragePrice = oh.AveragePrice;
                            break;
                        }
                        if (oh.Status == Constants.KORDER_STATUS_TRADED)
                        {
                            //order.AveragePrice = oh.AveragePrice;
                            executioncompleted = true;
                            break;
                        }
                        if (oh.Status == Constants.KORDER_STATUS_SLM)
                        {
                            //order.AveragePrice = oh.AveragePrice;
                            executioncompleted = true;
                            break;
                        }
                        else if (oh.Status == Constants.KORDER_STATUS_CANCELLED)
                        {
                            executioncompleted = true;
                            Logger.LogWrite("Order Cancelled");
                            LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Cancelled", "GetOrder");
                            break;
                        }
                        //else 
                        else if (oh.Status == Constants.KORDER_STATUS_REJECTED)
                        {
                            executioncompleted = true;
                            //_stopTrade = true;
                            Logger.LogWrite("Order Rejected");
                            LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
                            break;
                            //throw new Exception("order did not execute properly");
                        }
                        if (counter > 150 && oh.Status == Constants.KORDER_STATUS_OPEN)
                        {
                            executioncompleted = true;
                            //_stopTrade = true;
                            Logger.LogWrite("order did not execute properly. Waited for 1 minutes");
                            LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order did not go through. Waited for 10 minutes", "GetOrder");
                            break;
                            //throw new Exception("order did not execute properly. Waited for 10 minutes");
                        }
                        counter++;

                        System.Threading.Thread.Sleep(400);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWrite(ex.Message);
                    break;
                }
                if(executioncompleted)
                {
                    break;
                }
            }
            oh.AlgoInstance = algoInstance;
            oh.AlgoIndex = Convert.ToInt32(algoIndex);
            return oh;
        }
        public static Order GetOrder(string orderId, int algoInstance, AlgoIndex algoIndex, string status) // bool slOrder = false)
        {
            Order oh = null;
            int counter = 0;

            while (true)
            {
                try
                {
                    List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);

                    oh = orderInfo[orderInfo.Count - 1];

                    if (oh.Status == status)
                    {
                        //order.AveragePrice = oh.AveragePrice;
                        break;
                    }
                    if (oh.Status == Constants.ORDER_STATUS_COMPLETE)
                    {
                        //order.AveragePrice = oh.AveragePrice;
                        break;
                    }
                    //else if (oh.Status == Constants.ORDER_STATUS_TRIGGER_PENDING)
                    //{
                    //    //order.TriggerPrice = oh.TriggerPrice;
                    //    break;
                    //}
                    //else 
                    else if (oh.Status == Constants.ORDER_STATUS_REJECTED)
                    {
                        //_stopTrade = true;
                        Logger.LogWrite("Order Rejected");
                        LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
                        break;
                        //throw new Exception("order did not execute properly");
                    }
                    if (counter > 150)
                    {
                        //_stopTrade = true;
                        Logger.LogWrite("order did not execute properly. Waited for 1 minutes");
                        LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order did not go through. Waited for 10 minutes", "GetOrder");
                        break;
                        //throw new Exception("order did not execute properly. Waited for 10 minutes");
                    }
                    counter++;

                    System.Threading.Thread.Sleep(400);
                }
                catch (Exception ex)
                {
                    Logger.LogWrite(ex.Message);
                }
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

            //LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order detailed updated in the database", "UpdateOrderDetails");
        }
    }
}
