using Algorithms.Candles;
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
////using KafkaFacade;
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
    public class MultiEMADirectionalLevels : IZMQ, IObserver<Tick>////, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(MultiEMADirectionalLevels source);
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
        private Candle _previousIndexCandle;
        public List<Instrument> ActiveOptions { get; set; }
        //public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        private bool? ls;
        private int tradeCount;
        private Queue<decimal> referenceValues = new Queue<decimal>(5);
        public Dictionary<uint, Instrument> AllOptions { get; set; }

        public SortedList<decimal, Instrument> CallOptionsByStrike { get; set; }
        public SortedList<decimal, Instrument> PutOptionsByStrike { get; set; }

        Dictionary<int, ExponentialMovingAverage> _indexEMAs;

        //Dictionary < TimeCandle interval, Dictionary < EMALength, EMA>>
        Dictionary<int, Dictionary<int, ExponentialMovingAverage>> _indexCandleEMAs;
        Dictionary<int, Dictionary<int, ExponentialMovingAverage>> _indexPreviousCandleEMAs;
        //Dictionary < String (Candle Interval_EMALength), EMAValue)
        private Dictionary<string, decimal> _criticalLevels;

        Dictionary<int, decimal> _indexEMAsPrevCandle;
        private User _user;
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
        private Instrument atmOption;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);
        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public readonly decimal _minDistanceFromBInstrument = 300;
        public readonly decimal _maxDistanceFromBInstrument = 600;
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private const decimal SCH_UPPER_THRESHOLD = 70;
        private const decimal SCH_LOWER_THRESHOLD = 30;
        public Dictionary<uint, uint> MappedTokens { get; set; }
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private Candle _pCandle;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.MultiEMADirectionalLevels;
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

        private decimal _cumulativeScore;
        private decimal referencescore;
        StochasticOscillator _indexSch;

        private OrderTrio _callOrderTrio;
        private OrderTrio _putOrderTrio;

        private decimal _longTradeEntry;
        private decimal _longTradeSL;
        private decimal _longTradeTarget = 0;

        private decimal _shortTradeEntry;
        private decimal _shortTradeSL;
        private decimal _shortTradeTarget = 0;

        private int _callOrderTriggerEMALength = 0;
        private int _putOrderTriggerEMALength = 0;

        public struct PriceRange
        {
            public decimal Upper;
            public decimal Lower;
            public DateTime? CrossingTime;
        }
        private const decimal QUALIFICATION_ZONE_THRESHOLD = 15;
        private const decimal TRADING_ZONE_THRESHOLD = 10; // This should be % of qualification zone. 50% is good.
        private const decimal RR_BREAKDOWN = 1; // Risk Reward
        private const decimal RR_BREAKOUT = 2; // Risk Reward
        private decimal _pnl = 0;

        private PriceRange _resistancePriceRange;
        private PriceRange _supportPriceRange;

        bool _indexSchLoaded = false;
        bool _indexEMALoading = false;
        bool _indexEMALoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;

        Dictionary<DateTime, int> _candleScore;

        public MultiEMADirectionalLevels(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            int quantity, string uid, int algoInstance = 0, IHttpClientFactory httpClientFactory = null)
        {
            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            _candleTimeSpan = candleTimeSpan;

            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            //_trailingStopLoss = _stopLoss = stopLoss;
            //_targetProfit = targetProfit;

            _tradeQty = quantity;
            //_positionSizing = positionSizing;
            //_maxLossPerTrade = maxLossPerTrade;

            SetUpInitialData(algoInstance);


            //#if local
            //            _dateLoaded = new List<DateTime>();
            //            LoadPAInputsForTest();
            //#endif

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }
        private void SetUpInitialData(int algoInstance = 0)
        {
            //_expiryDate = expiry;
            //_orderTrios = new List<OrderTrio>();
            //_orderTriosFromEMATrades = new List<OrderTrio>();
            //_criticalLevels = new SortedList<decimal, int>();
            //_criticalLevelsWeekly = new SortedList<decimal, int>();


            //_indexSch = new StochasticOscillator();
            //_indexEMA = new ExponentialMovingAverage(length: 20);
            _candleScore = new Dictionary<DateTime, int>();
            _indexCandleEMAs = new Dictionary<int, Dictionary<int, ExponentialMovingAverage>>()
            {
                { 1,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            }
                },
                { 3,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                { 5,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                { 10,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                {15,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                {30,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
            };
            _indexPreviousCandleEMAs = new Dictionary<int, Dictionary<int, ExponentialMovingAverage>>()
            {
                { 1,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            }
                },
                { 3,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                { 5,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                { 10,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                {15,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
                {30,  new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            } },
            };

            _indexEMAs = new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 300, new ExponentialMovingAverage(length:300) },
                { 400, new ExponentialMovingAverage(length:400) }
            };
            _indexEMAsPrevCandle = new Dictionary<int, decimal>()
            {
                { 20, -1 },
                { 50, -1 },
                { 100, -1 },
                { 200, -1 },
                {300, -1 },
                { 400, -1 }
            };

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _criticalLevels = new Dictionary<string, decimal>();
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now, DateTime.Now,
                //expiry.GetValueOrDefault(DateTime.Now), 
                _tradeQty, 0, 0, 0, 0,
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
                    //LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);

                    //if (_cpr == null)
                    //{
                    //    LoadCriticalLevels(token, currentTime);
                    //}

                    if (!_indexEMALoading && !_indexEMALoadedFromDB)
                    {
                        _indexEMALoading = true;
                        LoadBaseInstrumentEMA(_baseInstrumentToken, currentTime);
                        LoadCriticalLevels(token, currentTime);
                    }

#if local

                    if (SubscriptionTokens.Contains(token))
                    {
#endif
                        if (_baseInstrumentToken == token)
                        {
                            MonitorCandles(tick, currentTime);
                        }
                        else if (tick.LastTradeTime != null)
                        {
                            UpdateOptionPrice(tick);
                            // TradeTPSL(currentTime, tp: true, sl: false);
                        }


                        //if (_longTradeEntry > 0 && _baseInstrumentPrice > _longTradeEntry && _callOrderTrio == null && _longTradeTarget == 0
                        //        && (!_criticalLevels.Any(x => x.Value > _baseInstrumentPrice && x.Value - _baseInstrumentPrice < 5 && x.Key != _callOrderTriggerEMALength))
                        //        && (!_criticalLevels.Any(x => x.Value < _baseInstrumentPrice && _longTradeEntry < x.Value && x.Key != _callOrderTriggerEMALength)))
                        //{
                        //    TakeTrade("ce", currentTime, _longTradeSL);
                        //}
                        //else if (_shortTradeEntry > 0 && _baseInstrumentPrice < _shortTradeEntry
                        //        && _putOrderTrio == null
                        //        && _shortTradeTarget == 0
                        //        && (!_criticalLevels.Any(x => x.Value < _baseInstrumentPrice && _baseInstrumentPrice - x.Value < 5 && x.Key != _putOrderTriggerEMALength))
                        //        && (!_criticalLevels.Any(x => x.Value > _baseInstrumentPrice && _shortTradeEntry > x.Value && x.Key != _putOrderTriggerEMALength)))
                        //{
                        //    TakeTrade("pe", currentTime, _shortTradeSL);
                        //}
                        ////    //breakdown
                        ////    TakeTrade("pe", e.CloseTime, RR_BREAKDOWN * (_resistancePriceRange.Upper - e.ClosePrice), _resistancePriceRange.Upper - e.ClosePrice);

                        //TradeTPSL(currentTime);
#if local
                    }
#endif
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
            try
            {
                if (e.InstrumentToken == _baseInstrumentToken)
                {
                    //Step 1: Update EMAs
                    UpdateEMAs(e.CloseTime, e.ClosePrice);


                    decimal closeRange = 40;
                    decimal minLevel, maxLevel;
                    bool nearPivot = false;
                    
                    foreach (var level in _criticalLevels.Keys.Where(x => x.Split('_')[0] == "0"))
                    {
                        decimal closePrice = _criticalLevels[level];
                        if (closePrice - e.ClosePrice < 40 && closePrice > e.ClosePrice &&  e.ClosePrice < e.OpenPrice)
                        {
                            nearPivot = true;
                            ls = false;
                            break;
                        }
                        else if (e.ClosePrice - closePrice < 40 && e.ClosePrice > closePrice && e.ClosePrice > e.OpenPrice)
                        {
                            nearPivot = true;
                            ls = true;
                            break;
                        }
                    }
                    decimal tradePrice = tradeCount == 0 ? e.ClosePrice : (2 * e.ClosePrice);
                    
                    if (nearPivot && Math.Abs(_criticalLevels["1_20"] - _criticalLevels["1_50"]) < 40)
                    {
                        if (ls.Value && e.ClosePrice > Math.Max(_criticalLevels["1_20"], _criticalLevels["1_50"]))
                        {
                            _pnl += -1 * tradePrice;
                            ++tradeCount;
                        }
                        else if (e.ClosePrice < Math.Min(_criticalLevels["1_20"], _criticalLevels["1_50"]))
                        {
                            _pnl += 1 * tradePrice;
                            ++tradeCount;
                        }
                    }
                    else if (!nearPivot)
                    {
                        decimal maxEMA = Math.Max(_criticalLevels["1_20"], Math.Max(_criticalLevels["1_50"], Math.Max(_criticalLevels["1_100"], _criticalLevels["1_200"])));
                        decimal minEMA = Math.Min(_criticalLevels["1_20"], Math.Min(_criticalLevels["1_50"], Math.Min(_criticalLevels["1_100"], _criticalLevels["1_200"])));

                        if (maxEMA - minEMA < 40)
                        {
                            if (e.ClosePrice > maxEMA)
                            {
                                if (_criticalLevels["3_20"] - e.ClosePrice < 30 
                                    || _criticalLevels["3_50"] - e.ClosePrice < 30 
                                    || _criticalLevels["3_100"] - e.ClosePrice < 30 
                                    || _criticalLevels["3_200"] - e.ClosePrice < 30)
                                {
                                    ls = true;
                                    _pnl += -1 * tradePrice;
                                    ++tradeCount;
                                }
                            }
                            else if (e.ClosePrice < minEMA)
                            {
                                if (e.ClosePrice - _criticalLevels["3_20"] < 30 || e.ClosePrice - _criticalLevels["3_50"] < 30 
                                    || e.ClosePrice - _criticalLevels["3_100"] < 30 || e.ClosePrice - _criticalLevels["3_200"] < 30)
                                {
                                    ls = false;
                                    _pnl += 1 * tradePrice;
                                    ++tradeCount;
                                }
                            }
                        }

                    }


                    //bool noTrade = false;

                    ////Step 2: Update score. For each candle compare with only high interval candle intervals, And weight is different for different interval.
                    //UpdateScore(e, 1, 1);
                    //if (e.CloseTime.TimeOfDay.Minutes % 3 == 0)
                    //{
                    //    Candle tc = GetLastCandle(_baseInstrumentToken, 3);
                    //    UpdateScore(tc, 1, 3);
                    //}
                    //if (e.CloseTime.TimeOfDay.Minutes % 5 == 0)
                    //{
                    //    Candle tc = GetLastCandle(_baseInstrumentToken, 5);
                    //    UpdateScore(tc, 1, 5);
                    //}
                    //if (e.CloseTime.TimeOfDay.Minutes % 10 == 0)
                    //{
                    //    Candle tc = GetLastCandle(_baseInstrumentToken, 10);
                    //    if (tc != null)
                    //    {
                    //        UpdateScore(tc, 1, 10);
                    //    }
                    //}
                    //if (e.CloseTime.TimeOfDay.Minutes % 15 == 0)
                    //{
                    //    Candle tc = GetLastCandle(_baseInstrumentToken, 15);
                    //    UpdateScore(tc, 1, 15);
                    //}
                    //if (e.CloseTime.TimeOfDay.Minutes % 30 == 0)
                    //{
                    //    Candle tc = GetLastCandle(_baseInstrumentToken, 30);
                    //    if (tc != null)
                    //    {
                    //        UpdateScore(tc, 1, 30);
                    //    }
                    //}


                    DataLogic dl = new DataLogic();
                    if (_candleScore.ContainsKey(e.CloseTime))
                    {
                        dl.UpdateCandleScore(AlgoInstance, e.CloseTime, e.ClosePrice, _candleScore[e.CloseTime]);
                    }
                    else
                    {
                        dl.UpdateCandleScore(AlgoInstance, e.CloseTime, e.ClosePrice, 0);
                    }


                    /****************Correct logic **/
                    ////Pnl - Check for cumulative score change greater than 5/10/15 to generate signal
                    //_cumulativeScore += _candleScore.ContainsKey(e.CloseTime) ? _candleScore[e.CloseTime] : 0;
                    //if (Math.Abs(_cumulativeScore - referencescore) > 5)
                    //{
                    //    decimal tradePrice = tradeCount == 0 ? e.ClosePrice : (2 * e.ClosePrice);

                    //    if (_cumulativeScore > referencescore && (!ls.HasValue || !ls.Value))
                    //    {
                    //        _pnl += -1 * tradePrice;
                    //        ls = true;
                    //        ++tradeCount;

                    //        //referencescore = Math.Max(_cumulativeScore, referencescore);
                    //    }
                    //    else if (_cumulativeScore < referencescore && (!ls.HasValue || ls.Value))
                    //    {
                    //        _pnl += tradePrice;
                    //        ls = false;
                    //        ++tradeCount;

                    //        //referencescore = Math.Min(_cumulativeScore, referencescore);
                    //    }
                    //    if (ls.HasValue)
                    //    {
                    //        referencescore = ls.Value ? Math.Max(_cumulativeScore, referencescore) : Math.Min(_cumulativeScore, referencescore);
                    //    }
                    //    //_pnl += ((_cumulativeScore > referencescore) ? -1 : 1) * e.ClosePrice;
                    //    //referencescore = _cumulativeScore;
                    //}

                    /****************Correct logic  ***/

                    //Pnl - Check for cumulative score change greater than 5/10/15 to generate signal
                   // _cumulativeScore += _candleScore.ContainsKey(e.CloseTime) ? _candleScore[e.CloseTime] : 0;
                    //if (_cumulativeScore > 0)
                    //{
                    //decimal tradePrice = tradeCount == 0 ? e.ClosePrice : (2 * e.ClosePrice);


                   // referenceValues.Enqueue(_candleScore.ContainsKey(e.CloseTime) ? _candleScore[e.CloseTime] : 0);



                    //if (_cumulativeScore > referenceValues.Sum() && (!ls.HasValue || !ls.Value))
                    //{
                    //    _pnl += -1 * tradePrice;
                    //    ls = true;
                    //    ++tradeCount;

                    //    //referencescore = Math.Max(_cumulativeScore, referencescore);
                    //}
                    //else if (_cumulativeScore < referenceValues.Sum() && (!ls.HasValue || ls.Value))
                    //{
                    //    _pnl += tradePrice;
                    //    ls = false;
                    //    ++tradeCount;

                    //    //referencescore = Math.Min(_cumulativeScore, referencescore);
                    //}
                    //if (referenceValues.Count > 5)
                    //    referenceValues.Dequeue();

                    //_pnl += ((_cumulativeScore > referencescore) ? -1 : 1) * e.ClosePrice;
                    //referencescore = _cumulativeScore;
                    //}




                    ////Run below loop for all emas
                    //foreach (var ema in _indexEMAs.Values.Where(x => x.Length == 20))
                    //{
                    //    decimal emaValue = ema.GetValue<Decimal>(0);
                    //    noTrade = false;
                    //    if (e.ClosePrice > emaValue && e.OpenPrice < emaValue
                    //        && ((e.ClosePrice - emaValue) > 0.05m * (e.ClosePrice - e.OpenPrice)))
                    //    {
                    //        foreach (decimal cv in _criticalLevels.Values)
                    //        {
                    //            if (cv > e.ClosePrice && cv - e.ClosePrice < 5)
                    //            {
                    //                noTrade = true;
                    //                break;
                    //            }
                    //        }
                    //        if (!noTrade)
                    //        {
                    //            _callOrderTriggerEMALength = ema.Length;
                    //            _longTradeEntry = e.HighPrice;
                    //            _longTradeSL = e.OpenPrice;// e.LowPrice;
                    //            _longTradeTarget = 0;
                    //        }
                    //    }
                    //    else if (e.ClosePrice < emaValue && e.OpenPrice > emaValue
                    //        && ((emaValue - e.ClosePrice) > 0.05m * (e.OpenPrice - e.ClosePrice)))
                    //    {
                    //        foreach (decimal cv in _criticalLevels.Values)
                    //        {
                    //            if (cv < e.ClosePrice && e.ClosePrice - cv < 5)
                    //            {
                    //                noTrade = true;
                    //                break;
                    //            }
                    //        }
                    //        if (!noTrade)
                    //        {
                    //            _putOrderTriggerEMALength = ema.Length;
                    //            _shortTradeEntry = e.LowPrice;
                    //            _shortTradeSL = e.OpenPrice;// e.HighPrice;
                    //            _shortTradeTarget = 0;
                    //        }

                    //    }
                    //}


                    //foreach (var emaValue in _indexEMAsPrevCandle.Values)
                    //{
                    //    if (_previousIndexCandle != null)
                    //    {
                    //        if (_previousIndexCandle.ClosePrice > emaValue && _previousIndexCandle.OpenPrice < emaValue)
                    //        {
                    //            if (_callOrderTrio == null)
                    //            {
                    //                _longTradeEntry = 0;
                    //                _callOrderTriggerEMALength = 0;
                    //            }
                    //            else
                    //            {
                    //                _longTradeTarget = e.HighPrice;
                    //                //_longTradeTarget = _longTradeEntry + (_longTradeEntry - _longTradeSL) * 2;
                    //            }
                    //        }
                    //        else if (_previousIndexCandle.ClosePrice < emaValue && _previousIndexCandle.OpenPrice > emaValue)
                    //        {
                    //            if (_putOrderTrio == null)
                    //            {
                    //                _shortTradeEntry = 0;
                    //                _putOrderTriggerEMALength = 0;
                    //            }
                    //            else
                    //            {
                    //                _shortTradeTarget = e.LowPrice;
                    //                // _shortTradeTarget = _shortTradeEntry - (_shortTradeSL - _shortTradeEntry) * 2;
                    //            }
                    //        }
                    //    }
                    //}
                    ////Closes all postions at 3:20 PM
                    TriggerEODPositionClose(e.CloseTime, e.ClosePrice);

                    //_previousIndexCandle = e;

                    ////copy dictionary
                    //foreach (var item in _indexEMAs)
                    //{
                    //    _indexEMAsPrevCandle[item.Key] = item.Value.GetValue<Decimal>(0);
                    //}
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "Candle Closusre");
                Thread.Sleep(100);
            }
        }
        private void TriggerEODPositionClose(DateTime currentTime, decimal closePrice)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00))
            {
                DataLogic dl = new DataLogic();

                //_pnl += tradeCount % 2 == 0 ? 0: (ls.Value? 1:-1)* closePrice;
                _pnl += (ls.Value ? 1 : -1) * closePrice;
                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                _pnl = 0;
                _stopTrade = true;
            }
        }


        //private void UpdateEMAs(Candle e)
        private void UpdateEMAs(DateTime candleCloseTime, Decimal candleClosePrice)
        {
            //First update all the EMAs, and put them within critical level dictionary,
            //and then check if any critical value is nearby
            foreach (var emaItem in _indexCandleEMAs[1])
            {
                //Check if this updating the values correctly within the dictionary
                emaItem.Value.Process(candleClosePrice, isFinal: true);
                //_criticalLevels[Int32.Parse(string.Format("1_{0}", emaItem.Key))] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                _criticalLevels[string.Format("1_{0}", emaItem.Key)] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
            }

            //3 min candles
            if (candleCloseTime.TimeOfDay.Minutes % 3 == 0)
            {
                foreach (var emaItem in _indexCandleEMAs[3])
                {
                    //Check if this updating the values correctly within the dictionary
                    emaItem.Value.Process(candleClosePrice, isFinal: true);
                    //_criticalLevels[Int32.Parse(string.Format("3_{0}", emaItem.Key))] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                    _criticalLevels[string.Format("3_{0}", emaItem.Key)] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                }
            }
            //5 min candles
            if (candleCloseTime.TimeOfDay.Minutes % 5 == 0)
            {
                foreach (var emaItem in _indexCandleEMAs[5])
                {
                    //Check if this updating the values correctly within the dictionary
                    emaItem.Value.Process(candleClosePrice, isFinal: true);
                    //_criticalLevels[Int32.Parse(string.Format("5_{0}", emaItem.Key))] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                    _criticalLevels[string.Format("5_{0}", emaItem.Key)] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                }
            }

            //10 min candles
            if (candleCloseTime.TimeOfDay.Minutes % 10 == 0)
            {
                foreach (var emaItem in _indexCandleEMAs[10])
                {
                    //Check if this updating the values correctly within the dictionary
                    emaItem.Value.Process(candleClosePrice, isFinal: true);
                    //_criticalLevels[Int32.Parse(string.Format("10_{0}", emaItem.Key))] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                    _criticalLevels[string.Format("10_{0}", emaItem.Key)] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                }
            }

            //15 min candles
            if (candleCloseTime.TimeOfDay.Minutes % 15 == 0)
            {
                foreach (var emaItem in _indexCandleEMAs[15])
                {
                    //Check if this updating the values correctly within the dictionary
                    emaItem.Value.Process(candleClosePrice, isFinal: true);
                    //_criticalLevels[Int32.Parse(string.Format("15_{0}", emaItem.Key))] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                    _criticalLevels[string.Format("15_{0}", emaItem.Key)] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                }
            }
            //30 min candles
            if (candleCloseTime.TimeOfDay.Minutes % 30 == 0)
            {
                foreach (var emaItem in _indexCandleEMAs[30])
                {
                    //Check if this updating the values correctly within the dictionary
                    emaItem.Value.Process(candleClosePrice, isFinal: true);
                    //_criticalLevels[Int32.Parse(string.Format("30_{0}", emaItem.Key))] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                    _criticalLevels[string.Format("30_{0}", emaItem.Key)] = Math.Round(emaItem.Value.GetValue<Decimal>(0), 2);
                }
            }
        }
        private void UpdateScore(Candle e, int weight, int interval)
        {
            var criticalLevelList = _criticalLevels.Where(x => Int32.Parse(x.Key.Split('_')[0]) >= interval).ToList();
            criticalLevelList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

            foreach (var criticalItem in criticalLevelList)
            {
                decimal level = criticalItem.Value;
                if ((e.HighPrice /* + 4*/ > level && Math.Max(e.OpenPrice, e.ClosePrice) < level) ||
                    (e.ClosePrice < level && e.OpenPrice > level))
                {
                    if (_candleScore.ContainsKey(e.CloseTime))
                    {
                        _candleScore[e.CloseTime] -= weight;
                    }
                    else
                    {
                        _candleScore.TryAdd(e.CloseTime, weight * -1);
                    }
                }
                else if ((e.LowPrice/* - 2*/ < level && Math.Min(e.OpenPrice, e.ClosePrice) > level) ||
                        (e.ClosePrice > level && e.OpenPrice < level))
                {
                    if (_candleScore.ContainsKey(e.CloseTime))
                    {
                        _candleScore[e.CloseTime] += weight;
                    }
                    else
                    {
                        _candleScore.TryAdd(e.CloseTime, weight);
                    }
                }
            }
        }

        private Candle GetLastCandle(uint instrumentToken, int candleCount)
        {
            if (TimeCandles[instrumentToken].Count < candleCount)
            {
                return null;
            }
            var lastCandles = TimeCandles[instrumentToken].TakeLast(candleCount);
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
            tC.ClosePrice = lastCandles.ElementAt(candleCount - 1).ClosePrice;
            tC.CloseTime = lastCandles.ElementAt(candleCount - 1).CloseTime;
            decimal lowPrice = lastCandles.ElementAt(0).LowPrice;
            decimal highPrice = lastCandles.ElementAt(0).HighPrice;

            for (int i = 0; i < candleCount; i++)
            {
                lowPrice = Math.Min(lowPrice, lastCandles.ElementAt(i).LowPrice);
                highPrice = Math.Max(highPrice, lastCandles.ElementAt(i).HighPrice);
            }
            tC.HighPrice = highPrice;
            tC.LowPrice = lowPrice;
            return tC;
        }
        private bool IsWickQualified(decimal wick)
        {
            return wick > QUALIFICATION_ZONE_THRESHOLD;
        }
        void UpdateFuturePrice(decimal lastPrice)
        {
            atmOption.LastPrice = lastPrice;
        }
        private void TakeTrade(string instrumentType, DateTime currentTime, decimal stopLevel)
        {
            int qty = _tradeQty;
            //            if (_orderTrios.Count > 0)
            //            {
            //                if (_orderTrios[0].Option.InstrumentType.Trim(' ').ToLower() != instrumentType.Trim(' ').ToLower())
            //                {
            //#if market
            //                    CloseTrade(currentTime, _orderTrios[0].Option);
            //#elif local
            //                    CloseTrade(currentTime, AllOptions[_orderTrios[0].Option.InstrumentToken]);
            //#endif
            //                }
            //                else
            //                {
            //                    qty = 0;
            //                }
            //            }

            //            if (qty > 0)
            //            {
            OrderTrio orderTrio = new OrderTrio();
            decimal atmStrike = GetATMStrike(_baseInstrumentPrice, instrumentType);
            Instrument atmOption = (instrumentType == "ce") ? CallOptionsByStrike[atmStrike] : PutOptionsByStrike[atmStrike];

#if BACKTEST
            atmOption = AllOptions[atmOption.InstrumentToken];
            if (atmOption.LastPrice == 0)
            {
                return;
            }
#endif

            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, atmOption.TradingSymbol, atmOption.InstrumentType.ToLower(), atmOption.LastPrice, //e.ClosePrice,
               atmOption.KToken, true, _tradeQty * Convert.ToInt32(atmOption.LotSize), algoIndex, currentTime, Tag: "Algo5",
               broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));



            orderTrio.StopLoss = stopLevel; // This level is of the index. A market order will be placed when index reaches this level
                                            //orderTrio.TargetProfit = orderTrio.Order.AveragePrice + Math.Max(targetLevel, 3); //at least 3 points scalping
                                            //orderTrio.TPFlag = false;
                                            ////atmOption.InstrumentToken = _baseInstrumentToken;
            atmOption.LastPrice = orderTrio.Order.AveragePrice;
            orderTrio.Option = atmOption;
            if (instrumentType == "ce")
            {
                _callOrderTrio = orderTrio;
            }
            else
            {
                _putOrderTrio = orderTrio;
            }

            //_orderTrios.Add(orderTrio);

#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Bought ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
            //}
        }
        private void CloseTrade(DateTime currentTime, OrderTrio orderTrio)
        {
            Instrument option = orderTrio.Option;

            decimal lastPrice = AllOptions[option.InstrumentToken].LastPrice;

            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice, //e.ClosePrice,
                   option.KToken, false, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Tag: "Algo4",
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

            _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * _tradeQty * Convert.ToInt32(option.LotSize);
#if !BACKTEST
                OnTradeExit(order);
#endif
            //return orderTrio.Order.AveragePrice;
        }

        //private void TradeTPSL(DateTime currentTime)
        //{
        //    if (_callOrderTrio != null)
        //    {
        //        if ((_baseInstrumentPrice < _callOrderTrio.StopLoss)
        //            || (_criticalLevels.Any(x => x.Value > _baseInstrumentPrice && x.Value - _baseInstrumentPrice < 3 && x.Key != _callOrderTriggerEMALength))
        //            || (_criticalLevels.Any(x => x.Value < _baseInstrumentPrice && _baseInstrumentPrice - x.Value < 3 && x.Key != _callOrderTriggerEMALength))
        //            || ((_longTradeTarget != 0 && _baseInstrumentPrice > _longTradeTarget)
        //            && (AllOptions[_callOrderTrio.Option.InstrumentToken].LastPrice > _callOrderTrio.Order.AveragePrice + 3)))
        //        {
        //            CloseTrade(currentTime, _callOrderTrio);
        //            _callOrderTrio = null;
        //            _longTradeTarget = 0;
        //            _longTradeEntry = 0;
        //            _callOrderTriggerEMALength = 0;
        //        }
        //    }
        //    if (_putOrderTrio != null)
        //    {
        //        if ((_baseInstrumentPrice > _putOrderTrio.StopLoss)
        //            || (_criticalLevels.Any(x => x.Value < _baseInstrumentPrice && _baseInstrumentPrice - x.Value < 3 && x.Key != _putOrderTriggerEMALength))
        //            || (_criticalLevels.Any(x => x.Value > _baseInstrumentPrice && x.Value - _baseInstrumentPrice < 3 && x.Key != _putOrderTriggerEMALength))
        //            || ((_shortTradeTarget != 0 && _baseInstrumentPrice < _shortTradeTarget)
        //            && (AllOptions[_putOrderTrio.Option.InstrumentToken].LastPrice > _putOrderTrio.Order.AveragePrice + 3)))
        //        {
        //            CloseTrade(currentTime, _putOrderTrio);
        //            _putOrderTrio = null;
        //            _shortTradeTarget = 0;
        //            _shortTradeEntry = 0;
        //            _putOrderTriggerEMALength = 0;
        //        }
        //    }
        //}
        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (CallOptionsByStrike == null ||
                (CallOptionsByStrike.Keys.Last() < _baseInstrumentPrice + _minDistanceFromBInstrument
                || CallOptionsByStrike.Keys.First() > _baseInstrumentPrice - _minDistanceFromBInstrument))
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, uint> mappedTokens;
                    SortedList<decimal, Instrument> calls, puts;

                    DataLogic dl = new DataLogic();
                    _expiryDate = dl.GetCurrentWeeklyExpiry(currentTime, _baseInstrumentToken);
                    var allOptions = dl.LoadOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out calls, out puts, out mappedTokens);

                    if (allOptions.Count == 0)
                    {
                        return;
                    }
                    AllOptions ??= new Dictionary<uint, Instrument>();
                    foreach (var optionItem in allOptions)
                    {
                        AllOptions.TryAdd(optionItem.InstrumentToken, optionItem);
                    }

                    if (CallOptionsByStrike == null)
                    {
                        CallOptionsByStrike = calls;
                        PutOptionsByStrike = puts;
                    }
                    else
                    {
                        foreach (var callItems in calls)
                        {
                            CallOptionsByStrike.TryAdd(callItems.Key, callItems.Value);
                        }
                        foreach (var putItems in puts)
                        {
                            PutOptionsByStrike.TryAdd(putItems.Key, putItems.Value);
                        }
                    }
                    if (MappedTokens == null)
                    {
                        MappedTokens = mappedTokens;
                    }
                    else
                    {
                        foreach (var mToken in mappedTokens)
                        {
                            MappedTokens.TryAdd(mToken.Key, mToken.Value);
                        }
                    }

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
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

        private void LoadCriticalLevels(uint token, DateTime currentTime)
        {
            try
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
                List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
                List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

                //OHLC pdOHLC = new OHLC() { Close = pdOHLCList.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

                OHLC pdOHLC = new OHLC() { Close = pdOHLCDay.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

                _cpr = new CentralPivotRange(pdOHLC);

                _previousDayOHLC = pdOHLC;
                _previousDayOHLC.Close = pdOHLCList.Last().Close;

                ////List<Historical> pwOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
                ////_previousWeekOHLC = new OHLC(pwOHLCList.First(), token);
                ////_weeklycpr = new CentralPivotRange(_previousWeekOHLC);
                //_criticalLevels = new SortedDictionary<int, decimal>();
                _criticalLevels.TryAdd("0_0", _previousDayOHLC.Close);
                _criticalLevels.TryAdd("0_1", _previousDayOHLC.High);
                _criticalLevels.TryAdd("0_2", _previousDayOHLC.Low);
                _criticalLevels.TryAdd("0_3", _cpr.Prices[(int)PivotLevel.CPR]);
                _criticalLevels.TryAdd("0_4", _cpr.Prices[(int)PivotLevel.R1]);
                _criticalLevels.TryAdd("0_5", _cpr.Prices[(int)PivotLevel.R2]);
                _criticalLevels.TryAdd("0_6", _cpr.Prices[(int)PivotLevel.R3]);
                _criticalLevels.TryAdd("0_7", _cpr.Prices[(int)PivotLevel.R4]);
                _criticalLevels.TryAdd("0_8", _cpr.Prices[(int)PivotLevel.S1]);
                _criticalLevels.TryAdd("0_9", _cpr.Prices[(int)PivotLevel.S2]);
                _criticalLevels.TryAdd("0_10", _cpr.Prices[(int)PivotLevel.S3]);
                _criticalLevels.TryAdd("0_11", _cpr.Prices[(int)PivotLevel.S4]);


                //_criticalLevels.TryAdd(Int32.Parse(string.Format("1{0}", 1)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("1{0}", 3)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("1{0}", 5)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("1{0}", 15)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("1{0}", 30)), 0);

                //_criticalLevels.TryAdd(Int32.Parse(string.Format("3{0}", 1)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("3{0}", 3)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("3{0}", 5)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("3{0}", 15)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("3{0}", 30)), 0);

                //_criticalLevels.TryAdd(Int32.Parse(string.Format("5{0}", 1)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("5{0}", 3)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("5{0}", 5)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("5{0}", 15)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("5{0}", 30)), 0);

                //_criticalLevels.TryAdd(Int32.Parse(string.Format("15{0}", 1)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("15{0}", 3)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("15{0}", 5)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("15{0}", 15)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("15{0}", 30)), 0);

                //_criticalLevels.TryAdd(Int32.Parse(string.Format("30{0}", 1)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("30{0}", 3)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("30{0}", 5)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("30{0}", 15)), 0);
                //_criticalLevels.TryAdd(Int32.Parse(string.Format("30{0}", 30)), 0);


                _criticalLevels.TryAdd(string.Format("1_{0}", 20), 0);
                _criticalLevels.TryAdd(string.Format("1_{0}", 50), 0);
                _criticalLevels.TryAdd(string.Format("1_{0}", 100), 0);
                _criticalLevels.TryAdd(string.Format("1_{0}", 200), 0);
                _criticalLevels.TryAdd(string.Format("1_{0}", 300), 0);
                _criticalLevels.TryAdd(string.Format("1_{0}", 400), 0);

                _criticalLevels.TryAdd(string.Format("3_{0}", 20), 0);
                _criticalLevels.TryAdd(string.Format("3_{0}", 50), 0);
                _criticalLevels.TryAdd(string.Format("3_{0}", 100), 0);
                _criticalLevels.TryAdd(string.Format("3_{0}", 200), 0);
                _criticalLevels.TryAdd(string.Format("3_{0}", 300), 0);
                _criticalLevels.TryAdd(string.Format("3_{0}", 400), 0);

                _criticalLevels.TryAdd(string.Format("5_{0}", 20), 0);
                _criticalLevels.TryAdd(string.Format("5_{0}", 50), 0);
                _criticalLevels.TryAdd(string.Format("5_{0}", 100), 0);
                _criticalLevels.TryAdd(string.Format("5_{0}", 200), 0);
                _criticalLevels.TryAdd(string.Format("5_{0}", 300), 0);
                _criticalLevels.TryAdd(string.Format("5_{0}", 400), 0);

                _criticalLevels.TryAdd(string.Format("10_{0}", 20), 0);
                _criticalLevels.TryAdd(string.Format("10_{0}", 50), 0);
                _criticalLevels.TryAdd(string.Format("10_{0}", 100), 0);
                _criticalLevels.TryAdd(string.Format("10_{0}", 200), 0);
                _criticalLevels.TryAdd(string.Format("10_{0}", 300), 0);
                _criticalLevels.TryAdd(string.Format("10_{0}", 400), 0);

                _criticalLevels.TryAdd(string.Format("15_{0}", 20), 0);
                _criticalLevels.TryAdd(string.Format("15_{0}", 50), 0);
                _criticalLevels.TryAdd(string.Format("15_{0}", 100), 0);
                _criticalLevels.TryAdd(string.Format("15_{0}", 200), 0);
                _criticalLevels.TryAdd(string.Format("15_{0}", 300), 0);
                _criticalLevels.TryAdd(string.Format("15_{0}", 400), 0);

                _criticalLevels.TryAdd(string.Format("30_{0}", 20), 0);
                _criticalLevels.TryAdd(string.Format("30_{0}", 50), 0);
                _criticalLevels.TryAdd(string.Format("30_{0}", 100), 0);
                _criticalLevels.TryAdd(string.Format("30_{0}", 200), 0);
                _criticalLevels.TryAdd(string.Format("30_{0}", 300), 0);
                _criticalLevels.TryAdd(string.Format("30_{0}", 400), 0);


                ////Dummy values for EMAs. This gets updated after candle closure, BEFORE checking for the order
                //_criticalLevels.TryAdd(20, -1);
                //_criticalLevels.TryAdd(50, -1);
                //_criticalLevels.TryAdd(100, -1);
                //_criticalLevels.TryAdd(200, -1);
                //_criticalLevels.TryAdd(400, -1);

                //_criticalLevels.Remove(0);
            }
            catch (Exception ex)
            {

            }

        }

        private uint GetKotakToken(uint kiteToken)
        {
            return MappedTokens[kiteToken];
        }
        private void UpdateOptionPrice(Tick tick)
        {
            AllOptions[tick.InstrumentToken].LastPrice = tick.LastPrice;
        }
        private decimal GetATMStrike(decimal bPrice, string instrumentType)
        {
            return instrumentType.ToLower() == "ce" ? Math.Floor(bPrice / 100) * 100 : Math.Ceiling(bPrice / 100) * 100;
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


        //        private void LoadBInstrumentSch(uint bToken, int candleCount, DateTime currentTime)
        //        {
        //            DateTime lastCandleEndTime;
        //            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
        //            try
        //            {
        //                lock (_indexSch)
        //                {
        //                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
        //                    {
        //                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
        //                    }
        //                    int firstCandleFormed = 0;
        //                    if (!_indexSchLoading)
        //                    {
        //                        _indexSchLoading = true;
        //                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
        //                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
        //                    }


        //                    if (TimeCandles.ContainsKey(bToken) && _indexSchLoadedFromDB)
        //                    {
        //                        if (_firstCandleOpenPriceNeeded[bToken])
        //                        {
        //                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
        //                            _indexSch.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

        //                            firstCandleFormed = 1;
        //                        }
        //                        //In case SQL loading took longer then candle time frame, this will be used to catch up
        //                        if (TimeCandles[bToken].Count > 1)
        //                        {
        //                            foreach (var price in TimeCandles[bToken])
        //                            {
        //                                _indexSch.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
        //                            }
        //                        }
        //                    }

        //                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexSchLoadedFromDB)
        //                    {
        //                        _indexSchLoaded = true;
        //#if !BACKTEST
        //                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
        //                            String.Format("{0} EMA loaded from DB for Base Instrument", 20), "LoadBInstrumentSCH");
        //#endif
        //                    }
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                _stopTrade = true;
        //                Logger.LogWrite(ex.Message + ex.StackTrace);
        //                Logger.LogWrite("Trading Stopped as algo encountered an error");
        //                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
        //                Thread.Sleep(100);
        //            }
        //        }
        private void LoadBaseInstrumentEMA(uint bToken, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_indexEMAs)
                {
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 34);

                    previousTradingDate = lastCandleEndTime.AddDays(-20);


                    //List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicals)
                    {
                        UpdateEMAs(price.TimeStamp.AddMinutes(1), price.Close);
                        //foreach (var candleEMAs in _indexCandleEMAs)
                        //{
                        //    foreach (var indexEMA in candleEMAs.Value)
                        //    {
                        //        indexEMA.Value.Process(price.Close, isFinal: true);
                        //    }
                        //}
                    }
                    _indexEMALoadedFromDB = true;
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
            if (_callOrderTrio != null)
            {
                Instrument option = _callOrderTrio.Option;
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                    option.KToken, false, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Tag: "Algo5",
                    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                _pnl += (order.AveragePrice - _callOrderTrio.Order.AveragePrice) * _tradeQty * option.LotSize;

                OnTradeEntry(order);
            }
            if (_putOrderTrio != null)
            {
                Instrument option = _putOrderTrio.Option;
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                    option.KToken, false, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Tag: "Algo5",
                    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                _pnl += (order.AveragePrice - _putOrderTrio.Order.AveragePrice) * _tradeQty * option.LotSize;

                OnTradeEntry(order);
            }
#if !BACKTEST
                    //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

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
        //                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, atmOption.TradingSymbol, "fut", atmOption.LastPrice,
        //                            atmOption.KToken, false, _tradeQty, algoIndex, currentTime, Tag: "Algo2",
        //                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

        //                        OnTradeEntry(orderTrio.Order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif

        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));


        //                        _orderTriosFromEMATrades.Remove(orderTrio);
        //                        i--;
        //                    }
        //                    else if (orderTrio.Order.TransactionType == "sell" && (lastPrice > _indexEMAValue.GetValue<decimal>() || closeAll))
        //                    {
        //                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, atmOption.TradingSymbol, "fut", atmOption.LastPrice,
        //                       atmOption.KToken, true, _tradeQty, algoIndex, currentTime, Tag: "Algo2",
        //                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

        //                        OnTradeEntry(orderTrio.Order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif
        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

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
        //    DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.MultiEMADirectionalLevels, Convert.ToDateTime("2021-11-30"), Convert.ToDateTime("2021-12-30"));

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
                if (atmOption == null)
                {


#if BACKTEST
                    DataLogic dl = new DataLogic();
                    atmOption = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    atmOption = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
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
                bool dataUpdated = false;
                if (AllOptions != null)
                {
                    foreach (var option in AllOptions)
                    {
                        if (!SubscriptionTokens.Contains(option.Key))
                        {
                            SubscriptionTokens.Add(option.Key);
                            dataUpdated = true;
                        }
                    }
                }
                if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                {
                    SubscriptionTokens.Add(_baseInstrumentToken);
                }
                if (dataUpdated)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
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
        //    HttpClient httpClient = KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient());

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
        //    var httpClient = KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient());

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
