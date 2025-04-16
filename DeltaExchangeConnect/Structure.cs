using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeltaExchangeConnect
{
//    public class Structure
//    {
//        public class ApiResponse<T>
//        {
//            public bool Success { get; set; }
//            public T Result { get; set; }
//            public MetaData Meta { get; set; }
//            public ApiError Error { get; set; }

//            public static ApiResponse<T> FromJson(string json)
//            {
//                return System.Text.Json.JsonSerializer.Deserialize<ApiResponse<T>>(json);
//            }
//            public ApiResponse(bool success, T result = default, MetaData meta = null, ApiError error = null)
//            {
//                Success = success;
//                Result = result;
//                Meta = meta;
//                Error = error;
//            }
//        }

//        public class MetaData
//        {
//            public string After { get; set; }
//            public string Before { get; set; }

//            public MetaData(string after, string before)
//            {
//                After = after;
//                Before = before;
//            }
//        }

//        public class ApiError
//        {
//            public string Code { get; set; }
//            public Dictionary<string, string> Context { get; set; }

//            public ApiError(string code, Dictionary<string, string> context)
//            {
//                Code = code;
//                Context = context;
//            }
//        }

//        //public class ApiResponse
//        //{
//        //    public bool Success { get; set; }
//        //    public List<Asset> Result { get; set; }

//        //    public ApiResponse(bool success, List<Asset> result)
//        //    {
//        //        Success = success;
//        //        Result = result;
//        //    }

//        //    public static ApiResponse FromJson(string json)
//        //    {
//        //        return JsonSerializer.Deserialize<ApiResponse>(json);
//        //    }
//        //}

//        public class Asset
//        {
//            public int Id { get; set; }
//            public string Symbol { get; set; }
//            public int Precision { get; set; }
//            public string DepositStatus { get; set; }
//            public string WithdrawalStatus { get; set; }
//            public string BaseWithdrawalFee { get; set; }
//            public string MinWithdrawalAmount { get; set; }

//            public Asset(int id, string symbol, int precision, string depositStatus, string withdrawalStatus, string baseWithdrawalFee, string minWithdrawalAmount)
//            {
//                Id = id;
//                Symbol = symbol;
//                Precision = precision;
//                DepositStatus = depositStatus;
//                WithdrawalStatus = withdrawalStatus;
//                BaseWithdrawalFee = baseWithdrawalFee;
//                MinWithdrawalAmount = minWithdrawalAmount;
//            }
//        }

//        public class IndexData
//        {
//            public int Id { get; set; }
//            public string Symbol { get; set; }
//            public List<ConstituentExchange> ConstituentExchanges { get; set; }
//            public int UnderlyingAssetId { get; set; }
//            public int QuotingAssetId { get; set; }
//            public string TickSize { get; set; }
//            public string IndexType { get; set; }

//            public IndexData(int id, string symbol, List<ConstituentExchange> constituentExchanges, int underlyingAssetId, int quotingAssetId, string tickSize, string indexType)
//            {
//                Id = id;
//                Symbol = symbol;
//                ConstituentExchanges = constituentExchanges;
//                UnderlyingAssetId = underlyingAssetId;
//                QuotingAssetId = quotingAssetId;
//                TickSize = tickSize;
//                IndexType = indexType;
//            }
//        }

//        public class ConstituentExchange
//        {
//            public string Name { get; set; }
//            public double Weight { get; set; }

//            public ConstituentExchange(string name, double weight)
//            {
//                Name = name;
//                Weight = weight;
//            }
//        }

//        public class Product
//        {
//            public int Id { get; set; }
//            public string Symbol { get; set; }
//            public string Description { get; set; }
//            public DateTime CreatedAt { get; set; }
//            public DateTime UpdatedAt { get; set; }
//            public string SettlementTime { get; set; }
//            public string NotionalType { get; set; }
//            public int ImpactSize { get; set; }
//            public string InitialMargin { get; set; }
//            public string MaintenanceMargin { get; set; }
//            public string ContractValue { get; set; }
//            public string ContractUnitCurrency { get; set; }
//            public string TickSize { get; set; }
//            public ProductSpecs ProductSpecs { get; set; }
//            public string State { get; set; }
//            public string TradingStatus { get; set; }
//            public string MaxLeverageNotional { get; set; }
//            public string DefaultLeverage { get; set; }
//            public string InitialMarginScalingFactor { get; set; }
//            public string MaintenanceMarginScalingFactor { get; set; }
//            public string TakerCommissionRate { get; set; }
//            public string MakerCommissionRate { get; set; }
//            public string LiquidationPenaltyFactor { get; set; }
//            public string ContractType { get; set; }
//            public int PositionSizeLimit { get; set; }
//            public string BasisFactorMaxLimit { get; set; }
//            public bool IsQuanto { get; set; }
//            public string FundingMethod { get; set; }
//            public string AnnualizedFunding { get; set; }
//            public string PriceBand { get; set; }
//            public Asset UnderlyingAsset { get; set; }
//            public Asset QuotingAsset { get; set; }
//            public Asset SettlingAsset { get; set; }
//            public SpotIndex SpotIndex { get; set; }
//        }

//        public class ProductSpecs
//        {
//            public double FundingClampValue { get; set; }
//            public bool OnlyReduceOnlyOrdersAllowed { get; set; }
//            public List<string> Tags { get; set; }
//        }

//        //public class Asset
//        //{
//        //    public int Id { get; set; }
//        //    public string Symbol { get; set; }
//        //    public int Precision { get; set; }
//        //    public string DepositStatus { get; set; }
//        //    public string WithdrawalStatus { get; set; }
//        //    public string BaseWithdrawalFee { get; set; }
//        //    public string MinWithdrawalAmount { get; set; }
//        //}

//        public class SpotIndex
//        {
//            public int Id { get; set; }
//            public string Symbol { get; set; }
//            public List<ConstituentExchange> ConstituentExchanges { get; set; }
//            public int UnderlyingAssetId { get; set; }
//            public int QuotingAssetId { get; set; }
//            public string TickSize { get; set; }
//            public string IndexType { get; set; }
//        }


//        //public class PriceBand
//        //{
//        //    public string LowerLimit { get; set; }
//        //    public string UpperLimit { get; set; }
//        //}

//        //public class Quotes
//        //{
//        //    public string AskIv { get; set; }
//        //    public int AskSize { get; set; }
//        //    public string BestAsk { get; set; }
//        //    public string BestBid { get; set; }
//        //    public string BidIv { get; set; }
//        //    public int BidSize { get; set; }
//        //}

//        //public class Greeks
//        //{
//        //    public string Delta { get; set; }
//        //    public string Gamma { get; set; }
//        //    public string Rho { get; set; }
//        //    public string Theta { get; set; }
//        //    public string Vega { get; set; }
//        //}

//        //public class Ticker
//        //{
//        //    public double Close { get; set; }
//        //    public string ContractType { get; set; }
//        //    public Greeks Greeks { get; set; }
//        //    public double High { get; set; }
//        //    public double Low { get; set; }
//        //    public string MarkPrice { get; set; }
//        //    public int MarkVol { get; set; }
//        //    public string Oi { get; set; }
//        //    public string OiValue { get; set; }
//        //    public string OiValueSymbol { get; set; }
//        //    public double OiValueUsd { get; set; }
//        //    public double Open { get; set; }
//        //    public PriceBand PriceBand { get; set; }
//        //    public int ProductId { get; set; }
//        //    public Quotes Quotes { get; set; }
//        //    public int Size { get; set; }
//        //    public string SpotPrice { get; set; }
//        //    public double StrikePrice { get; set; }
//        //    public string Symbol { get; set; }
//        //    public long Timestamp { get; set; }
//        //    public double Turnover { get; set; }
//        //    public string TurnoverSymbol { get; set; }
//        //    public double TurnoverUsd { get; set; }
//        //    public int Volume { get; set; }
//        //}

//        /*
//         {
//  "product_id": 27,
//  "product_symbol": "BTCUSD",
//  "limit_price": "59000",
//  "size": 10,
//  "side": "buy",
//  "order_type": "limit_order",
//  "stop_order_type": "stop_loss_order",
//  "stop_price": "56000",
//  "trail_amount": "50",
//  "stop_trigger_method": "last_traded_price",
//  "bracket_stop_loss_limit_price": "57000",
//  "bracket_stop_loss_price": "56000",
//  "bracket_trail_amount": "50",
//  "bracket_take_profit_limit_price": "62000",
//  "bracket_take_profit_price": "61000",
//  "time_in_force": "gtc",
//  "mmp": "disabled",
//  "post_only": false,
//  "reduce_only": false,
//  "client_order_id": "34521712",
//  "cancel_orders_accepted": false
//}
//         */
//        public class CreateOrderRequest
//        {
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public string LimitPrice { get; set; }
//            public int Size { get; set; }
//            public string Side { get; set; }
//            public string OrderType { get; set; }
//            public string StopOrderType { get; set; }
//            public string StopPrice { get; set; }
//            public string TrailAmount { get; set; }
//            public string StopTriggerMethod { get; set; }
//            public string BracketStopLossLimitPrice { get; set; }
//            public string BracketStopLossPrice { get; set; }
//            public string BracketTrailAmount { get; set; }
//            public string BracketTakeProfitLimitPrice { get; set; }
//            public string BracketTakeProfitPrice { get; set; }
//            public string TimeInForce { get; set; }
//            public string Mmp { get; set; }
//            public bool PostOnly { get; set; }
//            public bool ReduceOnly { get; set; }
//            public string ClientOrderId { get; set; }
//            public bool CancelOrdersAccepted { get; set; }


//            public static CreateOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<CreateOrderRequest>(json);
//        }

//        /*
//         {
//          "id": 13452112,
//          "client_order_id": "34521712",
//          "product_id": 27
//        }

//         */
//        public class DeleteOrderRequest
//        {
//            public int Id { get; set; }
//            public string ClientOrderId { get; set; }
//            public int ProductId { get; set; }

//            public static DeleteOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<DeleteOrderRequest>(json);
//        }

//        //public class OrderResponse
//        //{
//        //    public bool Success { get; set; }
//        //    public OrderResult Result { get; set; }

//        //    public static OrderResponse FromJson(string json) => JsonSerializer.Deserialize<OrderResponse>(json);
//        //}

//        public class OrderResult
//        {
//            public int Id { get; set; }
//            public int UserId { get; set; }
//            public int Size { get; set; }
//            public int UnfilledSize { get; set; }
//            public string Side { get; set; }
//            public string OrderType { get; set; }
//            public string LimitPrice { get; set; }
//            public string StopOrderType { get; set; }
//            public string StopPrice { get; set; }
//            public string PaidCommission { get; set; }
//            public string Commission { get; set; }
//            public bool ReduceOnly { get; set; }
//            public string ClientOrderId { get; set; }
//            public string State { get; set; }
//            public string CreatedAt { get; set; }
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//        }

//        //public class ConstituentExchange
//        //{
//        //    public string Name { get; set; }
//        //    public double Weight { get; set; }
//        //}


//        /*{
//  "limit_price": "59000",
//  "size": 10,
//  "side": "buy",
//  "order_type": "limit_order",
//  "time_in_force": "gtc",
//  "mmp": "disabled",
//  "post_only": false,
//  "client_order_id": "34521712"
//}
//*/

//        public class BatchCreateOrder
//        {
//            public string LimitPrice { get; set; }
//            public int Size { get; set; }
//            public string Side { get; set; }
//            public string OrderType { get; set; }
//            public string TimeInForce { get; set; }
//            public string Mmp { get; set; }
//            public bool PostOnly { get; set; }
//            public string ClientOrderId { get; set; }

//            public static BatchCreateOrder FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<BatchCreateOrder>(json);
//        }

//        /*
//         {
//  "product_id": 27,
//  "product_symbol": "BTCUSD",
//  "orders": [
//    {
//      "limit_price": "59000",
//      "size": 10,
//      "side": "buy",
//      "order_type": "limit_order",
//      "time_in_force": "gtc",
//      "mmp": "disabled",
//      "post_only": false,
//      "client_order_id": "34521712"
//    }
//  ]
//}
//         */
//        public class BatchCreateOrdersRequest
//        {
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public List<BatchCreateOrder> Orders { get; set; }

//            public static BatchCreateOrdersRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<BatchCreateOrdersRequest>(json);
//        }


//        /*
//         {
//  "id": 34521712,
//  "product_id": 27,
//  "product_symbol": "BTCUSD",
//  "limit_price": "59000",
//  "size": 15,
//  "mmp": "disabled",
//  "post_only": false,
//  "cancel_orders_accepted": false,
//  "stop_price": "56000",
//  "trail_amount": "50"
//}
//         */

//        public class EditOrderRequest
//        {
//            public int Id { get; set; }
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public string LimitPrice { get; set; }
//            public int Size { get; set; }
//            public string Mmp { get; set; }
//            public bool PostOnly { get; set; }
//            public bool CancelOrdersAccepted { get; set; }
//            public string StopPrice { get; set; }
//            public string TrailAmount { get; set; }

//            public static EditOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<EditOrderRequest>(json);
//        }


//        public class CreateBracketOrderRequest
//        {
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public StopLossOrder StopLossOrder { get; set; }
//            public TakeProfitOrder TakeProfitOrder { get; set; }
//            public string BracketStopTriggerMethod { get; set; }

//            public static CreateBracketOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<CreateBracketOrderRequest>(json);
//        }

//        public class StopLossOrder
//        {
//            public string OrderType { get; set; }
//            public string StopPrice { get; set; }
//            public string TrailAmount { get; set; }
//            public string LimitPrice { get; set; }
//        }

//        public class TakeProfitOrder
//        {
//            public string OrderType { get; set; }
//            public string StopPrice { get; set; }
//            public string LimitPrice { get; set; }
//        }

//        public class EditBracketOrderRequest
//        {
//            public int Id { get; set; }
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public string BracketStopLossLimitPrice { get; set; }
//            public string BracketStopLossPrice { get; set; }
//            public string BracketTakeProfitLimitPrice { get; set; }
//            public string BracketTakeProfitPrice { get; set; }
//            public string BracketTrailAmount { get; set; }
//            public string BracketStopTriggerMethod { get; set; }

//            public static EditBracketOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<EditBracketOrderRequest>(json);
//        }

//        public class Position
//        {
//            public int UserId { get; set; }
//            public int Size { get; set; }
//            public string EntryPrice { get; set; }
//            public string Margin { get; set; }
//            public string LiquidationPrice { get; set; }
//            public string BankruptcyPrice { get; set; }
//            public int AdlLevel { get; set; }
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public string Commission { get; set; }
//            public string RealizedPnl { get; set; }
//            public string RealizedFunding { get; set; }

//            // Default constructor
//            public Position()
//            {
//                UserId = 0;
//                Size = 0;
//                EntryPrice = string.Empty;
//                Margin = string.Empty;
//                LiquidationPrice = string.Empty;
//                BankruptcyPrice = string.Empty;
//                AdlLevel = 0;
//                ProductId = 0;
//                ProductSymbol = string.Empty;
//                Commission = string.Empty;
//                RealizedPnl = string.Empty;
//                RealizedFunding = string.Empty;
//            }

//            // Method to create a Position object from a JSON string
//            public static Position FromJson(string json)
//            {
//                return JsonConvert.DeserializeObject<Position>(json);
//            }
//        }

//        public class Fill
//        {
//            public int Id { get; set; }
//            public int Size { get; set; }
//            public string FillType { get; set; }
//            public string Side { get; set; }
//            public string Price { get; set; }
//            public string Role { get; set; }
//            public string Commission { get; set; }
//            public string CreatedAt { get; set; }
//            public int ProductId { get; set; }
//            public string ProductSymbol { get; set; }
//            public string OrderId { get; set; }
//            public int SettlingAssetId { get; set; }
//            public string SettlingAssetSymbol { get; set; }
//            public MetaData FillMetaData { get; set; }

//            // Default constructor
//            public Fill()
//            {
//                Id = 0;
//                Size = 0;
//                FillType = string.Empty;
//                Side = string.Empty;
//                Price = string.Empty;
//                Role = string.Empty;
//                Commission = string.Empty;
//                CreatedAt = string.Empty;
//                ProductId = 0;
//                ProductSymbol = string.Empty;
//                OrderId = string.Empty;
//                SettlingAssetId = 0;
//                SettlingAssetSymbol = string.Empty;
//                FillMetaData = new MetaData();
//            }

//            // Method to create a Fill object from a JSON string
//            public static Fill FromJson(string json)
//            {
//                return JsonConvert.DeserializeObject<Fill>(json);
//            }

//            // Nested MetaData class
//            public class MetaData
//            {
//                public string CommissionDeto { get; set; }
//                public string CommissionDetoInSettlingAsset { get; set; }
//                public string EffectiveCommissionRate { get; set; }
//                public string LiquidationFeeDeto { get; set; }
//                public string LiquidationFeeDetoInSettlingAsset { get; set; }
//                public string OrderPrice { get; set; }
//                public string OrderSize { get; set; }
//                public string OrderType { get; set; }
//                public string OrderUnfilledSize { get; set; }
//                public string TfcUsedForCommission { get; set; }
//                public string TfcUsedForLiquidationFee { get; set; }
//                public string TotalCommissionInSettlingAsset { get; set; }
//                public string TotalLiquidationFeeInSettlingAsset { get; set; }

//                // Default constructor
//                public MetaData()
//                {
//                    CommissionDeto = string.Empty;
//                    CommissionDetoInSettlingAsset = string.Empty;
//                    EffectiveCommissionRate = string.Empty;
//                    LiquidationFeeDeto = string.Empty;
//                    LiquidationFeeDetoInSettlingAsset = string.Empty;
//                    OrderPrice = string.Empty;
//                    OrderSize = string.Empty;
//                    OrderType = string.Empty;
//                    OrderUnfilledSize = string.Empty;
//                    TfcUsedForCommission = string.Empty;
//                    TfcUsedForLiquidationFee = string.Empty;
//                    TotalCommissionInSettlingAsset = string.Empty;
//                    TotalLiquidationFeeInSettlingAsset = string.Empty;
//                }
//            }
//        }

//        /*
//         {
//  "leverage": 10,
//  "order_margin": "563.2",
//  "product_id": 27
//}
         
//         */
//        public class OrderLeverage
//        {
//            public int Leverage { get; set; }
//            public string OrderMargin { get; set; }
//            public int ProductId { get; set; }

//            // Default constructor
//            public OrderLeverage()
//            {
//                Leverage = 0;
//                OrderMargin = string.Empty;
//                ProductId = 0;
//            }

//            // Method to create an OrderLeverage object from a JSON string
//            public static OrderLeverage FromJson(string json)
//            {
//                return JsonConvert.DeserializeObject<OrderLeverage>(json);
//            }
//        }

//        /*
//                     // l1 orderbook Response
//            {
//              "ask_qty":"839",
//              "best_ask":"1211.3",
//              "best_bid":"1211.25",
//              "bid_qty":"772",
//              "last_sequence_no":1671603257645135,
//              "last_updated_at":1671603257623000,
//              "product_id":176,"symbol":"ETHUSD",
//              "timestamp":1671603257645134,
//              "type":"l1_orderbook"
//            }
//         */
//        public class L1Orderbook
//        {
//            [JsonProperty("ask_qty")]
//            public string AskQuantity { get; set; }

//            [JsonProperty("best_ask")]
//            public string BestAsk { get; set; }

//            [JsonProperty("best_bid")]
//            public string BestBid { get; set; }

//            [JsonProperty("bid_qty")]
//            public string BidQuantity { get; set; }

//            [JsonProperty("last_sequence_no")]
//            public long LastSequenceNo { get; set; }

//            [JsonProperty("last_updated_at")]
//            public long LastUpdatedAt { get; set; }

//            [JsonProperty("product_id")]
//            public int ProductId { get; set; }

//            [JsonProperty("symbol")]
//            public string Symbol { get; set; }

//            [JsonProperty("timestamp")]
//            public long Timestamp { get; set; }

//            [JsonProperty("type")]
//            public string Type { get; set; }

//            public static L1Orderbook FromJson(string json) => JsonConvert.DeserializeObject<L1Orderbook>(json);
//        }


//        /*
         
//         {
//  "buy": [
//    {
//      "depth": "983",
//      "price": "9187.5",
//      "size": 205640
//    }
//  ],
//  "last_updated_at": 1654589595784000,
//  "sell": [
//    {
//      "depth": "1185",
//      "price": "9188.0",
//      "size": 113752
//    }
//  ],
//  "symbol": "BTCUSD"
//}

//         */
//        public class L2OrderBook
//        {
//            public List<Order> Buy { get; set; }
//            public long LastUpdatedAt { get; set; }
//            public List<Order> Sell { get; set; }
//            public string Symbol { get; set; }

//            // Default constructor
//            public L2OrderBook()
//            {
//                Buy = new List<Order>();
//                Sell = new List<Order>();
//                LastUpdatedAt = 0;
//                Symbol = string.Empty;
//            }

//            // Method to create an L2OrderBook object from a JSON string
//            public static L2OrderBook FromJson(string json)
//            {
//                return JsonConvert.DeserializeObject<L2OrderBook>(json);
//            }

//            // Order class representing buy/sell order
//            public class Order
//            {
//                public string Depth { get; set; }
//                public string Price { get; set; }
//                public int Size { get; set; }

//                // Default constructor
//                public Order()
//                {
//                    Depth = string.Empty;
//                    Price = string.Empty;
//                    Size = 0;
//                }
//            }
//        }

//        public class Trades
//        {
//            public List<Trade> TradesList { get; set; }

//            // Default constructor
//            public Trades()
//            {
//                TradesList = new List<Trade>();
//            }

//            // Method to create a Trades object from a JSON string
//            public static Trades FromJson(string json)
//            {
//                return JsonConvert.DeserializeObject<Trades>(json);
//            }

//            // Trade class representing an individual trade
//            public class Trade
//            {
//                public string Side { get; set; }
//                public int Size { get; set; }
//                public string Price { get; set; }
//                public long Timestamp { get; set; }

//                // Default constructor
//                public Trade()
//                {
//                    Side = string.Empty;
//                    Size = 0;
//                    Price = string.Empty;
//                    Timestamp = 0;
//                }
//            }
//        }

//        /*
         
//         {
//  "delta": "0.25",
//  "gamma": "0.10",
//  "rho": "0.05",
//  "theta": "-0.02",
//  "vega": "0.15"
//}
//         */
//        public class Greeks
//        {
//            public string Delta { get; set; }
//            public string Gamma { get; set; }
//            public string Rho { get; set; }
//            public string Theta { get; set; }
//            public string Vega { get; set; }

//            // Default constructor
//            public Greeks()
//            {
//                Delta = string.Empty;
//                Gamma = string.Empty;
//                Rho = string.Empty;
//                Theta = string.Empty;
//                Vega = string.Empty;
//            }

//            // Method to create a Greeks object from a JSON string
//            public static Greeks FromJson(string json)
//            {
//                return JsonConvert.DeserializeObject<Greeks>(json);
//            }
//        }


//public class DeltaUser
//    {
//        public int? Id { get; set; }
//        public string Email { get; set; }
//        public string AccountName { get; set; }
//        public string FirstName { get; set; }
//        public string LastName { get; set; }
//        public string Dob { get; set; }
//        public string Country { get; set; }
//        public string PhoneNumber { get; set; }
//        public string MarginMode { get; set; }
//        public string PfIndexSymbol { get; set; }
//        public bool IsSubAccount { get; set; }
//        public bool IsKycDone { get; set; }

//        // Default constructor
//        public DeltaUser()
//        {
//            Email = string.Empty;
//            AccountName = string.Empty;
//            FirstName = string.Empty;
//            LastName = string.Empty;
//            Dob = string.Empty;
//            Country = string.Empty;
//            PhoneNumber = string.Empty;
//            MarginMode = string.Empty;
//            PfIndexSymbol = string.Empty;
//            IsSubAccount = false;
//            IsKycDone = false;
//        }

//        // Method to create a DeltaUser object from a JSON string
//        public static DeltaUser FromJson(string json)
//        {
//            return JsonConvert.DeserializeObject<DeltaUser>(json);
//        }
//    }

//    public class OHLCData
//    {
//        public long Time { get; set; }
//        public decimal Open { get; set; }
//        public decimal High { get; set; }
//        public decimal Low { get; set; }
//        public decimal Close { get; set; }
//        public decimal Volume { get; set; }

//        // Default constructor
//        public OHLCData()
//        {
//            Time = 0;
//            Open = 0;
//            High = 0;
//            Low = 0;
//            Close = 0;
//            Volume = 0;
//        }

//        // Method to create an OHLCData object from a JSON string
//        public static OHLCData FromJson(string json)
//        {
//            return JsonConvert.DeserializeObject<OHLCData>(json);
//        }
//    }

//    public class Ticker
//    {
//        public decimal Close { get; set; }
//        public string ContractType { get; set; }
//        public Greeks Greeks { get; set; }
//        public decimal High { get; set; }
//        public decimal Low { get; set; }
//        public string MarkPrice { get; set; }
//        public int MarkVol { get; set; }
//        public string Oi { get; set; }
//        public string OiValue { get; set; }
//        public string OiValueSymbol { get; set; }
//        public string OiValueUsd { get; set; }
//        public decimal Open { get; set; }
//        public PriceBand TickPriceBand { get; set; }
//        public int ProductId { get; set; }
//        public Quotes TickQuotes { get; set; }
//        public int Size { get; set; }
//        public string SpotPrice { get; set; }
//        public decimal StrikePrice { get; set; }
//        public string Symbol { get; set; }
//        public long Timestamp { get; set; }
//        public decimal Turnover { get; set; }
//        public string TurnoverSymbol { get; set; }
//        public decimal TurnoverUsd { get; set; }
//        public int Volume { get; set; }

//        // Default constructor
//        public Ticker()
//        {
//            ContractType = string.Empty;
//            Greeks = new Greeks();
//            MarkPrice = string.Empty;
//            Oi = string.Empty;
//            OiValue = string.Empty;
//            OiValueSymbol = string.Empty;
//            OiValueUsd = string.Empty;
//            SpotPrice = string.Empty;
//            Symbol = string.Empty;
//            TurnoverSymbol = string.Empty;
//        }

//        // Method to create a Ticker object from a JSON string
//        public static Ticker FromJson(string json)
//        {
//            return JsonConvert.DeserializeObject<Ticker>(json);
//        }

//            // Greeks class representing option greeks
//            //public class Greeks
//            //{
//            //    public string Delta { get; set; }
//            //    public string Gamma { get; set; }
//            //    public string Rho { get; set; }
//            //    public string Theta { get; set; }
//            //    public string Vega { get; set; }

//            //    // Default constructor
//            //    public Greeks()
//            //    {
//            //        Delta = string.Empty;
//            //        Gamma = string.Empty;
//            //        Rho = string.Empty;
//            //        Theta = string.Empty;
//            //        Vega = string.Empty;
//            //    }
//            //}

//            // PriceBand class for the price limits
//            public class PriceBand
//            {
//                public string LowerLimit { get; set; }
//                public string UpperLimit { get; set; }

//                // Default constructor
//                public PriceBand()
//                {
//                    LowerLimit = string.Empty;
//                    UpperLimit = string.Empty;
//                }
//            }

//            // Quotes class for the quotes related to the ticker
//            public class Quotes
//            {
//                public string AskIv { get; set; }
//                public int AskSize { get; set; }
//                public string BestAsk { get; set; }
//                public string BestBid { get; set; }
//                public string BidIv { get; set; }
//                public int BidSize { get; set; }

//                // Default constructor
//                public Quotes()
//                {
//                    AskIv = string.Empty;
//                    AskSize = 0;
//                    BestAsk = string.Empty;
//                    BestBid = string.Empty;
//                    BidIv = string.Empty;
//                    BidSize = 0;
//                }
//            }
//        }

//}
}
