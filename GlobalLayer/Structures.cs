using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using GlobalLayer;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Google.Protobuf.WellKnownTypes;

namespace GlobalLayer
{
    /// <summary>
    /// Index of different algorithms
    /// </summary>
    public enum AlgoIndex
    {
        DeltaStrangle = 1,
        PriceStrangle = 2,
        BBStrangle = 3,
        FrequentBuySell = 4,
        FrequentBuySellWithMovement = 5,
        BBStrangleWithoutMovement = 6,
        BBStrangleWithoutMovement_Aggressive = 7,
        StrangleWithConstantWidth = 8,
        StrangleWithCutoff_Buy = 9,
        ActiveTradeWithVariableQty = 10,
        PivotedBasedTrade = 11,
        StrangleShiftToOneSide = 12,
        ManageStrangleWithMaxPain = 13,
        [Display(Name = "Expiry Strangle")]
        ExpiryTrade = 14,
        EMACross = 15,
        VolumeThreshold = 16,
        [Display(Name = "Momentum Trade - Option")]
        MomentumTrade_Option = 17,
        [Display(Name = "Sell On RSI Cross")]
        SellOnRSICross = 18,
        [Display(Name = "Strangle With RSI")]
        StrangleWithRSI = 19,
        MomentumBuyWithRSI = 20,
        MomentumBuyWithStraddle = 21,
        PremiumCross = 22,
        OptionBuyWithStraddle = 23,
        ManageReferenceStraddle = 24,
        IVSpreadTrade = 25,
        ChartSpreadTrade = 26,
        OptionOptimizer = 27,
        ValueSpreadTrade = 28,
        DeltaStrangleWithLevels = 29,
        GenerateAlert = 30,
        SellOnHistoricalAverage = 31,
        PAWithLevels = 32,
        StraddleOnIndexRange = 33,
        SchScalping = 34,
        CandleWickScalping = 35,
        InitialRangeBreakout = 36,
        StockInitialMomentum = 37,
        StraddleWithEachLegCutOff= 38,
        EMAScalpingKB = 39,
        MultipleEMAPriceAction = 40,
        MultiEMADirectionalLevels = 41,
        TJ2 = 42,
        MA = 43, //Market Alerts
        TJ3 = 44,
        TJ4 = 45,
        SARScalping = 46,
        OptionSellonHT = 47,
        MultiTimeFrameSellOnHT = 48,
        OptionSellOnOITrend = 49,
        TJ5 = 50,
        BC = 51,
        BC2 = 52,
        EST = 53,
        RangeBreakoutCandle = 54,
        EICT = 55,
        OptionSellOnEMA = 1032,
        KiteConnect = 1001,

        Crypto_PriceDirectionWithOption = 56,
        Crypto_ActiveStrangleBuy = 57,

    }
    public enum IndicatorType
    {
        EMA = 1,
        SMA = 2,
        RSI = 3,
        MACD = 4,
        Stochastic = 5,
        Candle_Open = 6,
        Candle_High = 7,
        Candle_Low = 8,
        Candle_Close = 9,
        Price = 10,
        Range_Breakout = 11
    }

    public enum CandlePrices
    {
        Open = 1,
        High = 2,
        Low = 3,
        Close = 4,
    }

    public enum MarketOpenRange
    {
        Sideways = 0,
        GapUp = 1,
        GapDown = 2,
        NA = 3
    }
    public enum TradeZone
    {
        Long = 0,
        Short = 1,
        Hold = 2,
        DoNotTrade = 3,
        Open = 4
    }
    public enum CurrentPostion
    {
        OutOfMarket = 0,
        Bought = 1,
        Sold = 2,
    }
    public enum CandleStates
    {
        Inprogress = 0,
        Finished = 1
    }
    public enum CandleType
    {
        Time = 0,
        Volume = 1,
        Money = 2,
        Renko = 3,
    }
    public enum CandleFormation
    {
        Bullish = 0,
        Bearish = 1,
        Indecisive = 3
    }
    public enum PutCallFlag
    {
        Put, Call
    }

    public enum OptionGreeks
    {
        Delta, Gamma, Vega, Theta, Rho
    }
    public enum InstrumentType : int
    {
        CE = 0,
        PE = 1,
        Fut = 2,
        ALL = 3
    }

    public enum PositionStatus
    {
        Closed = 0,
        Open = 1,
        NotTraded = 2,
        PercentClosed25 = 3,
        PercentClosed50 = 4,
        PercentClosed75 = 5
    }
    public enum TradeStatus
    {
        Closed = 0,
        Open = 1
    }
    public class LogData
    {
        public string AlgoIndex { get; set; }
        public int AlgoInstance { get; set; }
        public DateTime LogTime { get; set; }

        public LogLevel Level { get; set; }

        public string Message { get; set; }

        public string SourceMethod { get; set; }
    }


    //
    // Summary:
    //     Defines log levels.
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Stop = 5,
        Health = 6
    }

    [Serializable]
    public class TickKey
    {
        public UInt32 InstrumentToken { get; set; }
        public DateTime? Timestamp { get; set; }
    }
    [Serializable]
    public class FyerTick
    {
        public decimal Ltp { get; set; }
        public decimal Vol_traded_today { get; set; }
        public decimal Last_traded_time { get; set; }
        public decimal Exch_feed_time { get; set; }
        public decimal Bid_size { get; set; }
        public decimal Ask_size { get; set; }
        public decimal Bid_price { get; set; }
        public decimal Ask_price { get; set; }
        public uint Last_traded_qty { get; set; }
        public uint Tot_buy_qty { get; set; }
        public uint Tot_sell_qty { get; set; }
        public decimal Avg_trade_price { get; set; }
        public decimal Low_price { get; set; }
        public decimal High_price { get; set; }
        public decimal Yhigh { get; set; }
        public decimal Ylow { get; set; }
        public decimal Lower_ckt { get; set; }
        public decimal Upper_ckt { get; set; }
        public decimal Open_price { get; set; }
        public decimal Prev_close_price { get; set; }
        public decimal Ch { get; set; }
        public decimal Chp { get; set; }
        public decimal Turnover { get; set; }
        public string Type { get; set; }
        public string Symbol { get; set; }


    //      "ltp": 1163.95,
    //"vol_traded_today": 573412.0,
    //"last_traded_time": 1713165342.0,
    //"exch_feed_time": 1713165342.0,
    //"bid_size": 8.0,
    //"ask_size": 5.0,
    //"bid_price": 1163.75,
    //"ask_price": 1164.3,
    //"last_traded_qty": 12.0,
    //"tot_buy_qty": 109026.0,
    //"tot_sell_qty": 120039.0,
    //"avg_trade_price": 1164.05,
    //"low_price": 1155.0,
    //"high_price": 1176.5,
    //"Yhigh": 1330.0,
    //"Ylow": 748.4,
    //"lower_ckt": 1052.15,
    //"upper_ckt": 1285.95,
    //"open_price": 1155.0,
    //"prev_close_price": 1169.05,
    //"ch": -5.1,
    //"chp": -0.44,
    //"turnover": 66748023860.0,
    //"type": "sf",
    //"symbol": "NSE:BHARATFORG-EQ"
    //public Tick ToTick(bool tradable)
    //    {
    //        new Tick()
    //        {
    //            AveragePrice = Avg_trade_price,
    //            BuyQuantity = Tot_buy_qty,
    //            Change = Ch,
    //            Close = Ltp,
    //            High = High_price,
    //            LastPrice = Ltp,
    //            LastQuantity = Last_traded_qty,
    //            LastTradeTime = Date Last_traded_time,
    //            Low = Low_price,
    //            Open = Open_price,
    //            SellQuantity = Tot_sell_qty,
    //            Timestamp = Exch_feed_time,
    //            Tradable=tradable,
    //             Volume=Vol_traded_today
    //        }
    //    }
    }


    [Serializable]
    public class FyerTickIndex
    {
        public decimal Ltp { get; set; }
        public decimal Exch_feed_time { get; set; }
        public decimal prev_close_price { get; set; }
        public decimal Low_price { get; set; }
        public decimal High_price { get; set; }
        public decimal Open_price { get; set; }
        public decimal Ch { get; set; }
        public decimal Chp { get; set; }
        public string Type { get; set; }
        public string Symbol { get; set; }

        /*
         * "ltp": 22373.45,
    "prev_close_price": 22519.4,
    "exch_feed_time": 1713165343.0,
    "high_price": 22427.45,
    "low_price": 22263.55,
    "open_price": 22339.05,
    "ch": -145.95,
    "chp": -0.65,
    "type": "if",
    "symbol": "NSE:NIFTY50-INDEX"
         */

    }

    [Serializable]
    public class FyerTickLite
    {
        public decimal Ltp { get; set; }
        public decimal Ch { get; set; }
        public decimal Chp { get; set; }
        public string Type { get; set; }
        public string Symbol { get; set; }

        /*
             "ltp": 1164.35,
            "ch": -4.7,
            "chp": -0.4,
            "type": "sf",
            "symbol": "NSE:BHARATFORG-EQ"
         */

    }
    /// <summary>
    /// Tick data structure
    /// </summary>
    [Serializable]
    public class Tick
    {
        public string Mode { get; set; }
        public UInt32 InstrumentToken { get; set; }

            public string Symbol { get; set; }
            
            public bool Tradable { get; set; }
        public decimal LastPrice { get; set; }
        public UInt32 LastQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public UInt32 Volume { get; set; }
        public UInt32 BuyQuantity { get; set; }
        public UInt32 SellQuantity { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Change { get; set; }
        public DepthItem[] Bids { get; set; }
        public DepthItem[] Offers { get; set; }

        // KiteConnect 3 Fields

        public DateTime? LastTradeTime { get; set; }
        public UInt32 OI { get; set; }
        public UInt32 OIDayHigh { get; set; }
        public UInt32 OIDayLow { get; set; }
        public DateTime? Timestamp { get; set; }

        public Tick(JToken data)
        {
                //      "ltp": 1163.95,
                //"vol_traded_today": 573412.0,
                //"last_traded_time": 1713165342.0,
                //"exch_feed_time": 1713165342.0,
                //"bid_size": 8.0,
                //"ask_size": 5.0,
                //"bid_price": 1163.75,
                //"ask_price": 1164.3,
                //"last_traded_qty": 12.0,
                //"tot_buy_qty": 109026.0,
                //"tot_sell_qty": 120039.0,
                //"avg_trade_price": 1164.05,
                //"low_price": 1155.0,
                //"high_price": 1176.5,
                //"Yhigh": 1330.0,
                //"Ylow": 748.4,
                //"lower_ckt": 1052.15,
                //"upper_ckt": 1285.95,
                //"open_price": 1155.0,
                //"prev_close_price": 1169.05,
                //"ch": -5.1,
                //"chp": -0.44,
                //"turnover": 66748023860.0,
                //"type": "sf",
                //"symbol": "NSE:BHARATFORG-EQ"

            
                LastPrice= Convert.ToDecimal(data[0]);
                Volume = Convert.ToUInt32(data[1]);
                LastTradeTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds((double)data[2]);
                Timestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds((double)data[3]);
                LastQuantity = Convert.ToUInt32(data[8]);
                BuyQuantity = Convert.ToUInt32(data[9]);
                SellQuantity = Convert.ToUInt32(data[10]);
                AveragePrice = Convert.ToDecimal(data[11]);
                Low = Convert.ToDecimal(data[12]);
                High = Convert.ToDecimal(data[13]);
                Open = Convert.ToUInt32(data[18]);
                Symbol = Convert.ToString(data[24]);
        }
        public Tick()
        {

        }
    }
    public class AlgoMetaData
    {
        public int AlgoID { get; set; }

        public string AlgoName { get; set; }

        public int AlgoInstance { get; set; }

    }

    [Serializable]
    public class MData
    {
        //this is like a algo id to segreate data between different types. Once may be filling one table and another for another table.
        public int AlgoID { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public decimal LastPrice { get; set; }
        public int CandleTimeSpan { get; set; }
        public UInt32 BaseInstrumentToken { get; set; }
        
        //collate with * different properties
        public string Message { get; set; }

        //include all the below under messages. This will make it generic
        //try for RSI during that period or macd during this period. correlation price change within this period.
        //public decimal OIChange { get; set; }
        //public decimal PriceChange { get; set; }
        //public decimal VolumeChange { get; set; }

        public DateTime? Timestamp { get; set; }
    }

    [Serializable]
    public class Alert
    {
        public Guid ID { get; set; }
        public int AlertTriggerID { get; set; }
        public uint InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }

        public string UserId { get; set; }

        public decimal LastPrice { get; set; }

        //this may not be needed, but message is needed at the trigger level while setting an alert
        //public string Message { get; set; }
        public int CandleTimeSpan { get; set; }

        //Time when the alert was triggered. Once alert with ID will trigger only once. AlertTrigger can trigger multile alerts.
        public DateTime TriggeredDateTime { get; set; }

        //collection of alert mode ids
        public string AlertModes { get; set; }

        public string Message { get; set; }

        //Criteria that triggered the alert
        public string Criteria { get; set; }
    }

    public enum AlertModes
    {
        WEB_PAGE = 1,
        MOBILE_APP = 2,
        PHONE_SMS = 3,
        WHATSAPP = 4,
        EMAIL = 5
    }

    /// <summary>
	/// Base candle class (contains main parameters).
	/// </summary>
	[DataContract]
    [Serializable]
    public abstract class Candle : IConvertible
    {
        ///// <summary>
        ///// Instrument.
        ///// </summary>
        //[DataMember]
        //public Instrument Instrument { get; set; }

        /// <summary>
        /// Instrument.
        /// </summary>
        [DataMember]
        public uint InstrumentToken { get; set; }

        /// <summary>
        /// Open time.
        /// </summary>
        [DataMember]
        public DateTime OpenTime { get; set; }

        /// <summary>
        /// Close time.
        /// </summary>
        [DataMember]
        public DateTime CloseTime { get; set; }

        /// <summary>
        /// High time.
        /// </summary>
        [DataMember]
        public DateTime HighTime { get; set; }

        /// <summary>
        /// Low time.
        /// </summary>
        [DataMember]
        public DateTime LowTime { get; set; }

        /// <summary>
        /// Opening price.
        /// </summary>
        [DataMember]
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// Closing price.
        /// </summary>
        [DataMember]
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// Highest price.
        /// </summary>
        [DataMember]
        public decimal HighPrice { get; set; }

        /// <summary>
        /// Lowest price.
        /// </summary>
        [DataMember]
        public decimal LowPrice { get; set; }

        /// <summary>
        /// Total price size.
        /// </summary>
        [DataMember]
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Volume at open.
        /// </summary>
        [DataMember]
        public decimal? OpenVolume { get; set; }

        /// <summary>
        /// Volume at close.
        /// </summary>
        [DataMember]
        public decimal? CloseVolume { get; set; }

        /// <summary>
        /// Volume at high.
        /// </summary>
        [DataMember]
        public decimal? HighVolume { get; set; }

        /// <summary>
        /// Volume at low.
        /// </summary>
        [DataMember]
        public decimal? LowVolume { get; set; }

        /// <summary>
        /// Total volume.
        /// </summary>
        [DataMember]
        public decimal? TotalVolume { get; set; }

        /// <summary>
        /// Relative volume.
        /// </summary>
        [DataMember]
        public decimal? RelativeVolume { get; set; } = 0;

        /// <summary>
        /// Candle arg.
        /// </summary>
        public abstract object Arg { get; set; }

        /// <summary>
        /// Number of ticks.
        /// </summary>
        [DataMember]
        public int? TotalTicks { get; set; }

        /// <summary>
        /// Number of up trending ticks.
        /// </summary>
        [DataMember]
        public int? UpTicks { get; set; }
        
        /// <summary>
        /// Number of up trending ticks.
        /// </summary>
        [DataMember]
        public bool? Final { get; set; }

        /// <summary>
        /// Number of up trending ticks.
        /// </summary>
        [DataMember]
        public abstract CandleType CandleType { get; }

        /// <summary>
        /// Number of down trending ticks.
        /// </summary>
        [DataMember]
        public int? DownTicks { get; set; }

        private CandleStates _state;

        /// <summary>
        /// State.
        /// </summary>
        [DataMember]
        public CandleStates State
        {
            get => _state;
            set
            {
                //ThrowIfFinished();
                _state = value;
            }
        }
        public CandlePrices CandlePrices { get; set; }
        /// <summary>
        /// Price levels.
        /// </summary>
        [DataMember]
        public IEnumerable<CandlePriceLevel> PriceLevels { get; set; }

        /// <summary>
        /// <see cref="PriceLevels"/> with minimum <see cref="CandlePriceLevel.TotalVolume"/>.
        /// </summary>
        public CandlePriceLevel MinPriceLevel => PriceLevels?.OrderBy(l => l.TotalVolume).FirstOrDefault();

        /// <summary>
        /// <see cref="PriceLevels"/> with maximum <see cref="CandlePriceLevel.TotalVolume"/>.
        /// </summary>
        public CandlePriceLevel MaxPriceLevel => PriceLevels?.OrderByDescending(l => l.TotalVolume).FirstOrDefault();

        /// <summary>
        /// Open interest.
        /// </summary>
        [DataMember]
        public decimal? OpenInterest { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return String.Format("{0:HH:mm:ss} {1} (O:{2}, H:{3}, L:{4}, C:{5}, V:{6})",
                OpenTime, GetType().Name + "_" + InstrumentToken + "_" + Arg, OpenPrice, HighPrice, LowPrice, ClosePrice, TotalVolume);
        }

        private void ThrowIfFinished()
        {
            if (State == CandleStates.Finished)
                throw new InvalidOperationException("Candle Finished");
        }

        public void LoadCandle(DataRow candleRow)
        {
            InstrumentToken = Convert.ToUInt32(candleRow["instrumentToken"]);
            ClosePrice = Convert.ToDecimal(candleRow["closePrice"]);
            CloseTime = Convert.ToDateTime(candleRow["CloseTime"]);
            CloseVolume = Convert.ToDecimal(candleRow["closeVolume"]);
            DownTicks = Convert.ToInt32(candleRow["downTicks"]);
            HighPrice = Convert.ToDecimal(candleRow["highPrice"]);

            HighTime = Convert.ToDateTime(candleRow["highTime"]);
            HighVolume = Convert.ToDecimal(candleRow["highVolume"]);
            LowPrice = Convert.ToDecimal(candleRow["lowPrice"]);
            LowTime = Convert.ToDateTime(candleRow["lowTime"]);
            LowVolume = Convert.ToDecimal(candleRow["lowVolume"]);

            //List<CandlePriceLevel> candlePriceLevels = new List<CandlePriceLevel>();
            //foreach (DataRow drPriceLevel in candleRow.GetChildRows("Candle_PriceLevel"))
            //{
            //    CandlePriceLevel candlePriceLevel = new CandlePriceLevel(Convert.ToDecimal(drPriceLevel["Price"]));

            //    candlePriceLevel.BuyCount = Convert.ToInt32(drPriceLevel["BuyCount"]);
            //    candlePriceLevel.BuyVolume = Convert.ToInt32(drPriceLevel["BuyVolume"]);
            //    candlePriceLevel.SellCount = Convert.ToInt32(drPriceLevel["SellCount"]);
            //    candlePriceLevel.SellVolume = Convert.ToInt32(drPriceLevel["SellVolume"]);
            //    candlePriceLevel.TotalVolume = Convert.ToInt32(drPriceLevel["TotalVolume"]);
            //    candlePriceLevel.CandleType = (CandleType)Convert.ToInt32(drPriceLevel["CandleType"]);
            //    candlePriceLevels.Add(candlePriceLevel);
            //}

            //PriceLevels = candlePriceLevels;

            OpenInterest = Convert.ToUInt32(candleRow["openInterest"]);
            OpenPrice = Convert.ToDecimal(candleRow["openPrice"]);
            OpenTime = Convert.ToDateTime(candleRow["openTime"]);
            OpenVolume = Convert.ToDecimal(candleRow["openVolume"]);
            RelativeVolume = Convert.ToDecimal(candleRow["relativeVolume"]);
            TotalPrice = Convert.ToDecimal(candleRow["totalPrice"]);
            TotalTicks = Convert.ToInt32(candleRow["totalTicks"]);
            TotalVolume = Convert.ToUInt32(candleRow["totalVolume"]);
            UpTicks = Convert.ToInt32(candleRow["upTicks"]);
            //Historical candles are finished ones
            State = CandleStates.Finished;

            //Arg = Convert.ToUInt32(candleRow["Arg"]);
        }

        public TypeCode GetTypeCode()
        {
            throw new NotImplementedException();
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public byte ToByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public char ToChar(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            decimal currentValue = 0;
            switch (CandlePrices)
            {
                case CandlePrices.Open:
                    currentValue = OpenPrice;
                    break;
                case CandlePrices.High:
                    currentValue = HighPrice;
                    break;
                case CandlePrices.Low:
                    currentValue = LowPrice;
                    break;
                case CandlePrices.Close:
                    currentValue = ClosePrice;
                    break;
                default:
                    currentValue = ClosePrice;
                    break;
            }
            return currentValue;
        }

        public double ToDouble(IFormatProvider provider)
        {
            return (double)Convert.ChangeType(ClosePrice, typeof(double));
        }

        public short ToInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public object ToType(System.Type TimeFrameCandle, IFormatProvider provider)
        {
            return new TimeFrameCandle()
            {
                Final = this.Final,
                OpenTime = this.OpenTime,
                CloseTime = this.CloseTime,
                HighTime = this.HighTime,
                LowTime = this.LowTime,
                ClosePrice = this.ClosePrice,
                HighPrice = this.HighPrice,
                LowPrice = this.LowPrice,
                OpenPrice = this.OpenPrice,
                InstrumentToken = this.InstrumentToken,
                State = this.State,
                CandlePrices = this.CandlePrices
            };
        }
        
        //public object ToType(Type Candle, IFormatProvider provider)
        //{
        //    return new TimeFrameCandle() { ClosePrice = this.ClosePrice, HighPrice = this.HighPrice, LowPrice = this.LowPrice, OpenPrice = this.OpenPrice };
        //}

        public ushort ToUInt16(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }
        public T GetValue<T>(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public  T GetValue<T>()
        {
            return (T)Convert.ChangeType(ClosePrice, typeof(T));
        }

        ////public void LoadCandles(DataSet dsCandles, Candle candle)
        //public void LoadCandles(int numberOfCandles, Candle candle)
        //{
        //	DataLogic dl = new DataLogic();
        //	DataSet dsCandles = dl.LoadCandles(numberOfCandles, this.CandleType);

        //	List<Candle> candleList = new List<Candle>();

        //	DataRelation strangle_Token_Relation = dsCandles.Relations.Add("Candle_PriceLevel", new DataColumn[] { dsCandles.Tables[0].Columns["ID"], dsCandles.Tables[0].Columns["CandleType"] },
        //		new DataColumn[] { dsCandles.Tables[1].Columns["CandleId"], dsCandles.Tables[1].Columns["CandleType"] });

        //	foreach (DataRow candleRow in dsCandles.Tables[0].Rows)
        //	{
        //		//Candle candle = new VolumeCandle();
        //		//candle.LoadCandles(candleRow);
        //		candle.InstrumentToken = Convert.ToUInt32(candleRow["instrumentToken"]);
        //		candle.ClosePrice = Convert.ToDecimal(candleRow["closePrice"]);
        //		candle.CloseTime = Convert.ToDateTime(candleRow["CloseTime"]);
        //		candle.CloseVolume = Convert.ToDecimal(candleRow["closeVolume"]);
        //		candle.DownTicks = Convert.ToInt32(candleRow["downTicks"]);
        //		candle.HighPrice = Convert.ToDecimal(candleRow["highPrice"]);

        //		candle.HighTime = Convert.ToDateTime(candleRow["highTime"]);
        //		candle.HighVolume = Convert.ToDecimal(candleRow["highVolume"]);
        //		candle.LowPrice = Convert.ToDecimal(candleRow["lowPrice"]);
        //		candle.LowTime = Convert.ToDateTime(candleRow["lowTime"]);
        //		candle.LowVolume = Convert.ToDecimal(candleRow["lowVolume"]);

        //		List<CandlePriceLevel> candlePriceLevels = new List<CandlePriceLevel>();
        //		foreach (DataRow drPriceLevel in candleRow.GetChildRows("Candle_PriceLevel"))
        //		{
        //			CandlePriceLevel candlePriceLevel = new CandlePriceLevel(Convert.ToDecimal(drPriceLevel["Price"]));

        //			candlePriceLevel.BuyCount = Convert.ToInt32(drPriceLevel["BuyCount"]);
        //			candlePriceLevel.BuyVolume = Convert.ToInt32(drPriceLevel["BuyVolume"]);
        //			candlePriceLevel.SellCount = Convert.ToInt32(drPriceLevel["SellCount"]);
        //			candlePriceLevel.SellVolume = Convert.ToInt32(drPriceLevel["SellVolume"]);
        //			candlePriceLevel.TotalVolume = Convert.ToInt32(drPriceLevel["TotalVolume"]);
        //			candlePriceLevel.CandleType = (CandleType)Convert.ToInt32(drPriceLevel["CandleType"]);
        //			candlePriceLevels.Add(candlePriceLevel);
        //		}

        //		candle.PriceLevels = candlePriceLevels;

        //		candle.OpenInterest = Convert.ToUInt32(candleRow["openInterest"]);
        //		candle.OpenPrice = Convert.ToDecimal(candleRow["openPrice"]);
        //		candle.OpenTime = Convert.ToDateTime(candleRow["openTime"]);
        //		candle.OpenVolume = Convert.ToDecimal(candleRow["openVolume"]);
        //		candle.RelativeVolume = Convert.ToDecimal(candleRow["relativeVolume"]);
        //		candle.TotalPrice = Convert.ToDecimal(candleRow["totalPrice"]);
        //		candle.TotalTicks = Convert.ToInt32(candleRow["totalTicks"]);
        //		candle.TotalVolume = Convert.ToDecimal(candleRow["totalVolume"]);
        //		candle.UpTicks = Convert.ToInt32(candleRow["upTicks"]);

        //		candle.Arg = Convert.ToUInt32(candleRow["Arg"]);

        //		candleList.Add(candle);
        //	}
        //}

        ///// <summary>
        ///// Copy the message into the <paramref name="destination" />.
        ///// </summary>
        ///// <typeparam name="TCandle">The candle type.</typeparam>
        ///// <param name="destination">The object, to which copied information.</param>
        ///// <returns>The object, to which copied information.</returns>
        //protected TCandle CopyTo<TCandle>(TCandle destination)
        //	where TCandle : Candle
        //{
        //	destination.Arg = Arg;
        //	destination.ClosePrice = ClosePrice;
        //	destination.CloseTime = CloseTime;
        //	destination.CloseVolume = CloseVolume;
        //	destination.DownTicks = DownTicks;
        //	destination.HighPrice = HighPrice;
        //	destination.HighTime = HighTime;
        //	destination.HighVolume = HighVolume;
        //	destination.LowPrice = LowPrice;
        //	destination.LowTime = LowTime;
        //	destination.LowVolume = LowVolume;
        //	destination.OpenInterest = OpenInterest;
        //	destination.OpenPrice = OpenPrice;
        //	destination.OpenTime = OpenTime;
        //	destination.OpenVolume = OpenVolume;
        //	destination.RelativeVolume = RelativeVolume;
        //	destination.Instrument = this.Instrument;
        //	//destination.Series = Series;
        //	//destination.Source = Source;
        //	//destination.State = State;
        //	destination.TotalPrice = TotalPrice;
        //	destination.TotalTicks = TotalTicks;
        //	destination.TotalVolume = TotalVolume;
        //	//destination.VolumeProfileInfo = VolumeProfileInfo;
        //	destination.PriceLevels = PriceLevels?.Select(l => l.Clone()).ToArray();

        //	return destination;
        //}
    }

    public class TickComparer : IComparer<Tick>
    {
        public int Compare(Tick x, Tick y)
        {
            return (new CaseInsensitiveComparer()).Compare(x.LastTradeTime, y.LastTradeTime);
        }
    }

    /// <summary>
    /// Time-frame candle.
    /// </summary>
    [DataContract]
    [Serializable]
    public class TimeFrameCandle : Candle, IConvertible
    {
        /// <summary>
        /// Time-frame.
        /// </summary>
        [DataMember]
        public TimeSpan TimeFrame { get; set; }

        //[DataMember]
        //public DateTime StartTime { get; set; }
        /// <inheritdoc />
        public override object Arg
        {
            get => TimeFrame;
            set => TimeFrame = (TimeSpan)value;
        }
        public override CandleType CandleType
        {
            get => CandleType.Time;
        }
        //public object Arg2
        //{
        //	get => StartTime;
        //	set => StartTime = (DateTime)value;
        //}
        /////<summary>
        // ///Create a copy of<see cref= "TimeFrameCandle" />.
        /////</ summary >
        // ///< returns > Copy.</ returns >
        //public override Candle Clone()
        //{
        //    return CopyTo(new TimeFrameCandle());
        //}

    }

    /// <summary>
    /// Tick candle.
    /// </summary>
    [DataContract]
    [Serializable]
    public class TickCandle : Candle
    {
        private int _maxTradeCount;

        /// <summary>
        /// Maximum tick count.
        /// </summary>
        [DataMember]
        public int MaxTradeCount
        {
            get => _maxTradeCount;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _maxTradeCount = value;
            }
        }

        /// <inheritdoc />
        public override object Arg
        {
            get => MaxTradeCount;
            set => MaxTradeCount = (int)value;
        }

        /// <inheritdoc />
        public override CandleType CandleType
        {
            get => CandleType.Volume;
        }

        /// <summary>
        /// Create a copy of <see cref="TickCandle"/>.
        /// </summary>
        /// <returns>Copy.</returns>
        //public override Candle Clone()
        //{
        //	return CopyTo(new TickCandle());
        //}
    }

    /// <summary>
    /// Volume candle.
    /// </summary>
    [DataContract]
    [Serializable]
    public class VolumeCandle : Candle
    {
        private decimal _volume;

        /// <summary>
        /// Maximum volume.
        /// </summary>
        [DataMember]
        public decimal Volume
        {
            get => _volume;
            set
            {
                //if (value < 0)
                //	throw new ArgumentOutOfRangeException(nameof(value));

                _volume = value;
            }
        }

        /// <inheritdoc />
        public override object Arg
        {
            get => Volume;
            set => Volume = (int)value;
        }
        /// <inheritdoc />
        public override CandleType CandleType
        {
            get => CandleType.Volume;
        }

        /// <summary>
        /// Create a copy of <see cref="VolumeCandle"/>.
        /// </summary>
        /// <returns>Copy.</returns>
        //public override Candle Clone()
        //{
        //	return CopyTo(new VolumeCandle());
        //}
    }

    /// <summary>
    /// Volume candle.
    /// </summary>
    [DataContract]
    [Serializable]
    public class MoneyCandle : Candle
    {
        private decimal _money;

        /// <summary>
        /// Maximum volume.
        /// </summary>
        [DataMember]
        public decimal Money
        {
            get => _money;
            set
            {
                //if (value < 0)
                //	throw new ArgumentOutOfRangeException(nameof(value));

                _money = value;
            }
        }

        //candleType = CandleType.Money;

        /// <inheritdoc />
        public override CandleType CandleType
        {
            get => CandleType.Money;
        }
        /// <inheritdoc />
        public override object Arg
        {
            get => Money;
            set => Money = (decimal)value;
        }

        /// <summary>
        /// <see cref="PriceLevels"/> with minimum <see cref="CandlePriceLevel.TotalVolume"/>.
        /// </summary>
        public CandlePriceLevel MinMoneyLevel => PriceLevels?.OrderBy(l => l.TotalVolume * l.Price).FirstOrDefault();

        /// <summary>
        /// <see cref="PriceLevels"/> with maximum <see cref="CandlePriceLevel.TotalVolume"/>.
        /// </summary>
        public CandlePriceLevel MaxMoneyLevel => PriceLevels?.OrderByDescending(l => l.TotalVolume * l.Price).FirstOrDefault();


        /// <summary>
        /// Create a copy of <see cref="VolumeCandle"/>.
        /// </summary>
        /// <returns>Copy.</returns>
        //public override Candle Clone()
        //{
        //	return CopyTo(new VolumeCandle());
        //}
    }

    public class CandlePriceLevel : IComparer<CandlePriceLevel>
    {
        public decimal Price { get; set; }
        public int BuyCount { get; set; } = 0;
        public decimal BuyVolume { get; set; } = 0;
        public int SellCount { get; set; } = 0;
        public decimal SellVolume { get; set; } = 0;
        public decimal TotalVolume { get; set; } = 0;
        public CandleType CandleType { get; set; }

        public decimal Money { get => Price * TotalVolume; }
        public CandlePriceLevel(decimal price)
        {
            Price = price;
        }

        public int Compare(CandlePriceLevel priceLevel1, CandlePriceLevel priceLevel2)
        {
            return priceLevel1.Money.CompareTo(priceLevel2.Money);
        }
    }

    ///// <summary>
    ///// Range candle.
    ///// </summary>
    //[DataContract]
    //[Serializable]
    //public class RangeCandle : Candle
    //{
    //	private Unit _priceRange;

    //	/// <summary>
    //	/// Range of price.
    //	/// </summary>
    //	[DataMember]
    //	public Unit PriceRange
    //	{
    //		get => _priceRange;
    //		set => _priceRange = value ?? throw new ArgumentNullException(nameof(value));
    //	}

    //	/// <inheritdoc />
    //	public override object Arg
    //	{
    //		get => PriceRange;
    //		set => PriceRange = (Unit)value;
    //	}

    //	/// <summary>
    //	/// Create a copy of <see cref="RangeCandle"/>.
    //	/// </summary>
    //	/// <returns>Copy.</returns>
    //	public override Candle Clone()
    //	{
    //		return CopyTo(new RangeCandle());
    //	}
    //}

    ///// <summary>
    ///// The candle of point-and-figure chart (tac-toe chart).
    ///// </summary>
    //[DataContract]
    //[Serializable]
    //public class PnFCandle : Candle
    //{
    //	private PnFArg _pnFArg;

    //	/// <summary>
    //	/// Value of arguments.
    //	/// </summary>
    //	[DataMember]
    //	public PnFArg PnFArg
    //	{
    //		get => _pnFArg;
    //		set => _pnFArg = value ?? throw new ArgumentNullException(nameof(value));
    //	}

    //	///// <summary>
    //	///// Type of symbols.
    //	///// </summary>
    //	//[DataMember]
    //	//public PnFTypes Type { get; set; }

    //	/// <inheritdoc />
    //	public override object Arg
    //	{
    //		get => PnFArg;
    //		set => PnFArg = (PnFArg)value;
    //	}

    //	/// <summary>
    //	/// Create a copy of <see cref="PnFCandle"/>.
    //	/// </summary>
    //	/// <returns>Copy.</returns>
    //	public override Candle Clone()
    //	{
    //		return CopyTo(new PnFCandle());
    //	}
    //}

    /// <summary>
    /// Renko candle.
    /// </summary>
    [DataContract]
    [Serializable]
    public class RenkoCandle : Candle
    {
        private uint _boxSize;

        /// <summary>
        /// Possible price change range.
        /// </summary>
        [DataMember]
        public uint BoxSize
        {
            get => _boxSize;
            set => _boxSize = value;
        }

        /// <inheritdoc />
        public override object Arg
        {
            get => BoxSize;
            set => BoxSize = (uint)value;
        }

        ///// <summary>
        ///// Create a copy of <see cref="RenkoCandle"/>.
        ///// </summary>
        ///// <returns>Copy.</returns>
        //public override Candle Clone()
        //{
        //	return CopyTo(new RenkoCandle());
        //}

        public override CandleType CandleType
        {
            get => CandleType.Renko;
        }
    }

    /// <summary>
    /// Heikin ashi candle.
    /// </summary>
    [DataContract]
    [Serializable]
    public class HeikinAshiCandle : TimeFrameCandle
    {
        /// <summary>
        /// Create a copy of <see cref="HeikinAshiCandle"/>.
        /// </summary>
        ///// <returns>Copy.</returns>
        //public override Candle Clone()
        //{
        //    return CopyTo(new HeikinAshiCandle());
        //}
    }

    /// <summary>
    /// Market depth item structure
    /// </summary>
    [Serializable]
    public struct DepthItem
    {
        public DepthItem(Dictionary<string, dynamic> data)
        {
            Quantity = Convert.ToUInt32(data["quantity"]);
            Price = data["price"];
            Orders = Convert.ToUInt32(data["orders"]);
        }

        public UInt32 Quantity { get; set; }
        public decimal Price { get; set; }
        public UInt32 Orders { get; set; }
    }

    /// <summary>
    /// Historical structure
    /// </summary>
    public class Historical : IComparable<Historical>
    {
        public Historical(ArrayList data)
        {
            TimeStamp = Convert.ToDateTime(data[0]);
            Open = Convert.ToDecimal(data[1]);
            High = Convert.ToDecimal(data[2]);
            Low = Convert.ToDecimal(data[3]);
            Close = Convert.ToDecimal(data[4]);
            Volume = Convert.ToUInt32(data[5]);
            InstrumentToken = data.Count > 6 ? Convert.ToUInt32(data[6]) : 0;
        }
        public Historical(DataRow data)
        {
            TimeStamp = Convert.ToDateTime(data[0]);
            Open = Convert.ToDecimal(data[1]);
            High = Convert.ToDecimal(data[2]);
            Low = Convert.ToDecimal(data[3]);
            Close = Convert.ToDecimal(data[4]);
            Volume = Convert.ToUInt32(data[5]);
            InstrumentToken = Convert.ToUInt32(data[6]);
        }
        public Historical(JToken data)
        {
            TimeStamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified).AddSeconds((double)data[0]); 
            Open = Convert.ToDecimal(data[1]);
            High = Convert.ToDecimal(data[2]);
            Low = Convert.ToDecimal(data[3]);
            Close = Convert.ToDecimal(data[4]);
            Volume = Convert.ToUInt32(data[5]);
        }

        public Historical()
        {
        }

        public DateTime TimeStamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public UInt32 Volume { get; set; }

        public uint InstrumentToken { set; get; }

        public int CompareTo(Historical x)
        {
            int result = TimeStamp.CompareTo(x.TimeStamp);

            //if (result == 0)
            //    return 1; // Handle equality as being greater. Note: this will break Remove(key) or
            //else          // IndexOfKey(key) since the comparer never returns 0 to signal key equality
                return result;
        }
    }
    //public class HistoricalComparer<Historical> : IComparer<Historical> where Historical : IComparable
    //{
    //    #region IComparer<TKey> Members

    //    public int Compare(Historical x, Historical y)
    //    {
    //        int result = x.TimeStamp.CompareTo(y.TimeStamp);

    //        if (result == 0)
    //            return 1; 
    //        else          
    //            return result;
    //    }

    //    #endregion
    //}
    public class HistoricalData
    {
        public Historical HistoricalValue { get; set; }
        public uint InstrumentToken { get; set; }
    }

    /// <summary>
    /// Holding structure
    /// </summary>
    public struct Holding
    {
        public Holding(Dictionary<string, dynamic> data)
        {
            try
            {
                Product = data["product"];
                Exchange = data["exchange"];
                Price = data["price"];
                LastPrice = data["last_price"];
                CollateralQuantity = data["collateral_quantity"];
                PNL = data["pnl"];
                ClosePrice = data["close_price"];
                AveragePrice = data["average_price"];
                TradingSymbol = data["tradingsymbol"];
                CollateralType = data["collateral_type"];
                T1Quantity = data["t1_quantity"];
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                ISIN = data["isin"];
                RealisedQuantity = data["realised_quantity"];
                Quantity = data["quantity"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public string Product { get; set; }
        public string Exchange { get; set; }
        public decimal Price { get; set; }
        public decimal LastPrice { get; set; }
        public int CollateralQuantity { get; set; }
        public decimal PNL { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal AveragePrice { get; set; }
        public string TradingSymbol { get; set; }
        public string CollateralType { get; set; }
        public int T1Quantity { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public string ISIN { get; set; }
        public int RealisedQuantity { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Available margin structure
    /// </summary>
    public struct AvailableMargin
    {
        public AvailableMargin(Dictionary<string, dynamic> data)
        {
            try
            {
                AdHocMargin = data["adhoc_margin"];
                Cash = data["cash"];
                Collateral = data["collateral"];
                IntradayPayin = data["intraday_payin"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public decimal AdHocMargin { get; set; }
        public decimal Cash { get; set; }
        public decimal Collateral { get; set; }
        public decimal IntradayPayin { get; set; }
    }


    /// <summary>
    /// Utilised margin structure
    /// </summary>
    public struct UtilisedMargin
    {
        public UtilisedMargin(Dictionary<string, dynamic> data)
        {
            try
            {
                Debits = data["debits"];
                Exposure = data["exposure"];
                M2MRealised = data["m2m_realised"];
                M2MUnrealised = data["m2m_unrealised"];
                OptionPremium = data["option_premium"];
                Payout = data["payout"];
                Span = data["span"];
                HoldingSales = data["holding_sales"];
                Turnover = data["turnover"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public decimal Debits { get; set; }
        public decimal Exposure { get; set; }
        public decimal M2MRealised { get; set; }
        public decimal M2MUnrealised { get; set; }
        public decimal OptionPremium { get; set; }
        public decimal Payout { get; set; }
        public decimal Span { get; set; }
        public decimal HoldingSales { get; set; }
        public decimal Turnover { get; set; }

    }

    /// <summary>
    /// UserMargin structure
    /// </summary>
    public struct UserMargin
    {
        public UserMargin(Dictionary<string, dynamic> data)
        {
            try
            {
                Enabled = data["enabled"];
                Net = data["net"];
                Available = new AvailableMargin(data["available"]);
                Utilised = new UtilisedMargin(data["utilised"]);
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public bool Enabled { get; set; }
        public decimal Net { get; set; }
        public AvailableMargin Available { get; set; }
        public UtilisedMargin Utilised { get; set; }
    }

    /// <summary>
    /// User margins response structure
    /// </summary>
    public struct UserMarginsResponse
    {
        public UserMarginsResponse(Dictionary<string, dynamic> data)
        {
            try
            {
                Equity = new UserMargin(data["equity"]);
                Commodity = new UserMargin(data["commodity"]);
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }
        public UserMargin Equity { get; set; }
        public UserMargin Commodity { get; set; }
    }

    /// <summary>
    /// UserMargin structure
    /// </summary>
    public struct InstrumentMargin
    {
        public InstrumentMargin(Dictionary<string, dynamic> data)
        {
            try
            {
                Margin = data["margin"];
                COLower = data["co_lower"];
                MISMultiplier = data["mis_multiplier"];
                Tradingsymbol = data["tradingsymbol"];
                COUpper = data["co_upper"];
                NRMLMargin = data["nrml_margin"];
                MISMargin = data["mis_margin"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public string Tradingsymbol { get; set; }
        public decimal Margin { get; set; }
        public decimal COLower { get; set; }
        public decimal COUpper { get; set; }
        public decimal MISMultiplier { get; set; }
        public decimal MISMargin { get; set; }
        public decimal NRMLMargin { get; set; }
    }

    public class PositionSummary
    {
        public int Id { get; set; }
        public int OvernightQuantity { get; set; }
        public decimal SellValue { get; set; }
        public decimal LastPrice { get; set; }
        public  string TradingSymbol { get; set; }
        public decimal Realised { get; set; }
        public decimal TodayPNL { get; set; }
        public decimal PNL { get; set; }
        public decimal Multiplier { get; set; }
        public int SellQuantity { get; set; }
        public decimal BuyValue { get; set; }
        public int BuyQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Unrealised { get; set; }
        public decimal Value { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal BuySLPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellSLPrice { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public decimal ClosePrice { get; set; }
        public int Quantity { get; set; }
        public decimal Target { get; set; }
        public decimal StopLoss { get; set; }
    }
    public class KotakPosition
    {
        dynamic dynamicValue = 0;
        public KotakPosition(Dictionary<string, dynamic> data)
        {
            //{ "buyAmt":"0.00",
            //        "cfSellAmt":"0.00",
            //        "prod":"NRML",
            //        "exSeg":"nse_fo",
            //        "sqrFlg":"Y",
            //        "actId":"ZDOKN",
            //        "cfBuyQty":"400",
            //        "hsUpTm":"2024/07/20 12:28:55",
            //        "cfSellQty":"0",
            //        "tok":"63896",
            //        "upldPrc":"0.00",
            //        "flBuyQty":"0",
            //        "flSellQty":"0",
            //        "sellAmt":"0.00",
            //        "posFlg":"true",
            //        "cfBuyAmt":"813500.00",
            //        "stkPrc":"0.00",
            //        "trdSym":"UBL24JULFUT",
            //        "sym":"UBL","multiplier":"1",
            //        "precision":"2",
            //        "expDt":"25 Jul, 2024",
            //        "type":"FUTSTK",
            //        "genNum":"1",
            //        "series":"XX",
            //        "prcNum":"1",
            //        "genDen":"1",
            //        "brdLtQty":400,
            //        "exp":"1721917800",
            //        "lotSz":"400",
            //        "optTp":"XX",
            //        "prcDen":"1"}

            try
            {
                Product = data.TryGetValue("prod", out dynamicValue) ? dynamicValue : string.Empty;
                //OvernightQuantity = data["overnight_quantity"];
                Exchange = data.TryGetValue("exSeg", out dynamicValue) ? dynamicValue : string.Empty;
                
                ;// data["cfSellAmt"] != string.Empty? Convert.ToDecimal(data["cfSellAmt"]) : 0;
                SellValue = data.TryGetValue("cfSellAmt", out dynamicValue) ? Convert.ToDecimal(dynamicValue) : 0;
                //BuyM2M = data["buy_m2m"];
                //LastPrice = data["last_price"];
                TradingSymbol = data.TryGetValue("trdSym", out dynamicValue) ? dynamicValue : string.Empty;
                //Realised = data["realised"];
                //PNL = data["pnl"];
                ExpiryDate = data.TryGetValue("expDt", out dynamicValue) ? data["expDt"] != "NA" ? Convert.ToDateTime(data["expDt"]) : DateTime.MaxValue : DateTime.MaxValue;
                
                //Multiplier = data["multiplier"];
                SellQuantity = data.TryGetValue("cfSellQty", out dynamicValue) ? Convert.ToInt32(dynamicValue): 0;
                //SellM2M = data["sell_m2m"];
                BuyValue = data["cfBuyAmt"] != string.Empty ? Convert.ToDecimal(data["cfBuyAmt"]) : 0;
                BuyQuantity = data.TryGetValue("cfBuyQty", out dynamicValue) ? Convert.ToInt32(dynamicValue) : 0;
                //AveragePrice = data["average_price"];
                //Unrealised = data["unrealised"];
                //Value = data["value"];
                AveragePrice = BuyQuantity != 0 ? BuyValue / BuyQuantity : SellQuantity != 0 ? SellValue / SellQuantity : 0;
                //SellPrice = data["sell_price"];
                //M2M = data["m2m"];
                InstrumentToken = data.TryGetValue("tok", out dynamicValue) ? Convert.ToUInt32(dynamicValue) : 0;
                //ClosePrice = data["close_price"];
                //Quantity = data["quantity"];
                //DayBuyQuantity = data["day_buy_quantity"];
                //DayBuyValue = data["day_buy_value"];
                //DayBuyPrice = data["day_buy_price"];
                //DaySellQuantity = data["day_sell_quantity"];
                //DaySellValue = data["day_sell_value"];
                //DaySellPrice = data["day_sell_price"];
                //ActiveState = data["active_state"];
            }
            catch (Exception ex)
            {
                //throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public string Product { get; set; }
        //public int OvernightQuantity { get; set; }
        public string Exchange { get; set; }
        public decimal SellValue { get; set; }
      //  public decimal BuyM2M { get; set; }
        //public decimal LastPrice { get; set; }
        public string TradingSymbol { get; set; }
        //public decimal Realised { get; set; }
        //public decimal PNL { get; set; }
        //public decimal Multiplier { get; set; }
        public int SellQuantity { get; set; }
        //public decimal SellM2M { get; set; }
        public decimal BuyValue { get; set; }
        public int BuyQuantity { get; set; }
        public decimal AveragePrice { get; set; }
       // public decimal Unrealised { get; set; }
       // public decimal Value { get; set; }
      //  public decimal BuyPrice { get; set; }
       // public decimal SellPrice { get; set; }
        //public decimal M2M { get; set; }
        public UInt32 InstrumentToken { get; set; }
        //public decimal ClosePrice { get; set; }
        //public int Quantity { get; set; }
        //public int DayBuyQuantity { get; set; }
        //public decimal DayBuyPrice { get; set; }
        //public decimal DayBuyValue { get; set; }
        //public int DaySellQuantity { get; set; }
        //public decimal DaySellPrice { get; set; }
        //public decimal DaySellValue { get; set; }

        public DateTime ExpiryDate { get; set; }


       // public CurrentPostion ActiveState { get; set; }
    }

    /// <summary>
    /// Position structure
    /// </summary>
    public struct Position
    {
        public Position(Dictionary<string, dynamic> data)
        {
            try
            {
                Product = data["product"];
                OvernightQuantity = data["overnight_quantity"];
                Exchange = data["exchange"];
                SellValue = data["sell_value"];
                BuyM2M = data["buy_m2m"];
                LastPrice = data["last_price"];
                TradingSymbol = data["tradingsymbol"];
                Realised = data["realised"];
                PNL = data["pnl"];
                Multiplier = data["multiplier"];
                SellQuantity = data["sell_quantity"];
                SellM2M = data["sell_m2m"];
                BuyValue = data["buy_value"];
                BuyQuantity = data["buy_quantity"];
                AveragePrice = data["average_price"];
                Unrealised = data["unrealised"];
                Value = data["value"];
                BuyPrice = data["buy_price"];
                SellPrice = data["sell_price"];
                M2M = data["m2m"];
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                ClosePrice = data["close_price"];
                Quantity = data["quantity"];
                DayBuyQuantity = data["day_buy_quantity"];
                DayBuyValue = data["day_buy_value"];
                DayBuyPrice = data["day_buy_price"];
                DaySellQuantity = data["day_sell_quantity"];
                DaySellValue = data["day_sell_value"];
                DaySellPrice = data["day_sell_price"];
                ActiveState = data["active_state"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public string Product { get; set; }
        public int OvernightQuantity { get; set; }
        public string Exchange { get; set; }
        public decimal SellValue { get; set; }
        public decimal BuyM2M { get; set; }
        public decimal LastPrice { get; set; }
        public string TradingSymbol { get; set; }
        public decimal Realised { get; set; }
        public decimal PNL { get; set; }
        public decimal Multiplier { get; set; }
        public int SellQuantity { get; set; }
        public decimal SellM2M { get; set; }
        public decimal BuyValue { get; set; }
        public int BuyQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Unrealised { get; set; }
        public decimal Value { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal M2M { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public decimal ClosePrice { get; set; }
        public int Quantity { get; set; }
        public int DayBuyQuantity { get; set; }
        public decimal DayBuyPrice { get; set; }
        public decimal DayBuyValue { get; set; }
        public int DaySellQuantity { get; set; }
        public decimal DaySellPrice { get; set; }
        public decimal DaySellValue { get; set; }

        public CurrentPostion ActiveState { get; set; }
    }

    public class PriceTime
    {
        public decimal LastPrice;
        public DateTime? TradeTime;
    }
    public class OptionChain
    {
        [Key]
        public decimal Strike { get; set; }
        public DateTime Expiry { get; set; }

        public uint BToken { get; set; }

        public Option[] Option { get; set; } //0:CE, 1: PE

        //uint baseInstrument = 256265; //256265; //260105
        //DateTime expiry = Convert.ToDateTime("2020-04-30");
        ////Retrive all options

        //DataLogic dl = new DataLogic();
        //List<Instrument> bInstruments = dl.RetrieveBaseInstruments();
        //lstbxInstruments.DataSource = new BindingSource(bInstruments, null); ;
        //    lstbxInstruments.DisplayMember = "TradingSymbol";
        //    lstbxInstruments.ValueMember = "InstrumentToken";
    }

    [Serializable]
    public class Option : Instrument
    {
        public Option(Instrument instrument, DateTime lastTradeTime, Instrument baseInstrument)
        {
            this.InstrumentToken = instrument.InstrumentToken;
            this.ExchangeToken = instrument.ExchangeToken;
            this.TradingSymbol = instrument.TradingSymbol;
            this.Name = instrument.Name;
            this.LastPrice = instrument.LastPrice;
            this.TickSize = instrument.TickSize;
            this.Expiry = instrument.Expiry;
            this.InstrumentType = instrument.InstrumentType;
            this.Segment = instrument.Segment;
            this.Exchange = instrument.Exchange;
            this.Strike = instrument.Strike;
            this.LotSize = instrument.LotSize;
            this.Bids = instrument.Bids;
            this.Offers = instrument.Offers;
            this.Pain = instrument.Pain;
            this.OI = instrument.OI;
            this.BeginingPeriodOI = instrument.BeginingPeriodOI;
            this.OIDayHigh = instrument.OIDayHigh;
            this.OIDayLow = instrument.OIDayLow;
            this.BaseInstrumentToken = instrument.BaseInstrumentToken;
            this.IsTraded = instrument.IsTraded;
            this.LastTradeTime = lastTradeTime;
            this.BaseInstrument = baseInstrument;
        }
        public Option()
        { }

        public decimal OI { get; set; }
        public decimal DeltaOI { get; set; }
        public decimal? IV { get; set; }
        public new decimal Delta { get; set; }
        public decimal Vega { get; set; }
        public decimal Gamma { get; set; }
        public decimal Theta { get; set; }
        public string Symbol { get; set; }

        public decimal BaseInstrumentPrice { get; set; }
        public Instrument BaseInstrument { get; set; }
        public DateTime? LastTradeTime { get; set; }

        public decimal GetIntrinsicValue(decimal baseInstrumentPrice)
        {
            return ((decimal)(InstrumentType == "Call" ? baseInstrumentPrice - Strike : Strike - baseInstrumentPrice));
        }

        public decimal GetTimeValue(decimal baseInstrumentPrice)
        {
            var intrinsic = GetIntrinsicValue(baseInstrumentPrice);

            if (LastPrice == 0 || intrinsic == 0)
                return 0;

            return (decimal)(LastPrice - intrinsic);
        }

        public DateTime GetExpirationTime()
        {
            var expDate = Expiry.Value;

            if (expDate.TimeOfDay == TimeSpan.Zero)
            {
                TimeSpan closingTime = new TimeSpan(15, 30, 0);
                expDate += closingTime;
            }

            return expDate;
        }
        public Instrument GetUnderlyingAsset()
        {
            return BaseInstrument;
        }


    }

    public class OptionStrategy
    {
        public int Id { get; set; }

        public List<ShortOrder> Orders;

        public uint ParentInstToken { get; set; } = 0;
        public decimal ParentInstPrice { get; set; } = 0;

        public AlgoIndex AlgoIndex { get; set; }
        public decimal LowerThreshold { get; set; } = 0;
        public decimal UpperThreshold { get; set; } = 0;
        public bool ThresholdinPercent { get; set; } = false;
        public int StopLossPoints { get; set; } = 0;

        public int InitialQty { get; set; } = 0;
        public int MaxQty { get; set; } = 0;
        public int StepQty { get; set; } = 0;
        public int StrikePriceIncrement { get; set; } = 0;
    }

    public class StrangleDetails
    {
        public uint peToken { get; set; }
        public uint ceToken { get; set; }
        public string peSymbol { get; set; }
        public string ceSymbol { get; set; }
        public decimal pelowerThreshold { get; set; } = 0;
        public decimal peUpperThreshold { get; set; } = 0;
        public decimal celowerThreshold { get; set; } = 0;
        public decimal ceUpperThreshold { get; set; } = 0;

        public bool ThresholdinPercent { get; set; } = false;

        public double stopLossPoints { get; set; } = 0;
        public int strangleId { get; set; } = 0;
    }


    //public class AlgoPositionGroup
    //{
    //    int GId { get; set; }
    //    public int MaximumQty { get; set; }
    //    public uint InstrumentToken { get; set; }
    //    public List<AlgoPosition> ActivePositions { get; set; }
    //    public string TradingSymbol { get; set; }

    //    public string TradingSymbol { get; set; }
    //}


    public class AlgoPosition
    {
        public AlgoPosition()
        {
        }
        public AlgoPosition(Dictionary<string, dynamic> data)
        {
            try
            {
                OvernightQuantity = data["overnight_quantity"];
                SellValue = data["sell_value"];
                //BuyM2M = data["buy_m2m"];
                LastPrice = data["last_price"];
                TradingSymbol = data["tradingsymbol"];
                Realised = data["realised"];
                PNL = data["pnl"];
                Multiplier = data["multiplier"];
                SellQuantity = data["sell_quantity"];
                //SellM2M = data["sell_m2m"];
                BuyValue = data["buy_value"];
                BuyQuantity = data["buy_quantity"];
                AveragePrice = data["average_price"];
                Unrealised = data["unrealised"];
                Value = data["value"];
                BuyPrice = data["buy_price"];
                SellPrice = data["sell_price"];
                BuySLPrice = data["buy_sl_price"];
                SellSLPrice = data["sell_sl_price"];
                //M2M = data["m2m"];
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                ClosePrice = data["close_price"];
                Quantity = data["quantity"];
                //DayBuyQuantity = data["day_buy_quantity"];
                //DayBuyValue = data["day_buy_value"];
                //DayBuyPrice = data["day_buy_price"];
                //DaySellQuantity = data["day_sell_quantity"];
                //DaySellValue = data["day_sell_value"];
                //DaySellPrice = data["day_sell_price"];
                UpperLimit = data["upper_limit"];
                LowerLimit = data["lower_limit"];
                Delta = data["delta"];
                Expiry = data["expiry"];
                Algo = data["algo"];
                ID = data["id"];

            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }
        //public string Product { get; }
        public Guid ID { get; } = Guid.NewGuid();

        //public string Product { get; }
        public int OvernightQuantity { get; set; }
        //public string Exchange { get;  set;}
        public decimal SellValue { get; set; }
        //public decimal BuyM2M { get;  set;}
        public decimal LastPrice { get; set; }
        public string TradingSymbol { get; set; }
        public decimal Realised { get; set; }
        public decimal PNL { get; set; }
        public decimal Multiplier { get; set; }
        public int SellQuantity { get; set; }
        // public decimal SellM2M { get;  set;}
        public decimal BuyValue { get; set; }
        public int BuyQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Unrealised { get; set; }
        public decimal Value { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal BuySLPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal SellSLPrice { get; set; }

        //public decimal M2M { get;  set;}
        public UInt32 InstrumentToken { get; set; }
        public decimal ClosePrice { get; set; }
        public int Quantity { get; set; }
        //public int DayBuyQuantity { get;  set;}
        //public decimal DayBuyPrice { get;  set;}
        //public decimal DayBuyValue { get;  set;}
        //public int DaySellQuantity { get;  set;}
        //public decimal DaySellPrice { get;  set;}
        //public decimal DaySellValue { get;  set;}

        public int UpperLimit { get; set; }
        public int LowerLimit { get; set; }
        public int Delta { get; set; }
        public int Expiry { get; set; }
        public AlgoIndex Algo { get; set; }

    }

    

    /// <summary>
    /// Position response structure
    /// </summary>
    public struct PositionResponse
    {
        public PositionResponse(Dictionary<string, dynamic> data)
        {
            Day = new List<Position>();
            Net = new List<Position>();

            foreach (Dictionary<string, dynamic> item in data["day"])
                Day.Add(new Position(item));
            foreach (Dictionary<string, dynamic> item in data["net"])
                Net.Add(new Position(item));
        }

        public List<Position> Day { get; }
        public List<Position> Net { get; }
    }

    public struct ShortOrder
    {
        public ShortOrder(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                OrderId = data["order_id"];
                OrderTimestamp = Utils.StringToDate(data["order_timestamp"]);
                OrderType = data["order_type"];
                ParentOrderId = data["parent_order_id"];
                Price = data["price"];
                Product = data["product"];
                Quantity = data["quantity"];
                StrategyID = data["strategyid"];
                Tag = data["tag"];
                Tradingsymbol = data["tradingsymbol"];
                TransactionType = data["transaction_type"];
                TriggerPrice = data["trigger_price"];
                Validity = data["validity"];
                AlgoIndex = data["algoindex"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }
        public UInt32 InstrumentToken { get; set; }
        public string OrderId { get; set; }
        public DateTime? OrderTimestamp { get; set; }
        public string OrderType { get; set; }
        public string ParentOrderId { get; set; }
        public decimal Price { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public int StrategyID { get; set; }
        public string Tag { get; set; }
        public string Tradingsymbol { get; set; }
        public string TransactionType { get; set; }
        public decimal TriggerPrice { get; set; }
        public string Validity { get; set; }
        public AlgoIndex AlgoIndex { get; set; }
    }

    public class ChartData
    {
        public int AlgoId { get; set; } = 0;
        public UInt32 InstrumentToken { get; set; }
        public int AlgoInstance { get; set; }
        public int ChartId { get; set; }
        public int ChartDataId { get; set; }
        public decimal d { get; set; }
        public string xLabel { get; set; }
        public string yLabel { get; set; }
        public string Arg { get; set; }
        public decimal Arg2 { get; set; }
        public DateTime T { get; set; }
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
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
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
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
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
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
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
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
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

    /// <summary>
    /// Order structure
    /// </summary>
    public class Order
    {
        public Order()
        {

        }
        public Order (KotakOrder korder)
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
            TransactionType = korder.TransactionType.ToLower() == "b"? "buy": korder.TransactionType.ToLower() == "s" ? "sell" : korder.TransactionType;
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
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
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

    public class Strangle
    {
        public Instrument Call { get; set; }
        public Instrument Put { get; set; }
    }

    public struct OptionsBox
    {
        public Instrument[] Instruments { get; set; }
        public decimal BoxValue { get; set; }
    }


    //public struct BaseInstrument
    //{
    //    public UInt32 InstrumentToken { get; set; }
    //    public string TradingSymbol { get; set; }
    //    public decimal LastPrice { get; set; }
    //}




    /// <summary>
    /// Instrument structure
    /// </summary>
    public class Instrument
    {
        public Instrument()
        { }
        public Instrument(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                ExchangeToken = Convert.ToUInt32(data["exchange_token"]);
                TradingSymbol = data["tradingsymbol"];
                Name = data["name"];
                LastPrice = Convert.ToDecimal(data["last_price"]);
                TickSize = Convert.ToDecimal(data["tick_size"]);
                Expiry = Utils.StringToDate(data["expiry"]);
                InstrumentType = data["instrument_type"];
                Segment = data["segment"];
                Exchange = data["exchange"];

                if (data["strike"].Contains("e"))
                    Strike = Decimal.Parse(data["strike"], System.Globalization.NumberStyles.Float);
                else
                    Strike = Convert.ToDecimal(data["strike"]);

                LotSize = Convert.ToUInt32(data["lot_size"]);

                Bids = new DepthItem[5];
                Offers = new DepthItem[5];

                if (data.ContainsKey("depth"))
                {
                    if (data["depth"]["buy"] != null)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            Bids[i].Quantity = data["depth"]["buy"][i].Quantity;
                            Bids[i].Price = data["depth"]["buy"][i].Price;
                            Bids[i].Orders = data["depth"]["buy"][i].Orders;
                        }
                        //foreach (Dictionary<string, dynamic> bid in data["depth"]["buy"])
                        //    Bids.Add(new DepthItem(bid));
                    }

                    if (data["depth"]["sell"] != null)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            Offers[i].Quantity = data["depth"]["sell"][i].Quantity;
                            Offers[i].Price = data["depth"]["sell"][i].Price;
                            Offers[i].Orders = data["depth"]["sell"][i].Orders;
                        }
                        //foreach (Dictionary<string, dynamic> offer in data["depth"]["sell"])
                        //    Offers.Add(new DepthItem(offer));
                    }
                }
                if (data.ContainsKey("delta") && data["delta"] != null)
                {
                    Delta = Convert.ToDouble(data["delta"]);
                }
                else
                {
                    Delta = 1.0; //Highest Delta
                }
                if (data.ContainsKey("baseinstrument_token") && data["baseinstrument_token"] != null)
                {
                    BaseInstrumentToken = Convert.ToUInt32(data["baseinstrument_token"]);
                }
                else
                {
                    BaseInstrumentToken = 0; //Highest Delta
                }
                if (data.ContainsKey("oi") && data["oi"] != null && data["oiDayHigh"] != null && data["oiDayLow"] != null)
                {
                    OI = Convert.ToUInt32(data["oi"]);
                    OIDayHigh = Convert.ToUInt32(data["oiDayHigh"]);
                    OIDayLow = Convert.ToUInt32(data["oiDayLow"]);
                }
                else
                {
                    OI = 0;
                    OIDayHigh = 0;
                    OIDayLow = 0;
                }
                if (data.ContainsKey("beginingperiodoi") && data["beginingperiodoi"] != null)
                {
                    BeginingPeriodOI = Convert.ToUInt32(data["beginingperiodoi"]);
                }
                else
                {
                    BeginingPeriodOI = 0;
                }



                if (data.ContainsKey("pain") && data["pain"] != null)
                {
                    Pain = Convert.ToDecimal(data["pain"]);
                }
                else
                {
                    Pain = 0;
                }
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public UInt32 InstrumentToken { get; set; }
        public UInt32 ExchangeToken { get; set; }
        public string TradingSymbol { get; set; }
        public string Name { get; set; }
        public decimal LastPrice { get; set; }
        public decimal TickSize { get; set; }
        public DateTime? Expiry { get; set; }
        public string InstrumentType { get; set; }
        public string Segment { get; set; }
        public string Exchange { get; set; }
        public decimal Strike { get; set; }
        public UInt32 LotSize { get; set; }
        public DepthItem[] Bids { get; set; }
        public DepthItem[] Offers { get; set; }
        public decimal Pain { get; set; }
        public UInt32 OI { get; set; }

        public UInt32 KToken { get; set; }

        public UInt32 BeginingPeriodOI { get; set; }
        public UInt32 OIDayHigh { get; set; }
        public UInt32 OIDayLow { get; set; }

        //Relavant for Derivative instruments. For Base instruments this will be 0
        public UInt32 BaseInstrumentToken { get; set; }
        //Relevant for derivative instruments. For Baseinstrument this will be 1
        /// <summary>
        /// Call Update delta method before accessing this property
        /// TODO: Lazy load to be implemented
        /// </summary>
        public double Delta { get; set; }
        public decimal TradeStrike { get; set; }
        public bool IsTraded { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public decimal TradeEntryPrice { get; set; }
        public decimal TradeExitPrice { get; set; }

        public decimal StopLoss { get; set; }
        public decimal TSL { get; set; }
        public DateTime TradedTime { get; set; }
        //Blackscholes
        //public double UpdateDelta(double S, double r, DateTime? dateoftrade)
        //{
        //    double N, sigma2;
        //    double d1, d2, deltaT, sd;
        //    double phi1, phi2, phi3, phi4;
        //    //double p1, p2;
        //    double X = Convert.ToDouble(Strike);

        //    ///TODO: REMOVE THE BELOW LINE OF DATE OF TRADE.VALUE. THIS IS ONLY FOR TESTING PURPOSE.
        //    //double t = ((DateTime)Expiry - DateTime.Today).TotalDays/365;
        //    double t = ((DateTime)Expiry - dateoftrade.Value).TotalDays / 365;

        //    double sigma = option_price_implied_volatility_call_black_scholes_newton(S, X, r, t, Convert.ToDouble(LastPrice));

        //    sigma2 = sigma * sigma;
        //    deltaT = t;//T - t;
        //    N = 1.0 / Math.Sqrt(2.0 * Math.PI * sigma2);
        //    sd = Math.Sqrt(deltaT);
        //    d1 = (Math.Log(S / X) + (r + 0.5 * sigma2) * deltaT) / (sigma * sd);
        //    d2 = d1 - sigma * sd;
        //    phi1 = CDF(d1);
        //    phi2 = CDF(d2);
        //    phi3 = CDF(-d2);
        //    phi4 = CDF(-d1);

        //    if (InstrumentType.Trim(' ') == "CE")
        //    {
        //        Delta = phi1;
        //    }
        //    else
        //    {
        //        Delta = phi1 - 1;
        //    }
        //    return Math.Abs(Delta);
        //}

        //public double option_price_implied_volatility_call_black_scholes_newton(
        //    double S, double X, double r, double time, double option_price)
        //{
        //    // check for arbitrage violations:
        //    // if price at almost zero volatility greater than price, return 0
        //    double sigma_low = 1e-5;
        //    double c, p;
        //    //EuropeanCall ec = new EuropeanCall(S, X, r * 0.01, sigma_low, 0, time, out c, out p);

        //    DeriveOptionPrice(S, X, r * 0.01, sigma_low, 0, time, out c, out p);

        //    double price = c;
        //    if (price > option_price) return 0.0;

        //    const int MAX_ITERATIONS = 100;
        //    const double ACCURACY = 1.0e-4;
        //    double t_sqrt = Math.Sqrt(time);

        //    double sigma = (option_price / S) / (0.398 * t_sqrt);    // find initial value
        //    for (int i = 0; i < MAX_ITERATIONS; i++)
        //    {
        //        DeriveOptionPrice(S, X, r * 0.01, sigma_low, 0, time, out c, out p);
        //        price = c;
        //        double diff = option_price - price;
        //        if (Math.Abs(diff) < ACCURACY) return sigma;
        //        double d1 = (Math.Log(S / X) + r * time) / (sigma * t_sqrt) + 0.5 * sigma * t_sqrt;
        //        double vega = S * t_sqrt * CDF(d1);
        //        sigma = sigma + diff / vega;
        //    };
        //    return 1;  // something screwy happened, should throw exception
        //}

        //private void DeriveOptionPrice(double S, double X, double r,
        //  double sigma, double t, double T, out double c, out double p)
        //{
        //    // S = underlying asset price (stock price)
        //    // X = exercise price
        //    // r = risk free interst rate
        //    // sigma = standard deviation of underlying asset (stock)
        //    // t = current date
        //    // T = maturity date
        //    //double sigma = option_price_implied_volatility_call_black_scholes_newton(S, X, r, t, option_price);

        //    double N, sigma2;
        //    double d1, d2, deltaT, sd;
        //    double phi1, phi2, phi3, phi4;
        //    double p1, p2;

        //    sigma2 = sigma * sigma;
        //        deltaT = T - t;
        //        N = 1.0 / Math.Sqrt(2.0 * Math.PI * sigma2);
        //        sd = Math.Sqrt(deltaT);
        //        d1 = (Math.Log(S / X) + (r + 0.5 * sigma2) * deltaT) / (sigma * sd);
        //        d2 = d1 - sigma * sd;
        //        phi1 = CDF(d1);
        //        phi2 = CDF(d2);
        //        phi3 = CDF(-d2);
        //        phi4 = CDF(-d1);
        //        c = S * phi1 - X * Math.Exp(-r * deltaT) * phi2;
        //        p = X * Math.Exp(-r * deltaT) * phi3 - S * phi4;
        //}
        //private double CDF(double x)
        //{
        //    if (x > 99)
        //        return 1;
        //    if (x < -99)
        //        return 0;

        //    double sum = x, val = x;

        //    for (int i = 1; i <= 100; i++)
        //    {
        //        val *= x * x / (2.0 * i + 1.0);
        //        sum += val;
        //    }

        //    return 0.5 + (sum / Math.Sqrt(2.0 * Math.PI)) * Math.Exp(-(x * x) / 2.0);
        //}


        public double UpdateDelta(double S, double r, DateTime? dateoftrade, double baseInstrumentPrice)
        {
            ///TODO: REMOVE THE BELOW LINE OF DATE OF TRADE.VALUE. THIS IS ONLY FOR TESTING PURPOSE.
            //  double t = ((DateTime)Expiry - DateTime.Today).TotalDays/365;
            double t = ((DateTime)Expiry - dateoftrade.Value).TotalDays / 365;
            double x = Convert.ToDouble(Strike);
            PutCallFlag putCallFlag = InstrumentType.Trim(' ') == "CE" ? PutCallFlag.Call : PutCallFlag.Put;
            double iv = BlackScholesImpliedVol(S, x, baseInstrumentPrice, t, r, 0, putCallFlag);
            iv = double.IsNegativeInfinity(iv) ? 0 : iv;
            Delta = BlackScholesDelta(x, baseInstrumentPrice, t, iv, r, 0, putCallFlag);
            return Delta;
        }
        public static double BlackScholesDelta(double strike, double underlyingPrice, double yearsToExpiry, double vol, double riskFreeRate, double dividendYield, PutCallFlag putCallFlag)
        {
            double sqrtT = Math.Sqrt(yearsToExpiry);
            double d1 = (Math.Log(underlyingPrice / strike) + (riskFreeRate - dividendYield + 0.5 * vol * vol) * yearsToExpiry) / (vol * sqrtT);
            double N1 = StandardNormalCumulativeDistributionFunction(d1);
            if (putCallFlag == PutCallFlag.Call)
                return Math.Exp(-dividendYield * yearsToExpiry) * N1;
            return Math.Exp(-dividendYield * yearsToExpiry) * (N1 - 1.0);
        }

        public static double StandardNormalCumulativeDistributionFunction(double x)
        {
            //Approimation based on Abramowitz & Stegun (1964)

            if (x < 0)
                return 1.0 - StandardNormalCumulativeDistributionFunction(-x);
            const double b0 = 0.2316419;
            const double b1 = 0.319381530;
            const double b2 = -0.356563782;
            const double b3 = 1.781477937;
            const double b4 = -1.821255978;
            const double b5 = 1.330274429;
            double pdf = StandardNormalProbabilityDensityFunction(x);
            double a = 1.0 / (1.0 + b0 * x);
            return 1.0 - pdf * (b1 * a + b2 * Math.Pow(a, 2) + b3 * Math.Pow(a, 3) + b4 * Math.Pow(a, 4) + b5 * Math.Pow(a, 5));
        }
        /// <summary>
        /// Returns the PDF of the standard normal distribution.
        /// </summary>
        /// <param name="x">Value at which the distribution is evaluated.</param>
        public static double StandardNormalProbabilityDensityFunction(double x)
        {
            const double SqrtTwoPiInv = 0.398942280401433;
            return SqrtTwoPiInv * Math.Exp(-0.5 * x * x);
        }

        public static double BlackScholesImpliedVol(double price, double strike, double underlyingPrice, double yearsToExpiry, double riskFreeRate, double dividendYield, PutCallFlag putCallFlag)
        {
            const double tolerance = 0.0001;
            const int maxLoops = 100;

            double vol = Math.Sqrt(2 * Math.Abs(Math.Log(underlyingPrice / strike) / yearsToExpiry + riskFreeRate));    //Manaster and Koehler intial vol value
            vol = Math.Max(0.01, vol);
            double vega;
            double impliedPrice = BlackScholesPriceAndVega(strike, underlyingPrice, yearsToExpiry, vol, riskFreeRate, dividendYield, putCallFlag, out vega);

            int nLoops = 0;
            while (Math.Abs(impliedPrice - price) > tolerance)
            {
                if (double.IsNaN(vega) || vega == 0 || double.IsNegativeInfinity(vega))
                {
                    return 0;
                }
                if (double.IsNaN(vol) || vol == 0 || double.IsNegativeInfinity(vol))
                {
                    return 0;
                }
                nLoops++;
                if (nLoops > maxLoops)
                    throw new Exception("BlackScholesImpliedVol did not converge.");

                vol = vol - (impliedPrice - price) / vega;
                if (vol <= 0)
                    vol = 0.5 * (vol + (impliedPrice - price) / vega); //half way btwn previous estimate and zero




                impliedPrice = BlackScholesPriceAndVega(strike, underlyingPrice, yearsToExpiry, vol, riskFreeRate, dividendYield, putCallFlag, out vega);
            }

           
            return vol;
        }

        private static double BlackScholesPriceAndVega(double strike, double underlyingPrice, double yearsToExpiry, double vol, double riskFreeRate, double dividendYield, PutCallFlag putCallFlag, out double vega)
        {
            double sqrtT = Math.Sqrt(yearsToExpiry);
            double d1 = (Math.Log(underlyingPrice / strike) + (riskFreeRate - dividendYield + 0.5 * vol * vol) * yearsToExpiry) / (vol * sqrtT);
            double d2 = d1 - vol * sqrtT;
            if (putCallFlag == PutCallFlag.Call)
            {
                double N1 = StandardNormalCumulativeDistributionFunction(d1);
                double N2 = StandardNormalCumulativeDistributionFunction(d2);
                double nn1 = StandardNormalProbabilityDensityFunction(d1);

                vega = underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry) * nn1 * sqrtT;
                return N1 * underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry) - N2 * strike * Math.Exp(-riskFreeRate * yearsToExpiry);
            }
            double Nn1 = StandardNormalCumulativeDistributionFunction(-d1);
            double Nn2 = StandardNormalCumulativeDistributionFunction(-d2);
            double n1 = StandardNormalProbabilityDensityFunction(d1);

            vega = underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry) * n1 * sqrtT;
            return Nn2 * strike * Math.Exp(-riskFreeRate * yearsToExpiry) - Nn1 * underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry);
        }

    }

    public class InstrumentListNode
    {
        public InstrumentListNode(Instrument instrument)
        {
            Instrument = instrument;
            PrevNode = null;
            NextNode = null;
            Prices = new List<decimal>();
            CurrentPosition = PositionStatus.Closed;
        }

        public Instrument Instrument { get; set; }

        //The reason for list is the the same instrument can come multiple times during the life of the stangle
        //The signature of the prices is w.r.t cash in  or out. So sell prices are positive and buy prices are negative
        public List<decimal> Prices { get; set; }

        //this is used to link the node with linked list.
        public int Index { get; set; }

        public PositionStatus CurrentPosition { get; set; }

        //public List<decimal> SellPrice { get; set; }
        public InstrumentListNode PrevNode { get; set; }
        public InstrumentListNode NextNode { get; set; }

        public bool LastNode { get; set; }
        public bool FirstNode { get; set; }
        public void AttachNode(InstrumentListNode nodeToBeAttached)
        {
            InstrumentListNode node;
            bool nodeAttached = false;
            if (nodeToBeAttached.Index > this.Index)
            {
                node = this.NextNode;
                if (node == null)
                {
                    this.NextNode = nodeToBeAttached;
                    nodeToBeAttached.PrevNode = this;
                    nodeAttached = true;
                }
                else
                {
                    while (node.NextNode != null)
                    {
                        node = node.NextNode;
                        if (node.Index > nodeToBeAttached.Index)
                        {
                            //this.NextNode = nodeToBeAttached;
                            //nodeToBeAttached.PrevNode = this;

                            nodeToBeAttached.NextNode = node;
                            node.PrevNode = nodeToBeAttached;

                            nodeAttached = true;
                            break;
                        }

                    }
                }
                if (!nodeAttached)
                {
                    node.NextNode = nodeToBeAttached;
                    nodeToBeAttached.PrevNode = node;
                    nodeAttached = true;
                }
            }
            else if (nodeToBeAttached.Index < this.Index)
            {
                node = this.PrevNode;
                if (node == null)
                {
                    this.PrevNode = nodeToBeAttached;
                    nodeToBeAttached.NextNode = this;
                    nodeAttached = true;
                }
                else
                {
                    while (node.PrevNode != null)
                    {
                        node = node.PrevNode;
                        if (node.Index < nodeToBeAttached.Index)
                        {
                            node.PrevNode = nodeToBeAttached;
                            nodeToBeAttached.NextNode = node;

                            //nodeToBeAttached.PrevNode = node;
                            //node.PrevNode = nodeToBeAttached;

                            nodeAttached = true;
                            break;
                        }

                    }
                }
                if (!nodeAttached)
                {
                    node.PrevNode = nodeToBeAttached;
                    nodeToBeAttached.NextNode = node;
                    nodeAttached = true;
                }
            }
        }
        public InstrumentListNode GetNodebyIndex(Int16 nodeIndex)
        {
            InstrumentListNode node = this;
            if (nodeIndex > this.Index)
            {
                while (node.NextNode != null)
                {
                    node = node.NextNode;
                    if (node.Index == nodeIndex)
                    {
                        break;
                    }
                }
            }
            else if (nodeIndex < this.Index)
            {
                while (node.PrevNode != null)
                {
                    node = node.PrevNode;
                    if (node.Index == nodeIndex)
                    {
                        break;
                    }
                }
            }
            return node;
        }
    }


    public class StrangleNode
    {
        public StrangleNode(Instrument call, Instrument put)
        {
            Call = call;
            Put = put;
            CallOrders = new List<Order>();
            PutOrders = new List<Order>();
            CurrentPosition = PositionStatus.Open;
            NetPnL = 0;
        }
        public PositionStatus CurrentPosition { get; set; }
        public Instrument Call { get; set; }
        public Instrument Put { get; set; }
        public decimal BaseInstrumentPrice { get; set; }
        public uint BaseInstrumentToken { get; set; }

        public List<Order> CallOrders { get; set; }
        public List<Order> PutOrders { get; set; }
        public int ID { get; set; }
        public decimal Threshold { get; set; }
        public int MaxQty { get; set; }
        public int CallTradedQty { get; set; }
        public int PutTradedQty { get; set; }
        public int StepQty { get; set; }
        public int InitialQty { get; set; }
        public decimal NetPnL { get; set; }

        public int CallInitialQty { get; set; }
        public int PutInitialQty { get; set; }
    }
    public class StrangleListNode
    {
        public StrangleListNode(Instrument call, Instrument put)
        {
            Call = call;
            Put = put;
            PrevNode = null;
            NextNode = null;
            Prices = new List<decimal[]>();
            CurrentPosition = PositionStatus.Closed;
        }
        //public List<decimal> SellPrice { get; set; }
        public StrangleListNode PrevNode { get; set; }
        public StrangleListNode NextNode { get; set; }
        public PositionStatus CurrentPosition { get; set; }
        public Instrument Call { get; set; }
        public Instrument Put { get; set; }
        public List<decimal[]> Prices { get; set; }
        public int Index { get; set; }
        public double DeltaThreshold { get; set; }
        public StrangleListNode GetNodebyIndex(Int16 nodeIndex)
        {
            StrangleListNode node = this;
            if (nodeIndex > this.Index)
            {
                while (node.NextNode != null)
                {
                    node = node.NextNode;
                    if (node.Index == nodeIndex)
                    {
                        break;
                    }
                }
            }
            else if (nodeIndex < this.Index)
            {
                while (node.PrevNode != null)
                {
                    node = node.PrevNode;
                    if (node.Index == nodeIndex)
                    {
                        break;
                    }
                }
            }
            return node;
        }

        public void AttachNode(StrangleListNode nodeToBeAttached)
        {
            StrangleListNode node;
            bool nodeAttached = false;
            if (nodeToBeAttached.Index > this.Index)
            {
                node = this.NextNode;
                if (node == null)
                {
                    this.NextNode = nodeToBeAttached;
                    nodeToBeAttached.PrevNode = this;
                    nodeAttached = true;
                }
                else
                {
                    while (node.NextNode != null)
                    {
                        node = node.NextNode;
                        if (node.Index > nodeToBeAttached.Index)
                        {
                            //this.NextNode = nodeToBeAttached;
                            //nodeToBeAttached.PrevNode = this;

                            nodeToBeAttached.NextNode = node;
                            node.PrevNode = nodeToBeAttached;

                            nodeAttached = true;
                            break;
                        }

                    }
                }
                if (!nodeAttached)
                {
                    node.NextNode = nodeToBeAttached;
                    nodeToBeAttached.PrevNode = node;
                    nodeAttached = true;
                }
            }
            else if (nodeToBeAttached.Index < this.Index)
            {
                node = this.PrevNode;
                if (node == null)
                {
                    this.PrevNode = nodeToBeAttached;
                    nodeToBeAttached.NextNode = this;
                    nodeAttached = true;
                }
                else
                {
                    while (node.PrevNode != null)
                    {
                        node = node.PrevNode;
                        if (node.Index < nodeToBeAttached.Index)
                        {
                            node.PrevNode = nodeToBeAttached;
                            nodeToBeAttached.NextNode = node;

                            //nodeToBeAttached.PrevNode = node;
                            //node.PrevNode = nodeToBeAttached;

                            nodeAttached = true;
                            break;
                        }

                    }
                }
                if (!nodeAttached)
                {
                    node.PrevNode = nodeToBeAttached;
                    nodeToBeAttached.NextNode = node;
                    nodeAttached = true;
                }
            }
        }
    }
    public class StrangleChain
    {
        public InstrumentListNode CallNode { get; set; }
        public InstrumentListNode PutNode { get; set; }
    }
    public class StrangleInstrumentListNode
    {
        public StrangleInstrumentListNode(InstrumentListNode callNode, InstrumentListNode putNode)
        {
            CallNode = callNode;
            PutNode = putNode;
            PrevNode = null;
            NextNode = null;
            Prices = new List<decimal[]>();
            CurrentPosition = PositionStatus.Closed;
        }
        //public List<decimal> SellPrice { get; set; }
        public StrangleInstrumentListNode PrevNode { get; set; }
        public StrangleInstrumentListNode NextNode { get; set; }
        public PositionStatus CurrentPosition { get; set; }
        public InstrumentListNode CallNode { get; set; }
        public InstrumentListNode PutNode { get; set; }
        public List<decimal[]> Prices { get; set; }
        public int Index { get; set; }
        public double DeltaThreshold { get; set; }
        public decimal BaseInstrumentPrice { get; set; }
        public uint BaseInstrumentToken { get; set; }
        public decimal NetPrice { get; set; }
        public int ListID { get; set; }

        public StrangleInstrumentListNode GetNodebyIndex(Int16 nodeIndex)
        {
            StrangleInstrumentListNode node = this;
            if (nodeIndex > this.Index)
            {
                while (node.NextNode != null)
                {
                    node = node.NextNode;
                    if (node.Index == nodeIndex)
                    {
                        break;
                    }
                }
            }
            else if (nodeIndex < this.Index)
            {
                while (node.PrevNode != null)
                {
                    node = node.PrevNode;
                    if (node.Index == nodeIndex)
                    {
                        break;
                    }
                }
            }
            return node;
        }

        public void AttachNode(StrangleInstrumentListNode nodeToBeAttached)
        {
            StrangleInstrumentListNode node;
            bool nodeAttached = false;
            if (nodeToBeAttached.Index > this.Index)
            {
                node = this.NextNode;
                if (node == null)
                {
                    this.NextNode = nodeToBeAttached;
                    nodeToBeAttached.PrevNode = this;
                    nodeAttached = true;
                }
                else
                {
                    while (node.NextNode != null)
                    {
                        node = node.NextNode;
                        if (node.Index > nodeToBeAttached.Index)
                        {
                            //this.NextNode = nodeToBeAttached;
                            //nodeToBeAttached.PrevNode = this;

                            nodeToBeAttached.NextNode = node;
                            node.PrevNode = nodeToBeAttached;

                            nodeAttached = true;
                            break;
                        }

                    }
                }
                if (!nodeAttached)
                {
                    node.NextNode = nodeToBeAttached;
                    nodeToBeAttached.PrevNode = node;
                    nodeAttached = true;
                }
            }
            else if (nodeToBeAttached.Index < this.Index)
            {
                node = this.PrevNode;
                if (node == null)
                {
                    this.PrevNode = nodeToBeAttached;
                    nodeToBeAttached.NextNode = this;
                    nodeAttached = true;
                }
                else
                {
                    while (node.PrevNode != null)
                    {
                        node = node.PrevNode;
                        if (node.Index < nodeToBeAttached.Index)
                        {
                            node.PrevNode = nodeToBeAttached;
                            nodeToBeAttached.NextNode = node;

                            //nodeToBeAttached.PrevNode = node;
                            //node.PrevNode = nodeToBeAttached;

                            nodeAttached = true;
                            break;
                        }

                    }
                }
                if (!nodeAttached)
                {
                    node.PrevNode = nodeToBeAttached;
                    nodeToBeAttached.NextNode = node;
                    nodeAttached = true;
                }
            }
        }
    }
    public class StraddleNode
    {
        public decimal Strike { get; set; }
        public Instrument Call { get; set; }
        public Instrument Put { get; set; }
        public int isCallLower { get; set; } = 0; // 1 means yes, -1 means no, 0 means NA
        public bool Crossed { get; set; } = false;
        public DateTime? CrossTime { get; set; }
        public StraddleNode PrevNode { get; set; }
        public StraddleNode NextNode { get; set; }
    }
    public class StraddleLinkedList
    {
        public StraddleNode First;
        public StraddleNode Current;

        public StraddleLinkedList(StraddleNode straddleNode)
        {
            //Current = straddleNode;
            First = straddleNode;
        }
        public void InsertNode(StraddleNode straddleNode)
        {
            StraddleNode tempNode = First;
            StraddleNode dummyNode;

            if (straddleNode.Strike < First.Strike)
            {
                straddleNode.NextNode = First;
                First.PrevNode = straddleNode;
                First = straddleNode;
            }
            else
            {
                while (tempNode.NextNode != null)
                {
                    if (straddleNode.Strike > tempNode.Strike && straddleNode.Strike < tempNode.NextNode.Strike)
                    {
                        dummyNode = tempNode.NextNode;
                        tempNode.NextNode = straddleNode;
                        straddleNode.PrevNode = tempNode;

                        straddleNode.NextNode = dummyNode;
                        dummyNode.PrevNode = straddleNode;
                        break;
                    }
                    tempNode = tempNode.NextNode;
                }

                if(tempNode.NextNode == null && straddleNode.Strike > tempNode.Strike)
                {
                    tempNode.NextNode = straddleNode;
                    straddleNode.PrevNode = tempNode;
                }
            }
        }
        public StraddleNode FindImmediateNode(decimal strike, bool lowerNode)
        {
            StraddleNode tempNode = First;
            if (lowerNode)
            {
                while (tempNode != null)
                {
                    if (tempNode.Strike < strike && (tempNode.NextNode == null || tempNode.NextNode.Strike > strike))
                    {
                        return tempNode;
                    }
                    tempNode = tempNode.NextNode;
                }
            }
            else
            {
                while (tempNode != null)
                {
                    if (tempNode.Strike > strike && (tempNode.PrevNode == null || tempNode.PrevNode.Strike < strike))
                    {
                        return tempNode;
                    }
                    tempNode = tempNode.NextNode;
                }
            }
            return null;
        }
    }
    public class StrangleInstrumentLinkedList
    {
        public StrangleInstrumentListNode Current { get; set; }
        public StrangleInstrumentLinkedList(StrangleInstrumentListNode instrument)
        {
            Current = instrument;
        }

        public int listID;

        public decimal MaxLossPoints { get; set; }
        public decimal MaxProfitPoints { get; set; }
        public double LowerDelta { get; set; }
        public double UpperDelta { get; set; }
        public double StopLossPoints { get; set; }

        public int CurrentInstrumentIndex { get; set; }

        public decimal BaseInstrumentPrice { get; set; }

        public uint BaseInstrumentToken { get; set; }

        public decimal NetPrice { get; set; }

        public Instrument BaseInstrument { get; set; }
    }
    public class StrangleLinkedList
    {
        public StrangleListNode Current { get; set; }

        public StrangleLinkedList(StrangleListNode instrument)
        {
            Current = instrument;
        }

        public int listID;

        public decimal MaxLossPoints { get; set; }
        public decimal MaxProfitPoints { get; set; }
        public double LowerDelta { get; set; }
        public double UpperDelta { get; set; }
        public double StopLossPoints { get; set; }

        public int CurrentInstrumentIndex { get; set; }

        public decimal BaseInstrumentPrice { get; set; }

        public uint BaseInstrumentToken { get; set; }

        public decimal NetPrice { get; set; }

        public Instrument BaseInstrument { get; set; }
    }

    public class InstrumentLinkedList
    {
        public InstrumentLinkedList(InstrumentListNode instrument)
        {
            Current = instrument;
        }
        public int listID;

        public decimal MaxLossPoints { get; set; }
        public decimal MaxProfitPoints { get; set; }
        public decimal MaxLossPercent { get; set; }
        public decimal MaxProfitPercent { get; set; }
        public double LowerDelta { get; set; }
        public double UpperDelta { get; set; }
        public double StopLossPoints { get; set; }

        public int CurrentInstrumentIndex { get; set; }
        public InstrumentListNode Current { get; set; }

        public decimal BaseInstrumentPrice { get; set; }

        public uint BaseInstrumentToken { get; set; }

        public decimal NetPrice { get; set; }

        public string InstrumentType { get; set; }

    }

    public class OptionInstrument
    {
        public Instrument Instrument { get; set; }
        public List<decimal> Prices { get; set; }
        public int Index { get; set; }
    }
    public class SortedOptionSet : SortedSet<OptionInstrument>
    {
        public SortedOptionSet(OptionInstrument instrument)
        {
            base.Add(instrument);
        }
        public int listID;

        public decimal MaxLossPoints { get; set; }
        public decimal MaxProfitPoints { get; set; }
        public double LowerDelta { get; set; }
        public double UpperDelta { get; set; }
        public decimal StopLossPoints { get; set; }

        public int CurrentInstrumentIndex { get; set; }
        public InstrumentListNode Current { get; set; }

        public decimal NetPrice { get; set; }
    }

    public class StrangleDataList
    {
        public StrangleDataList()
        {
            TradedCalls = new List<TradedInstrument>();
            TradedPuts = new List<TradedInstrument>();
            CallUniverse = new SortedList<decimal, Instrument>();
            PutUniverse = new SortedList<decimal, Instrument>();
            UnBookedPnL = 0;
            NetCallQtyInTrade = 0;
            NetPutQtyInTrade = 0;
        }
        public int ID { get; set; }
        public SortedList<Decimal, Instrument> CallUniverse { get; set; }
        public SortedList<Decimal, Instrument> PutUniverse { get; set; }
        public List<TradedInstrument> TradedCalls { get; set; } //here the order is based on trading sequence
        public List<TradedInstrument> TradedPuts { get; set; } //here the order is based on trading sequence
        public decimal BaseInstrumentPrice { get; set; }
        public uint BaseInstrumentToken { get; set; }
        public decimal UnBookedPnL { get; set; }
        public int NetCallQtyInTrade { get; set; }
        public int NetPutQtyInTrade { get; set; }
        public decimal BookedPnL { get; set; }

        /// <summary>
        /// Value of total strangle; pricexquantity at each trade
        /// </summary>
        public decimal StrangleValue { get; set; }
        public decimal MaxPainStrike { get; set; }
        public DateTime? Expiry { get; set; }
        public decimal MaxLossThreshold { get; set; }
        public decimal ProfitTarget { get; set; }
        public int TradingQuantity { get; set; }

        [DefaultValue(0)]
        public int StrikePriceIncrement { get; set; }

        public decimal Threshold { get; set; }
        public int MaxQty { get; set; }
        public int StepQty { get; set; }
        public int InitialQty { get; set; }
    }
    public class StrangleDataStructure
    {
        public StrangleDataStructure()
        {
            //TradedCalls = new List<TradedInstrument>();
            //TradedPuts = new List<TradedInstrument>();
            TradedStrangle = new TradedStrangle();
            CallUniverse = new SortedList<decimal, Instrument>();
            PutUniverse = new SortedList<decimal, Instrument>();
            UnBookedPnL = 0;
            NetCallQtyInTrade = 0;
            NetPutQtyInTrade = 0;
            //callMatix = new Decimal[10, 100];
            //putMatix = new Decimal[10, 100];
            OptionMatrix = new Decimal[2][,];
        }
        public int ID { get; set; }
        public SortedList<Decimal, Instrument> CallUniverse { get; set; }
        public SortedList<Decimal, Instrument> PutUniverse { get; set; }

        ///TODO: traded strangle should be 1 with as many instruments as one want.
        ///Trades will determine the combination of entry
        public TradedStrangle TradedStrangle { get; set; } //here the order is based on trading sequence
        //public List<TradedStrangle> TradedStrangles { get; set; } //here the order is based on trading sequence
        //public List<TradedInstrument> TradedCalls { get; set; } //here the order is based on trading sequence
        //public List<TradedInstrument> TradedPuts { get; set; } //here the order is based on trading sequence
        public decimal BaseInstrumentPrice { get; set; }
        public uint BaseInstrumentToken { get; set; }
        public decimal UnBookedPnL { get; set; }
        public int NetCallQtyInTrade { get; set; }
        public int NetPutQtyInTrade { get; set; }
        public decimal BookedPnL { get; set; }

        /// <summary>
        /// Value of total strangle; pricexquantity at each trade
        /// </summary>
        public decimal StrangleValue { get; set; }
        public decimal MaxPainStrike { get; set; }
        public DateTime? Expiry { get; set; }
        public decimal MaxLossThreshold { get; set; }
        public decimal ProfitTarget { get; set; }
        public int TradingQuantity { get; set; }

        [DefaultValue(0)]
        public int StrikePriceIncrement { get; set; }
        public decimal Threshold { get; set; }
        public int MaxQty { get; set; }
        public int StepQty { get; set; }
        public int InitialQty { get; set; }

        public int MinDistanceFromBInstrument { get; set; }

        public double MaxDelta { get; set; }

        public double InitialDelta { get; set; }

        public decimal MinPremiumToTrade { get; set; }

        //public Decimal[,] callMatix { get; set; }
        //public Decimal[,] putMatix { get; set; }

        public Decimal[][,] OptionMatrix { get; set; }

        public void AddMatrixRow(Decimal[,] data, InstrumentType callPutIndex)
        {
            Decimal[,] tempMatrix = OptionMatrix[(int)callPutIndex];

            Decimal[,] newMatrix = new decimal[tempMatrix.GetLength(0) + 1, tempMatrix.GetLength(1)];

            Array.Copy(tempMatrix, newMatrix, tempMatrix.Length - 1);
            Array.Copy(data, 0, newMatrix, tempMatrix.Length, data.Length - 1);
            OptionMatrix[(int)callPutIndex] = newMatrix;
        }
    }


    /// <summary>
    /// Depicts each strangle position entered. This could involve mutiple calls or puts
    /// Trades are listed in Buy/Sell Trades
    /// </summary>
    public class TradedStrangle
    {
        public TradedStrangle()
        {
            //Call = new List<Instrument>();
            //Put = new List<Instrument>();
            Options = new List<Instrument>();
            BuyTrades = new List<ShortTrade>();
            SellTrades = new List<ShortTrade>();
            TradingStatus = PositionStatus.NotTraded;
            BookedPnL = 0;
            UnbookedPnl = 0;
        }

        public List<Instrument> Options { get; set; }
        //public List<Instrument> Put { get; set; }

        [DefaultValue(0)]
        public decimal BookedPnL { get; set; }

        [DefaultValue(0)]
        public decimal UnbookedPnl { get; set; }

        /// <summary>
        /// Trading status (tradedid, position status)
        /// </summary>
        [DefaultValue(PositionStatus.NotTraded)]
        public PositionStatus TradingStatus { get; set; }
        /// <summary>
        /// This could be used for total strangle value at the time of trade
        /// </summary>
        public decimal Attribute { get; set; }
        /// <summary>
        /// trades with triggerid. triggerid will determine all trades that happend together. 
        /// And the status of the trade comes from Trading status
        /// </summary>
        public List<ShortTrade> BuyTrades { get; set; }
        public List<ShortTrade> SellTrades { get; set; }
    }
    public class TradedInstrument
    {
        public TradedInstrument()
        {
            BuyTrades = new List<ShortTrade>();
            SellTrades = new List<ShortTrade>();
            BookedPnL = 0;
            UnbookedPnl = 0;
        }
        public Instrument Option { get; set; }
        public List<ShortTrade> BuyTrades { get; set; }
        public List<ShortTrade> SellTrades { get; set; }
        [DefaultValue(0)]
        public decimal BookedPnL { get; set; }

        [DefaultValue(0)]
        public decimal UnbookedPnl { get; set; }

        [DefaultValue(PositionStatus.NotTraded)]
        public PositionStatus TradingStatus { get; set; }
        /// <summary>
        /// This could be used for total strangle value at the time of trade
        /// </summary>
        public decimal Attribute { get; set; }
    }
    public class StrangleTrade
    {
        public List<ShortTrade> OpenTrade { get; set; }
        public List<ShortTrade> CloseTrade { get; set; }
    }
    public class StrangleData
    {
        public int ID { get; set; }
        public SortedList<Decimal, Instrument> Calls { get; set; }
        public SortedList<Decimal, Instrument> Puts { get; set; }
        public TradedOption CurrentCall { get; set; }
        public TradedOption CurrentPut { get; set; }
        public decimal BaseInstrumentPrice { get; set; }
        public uint BaseInstrumentToken { get; set; }
        public decimal NetPnL { get; set; }
        public decimal MaxPainStrike { get; set; }
        public DateTime? Expiry { get; set; }
        public decimal MaxLossThreshold { get; set; }
        public decimal ProfitTarget { get; set; }
        public int TradingQuantity { get; set; }
        public int StrikePriceIncrement { get; set; }
    }
    public class TradedOption
    {
        public Instrument Option { get; set; }
        public ShortTrade BuyTrade { get; set; }
        public ShortTrade SellTrade { get; set; }

        [DefaultValue(PositionStatus.NotTraded)]
        public PositionStatus TradingStatus { get; set; }
    }

    public class CriticalLevels
    {
        public Candle CurrentCandle { get; set; }
        public Candle PreviousCandle { get; set; }
        public decimal LocalMaxPrice { get; set; }
        public decimal LocalMinPrice { get; set; }
        public decimal TradedPrice { get; set; }
        public decimal TargetPrice { get; set; }
        public decimal StopLossPrice { get; set; }
    }

    public class TradeLevels
    {
        public ShortTrade Trade { get; set; }
        public CriticalLevels Levels { get; set; }
        public ShortTrade SLTrade { get; set; }
    }
    public class OrderLevels
    {
        public Order FirstLegOrder { get; set; }
        public CriticalLevels Levels { get; set; }
        public Order SLOrder { get; set; }
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

    /// <summary>
    /// Every order may have refernce to original order, and an SL order.
    /// The SL order could be shared.
    /// When an order is placed, its SL order could also be place and target order (sharing reference order) could be placed.
    /// When target is executed, the SL order have to be cancelled.
    /// </summary>
    public class OrderLinkedList
    {
        public Instrument Option { get; set; }
        public OrderLinkedListNode FirstOrderNode { get; set; }
    }
    public class OrderLinkedListNode
    {
        public bool FirstLegCompleted { get; set; } = false;
        public Order Order { get; set; }
        public Order SLOrder { get; set; }
        public OrderLinkedListNode PrevOrderNode { get; set; }
        public OrderLinkedListNode NextOrderNode { get; set; }
    }
    public class StrangleOrderLinkedList
    {
        public OrderLinkedList CallOrderLinkedList { get; set; }
        public OrderLinkedList PutOrderLinkedList { get; set; }
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

        public bool? SLFlag { get; set; } = null;

        public bool isActive { get; set; } = true;//0: Inactive; 1 : Active

        public DateTime? IntialSlHitTime { get; set; } = null;

        public Dictionary<int, bool> IndicatorsValue {get; set;} = null;

        public int flag;
    }

    /// <summary>
    /// ShortTrade structure
    /// </summary>
    public struct ShortTrade
    {
        public string OrderId { get; set; }
        public string TransactionType { get; set; }
        public decimal AveragePrice { get; set; }
        public int Quantity { get; set; }
        public DateTime? ExchangeTimestamp { get; set; }
        
        //This can also act as group ID. Triggered together
        public int TriggerID { get; set; }

        public TradeStatus TradingStatus { get; set; }

        public string InstrumentType { get; set; }
        public uint InstrumentToken { get; set; }

        public string TradingSymbol { get; set; }
        public DateTime TradeTime { get; set; }
    }

    /// <summary>
    /// Trade structure
    /// </summary>
    public struct Trade
    {
        public Trade(Dictionary<string, dynamic> data)
        {
            try
            {
                TradeId = data["trade_id"];
                OrderId = data["order_id"];
                ExchangeOrderId = data["exchange_order_id"];
                Tradingsymbol = data["tradingsymbol"];
                Exchange = data["exchange"];
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                TransactionType = data["transaction_type"];
                Product = data["product"];
                AveragePrice = data["average_price"];
                Quantity = data["quantity"];
                FillTimestamp =  Utils.StringToDate(data["fill_timestamp"]);
                ExchangeTimestamp =  Utils.StringToDate(data["exchange_timestamp"]);
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public string TradeId { get; }
        public string OrderId { get; }
        public string ExchangeOrderId { get; }
        public string Tradingsymbol { get; }
        public string Exchange { get; }
        public UInt32 InstrumentToken { get; }
        public string TransactionType { get; }
        public string Product { get; }
        public decimal AveragePrice { get; }
        public int Quantity { get; }
        public DateTime? FillTimestamp { get; }
        public DateTime? ExchangeTimestamp { get; }
    }

    /// <summary>
    /// Trigger range structure
    /// </summary>
    public struct TrigerRange
    {
        public TrigerRange(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                Lower = data["lower"];
                Upper = data["upper"];
                Percentage = data["percentage"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }
        public UInt32 InstrumentToken { get; }
        public decimal Lower { get; }
        public decimal Upper { get; }
        public decimal Percentage { get; }
    }

    public class AspNetUser
    {
        public AspNetUser(DataTable data)
        {
            try
            {
                Id = data.Rows[0]["Id"] != DBNull.Value ? (string)data.Rows[0]["Id"] : "";
                UserName = data.Rows[0]["UserName"] != DBNull.Value ? (string)data.Rows[0]["UserName"] : "";
                Email = data.Rows[0]["Email"] != DBNull.Value ? (string)data.Rows[0]["Email"] : "";
                Email = data.Rows[0]["PhoneNumber"] != DBNull.Value ? (string)data.Rows[0]["PhoneNumber"] : "";
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public string Id { get; }
        public string UserName { get; }
        public string Email { get; }
        public string PhoneNumber { get; }
    }

    /// <summary>
    /// User structure
    /// </summary>
    public class User
    {
        public User(Dictionary<string, dynamic> data)
        {
            try
            {
                APIKey = data["data"]["api_key"];
                Products = new string[] { "" };// (string[])data["data"]["products"].ToArray(typeof(string));
                UserName = data["data"]["user_name"];
                UserShortName = data["data"]["user_shortname"];
                AvatarURL = data["data"]["avatar_url"];
                Broker = data["data"]["broker"];
                AccessToken = data["data"]["access_token"];
                PublicToken = data["data"]["public_token"];
                RefreshToken = data["data"]["refresh_token"];
                UserType = data["data"]["user_type"];
                UserId = data["data"]["user_id"];
                LoginTime = Utils.StringToDate(data["data"]["login_time"]);
                Exchanges = new string[] { "" };//(string[])data["data"]["exchanges"].ToArray(typeof(string));
                OrderTypes = new string[] { "" }; //(string[])data["data"]["order_types"].ToArray(typeof(string));
                Email = data["data"]["email"];
                //ConsumerKey = data["data"]["consumerkey"];
                Email = data["data"]["email"];
                SID = String.Empty; //data["data"]["sid"]; //!= DBNull.Value ? (string)data.Rows[0]["SID"] : "";
                AppSecret = String.Empty;
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public User (DataTable data)
        {
            try
            {
                APIKey = data.Rows[0]["ApiKey"] != DBNull.Value? (string) data.Rows[0]["ApiKey"]: "";
                UserName = data.Rows[0]["UserName"] != DBNull.Value ? (string)data.Rows[0]["UserName"] : "";
                Broker = data.Rows[0]["Broker"] != DBNull.Value ? (string)data.Rows[0]["Broker"] : "";
                AccessToken = data.Rows[0]["AccessToken"] != DBNull.Value ? (string)data.Rows[0]["AccessToken"] : "";
                PublicToken = data.Rows[0]["RequestToken"] != DBNull.Value ? (string)data.Rows[0]["RequestToken"] : "";
                AppSecret = data.Rows[0]["AppSecret"] != DBNull.Value ? (string)data.Rows[0]["AppSecret"] : "";
                UserId = data.Rows[0]["UserId"] != DBNull.Value ? (string)data.Rows[0]["UserId"] : "";
                Email = data.Rows[0]["Email"] != DBNull.Value ? (string)data.Rows[0]["Email"] : "";
                UserShortName = data.Rows[0]["UserName"] != DBNull.Value ? (string)data.Rows[0]["UserName"] : "";
                ConsumerKey = data.Rows[0]["ConsumerKey"] != DBNull.Value ? (string)data.Rows[0]["ConsumerKey"] : "";
                SessionToken = data.Rows[0]["SessionToken"] != DBNull.Value ? (string)data.Rows[0]["SessionToken"] : "";
                RootServer = data.Rows[0]["RootServer"] != DBNull.Value ? (string)data.Rows[0]["RootServer"] : "";
                SID = data.Rows[0]["SID"] != DBNull.Value ? (string)data.Rows[0]["SID"] : "";
                HsServerId = data.Rows[0]["HsServerId"] != DBNull.Value ? (string)data.Rows[0]["HsServerId"] : "";
                Products = null;
                AvatarURL = String.Empty;
                UserType = String.Empty;
                RefreshToken = String.Empty;
                LoginTime = null;
                Exchanges = null;
                OrderTypes = null;
                ApplicationUserId = data.Rows[0]["ApplicationUserId"] != DBNull.Value ? (string)data.Rows[0]["ApplicationUserId"] : "";
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }

        public string APIKey { get; }
        public string[] Products { get; }
        public string UserName { get; }
        public string UserShortName { get; }
        public string AvatarURL { get; }
        public string Broker { get; }
        public string AccessToken { get; set; }
        public string PublicToken { get; }
        public string RefreshToken { get; }
        public string UserType { get; }
        public string UserId { get; }
        public DateTime? LoginTime { get; }
        public string[] Exchanges { get; }
        public string[] OrderTypes { get; }
        public string Email { get; }
        public string SessionToken { get; set; }
        public string ConsumerKey { get; }
        public string AppSecret { get; }
        public string SID { get; set; }
        public string HsServerId { get; set; }
        public string RootServer { get; set; }

        public string ApplicationUserId { get; set; } = "";
        public string vToken { get; set; }
    }

    /// <summary>
    /// GTTOrder structure
    /// </summary>
    public struct GTT
    {
        public GTT(Dictionary<string, dynamic> data)
        {
            try
            {
                Id = Convert.ToInt32(data["id"]);
                Condition = new GTTCondition(data["condition"]);
                TriggerType = data["type"];

                Orders = new List<GTTOrder>();
                foreach (Dictionary<string, dynamic> item in data["orders"])
                    Orders.Add(new GTTOrder(item));

                Status = data["status"];
                CreatedAt = Utils.StringToDate(data["created_at"]);
                UpdatedAt = Utils.StringToDate(data["updated_at"]);
                ExpiresAt = Utils.StringToDate(data["expires_at"]);
                Meta = new GTTMeta(data["meta"]);
            }
            catch (Exception e)
            {
                throw new DataException("Unable to parse data. " + Utils.JsonSerialize(data), HttpStatusCode.OK, e);
            }
        }

        public int Id { get; set; }
        public GTTCondition? Condition { get; set; }
        public string TriggerType { get; set; }
        public List<GTTOrder> Orders { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public GTTMeta? Meta { get; set; }
    }

    /// <summary>
    /// GTTMeta structure
    /// </summary>
    public struct GTTMeta
    {
        public GTTMeta(Dictionary<string, dynamic> data)
        {
            try
            {
                RejectionReason = data != null && data.ContainsKey("rejection_reason") ? data["rejection_reason"] : "";
            }
            catch (Exception e)
            {
                throw new DataException("Unable to parse data. " + Utils.JsonSerialize(data), HttpStatusCode.OK, e);
            }
        }

        public string RejectionReason { get; set; }
    }

    /// <summary>
    /// GTTCondition structure
    /// </summary>
    public struct GTTCondition
    {
        public GTTCondition(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = 0;
                if (data.ContainsKey("instrument_token"))
                {
                    InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                }
                Exchange = data["exchange"];
                TradingSymbol = data["tradingsymbol"];
                TriggerValues = (data["trigger_values"] as ArrayList).Cast<decimal>().ToList();
                LastPrice = data["last_price"];
            }
            catch (Exception e)
            {
                throw new DataException("Unable to parse data. " + Utils.JsonSerialize(data), HttpStatusCode.OK, e);
            }
        }

        public UInt32 InstrumentToken { get; set; }
        public string Exchange { get; set; }
        public string TradingSymbol { get; set; }
        public List<decimal> TriggerValues { get; set; }
        public decimal LastPrice { get; set; }
    }

    /// <summary>
    /// GTTOrder structure
    /// </summary>
    public struct GTTOrder
    {
        public GTTOrder(Dictionary<string, dynamic> data)
        {
            try
            {
                TransactionType = data["transaction_type"];
                Product = data["product"];
                OrderType = data["order_type"];
                Quantity = Convert.ToInt32(data["quantity"]);
                Price = data["price"];
                Result = data["result"] == null ? null : new Nullable<GTTResult>(new GTTResult(data["result"]));
            }
            catch (Exception e)
            {
                throw new DataException("Unable to parse data. " + Utils.JsonSerialize(data), HttpStatusCode.OK, e);
            }
        }

        public string TransactionType { get; set; }
        public string Product { get; set; }
        public string OrderType { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public GTTResult? Result { get; set; }
    }

    /// <summary>
    /// GTTResult structure
    /// </summary>
    public struct GTTResult
    {
        public GTTResult(Dictionary<string, dynamic> data)
        {
            try
            {
                OrderResult = data["order_result"] == null ? null : new Nullable<GTTOrderResult>(new GTTOrderResult(data["order_result"]));
                Timestamp = data["timestamp"];
                TriggeredAtPrice = data["triggered_at"];
            }
            catch (Exception e)
            {
                throw new DataException("Unable to parse data. " + Utils.JsonSerialize(data), HttpStatusCode.OK, e);
            }
        }

        public GTTOrderResult? OrderResult { get; set; }
        public string Timestamp { get; set; }
        public decimal TriggeredAtPrice { get; set; }
    }

    /// <summary>
    /// GTTOrderResult structure
    /// </summary>
    public struct GTTOrderResult
    {
        public GTTOrderResult(Dictionary<string, dynamic> data)
        {
            try
            {
                OrderId = data["order_id"];
                RejectionReason = data["rejection_reason"];
            }
            catch (Exception e)
            {
                throw new DataException("Unable to parse data. " + Utils.JsonSerialize(data), HttpStatusCode.OK, e);
            }
        }

        public string OrderId { get; set; }
        public string RejectionReason { get; set; }
    }

    /// <summary>
    /// GTTParams structure
    /// </summary>
    public struct GTTParams
    {
        public string TradingSymbol { get; set; }
        public string Exchange { get; set; }
        public UInt32 InstrumentToken { get; set; }
        public string TriggerType { get; set; }
        public decimal LastPrice { get; set; }
        public List<GTTOrderParams> Orders { get; set; }
        public List<decimal> TriggerPrices { get; set; }
    }

    /// <summary>
    /// GTTOrderParams structure
    /// </summary>
    public struct GTTOrderParams
    {
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        // Order type (LIMIT, SL, SL-M, MARKET)
        public string OrderType { get; set; }
        // Product code (NRML, MIS, CNC)
        public string Product { get; set; }
        // Transaction type (BUY, SELL)
        public string TransactionType { get; set; }
    }
    public struct TokenSet
    {
        public TokenSet(Dictionary<string, dynamic> data)
        {
            try
            {
                UserId = data["data"]["user_id"];
                AccessToken = data["data"]["access_token"];
                RefreshToken = data["data"]["refresh_token"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }
        }
        public string UserId { get; }
        public string AccessToken { get; }
        public string RefreshToken { get; }
    }

    /// <summary>
    /// User structure
    /// </summary>
    public struct Profile
    {
        public Profile(Dictionary<string, dynamic> data)
        {
            try
            {
                Products = (string[])data["data"]["products"].ToArray(typeof(string));
                UserName = data["data"]["user_name"];
                UserShortName = data["data"]["user_shortname"];
                AvatarURL = data["data"]["avatar_url"];
                Broker = data["data"]["broker"];
                UserType = data["data"]["user_type"];
                Exchanges = (string[])data["data"]["exchanges"].ToArray(typeof(string));
                OrderTypes = (string[])data["data"]["order_types"].ToArray(typeof(string));
                Email = data["data"]["email"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }


        public string[] Products { get; }
        public string UserName { get; }
        public string UserShortName { get; }
        public string AvatarURL { get; }
        public string Broker { get; }
        public string UserType { get; }
        public string[] Exchanges { get; }
        public string[] OrderTypes { get; }
        public string Email { get; }
    }

    /// <summary>
    /// Quote structure
    /// </summary>
    public struct Quote
    {
        public Quote(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                Timestamp = Utils.StringToDate(data["timestamp"]);
                LastPrice = data["last_price"];
                LastQuantity = Convert.ToUInt32(data["last_quantity"]);
                LastTradeTime = Utils.StringToDate(data["last_trade_time"]);
                AveragePrice = data["average_price"];
                Volume = Convert.ToUInt32(data["volume"]);

                BuyQuantity = Convert.ToUInt32(data["buy_quantity"]);
                SellQuantity = Convert.ToUInt32(data["sell_quantity"]);

                Open = data["ohlc"]["open"];
                Close = data["ohlc"]["close"];
                Low = data["ohlc"]["low"];
                High = data["ohlc"]["high"];

                Change = data["net_change"];
                
                OI = Convert.ToUInt32(data["oi"]);
                
                OIDayHigh = Convert.ToUInt32(data["oi_day_high"]);
                OIDayLow = Convert.ToUInt32(data["oi_day_low"]);

                Bids = new List<DepthItem>();
                Offers = new List<DepthItem>();

                if(data["depth"]["buy"] != null)
                {
                    foreach (Dictionary<string, dynamic> bid in data["depth"]["buy"])
                        Bids.Add(new DepthItem(bid));
                }

                if (data["depth"]["sell"] != null)
                {
                    foreach (Dictionary<string, dynamic> offer in data["depth"]["sell"])
                        Offers.Add(new DepthItem(offer));
                }
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public UInt32 InstrumentToken { get; set; }
        public decimal LastPrice { get; set; }
        public UInt32 LastQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public UInt32 Volume { get; set; }
        public UInt32 BuyQuantity { get; set; }
        public UInt32 SellQuantity { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Change { get; set; }
        public List<DepthItem> Bids { get; set; }
        public List<DepthItem> Offers { get; set; }

        // KiteConnect 3 Fields

        public DateTime? LastTradeTime { get; set; }
        public UInt32 OI { get; set; }
        public UInt32 OIDayHigh { get; set; }
        public UInt32 OIDayLow { get; set; }
        public DateTime? Timestamp { get; set; }
    }


    /// <summary>
    /// LiveOHLCData Quote structure
    /// </summary>
    public struct LiveOHLCData
    {
        public OHLC OHLCData { get; set; }
        public Dictionary<Byte, DepthItem[]> Bids { get; set; }
        public Dictionary<Byte, DepthItem[]> Offers { get; set; }
    }


    /// <summary>
    /// OHLC Quote structure
    /// </summary>
    public class OHLC
    {
        public OHLC(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                LastPrice = data["last_price"];

                Open = data["ohlc"]["open"];
                Close = data["ohlc"]["close"];
                Low = data["ohlc"]["low"];
                High = data["ohlc"]["high"];

                //Adder Later
                Volume = 0;
                OpenTime = null;
                CloseTime = null;
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }
        public OHLC(Historical historical, uint token)
        {
            InstrumentToken = token;
            LastPrice = historical.Close;

            Open = historical.Open;
            Close = historical.Close;
            Low = historical.Low;
            High = historical.High;

            //Adder Later
            Volume = historical.Volume;
            OpenTime = null;
            CloseTime = null;
        }
        public OHLC()
        {

        }
        public UInt32 InstrumentToken { get; set; }
        public decimal LastPrice { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public UInt32 Volume { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
    }

    public enum PivotFrequency
    {
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Yearly = 4,
        Rolling = 5
    }
    public enum PivotLevel
    {
        CPR = 0,
        LCPR = 1,
        UCPR = 2,
        R1 = 3,
        LR1 = 4,
        UR1 = 5,
        R2 = 6,
        LR2 = 7,
        UR2 = 8,
        R3 = 9,
        LR3 = 10,
        UR3 = 11,
        S1 = 12,
        LS1 = 13,
        US1 = 14,
        S2 = 15,
        LS2 = 16,
        US2 = 17,
        S3 = 18,
        LS3 = 19,
        US3 = 20,
        S4 = 21,
        LS4 = 22,
        US4 = 23,
        R4 = 24,
        LR4 = 25,
        UR4 = 26
    }
    public class Pivot
    {
        public decimal[] Price { get; set; }
       // public Instrument InstrumentToTrade { get; set; }

        public Pivot()
        {
            Price = new decimal[3];
        }
    }

    public class CentralPivotRange
    {
        //public Pivot CentralPivot { get; set; }
        //public Pivot[] R { get; set; }
        //public Pivot[] S { get; set; }

        public decimal[] Prices { get; set; }
        //public CentralPivotRange(DataRow[] instrumentsOHLC)
        //{
        //    foreach (DataRow instrumentOHLC in instrumentOHLC)
        //    {
        //        CentralPivot = new Pivot();

        //        CentralPivot.Price[Constants.CURRENT] = (ohlc.High + ohlc.Low + ohlc.Close) / 3;
        //        CentralPivot.Price[Constants.LOW] = (ohlc.High + ohlc.Low) / 2;


        //        if (CentralPivot.Price[Constants.LOW] > CentralPivot.Price[Constants.CURRENT])
        //        {
        //            CentralPivot.Price[Constants.HIGH] = CentralPivot.Price[Constants.LOW];
        //            CentralPivot.Price[Constants.LOW] = (CentralPivot.Price[Constants.CURRENT] - CentralPivot.Price[Constants.HIGH]) + CentralPivot.Price[Constants.CURRENT];
        //        }
        //        else
        //        {
        //            CentralPivot.Price[Constants.HIGH] = (CentralPivot.Price[Constants.CURRENT] - CentralPivot.Price[Constants.LOW]) + CentralPivot.Price[Constants.CURRENT];
        //        }

        //        R = new Pivot[3]; S = new Pivot[3];

        //        R[0] = new Pivot(); R[1] = new Pivot(); R[2] = new Pivot();
        //        S[0] = new Pivot(); S[1] = new Pivot(); S[2] = new Pivot();

        //        R[0].Price[Constants.CURRENT] = 2 * (CentralPivot.Price[Constants.CURRENT]) - ohlc.Low;
        //        S[0].Price[Constants.CURRENT] = 2 * (CentralPivot.Price[Constants.CURRENT]) - ohlc.High;
        //        R[0].Price[Constants.HIGH] = R[0].Price[Constants.LOW] = R[0].Price[Constants.CURRENT];
        //        S[0].Price[Constants.HIGH] = S[0].Price[Constants.LOW] = S[0].Price[Constants.CURRENT];

        //        R[1].Price[Constants.CURRENT] = CentralPivot.Price[Constants.CURRENT] + (ohlc.High - ohlc.Low);
        //        S[1].Price[Constants.CURRENT] = Math.Abs(CentralPivot.Price[Constants.CURRENT] - (ohlc.High - ohlc.Low));
        //        R[1].Price[Constants.HIGH] = R[1].Price[Constants.LOW] = R[1].Price[Constants.CURRENT];
        //        S[1].Price[Constants.HIGH] = S[1].Price[Constants.LOW] = S[1].Price[Constants.CURRENT];

        //        R[2].Price[Constants.CURRENT] = ohlc.High + 2 * (CentralPivot.Price[Constants.CURRENT] - ohlc.Low);
        //        S[2].Price[Constants.CURRENT] = Math.Abs(ohlc.Low - 2 * (ohlc.High - CentralPivot.Price[Constants.CURRENT]));
        //        R[2].Price[Constants.HIGH] = R[2].Price[Constants.LOW] = R[2].Price[Constants.CURRENT];
        //        S[2].Price[Constants.HIGH] = S[2].Price[Constants.LOW] = S[2].Price[Constants.CURRENT];
        //    }
        //}

        private CentralPivotRange ()
        {

        }

        public CentralPivotRange(OHLC ohlc)
        {
            Prices = new decimal[System.Enum.GetValues(typeof(PivotLevel)).Length];

            Prices[(int)PivotLevel.CPR] = (ohlc.High + ohlc.Low + ohlc.Close) / 3;
            Prices[(int)PivotLevel.LCPR] = (ohlc.High + ohlc.Low) / 2;

            if (Prices[(int)PivotLevel.LCPR] > Prices[(int)PivotLevel.CPR])
            {
                Prices[(int)PivotLevel.UCPR] = Prices[(int)PivotLevel.LCPR];
                Prices[(int)PivotLevel.LCPR] = (Prices[(int)PivotLevel.CPR] - Prices[(int)PivotLevel.UCPR]) + Prices[(int)PivotLevel.CPR];
            }
            else
            {
                Prices[(int)PivotLevel.UCPR] = (Prices[(int)PivotLevel.CPR] - Prices[(int)PivotLevel.LCPR]) + Prices[(int)PivotLevel.CPR];
            }
            
            decimal bandProportion = (Prices[(int)PivotLevel.CPR] - Prices[(int)PivotLevel.LCPR]) / (Prices[(int)PivotLevel.CPR]);

            Prices[(int)PivotLevel.R1] = 2 * (Prices[(int)PivotLevel.CPR]) - ohlc.Low;
            Prices[(int)PivotLevel.UR1] = Prices[(int)PivotLevel.R1] * (1 + bandProportion);
            Prices[(int)PivotLevel.LR1] = Prices[(int)PivotLevel.R1] - (Prices[(int)PivotLevel.UR1] - Prices[(int)PivotLevel.R1]);


            Prices[(int)PivotLevel.S1] = 2 * (Prices[(int)PivotLevel.CPR]) - ohlc.High;
            Prices[(int)PivotLevel.US1] = Prices[(int)PivotLevel.S1] * (1 + bandProportion);
            Prices[(int)PivotLevel.LS1] = Prices[(int)PivotLevel.S1] - (Prices[(int)PivotLevel.US1] - Prices[(int)PivotLevel.S1]);


            Prices[(int)PivotLevel.R2] = Prices[(int)PivotLevel.CPR] + (ohlc.High - ohlc.Low);
            Prices[(int)PivotLevel.UR2] = Prices[(int)PivotLevel.R2] * (1 + bandProportion);
            Prices[(int)PivotLevel.LR2] = Prices[(int)PivotLevel.R2] - (Prices[(int)PivotLevel.UR2] - Prices[(int)PivotLevel.R2]);


            Prices[(int)PivotLevel.S2] = Math.Abs(Prices[(int)PivotLevel.CPR] - (ohlc.High - ohlc.Low));
            Prices[(int)PivotLevel.US2] = Prices[(int)PivotLevel.S2] * (1 + bandProportion);
            Prices[(int)PivotLevel.LS2] = Prices[(int)PivotLevel.S2] - (Prices[(int)PivotLevel.US2] - Prices[(int)PivotLevel.S2]);


            Prices[(int)PivotLevel.R3] = ohlc.High + 2 * (Prices[(int)PivotLevel.CPR] - ohlc.Low);
            Prices[(int)PivotLevel.UR3] = Prices[(int)PivotLevel.R3] * (1 + bandProportion);
            Prices[(int)PivotLevel.LR3] = Prices[(int)PivotLevel.R3] - (Prices[(int)PivotLevel.UR3] - Prices[(int)PivotLevel.R3]);

            Prices[(int)PivotLevel.S3] = Math.Abs(ohlc.Low - 2 * (ohlc.High - Prices[(int)PivotLevel.CPR]));
            Prices[(int)PivotLevel.US3] = Prices[(int)PivotLevel.S3] * (1 + bandProportion);
            Prices[(int)PivotLevel.LS3] = Prices[(int)PivotLevel.S3] - (Prices[(int)PivotLevel.US3] - Prices[(int)PivotLevel.S3]);


            Prices[(int)PivotLevel.R4] = ohlc.High + 3 * (Prices[(int)PivotLevel.CPR] - ohlc.Low);
            Prices[(int)PivotLevel.UR4] = Prices[(int)PivotLevel.R4] * (1 + bandProportion);
            Prices[(int)PivotLevel.LR4] = Prices[(int)PivotLevel.R4] - (Prices[(int)PivotLevel.UR4] - Prices[(int)PivotLevel.R4]);

            Prices[(int)PivotLevel.S4] = Math.Abs(ohlc.Low - 3 * (ohlc.High - Prices[(int)PivotLevel.CPR]));
            Prices[(int)PivotLevel.US4] = Prices[(int)PivotLevel.S4] * (1 + bandProportion);
            Prices[(int)PivotLevel.LS4] = Prices[(int)PivotLevel.S4] - (Prices[(int)PivotLevel.US4] - Prices[(int)PivotLevel.S4]);

            //PivotFrequency = PivotFrequency.Daily;
            //PivotFrequencyWindow = null;
        }

        //public CentralPivotRange(OHLC ohlc)
        //{
        //    CentralPivot = new Pivot();

        //    CentralPivot.Price[Constants.CURRENT] = (ohlc.High + ohlc.Low + ohlc.Close) / 3;
        //    CentralPivot.Price[Constants.LOW] = (ohlc.High + ohlc.Low) / 2;

        //    if (CentralPivot.Price[Constants.LOW] > CentralPivot.Price[Constants.CURRENT])
        //    {
        //        CentralPivot.Price[Constants.HIGH] = CentralPivot.Price[Constants.LOW];
        //        CentralPivot.Price[Constants.LOW] = (CentralPivot.Price[Constants.CURRENT] - CentralPivot.Price[Constants.HIGH]) + CentralPivot.Price[Constants.CURRENT];
        //    }
        //    else
        //    {
        //        CentralPivot.Price[Constants.HIGH] = (CentralPivot.Price[Constants.CURRENT] - CentralPivot.Price[Constants.LOW]) + CentralPivot.Price[Constants.CURRENT];
        //    }

        //    decimal bandProportion = (CentralPivot.Price[Constants.CURRENT] - CentralPivot.Price[Constants.LOW]) / (CentralPivot.Price[Constants.CURRENT]);

        //    R = new Pivot[3]; S = new Pivot[3];

        //    R[0] = new Pivot(); R[1] = new Pivot(); R[2] = new Pivot();
        //    S[0] = new Pivot(); S[1] = new Pivot(); S[2] = new Pivot();

        //    R[0].Price[Constants.CURRENT] = 2 * (CentralPivot.Price[Constants.CURRENT]) - ohlc.Low;

        //    R[0].Price[Constants.HIGH] = R[0].Price[Constants.CURRENT] * (1 + bandProportion);
        //    R[0].Price[Constants.LOW] = R[0].Price[Constants.CURRENT] - (R[0].Price[Constants.HIGH] - R[0].Price[Constants.CURRENT]);


        //    S[0].Price[Constants.CURRENT] = 2 * (CentralPivot.Price[Constants.CURRENT]) - ohlc.High;
        //    S[0].Price[Constants.HIGH] = S[0].Price[Constants.CURRENT] * (1 + bandProportion);
        //    S[0].Price[Constants.LOW] = S[0].Price[Constants.CURRENT] - (S[0].Price[Constants.HIGH] - S[0].Price[Constants.CURRENT]);


        //    R[1].Price[Constants.CURRENT] = CentralPivot.Price[Constants.CURRENT] + (ohlc.High - ohlc.Low);
        //    R[1].Price[Constants.HIGH] = R[1].Price[Constants.CURRENT] * (1 + bandProportion);
        //    R[1].Price[Constants.LOW] = R[1].Price[Constants.CURRENT] - (R[1].Price[Constants.HIGH] - R[1].Price[Constants.CURRENT]);


        //    S[1].Price[Constants.CURRENT] = Math.Abs(CentralPivot.Price[Constants.CURRENT] - (ohlc.High - ohlc.Low));
        //    S[1].Price[Constants.HIGH] = S[1].Price[Constants.CURRENT] * (1 + bandProportion);
        //    S[1].Price[Constants.LOW] = S[1].Price[Constants.CURRENT] - (S[1].Price[Constants.HIGH] - S[1].Price[Constants.CURRENT]);


        //    R[2].Price[Constants.CURRENT] = ohlc.High + 2 * (CentralPivot.Price[Constants.CURRENT] - ohlc.Low);
        //    R[2].Price[Constants.HIGH] = R[2].Price[Constants.CURRENT] * (1 + bandProportion);
        //    R[2].Price[Constants.LOW] = R[2].Price[Constants.CURRENT] - (R[2].Price[Constants.HIGH] - R[2].Price[Constants.CURRENT]);

        //    S[2].Price[Constants.CURRENT] = Math.Abs(ohlc.Low - 2 * (ohlc.High - CentralPivot.Price[Constants.CURRENT]));
        //    S[2].Price[Constants.HIGH] = S[2].Price[Constants.CURRENT] * (1 + bandProportion);
        //    S[2].Price[Constants.LOW] = S[2].Price[Constants.CURRENT] - (S[2].Price[Constants.HIGH] - S[2].Price[Constants.CURRENT]);

        //    //PivotFrequency = PivotFrequency.Daily;
        //    //PivotFrequencyWindow = null;
        //}
    }

    public class PivotStrategy
    {
        public int StrategyID { get; set; }
        public PivotInstrument PrimaryInstrument { get; set; }
        public int TradedLot { get; set; }
        public List<PivotInstrument> TradedInstruments { get; set; }
        public int MaximumTradeLot { get; set; }
        public decimal LastTradedPrice { get; set; }
        public int TradingUnitInLots { get; set; }
        public PivotFrequency PivotFrequency { get; set; }
        public int? PivotFrequencyWindow;
        public SortedDictionary<string, PivotInstrument> SubInstruments { get; set; }
    }
    
    public class PivotInstrument : IEquatable<PivotInstrument>
    {
        public int StrategyID { get; set; }
        public CentralPivotRange CPR { get; set; }
        public OHLC ohlc { get; set; }
        public Instrument Instrument { get; set; }
        public decimal TriggerID { get; set; }
        public DateTime LastTradeTime { get; set; }
        public bool Equals(PivotInstrument other)
        {
            if (other == null) return false;
            return (this.StrategyID == other.StrategyID && this.Instrument.InstrumentToken == other.Instrument.InstrumentToken);
        }
    }

    /// <summary>
    /// LTP Quote structure
    /// </summary>
    public struct LTP
    {
        public LTP(Dictionary<string, dynamic> data)
        {
            try
            {
                InstrumentToken = Convert.ToUInt32(data["instrument_token"]);
                LastPrice = Convert.ToDecimal(data["last_price"]);
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }
        public UInt32 InstrumentToken { get; set; }
        public decimal LastPrice { get; }
    }

    /// <summary>
    /// Mutual funds holdings structure
    /// </summary>
    public struct MFHolding
    {
        public MFHolding(Dictionary<string, dynamic> data)
        {
            try
            {
                Quantity = data["quantity"];
                Fund = data["fund"];
                Folio = data["folio"];
                AveragePrice = data["average_price"];
                TradingSymbol = data["tradingsymbol"];
                LastPrice = data["last_price"];
                PNL = data["pnl"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public decimal Quantity { get; }
        public string Fund { get; }
        public string Folio { get; }
        public decimal AveragePrice { get; }
        public string TradingSymbol { get; }
        public decimal LastPrice { get; }
        public decimal PNL { get; }
    }

    /// <summary>
    /// Mutual funds instrument structure
    /// </summary>
    public struct MFInstrument
    {
        public MFInstrument(Dictionary<string, dynamic> data)
        {
            try
            {
                TradingSymbol = data["tradingsymbol"];
                AMC = data["amc"];
                Name = data["name"];

                PurchaseAllowed = data["purchase_allowed"] == "1";
                RedemtpionAllowed = data["redemption_allowed"] == "1";

                MinimumPurchaseAmount = Convert.ToDecimal(data["minimum_purchase_amount"]);
                PurchaseAmountMultiplier = Convert.ToDecimal(data["purchase_amount_multiplier"]);
                MinimumAdditionalPurchaseAmount = Convert.ToDecimal(data["minimum_additional_purchase_amount"]);
                MinimumRedemptionQuantity = Convert.ToDecimal(data["minimum_redemption_quantity"]);
                RedemptionQuantityMultiplier = Convert.ToDecimal(data["redemption_quantity_multiplier"]);
                LastPrice = Convert.ToDecimal(data["last_price"]);

                DividendType = data["dividend_type"];
                SchemeType = data["scheme_type"];
                Plan = data["plan"];
                SettlementType = data["settlement_type"];
                LastPriceDate = Utils.StringToDate(data["last_price_date"]);
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public string TradingSymbol { get; }
        public string AMC { get; }
        public string Name { get; }

        public bool PurchaseAllowed { get; }
        public bool RedemtpionAllowed { get; }

        public decimal MinimumPurchaseAmount { get; }
        public decimal PurchaseAmountMultiplier { get; }
        public decimal MinimumAdditionalPurchaseAmount { get; }
        public decimal MinimumRedemptionQuantity { get; }
        public decimal RedemptionQuantityMultiplier { get; }
        public decimal LastPrice { get; }

        public string DividendType { get; }
        public string SchemeType { get; }
        public string Plan { get; }
        public string SettlementType { get; }
        public DateTime? LastPriceDate { get; }
    }

    /// <summary>
    /// Mutual funds order structure
    /// </summary>
    public struct MFOrder
    {
        public MFOrder(Dictionary<string, dynamic> data)
        {
            try
            {
                StatusMessage = data["status_message"];
                PurchaseType = data["purchase_type"];
                PlacedBy = data["placed_by"];
                Amount = data["amount"];
                Quantity = data["quantity"];
                SettlementId = data["settlement_id"];
                OrderTimestamp =  Utils.StringToDate(data["order_timestamp"]);
                AveragePrice = data["average_price"];
                TransactionType = data["transaction_type"];
                ExchangeOrderId = data["exchange_order_id"];
                ExchangeTimestamp =  Utils.StringToDate(data["exchange_timestamp"]);
                Fund = data["fund"];
                Variety = data["variety"];
                Folio = data["folio"];
                Tradingsymbol = data["tradingsymbol"];
                Tag = data["tag"];
                OrderId = data["order_id"];
                Status = data["status"];
                LastPrice = data["last_price"];
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public string StatusMessage { get; }
        public string PurchaseType { get; }
        public string PlacedBy { get; }
        public decimal Amount { get; }
        public decimal Quantity { get; }
        public string SettlementId { get; }
        public DateTime? OrderTimestamp { get; }
        public decimal AveragePrice { get; }
        public string TransactionType { get; }
        public string ExchangeOrderId { get; }
        public DateTime? ExchangeTimestamp { get; }
        public string Fund { get; }
        public string Variety { get; }
        public string Folio { get; }
        public string Tradingsymbol { get; }
        public string Tag { get; }
        public string OrderId { get; }
        public string Status { get; }
        public decimal LastPrice { get; }
    }

    /// <summary>
    /// Mutual funds SIP structure
    /// </summary>
    public struct MFSIP
    {
        public MFSIP(Dictionary<string, dynamic> data)
        {
            try
            {
                DividendType = data["dividend_type"];
                PendingInstalments = data["pending_instalments"];
                Created = Utils.StringToDate(data["created"]);
                LastInstalment = Utils.StringToDate(data["last_instalment"]);
                TransactionType = data["transaction_type"];
                Frequency = data["frequency"];
                InstalmentDate = data["instalment_date"];
                Fund = data["fund"];
                SIPId = data["sip_id"];
                Tradingsymbol = data["tradingsymbol"];
                Tag = data["tag"];
                InstalmentAmount = data["instalment_amount"];
                Instalments = data["instalments"];
                Status = data["status"];
                OrderId = data.ContainsKey(("order_id")) ? data["order_id"] : "";
            }
            catch (Exception)
            {
                throw new Exception("Unable to parse data. " + Utils.JsonSerialize(data));
            }

        }

        public string DividendType { get; }
        public int PendingInstalments { get; }
        public DateTime? Created { get; }
        public DateTime? LastInstalment { get; }
        public string TransactionType { get; }
        public string Frequency { get; }
        public int InstalmentDate { get; }
        public string Fund { get; }
        public string SIPId { get; }
        public string Tradingsymbol { get; }
        public string Tag { get; }
        public int InstalmentAmount { get; }
        public int Instalments { get; }
        public string Status { get; }
        public string OrderId { get; }
    }

    public class EventWaiter<T>
    {
        private AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private EventInfo _event = null;
        private object _eventContainer = null;

        public EventWaiter(object eventContainer, string eventName)
        {
            _eventContainer = eventContainer;
            _event = eventContainer.GetType().GetEvent(eventName);
        }

        public void WaitForEvent(TimeSpan timeout)
        {
            EventHandler<T> eventHandler = new EventHandler<T>((sender, args) => { _autoResetEvent.Set(); });
            _event.AddEventHandler(_eventContainer, eventHandler);
            _autoResetEvent.WaitOne(timeout);
            _event.RemoveEventHandler(_eventContainer, eventHandler);
        }
    }

}
