using StockMarketAlertsApp.Components.Pages;

namespace StockMarketAlertsApp.Models
{
    public class PriceActionInput
    {
        /// <summary>
        /// Base Instrument Token
        /// </summary>
        public uint BToken { get; set; }

        /// <summary>
        /// Reference Instrument Token
        /// </summary>
        public uint? RToken { get; set; }

        /// <summary>
        /// Expiry
        /// </summary>
        public DateTime Expiry { get; set; }

        /// <summary>
        /// Candle Time frame
        /// </summary>
        public int CTF { get; set; }

        /// <summary>
        /// Initial quantity
        /// </summary>
        public int Qty { get; set; }

        /// <summary>
        /// Target Profit
        /// </summary>
        public decimal? TP { get; set; } = 0;

        /// <summary>
        /// Stop Loss
        /// </summary>
        public decimal? SL { get; set; } = 0;

        //public decimal PD_H { get; set; } = 0;

        /// <summary>
        /// Intraday flag
        /// </summary>
        public bool? IntD { get; set; } = true;

        //public decimal TP1 { get; set; } = 0;
        //public decimal TP2 { get; set; } = 0;
        //public decimal TP3 { get; set; } = 0;
        //public decimal TP4 { get; set; } = 0;
        //public decimal SL1 { get; set; } = 0;
        //public decimal SL2 { get; set; } = 0;
        //public decimal SL3 { get; set; } = 0;
        //public decimal SL4 { get; set; } = 0;


        public string? UID { get; set; }

        //public int AID { get; set; } = 0;

        //public decimal PnL { get; set; }

        //public List<OrderTrio> ActiveOrderTrios { get; set; } = null;

        ////Current breakout model
        //public Breakout BM { get; set; } = Breakout.NONE;

        //public PriceRange CPR { get; set; }
        //public PriceRange PPR { get; set; }

    }
    public class OrderTrio
    {
        public int Id { get; set; }
        public Instrument Option { get; set; }
        public Order Order { get; set; }
        public decimal EntryRSI { get; set; }
        public DateTime EntryTradeTime { get; set; }
        public Order SLOrder { get; set; }
        public decimal StopLoss { get; set; }
        public decimal InitialStopLoss { get; set; }
        public decimal BaseInstrumentStopLoss { get; set; }
        public Order TPOrder { get; set; }
        public decimal TargetProfit { get; set; }
        public bool TPFlag { get; set; } = false;

        public bool isActive { get; set; } = true;//0: Inactive; 1 : Active

        public DateTime? IntialSlHitTime { get; set; } = null;

        public int flag;
    }
    public class KotakNeoOrder
    {
        public KotakNeoOrder(Dictionary<string, dynamic> data, decimal disclosedQuantity, uint instrumentToken, string status, string statusMessage,
            string product, string validity, string variety, string orderType, string tradingSymbol, string transactionType, int algoInstance, int algoIndex)
        {
            try
            {
                OrderId = Convert.ToString(data["orderId"]);
                Price = Convert.ToDecimal(data["price"]);
                AveragePrice = Convert.ToDecimal(data["price"]);
                Tag = data["tag"];
                Quantity = Convert.ToInt32(data["quantity"]);
                Status = status;
                StatusMessage = statusMessage;
                DisclosedQuantity = Quantity;
                Exchange = "NSE";
                InstrumentToken = instrumentToken;
                OrderType = orderType;
                Product = product;
                Tradingsymbol = tradingSymbol;
                TransactionType = transactionType;
                TriggerPrice = AveragePrice;
                Validity = validity;
                Variety = variety;
                AlgoInstance = algoInstance;
                AlgoIndex = algoIndex;
            }
            catch (Exception ex)
            {
               // throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public KotakNeoOrder(Dictionary<string, dynamic> data)
        {
            try
            {
                OrderId = Convert.ToString(data["nOrdNo"]);
                //Variety = Convert.ToString(data["variety"]);
                Tradingsymbol = Convert.ToString(data["trdSym"]);
                InstrumentToken = Convert.ToUInt32(data["tok"]);
                Exchange = Convert.ToString(data["exch"]);
                Quantity = Convert.ToInt32(data["qty"]);
                PendingQuantity = Convert.ToInt32(data["unFldSz"]);
                //CancelledQuantity = Convert.ToInt32(data["cancelledQuantity"]);
                FilledQuantity = Convert.ToInt32(data["fldQty"]);
                DisclosedQuantity = Convert.ToInt32(data["dclQty"]);
                TriggerPrice = Convert.ToDecimal(data["trgPrc"]);
                Price = Convert.ToDecimal(data["prc"]);
                AveragePrice = Convert.ToDecimal(data["avgPrc"]);
                Product = Convert.ToString(data["prod"]);
                TransactionType = Convert.ToString(data["trnsTp"]);
                OrderTimestamp = Utils.StringToDate(data["flDtTm"]);
                //Validity = Convert.ToString(data["validity"]);
                //StatusMessage = Convert.ToString(data["statusMessage"]);
                Tag = Convert.ToString(data["rejRsn"]);
                Status = Convert.ToString(data["ordSt"]);
                //info = Convert.ToString(data["statusInfo"]);
                //isfno = Convert.ToString(data["isFNO"]);
            }
            catch (Exception ex)
            {
               // throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public decimal AveragePrice { get; set; }
        public int CancelledQuantity { get; set; }
        public int DisclosedQuantity { get; set; }
        public string Exchange { get; set; }
        public string ExchangeOrderId { get; set; }
        public DateTime? ExchangeTimestamp { get; set; }
        public int FilledQuantity { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public string OrderId { get; set; }
        public DateTime? OrderTimestamp { get; set; }
        public string OrderType { get; set; }
        public string ParentOrderId { get; set; }
        public int PendingQuantity { get; set; }
        public string PlacedBy { get; set; }
        public decimal Price { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string StatusMessage { get; set; }
        public string Tag { get; set; }
        public string Tradingsymbol { get; set; }
        public string TransactionType { get; set; }
        public decimal TriggerPrice { get; set; }
        public string Validity { get; set; }
        public string Variety { get; set; }

        public int AlgoIndex { get; set; } = 0;
        public bool UpOrder { get; set; } = true;
        public int AlgoInstance { get; set; } = 0;
    }

    public class KotakOrder
    {
        public KotakOrder()
        {

        }
        public KotakOrder(Dictionary<string, dynamic> data, decimal disclosedQuantity, uint instrumentToken, string status, string statusMessage,
            string product, string validity, string variety, string orderType, string tradingSymbol, string transactionType, int algoInstance, int algoIndex)
        {
            try
            {
                OrderId = Convert.ToString(data["orderId"]);
                Price = Convert.ToDecimal(data["price"]);
                AveragePrice = Convert.ToDecimal(data["price"]);
                Tag = data["tag"];
                Quantity = Convert.ToInt32(data["quantity"]);
                Status = status;
                StatusMessage = statusMessage;
                DisclosedQuantity = Quantity;
                Exchange = "NSE";
                InstrumentToken = instrumentToken;
                OrderType = orderType;
                Product = product;
                Tradingsymbol = tradingSymbol;
                TransactionType = transactionType;
                TriggerPrice = AveragePrice;
                Validity = validity;
                Variety = variety;
                AlgoInstance = algoInstance;
                AlgoIndex = algoIndex;
            }
            catch (Exception ex)
            {
                //throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public KotakOrder(Dictionary<string, dynamic> data)
        {
            try
            {
                OrderId = Convert.ToString(data["orderId"]);
                Variety = Convert.ToString(data["variety"]);
                Tradingsymbol = Convert.ToString(data["instrumentName"]);
                InstrumentToken = Convert.ToUInt32(data["instrumentToken"]);
                Exchange = Convert.ToString(data["exchange"]);
                Quantity = Convert.ToInt32(data["orderQuantity"]);
                PendingQuantity = Convert.ToInt32(data["pendingQuantity"]);
                CancelledQuantity = Convert.ToInt32(data["cancelledQuantity"]);
                FilledQuantity = Convert.ToInt32(data["filledQuantity"]);
                DisclosedQuantity = Convert.ToInt32(data["disclosedQuantity"]);
                TriggerPrice = Convert.ToDecimal(data["triggerPrice"]);
                Price = Convert.ToDecimal(data["price"]);
                AveragePrice = Convert.ToDecimal(data["price"]);
                Product = Convert.ToString(data["product"]);
                TransactionType = Convert.ToString(data["transactionType"]);
                OrderTimestamp = Utils.StringToDate(data["orderTimestamp"]);
                Validity = Convert.ToString(data["validity"]);
                StatusMessage = Convert.ToString(data["statusMessage"]);
                Tag = Convert.ToString(data["tag"]);
                Status = Convert.ToString(data["status"]);
                //info = Convert.ToString(data["statusInfo"]);
                //isfno = Convert.ToString(data["isFNO"]);
            }
            catch (Exception ex)
            {
                //throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public decimal AveragePrice { get; set; }
        public int CancelledQuantity { get; set; }
        public int DisclosedQuantity { get; set; }
        public string Exchange { get; set; }
        public string ExchangeOrderId { get; set; }
        public DateTime? ExchangeTimestamp { get; set; }
        public int FilledQuantity { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public string OrderId { get; set; }
        public DateTime? OrderTimestamp { get; set; }
        public string OrderType { get; set; }
        public string ParentOrderId { get; set; }
        public int PendingQuantity { get; set; }
        public string PlacedBy { get; set; }
        public decimal Price { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string StatusMessage { get; set; }
        public string Tag { get; set; }
        public string Tradingsymbol { get; set; }
        public string TransactionType { get; set; }
        public decimal TriggerPrice { get; set; }
        public string Validity { get; set; }
        public string Variety { get; set; }

        public int AlgoIndex { get; set; } = 0;
        public bool UpOrder { get; set; } = true;
        public int AlgoInstance { get; set; } = 0;
    }

    public class Order
    {
        public Order()
        {

        }
        public Order(KotakOrder korder)
        {
            AveragePrice = korder.AveragePrice;
            CancelledQuantity = korder.CancelledQuantity;
            DisclosedQuantity = korder.DisclosedQuantity;
            Exchange = korder.Exchange;
            ExchangeOrderId = korder.ExchangeOrderId;
            ExchangeTimestamp = korder.ExchangeTimestamp;
            FilledQuantity = korder.FilledQuantity;
            InstrumentToken = korder.InstrumentToken;
            OrderId = korder.OrderId; ;
            OrderTimestamp = korder.OrderTimestamp ?? DateTime.Now;
            OrderType = korder.OrderType;
            ParentOrderId = korder.ParentOrderId;
            PendingQuantity = korder.PendingQuantity;
            PlacedBy = korder.PlacedBy;
            Price = korder.Price;
            Product = korder.Product;
            Quantity = korder.Quantity;
            Status = korder.Status;
            StatusMessage = korder.StatusMessage;
            Tag = korder.Tag;
            Tradingsymbol = korder.Tradingsymbol;
            TransactionType = korder.TransactionType;
            TriggerPrice = korder.TriggerPrice;
            Validity = korder.Validity;
            Variety = korder.Variety;
            AlgoInstance = korder.AlgoInstance;
            AlgoIndex = korder.AlgoIndex;
        }
        public Order(KotakNeoOrder korder)
        {
            AveragePrice = korder.AveragePrice;
            CancelledQuantity = korder.CancelledQuantity;
            DisclosedQuantity = korder.DisclosedQuantity;
            Exchange = korder.Exchange;
            ExchangeOrderId = korder.ExchangeOrderId;
            ExchangeTimestamp = korder.ExchangeTimestamp;
            FilledQuantity = korder.FilledQuantity;
            InstrumentToken = korder.InstrumentToken;
            OrderId = korder.OrderId;
            OrderTimestamp = korder.OrderTimestamp ?? DateTime.Now;
            OrderType = korder.OrderType;
            ParentOrderId = korder.ParentOrderId;
            PendingQuantity = korder.PendingQuantity;
            PlacedBy = korder.PlacedBy;
            Price = korder.Price;
            Product = korder.Product;
            Quantity = korder.Quantity;
            Status = korder.Status;
            StatusMessage = korder.StatusMessage;
            Tag = korder.Tag;
            Tradingsymbol = korder.Tradingsymbol;
            TransactionType = korder.TransactionType.ToLower() == "b" ? "buy" : korder.TransactionType.ToLower() == "s" ? "sell" : korder.TransactionType;
            TriggerPrice = korder.TriggerPrice;
            Validity = korder.Validity;
            Variety = korder.Variety;
            AlgoInstance = korder.AlgoInstance;
            AlgoIndex = korder.AlgoIndex;
        }
        public Order(Dictionary<string, dynamic> data)
        {
            try
            {
                AveragePrice = Convert.ToDecimal(data["average_price"]);
                CancelledQuantity = Convert.ToInt32(data["cancelled_quantity"]);
                DisclosedQuantity = Convert.ToInt32(data["disclosed_quantity"]);
                Exchange = data["exchange"];
                ExchangeOrderId = data["exchange_order_id"];
                ExchangeTimestamp = Utils.StringToDate(data["exchange_timestamp"]);
                FilledQuantity = Convert.ToInt32(data["filled_quantity"]);
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                OrderId = data["order_id"];
                OrderTimestamp = Utils.StringToDate(data["order_timestamp"]);
                OrderType = data["order_type"];
                ParentOrderId = data["parent_order_id"];
                PendingQuantity = Convert.ToInt32(data["pending_quantity"]);
                PlacedBy = data["placed_by"];
                Price = Convert.ToDecimal(data["price"]);
                Product = data["product"];
                Quantity = Convert.ToInt32(data["quantity"]);
                Status = data["status"];
                StatusMessage = data["status_message"];
                Tag = data["tag"];
                Tradingsymbol = data["tradingsymbol"];
                TransactionType = data["transaction_type"];
                TriggerPrice = Convert.ToDecimal(data["trigger_price"]);
                Validity = data["validity"];
                Variety = data["variety"];
            }
            catch (Exception ex)
            {
                //throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public decimal AveragePrice { get; set; }
        public int CancelledQuantity { get; set; }
        public int DisclosedQuantity { get; set; }
        public string Exchange { get; set; }
        public string ExchangeOrderId { get; set; }
        public DateTime? ExchangeTimestamp { get; set; }
        public int FilledQuantity { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public string OrderId { get; set; }
        public DateTime? OrderTimestamp { get; set; }
        public string OrderType { get; set; }
        public string ParentOrderId { get; set; }
        public int PendingQuantity { get; set; }
        public string PlacedBy { get; set; }
        public decimal Price { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string StatusMessage { get; set; }
        public string Tag { get; set; }
        public string Tradingsymbol { get; set; }
        public string TransactionType { get; set; }
        public decimal TriggerPrice { get; set; }
        public string Validity { get; set; }
        public string Variety { get; set; }

        public int AlgoIndex { get; set; } = 0;
        public bool UpOrder { get; set; } = true;
        public int AlgoInstance { get; set; } = 0;
    }
    public class PriceRange
    {
        public decimal Upper;
        public decimal Lower;
        public decimal NextUpper;
        public decimal NextLower;
        public int UpperCandleIndex;
        public int LowerCandleIndex;
        public DateTime? CrossingTime;
    }
    public enum Breakout
    {
        NONE = 0,
        UP = 1,
        DOWN = -1
    };


}
