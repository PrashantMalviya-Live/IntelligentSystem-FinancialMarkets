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
using Algos.Utilities;
using System.Drawing;

namespace Algorithms.Utilities
{
    public class CryptoOrders
    {

        public static CryptoOrder PlaceDEOrder(int algoInstance, string productSymbol, int orderSize, int productId,
            bool buyOrder, AlgoIndex algoIndex, User user, decimal limitPrice, string orderType = Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET,
            DateTime? timeStamp = null, string clientOrderId = "", HttpClient httpClient = null)
        {
#if market
            ZObjects.deltaExchange ??= new DeltaExchangeConnect.DeltaExchange(user.APIKey, user.AppSecret);

            CreateOrderRequest orderRequest = new CreateOrderRequest()
            {
                ProductId = productId == 0 ? null : productId,
                ProductSymbol = productSymbol,
                Size = orderSize,
                OrderType = orderType,
                Side = buyOrder ? "buy" : "sell",
                LimitPrice = limitPrice.ToString(),
                ClientOrderId = clientOrderId,
            };

            var cryptoOrder = ZObjects.deltaExchange.PlaceNewOrder(orderRequest, httpClient);

#elif local
            //ZObjects.deltaExchange ??= new DeltaExchangeConnect.DeltaExchange(user.APIKey, user.AppSecret);

            //CreateOrderRequest orderRequest = new CreateOrderRequest()
            //{
            //    ProductId = productId,
            //    ProductSymbol = productSymbol,
            //    Size = orderSize,
            //    OrderType = orderType,
            //    Side = buyOrder ? "buy" : "sell",
            //    LimitPrice = limitPrice.ToString(),
            //    ClientOrderId = clientOrderId,
            //};

            var cryptoOrder = new CryptoOrder()
            {
                AlgoInstanceId = algoInstance,
                AverageFilledPrice = limitPrice.ToString(),
                PaidCommission = (productSymbol.StartsWith("C-") || productSymbol.StartsWith("P-")) ? (limitPrice * 0.1m * orderSize / 1000).ToString() : (limitPrice * 0.02M / 100 * orderSize / 1000).ToString(),
                ProductSymbol = productSymbol,
                OrderType = orderType,
                Side = buyOrder ? "buy" : "sell",
                Size = orderSize,
                LimitPrice = limitPrice.ToString(),
                CreatedAt = timeStamp == null ? DateTime.Now.ToString() : timeStamp.Value.ToString(),
            };
            ///TEMP, REMOVE Later

            //if (currentPrice == 0)
            //{
            //    currentPrice = 10;
            //   // DataLogic dl = new DataLogic();
            //   // currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
            //    //order.AveragePrice = currentPrice;
            //}
            ////System.Threading.Thread.Sleep(5000);
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
            if (cryptoOrder != null)
            {
                UpdateOrderDetails(algoInstance, cryptoOrder);
            }
            return cryptoOrder;
        }

       
        //private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        //{
        //    DataLogic dl = new DataLogic();
        //    dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.VolumeThreshold, tradedLot, triggerID);
        //}

        public static void UpdateOrderDetails(int algoInstance, CryptoOrder order, int strategyId = 0)
        {
            CryptoDataLogic dl = new CryptoDataLogic();
            dl.UpdateOrder(algoInstance, order, strategyId);

            //LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order detailed updated in the database", "UpdateOrderDetails");
        }
    }
}
