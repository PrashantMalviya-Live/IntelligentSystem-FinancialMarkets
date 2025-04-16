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
//using KafkaFacade;
using System.Timers;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using Algos.Utilities.Views;
using FirebaseAdmin.Messaging;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Net.NetworkInformation;

namespace Algorithms.Algorithms
{
    public class BreakoutCandles : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(BreakoutCandles source);
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

        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        private List<OrderTrio> _callOrderTrios;
        private List<OrderTrio> _putOrderTrios;

        //private List<OrderTrio> _orderTrios;
        private List<OrderTrio> _orderTriosFromEMATrades;
        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public Queue<decimal> _indexValues;


        //public class PriceTime
        //{
        //    public decimal LastPrice;
        //    public DateTime? TradeTime;
        //}

        public List<PriceTime> _bValues;
        //public Dictionary<decimal, DateTime> _bValues;

        private Instrument _activePut, _activeCall;


        public decimal _minDistanceFromBInstrument;
        public decimal _maxDistanceFromBInstrument;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private int _lotSize = 25;
        //private Instrument _activeFuture;
        //private Instrument _referenceIndex;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);

        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);

        public List<Trigger> _triggers = new List<Trigger>();
        public class Trigger
        {
            public decimal tradePrice;
            public bool up;
            public decimal sl;
            public decimal tp;
            public DateTime? lastBreakoutTime;
            public bool traded = false;
        }
        public const decimal BAND_THRESHOLD = 10m;
        //public const decimal MINIMUM_TARGET = 50m;// 5 mins : 30; 15 mins: 40m;
        private decimal _minimumTarget;
        public readonly TimeSpan TIME_THRESHOLD = new TimeSpan(0, 1, 0);
        public readonly TimeSpan BREAKOUT_TIME_THRESHOLD = new TimeSpan(0, 1, 5);
        public readonly TimeSpan BREAKOUT_TIME_LIMIT = new TimeSpan(0, 7, 0);

        public const decimal PERCENT_RETRACEMENT = 0.0008m; //0.08% retracement of index value or 50 points

        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private const decimal SCH_UPPER_THRESHOLD = 70;
        private const decimal THRESHOLD = 40;
        //private TimeSpan TIME_THRESHOLD = new TimeSpan(0, 3, 0);
        private const decimal PROFIT_TARGET = 50;
        private const decimal STOP_LOSS = 50;
        private const decimal VALUES_THRESHOLD = 25000;
        private const decimal SCH_LOWER_THRESHOLD = 30;
        //private const decimal CANDLE_WICK_SIZE = 45;//10000;//45;//35
        //private const decimal CANDLE_BODY_MIN = 5;
        //private const decimal CANDLE_BODY = 40;//10000;//40; //25
        //private const decimal CANDLE_BODY_BIG = 35;
        //private const decimal EMA_ENTRY_RANGE = 35;
        //private const decimal RISK_REWARD = 2.3M;
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private DateTime? _upBreakoutTime;
        private bool _tradeSuspended = false;
        private DateTime? _downBreakoutTime;
        private Dictionary<uint, Candle> _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.SARScalping;
        private decimal _targetProfitForUpperBreakout;
        private decimal _stopLossForUpperBreakout;
        private decimal _targetProfitForLowerBreakout;
        private decimal _stopLossForLowerBreakout;
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

        //private bool _longTrigerred = false;
        //private bool _shortTrigerred = false;
        //Sorted list of target and sl. key is target, value is sl
        private Dictionary<uint, List<Trigger>> _longTrigerred;
        private Dictionary<uint, List<Trigger>> _shortTrigerred;
        
        //private decimal _shortSL, _shortTP;

        private decimal _tradeEntryPriceForUpperBreakout;
        private decimal _tradeEntryPriceForLowerBreakout;
        private decimal _lastLongSLPrice;
        private decimal _lastShortSLPrice;
        private decimal _firstBottomPrice = 0;
        private decimal _secondBottomPrice = 0;
        private decimal _firstTopPrice = 0;
        private decimal _secondTopPrice = 0;
        private User _user;
        private DateTime _slhitTime;
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
        private bool _historicalDataLoaded = false;
        bool _indexSchLoaded = false;
        bool _indexSchLoading = false;
        bool _indexSchLoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;

        public BreakoutCandles(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss, 
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;

            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _trailingStopLoss = _stopLossForLowerBreakout = _stopLossForUpperBreakout = stopLoss;
            _stopLossForLowerBreakout = _stopLossForUpperBreakout = targetProfit;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            SetUpInitialData(expiry, algoInstance);

            _minimumTarget = targetProfit;

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
            
            //_orderTrios = new List<OrderTrio>();
            _callOrderTrios = new List<OrderTrio>();
            _putOrderTrios = new List<OrderTrio>();

            _orderTriosFromEMATrades = new List<OrderTrio>();

            _maxDistanceFromBInstrument = 800;
            _minDistanceFromBInstrument = 300;



            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();

            //_indexSch = new StochasticOscillator();
            
            _longTrigerred = new Dictionary<uint, List<Trigger>>();
            _shortTrigerred = new Dictionary<uint, List<Trigger>>();
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 7);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 8);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 9);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 10);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 11);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 12);
            _pCandle = new Dictionary<uint, Candle>();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            //_indexValues = new Queue<decimal>(60);
            ////_bValues = new List<decimal>(100000);
            //_bValues = new List<PriceTime>(100000);

            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfitForUpperBreakout, _stopLossForUpperBreakout, 0,
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
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken || tick.LastTradeTime == null) ?
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
                    
                    //LoadFutureToTrade(currentTime);
                    LoadOptionsToTrade(currentTime);
                    //_referenceIndex.LastPrice = _baseInstrumentPrice;

                    if (OptionUniverse != null)
                    {
                        UpdateInstrumentSubscription(currentTime);
                    }
#if BACKTEST
                    if (SubscriptionTokens.Contains(token))
                    {
#endif

                        if (OptionUniverse != null)
                        {
                            UpdateOptionPrice(tick);
                            if (token == _activeCall.InstrumentToken || token == _activePut.InstrumentToken)
                            {
                                MonitorCandles(tick, currentTime);

                                Instrument optionsToTrade = null;
                                string instrumentType;
                                if(token == _activeCall.InstrumentToken)
                                {
                                    instrumentType = "ce";
                                    optionsToTrade = _activeCall;
                                }
                                else
                                {
                                    instrumentType = "pe";
                                    optionsToTrade = _activePut;
                                }

                                //TakeTrade(currentTime);
                                ExecuteTrade(currentTime, token, tick.LastPrice, instrumentType, optionsToTrade) ;

                                TradeTP(currentTime, _callOrderTrios);
                                TradeTP(currentTime, _putOrderTrios);

                                PurgeTriggers(_shortTrigerred[_activeCall.InstrumentToken], _activeCall.LastPrice, false);
                                PurgeTriggers(_longTrigerred[_activeCall.InstrumentToken], _activePut.LastPrice, true);
                                PurgeTriggers(_shortTrigerred[_activePut.InstrumentToken], _activePut.LastPrice, false);
                                PurgeTriggers(_longTrigerred[_activePut.InstrumentToken], _activePut.LastPrice, true);
                            }


                            //UpdateFuturePrice(tick.LastPrice, token);
                            //if (_referenceIndex.InstrumentToken == token)
                            //{
                            //    MonitorCandles(tick, currentTime);
                            //    if (!_tradeSuspended) 
                            //        TakeTrade(currentTime);

                            //    TradeTP(currentTime);

                            //    PurgeTriggers(_shortTrigerred, _referenceIndex.LastPrice, false);
                            //    PurgeTriggers(_longTrigerred, _referenceIndex.LastPrice, true);
                        }

#if BACKTEST
                    }
#endif
                    TriggerEODPositionClose(currentTime);

                    
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
            //Senario 1: Current red candle and previous red candle
            //2: Current Red candle and previous green candle
            //3: Current Green Candle and previous green candle
            //4: Current Green Candle and previous red candle

            if (e.InstrumentToken == _activeCall.InstrumentToken || e.InstrumentToken == _activePut.InstrumentToken)
            {
                uint token = e.InstrumentToken;
                //Trigger trigger = new Trigger();

                if (_pCandle[token] == null)
                {
                    _pCandle[token] = e;
                    return;
                }
                else if (
                    (_pCandle[token].ClosePrice > _pCandle[token].OpenPrice) ? (((Math.Max(e.HighPrice, _pCandle[token].HighPrice) - _pCandle[token].LowPrice) * 0.382m > Math.Abs((e.LowPrice - Math.Max(e.HighPrice, _pCandle[token].HighPrice)))) 
                    || ( (_pCandle[token].HighPrice - _pCandle[token].LowPrice) * 0.382m > Math.Abs(_pCandle[token].HighPrice - e.LowPrice) )) :
                    (((_pCandle[token].HighPrice - Math.Min(_pCandle[token].LowPrice, e.LowPrice)) * 0.382m > Math.Abs((e.HighPrice - Math.Min(_pCandle[token].LowPrice, e.LowPrice)))) 
                    || ( (_pCandle[token].HighPrice- _pCandle[token].LowPrice) *.382m > Math.Abs (_pCandle[token].LowPrice - e.HighPrice) ) )
                    )
                {
                    _pCandle[token] = JoinCandle(_pCandle[token], e);
                    return;
                }


                //PurgeTriggers(_shortTrigerred[token], _referenceIndex.LastPrice, false);
                //PurgeTriggers(_longTrigerred[token], _referenceIndex.LastPrice, true);

                //Two red Candles
                if (e.ClosePrice <  e.OpenPrice && _pCandle[token].ClosePrice< _pCandle[token].OpenPrice)
                {
                    //low breakdown
                    if(e.ClosePrice < _pCandle[token].LowPrice)
                    {
                        decimal sl = Math.Min(e.HighPrice, _pCandle[token].HighPrice); //e.HighPrice  (e.HighPrice > (_pCandle[token].HighPrice - (_pCandle[token].HighPrice - _pCandle[token].LowPrice) * 0.615m) ? e.HighPrice : _pCandle[token].HighPrice);
                        decimal target = _pCandle[token].LowPrice - (sl - _pCandle[token].LowPrice) * 0.768m;

                        //Trigger trigger = new Trigger() { tradePrice = _pCandle[token].LowPrice, sl = sl, tp = target, traded = false, up = false };
                        //_shortTrigerred[token] ??= new List<Trigger>();
                        //_shortTrigerred[token].Add(trigger);
                    }
                }
                //Two green Candles
                else if (e.ClosePrice > e.OpenPrice && _pCandle[token].ClosePrice > _pCandle[token].OpenPrice)
                {
                    //high breakout
                    if (e.ClosePrice > _pCandle[token].HighPrice)
                    {
                        decimal sl = Math.Max(e.LowPrice, _pCandle[token].LowPrice); //(e.LowPrice < (_pCandle[token].LowPrice + (_pCandle[token].HighPrice - _pCandle[token].LowPrice) * 0.615m) ? e.LowPrice : _pCandle[token].LowPrice);
                        //decimal target = 2 * _pCandle[token].HighPrice - sl;
                        decimal target = _pCandle[token].HighPrice + (_pCandle[token].HighPrice - sl)*0.768m;

                        Trigger trigger = new Trigger() { tradePrice = _pCandle[token].HighPrice, sl = sl, tp = target, traded = false, up = true };
                        _longTrigerred[token] ??= new List<Trigger>();
                        _longTrigerred[token].Add(trigger);
                    }
                }
                //Current green previous red
                else if (e.ClosePrice > e.OpenPrice && _pCandle[token].ClosePrice < _pCandle[token].OpenPrice)
                {
                    //high breakout
                    if (e.ClosePrice > _pCandle[token].HighPrice)
                    {
                        decimal sl = Math.Min(e.LowPrice, _pCandle[token].LowPrice);
                        decimal target = _pCandle[token].HighPrice + (_pCandle[token].HighPrice - sl)*0.768m;

                        Trigger trigger = new Trigger() { tradePrice = _pCandle[token].HighPrice, sl = sl, tp = target, traded = false, up = true };
                        _longTrigerred[token] ??= new List<Trigger>();
                        _longTrigerred[token].Add(trigger);
                    }
                }
                //Current red previous green
                else if (e.ClosePrice < e.OpenPrice && _pCandle[token].ClosePrice > _pCandle[token].OpenPrice)
                {
                    //low breakdown
                    if (e.ClosePrice < _pCandle[token].LowPrice)
                    {
                        decimal sl = Math.Max(e.HighPrice, _pCandle[token].HighPrice);
                        decimal target = _pCandle[token].LowPrice - (sl - _pCandle[token].LowPrice)*0.768m;

                        //Trigger trigger = new Trigger() { tradePrice = _pCandle[token].LowPrice, sl = sl, tp = target, traded = false, up = false };
                        //_shortTrigerred[token] ??= new List<Trigger>();
                        //_shortTrigerred[token].Add(trigger);
                    }
                }

                if (e.InstrumentToken == _activeCall.InstrumentToken)
                {
                    TradeSL(e.CloseTime, _callOrderTrios, e.ClosePrice);
                }
                else
                {
                    TradeSL(e.CloseTime, _putOrderTrios, e.ClosePrice);
                }

                _pCandle[token] = e;

                
            }    

        }
        private void PurgeTriggers(List<Trigger> triggers, decimal lastPrice, bool longTrigger)
        {
            for (int t = 0; t < triggers.Count; t++)
            {
                var trigger = triggers[t];

                if ((longTrigger && (trigger.sl > lastPrice || trigger.tp < lastPrice)) || (!longTrigger && (trigger.sl < lastPrice || trigger.tp > lastPrice)))
                {
                    triggers.RemoveAt(t);
                    t--;
                }
            }
        }

        private bool IsWickQualified(decimal wick)
        {
            return wick > QUALIFICATION_ZONE_THRESHOLD;
        }

        private void TradeTP(DateTime currentTime, List<OrderTrio> orderTrios)
        {
            bool _intraday = true;
            if (orderTrios.Count > 0)
            {
                for (int i = 0; i < orderTrios.Count; i++)
                {
                    OrderTrio orderTrio = orderTrios[i];
                    Instrument option = orderTrio.Option;

                    bool buy = orderTrio.Order.TransactionType.ToLower() == "buy";

                    if (buy ? option.LastPrice > orderTrio.TargetProfit : option.LastPrice < orderTrio.TargetProfit)
                        //&& ((option.InstrumentType.ToLower() == "ce" && _activeCall.LastPrice > orderTrio.TargetProfit)
                        //|| (option.InstrumentType.ToLower() == "pe" && _activePut.LastPrice > orderTrio.TargetProfit))
                        //)
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                                   option.KToken, !buy, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: Convert.ToString(orderTrio.Order.AveragePrice),
                                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                   broker: Constants.KOTAK,
                                   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        OnTradeExit(order);

                        _pnl += order.Quantity * order.AveragePrice * (!buy ? -1 : 1);

                        orderTrio.isActive = false;
                        orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                        DataLogic dl = new DataLogic();
                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                        dl.UpdateAlgoPnl(_algoInstance, _pnl);


                       // Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "fut", _activeFuture.LastPrice,
                       //_activeFuture.KToken, false, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: Convert.ToString(orderTrio.Order.AveragePrice),
                       //product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                       //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                        //OnTradeEntry(order);
                        orderTrios.Remove(orderTrio);

                        //_pnl += order.Quantity * order.AveragePrice * 1;

                        i--;
                    }
                    //else if (orderTrio.Order.TransactionType.ToLower() == "sell" && _referenceIndex.LastPrice < orderTrio.TargetProfit)
                    //{
                    //    Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                    //   _activeFuture.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: Convert.ToString(orderTrio.Order.AveragePrice),
                    //   product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                    //   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                    //    OnTradeEntry(order);
                    //    orderTrios.Remove(orderTrio);

                    //    _pnl += order.Quantity * order.AveragePrice * -1;

                    //    i--;
                    //}
                }

            }
        }

        private void TradeSL(DateTime CurrentTime, List<OrderTrio> orderTrios, decimal lastPrice)
        {
            if (orderTrios.Count > 0)
            {
                for (int i = 0; i < orderTrios.Count; i++)
                {
                    OrderTrio orderTrio = orderTrios[i];
                    Instrument option = orderTrio.Option;


                    bool buy = orderTrio.Order.TransactionType.ToLower() == "buy";
                    if (buy ? lastPrice < orderTrio.StopLoss : lastPrice > orderTrio.StopLoss)
                    {
                   //     Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                   //option.KToken, !buy, orderTrio.Order.Quantity, algoIndex, CurrentTime, Tag: Convert.ToString(orderTrio.Order.AveragePrice),
                   //product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                   //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                   //     OnTradeEntry(order);
                        orderTrios.Remove(orderTrio);

                        //_pnl += order.Quantity * order.AveragePrice * (!buy ? -1 : 1);

                        i--;

                        decimal newTarget, newsl;
                        //take opposite trade
                        if(buy)
                        {
                            newTarget = orderTrio.StopLoss - (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                            newsl = orderTrio.StopLoss + (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;

                            if (option.InstrumentType.ToLower() == "ce")
                            {
                                _shortTrigerred[_activeCall.InstrumentToken].Add(new Trigger() { sl = newsl, tp = newTarget, tradePrice = orderTrio.StopLoss, traded = false, up = false });
                            }
                            else
                            {
                                _shortTrigerred[_activePut.InstrumentToken].Add(new Trigger() { sl = newsl, tp = newTarget, tradePrice = orderTrio.StopLoss, traded = false, up = false });
                            }
                        }
                        else
                        {
                            newTarget = orderTrio.StopLoss + (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                            newsl = orderTrio.StopLoss - (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;

                            if (option.InstrumentType.ToLower() == "ce")
                            {
                                _longTrigerred[_activeCall.InstrumentToken].Add(new Trigger() { sl = newsl, tp = newTarget, tradePrice = orderTrio.StopLoss, traded = false, up = true });
                            }
                            else
                            {
                                //take opposite trade
                                //_longTrigerred[_activePut.InstrumentToken].Add(newTarget, newsl);

                                _longTrigerred[_activePut.InstrumentToken].Add(new Trigger() { sl = newsl, tp = newTarget, tradePrice = orderTrio.StopLoss, traded = false, up = true });
                            }
                        }


                        //if (option.InstrumentType.ToLower() == "ce")
                        //{
                        //    newTarget = orderTrio.StopLoss - (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                        //    newsl = orderTrio.StopLoss + (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                        //   _shortTrigerred[_activeCall.InstrumentToken].Add(newTarget, newsl);

                        //}
                        //else
                        //{
                        //    newTarget = orderTrio.StopLoss + (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                        //    newsl = orderTrio.StopLoss - (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                        //    //take opposite trade
                        //    _shortTrigerred[_activePut.InstrumentToken].Add(newTarget, newsl);
                        //}
                    }
                    // else if (orderTrio.Order.TransactionType.ToLower() == "sell" && e.ClosePrice > orderTrio.StopLoss)
                    // {
                    //     Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                    //_activeFuture.KToken, true, orderTrio.Order.Quantity, algoIndex, e.CloseTime, Tag: Convert.ToString(orderTrio.Order.AveragePrice),
                    //product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                    //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    //     OnTradeEntry(order);
                    //     _orderTrios.Remove(orderTrio);

                    //     _pnl += order.Quantity * order.AveragePrice * -1;

                    //     i--;

                    //     decimal newTarget = orderTrio.StopLoss + (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                    //     decimal newsl = orderTrio.StopLoss - (orderTrio.TargetProfit - orderTrio.StopLoss) / 1.768m;
                    //     //take opposite trade
                    //     _longTrigerred.Add(newTarget, newsl);
                    // }
                }

            }

        }
        //void UpdateFuturePrice(decimal lastPrice, uint token)
        //{
        //    if (_activeFuture.InstrumentToken == token)
        //    {
        //        _activeFuture.LastPrice = lastPrice;
        //    }
        //    else if (_referenceIndex.InstrumentToken == token)
        //    {
        //        _referenceIndex.LastPrice= lastPrice;
        //    }
        //    //_indexValues.Enqueue(lastPrice);
        //    //_bValues.Add(lastPrice);
        //    //_bValues.Add(new PriceTime() { LastPrice = lastPrice, TradeTime = currentTime });

        //    //if (_indexValues.Count > VALUES_THRESHOLD)
        //    //{
        //    //  _indexValues.Dequeue();
        //    //}
        //}

        private void UpdateOptionPrice(Tick tick)
        {
            bool optionFound = false;
            for (int i = 0; i < 2; i++)
            {
                foreach (var optionVar in OptionUniverse[i])
                {
                    Instrument option = optionVar.Value;
                    if (option != null && option.InstrumentToken == tick.InstrumentToken)
                    {
                        option.LastPrice = tick.LastPrice;
                        optionFound = true;
                        break;
                    }
                }
                if (optionFound)
                {
                    break;
                }
            }
        }


        private void ExecuteTrade(DateTime currentTime, uint token, decimal lastPrice, string instrumentType, Instrument optiontoTrade)
        {
            //uint token = tick.InstrumentToken;

            for(int i = 0;i < _longTrigerred[token].Count;i++)
            {
                var trigger = _longTrigerred[token][i];
                
                if (trigger.tp - lastPrice > _minimumTarget 
                    && (lastPrice <  trigger.tradePrice + (trigger.tp - trigger.tradePrice) * .236m) 
                    
                    )
                {
                    _longTrigerred[token].RemoveAt(i);

                    int qty = GetTradedQuantity(_tradeQty * Convert.ToInt32(_lotSize));// _baseInstrumentPrice, trigger.Value,);
                    if (qty <= 0)
                    {
                        return;
                    }

                    bool _intraday = true;
                    Instrument _atmOption = optiontoTrade;
                    
                    OrderTrio orderTrio = new OrderTrio();
                    orderTrio.Order = new Order();
                    orderTrio.Order.TransactionType = "buy";
                    //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, _atmOption.InstrumentType.ToLower(), _atmOption.LastPrice,
                    //   _atmOption.KToken, true, qty, algoIndex, currentTime,
                    //   Tag: "",
                    //   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                    //   broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    //OnTradeEntry(orderTrio.Order);

                    orderTrio.StopLoss = trigger.sl;
                    orderTrio.TargetProfit = trigger.tp;
                    orderTrio.Option = _atmOption;
                    orderTrio.EntryTradeTime = currentTime;
                    
                    orderTrio.Order.Quantity = qty;
                   // _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

                    //DataLogic dl = new DataLogic();
                    //orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    //dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    if (_activeCall.InstrumentToken == token)
                    {
                        _callOrderTrios.Add(orderTrio);
                    }
                    else
                    {
                        _putOrderTrios.Add(orderTrio);
                    }

                    //OrderTrio orderTrio = new OrderTrio();
                    //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                    //   _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "",
                    //   product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId, 
                    //   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    //orderTrio.StopLoss = trigger.Value;
                    //orderTrio.TargetProfit = trigger.Key;
                    //orderTrio.Option = _activeFuture;
                    //_orderTrios.Add(orderTrio);
                    //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", buy ? "Bought " : "Sold", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                    i--;
                }

            }
            for (int i = 0; i < _shortTrigerred[token].Count; i++)
            {
                var trigger = _shortTrigerred[token][i];

                if (lastPrice - trigger.tp > _minimumTarget && (lastPrice > trigger.tradePrice - (trigger.tradePrice - trigger.tp) * .236m))
                {
                    _shortTrigerred[token].RemoveAt(i);


                    int qty = GetTradedQuantity(_tradeQty * Convert.ToInt32(_lotSize));//, _baseInstrumentPrice, stopLoss);//, 0, _thirdPETargetActive);
                    if (qty <= 0)// || _baseInstrumentPrice < stopLoss)
                    {
                        return;
                    }

                    bool _intraday = true;
                    Instrument _atmOption = optiontoTrade;


                    OrderTrio orderTrio = new OrderTrio();
                    orderTrio.Order = new Order();
                    orderTrio.Order.TransactionType = "sell";
                    //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, _atmOption.InstrumentType.ToLower(), _atmOption.LastPrice,
                    //   _atmOption.KToken, false, qty, algoIndex, currentTime,
                    //   Tag: "",
                    //   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                    //   broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                    orderTrio.StopLoss = trigger.sl;
                    orderTrio.TargetProfit = trigger.tp;
                    orderTrio.Option = _atmOption;
                    orderTrio.EntryTradeTime = currentTime;

                    orderTrio.Order.Quantity = qty;
                    //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                    //DataLogic dl = new DataLogic();
                    //orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    //dl.UpdateAlgoPnl(_algoInstance, _pnl);
                    //_putOrderTrios.Add(orderTrio);

                    if (_activeCall.InstrumentToken == token)
                    {
                        _callOrderTrios.Add(orderTrio);
                    }
                    else
                    {
                        _putOrderTrios.Add(orderTrio);
                    }

                    //OrderTrio orderTrio = new OrderTrio();
                    //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                    //   _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "",
                    //   product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                    //   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    //orderTrio.StopLoss = trigger.Value;
                    //orderTrio.TargetProfit = trigger.Key;
                    //orderTrio.Option = _activeFuture;
                    //_orderTrios.Add(orderTrio);
                    //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", buy ? "Bought " : "Sold", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                    i--;
                }

            }
        }


        private Candle JoinCandle(Candle c1, Candle c2)
        {
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.InstrumentToken = c1.InstrumentToken;
            tC.OpenPrice = c1.OpenPrice;
            tC.OpenTime = c1.OpenTime;
            tC.ClosePrice = c2.ClosePrice;
            tC.CloseTime = c2.CloseTime;
            tC.HighPrice = Math.Max(c1.HighPrice, c2.HighPrice);
            tC.LowPrice = Math.Min(c1.LowPrice, c2.LowPrice);
            tC.State = CandleStates.Finished;
            return tC;
        }

        private int GetTradedQuantity(int totalQuantity)// decimal triggerLevel, decimal SL, )//, int bookedQty, bool thirdTargetActive)
        {
            int tradedQuantity = 0;

            //if (Math.Abs(triggerLevel - SL) < 90)
            //{
            //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //    tradedQuantity = totalQuantity;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
            //{
            //    tradedQuantity = totalQuantity * 2 / 4;
            //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}

            tradedQuantity = totalQuantity / 3;
            tradedQuantity -= tradedQuantity % 25;

            return tradedQuantity;// - bookedQty;
        }
        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay > new TimeSpan(15, 00, 00))
            {
                _tradeSuspended = true;
                if (currentTime.TimeOfDay >= new TimeSpan(15, 12, 00))
                {
                    CloseTrade(currentTime, _callOrderTrios);
                    CloseTrade(currentTime, _putOrderTrios);
                    DataLogic dl = new DataLogic();
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);
                    _pnl = 0;
                    _stopTrade = true;
                }


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
        //private void LoadBaseInstrumentEMA(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        //{
        //    try
        //    {
        //        lock (_indexSch)
        //        {
        //            DataLogic dl = new DataLogic();
        //            DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
        //            List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
        //            //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

        //            TimeFrameCandle c;

        //            foreach (var price in historicals)
        //            {
        //                c = new TimeFrameCandle();
        //                c.TimeFrame = new TimeSpan(0, _candleTimeSpan.Minutes, 0);
        //                c.ClosePrice = price.Close;
        //                c.HighPrice = price.High;
        //                c.LowPrice = price.Low;
        //                c.State = CandleStates.Finished;
        //                _indexSch.Process(c);//, isFinal: true);
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
        private void CloseTrade(DateTime currentTime, List<OrderTrio> orderTrios)
        {
            if (orderTrios.Count > 0)
            {
                for (int i = 0; i < orderTrios.Count; i++)
                {
                    var orderTrio = orderTrios[i];
                    Instrument option = orderTrio.Option;
                    if (orderTrio.Order.TransactionType == "buy")
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                       option.KToken, false, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: Convert.ToString(Guid.NewGuid()),
                       product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        OnTradeEntry(order);

                        _pnl += order.Quantity * order.AveragePrice * 1;

#if !BACKTEST
                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());


                        orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType == "sell")
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
           option.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: Convert.ToString(Guid.NewGuid()),
           product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
           httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                        OnTradeEntry(order);

#if !BACKTEST
                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _pnl += order.Quantity * order.AveragePrice * -1;

                        orderTrios.Remove(orderTrio);
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
        //    DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.Breakoutlive, Convert.ToDateTime("2021-11-30"), Convert.ToDateTime("2021-12-30"));

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

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, uint> mTokens;
                    DataLogic dl = new DataLogic();
                    
                   // _referenceIndex = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
                   // _referenceIndex.LotSize = 25;

                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out mTokens);

                    _lotSize = Convert.ToInt32(OptionUniverse[0].First().Value.LotSize);

                    if(_activeCall == null)
                    {
                        _activeCall = OptionUniverse[(int)InstrumentType.CE][Math.Floor(_baseInstrumentPrice / 100) * 100];

                        var slist = new List<Trigger>();
                        _shortTrigerred.Add(_activeCall.InstrumentToken, slist);
                        slist = new List<Trigger>();
                        _longTrigerred.Add(_activeCall.InstrumentToken, slist);

                        _pCandle.Add(_activeCall.InstrumentToken, null);
                        
                    }
                    if (_activePut == null)
                    {
                        _activePut = OptionUniverse[(int)InstrumentType.PE][Math.Ceiling(_baseInstrumentPrice / 100) * 100];
                        var slist = new List<Trigger>();
                        _shortTrigerred.Add(_activePut.InstrumentToken, slist);
                        slist = new List<Trigger>();
                        _longTrigerred.Add(_activePut.InstrumentToken, slist);

                        _pCandle.Add(_activePut.InstrumentToken, null);
                    }

                    //MappedTokens = mTokens;

                    //if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Option != null
                    //    && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_straddleCallOrderTrio.Option.Strike))
                    //{
                    //    OptionUniverse[(int)InstrumentType.CE].Add(_straddleCallOrderTrio.Option.Strike, _straddleCallOrderTrio.Option);
                    //}
                    //if (_straddlePutOrderTrio != null && _straddlePutOrderTrio.Option != null
                    //    && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_straddlePutOrderTrio.Option.Strike))
                    //{
                    //    OptionUniverse[(int)InstrumentType.PE].Add(_straddlePutOrderTrio.Option.Strike, _straddlePutOrderTrio.Option);
                    //}

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

        //        private void LoadFutureToTrade(DateTime currentTime)
        //        {
        //            try
        //            {
        //                if (_activeFuture == null)
        //                {

        //                    DataLogic dl = new DataLogic();
        //                    _referenceIndex = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
        //                    _referenceIndex.LotSize = 25;
        //#if BACKTEST

        //                    //_activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
        //                    _activeFuture = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
        //                    _activeFuture.LotSize = 25;
        //#else
        //                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
        //                    //Load options asynchronously
        //                    //DataLogic dl = new DataLogic();
        //                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
        //                    _activeFuture.LotSize = 25;
        //                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadFutureToTrade");
        //                    //OnCriticalEvents(currentTime.ToShortTimeString(), "Trade Started. Future Loaded.");
        //#endif
        //                }

        //            }
        //            catch (Exception ex)
        //            {
        //                _stopTrade = true;
        //                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //                Logger.LogWrite("Closing Application");
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
        //                Thread.Sleep(100);
        //            }
        //        }

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;

                //decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

                //Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][atmStrike];
                //if (_atmOption != null && !SubscriptionTokens.Contains(_atmOption.InstrumentToken))
                //{
                //    SubscriptionTokens.Add(_atmOption.InstrumentToken);
                //    dataUpdated = true;
                //}
                //_atmOption = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                //if (_atmOption != null && !SubscriptionTokens.Contains(_atmOption.InstrumentToken))
                //{
                //    SubscriptionTokens.Add(_atmOption.InstrumentToken);
                //    dataUpdated = true;
                //}
                if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                {
                    SubscriptionTokens.Add(_baseInstrumentToken);
                    dataUpdated = true;
                }
                foreach (var optionUniverse in OptionUniverse)
                {
                    foreach (var instrument in optionUniverse.Values)
                    {
                        if (!SubscriptionTokens.Contains(instrument.InstrumentToken))
                        {
                            SubscriptionTokens.Add(instrument.InstrumentToken);
                            dataUpdated = true;
                        }
                    }
                }
                if (dataUpdated)
                {
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Options", "UpdateInstrumentSubscription");
#endif
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
                //if (tick.Timestamp.HasValue && _currentDate.HasValue && tick.Timestamp.Value.Date.ToShortDateString() != _currentDate.Value.Date.ToShortDateString())
                //{
                //    ResetAlgo(tick.Timestamp.Value.Date);
                //}
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
