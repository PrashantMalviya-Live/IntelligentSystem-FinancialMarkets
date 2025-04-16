using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GlobalLayer
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("result")]
        public T Result { get; set; }

        [JsonPropertyName("meta")]
        public MetaData Meta { get; set; }

        [JsonPropertyName("error")]
        public ApiError Error { get; set; }

        public static ApiResponse<T> FromJson(string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<ApiResponse<T>>(json);
        }
        public ApiResponse(bool success, T result = default, MetaData meta = null, ApiError error = null)
        {
            Success = success;
            Result = result;
            Meta = meta;
            Error = error;
        }
    }

    public class MetaData
    {
        public string After { get; set; }
        public string Before { get; set; }

        public MetaData(string after, string before)
        {
            After = after;
            Before = before;
        }
    }

    public class ApiError
    {
        public string Code { get; set; }
        public Dictionary<string, string> Context { get; set; }

        public ApiError(string code, Dictionary<string, string> context)
        {
            Code = code;
            Context = context;
        }
    }

    //public class ApiResponse
    //{
    //    public bool Success { get; set; }
    //    public List<Asset> Result { get; set; }

    //    public ApiResponse(bool success, List<Asset> result)
    //    {
    //        Success = success;
    //        Result = result;
    //    }

    //    public static ApiResponse FromJson(string json)
    //    {
    //        return JsonSerializer.Deserialize<ApiResponse>(json);
    //    }
    //}

    public class Asset
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public int Precision { get; set; }
        public string DepositStatus { get; set; }
        public string WithdrawalStatus { get; set; }
        public string BaseWithdrawalFee { get; set; }
        public string MinWithdrawalAmount { get; set; }

        public Asset(int id, string symbol, int precision, string depositStatus, string withdrawalStatus, string baseWithdrawalFee, string minWithdrawalAmount)
        {
            Id = id;
            Symbol = symbol;
            Precision = precision;
            DepositStatus = depositStatus;
            WithdrawalStatus = withdrawalStatus;
            BaseWithdrawalFee = baseWithdrawalFee;
            MinWithdrawalAmount = minWithdrawalAmount;
        }
    }

    public class IndexData
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public List<ConstituentExchange> ConstituentExchanges { get; set; }
        public int UnderlyingAssetId { get; set; }
        public int QuotingAssetId { get; set; }
        public string TickSize { get; set; }
        public string IndexType { get; set; }

        public IndexData(int id, string symbol, List<ConstituentExchange> constituentExchanges, int underlyingAssetId, int quotingAssetId, string tickSize, string indexType)
        {
            Id = id;
            Symbol = symbol;
            ConstituentExchanges = constituentExchanges;
            UnderlyingAssetId = underlyingAssetId;
            QuotingAssetId = quotingAssetId;
            TickSize = tickSize;
            IndexType = indexType;
        }
    }

    public class ConstituentExchange
    {
        public string Name { get; set; }
        public double Weight { get; set; }

        public ConstituentExchange(string name, double weight)
        {
            Name = name;
            Weight = weight;
        }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string SettlementTime { get; set; }
        public string NotionalType { get; set; }
        public int ImpactSize { get; set; }
        public string InitialMargin { get; set; }
        public string MaintenanceMargin { get; set; }
        public string ContractValue { get; set; }
        public string ContractUnitCurrency { get; set; }
        public string TickSize { get; set; }
        public ProductSpecs ProductSpecs { get; set; }
        public string State { get; set; }
        public string TradingStatus { get; set; }
        public string MaxLeverageNotional { get; set; }
        public string DefaultLeverage { get; set; }
        public string InitialMarginScalingFactor { get; set; }
        public string MaintenanceMarginScalingFactor { get; set; }
        public string TakerCommissionRate { get; set; }
        public string MakerCommissionRate { get; set; }
        public string LiquidationPenaltyFactor { get; set; }
        public string ContractType { get; set; }
        public int PositionSizeLimit { get; set; }
        public string BasisFactorMaxLimit { get; set; }
        public bool IsQuanto { get; set; }
        public string FundingMethod { get; set; }
        public string AnnualizedFunding { get; set; }
        public string PriceBand { get; set; }
        public Asset UnderlyingAsset { get; set; }
        public Asset QuotingAsset { get; set; }
        public Asset SettlingAsset { get; set; }
        public SpotIndex SpotIndex { get; set; }
    }

    public class ProductSpecs
    {
        public double FundingClampValue { get; set; }
        public bool OnlyReduceOnlyOrdersAllowed { get; set; }
        public List<string> Tags { get; set; }
    }

    //public class Asset
    //{
    //    public int Id { get; set; }
    //    public string Symbol { get; set; }
    //    public int Precision { get; set; }
    //    public string DepositStatus { get; set; }
    //    public string WithdrawalStatus { get; set; }
    //    public string BaseWithdrawalFee { get; set; }
    //    public string MinWithdrawalAmount { get; set; }
    //}

    public class SpotIndex
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public List<ConstituentExchange> ConstituentExchanges { get; set; }
        public int UnderlyingAssetId { get; set; }
        public int QuotingAssetId { get; set; }
        public string TickSize { get; set; }
        public string IndexType { get; set; }
    }


    //public class PriceBand
    //{
    //    public string LowerLimit { get; set; }
    //    public string UpperLimit { get; set; }
    //}

    public class Quotes
    {
        public string AskIv { get; set; }
        public int AskSize { get; set; }
        public string BestAsk { get; set; }
        public string BestBid { get; set; }
        public string BidIv { get; set; }
        public int BidSize { get; set; }
    }

    //public class Greeks
    //{
    //    public string Delta { get; set; }
    //    public string Gamma { get; set; }
    //    public string Rho { get; set; }
    //    public string Theta { get; set; }
    //    public string Vega { get; set; }
    //}

    public class ProductTicker
    {
        public double Close { get; set; }
        public string ContractType { get; set; }
        public Greeks Greeks { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public string MarkPrice { get; set; }
        public int MarkVol { get; set; }
        public string Oi { get; set; }
        public string OiValue { get; set; }
        public string OiValueSymbol { get; set; }
        public double OiValueUsd { get; set; }
        public double Open { get; set; }
        public PriceBand PriceBand { get; set; }
        public int ProductId { get; set; }
        public Quotes Quotes { get; set; }
        public int Size { get; set; }
        public string SpotPrice { get; set; }
        public double StrikePrice { get; set; }
        public string Symbol { get; set; }
        public long Timestamp { get; set; }
        public double Turnover { get; set; }
        public string TurnoverSymbol { get; set; }
        public double TurnoverUsd { get; set; }
        public int Volume { get; set; }
    }

    /*
     {
"product_id": 27,
"product_symbol": "BTCUSD",
"limit_price": "59000",
"size": 10,
"side": "buy",
"order_type": "limit_order",
"stop_order_type": "stop_loss_order",
"stop_price": "56000",
"trail_amount": "50",
"stop_trigger_method": "last_traded_price",
"bracket_stop_loss_limit_price": "57000",
"bracket_stop_loss_price": "56000",
"bracket_trail_amount": "50",
"bracket_take_profit_limit_price": "62000",
"bracket_take_profit_price": "61000",
"time_in_force": "gtc",
"mmp": "disabled",
"post_only": false,
"reduce_only": false,
"client_order_id": "34521712",
"cancel_orders_accepted": false
}
     */

public class CreateOrderRequest
    {
        [JsonPropertyName("product_id")]
        public int? ProductId { get; set; }

        [JsonPropertyName("product_symbol")]
        public string ProductSymbol { get; set; }

        [JsonPropertyName("limit_price")]
        public string? LimitPrice { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("side")]
        public string Side { get; set; }

        [JsonPropertyName("order_type")]
        public string OrderType { get; set; }

        [JsonPropertyName("stop_order_type")]
        public string? StopOrderType { get; set; }

        [JsonPropertyName("stop_price")]
        public string? StopPrice { get; set; }

        [JsonPropertyName("trail_amount")]
        public string? TrailAmount { get; set; }

        [JsonPropertyName("stop_trigger_method")]
        public string? StopTriggerMethod { get; set; }

        [JsonPropertyName("bracket_stop_loss_limit_price")]
        public string? BracketStopLossLimitPrice { get; set; }

        [JsonPropertyName("bracket_stop_loss_price")]
        public string? BracketStopLossPrice { get; set; }

        [JsonPropertyName("bracket_trail_amount")]
        public string? BracketTrailAmount { get; set; }

        [JsonPropertyName("bracket_take_profit_limit_price")]
        public string? BracketTakeProfitLimitPrice { get; set; }

        [JsonPropertyName("bracket_take_profit_price")]
        public string? BracketTakeProfitPrice { get; set; }

        [JsonPropertyName("time_in_force")]
        public string? TimeInForce { get; set; }

        [JsonPropertyName("mmp")]
        public string? Mmp { get; set; }

        [JsonPropertyName("post_only")]
        public bool? PostOnly { get; set; }

        [JsonPropertyName("reduce_only")]
        public bool? ReduceOnly { get; set; }

        [JsonPropertyName("client_order_id")]
        public string? ClientOrderId { get; set; }

        [JsonPropertyName("cancel_orders_accepted")]
        public bool? CancelOrdersAccepted { get; set; }

        public static CreateOrderRequest FromJson(string json) =>
            System.Text.Json. JsonSerializer.Deserialize<CreateOrderRequest>(json);

        public string ToJson() =>
         System.Text.Json.JsonSerializer.Serialize(this, new JsonSerializerOptions
         {
             //PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
         });
    }

   
    /*
     {
      "id": 13452112,
      "client_order_id": "34521712",
      "product_id": 27
    }

     */
    public class DeleteOrderRequest
    {
        public int Id { get; set; }
        public string ClientOrderId { get; set; }
        public int ProductId { get; set; }

        public static DeleteOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<DeleteOrderRequest>(json);
    }

    //public class OrderResponse
    //{
    //    public bool Success { get; set; }
    //    public OrderResult Result { get; set; }

    //    public static OrderResponse FromJson(string json) => JsonSerializer.Deserialize<OrderResponse>(json);
    //}

    public class CryptoOrderTrio
    {
        public int Id { get; set; }
        public TradedCryptoFuture Future { get; set; }
        public CryptoOrder Order { get; set; }
        public DateTime EntryTradeTime { get; set; }
        public decimal StopLoss { get; set; }
        public decimal InitialStopLoss { get; set; }
        public decimal BaseInstrumentStopLoss { get; set; }
        public decimal TargetProfit { get; set; }
        public int flag;
    }

    public class CryptoOrder
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("unfilled_size")]
        public int UnfilledSize { get; set; }

        [JsonPropertyName("side")]
        public string Side { get; set; }

        [JsonPropertyName("order_type")]
        public string OrderType { get; set; }

        [JsonPropertyName("limit_price")]
        public string LimitPrice { get; set; }

        [JsonPropertyName("stop_order_type")]
        public string StopOrderType { get; set; }

        [JsonPropertyName("stop_price")]
        public string StopPrice { get; set; }

        [JsonPropertyName("paid_commission")]
        public string PaidCommission { get; set; }

        [JsonPropertyName("commission")]
        public string Commission { get; set; }

        [JsonPropertyName("reduce_only")]
        public bool ReduceOnly { get; set; }

        [JsonPropertyName("client_order_id")]
        public string ClientOrderId { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("average_fill_price")]
        public string AverageFilledPrice { get; set; }

        [JsonPropertyName("product_id")]
        public int ProductId { get; set; }

        [JsonPropertyName("product_symbol")]
        public string ProductSymbol { get; set; }

        public int AlgoInstanceId { get; set; }
        public int StrategyId { get; set; }

        public static CryptoOrder FromJson(string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<CryptoOrder>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true });
        }
    }

    //public class CryptoOrder
    //{
    //    public int Id { get; set; }
    //    public int UserId { get; set; }
    //    public int Size { get; set; }
    //    public int UnfilledSize { get; set; }
    //    public string Side { get; set; }
    //    public string OrderType { get; set; }
    //    public string LimitPrice { get; set; }
    //    public string StopOrderType { get; set; }
    //    public string StopPrice { get; set; }
    //    public string PaidCommission { get; set; }
    //    public string Commission { get; set; }
    //    public bool ReduceOnly { get; set; }
    //    public string ClientOrderId { get; set; }
    //    public string State { get; set; }
    //    public string CreatedAt { get; set; }
    //    public int ProductId { get; set; }
    //    public string ProductSymbol { get; set; }


    //    public int AlgoInstanceId { get; set; }

    //    public int StrategyId { get; set; }
    //}

    //public class ConstituentExchange
    //{
    //    public string Name { get; set; }
    //    public double Weight { get; set; }
    //}


    /*{
"limit_price": "59000",
"size": 10,
"side": "buy",
"order_type": "limit_order",
"time_in_force": "gtc",
"mmp": "disabled",
"post_only": false,
"client_order_id": "34521712"
}
*/

    public class BatchCreateOrder
    {
        public string LimitPrice { get; set; }
        public int Size { get; set; }
        public string Side { get; set; }
        public string OrderType { get; set; }
        public string TimeInForce { get; set; }
        public string Mmp { get; set; }
        public bool PostOnly { get; set; }
        public string ClientOrderId { get; set; }

        public static BatchCreateOrder FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<BatchCreateOrder>(json);
    }

    /*
     {
"product_id": 27,
"product_symbol": "BTCUSD",
"orders": [
{
  "limit_price": "59000",
  "size": 10,
  "side": "buy",
  "order_type": "limit_order",
  "time_in_force": "gtc",
  "mmp": "disabled",
  "post_only": false,
  "client_order_id": "34521712"
}
]
}
     */
    public class BatchCreateOrdersRequest
    {
        public int ProductId { get; set; }
        public string ProductSymbol { get; set; }
        public List<BatchCreateOrder> Orders { get; set; }

        public static BatchCreateOrdersRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<BatchCreateOrdersRequest>(json);
    }


    /*
     {
"id": 34521712,
"product_id": 27,
"product_symbol": "BTCUSD",
"limit_price": "59000",
"size": 15,
"mmp": "disabled",
"post_only": false,
"cancel_orders_accepted": false,
"stop_price": "56000",
"trail_amount": "50"
}
     */

    public class EditOrderRequest
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductSymbol { get; set; }
        public string LimitPrice { get; set; }
        public int Size { get; set; }
        public string Mmp { get; set; }
        public bool PostOnly { get; set; }
        public bool CancelOrdersAccepted { get; set; }
        public string StopPrice { get; set; }
        public string TrailAmount { get; set; }

        public static EditOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<EditOrderRequest>(json);
    }


    public class CreateBracketOrderRequest
    {
        public int ProductId { get; set; }
        public string ProductSymbol { get; set; }
        public StopLossOrder StopLossOrder { get; set; }
        public TakeProfitOrder TakeProfitOrder { get; set; }
        public string BracketStopTriggerMethod { get; set; }

        public static CreateBracketOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<CreateBracketOrderRequest>(json);
    }

    public class StopLossOrder
    {
        public string OrderType { get; set; }
        public string StopPrice { get; set; }
        public string TrailAmount { get; set; }
        public string LimitPrice { get; set; }
    }

    public class TakeProfitOrder
    {
        public string OrderType { get; set; }
        public string StopPrice { get; set; }
        public string LimitPrice { get; set; }
    }

    public class EditBracketOrderRequest
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductSymbol { get; set; }
        public string BracketStopLossLimitPrice { get; set; }
        public string BracketStopLossPrice { get; set; }
        public string BracketTakeProfitLimitPrice { get; set; }
        public string BracketTakeProfitPrice { get; set; }
        public string BracketTrailAmount { get; set; }
        public string BracketStopTriggerMethod { get; set; }

        public static EditBracketOrderRequest FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<EditBracketOrderRequest>(json);
    }

    public class CryptoPosition
    {
        public int UserId { get; set; }
        public int Size { get; set; }
        public string EntryPrice { get; set; }
        public string Margin { get; set; }
        public string LiquidationPrice { get; set; }
        public string BankruptcyPrice { get; set; }
        public int AdlLevel { get; set; }
        public int ProductId { get; set; }
        public string ProductSymbol { get; set; }
        public string Commission { get; set; }
        public string RealizedPnl { get; set; }
        public string RealizedFunding { get; set; }

        // Default constructor
        public CryptoPosition()
        {
            UserId = 0;
            Size = 0;
            EntryPrice = string.Empty;
            Margin = string.Empty;
            LiquidationPrice = string.Empty;
            BankruptcyPrice = string.Empty;
            AdlLevel = 0;
            ProductId = 0;
            ProductSymbol = string.Empty;
            Commission = string.Empty;
            RealizedPnl = string.Empty;
            RealizedFunding = string.Empty;
        }

        // Method to create a Position object from a JSON string
        public static CryptoPosition FromJson(string json)
        {
            return JsonConvert.DeserializeObject<CryptoPosition>(json);
        }
    }


    /*
              "adl_level":"4.3335",
         "auto_topup":false,
         "bankruptcy_price":"261.82",
         "commission":"17.6571408",
         "created_at":"2021-04-29T07:25:59Z",
         "entry_price":"238.023457888493475682",
         "liquidation_price":"260.63",
         "margin":"4012.99",
         "product_id":357,
         "product_symbol":"ZECUSD",
         "realized_funding":"-3.08",
         "realized_pnl":"6364.57",
         "size":-1686,
         "updated_at":"2021-04-29T10:00:05Z",
         "user_id":1,
         "symbol":"ZECUSD"
     */


    public class Fill
    {
        public int Id { get; set; }
        public int Size { get; set; }
        public string FillType { get; set; }
        public string Side { get; set; }
        public string Price { get; set; }
        public string Role { get; set; }
        public string Commission { get; set; }
        public string CreatedAt { get; set; }
        public int ProductId { get; set; }
        public string ProductSymbol { get; set; }
        public string OrderId { get; set; }
        public int SettlingAssetId { get; set; }
        public string SettlingAssetSymbol { get; set; }
        public MetaData FillMetaData { get; set; }

        // Default constructor
        public Fill()
        {
            Id = 0;
            Size = 0;
            FillType = string.Empty;
            Side = string.Empty;
            Price = string.Empty;
            Role = string.Empty;
            Commission = string.Empty;
            CreatedAt = string.Empty;
            ProductId = 0;
            ProductSymbol = string.Empty;
            OrderId = string.Empty;
            SettlingAssetId = 0;
            SettlingAssetSymbol = string.Empty;
            FillMetaData = new MetaData();
        }

        // Method to create a Fill object from a JSON string
        public static Fill FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Fill>(json);
        }

        // Nested MetaData class
        public class MetaData
        {
            public string CommissionDeto { get; set; }
            public string CommissionDetoInSettlingAsset { get; set; }
            public string EffectiveCommissionRate { get; set; }
            public string LiquidationFeeDeto { get; set; }
            public string LiquidationFeeDetoInSettlingAsset { get; set; }
            public string OrderPrice { get; set; }
            public string OrderSize { get; set; }
            public string OrderType { get; set; }
            public string OrderUnfilledSize { get; set; }
            public string TfcUsedForCommission { get; set; }
            public string TfcUsedForLiquidationFee { get; set; }
            public string TotalCommissionInSettlingAsset { get; set; }
            public string TotalLiquidationFeeInSettlingAsset { get; set; }

            // Default constructor
            public MetaData()
            {
                CommissionDeto = string.Empty;
                CommissionDetoInSettlingAsset = string.Empty;
                EffectiveCommissionRate = string.Empty;
                LiquidationFeeDeto = string.Empty;
                LiquidationFeeDetoInSettlingAsset = string.Empty;
                OrderPrice = string.Empty;
                OrderSize = string.Empty;
                OrderType = string.Empty;
                OrderUnfilledSize = string.Empty;
                TfcUsedForCommission = string.Empty;
                TfcUsedForLiquidationFee = string.Empty;
                TotalCommissionInSettlingAsset = string.Empty;
                TotalLiquidationFeeInSettlingAsset = string.Empty;
            }
        }
    }

    /*
     {
"leverage": 10,
"order_margin": "563.2",
"product_id": 27
}

     */
    public class OrderLeverage
    {
        public int Leverage { get; set; }
        public string OrderMargin { get; set; }
        public int ProductId { get; set; }

        // Default constructor
        public OrderLeverage()
        {
            Leverage = 0;
            OrderMargin = string.Empty;
            ProductId = 0;
        }

        // Method to create an OrderLeverage object from a JSON string
        public static OrderLeverage FromJson(string json)
        {
            return JsonConvert.DeserializeObject<OrderLeverage>(json);
        }
    }

    /*
                 // l1 orderbook Response
        {
          "ask_qty":"839",
          "best_ask":"1211.3",
          "best_bid":"1211.25",
          "bid_qty":"772",
          "last_sequence_no":1671603257645135,
          "last_updated_at":1671603257623000,
          "product_id":176,"symbol":"ETHUSD",
          "timestamp":1671603257645134,
          "type":"l1_orderbook"
        }
     */

    
    public class L1Orderbook
    {
        [JsonProperty("ask_qty")]
        public string AskQuantity { get; set; }

        [JsonProperty("best_ask")]
        public string BestAsk { get; set; }

        [JsonProperty("best_bid")]
        public string BestBid { get; set; }

        [JsonProperty("bid_qty")]
        public string BidQuantity { get; set; }

        [JsonProperty("last_sequence_no")]
        public long LastSequenceNo { get; set; }

        [JsonProperty("last_updated_at")]
        public long LastUpdatedAt { get; set; }

        [JsonProperty("product_id")]
        public int ProductId { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        public static L1Orderbook FromJson(string json) => JsonConvert.DeserializeObject<L1Orderbook>(json);
    }

    /*
     
     {
    "candle_start_time": 1596015240000000,
    "close": 9223,
    "high": 9228,
    "low": 9220,
    "open": 9221,
    "resolution": "1m",
    "symbol": "BTCUSD",
    "timestamp": 1596015289339699,
    "type": "candlestick_1m",
    "volume": 1.2            // volume not present in Mark Price candlestick
}
     */

    public class CandleStick: Candle, IConvertible
    {
        [JsonProperty("candle_start_time")]
        public long StartTime { get; set; }
        
        // OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(StartTime / 1000).LocalDateTime;
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("close")]
        public decimal Close
        {
            get
            {
                return ClosePrice;
            }
            set
            {

                ClosePrice = value;
            }
        }

        [JsonProperty("high")]
        public decimal High
        {
            get
            {
                return HighPrice;
            }
            set
            {

                HighPrice = value;
            }
        }
      
        [JsonProperty("low")]
        public decimal Low
        {
            get
            {
                return LowPrice;
            }
            set
            {

                LowPrice = value;
            }
        }
       
        [JsonProperty("open")]
        public decimal Open
        {
            get
            {
                return OpenPrice;
            }
            set
            {

                OpenPrice = value;
            }
        }
        
        [JsonProperty("resolution")]
        public string Resolution { get; set; }

        /// <summary>
        /// Time-frame.
        /// </summary>
        [DataMember]
        public TimeSpan? TimeFrame { get; set; }

        //[DataMember]
        //public DateTime StartTime { get; set; }
        /// <inheritdoc />
        public override object Arg
        {
            get => TimeFrame;
            set => TimeFrame = value == null ? null : (TimeSpan)value;
        }
        public override CandleType CandleType
        {
            get => CandleType.Time; //.Crypto
        }
        [JsonProperty("volume")]
        public new decimal? TotalVolume  { get;set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        public static CandleStick FromJson(string json) => System.Text.Json.JsonSerializer.Deserialize<CandleStick>(json);
    }


    public class TradedCryptoOption
    {
        public decimal EntryPremium { get; set; }
        public decimal CurrentPremium { get; set; }

        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public string Symbol { get; set; }
        public int ProductId { get; set; }
        public decimal Strike { get; set; } = 0;
        public DateTime Expiry { get; set; }
        public decimal Size { get; set; }

        public string Side { get; set; }
        public decimal PaidCommission { get; set; }
    }
    public class TradedCryptoFuture
    {
        public decimal EntryPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public string Symbol { get; set; }
        public int ProductId { get; set; }
        public decimal Size { get; set; }
        public string Side { get; set; }
        public decimal PaidCommission { get; set; }
    }

    public class PriceBand
    {
        [JsonProperty("lower_limit")]
        public string LowerLimit { get; set; }

        [JsonProperty("upper_limit")]
        public string UpperLimit { get; set; }
    }

    /*// Mark Price Response
    {
        "ask_iv":null,
        "ask_qty":null,
        "best_ask":null,
        "best_bid":"9532",
        "bid_iv":"5.000",
        "bid_qty":"896",
        "delta":"0",
        "gamma":"0",
        "implied_volatility":"0",
        "price":"3910.088012",
        "price_band":{"lower_limit":"3463.375340559572217228510815","upper_limit":"4354.489445440427782771489185"},
        "product_id":39687,
        "rho":"0",
        "symbol":"MARK:C-BTC-13000-301222",
        "timestamp":1671867039712836,
        "type":"mark_price",
        "vega":"0"
    }*/
    public class MarkPrice
    {
        [JsonProperty("ask_iv")]
        public string AskIv { get; set; }

        [JsonProperty("ask_qty")]
        public string AskQty { get; set; }

        [JsonProperty("best_ask")]
        public string BestAsk { get; set; }

        [JsonProperty("best_bid")]
        public string BestBid { get; set; }

        [JsonProperty("bid_iv")]
        public string BidIv { get; set; }

        [JsonProperty("bid_qty")]
        public string BidQty { get; set; }

        [JsonProperty("delta")]
        public string Delta { get; set; }

        [JsonProperty("gamma")]
        public string Gamma { get; set; }

        [JsonProperty("implied_volatility")]
        public string ImpliedVolatility { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("price_band")]
        public PriceBand PriceBand { get; set; }

        [JsonProperty("product_id")]
        public int ProductId { get; set; }

        [JsonProperty("rho")]
        public string Rho { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("vega")]
        public string Vega { get; set; }

        public static MarkPrice FromJson(string json)
        {
            return JsonConvert.DeserializeObject<MarkPrice>(json);
        }
    }


    /*
     {
    "candle_start_time": 1596015240000000,
    "close": 9223,
    "high": 9228,
    "low": 9220,
    "open": 9221,
    "resolution": "1m",
    "symbol": "BTCUSD",
    "timestamp": 1596015289339699,
    "type": "candlestick_1m",
    "volume": 1.2
}
     */
    public class CandleSticksResponse
    {
        [JsonProperty("candle_start_time")]
        public long CandleStartTime { get; set; }

        [JsonProperty("close")]
        public int Close { get; set; }

        [JsonProperty("high")]
        public int High { get; set; }

        [JsonProperty("low")]
        public int Low { get; set; }

        [JsonProperty("open")]
        public int Open { get; set; }

        [JsonProperty("resolution")]
        public string Resolution { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("volume")]
        public double Volume { get; set; }

        public static CandleSticksResponse FromJson(string json) => JsonConvert.DeserializeObject<CandleSticksResponse>(json);

       
    }

    /*

     {
"buy": [
{
  "depth": "983",
  "price": "9187.5",
  "size": 205640
}
],
"last_updated_at": 1654589595784000,
"sell": [
{
  "depth": "1185",
  "price": "9188.0",
  "size": 113752
}
],
"symbol": "BTCUSD"
}

     */
    public class L2OrderBook
    {
        public List<Order> Buy { get; set; }
        public long LastUpdatedAt { get; set; }
        public List<Order> Sell { get; set; }
        public string Symbol { get; set; }

        // Default constructor
        public L2OrderBook()
        {
            Buy = new List<Order>();
            Sell = new List<Order>();
            LastUpdatedAt = 0;
            Symbol = string.Empty;
        }

        // Method to create an L2OrderBook object from a JSON string
        public static L2OrderBook FromJson(string json)
        {
            return JsonConvert.DeserializeObject<L2OrderBook>(json);
        }

        // Order class representing buy/sell order
        public class Order
        {
            public string Depth { get; set; }
            public string Price { get; set; }
            public int Size { get; set; }

            // Default constructor
            public Order()
            {
                Depth = string.Empty;
                Price = string.Empty;
                Size = 0;
            }
        }
    }

    /*
      // All Trades Response
{
    "symbol": "BTCUSD",
    "price": "25816.5",
    "size": 100,
    "type": "all_trades",
    "buyer_role": "maker",
    "seller_role": "taker",
    "timestamp": 1686577411879974
}
     */
    public class AllTradesResponse
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("price")]
        public string Price { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("buyer_role")]
        public string BuyerRole { get; set; }

        [JsonProperty("seller_role")]
        public string SellerRole { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        public static AllTradesResponse FromJson(string json) => JsonConvert.DeserializeObject<AllTradesResponse>(json);

       
    }

    public class Trades
    {
        public List<Trade> TradesList { get; set; }

        // Default constructor
        public Trades()
        {
            TradesList = new List<Trade>();
        }

        // Method to create a Trades object from a JSON string
        public static Trades FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Trades>(json);
        }

        // Trade class representing an individual trade
        public class Trade
        {
            public string Side { get; set; }
            public int Size { get; set; }
            public string Price { get; set; }
            public long Timestamp { get; set; }

            // Default constructor
            public Trade()
            {
                Side = string.Empty;
                Size = 0;
                Price = string.Empty;
                Timestamp = 0;
            }
        }
    }

    /*

     {
"delta": "0.25",
"gamma": "0.10",
"rho": "0.05",
"theta": "-0.02",
"vega": "0.15"
}
     */
    public class Greeks
    {
        public string Delta { get; set; }
        public string Gamma { get; set; }
        public string Rho { get; set; }
        public string Theta { get; set; }
        public string Vega { get; set; }

        // Default constructor
        public Greeks()
        {
            Delta = string.Empty;
            Gamma = string.Empty;
            Rho = string.Empty;
            Theta = string.Empty;
            Vega = string.Empty;
        }

        // Method to create a Greeks object from a JSON string
        public static Greeks FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Greeks>(json);
        }
    }


    public class DeltaUser
    {
        public int? Id { get; set; }
        public string Email { get; set; }
        public string AccountName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Dob { get; set; }
        public string Country { get; set; }
        public string PhoneNumber { get; set; }
        public string MarginMode { get; set; }
        public string PfIndexSymbol { get; set; }
        public bool IsSubAccount { get; set; }
        public bool IsKycDone { get; set; }

        // Default constructor
        public DeltaUser()
        {
            Email = string.Empty;
            AccountName = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            Dob = string.Empty;
            Country = string.Empty;
            PhoneNumber = string.Empty;
            MarginMode = string.Empty;
            PfIndexSymbol = string.Empty;
            IsSubAccount = false;
            IsKycDone = false;
        }

        // Method to create a DeltaUser object from a JSON string
        public static DeltaUser FromJson(string json)
        {
            return JsonConvert.DeserializeObject<DeltaUser>(json);
        }
    }

    /**/

    public class OrderUpdate
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("product_id")]
        public int ProductId { get; set; }

        [JsonProperty("order_id")]
        public int OrderId { get; set; }

        [JsonProperty("client_order_id")]
        public string ClientOrderId { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("unfilled_size")]
        public int UnfilledSize { get; set; }

        [JsonProperty("average_fill_price")]
        public string AverageFillPrice { get; set; }

        [JsonProperty("limit_price")]
        public string LimitPrice { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("cancellation_reason")]
        public string CancellationReason { get; set; }

        [JsonProperty("stop_order_type")]
        public string StopOrderType { get; set; }

        [JsonProperty("bracket_order")]
        public bool BracketOrder { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("seq_no")]
        public int SeqNo { get; set; }

        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("stop_price")]
        public string StopPrice { get; set; }

        [JsonProperty("trigger_price_max_or_min")]
        public string TriggerPriceMaxOrMin { get; set; }

        [JsonProperty("bracket_stop_loss_price")]
        public string BracketStopLossPrice { get; set; }

        [JsonProperty("bracket_stop_loss_limit_price")]
        public string BracketStopLossLimitPrice { get; set; }

        [JsonProperty("bracket_take_profit_price")]
        public string BracketTakeProfitPrice { get; set; }

        [JsonProperty("bracket_take_profit_limit_price")]
        public string BracketTakeProfitLimitPrice { get; set; }

        [JsonProperty("bracket_trail_amount")]
        public string BracketTrailAmount { get; set; }

        public static OrderUpdate FromJson(string json) => JsonConvert.DeserializeObject<OrderUpdate>(json);

       

        public class OHLCData
    {
        public long Time { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }

        // Default constructor
        public OHLCData()
        {
            Time = 0;
            Open = 0;
            High = 0;
            Low = 0;
            Close = 0;
            Volume = 0;
        }

        // Method to create an OHLCData object from a JSON string
        public static OHLCData FromJson(string json)
        {
            return JsonConvert.DeserializeObject<OHLCData>(json);
        }
    }

        public class Ticker
        {
            public decimal Close { get; set; }
            public string ContractType { get; set; }
            public Greeks Greeks { get; set; }
            public decimal High { get; set; }
            public decimal Low { get; set; }
            public string MarkPrice { get; set; }
            public int MarkVol { get; set; }
            public string Oi { get; set; }
            public string OiValue { get; set; }
            public string OiValueSymbol { get; set; }
            public string OiValueUsd { get; set; }
            public decimal Open { get; set; }
            public PriceBand TickPriceBand { get; set; }
            public int ProductId { get; set; }
            public Quotes TickQuotes { get; set; }
            public int Size { get; set; }
            public string SpotPrice { get; set; }
            public decimal StrikePrice { get; set; }
            public string Symbol { get; set; }
            public long Timestamp { get; set; }
            public decimal Turnover { get; set; }
            public string TurnoverSymbol { get; set; }
            public decimal TurnoverUsd { get; set; }
            public int Volume { get; set; }

            // Default constructor
            public Ticker()
            {
                ContractType = string.Empty;
                Greeks = new Greeks();
                MarkPrice = string.Empty;
                Oi = string.Empty;
                OiValue = string.Empty;
                OiValueSymbol = string.Empty;
                OiValueUsd = string.Empty;
                SpotPrice = string.Empty;
                Symbol = string.Empty;
                TurnoverSymbol = string.Empty;
            }

            // Method to create a Ticker object from a JSON string
            public static Ticker FromJson(string json)
            {
                return JsonConvert.DeserializeObject<Ticker>(json);
            }
        }
        // Greeks class representing option greeks
        //public class Greeks
        //{
        //    public string Delta { get; set; }
        //    public string Gamma { get; set; }
        //    public string Rho { get; set; }
        //    public string Theta { get; set; }
        //    public string Vega { get; set; }

        //    // Default constructor
        //    public Greeks()
        //    {
        //        Delta = string.Empty;
        //        Gamma = string.Empty;
        //        Rho = string.Empty;
        //        Theta = string.Empty;
        //        Vega = string.Empty;
        //    }
        //}

        // PriceBand class for the price limits
        public class PriceBand
        {
            public string LowerLimit { get; set; }
            public string UpperLimit { get; set; }

            // Default constructor
            public PriceBand()
            {
                LowerLimit = string.Empty;
                UpperLimit = string.Empty;
            }
        }

        // Quotes class for the quotes related to the ticker
        public class Quotes
        {
            public string AskIv { get; set; }
            public int AskSize { get; set; }
            public string BestAsk { get; set; }
            public string BestBid { get; set; }
            public string BidIv { get; set; }
            public int BidSize { get; set; }

            // Default constructor
            public Quotes()
            {
                AskIv = string.Empty;
                AskSize = 0;
                BestAsk = string.Empty;
                BestBid = string.Empty;
                BidIv = string.Empty;
                BidSize = 0;
            }
        }
    }

}
