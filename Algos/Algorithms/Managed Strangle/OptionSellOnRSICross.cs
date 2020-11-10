using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using GlobalCore;
using KiteConnect;
using ZConnectWrapper;
using System.Data;
using ZMQFacade;
using Algorithms.Indicators;
using Algorithms.Utils;
using Algorithms.Candles;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using System.Reflection.Metadata;

namespace Algos.TLogics
{
    public class OptionSellOnRSICross : IZMQ
    {
        private readonly int _algoInstance;

        public int AlgoInstance
        {
            get
            { return _algoInstance; }
        }

        IOrderedEnumerable<KeyValuePair<decimal, Instrument>> callOptions;
        IOrderedEnumerable<KeyValuePair<decimal, Instrument>> putOptions;
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        //public List<Instrument> CandleOptions { get; set; }

        /// <summary>
        /// algo instance and traded option if any. This will be kept in the DB for next day retrieval.
        /// </summary>
        public Dictionary<int, TradedOption> _tradedOptionInstance;

        List<Instrument> _activeOptions;
        public OrderTrio _activeOrderTrio;
        private readonly int _tradeQty;
        private bool _stopTrade;
        public decimal _baseInstrumentPrice;
        public readonly uint _baseInstrumentToken;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        DateTime _endDateTime;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        List<uint> _RSILoaded;
        List<uint> _SQLLoading;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;

        public const decimal INDEX_PERCENT_CHANGE = 0.001m;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly int _maxDistanceFromBInstrument;
        public readonly int _minDistanceFromBInstrument;
        public readonly decimal _rsiBandForExit;

        private const int RSI_LENGTH = 15;
        private const int CE = 0;
        private const int PE = 1;
        public const AlgoIndex algoIndex = AlgoIndex.SellOnRSICross;
        //public List<string> SubscriptionTokens;
        public List<uint> SubscriptionTokens;
        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        private bool _nwSet = false;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionSellOnRSICross source);

        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;

        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;

        /// TODO: All orders initially should go to database and then it should be loaded here.


        public OptionSellOnRSICross(TimeSpan candleTimeSpan, uint baseInstrumentToken, 
            DateTime endTime, DateTime? expiry, int quantity, int maxDistanceFromBInstrument, 
            int minDistanceFromBInstrument, decimal rsiBandForExit, int algoInstance = 0)
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _activeOptions = new List<Instrument>();
            _maxDistanceFromBInstrument = maxDistanceFromBInstrument;
            _minDistanceFromBInstrument = minDistanceFromBInstrument;
            _rsiBandForExit = rsiBandForExit;
            _tradeQty = quantity;
            SubscriptionTokens = new List<uint>();

            //CandleOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            //RSI
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();

            _RSILoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();

            CandleSeries candleSeries = new CandleSeries();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            
            _stopTrade = true;

            _algoInstance = algoInstance != 0 ? algoInstance :
              Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
              expiry.GetValueOrDefault(DateTime.Now), quantity, 0, 0, 0, 0,
              0, 0, 0, 0, candleTimeFrameInMins:
              (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, 0, _rsiBandForExit, 0,
              _maxDistanceFromBInstrument, _minDistanceFromBInstrument, false, 0);

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
        }

        public void LoadActiveOrders(Order activeOrder)
        {
            if (activeOrder != null)
            {
                _activeOrderTrio = new OrderTrio();
                _activeOrderTrio.Order = activeOrder;

                DataLogic dl = new DataLogic();
                _activeOrderTrio.Option = dl.GetInstrument(activeOrder.InstrumentToken);
                _activeOptions.Add(_activeOrderTrio.Option);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tick"></param>
        private async void ManageTrades(Tick tick)
        {
            DateTime currentTime = tick.Timestamp.Value;
            try
            {
                lock (TimeCandles)
                {
                    //Step 1: Check the base instrument price to load options
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    uint token = tick.InstrumentToken;

                    //Step 2: Load all potentional call/put options for trade. Loading 4+- options
                    LoadOptionsToTrade(currentTime);

                    UpdateInstrumentSubscription(currentTime);

                    if (token != _baseInstrumentToken && tick.LastTradeTime.HasValue)
                    {
                        //Step 3: Create/Update Candles
                        MonitorCandles(tick);

                        //Step 4: If a trade is on, keep updating LTP
                        UpdateTradedOptionPrice(tick);

                        if (!_RSILoaded.Contains(token))
                        {
                            LoadHistoricalRSIs(tick.Timestamp.Value);
                        }
                        else
                        {
                            if (!tokenRSI.ContainsKey(token))
                            {
                                return;
                            }
                            tokenRSI[token].Process(tick.LastPrice, isFinal: false);

                            if (_activeOptions.Any(x => x.InstrumentToken == token))
                            {
                                Instrument option = _activeOptions.First(x => x.InstrumentToken == token);

                                if (_activeOrderTrio == null || _activeOrderTrio.Option.InstrumentToken != token)
                                {
                                    TradeEntry(token, tick.LastPrice, option, currentTime);
                                }
                                else
                                {
                                    TrailMarket(currentTime, tick.LastPrice);
                                }

                                //Put a hedge at 3:15 PM
                                TriggerEODPositionClose(tick.LastTradeTime);
                            }
                        }
                    }
                }
                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
            }

        }

        private void TriggerEODPositionClose(DateTime? currentTime)
        {
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 15, 00))
            {
                if (_activeOrderTrio != null && _activeOrderTrio.Option != null && !_nwSet)
                {
                    bool isOptionCall = _activeOrderTrio.Option.InstrumentType.Trim(' ').ToLower() == "ce";

                    _nwSet = true;
                    decimal nwStrike = isOptionCall ? _activeOrderTrio.Option.Strike + 500 : _activeOrderTrio.Option.Strike - 500;
                    Instrument nwOption = isOptionCall? OptionUniverse[(int)InstrumentType.CE][nwStrike] 
                        : OptionUniverse[(int)InstrumentType.PE][nwStrike];
                    
                    if (nwOption == null)
                    {
                        DataLogic dl = new DataLogic();
                        nwOption = dl.GetInstrument(_activeOrderTrio.Option.Expiry.Value, _baseInstrumentToken,
                            nwStrike, _activeOrderTrio.Option.InstrumentType.Trim(' '));
                    }
                    
                    //ENTRY ORDER - Buy orders at 500 points discuss ALERT
                    Order order = MarketOrders.PlaceOrder(_algoInstance, nwOption.TradingSymbol, nwOption.InstrumentType, 0,
                        nwOption.InstrumentToken, true, _tradeQty * Convert.ToInt32(nwOption.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, "NW");

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       string.Format("TRADE!! Placed night watchman. {0} lots of {1} @ {2}", _tradeQty,
                       nwOption.TradingSymbol, order.Tradingsymbol, order.AveragePrice), "TradeEODEntry");

                    //Save order ID to DB
                    OnTradeEntry(order);
                }
            }
        }

        ///Steps:
        ///Step 1: Determine position at fixed distance from index. Take initial position based on RSI < 40
        ///Step 2: Monitor till RSI increases to above 50

        private async void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
                var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

                if (_activeOptions.Count > 1)
                {
                    Instrument ce = _activeOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "ce");
                    Instrument pe = _activeOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

                    if (
                       ((ce.Strike >= _baseInstrumentPrice + _minDistanceFromBInstrument && ce.Strike <= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                           || (_activeOrderTrio != null && _activeOrderTrio.Option != null && _activeOrderTrio.Option.InstrumentToken == ce.InstrumentToken))
                       && ((pe.Strike <= _baseInstrumentPrice - _minDistanceFromBInstrument && pe.Strike >= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                           || (_activeOrderTrio != null && _activeOrderTrio.Option != null && _activeOrderTrio.Option.InstrumentToken == pe.InstrumentToken))
                       )
                    {
                        return;
                    }

                }
                DataLogic dl = new DataLogic();

                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

                var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
                var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);


                //First time.
                if (_activeOptions.Count == 0)
                {
                    _activeOptions.Add(activeCE.Value);
                    _activeOptions.Add(activePE.Value);
                }
                //Already loaded from last run
                else if (_activeOptions.Count == 1)
                {
                    _activeOptions.Add(_activeOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value);
                }
                else
                {
                    for (int i = 0; i < _activeOptions.Count; i++)
                    {
                        Instrument option = _activeOptions[i];
                        bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                        if (isOptionCall && _activeOrderTrio == null)
                        {
                            _activeOptions[i] = activeCE.Value;
                        }
                        if (!isOptionCall && _activeOrderTrio == null)
                        {
                            _activeOptions[i] = activePE.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trade Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
            }
        }
        
        private void UpdateTradedOptionPrice(Tick tick)
        {
            if (_activeOrderTrio != null && _activeOrderTrio.Option != null && _activeOrderTrio.Option.InstrumentToken == tick.InstrumentToken)
            {
                _activeOrderTrio.Option.LastPrice = tick.LastPrice;
            }
            if(_activeOptions != null)
            {
                foreach(Instrument option in _activeOptions)
                {
                    if (option.InstrumentToken == tick.InstrumentToken)
                    {
                        option.LastPrice = tick.LastPrice;
                    }
                }
            }
        }
        private async void TrailMarket(DateTime currentTime, decimal lastPrice)
        {
            if (_activeOrderTrio != null && _activeOrderTrio.Option != null)
            {
                Instrument option = _activeOrderTrio.Option;

                var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
                var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);
                decimal rsi;

                bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";

                if (isOptionCall && option.Strike > _baseInstrumentPrice + _maxDistanceFromBInstrument
                    && CheckRSIForTrailing(activeCE.Value, activePE.Value, currentTime, out rsi))
                {
                    PlaceTrailingOrder(option, activeCE.Value, lastPrice, currentTime, _activeOrderTrio);
                    _activeOrderTrio.EntryRSI = rsi;
                    _activeOrderTrio.EntryTradeTime = currentTime;

                    _activeOptions.Remove(option);
                    _activeOptions.Add(activeCE.Value);
                    //return newOrderTrio;
                }
                else if (!isOptionCall && option.Strike < _baseInstrumentPrice - _maxDistanceFromBInstrument
                    && CheckRSIForTrailing(activeCE.Value, activePE.Value, currentTime, out rsi))
                {
                    PlaceTrailingOrder(option, activePE.Value, lastPrice, currentTime, _activeOrderTrio);
                    _activeOrderTrio.EntryRSI = rsi;
                    _activeOrderTrio.EntryTradeTime = currentTime;
                    _activeOptions.Remove(option);
                    _activeOptions.Add(activePE.Value);
                    //return newOrderTrio;
                }
            }
        }
        private void PlaceTrailingOrder(Instrument option, 
            Instrument newInstrument, decimal lastPrice, DateTime currentTime, OrderTrio orderTrio)
        {
            try
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                       "Trailing market...", "PlaceTrailingOrder");

                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType,
                    lastPrice, option.InstrumentToken, true,
                    _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Closed Option {0}. Bought {1} lots @ {2}.", option.TradingSymbol,
                        _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                OnTradeExit(order);

                Order slorder = orderTrio.SLOrder;
                if (slorder != null)
                {
                   slorder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slorder, currentTime).Result;
                    OnTradeExit(slorder);
                }

                lastPrice = TimeCandles.ContainsKey(newInstrument.InstrumentToken) ? TimeCandles[newInstrument.InstrumentToken].Last().ClosePrice : newInstrument.LastPrice;
                order = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, lastPrice, // newInstrument.LastPrice,
                    newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       string.Format("Traded Option {0}. Sold {1} lots @ {2}.", newInstrument.TradingSymbol,
                       _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                slorder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, Math.Round(order.AveragePrice * 5 * 20) / 20,
                   newInstrument.InstrumentToken, true, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                   algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                OnTradeEntry(order);
                OnTradeEntry(slorder);

                orderTrio.Option = newInstrument;
                orderTrio.Order = order;
                orderTrio.SLOrder = slorder;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "PlaceTrailingOrder");
                Thread.Sleep(100);
            }
        }



        /// <summary>
        /// Check for RSI crossover
        /// </summary>
        /// <param name="previousCandle"></param>
        /// <param name="option"></param>
        private bool TradeEntry(uint token, decimal lastPrice, Instrument option, DateTime currentTime)
        {
            try
            {
                Instrument opposite = _activeOptions.FirstOrDefault(x => x.InstrumentToken != option.InstrumentToken);
                decimal entryRSI = 0;
                
                if (CheckRSICross(option, opposite, currentTime, out entryRSI))
                {
                    //ENTRY ORDER - Sell ALERT
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                        token, false, _tradeQty * Convert.ToInt32(option.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                    //SL for first orders
                    Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, Math.Round(order.AveragePrice * 5 * 20) / 20,
                        token, true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       string.Format("TRADE!! Sold {0} lots of {1} @ {2}. Stop Loss @ {3}", _tradeQty,
                       option.TradingSymbol, order.AveragePrice, slOrder.AveragePrice), "TradeEntry");

                    OrderTrio orderTrio = new OrderTrio();
                    orderTrio.Order = order;
                    orderTrio.SLOrder = slOrder;
                    orderTrio.Option = option;
                    orderTrio.EntryRSI = entryRSI;
                    orderTrio.EntryTradeTime = currentTime;

                    OnTradeEntry(order);
                    OnTradeEntry(slOrder);

                    //Close the other side option if it is present in active orders
                    if(_activeOrderTrio != null && _activeOrderTrio.Option.InstrumentToken == opposite.InstrumentToken)
                    {
                        //ENTRY ORDER - Sell ALERT
                        Order oppOrder = MarketOrders.PlaceOrder(_algoInstance, opposite.TradingSymbol, opposite.InstrumentType, opposite.LastPrice,
                            token, true, _tradeQty * Convert.ToInt32(opposite.LotSize),
                            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                        OnTradeExit(oppOrder);

                        Order oppSlOrder = _activeOrderTrio.SLOrder;
                        if (oppSlOrder != null)
                        {
                            //Cancel the existing SL order.
                            oppSlOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, oppSlOrder, currentTime).Result;
                            OnTradeExit(oppSlOrder);
                        }

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                           string.Format("TRADE!! Bought back {0} lots of {1} @ {2} and Cancelled Stop loss order", _tradeQty,
                           option.TradingSymbol, order.AveragePrice), "TradeEntry");

                        _activeOrderTrio = null;
                    }
                    _activeOrderTrio = orderTrio;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
            }
            return false;
        }
        private bool CheckRSICross(Instrument current, Instrument opposite, 
            DateTime currentTime, out decimal entryRsi)
        {
            uint t1 = current.InstrumentToken;
            uint t2 = opposite.InstrumentToken;
            entryRsi = 0;
            
            if (!tokenRSI.ContainsKey(opposite.InstrumentToken))
            {
                return false;
            }
            if (tokenRSI[t1].IsFormed && tokenRSI[t2].IsFormed 
                && tokenRSI[t1].GetValue<Decimal>(0) < tokenRSI[t2].GetValue<Decimal>(0) 
                && tokenRSI[t1].GetValue<Decimal>(1) > tokenRSI[t2].GetValue<Decimal>(1)
                && (_activeOrderTrio == null 
                || (currentTime -  _activeOrderTrio.EntryTradeTime).TotalMinutes > 15 
                || tokenRSI[t2].GetValue<Decimal>(0) - tokenRSI[t1].GetValue<Decimal>(0) >= _rsiBandForExit))
            {
                entryRsi = tokenRSI[t1].GetValue<Decimal>(0);
                return true;
            }
            return false;
        }

        private bool CheckRSIForTrailing(Instrument current, Instrument opposite, 
            DateTime currentTime, out decimal entryRsi)
        {
            uint t1 = current.InstrumentToken;
            uint t2 = opposite.InstrumentToken;
            entryRsi = 0;

            if (!tokenRSI.ContainsKey(opposite.InstrumentToken) || !tokenRSI.ContainsKey(current.InstrumentToken))
            {
                return false;
            }
            
            if (tokenRSI[t1].IsFormed && tokenRSI[t2].IsFormed
                && tokenRSI[t1].GetValue<Decimal>(0) < tokenRSI[t2].GetValue<Decimal>(0))
            {
                entryRsi = tokenRSI[t1].GetValue<Decimal>(0);
                return true;
            }
            return false;
        }

        //private bool CheckRSIForEntry(uint ceToken, uint peToken, bool isOptionCall, DateTime currentTime, out decimal entryRsi)
        //{
        //    entryRsi = 0;
        //    try
        //    {
        //        decimal ce_rsi = tokenRSI[ceToken].GetCurrentValue<decimal>();
        //        decimal pe_rsi = tokenRSI[peToken].GetCurrentValue<decimal>();

        //        if (tokenRSI[ceToken].IsFormed && tokenRSI[peToken].IsFormed && isOptionCall && ce_rsi < pe_rsi)
        //        {
        //            entryRsi = ce_rsi;
        //            return true;
        //        }
        //        else if (tokenRSI[ceToken].IsFormed && tokenRSI[peToken].IsFormed && !isOptionCall && pe_rsi < ce_rsi)
        //        {
        //            entryRsi = pe_rsi;
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message);
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Trading Stopped");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //        return false;
        //    }
        //}


        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        private async void MonitorCandles(Tick tick)
        {
            try
            {
                uint token = tick.InstrumentToken;
                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built
                if (TimeCandles.ContainsKey(token))
                {
                    candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(tick.Timestamp.Value, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tick.Timestamp.Value, "Starting first Candle now", "MonitorCandles");
                        //candle starts from there
                        candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message);
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trade Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.Value,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }


        private async void LoadHistoricalRSIs(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_RSILoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!tokenRSI.ContainsKey(t) && !_SQLLoading.Contains(t))
                    {
                        sb.AppendFormat("{0},", t);
                        _SQLLoading.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //_SQLLoading.Add(token);
                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, 15, lastCandleEndTime));
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && tokenRSI.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            tokenRSI[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                tokenRSI[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && tokenRSI.ContainsKey(tkn))
                    {
                        _RSILoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("RSI loaded from DB for {0}", tkn), "MonitorCandles");
                    }
                    //}
                    // }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalRSIs");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            DateTime? candleStartTime = null;
            lastEndTime = DateTime.Now;
            try
            {
                double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
                //if(mselapsed < 1000) //less than a second
                //{
                //    candleStartTime =  currentTime;
                //}
                if (mselapsed < 60 * 1000)
                {
                    candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
                }
                //else
                //{
                lastEndTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
                //}
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckCandleStartTime");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }

            return candleStartTime;
        }

        #region Historical Candle 
        private void LoadHistoricalCandles(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
           
                DataLogic dl = new DataLogic();

                Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);
                RelativeStrengthIndex rsi;

            lock (tokenRSI)
            {
                foreach (uint t in historicalCandlePrices.Keys)
                {
                    rsi = new RelativeStrengthIndex();
                    foreach (var price in historicalCandlePrices[t])
                    {
                        rsi.Process(price, isFinal: true);
                    }
                    tokenRSI.Add(t, rsi);
                }
            }
        }
        #endregion

        //private void LoadHistoricalCandles(DateTime? currentTime, uint token)
        //{
        //    DataLogic dl = new DataLogic();

        //    CandleSeries candleSeries = new CandleSeries();

        //    //This time should be current time so that all historical candles can be retrieved
        //    DateTime ydayEndTime = DateTime.Now;

        //    //TEST: Remove on live ticks
        //    ydayEndTime = currentTime.Value;
        //    //ydayEndTime = currentTime.GetValueOrDefault(DateTime.Now);

        //    RelativeStrengthIndex rsi = null;

        //    //foreach (uint token in _activeOptions.Select(x => x.InstrumentToken))
        //    //{
        //    List<Candle> historicalCandles = candleSeries.LoadCandles(RSI_LENGTH, CandleType.Time, ydayEndTime, token, _candleTimeSpan);

        //    TimeCandles.Add(token, historicalCandles);

        //    rsi = new RelativeStrengthIndex();

        //    foreach (var candle in historicalCandles)
        //    {
        //        rsi.Process(candle.ClosePrice, isFinal: true);
        //    }
        //    tokenRSI.Add(token, rsi);
        //    //}
        //}
        //private void LoadHistoricalCandles(DateTime? currentTime)
        //{
        //    DataLogic dl = new DataLogic();

        //    CandleSeries candleSeries = new CandleSeries();

        //    //This time should be current time so that all historical candles can be retrieved
        //    DateTime ydayEndTime = DateTime.Now;

        //    //TEST: Remove on live ticks
        //    ydayEndTime = currentTime.Value;
        //    //ydayEndTime = currentTime.GetValueOrDefault(DateTime.Now);

        //    RelativeStrengthIndex rsi = null;

        //    foreach (uint token in _activeOptions.Select(x => x.InstrumentToken))
        //    {
        //        List<Candle> historicalCandles = candleSeries.LoadCandles(RSI_LENGTH, CandleType.Time, ydayEndTime, token, _candleTimeSpan);

        //        TimeCandles.Add(token, historicalCandles);

        //        rsi = new RelativeStrengthIndex();

        //        foreach (var candle in historicalCandles)
        //        {
        //            rsi.Process(candle.ClosePrice, isFinal: true);
        //        }
        //        tokenRSI.Add(token, rsi);
        //    }
        //}
        private bool GetBaseInstrumentPrice(Tick tick)
        {
            //Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _baseInstrumentToken);

            if (tick.InstrumentToken == _baseInstrumentToken && tick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                _baseInstrumentPrice = tick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
        //private DateTime? StartCandleStreaming(DateTime currentTime)
        //{
        //    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
        //    DateTime? candleStartTime = null;
        //    if (mselapsed < 1000) //less than a second
        //    {
        //        candleStartTime = currentTime;
        //    }
        //    else if (mselapsed < 60 * 1000)
        //    {
        //        candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
        //    }

        //    return candleStartTime;
        //}
        private async void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                //foreach (var option in CandleOptions)
                foreach (var optionList in OptionUniverse)
                    foreach (var option in optionList)
                    {
                        if (!SubscriptionTokens.Contains(option.Value.InstrumentToken))
                        {
                            SubscriptionTokens.Add(option.Value.InstrumentToken);
                            dataUpdated = true;
                        }
                    }
                if (dataUpdated)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, 
                        currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (tokenRSI.ContainsKey(e.InstrumentToken))
                {
                    tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                }
                

                ///putting trade entry at candle close only

                
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CandleManger_TimeCandleFinished");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        //private ShortTrade PlaceOrder(string instrument_tradingsymbol, string instrumenttype, decimal instrument_currentPrice, uint instrument_Token,
        //   bool buyOrder, int quantity, DateTime? tickTime = null)
        //{
        //    string tradingSymbol = instrument_tradingsymbol;
        //    decimal currentPrice = instrument_currentPrice;
        //    //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
        //    //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
        //    //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

        //    ///TEMP, REMOVE Later
        //    if (currentPrice == 0)
        //    {
        //        DataLogic dl = new DataLogic();
        //        currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
        //    }

        //    string orderId = "0";
        //    decimal averagePrice = 0;
        //    //if (orderStatus["data"]["order_id"] != null)
        //    //{
        //    //    orderId = orderStatus["data"]["order_id"];
        //    //}
        //    if (orderId != "0")
        //    {
        //        System.Threading.Thread.Sleep(200);
        //        List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
        //        averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
        //    }
        //    if (averagePrice == 0)
        //        averagePrice = buyOrder ? currentPrice : currentPrice;
        //    // averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

        //    ShortTrade trade = new ShortTrade();
        //    trade.InstrumentToken = instrument_Token;
        //    trade.AveragePrice = averagePrice;
        //    trade.ExchangeTimestamp = tickTime;// DateTime.Now;
        //    trade.TradeTime = tickTime ?? DateTime.Now;
        //    trade.Quantity = quantity;
        //    trade.OrderId = orderId;
        //    trade.TransactionType = buyOrder ? "Buy" : "Sell";
        //    trade.TriggerID = Convert.ToInt32(AlgoIndex.VolumeThreshold);
        //    trade.TradingStatus = buyOrder ? TradeStatus.Closed : TradeStatus.Open;
        //    trade.InstrumentType = instrumenttype;

        //    UpdateTradeDetails(strategyID: 0, instrument_Token, quantity, trade, Convert.ToInt32(trade.TriggerID));

        //    return trade;
        //}
        //private void UpdateTradeDetails(int strategyID, uint instrumentToken, int tradedLot, ShortTrade trade, int triggerID)
        //{
        //    DataLogic dl = new DataLogic();
        //    dl.UpdateTrade(strategyID, instrumentToken, trade, AlgoIndex.ExpiryTrade, tradedLot, triggerID);
        //}

        private void UpdateTokenSubcription()
        {
            //ZMQClient.ZMQSubcribebyToken(this, SubscriptionTokens.ToArray());
        }
        public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);
            return nodeData;
        }

        public Task<bool> OnNext(Tick[] ticks)
        {
           
            //return true;

            try
            {
                if (_stopTrade || !ticks[0].Timestamp.HasValue)
                {
                    return Task.FromResult(false);
                }
                ManageTrades(ticks[0]);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, ticks[0].Timestamp.GetValueOrDefault(DateTime.UtcNow), 
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                //Environment.Exit(0);
                return Task.FromResult(false);
            }
        }

        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
            }
        }

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }


        public virtual void OnCompleted()
        {
        }
    }
}
