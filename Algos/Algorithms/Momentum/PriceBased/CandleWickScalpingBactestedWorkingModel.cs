﻿using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using GlobalLayer;
using GlobalCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BrokerConnectWrapper;
using ZMQFacade;
using KafkaFacade;
using System.Timers;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using Algos.Utilities.Views;
using FirebaseAdmin.Messaging;

namespace Algorithms.Algorithms
{
    public class CandleWickScalping : IZMQ, IObserver<Tick>, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(CandleWickScalping source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnCriticalEventsHandler(string title, string body);
        [field: NonSerialized]
        public event OnCriticalEventsHandler OnCriticalEvents;


        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;

        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(10);

        private List<OrderTrio> _orderTrios;
        private List<OrderTrio> _orderTriosFromEMATrades;
        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private Instrument _activeFuture;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);

        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private const decimal SCH_UPPER_THRESHOLD = 70;
        private const decimal SCH_LOWER_THRESHOLD = 30;
        //private const decimal CANDLE_WICK_SIZE = 45;//10000;//45;//35
        //private const decimal CANDLE_BODY_MIN = 5;
        //private const decimal CANDLE_BODY = 40;//10000;//40; //25
        //private const decimal CANDLE_BODY_BIG = 35;
        //private const decimal EMA_ENTRY_RANGE = 35;
        //private const decimal RISK_REWARD = 2.3M;
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        //#if market
        //        private readonly decimal _previousDayHigh;
        //        private readonly decimal _previousDayLow;
        //        private readonly decimal _previousDayBodyLow;
        //        private readonly decimal _previousDayBodyHigh;
        //        private readonly decimal _previousDayClose;
        //        private readonly decimal _previousDayOpen;
        //        private readonly decimal _previousSwingLow;
        //        private readonly decimal _previousSwingHigh;
        //        private readonly decimal _currentWeekHigh;
        //        private readonly decimal _currentWeekLow;
        //        private readonly decimal _previousWeekHigh;
        //        private readonly decimal _previousWeekLow;
        //        private readonly decimal _previousWeekClose;
        //#elif local
        //        private decimal _previousDayHigh;
        //        private decimal _previousDayLow;
        //        private decimal _previousDayBodyLow;
        //        private decimal _previousDayBodyHigh;
        //        private decimal _previousDayClose;
        //        private decimal _previousDayOpen;
        //        private decimal _previousSwingLow;
        //        private decimal _previousSwingHigh;
        //        private decimal _currentWeekHigh;
        //        private decimal _currentWeekLow;
        //        private decimal _previousWeekHigh;
        //        private decimal _previousWeekLow;
        //        private decimal _previousWeekClose;
        //        private DateTime _currentDate;
        //      //  private Dictionary<DateTime, PriceActionInput> _priceActions;
        //#endif
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private Candle _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.SchScalping;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private bool? _firstCandleOutsideRange;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        private IHttpClientFactory _httpClientFactory;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        FirebaseMessaging _firebaseMessaging;
        
        StochasticOscillator _indexSch;

        public struct PriceRange
        {
            public decimal Upper;
            public decimal Lower;
            public DateTime? CrossingTime;
        }
        private const decimal QUALIFICATION_ZONE_THRESHOLD = 15;
        private const decimal TRADING_ZONE_THRESHOLD = 8; // This should be % of qualification zone. 50% is good.
        private const decimal RR_BREAKDOWN = 1; // Risk Reward
        private const decimal RR_BREAKOUT = 2; // Risk Reward
        private decimal _pnl = 0;

        private PriceRange _resistancePriceRange;
        private PriceRange _supportPriceRange;

        bool _indexSchLoaded = false;
        bool _indexSchLoading = false;
        bool _indexSchLoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;

        public CandleWickScalping(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime? expiry, int quantity, decimal targetProfit, decimal stopLoss,
            //OHLC previousDayOHLC, OHLC previousWeekOHLC, decimal previousDayBodyHigh, 
            //decimal previousDayBodyLow, decimal previousSwingHigh, decimal previousSwingLow,
            //decimal previousWeekLow, decimal previousWeekHigh, 
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            ZConnect.Login();
            KoConnect.Login();

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;

            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _trailingStopLoss = _stopLoss = stopLoss;
            _targetProfit = targetProfit;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            SetUpInitialData(expiry, algoInstance);


            //#if local
            //            _dateLoaded = new List<DateTime>();
            //            LoadPAInputsForTest();
            //#endif

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }
        private void SetUpInitialData(DateTime? expiry, int algoInstance = 0)
        {
            _expiryDate = expiry;
            _orderTrios = new List<OrderTrio>();
            _orderTriosFromEMATrades = new List<OrderTrio>();
            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();

            _indexSch = new StochasticOscillator();

            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 7);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 8);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 9);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 10);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 11);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 12);

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

#if !BACKTEST
            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
#endif
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken) ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    LoadFutureToTrade(currentTime);


                    //if (_baseInstrumentToken == tick.InstrumentToken)
                    //{
                    //    if (!_indexSchLoaded)
                    //    {
                    //        LoadBInstrumentSch(token, 20, currentTime);
                    //    }
                    //    else
                    //    {
                    //        // _indexEMAValue = _indexSch.Process(tick.LastPrice, isFinal: false);
                    //    }
                    //}
#if local

                    // SetParameters(currentTime);
#endif
                    if (_activeFuture.InstrumentToken == token)
                    {
                        //var message = new Message()
                        //{
                        //    Data = new Dictionary<string, string>() { { "Nifty", "17600" }, },
                        //    Topic = Constants.MARKET_ALERTS,
                        //    Notification = new Notification() { Title = "Index Alerts", Body = "Price Action Algo Started!" }
                        //};
                        //_firebaseMessaging.SendAsync(message);

                        UpdateFuturePrice(tick.LastPrice);
                        TradeTPSL(currentTime, tp: true, sl:false);
                    }
                    UpdateInstrumentSubscription(currentTime);
                    //LoadCriticalValues(currentTime);
                    MonitorCandles(tick, currentTime);
                }

                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
            }
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            //TEMP COMMENTED 
            //if(e.InstrumentToken == _activeFuture.InstrumentToken)
            //decimal candlelowbody, candlehighbody;
            if (e.InstrumentToken == _baseInstrumentToken)
            {
                //Resitance and support change as price crosses it.
                if (_resistancePriceRange.Upper !=0 && _resistancePriceRange.Upper < e.ClosePrice)
                {
                    if (_resistancePriceRange.CrossingTime == null)
                    {
                        _resistancePriceRange.CrossingTime = e.CloseTime;
                    }
                    else
                    {
                        if ((_resistancePriceRange.CrossingTime - e.CloseTime).Value.TotalMinutes > 15)  // 5 candles
                        {
                            _supportPriceRange = _resistancePriceRange;
                        }
                    }
                }
                if (_supportPriceRange.Lower != 0 && _supportPriceRange.Lower > e.ClosePrice)
                {
                    if (_supportPriceRange.CrossingTime == null)
                    {
                        _supportPriceRange.CrossingTime = e.CloseTime;
                    }
                    else
                    {
                        if ((_supportPriceRange.CrossingTime - e.CloseTime).Value.TotalMinutes > 15)  // 5 candles
                        {
                            _resistancePriceRange = _supportPriceRange;
                        }
                    }
                }

                decimal lowerWick = Math.Min(e.ClosePrice, e.OpenPrice) - e.LowPrice;
                decimal upperWick = e.HighPrice - Math.Max(e.ClosePrice, e.OpenPrice);

                //Step 1: Is Wick Qualified
                //Step 2: Take trade within trading zone.

                if (IsWickQualified(upperWick))
                {
                    //Resistance Zone
                    _resistancePriceRange.Upper = e.HighPrice;
                    _resistancePriceRange.Lower = Math.Max(e.ClosePrice, e.OpenPrice);
                }
                else
                {
                    //Check for shorts within Trading Zone

                    if (_resistancePriceRange.Upper != 0
                        && e.ClosePrice < _resistancePriceRange.Upper
                        && _resistancePriceRange.Upper - e.ClosePrice < TRADING_ZONE_THRESHOLD)
                    {
                        //breakdown
                        TakeTrade(buy: false, e.CloseTime, RR_BREAKDOWN * (_resistancePriceRange.Upper - e.ClosePrice), _resistancePriceRange.Upper);
                    }
                    else if (_resistancePriceRange.Upper != 0 
                        && e.ClosePrice > _resistancePriceRange.Upper
                        && e.ClosePrice  - _resistancePriceRange.Upper < TRADING_ZONE_THRESHOLD)
                    {
                        //breakout
                        TakeTrade(buy: true, e.CloseTime, RR_BREAKOUT * (e.ClosePrice - _resistancePriceRange.Upper), _resistancePriceRange.Upper);
                    }
                }
                if (IsWickQualified(lowerWick))
                {
                    //Support Zone
                    _supportPriceRange.Upper = Math.Min(e.ClosePrice, e.OpenPrice);
                    _supportPriceRange.Lower = e.LowPrice;
                }
                else
                {
                    //Check for shorts within Trading Zone

                    if (_supportPriceRange.Lower != 0 
                        && e.ClosePrice > _supportPriceRange.Lower
                        && e.ClosePrice - _supportPriceRange.Lower < TRADING_ZONE_THRESHOLD)
                    {
                        TakeTrade(buy: true, e.CloseTime, RR_BREAKDOWN * (e.ClosePrice - _supportPriceRange.Lower), _supportPriceRange.Lower);
                    }
                    else if (_supportPriceRange.Lower != 0 
                        && e.ClosePrice < _supportPriceRange.Lower
                        && _supportPriceRange.Lower - e.ClosePrice < TRADING_ZONE_THRESHOLD)
                    {
                        //breakout
                        TakeTrade(buy: false, e.CloseTime, RR_BREAKOUT * (_supportPriceRange.Lower - e.ClosePrice), _supportPriceRange.Lower);
                    }

                }

                TradeTPSL(e.CloseTime, tp:true, sl:true);

                //Closes all postions at 3:20 PM
                TriggerEODPositionClose(e.CloseTime);
            }
        }
        private bool IsWickQualified(decimal wick)
        {
            return wick > QUALIFICATION_ZONE_THRESHOLD;
        }
        void UpdateFuturePrice(decimal lastPrice)
        {
            _activeFuture.LastPrice = lastPrice;
        }
        private void TakeTrade(bool buy, DateTime currentTime, decimal targetLevel, decimal stopLevel)
        {
            int qty;
            if (_orderTrios.Count > 0)
            {
                if (_orderTrios[0].Order.TransactionType.ToLower() == (buy ? "buy" : "sell"))
                {
                    qty = 0;
                }
                else
                {
                    qty = _tradeQty * 2;
                    _pnl += (_orderTrios.ElementAt(0).Order.AveragePrice - _activeFuture.LastPrice) * (buy ? 1 : -1);
                    _orderTrios.RemoveAt(0);
                }
            }
            else
            {
                qty = _tradeQty;
            }

            if (qty > 0)
            {
                OrderTrio orderTrio = new OrderTrio();
                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice, //e.ClosePrice,
                   _activeFuture.KToken, buy, qty, algoIndex, currentTime, Tag: "Algo4",
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                

                orderTrio.StopLoss = stopLevel;
                orderTrio.TargetProfit = orderTrio.Order.AveragePrice + (buy ? 1 : -1) * targetLevel;
                //orderTrio.TPFlag = false;
                ////_activeFuture.InstrumentToken = _baseInstrumentToken;
                orderTrio.Option = _activeFuture;
                _orderTrios.Add(orderTrio);
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", buy ? "Bought " : "Sold", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
            }
        }
        private decimal CloseTrade(bool buy, DateTime currentTime)
        {
            int qty;
            if (_orderTrios.Count > 0)
            {
                OrderTrio orderTrio = new OrderTrio();
                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice, //e.ClosePrice,
                   _activeFuture.KToken, buy, _tradeQty, algoIndex, currentTime, Tag: "Algo4",
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


                //orderTrio.TPFlag = false;
                ////_activeFuture.InstrumentToken = _baseInstrumentToken;
                orderTrio.Option = _activeFuture;
                _orderTrios.Remove(orderTrio);
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", buy ? "Bought " : "Sold", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                return orderTrio.Order.AveragePrice;
            }
            return 0;
        }

        private void TradeTPSL(DateTime currentTime, bool tp, bool sl)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    decimal tradePrice = 0;
                    if ((orderTrio.Order.TransactionType == "buy") && (((_baseInstrumentPrice > orderTrio.TargetProfit) && tp) || ((_baseInstrumentPrice < orderTrio.StopLoss) && sl)))
                    {
                        tradePrice = CloseTrade(buy: false, currentTime);
                        _pnl += (tradePrice - orderTrio.Order.AveragePrice);
                        _orderTrios.Remove(orderTrio);

#if !BACKTEST
                    OnTradeEntry(orderTrio.Order);
                    OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Closed", Math.Round(tradePrice, 2)));
#endif
                    }
                    else if (orderTrio.Order.TransactionType == "sell" && (((_baseInstrumentPrice < orderTrio.TargetProfit) && tp) || ((_baseInstrumentPrice > orderTrio.StopLoss) && sl)))
                    {
                        tradePrice = CloseTrade(buy: true, currentTime);
                        _pnl += (orderTrio.Order.AveragePrice - tradePrice);
                        _orderTrios.Remove(orderTrio);

#if !BACKTEST
                    OnTradeEntry(orderTrio.Order);
                    OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Closed", Math.Round(tradePrice, 2)));
#endif
                    }


                }
            }
        }
        //void SetTargetHitFlag(uint token, decimal lastPrice)
        //{
        //    if (_orderTrios.Count > 0)
        //    {
        //        for (int i = 0; i < _orderTrios.Count; i++)
        //        {
        //            var orderTrio = _orderTrios[i];
        //            if (orderTrio.Option.InstrumentToken == token)
        //            {
        //                if ((orderTrio.Order.TransactionType == "buy") && (_baseInstrumentPrice > orderTrio.TargetProfit))
        //                {
        //                    orderTrio.TPFlag = true;
        //                }
        //                else if (orderTrio.Order.TransactionType == "sell" && (_baseInstrumentPrice < orderTrio.TargetProfit))
        //                {
        //                    orderTrio.TPFlag = true;
        //                }
        //            }
        //        }
        //    }
        //}


        //private void LoadCriticalLevels(uint token, DateTime currentTime)
        //{
        //    try
        //    {

        //        DataLogic dl = new DataLogic();
        //        DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
        //        List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
        //        List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

        //        //OHLC pdOHLC = new OHLC() { Close = pdOHLCList.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

        //        OHLC pdOHLC = new OHLC() { Close = pdOHLCDay.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

        //        _cpr = new CentralPivotRange(pdOHLC);

        //        _previousDayOHLC = pdOHLC;
        //        _previousDayOHLC.Close = pdOHLCList.Last().Close;

        //        List<Historical> pwOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
        //        _previousWeekOHLC = new OHLC(pwOHLCList.First(), token);
        //        _weeklycpr = new CentralPivotRange(_previousWeekOHLC);

        //        //load Previous Day and previous week data

        //        _criticalLevels.TryAdd(_previousDayOHLC.Close, 0);
        //        _criticalLevels.TryAdd(_previousDayOHLC.High, 1);
        //        _criticalLevels.TryAdd(_previousDayOHLC.Low, 2);
        //        _criticalLevels.TryAdd(_previousDayOHLC.Open, 3);
        //        //_criticalLevels.TryAdd(_previousSwingHigh, 4);
        //        //_criticalLevels.TryAdd(_previousSwingLow, 5);
        //        // _criticalLevels.TryAdd(_previousDayBodyHigh, 0);
        //        //_criticalLevels.TryAdd(_previousDayBodyLow, 0);

        //        _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.CPR], 6);
        //        _criticalLevels.Remove(0);

        //        _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.Close, 0);
        //        _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.High, 1);
        //        _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.Low, 2);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.CPR], 3);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R1], 4);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R2], 5);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R3], 6);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S3], 7);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S2], 8);
        //        _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S1], 9);
        //        _criticalLevelsWeekly.TryAdd(_previousDayOHLC.High, 10);
        //        _criticalLevelsWeekly.TryAdd(_previousDayOHLC.Low, 11);
        //        _criticalLevelsWeekly.Remove(0);
        //    }
        //    catch (Exception ex)
        //    {

        //    }

        //}


        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00))
            {
                CheckSL(currentTime);
                DataLogic dl = new DataLogic();
                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                _pnl = 0;
                _stopTrade = true;
            }
        }

        private void MonitorCandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (TimeCandles.ContainsKey(token))
                {
                    candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
#endif
                        //candle starts from there
                        candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            try
            {
                DateTime? candleStartTime = null;

                if (currentTime.TimeOfDay < MARKET_START_TIME)
                {
                    candleStartTime = DateTime.Now.Date + MARKET_START_TIME;
                    lastEndTime = candleStartTime.Value;
                }
                else
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

                return candleStartTime;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckCandleStartTime");
                Thread.Sleep(100);
                Environment.Exit(0);
                lastEndTime = DateTime.Now;
                return null;
            }
        }


        private void LoadBInstrumentSch(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_indexSch)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_indexSchLoading)
                    {
                        _indexSchLoading = true;
                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _indexSchLoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _indexSch.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var price in TimeCandles[bToken])
                            {
                                _indexSch.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexSchLoadedFromDB)
                    {
                        _indexSchLoaded = true;
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} EMA loaded from DB for Base Instrument", 20), "LoadBInstrumentSCH");
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
            }
        }
        private void LoadBaseInstrumentEMA(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_indexSch)
                {
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    TimeFrameCandle c;

                    foreach (var price in historicals)
                    {
                        c = new TimeFrameCandle();
                        c.TimeFrame = new TimeSpan(0, _candleTimeSpan.Minutes, 0);
                        c.ClosePrice = price.Close;
                        c.HighPrice = price.High;
                        c.LowPrice = price.Low;
                        c.State = CandleStates.Finished;
                        _indexSch.Process(c);//, isFinal: true);
                    }
                    _indexSchLoadedFromDB = true;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error,
                    lastCandleEndTime, String.Format(@"Error occurred! Trading has stopped. {0}", ex.Message), "LoadHistoricalCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }

        }
        private void CheckSL(DateTime currentTime)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if (orderTrio.Order.TransactionType == "buy")
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty, algoIndex, currentTime, Tag: "Algo3",
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());


                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType == "sell")
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty, algoIndex, currentTime, Tag: "Algo3",
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }
        //        private void CheckSLForEMABasedOrders(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        //        {
        //            if (_orderTriosFromEMATrades.Count > 0)
        //            {
        //                for (int i = 0; i < _orderTriosFromEMATrades.Count; i++)
        //                {
        //                    var orderTrio = _orderTriosFromEMATrades[i];

        //                    if ((orderTrio.Order.TransactionType == "buy") && (lastPrice < _indexEMAValue.GetValue<decimal>() || closeAll))
        //                    {
        //                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                            _activeFuture.KToken, false, _tradeQty, algoIndex, currentTime, Tag: "Algo2",
        //                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //                        OnTradeEntry(orderTrio.Order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif

        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: _httpClientFactory.CreateClient());


        //                        _orderTriosFromEMATrades.Remove(orderTrio);
        //                        i--;
        //                    }
        //                    else if (orderTrio.Order.TransactionType == "sell" && (lastPrice > _indexEMAValue.GetValue<decimal>() || closeAll))
        //                    {
        //                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                       _activeFuture.KToken, true, _tradeQty, algoIndex, currentTime, Tag: "Algo2",
        //                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //                        OnTradeEntry(orderTrio.Order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif
        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: _httpClientFactory.CreateClient());

        //                        _orderTriosFromEMATrades.Remove(orderTrio);
        //                        i--;
        //                    }
        //                }
        //            }
        //        }

        //        private bool ValidateRiskReward(Candle c, out decimal targetLevel, out decimal stoploss, out bool s2r2level)
        //        {
        //            targetLevel = 0;
        //            stoploss = 0;
        //            s2r2level = false;
        //            bool _favourableRiskReward = false;
        //            if (c.ClosePrice > _cpr.Prices[(int)PivotLevel.LCPR] && c.ClosePrice < _cpr.Prices[(int)PivotLevel.UCPR])
        //            {
        //                // Do not take trade within CPR
        //            }
        //            else //if (ValidateCandleSize(c))
        //            {
        //                if (c.ClosePrice > _criticalLevels.Keys.Max() || c.ClosePrice < _criticalLevels.Keys.Min())
        //                {
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 17);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 18);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 19);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 20);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 21);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 22);
        //                }
        //                else
        //                {
        //                }

        //                //bullish scenario
        //                if (c.ClosePrice > c.OpenPrice)
        //                {
        //                    decimal plevel = 0;
        //                    foreach (var level in _criticalLevels)
        //                    {
        //                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
        //                        {
        //                            if ((level.Key - c.ClosePrice) / (c.ClosePrice - plevel) > 2.3M)
        //                            {
        //                                targetLevel = level.Key;
        //                                stoploss = c.LowPrice;
        //                                _favourableRiskReward = true;
        //                                if (plevel == _cpr.Prices[(int)PivotLevel.S2]
        //                                    //|| plevel == _cpr.Prices[(int)PivotLevel.R2]
        //                                    || plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
        //                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.R2]
        //                                    )
        //                                {
        //                                    s2r2level = true;
        //                                }
        //                            }
        //                            break;
        //                        }
        //                        plevel = level.Key;
        //                    }
        //                }
        //                else
        //                {
        //                    decimal plevel = 0;
        //                    foreach (var level in _criticalLevels)
        //                    {
        //                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
        //                        {
        //                            if (Math.Abs((c.ClosePrice - plevel) / (level.Key - c.ClosePrice)) > 2.3M)
        //                            {
        //                                targetLevel = plevel;
        //                                stoploss = c.HighPrice;
        //                                _favourableRiskReward = true;

        //                                if (//plevel == _cpr.Prices[(int)PivotLevel.S2] ||
        //                                    level.Key == _cpr.Prices[(int)PivotLevel.R2]
        //                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
        //                                    || level.Key == _weeklycpr.Prices[(int)PivotLevel.R2])
        //                                {
        //                                    s2r2level = true;
        //                                }
        //                                break;
        //                            }
        //                        }
        //                        plevel = level.Key;
        //                    }
        //                }
        //            }
        //            return _favourableRiskReward;
        //        }
        //        private bool ValidateCandleSize(Candle c, bool s2r2Level)
        //        {
        //            bool validCandle = false;

        //            if (Math.Abs(c.ClosePrice - c.OpenPrice) < CANDLE_BODY)
        //            {
        //                if (Math.Abs(c.ClosePrice - c.OpenPrice) <= CANDLE_BODY_MIN)
        //                {
        //                    validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? ((c.OpenPrice - c.LowPrice) >= 2.3m * (c.HighPrice - c.OpenPrice)) : ((c.LowPrice - c.OpenPrice) * 2.3m <= (c.HighPrice - c.OpenPrice));
        //                }
        //                else
        //                {
        //                    validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? (((c.OpenPrice - c.LowPrice) >= (c.HighPrice - c.ClosePrice) * 0.9m && ((c.OpenPrice - c.LowPrice) < CANDLE_WICK_SIZE) || (s2r2Level))
        //                        || ((c.ClosePrice - c.OpenPrice) >= 0.6m * (c.HighPrice - c.LowPrice))) :
        //                        (((c.ClosePrice - c.LowPrice) * 0.9m <= (c.HighPrice - c.OpenPrice) && ((c.HighPrice - c.OpenPrice) < CANDLE_WICK_SIZE) || (s2r2Level))
        //                        || ((c.OpenPrice - c.ClosePrice) >= 0.6m * (c.HighPrice - c.LowPrice)));
        //                }
        //            }
        //            return validCandle;
        //        }


        //        private Candle GetLastCandle(uint instrumentToken)
        //        {
        //            if (TimeCandles[instrumentToken].Count < 3)
        //            {
        //                return null;
        //            }
        //            var lastCandles = TimeCandles[instrumentToken].TakeLast(3);
        //            TimeFrameCandle tC = new TimeFrameCandle();
        //            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
        //            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
        //            tC.ClosePrice = lastCandles.ElementAt(2).ClosePrice;
        //            tC.CloseTime = lastCandles.ElementAt(2).CloseTime;
        //            tC.HighPrice = Math.Max(Math.Max(lastCandles.ElementAt(0).HighPrice, lastCandles.ElementAt(1).HighPrice), lastCandles.ElementAt(2).HighPrice);
        //            tC.LowPrice = Math.Min(Math.Min(lastCandles.ElementAt(0).LowPrice, lastCandles.ElementAt(1).LowPrice), lastCandles.ElementAt(2).LowPrice);
        //            return tC;
        //        }
        //        private bool ValidateRiskRewardFromWeeklyPivots(Candle c)
        //        {
        //            bool _favourableRiskReward = false;

        //            //bullish scenario
        //            if (c.ClosePrice > c.OpenPrice)
        //            {
        //                decimal plevel = 0;
        //                foreach (var level in _criticalLevelsWeekly)
        //                {
        //                    if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
        //                    {
        //                        if ((level.Key - c.ClosePrice) / (c.ClosePrice - plevel) > 2.3M)
        //                        {
        //                            _favourableRiskReward = true;
        //                        }
        //                        break;
        //                    }
        //                    plevel = level.Key;
        //                }
        //            }
        //            else
        //            {
        //                decimal plevel = 0;
        //                foreach (var level in _criticalLevelsWeekly)
        //                {
        //                    if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
        //                    {
        //                        if (Math.Abs((c.ClosePrice - plevel) / (level.Key - c.ClosePrice)) > 2.3M)
        //                        {
        //                            _favourableRiskReward = true;
        //                            break;
        //                        }
        //                    }
        //                    plevel = level.Key;
        //                }
        //            }
        //            return _favourableRiskReward;
        //        }

        //        private bool ValidateCandleForEMABaseEntry(Candle c)
        //        {
        //            bool validCandle = false;
        //            if (Math.Abs(c.ClosePrice - c.OpenPrice) < CANDLE_BODY_BIG)
        //            {
        //                validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? (((c.OpenPrice - c.LowPrice) * 0.9m > (c.HighPrice - c.ClosePrice) && (c.OpenPrice - c.LowPrice) < CANDLE_WICK_SIZE) || ((c.ClosePrice - c.OpenPrice) >= 0.6m * (c.HighPrice - c.LowPrice))) :
        //                    (((c.ClosePrice - c.LowPrice) < (c.HighPrice - c.OpenPrice) * 0.9m && (c.HighPrice - c.OpenPrice) < CANDLE_WICK_SIZE) || ((c.OpenPrice - c.ClosePrice) >= 0.6m * (c.HighPrice - c.LowPrice)));
        //            }
        //            if (validCandle)
        //            {
        //                validCandle = (((_indexEMAValue.GetValue<decimal>() - c.ClosePrice < EMA_ENTRY_RANGE) && (_indexEMAValue.GetValue<decimal>() > c.ClosePrice) && c.ClosePrice < _previousDayOHLC.Low && c.ClosePrice < c.OpenPrice) || (
        //                    (c.ClosePrice - _indexEMAValue.GetValue<decimal>() < EMA_ENTRY_RANGE) && (c.ClosePrice > _indexEMAValue.GetValue<decimal>())
        //                    && c.ClosePrice > _previousDayOHLC.High && c.ClosePrice > c.OpenPrice));
        //            }

        //            return validCandle;
        //        }
        //private void LoadCriticalValues(DateTime currentTime)
        //{
        //    if (!_criticalValuesLoaded)
        //    {
        //        _criticalValuesLoaded = true;
        //    }
        //}
        //private void LoadPAInputsForTest()
        //{
        //    DataLogic dl = new DataLogic();
        //    DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.CandleWickScalping, Convert.ToDateTime("2021-11-30"), Convert.ToDateTime("2021-12-30"));

        //    List<PriceActionInput> priceActionInputs = new List<PriceActionInput>();
        //    _priceActions = new Dictionary<DateTime, PriceActionInput>();
        //    for (int i = 0; i < dsPAInputs.Tables[0].Rows.Count; i++)
        //    {
        //        DataRow drPAInputs = dsPAInputs.Tables[0].Rows[i];

        //        _priceActions.Add((DateTime)drPAInputs["Date"], new PriceActionInput()
        //        {
        //            BToken = Convert.ToUInt32(drPAInputs["BToken"]),
        //            CTF = (int)drPAInputs["CTF"],
        //            Expiry = (DateTime)drPAInputs["Expiry"],
        //            CurrentDate = (DateTime)drPAInputs["Date"],
        //            PD_H = (decimal)drPAInputs["PD_H"],
        //            PD_L = (decimal)drPAInputs["PD_L"],
        //            PD_C = (decimal)drPAInputs["PD_C"],
        //            SL = Convert.ToDecimal(drPAInputs["SL"]),
        //            TP = Convert.ToDecimal(drPAInputs["TP"]),
        //            Qty = (int)drPAInputs["QTY"],
        //        });
        //    }
        //}

        //private void SetParameters(DateTime currentTime)
        //{ 
        //    if (!_dateLoaded.Contains(currentTime.Date))
        //    {
        //        _dateLoaded.Add(currentTime.Date);

        //        _previousDayHigh = _priceActions[currentTime.Date].PD_H;
        //        _previousDayLow = _priceActions[currentTime.Date].PD_L;
        //        _previousDayClose = _priceActions[currentTime.Date].PD_C;


        //        _orderTrios = new List<OrderTrio>();
        //        _criticalLevels = new SortedList<decimal, int>();

        //        _cpr = new CentralPivotRange(new OHLC() { Close = _previousDayClose, High = _previousDayHigh, Low = _previousDayLow});

        //        _criticalLevels.TryAdd(_previousDayClose, 0);
        //        _criticalLevels.TryAdd(_previousDayHigh, 1);
        //        _criticalLevels.TryAdd(_previousDayLow, 2);
        //        _criticalLevels.TryAdd(_previousDayOpen, 3);
        //        _criticalLevels.TryAdd(_previousSwingHigh, 4);
        //        _criticalLevels.TryAdd(_previousSwingLow, 5);

        //        _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.CPR], 6);
        //        _criticalLevels.Remove(0);
        //    }
        //}
        //private void LoadBaseInstrumentEMA(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        //{
        //    try
        //    {
        //        lock (_indexSch)
        //        {
        //            DataLogic dl = new DataLogic();
        //            Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

        //            foreach (var price in historicalCandlePrices[bToken])
        //            {
        //                _indexSch.Process(price, isFinal: true);
        //            }
        //            _indexSchLoadedFromDB = true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Trading Stopped");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error,
        //            lastCandleEndTime, String.Format(@"Error occurred! Trading has stopped. {0}", ex.Message), "LoadHistoricalCandles");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }

        //}

        private void LoadFutureToTrade(DateTime currentTime)
        {
            try
            {
                if (_activeFuture == null)
                {


#if BACKTEST
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadFutureToTrade");
                    OnCriticalEvents(currentTime.ToShortTimeString(), "Trade Started. Future Loaded.");
#endif
                }

            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
            }
        }

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                if (_activeFuture != null && !SubscriptionTokens.Contains(_activeFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeFuture.InstrumentToken);

#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Future", "UpdateInstrumentSubscription");
#endif
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
                Thread.Sleep(100);
            }
        }

        public int AlgoInstance
        {
            get { return _algoInstance; }
        }
        private bool GetBaseInstrumentPrice(Tick tick)
        {
            Tick baseInstrumentTick = tick.InstrumentToken == _baseInstrumentToken ? tick : null;
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)
            {
                _baseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }

        public virtual void OnNext(Tick tick)
        {
            try
            {
#if Market
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    return;

                }
#endif

#if local
                if (tick.Timestamp.HasValue && _currentDate.HasValue && tick.Timestamp.Value.Date.ToShortDateString() != _currentDate.Value.Date.ToShortDateString())
                {
                    ResetAlgo(tick.Timestamp.Value.Date);
                }
#endif
                if (!_stopTrade)
                {
                    ActiveTradeIntraday(tick);
                }
                //return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                //   return;
            }
        }
        private void ResetAlgo(DateTime tradeDate)
        {
            _currentDate = tradeDate;
            _stopTrade = false;
            //DataLogic dl = new DataLogic();
            //DateTime? nextExpiry = dl.GetCurrentMonthlyExpiry(_currentDate.Value);
            //SetUpInitialData(nextExpiry);
        }
        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
#if !BACKTEST
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
                Thread.Sleep(100);
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
                Thread.Sleep(100);
            }
#endif
        }

        //private void PublishLog(object sender, ElapsedEventArgs e)
        //{
        //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
        //    String.Format("Current ADX: {0}", Decimal.Round(0), "Log_Timer_Elapsed"));
        //    Thread.Sleep(100);
        //}

        public virtual void Subscribe(IObservable<Tick> provider)
        {
            unsubscriber = provider.Subscribe(this);
        }

        public virtual void Unsubscribe()
        {
            unsubscriber.Dispose();
        }

        public virtual void OnCompleted()
        {
            Console.WriteLine("Additional Ticks data will not be transmitted.");
        }

        public virtual void OnError(Exception error)
        {
            // Do nothing.
        }

        //public virtual void OnNext(Temperature value)
        //{
        //    Console.WriteLine("The temperature is {0}°C at {1:g}", value.Degrees, value.Date);
        //    if (first)
        //    {
        //        last = value;
        //        first = false;
        //    }
        //    else
        //    {
        //        Console.WriteLine("   Change: {0}° in {1:g}", value.Degrees - last.Degrees,
        //                                                      value.Date.ToUniversalTime() - last.Date.ToUniversalTime());
        //    }
        //}


        //private void PostOrderInKotak(Instrument option, DateTime currentTime, int qtyInlots, bool buyOrder)
        //{
        //    HttpClient httpClient = _httpClientFactory.CreateClient();

        //    string url = "https://tradeapi.kotaksecurities.com/apim/orders/1.0/order/mis";

        //    StringContent dataJson = null;
        //    string accessToken = ZObjects.kotak.KotakAccessToken;
        //    string sessionToken = ZObjects.kotak.UserSessionToken;
        //    HttpRequestMessage httpRequest = new HttpRequestMessage();
        //    httpRequest.Method = new HttpMethod("POST");

        //    //httpClient.DefaultRequestHeaders.Add("accept", "application/json");
        //    //httpClient.DefaultRequestHeaders.Add("consumerKey", ZObjects.kotak.ConsumerKey);
        //    //httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        //    //httpClient.DefaultRequestHeaders.Add("sessionToken", sessionToken);
        //    //httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //    httpRequest.Headers.Add("accept", "application/json");
        //    httpRequest.Headers.Add("consumerKey", ZObjects.kotak.ConsumerKey);
        //    httpRequest.Headers.Add("Authorization", "Bearer " + accessToken);

        //    httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //    httpRequest.RequestUri = new Uri(url);
        //    httpRequest.Headers.Add("sessionToken", sessionToken);
        //    //using (Stream webStream = httpRequest.GetRequestStream())
        //    //using (StreamWriter requestWriter = new StreamWriter(webStream))
        //    //    requestWriter.Write(requestBody);


        //    StringBuilder httpContentBuilder = new StringBuilder("{");
        //    httpContentBuilder.Append("\"instrumentToken\": ");
        //    httpContentBuilder.Append(option.KToken);
        //    httpContentBuilder.Append(", ");

        //    httpContentBuilder.Append("\"transactionType\": \"");
        //    httpContentBuilder.Append(buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL);
        //    httpContentBuilder.Append("\", ");

        //    httpContentBuilder.Append("\"quantity\": ");
        //    httpContentBuilder.Append(qtyInlots * Convert.ToInt32(option.LotSize));
        //    httpContentBuilder.Append(", ");

        //    httpContentBuilder.Append("\"price\": ");
        //    httpContentBuilder.Append(0); //Price = 0 means market order
        //    httpContentBuilder.Append(", ");

        //    //httpContentBuilder.Append("\"product\": \"");
        //    //httpContentBuilder.Append(Constants.PRODUCT_MIS.ToLower());
        //    //httpContentBuilder.Append("\", ");

        //    httpContentBuilder.Append("\"validity\": \"GFD\", ");
        //    httpContentBuilder.Append("\"disclosedQuantity\": 0");
        //    httpContentBuilder.Append(", ");
        //    httpContentBuilder.Append("\"triggerPrice\": 0");
        //    httpContentBuilder.Append(", ");
        //    httpContentBuilder.Append("\"variety\": \"True\"");
        //    httpContentBuilder.Append(", ");

        //    httpContentBuilder.Append("\"tag\": \"");
        //    httpContentBuilder.Append(buyOrder.ToString());
        //    httpContentBuilder.Append("\"");
        //    httpContentBuilder.Append("}");

        //    dataJson = new StringContent(httpContentBuilder.ToString(), Encoding.UTF8, Application.Json);
        //    httpRequest.Content = dataJson;

        //    //httpClient.DefaultRequestHeaders. = httpRequest.Headers;

        //    try
        //    {
        //        semaphore.WaitAsync();

        //        //Task<HttpResponseMessage> httpResponsetask = 
        //        //httpClient.PostAsync(url, httpRequest.Content)
        //        httpClient.SendAsync(httpRequest)
        //            .ContinueWith((postTask, option) =>
        //            {
        //                HttpResponseMessage response = postTask.Result;
        //                response.EnsureSuccessStatusCode();

        //                response.Content.ReadAsStreamAsync().ContinueWith(
        //                      (readTask) =>
        //                      {
        //                          //Console.WriteLine("Web content in response:" + readTask.Result);
        //                          using (StreamReader responseReader = new StreamReader(readTask.Result))
        //                          {
        //                              string response = responseReader.ReadToEnd();
        //                              Dictionary<string, dynamic> orderStatus = GlobalLayer.Utils.JsonDeserialize(response);

        //                              if (orderStatus != null && orderStatus.ContainsKey("Success") && orderStatus["Success"] != null)
        //                              {
        //                                  Dictionary<string, dynamic> data = orderStatus["Success"]["NSE"];
        //                                  Instrument instrument = (Instrument)option;
        //                                  Order order = null;
        //                                  Task<Order> orderTask;
        //                                  int counter = 0;
        //                                  while (true)
        //                                  {
        //                                      orderTask = GetKotakOrder(Convert.ToString(data["orderId"]), _algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, instrument.TradingSymbol);
        //                                      orderTask.Wait();
        //                                      order = orderTask.Result;

        //                                      if (order.Status == Constants.KORDER_STATUS_TRADED)
        //                                      {
        //                                          break;
        //                                      }
        //                                      else if (order.Status == Constants.KORDER_STATUS_REJECTED)
        //                                      {
        //                                          //_stopTrade = true;
        //                                          Logger.LogWrite("Order Rejected");
        //                                          LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
        //                                          break;
        //                                          //throw new Exception("order did not execute properly");
        //                                      }
        //                                      else if (counter > 5 && order.Status == Constants.KORDER_STATUS_OPEN)
        //                                      {
        //                                          //_stopTrade = true;
        //                                          Logger.LogWrite("order did not execute properly. Waited for 1 minutes");
        //                                          LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow),
        //                                              "Order did not go through. Waited for 10 minutes", "GetOrder");
        //                                          break;
        //                                          //throw new Exception("order did not execute properly. Waited for 10 minutes");
        //                                      }
        //                                      counter++;
        //                                  }

        //                                  OnTradeEntry(order);
        //                                  MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);
        //                              }
        //                              else if (orderStatus != null && orderStatus.ContainsKey("fault") && orderStatus["fault"] != null)
        //                              {
        //                                  Logger.LogWrite(Convert.ToString(orderStatus["Fault"]["message"]));
        //                                  throw new Exception(string.Format("Error while placing order", _algoInstance));
        //                              }
        //                              else
        //                              {
        //                                  throw new Exception(string.Format("Place Order status null for algo instance:{0}", _algoInstance));
        //                              }
        //                          }
        //                      }
        //                      );
        //            }, option);

        //        semaphore.Release();
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(ex.Message);
        //        throw ex;
        //    }
        //    finally
        //    {
        //        // httpClient.Dispose();
        //    }
        //}

        //public Task<Order> GetKotakOrder(string orderId, int algoInstance, AlgoIndex algoIndex, string status, string tradingSymbol)
        //{
        //    Order oh = null;
        //    int counter = 0;
        //    var httpClient = _httpClientFactory.CreateClient();

        //    StringBuilder url = new StringBuilder("https://tradeapi.kotaksecurities.com/apim/reports/1.0/orders/");//.Append(orderId);
        //    httpClient.BaseAddress = new Uri(url.ToString());
        //    httpClient.DefaultRequestHeaders.Add("accept", "application/json");
        //    httpClient.DefaultRequestHeaders.Add("consumerKey", ZObjects.kotak.ConsumerKey);
        //    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
        //    httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
        //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //    //HttpRequestMessage httpRequest = new HttpRequestMessage();
        //    //httpRequest.Method = new HttpMethod("GET");
        //    //httpRequest.Headers.Add("accept", "application/json");
        //    //httpRequest.Headers.Add("consumerKey", ZObjects.kotak.ConsumerKey);
        //    //httpRequest.Headers.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
        //    //httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
        //    //httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //    //httpRequest.RequestUri = new Uri(url.ToString());

        //    //while (true)
        //    //{
        //    try
        //    {
        //        //return httpClient.SendAsync(httpRequest).ContinueWith((getTask) =>
        //        return httpClient.GetAsync(url.ToString()).ContinueWith((getTask) =>
        //        {

        //            HttpResponseMessage response = getTask.Result;
        //            response.EnsureSuccessStatusCode();

        //            return response.Content.ReadAsStreamAsync().ContinueWith(
        //                  (readTask) =>
        //                  {
        //                      //Console.WriteLine("Web content in response:" + readTask.Result);
        //                      using (StreamReader responseReader = new StreamReader(readTask.Result))
        //                      {
        //                          string response = responseReader.ReadToEnd();
        //                          Dictionary<string, dynamic> orderData = GlobalLayer.Utils.JsonDeserialize(response);


        //                          KotakOrder kOrder = null;
        //                          List<KotakOrder> orderhistory = new List<KotakOrder>();
        //                          dynamic oid;
        //                          foreach (Dictionary<string, dynamic> item in orderData["success"])
        //                          {
        //                              if (item.TryGetValue("orderId", out oid) && Convert.ToString(oid) == orderId)
        //                              {
        //                                  kOrder = new KotakOrder(item);
        //                                  break;
        //                              }
        //                          }

        //                          kOrder.Tradingsymbol = tradingSymbol;
        //                          kOrder.OrderType = "Market";
        //                          oh = new Order(kOrder);

        //                          oh.AlgoInstance = algoInstance;
        //                          oh.AlgoIndex = Convert.ToInt32(algoIndex);
        //                          return oh;
        //                      }
        //                  });
        //        }).Unwrap();
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(ex.Message);
        //        throw ex;
        //    }
        //    finally
        //    {
        //        // httpClient.Dispose();
        //    }
        //}
        //oh.AlgoInstance = algoInstance;
        //oh.AlgoIndex = Convert.ToInt32(algoIndex);
        //return oh;
        // }
        // }

    }
}
