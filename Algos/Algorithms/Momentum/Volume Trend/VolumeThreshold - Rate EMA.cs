using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using ZConnectWrapper;
using System.Data;
using ZMQFacade;
using System.Collections;
using System.Net.Http.Headers;
using Algorithms.Indicators;
using Algorithms.Utils;
using Algorithms.Candles;
using System.Dynamic;
using System.Threading;
using MathNet.Numerics;
using System.Diagnostics.Eventing.Reader;

namespace Algorithms.Algorithms
{
    public class VolumeRateEMAThreshold : IZMQ
    {
        //Token ID and Volumes
        //public Dictionary<uint, decimal> tokenVolume;
        public Dictionary<uint, Instrument> ActiveInstruments { get; set; }
        public SortedList<Decimal, Instrument>[] ActiveOptions { get; set; }

        public Dictionary<uint, decimal> tokenLastClose; // This will come from the close in today's ticks
        public Dictionary<uint, CentralPivotRange> tokenCPR;
        //public Dictionary<uint, string> tokenSymbol;
        //public Dictionary<ShortTrade, CriticalLevels> tradeLevels;
        public Dictionary<uint, TradeLevels> tokenTradeLevels;
        public decimal _baseInstrumentPrice;
        Dictionary<uint, ExponentialMovingAverage> sTokenEMA;
        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        private bool _activeTradeExists;
        public List<uint> tokenPrice;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _startDateTime;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        MarketOpenRange indexOpenZone;
        uint _baseInstrumentToken; // = 256265;
        decimal indexPrice = 0;
        //int noOfCandles = 30;
        public const int CANDLE_COUNT = 30;
        public const decimal PRICE_PERCENT_INCREASE = 0.005m;
        public const decimal VOLUME_PERCENT_INCREASE = 1.0m;
        public const long VOLUME_INCREASE_RATE = 2; //1/10 of the candle time
        public const decimal INDEX_PERCENT_CHANGE = 0.001m;
        public const decimal CPR_DISTANCE = 0.00005m;
        public const decimal TARGET_PROFIT = 0.005m;
        public const decimal STOPLOSS_PRICE = 0.0005m;
        public const decimal STOPLOSS_PRICE_OPTION = 0.20m;
        public const int TRADE_QTY = 300;

        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;

        //TimeSpan candletimeframe;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }

        public VolumeRateEMAThreshold(DateTime startTime,
            DateTime endTime, TimeSpan candleTimeSpan, uint baseInstrumentToken, DateTime? expiry)//, decimal strikePriceRange)
        {
            _startDateTime = startTime;
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            indexOpenZone = MarketOpenRange.NA;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            //_strikePriceRange = strikePriceRange;

            //candletimeframe = new TimeSpan(0, 1, 0);

            //tokenVolume = new Dictionary<uint, decimal>();
            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenPrice = new List<uint>();
            tokenExits = new List<uint>();
            //tradeLevels = new Dictionary<ShortTrade, CriticalLevels>();
            tokenTradeLevels = new Dictionary<uint, TradeLevels>();

            //activeInstruments = LoadActiveData(_startDateTime, _endDateTime, _candleTimeSpan, CANDLE_COUNT);
            //LoadActiveData(_baseInstrumentToken, _startDateTime, _endDateTime, _candleTimeSpan, CANDLE_COUNT, _expiryDate); //, _strikePriceRange);
            LoadActiveData(_baseInstrumentToken, _expiryDate); //, _strikePriceRange);

            IEnumerable<uint> tokensToSubscribe = ActiveInstruments.Keys;
            SubscriptionTokens = new List<uint>();
            //SubscriptionTokens.AddRange(tokenVolume.Keys);
            SubscriptionTokens.AddRange(tokensToSubscribe);

            ActiveOptions = new SortedList<decimal, Instrument>[2];

            TimeCandles = new Dictionary<uint, List<Candle>>();

            //EMAs
            sTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();

            CandleSeries candleSeries = new CandleSeries();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            ExponentialMovingAverage sema = null, lema = null;

            ///TODO: Consider sending instrumenttoken in a list, and avoid loop while calling stored procedure
            foreach (uint token in  tokensToSubscribe )//tokenVolume.Keys)
            {
                List<Candle> historicalCandles = candleSeries.LoadCandles(Math.Max(LONG_EMA, CANDLE_COUNT), CandleType.Time, ydayEndTime, token, _candleTimeSpan);
                
                TimeCandles.Add(token, historicalCandles);

                sema = new ExponentialMovingAverage(SHORT_EMA); //.Process(candle.ClosePrice, isFinal: true)
                lema = new ExponentialMovingAverage(LONG_EMA); //.Process(candle.ClosePrice, isFinal: true)

                foreach (var candle in historicalCandles)
                {
                    sema.Process(candle.ClosePrice, isFinal: true);
                    lema.Process(candle.ClosePrice, isFinal: true);
                }
                sTokenEMA.Add(token, sema);
                lTokenEMA.Add(token, lema);
            }

            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
        }

        //private void LoadActiveData(decimal baseInstrumentToken, DateTime startTime,
        //    DateTime endTime, TimeSpan candleTimeSpan, int numberofCandles, DateTime? expiry ) //, decimal _strikePriceRange, DateTime? expiry)
        private void LoadActiveData(decimal baseInstrumentToken, DateTime? expiry, decimal baseInstrumentPrice = 0) //, decimal _strikePriceRange, DateTime? expiry)
        {
            GetInstruments(baseInstrumentToken, expiry, baseInstrumentPrice);
            LoadCPR();
        }
        private void GetInstruments(decimal baseInstrumentToken, DateTime? expiry, decimal baseInstrumentPrice = 0)
        {
            AlgoIndex algoIndex = AlgoIndex.VolumeThreshold;
            DataLogic dl = new DataLogic();

            //decimal fromStrikePrice = baseInstrumentPrice - _strikePriceRange;
            //decimal toStrikePrice = baseInstrumentPrice + _strikePriceRange;

            ActiveInstruments = dl.LoadInstruments(algoIndex, expiry, _baseInstrumentToken, baseInstrumentPrice);
            //,fromStrikePrice, toStrikePrice, InstrumentType.ALL);
        }
        private void LoadCPR()
        {
            DataLogic dl = new DataLogic();
            DataSet dsDailyOHLC = dl.GetDailyOHLC(ActiveInstruments.Keys, _startDateTime);

            OHLC ohlc;
            CentralPivotRange cpr;
            foreach (DataRow dr in dsDailyOHLC.Tables[0].Rows)
            {
                ohlc = new OHLC();
                ohlc.Open = (decimal)dr["Open"];
                ohlc.High = (decimal)dr["High"];
                ohlc.Low = (decimal)dr["Low"];
                ohlc.Close = (decimal)dr["Close"];

                cpr = new CentralPivotRange(ohlc);
                tokenCPR.Add(Convert.ToUInt32(dr["InstrumentToken"]), cpr);
            }
        }
        private void LoadActiveData(DateTime startTime,
            DateTime endTime, TimeSpan candleTimeSpan, int numberofCandles)
        {
            AlgoIndex algoIndex = AlgoIndex.VolumeThreshold;
            DataLogic dl = new DataLogic();
            DataSet ds = dl.LoadHistoricalTokenVolume(algoIndex, startTime,
            endTime, candleTimeSpan, numberofCandles);

            //foreach (DataRow dr in ds.Tables[0].Rows)
            //{
            //    tokenVolume.Add(Convert.ToUInt32(dr["InstrumentToken"]), Convert.ToUInt32(dr["Volume"]));
            //}

            //foreach (DataRow dr in ds.Tables[1].Rows)
            //{
            //    tokenSymbol.Add(Convert.ToUInt32(dr["InstrumentToken"]), (string)dr["TradingSymbol"]);
            //}

            OHLC ohlc;
            CentralPivotRange cpr;
            foreach (DataRow dr in ds.Tables[2].Rows)
            {
                ohlc = new OHLC();
                ohlc.Open = (decimal)dr["Open"];
                ohlc.High = (decimal)dr["High"];
                ohlc.Low = (decimal)dr["Low"];
                ohlc.Close = (decimal)dr["Close"];

                cpr = new CentralPivotRange(ohlc);
                tokenCPR.Add(Convert.ToUInt32(dr["InstrumentToken"]), cpr);
            }

        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            sTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
            lTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
        }

        private void MonitorVolumeThreshold(Tick[] ticks)
        {
            lock (tokenTradeLevels)
            {
                try
                {
                    GetMarketOpenRange(ticks);
                    SetLastPrice(ticks);
                    if (indexOpenZone == MarketOpenRange.NA || ticks[0].InstrumentToken == _baseInstrumentToken)
                    {
                        return;
                    }

                    //Build Candles
                    MonitorCandles(ticks[0]);
                    //Trigger entry
                    MonitorVolumeThreshold(ticks[0].InstrumentToken);
                    //Trigger exit
                    ExitTrades(ticks);
                }
                catch (Exception ex)
                {

                }
            }
        }

        private bool GetBaseInstrumentPrice(Tick[] ticks)
        {
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _baseInstrumentToken);
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                _baseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
        private void ActiveTradeIntraday(Tick[] ticks)
        {
            ///Steps:
            /// 1. Look at the first candle on both CE and PE. Which ever is positive, then take next trade on that side
            /// SL: Wait for 75% value loss compared to previous candle
            /// SL for subsequent candles will be previous candle body low/high
            /// 2.0 Try to fit in RSI to determine when to enter
            /// 3.0 try the trade with money candle and volume candle

            if (GetBaseInstrumentPrice(ticks))
            {
                return;
            }

            Instrument[] options = GetOptionsToTrade();

            foreach (var option in options)
            {
                Candle previousCandle = TimeCandles[option.InstrumentToken].Last(x => x.State == CandleStates.Finished);

                uint token = option.InstrumentToken;
                if (!tokenPrice.Contains(token) && previousCandle.ClosePrice - previousCandle.OpenPrice > 0.002m * previousCandle.OpenPrice)
                {
                    //ENTRY ORDER - BUY ALERT
                    ShortTrade shortTrade = PlaceOrder(option.TradingSymbol, previousCandle.ClosePrice, token, true,
                        TRADE_QTY * Convert.ToInt32(ActiveInstruments[token].LotSize), previousCandle.CloseTime);
                    tokenTradeLevels.Add(token, new TradeLevels { Trade = shortTrade, Levels = GetCriticalLevels(shortTrade, token, optionIntraDay: true) }); // Dictionary<ShortTrade, CriticalLevels>  shortTrade, GetCriticalLevels(shortTrade, timeCandles));
                    tokenPrice.Add(token);
                    //_activeTradeExists = true;
                }

                ExitOptions(ticks, option);
            }
        }
        private void ExitOptions(Tick[] ticks, Instrument option)
        {
            try
            {
                uint token = 0;
                foreach (Tick tick in ticks)
                {
                    token = option.InstrumentToken;
                    if (!tokenTradeLevels.ContainsKey(token))
                    {
                        continue;
                    }
                    var tt = tokenTradeLevels[tick.InstrumentToken];

                    if (tokenPrice.Contains(token) && tt.Trade.TransactionType.ToLower() == "buy" && tick.LastPrice < tt.Levels.StopLossPrice)
                    {
                        //exit trade
                        ShortTrade shortTrade = PlaceOrder(option.TradingSymbol, tick.LastPrice, token, false, 
                            TRADE_QTY * Convert.ToInt32(option.LotSize), tick.LastTradeTime);
                        shortTrade.TradingStatus = TradeStatus.Closed;

                        tokenPrice.Remove(tick.InstrumentToken);
                    }
                    else if (tokenPrice.Contains(token) &&  tt.Trade.TransactionType.ToLower() == "sell" && tick.LastPrice > tt.Levels.StopLossPrice)
                    {
                        //exit trade
                        ShortTrade shortTrade = PlaceOrder(option.TradingSymbol, tick.LastPrice, token, true, 
                            TRADE_QTY * Convert.ToInt32(option.LotSize), tick.LastTradeTime);
                        shortTrade.TradingStatus = TradeStatus.Closed;
                        
                        tokenPrice.Remove(tick.InstrumentToken);
                    }
                }
            }
            catch (Exception exp)
            {

            }
        }
            private Instrument[] GetOptionsToTrade()
        {
            int _strikePriceIncrement = 100;

            var ceStrike = Math.Round(_baseInstrumentPrice / 100m, MidpointRounding.AwayFromZero) * 100m;
            var peStrike = ceStrike - _strikePriceIncrement;
            Instrument[] options = new Instrument[2];

            if (ActiveOptions == null || ActiveOptions[(int)InstrumentType.CE] == null || ActiveOptions[(int)InstrumentType.PE] == null)
            {
                ActiveOptions = GetNewStrikes(_baseInstrumentToken, _baseInstrumentPrice, _expiryDate, _strikePriceIncrement);
            }
            options[(int)InstrumentType.CE] = ActiveOptions[(int)InstrumentType.CE][ceStrike];
            options[(int)InstrumentType.PE] = ActiveOptions[(int)InstrumentType.PE][peStrike];

            return options;
        }

        private void MonitorCandles(Tick tick)
        {
            uint token = tick.InstrumentToken;
            //check the below statement, this should not keep on adding to TimeCandles with everycall, as the list doesnt return new candles unless built
            if (TimeCandles.ContainsKey(token))
            {
                TimeCandles[token] = candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
            }
            else
            {
                TimeCandles.Add(token, candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true)); // TODO: USING LOCAL VERSION RIGHT NOW

                sTokenEMA.Add(token, new ExponentialMovingAverage(SHORT_EMA));
                lTokenEMA.Add(token, new ExponentialMovingAverage(LONG_EMA));
            }
            
            sTokenEMA[token].Process(TimeCandles[token].Last().ClosePrice, isFinal: false);
            lTokenEMA[token].Process(TimeCandles[token].Last().ClosePrice, isFinal: false);
        }


        private void GetMarketOpenRange(Tick[] ticks)
        {
            //Check NIFTY GAP UP OR DOWN
            //if (indexOpenZone == MarketOpenRange.NA)
            //{
            Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _baseInstrumentToken);
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                indexPrice = baseInstrumentTick.LastPrice;

                //checking it with continuous price
                if (indexPrice < baseInstrumentTick.Close * (1 - INDEX_PERCENT_CHANGE))
                {
                    indexOpenZone = MarketOpenRange.GapDown;
                }
                else if (indexPrice > baseInstrumentTick.Close * (1 + INDEX_PERCENT_CHANGE))
                {
                    indexOpenZone = MarketOpenRange.GapUp;
                }
                else
                {
                    indexOpenZone = MarketOpenRange.Sideways;
                }
            }
            //else if (indexPrice == 0)
            //{
            //    return;
            //}
            //}
        }
        private void MonitorVolumeThreshold(uint token)
        {
            TimeFrameCandle t = TimeCandles[token].Last() as TimeFrameCandle;

            ///TODO: Currently using ClosePrice from live ticks to determine yesterday's close. 
            ///This close price is different from last price at 3:30 yesteday, so need to revisit 
            ///to check if there is a better and more reliable way to pull closing price.
            ///check for Nifty closing price

            //foreach (uint token in tokenVolume.Keys)
            //{
            ///TODO: Currently using ClosePrice from live ticks to determine yesterday's close. 
            ///This close price is different from last price at 3:30 yesteday, so need to revisit 
            ///to check if there is a better and more reliable way to pull closing price.
            ///check for Nifty closing price

            //If index is up, look for upwards breakout. if index is down look for downwards breakout.
            if (indexOpenZone == MarketOpenRange.GapUp)
            {
                if (sTokenEMA[token].GetValue<decimal>(0) > lTokenEMA[token].GetValue<decimal>(0) 
                    &&  t.ClosePrice > TimeCandles[token].GetRange(TimeCandles[token].Count - 1 - CANDLE_COUNT, CANDLE_COUNT).Max(x=>x.ClosePrice) * (1 + PRICE_PERCENT_INCREASE) && (t.ClosePrice > t.OpenPrice)
                    // && t.TotalVolume > tokenVolume[token]/5m && (t.CloseTime - t.OpenTime) <= new TimeSpan(_candleTimeSpan.Ticks / VOLUME_INCREASE_RATE))
                    //&& t.TotalVolume > TimeCandles[token].GetRange(TimeCandles[token].Count - 1 - CANDLE_COUNT, CANDLE_COUNT).Max(x => x.TotalVolume) 
                    && (t.CloseTime - t.OpenTime) <= new TimeSpan(_candleTimeSpan.Ticks / VOLUME_INCREASE_RATE))
                {
                    //Check if any CPR range is within 5% movement.
                    if (!tokenPrice.Contains(token) && !tokenExits.Contains(token)) // && CheckNoCPRNearBy(token, t.ClosePrice, true)) //Since the logic of volume trade changed from buy to sell , the CPR check is not needed
                    {
                        //BUY ALERT
                        ShortTrade shortTrade = PlaceOrder(ActiveInstruments[token].TradingSymbol, t.ClosePrice, token, true, 
                            TRADE_QTY* Convert.ToInt32(ActiveInstruments[token].LotSize), t.CloseTime);
                        tokenTradeLevels.Add(token, new TradeLevels { Trade = shortTrade, Levels = GetCriticalLevels(shortTrade, token) }); // Dictionary<ShortTrade, CriticalLevels>  shortTrade, GetCriticalLevels(shortTrade, timeCandles));
                        tokenPrice.Add(token);
                    }
                }

                //Tick tick = ticks.FirstOrDefault(x => x.InstrumentToken == indexToken);
                //if(tick != null && tick.LastPrice != 0)
                //{
                //Remove ticks that do not qualify from the subcribed list for this algo
                //if (tick.LastPrice > tick.Close * (1 + PRICE_PERCENT_INCREASE) && tick.Volume > tokenVolume[token] * (1 + VOLUME_PERCENT_INCREASE))
                //    {
                //        //Check if any CPR range is within 5% movement.
                //        if(CheckNoCPRNearBy(tick, true))
                //        {
                //            //BUY ALERT
                //            ShortTrade shortTrade = PlaceOrder(tokenSymbol[tick.InstrumentToken], tick.LastPrice, tick.InstrumentToken, true, TRADE_QTY, tick.Timestamp);
                //        }
                //    }
                //}
            }
            else if (indexOpenZone == MarketOpenRange.GapDown)
            {
                //Tick tick = ticks.FirstOrDefault(x => x.InstrumentToken == indexToken);
                //if (tick != null && tick.LastPrice != 0)
                //{
                //Remove ticks that do not qualify from the subcribed list for this algo
                if (t.ClosePrice < TimeCandles[token].GetRange(TimeCandles[token].Count - 1 - CANDLE_COUNT, CANDLE_COUNT).Min(x => x.ClosePrice) * (1 - PRICE_PERCENT_INCREASE) && (t.ClosePrice < t.OpenPrice)
                   // && (t.TotalVolume > TimeCandles[token].GetRange(TimeCandles[token].Count - 1 - CANDLE_COUNT, CANDLE_COUNT).Max(x => x.TotalVolume)) //tokenVolume[token]) 
                    && (t.CloseTime - t.OpenTime) <= new TimeSpan(_candleTimeSpan.Ticks / VOLUME_INCREASE_RATE))
                {
                    //Check if any CPR range is within 5% movement.
                    if (!tokenPrice.Contains(token) && !tokenExits.Contains(token)) // && CheckNoCPRNearBy(token, t.ClosePrice, false)) //Since the logic of volume trade changed from buy to sell , the CPR check is not needed
                    {
                        //SELL ALERT
                        ShortTrade shortTrade = PlaceOrder(ActiveInstruments[token].TradingSymbol, 
                            t.ClosePrice, token, false, TRADE_QTY * Convert.ToInt32(ActiveInstruments[token].LotSize), t.CloseTime);
                        tokenTradeLevels.Add(token, new TradeLevels { Trade = shortTrade, Levels = GetCriticalLevels(shortTrade, token) });
                        tokenPrice.Add(token);
                    }
                }
                //}
            }
            //}
            
            //Calculate the volume within time span specified.
            //Check if volume is higher (by a %) then all previous stored candlevolumes
            //check the pivot points
            //check the gap up/down
            //check nifty/BNF gap up/down
            //if there is gap up in NIFTY and the stock, volume is high, no pivot points near by - > log alert in database
        }

        private CriticalLevels GetCriticalLevels(ShortTrade st, uint token, bool optionIntraDay = false)
        {
            List<Candle> timeCandles = TimeCandles[token];
            CriticalLevels cl = new CriticalLevels();
            cl.CurrentCandle = timeCandles.Last();

            if (timeCandles.Count > 1)
                cl.PreviousCandle = timeCandles.ElementAt(timeCandles.Count - 2);

            if (st.TransactionType.ToLower() == "sell") //if current trade was sell, next we hve to buy at lower price
            {
                if (optionIntraDay)
                {
                    cl.TargetPrice = 0; // No Target only trailing SL.
                    cl.StopLossPrice = cl.PreviousCandle == null ? st.AveragePrice * (1 - STOPLOSS_PRICE_OPTION) : cl.PreviousCandle.ClosePrice;
                }
                else
                {
                    cl.TargetPrice = st.AveragePrice * (1 - TARGET_PROFIT);
                    //cl.TradedPrice = st.AveragePrice;
                    //cl.StopLossPrice = st.AveragePrice * (1 + STOPLOSS_PRICE);

                    cl.StopLossPrice = cl.CurrentCandle.OpenPrice > st.AveragePrice ?
                       cl.CurrentCandle.OpenPrice : st.AveragePrice * (1 + STOPLOSS_PRICE);

                }

                //cl.StopLossPrice = GetStopLossPrice(st.AveragePrice, true, timeCandles);

                ////SET SL to minumu of 5 mins candle
                //TimeSpan five_min = new TimeSpan(0, 5, 0);
                //long noOfCandles = (five_min.Ticks / ((TimeFrameCandle)cl.CurrentCandle).TimeFrame.Ticks);
                //decimal price = 0;

                //if (timeCandles.Count > noOfCandles)
                //{
                //    price = timeCandles.Skip(timeCandles.Count - Convert.ToInt32(noOfCandles)).Max(x => x.OpenPrice);
                //}
                //else
                //{
                //    price = timeCandles.Max(x => x.OpenPrice);
                //}

                //cl.StopLossPrice = price > st.AveragePrice ?
                //    price : st.AveragePrice * (1 + STOPLOSS_PRICE);

            }
            else
            {
               
                //cl.StopLossPrice = st.AveragePrice * (1 - STOPLOSS_PRICE);

                if (optionIntraDay)
                {
                    cl.TargetPrice = st.AveragePrice * 100; // No Target only trailing SL.
                    cl.StopLossPrice = cl.PreviousCandle == null ? st.AveragePrice * (1 - STOPLOSS_PRICE_OPTION) : cl.PreviousCandle.OpenPrice;
                }
                else
                {
                    cl.TargetPrice = st.AveragePrice * (1 + TARGET_PROFIT);
                    //cl.TradedPrice = st.AveragePrice;

                    cl.StopLossPrice = cl.CurrentCandle.OpenPrice < st.AveragePrice ?
                        cl.CurrentCandle.OpenPrice : st.AveragePrice * (1 - STOPLOSS_PRICE);
                }


                //cl.StopLossPrice = GetStopLossPrice(st.AveragePrice, true, timeCandles);
            }
            return cl;
        }

        private decimal GetStopLossPrice(decimal tradePrice, bool buyTrade, List<Candle> timeCandles)
        {
            //SET SL to minumu of 5 mins candle
            TimeSpan five_min = new TimeSpan(0, 5, 0);
            long noOfCandles = (five_min.Ticks / ((TimeFrameCandle)timeCandles[0]).TimeFrame.Ticks);
            decimal price = 0;

            if (timeCandles.Count > noOfCandles)
            {
                price = timeCandles.Skip(timeCandles.Count - Convert.ToInt32(noOfCandles)).Min(x => x.ClosePrice);
            }
            else
            {
                price = timeCandles.Min(x => x.ClosePrice);
            }

            decimal slPrice = price < tradePrice ?
                    price : tradePrice * (1 - STOPLOSS_PRICE);

            return slPrice;
        }
        private void ExitTrades(Tick[] ticks)
        {
            try
            {
                uint token = 0;
                foreach (Tick tick in ticks)
                {
                    //for (int i = 0; i < tokenTradeLevels.Count; i++)
                    //{
                    token = tick.InstrumentToken;
                    if (!tokenTradeLevels.ContainsKey(token))
                    {
                        continue;
                    }
                    var tt = tokenTradeLevels[tick.InstrumentToken];

                    if (tt.Trade.TransactionType.ToLower() == "sell" && !tokenExits.Contains(token) && tokenPrice.Contains(token))
                    {
                        if (tick.LastPrice <= tt.Levels.TargetPrice)
                        {
                            //set SL to target price
                            //tt.Levels.StopLossPrice = tt.Levels.TargetPrice * (1 + STOPLOSS_PRICE);
                            //tt.Levels.TargetPrice = tt.Levels.TargetPrice * (1 - TARGET_PROFIT / 2);

                            tt.Levels.StopLossPrice = tick.LastPrice * (1 + STOPLOSS_PRICE);
                            tt.Levels.TargetPrice = tick.LastPrice * (1 - TARGET_PROFIT);
                        }
                        else if (tick.LastPrice > tt.Levels.StopLossPrice)// || tick.LastPrice > sTokenEMA[token].GetValue<decimal>(0)) //Math.Max(tt.CurrentCandle.LowPrice, tt.StopLossPrice))
                        {
                            //exit trade
                            ShortTrade shortTrade = PlaceOrder(ActiveInstruments[token].TradingSymbol, 
                                tick.LastPrice, token, true, TRADE_QTY * Convert.ToInt32(ActiveInstruments[token].LotSize), tick.LastTradeTime);
                            shortTrade.TradingStatus = TradeStatus.Closed;
                            tokenExits.Add(token);
                            //tokenPrice.Remove(tick.InstrumentToken);
                        }
                    }
                    if (tt.Trade.TransactionType.ToLower() == "buy" && !tokenExits.Contains(token) && tokenPrice.Contains(token))
                    {
                        if (tick.LastPrice >= tt.Levels.TargetPrice)
                        {
                            //set SL to target price

                            //TimeCandles[ticks[0].InstrumentToken]

                            //tt.Levels.StopLossPrice = tt.Levels.TargetPrice * (1 - STOPLOSS_PRICE);
                            tt.Levels.TargetPrice = tick.LastPrice * (1 + TARGET_PROFIT);
                            tt.Levels.StopLossPrice = tick.LastPrice * (1 - STOPLOSS_PRICE);
                        }
                        else if (tick.LastPrice < tt.Levels.StopLossPrice)// || tick.LastPrice < sTokenEMA[token].GetValue<decimal>(0)) //Math.Min(tt.CurrentCandle.HighPrice, tt.StopLossPrice))
                        {
                            //exit trade
                            ShortTrade shortTrade = PlaceOrder(ActiveInstruments[token].TradingSymbol, 
                                tick.LastPrice, token, false, TRADE_QTY * Convert.ToInt32(ActiveInstruments[token].LotSize), tick.LastTradeTime);
                            //tokenPrice.Remove(tick.InstrumentToken);
                            tokenExits.Add(token);
                        }

                    }
                    //}
                }
            }
            catch (Exception exp)
            {

            }
        }


        public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry, int strikePriceIncrement)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);
            return nodeData;
        }

        public Task<bool> OnNext(Tick[] ticks)
        {
            MonitorVolumeThreshold(ticks);
            //ActiveTradeIntraday(ticks);
            return Task.FromResult(true);
        }
        private void SetLastPrice(Tick[] ticks)
        {
            foreach (Tick tick in ticks)
            {
                if (!tokenLastClose.ContainsKey(tick.InstrumentToken))
                {
                    tokenLastClose.Add(tick.InstrumentToken, tick.Close);
                }
            }
        }
        /// <summary>
        /// Check if CPR is near by in the direction of breakout
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="up"></param>
        /// <returns></returns>
        private bool CheckNoCPRNearBy(uint instrumentToken, decimal currentPrice, bool up)
        {
            CentralPivotRange cpr;
            if (tokenCPR.TryGetValue(instrumentToken, out cpr))
            {
                decimal price = currentPrice * (1 + CPR_DISTANCE);
                if (up && ((price < cpr.Prices[(int)PivotLevel.CPR] && currentPrice > cpr.Prices[(int)PivotLevel.S1]) 
                    || (price < cpr.Prices[(int)PivotLevel.S1] && currentPrice > cpr.Prices[(int)PivotLevel.S2])
                    || (price < cpr.Prices[(int)PivotLevel.S2] && currentPrice > cpr.Prices[(int)PivotLevel.S3])
                    || (price < cpr.Prices[(int)PivotLevel.R2] && currentPrice > cpr.Prices[(int)PivotLevel.R1])
                    || (price < cpr.Prices[(int)PivotLevel.R3] && currentPrice > cpr.Prices[(int)PivotLevel.R2])
                    || (price < cpr.Prices[(int)PivotLevel.R1] && currentPrice > cpr.Prices[(int)PivotLevel.CPR])
                    || (currentPrice > cpr.Prices[(int)PivotLevel.UR3])
                    ))
                {
                    return true;
                }
                price = currentPrice * (1 - CPR_DISTANCE);
                if (!up && ((price > cpr.Prices[(int)PivotLevel.CPR] && currentPrice < cpr.Prices[(int)PivotLevel.R1])
                    || (price > cpr.Prices[(int)PivotLevel.R1] && currentPrice < cpr.Prices[(int)PivotLevel.R2])
                    || (price > cpr.Prices[(int)PivotLevel.R2] && currentPrice < cpr.Prices[(int)PivotLevel.R3])
                    || (price > cpr.Prices[(int)PivotLevel.S1] && currentPrice < cpr.Prices[(int)PivotLevel.CPR])
                    || (price > cpr.Prices[(int)PivotLevel.S2] && currentPrice < cpr.Prices[(int)PivotLevel.S1])
                    || (price > cpr.Prices[(int)PivotLevel.S3] && currentPrice < cpr.Prices[(int)PivotLevel.S2])
                    || (currentPrice < cpr.Prices[(int)PivotLevel.LS3])
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Place order and update database
        /// </summary>
        /// <param name="strangleID"></param>
        /// <param name="instrument_tradingsymbol"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <param name="token"></param>
        /// <param name="triggerID"></param>
        /// <returns></returns>
        private ShortTrade PlaceOrder(string instrument_tradingsymbol, decimal instrument_currentPrice, uint instrument_Token,
            bool buyOrder, int quantity, DateTime? tickTime = null)
        {
            string tradingSymbol = instrument_tradingsymbol;
            decimal currentPrice = instrument_currentPrice;
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            if (orderId != "0")
            {
                System.Threading.Thread.Sleep(200);
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice : currentPrice;
            // averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = tickTime;// DateTime.Now;
            trade.Quantity = quantity;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy" : "Sell";
            trade.TriggerID = Convert.ToInt32(AlgoIndex.VolumeThreshold);
            trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
            
            UpdateTradeDetails(strategyID:0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

            return trade;
        }
        private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        {
            DataLogic dl = new DataLogic();
            dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.ExpiryTrade, tradedLot, triggerID);
        }
    }
}
