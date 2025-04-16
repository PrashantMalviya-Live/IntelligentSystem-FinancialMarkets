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
//using Twilio.Jwt.AccessToken;
using static Algorithms.Indicators.DirectionalIndex;
using Google.Protobuf.WellKnownTypes;
using Npgsql.Internal.TypeHandlers.DateTimeHandlers;

namespace Algorithms.Algorithms
{
    public class OptionSellonHT : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionSellonHT source);
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

        private List<OrderTrio> _callorderTrios, _putorderTrios;

        List<uint> _HTLoaded, _HTLoaded1, _SQLLoadingHT, _SQLLoadingHT1;

        public Dictionary<uint, Instrument> AllStocks { get; set; }
        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        Dictionary<uint, HalfTrend> _stockTokenHalfTrendLong;
        decimal _downtrendlowswingindexvalue = 0;
        decimal _uptrendhighswingindexvalue = 0;
        decimal _downtrendlowswingindexvalue1 = 0;
        decimal _uptrendhighswingindexvalue1 = 0;
        bool _longTermTrendEstablished = false;
        //Dictionary<uint, HalfTrend> _stockTokenHalfTrendLong;
        //Dictionary<uint, HalfTrend> _stockTokenHT;
        

        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        private CentralPivotRange _rcpr;
        private CentralPivotRange _rweeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        TimeSpan _candleTimeSpanShort, _candleTimeSpanLong;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private bool _ibLoaded = false;
        public decimal _minDistanceFromBInstrument;
        public decimal _maxDistanceFromBInstrument;
        public Dictionary<uint, uint> MappedTokens { get; set; }
        public OrderTrio _straddleCallOrderTrio;
        public OrderTrio _straddlePutOrderTrio;

        public List<Instrument> ActiveOptions { get; set; }
        public Dictionary<uint, Instrument> AllOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }


        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);
        public readonly TimeSpan EXTREME_END_TIME = new TimeSpan(12, 00, 0);
        public readonly TimeSpan INITIAL_BALANCE_END_TIME = new TimeSpan(11, 00, 0);
        //public bool _firstCETrade = true;
        //public bool _firstPETrade = true;
        string _lastOrderTransactionType = "";
        decimal _initialStopLoss = 0;
        private bool _referenceCandleLoaded = false;
        private bool _baseCandleLoaded = false;
        private Candle _smallCandle, _bigCandle;
        //private Candle _bCandle;
        //public Candle _rCandle;
        //decimal _previoushtred = 2;
        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty, _putTradedQty = 0, _callTradedQty = 0;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private decimal _ibLowestPrice = 1000000, _ibHighestPrice;
        private decimal _ibrLowestPrice = 1000000, _ibrHighestPrice;
        //private decimal _swingLowPrice = 1000000, _swingHighPrice;
        private decimal _rswingLowPrice = 1000000, _rswingHighPrice;
        private User _user;

        private readonly TimeSpan TRADING_WINDOW_START = new TimeSpan(09, 45, 0);
        //11 AM to 1Pm suspension
        private readonly TimeSpan SUSPENSION_WINDOW_START = new TimeSpan(17, 00, 0);
        private readonly TimeSpan SUSPENSION_WINDOW_END = new TimeSpan(18, 00, 0);
        private readonly int STOP_LOSS_POINTS = 130;//30; //40 points max loss on an average 
        private readonly int STOP_LOSS_POINTS_GAMMA = 130;//30;
        private const int DAY_STOP_LOSS_PERCENT = 1; //40 points max loss on an average 

        private decimal _maxLossForDay = 200000;

        private const int HIGH_NUMBER = int.MaxValue;
        private decimal _ceReferencePrice = 0, _peReferencePrice = HIGH_NUMBER;
        private decimal _ceSLReference = 0, _peSLReference = 0;
        private bool _ceSLTrigerred = false, _peSLTrigerred = false;

        //25% quantity booked in each target
        //private const decimal PRE_FIRST_TARGET = 30;
        //bool _preFirstPETargetActive = true;
        //bool _preFirstCETargetActive = true;
        //decimal _preFirstTargetQty = 0.15m;

        //25% quantity booked in each target
        private const decimal FIRST_TARGET = 40;
        bool _firstPETargetActive = true;
        bool _firstCETargetActive = true;
        decimal _firstTargetQty = 0.25m;

        private const decimal SECOND_TARGET = 50;
        bool _secondPETargetActive = true;
        bool _secondCETargetActive = true;
        decimal _secondTargetQty = 0.5m;

        private const decimal THIRD_TARGET = 65;
        bool _thirdPETargetActive = true;
        bool _thirdCETargetActive = true;
        decimal _thirdTargetQty = 0.25m;

        private decimal _pnl = 0;
        private Candle _prCandle;
        int _swingCounter = 60;
        int _rswingCounter = 60;
        uint _lotSize;
        private enum MarketScenario
        {
            Bullish = 1,
            Neutral = 0,
            Bearish = -1
        }
        private MarketScenario _extremeScenario = MarketScenario.Neutral;
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private Candle _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private SortedList<decimal, int> _rcriticalLevels;
        private SortedList<decimal, int> _rcriticalLevelsWeekly;
        private bool _putTriggered = false;
        private bool _callTrigerred = false;
        private bool _triggerPutTrade = false;
        private bool _triggerCallTrade = false;

        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded, _firstCandleOpenPriceNeeded1;
        public const AlgoIndex algoIndex = AlgoIndex.OptionSellonHT;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private int _putProfitBookedQty = 0;
        private int _callProfitBookedQty = 0;
        private bool? _firstCandleOutsideRange;
        CandleManger candleManger;//, candleManager1;
        Dictionary<uint, List<Candle>> TimeCandles;//, TimeCandles1;
        private IHttpClientFactory _httpClientFactory;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        FirebaseMessaging _firebaseMessaging;
        ExponentialMovingAverage _indexEMA;
        ExponentialMovingAverage _rindexEMA;
        bool _indexEMALoaded = false;
        bool _rindexEMALoaded = false;
        bool _indexEMALoading = false;
        bool _indexEMALoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;
        private IIndicatorValue _rindexEMAValue;
        private uint _referenceBToken = 0;
        private bool _intraday;
        private decimal nextTriggerLevel;
        private decimal _trailProfit = 10000;
        IEnumerable<Candle> _historicalcandles = null;
        public OptionSellonHT(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            bool intraday, decimal pnl,
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;

            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);

            _candleTimeSpan = candleTimeSpan;
            if(_candleTimeSpan.TotalMinutes == 5)
            {
                STOP_LOSS_POINTS = 130;
                STOP_LOSS_POINTS_GAMMA = 130;
            }

            _candleTimeSpanLong = new TimeSpan(0, 30, 0);


            _baseInstrumentToken = baseInstrumentToken;

            _stopTrade = true;
            _trailingStopLoss = _stopLoss = stopLoss;
            _targetProfit = targetProfit;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;
            _intraday = intraday;
            SetUpInitialData(expiry, algoInstance);
            _pnl = pnl;

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

            _callorderTrios = new List<OrderTrio>();
            _putorderTrios = new List<OrderTrio>();

            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();
            _rcriticalLevels = new SortedList<decimal, int>();
            _rcriticalLevelsWeekly = new SortedList<decimal, int>();

            _indexEMA = new ExponentialMovingAverage(length: 20);
            
            _maxDistanceFromBInstrument = 800;
            _minDistanceFromBInstrument = 300;

            _stockTokenHalfTrend = new Dictionary<uint, HalfTrend>();
            _stockTokenHalfTrendLong = new Dictionary<uint, HalfTrend>();

            _HTLoaded = new List<uint>();
            _SQLLoadingHT = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            _HTLoaded1 = new List<uint>();
            _SQLLoadingHT1 = new List<uint>();
            _firstCandleOpenPriceNeeded1 = new Dictionary<uint, bool>();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            

            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            //TimeCandles1 = new Dictionary<uint, List<Candle>>();
            //candleManager1 = new CandleManger(TimeCandles1, CandleType.Time);
            //candleManager1.TimeCandleFinished += CandleManager1_TimeCandleFinished; ;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, _intraday ? 1 : 0,
                0, 0, Arg9: _user.UserId, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

        }

       

        public void LoadActiveOrders(List<OrderTrio> activeOrderTrios)
        {
            if (activeOrderTrios != null)
            {
                DataLogic dl = new DataLogic();
                foreach (OrderTrio orderTrio in activeOrderTrios)
                {
                    Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                    orderTrio.Option = option;
                    if (option.InstrumentType.ToLower() == "ce")
                    {
                        _callorderTrios.Add(orderTrio);
                    }
                    else if (option.InstrumentType.ToLower() == "pe")
                    {
                        _putorderTrios.Add(orderTrio);
                    }
                }
            }
        }


        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken) ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                if (currentTime.TimeOfDay > MARKET_START_TIME)
                {
                    uint token = tick.InstrumentToken;
                    lock (tradeLock)
                    {
                        if (!GetBaseInstrumentPrice(tick))
                        {
                            return;
                        }

                        LoadOptionsToTrade(currentTime);
                        if (OptionUniverse != null)
                        {
                            UpdateInstrumentSubscription(currentTime);
                        }
#if BACKTEST
                        if (SubscriptionTokens.Contains(token))
                        {
#endif
                        if (!_HTLoaded.Contains(token) && token == _baseInstrumentToken)
                        {
                            LoadHistoricalHT(currentTime, _candleTimeSpan, _HTLoaded, _SQLLoadingHT, _firstCandleOpenPriceNeeded, _stockTokenHalfTrend, TimeCandles);
                        }
                            if (!_HTLoaded1.Contains(token) && token == _baseInstrumentToken)
                            {
                                LoadHistoricalHT(currentTime, _candleTimeSpanLong, _HTLoaded1, _SQLLoadingHT1, _firstCandleOpenPriceNeeded1, _stockTokenHalfTrendLong, TimeCandles);
                            }

                            if (OptionUniverse != null)
                            {
                                UpdateOptionPrice(tick);
                                decimal htrend = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend();
                                if (tick.InstrumentToken == _baseInstrumentToken)
                                {
                                    MonitorCandles(tick, currentTime, _candleTimeSpan, TimeCandles, candleManger);
                                    //MonitorCandles(tick, currentTime, _candleTimeSpanLong, TimeCandles1, candleManager1);

                                    //TakeTrade(currentTime);
                                    ExecuteTrade(tick, currentTime);

                                    //Stop loss points and day stop loss
                                    //if (BookStopLoss(_putorderTrios))
                                    if (_putorderTrios.Count > 0
                               && BookStopLoss(_putorderTrios, htrend == 0, currentTime)
                               )
                                    {
                                        _putProfitBookedQty = 0;
                                        _putTriggered = false;
                                        _triggerPutTrade = false;
                                    }
                                    //if (BookStopLoss(_callorderTrios))
                                    if (_callorderTrios.Count > 0
                               && BookStopLoss(_callorderTrios, htrend == 1, currentTime)
                               )
                                    {
                                        _callProfitBookedQty = 0;
                                        _callTrigerred = false;
                                        _triggerCallTrade = false;
                                    }
                                }
                                else
                                {
                                    //_putProfitBookedQty += BookPartialProfit(_putorderTrios, tick);
                                    //_callProfitBookedQty += BookPartialProfit(_callorderTrios, tick);
                                    _putProfitBookedQty += BookPartialProfit(_putorderTrios, tick, htrend == 1, currentTime,
                                    /*ref _preFirstPETargetActive,*/ ref _firstPETargetActive, ref _secondPETargetActive, ref _thirdPETargetActive);
                                    _callProfitBookedQty += BookPartialProfit(_callorderTrios, tick, htrend == 0, currentTime,
                                        /*ref _preFirstCETargetActive,*/ ref _firstCETargetActive, ref _secondCETargetActive, ref _thirdCETargetActive);
                                }
                            }

                            CheckMaxLoss(currentTime);

#if BACKTEST
                        }
#endif
                    }
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
                    uint token = e.InstrumentToken;
                    _baseCandleLoaded = true;
                    //_bCandle = e;
                    decimal previoushtred1 = 0;
                    decimal htred1 = 0, htvalue1 = 0;
                    if (_stockTokenHalfTrendLong.ContainsKey(token))
                    {
                        _longTermTrendEstablished = true;
                        htred1 = _stockTokenHalfTrendLong[token].GetTrend(); // 0 is up trend

                        if ((e.CloseTime.Minute - MARKET_START_TIME.Minutes) % _candleTimeSpanLong.Minutes == 0)
                        {
                            _bigCandle = GetLastCandle(_candleTimeSpanLong.Minutes / _candleTimeSpan.Minutes, e.CloseTime);
                            previoushtred1 = _stockTokenHalfTrendLong[_bigCandle.InstrumentToken].GetTrend(); // 0 is up trend
                            _stockTokenHalfTrendLong[_bigCandle.InstrumentToken].Process(_bigCandle);
                            htred1 = _stockTokenHalfTrendLong[token].GetTrend(); // 0 is up trend


                            if (TimeCandles[token].Count == _candleTimeSpanLong.Minutes)
                            {
                                _downtrendlowswingindexvalue1 = _bigCandle.LowPrice;
                                _uptrendhighswingindexvalue1 = _bigCandle.HighPrice;
                            }

                            if (htred1 == 0 && _bigCandle.HighPrice > _uptrendhighswingindexvalue1)
                            {
                                _uptrendhighswingindexvalue1 = _bigCandle.HighPrice;
                            }
                            if (htred1 == 1 && (_bigCandle.LowPrice < _downtrendlowswingindexvalue1 || _downtrendlowswingindexvalue1 == 0))
                            {
                                _downtrendlowswingindexvalue1 = _bigCandle.LowPrice;
                            }
                        }
                    }
                    if (_stockTokenHalfTrend.ContainsKey(token))
                    {
                        //_smallCandle = GetLastCandle(_candleTimeSpan.Minutes, e.CloseTime);
                        _smallCandle = e;
                        decimal previoushtred = _stockTokenHalfTrend[token].GetTrend(); // 0 is up trend
                        _stockTokenHalfTrend[token].Process(_smallCandle);
                        decimal htred = _stockTokenHalfTrend[token].GetTrend(); // 0 is up trend
                        
                       // decimal htvalue = _stockTokenHalfTrend[token].GetCurrentValue<decimal>(); // 0 is up trend

                        bool suspensionWindow = _smallCandle.CloseTime.TimeOfDay > SUSPENSION_WINDOW_START && _smallCandle.CloseTime.TimeOfDay < SUSPENSION_WINDOW_END;

                        if (TimeCandles[token].Count == 1)
                        {
                            _downtrendlowswingindexvalue = _smallCandle.LowPrice;
                            _uptrendhighswingindexvalue = _smallCandle.HighPrice;
                        }

                        if (htred == 0 )//&& _smallCandle.HighPrice > _uptrendhighswingindexvalue)
                        {
                            _downtrendlowswingindexvalue = Math.Min(_downtrendlowswingindexvalue, _smallCandle.LowPrice);
                            _uptrendhighswingindexvalue = Math.Max(_uptrendhighswingindexvalue, _smallCandle.HighPrice);
                        }
                        if (htred == 1)// && (_smallCandle.LowPrice < _downtrendlowswingindexvalue || _downtrendlowswingindexvalue == 0))
                        {
                            //_downtrendlowswingindexvalue = _smallCandle.LowPrice;
                            _downtrendlowswingindexvalue = _downtrendlowswingindexvalue != 0 ? Math.Min(_downtrendlowswingindexvalue, _smallCandle.LowPrice): _smallCandle.LowPrice;
                            _uptrendhighswingindexvalue = Math.Max(_uptrendhighswingindexvalue, _smallCandle.HighPrice);
                        }


                        //if (_smallCandle.CloseTime.TimeOfDay > TRADING_WINDOW_START)
                        //{
                        //Uptrend
                        //Sell Put if not sold. do not close call unless SL hit
                        //if (previoushtred == 1 && htred == 0)
                        //{
                        if (_putTriggered)// && _smallCandle.ClosePrice > _peReferencePrice + 6)
                        {
                            _callTrigerred = false;
                            _triggerCallTrade = false;
                            //   _putTriggered = true;
                            _peReferencePrice = HIGH_NUMBER;
                            _triggerPutTrade = true;

                            _uptrendhighswingindexvalue = _smallCandle.HighPrice;
                            _firstPETargetActive = true;
                            _secondPETargetActive = true;
                            _thirdPETargetActive = true;

                            if (_putorderTrios.Count != 0)
                            {
                                //LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                                OrderTrio orderTrio = _putorderTrios[0];
                                Instrument option = orderTrio.Option;

                                //No ITM selling
                                if (_downtrendlowswingindexvalue > _baseInstrumentPrice)
                                {
                                    return;
                                }

                                decimal atmStrike = GetPEStrike(_baseInstrumentPrice, _downtrendlowswingindexvalue);// Math.Floor((_downtrendlowswingindexvalue - 50) / 100) * 100;// Math.Floor(_swingLowPrice / 100) * 100;

                                //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                                //{
                                //    atmStrike = Math.Floor((_downtrendlowswingindexvalue - 25) / 50) * 50;// Math.Floor(_swingLowPrice / 50) * 50; 
                                //}

                                int qty = orderTrio.Order.Quantity;
                                
                                int newQty = GetTradedQuantity(_baseInstrumentPrice, _downtrendlowswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdPETargetActive);

                                if (option.Strike != atmStrike && newQty > 0)
                                {
                                    DataLogic dl = new DataLogic();
                                    for (int i = 0; i < _putorderTrios.Count; i++)
                                    {
                                        orderTrio = _putorderTrios[i];
                                        qty = orderTrio.Order.Quantity;

                                        //buyback existing option, and sell at new swing low
                                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                                       option.KToken, true, qty, algoIndex, _smallCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                        OnTradeExit(orderTrio.Order);

                                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

                                        orderTrio.isActive = false;
                                        orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                    }
                                    _putorderTrios.Clear();
                                    _putProfitBookedQty = 0;
                                    _uptrendhighswingindexvalue = 0;
                                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                                    qty = GetTradedQuantity(_baseInstrumentPrice, _downtrendlowswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdPETargetActive);

                                    if (qty < _tradeQty * Convert.ToInt32(_lotSize))
                                    {
                                        _putTriggered = true;
                                    }

                                    orderTrio = new OrderTrio();
                                    //buyback existing option, and sell at new swing low
                                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
                                       _atmOption.KToken, false, qty, algoIndex, _smallCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                    orderTrio.Option = _atmOption;
                                    orderTrio.StopLoss = _downtrendlowswingindexvalue;
                                    _initialStopLoss = _downtrendlowswingindexvalue;
                                    //first target is 1:1 R&R
                                    orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _downtrendlowswingindexvalue;
                                    _lastOrderTransactionType = "sell";
                                    orderTrio.EntryTradeTime = e.CloseTime;

                                    OnTradeExit(orderTrio.Order);

                                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                                    _putorderTrios.Add(orderTrio);
                                }
                                else
                                {
                                    int totalTradedQty = _putorderTrios.Sum(x => x.Order.Quantity);
                                    int shouldbeQty = GetTradedQuantity(_baseInstrumentPrice, _downtrendlowswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdPETargetActive);

                                    if (shouldbeQty > totalTradedQty)
                                    {
                                        int addtionalQty = shouldbeQty - totalTradedQty;
                                        orderTrio = new OrderTrio();
                                        //sell addtionalqty
                                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                                           option.KToken, false, addtionalQty, algoIndex, _smallCandle.CloseTime,
                                           Tag: "",
                                           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                           broker: Constants.KOTAK,
                                           httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                        orderTrio.Option = option;
                                        orderTrio.StopLoss = _putorderTrios[0].StopLoss;
                                        _initialStopLoss = orderTrio.StopLoss;
                                        orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                        //first target is 1:1 R&R
                                        orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _downtrendlowswingindexvalue;
                                        _lastOrderTransactionType = "sell";

                                        _putProfitBookedQty = 0;
                                        _uptrendhighswingindexvalue = 0;

                                        OnTradeExit(orderTrio.Order);

                                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                                        DataLogic dl = new DataLogic();
                                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);

                                        _putorderTrios.Add(orderTrio);
                                    }
                                }

                            }
                        }
                        //else if (previoushtred == 0 && htred == 1)
                        else if (_callTrigerred)// && _smallCandle.ClosePrice < _ceReferencePrice - 6)
                        {
                            _ceReferencePrice = 0;
                            _putTriggered = false;
                            _triggerPutTrade = false;
                            //_callTrigerred = true;
                            _triggerCallTrade = true;


                            _downtrendlowswingindexvalue = _smallCandle.LowPrice;
                            _thirdCETargetActive = true;
                            _firstCETargetActive = true;
                            _secondCETargetActive = true;
                            if (_callorderTrios.Count != 0)
                            {
                                //LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                                OrderTrio orderTrio = _callorderTrios[0];
                                Instrument option = orderTrio.Option;

                                //No ITM selling
                                if (_baseInstrumentPrice > _uptrendhighswingindexvalue)
                                {
                                    return;
                                }
                                decimal atmStrike = GetCEStrike(_baseInstrumentPrice, _uptrendhighswingindexvalue);// Math.Ceiling((_uptrendhighswingindexvalue + 50) / 100) * 100;// Math.Ceiling(_swingHighPrice / 100) * 100;
                                                                                                                   //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                                                                                                                   //{
                                                                                                                   //    atmStrike = Math.Ceiling((_uptrendhighswingindexvalue + 25) / 50) * 50;// Math.Ceiling(_swingHighPrice / 50) * 50;
                                                                                                                   //}

                                int qty = orderTrio.Order.Quantity;

                                int newQty = GetTradedQuantity(_baseInstrumentPrice, _uptrendhighswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdCETargetActive);
                                if (option.Strike != atmStrike && newQty > 0)
                                {
                                    DataLogic dl = new DataLogic();
                                    for (int i = 0; i < _callorderTrios.Count; i++)
                                    {
                                        orderTrio = _callorderTrios[i];
                                        qty = orderTrio.Order.Quantity;

                                        //buyback existing option, and sell at new swing low
                                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                                       option.KToken, true, qty, algoIndex, _smallCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                        OnTradeExit(orderTrio.Order);

                                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

                                        orderTrio.isActive = false;
                                        orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                    }

                                    _callorderTrios.Clear();
                                    _callProfitBookedQty = 0;
                                    _downtrendlowswingindexvalue = 0;
                                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                                    qty = GetTradedQuantity(_baseInstrumentPrice, _uptrendhighswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdCETargetActive);

                                    if (qty < _tradeQty * Convert.ToInt32(_lotSize))
                                    {
                                        _callTrigerred = true;
                                        _triggerCallTrade = true;
                                    }

                                    orderTrio = new OrderTrio();
                                    //buyback existing option, and sell at new swing low
                                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
                                       _atmOption.KToken, false, qty, algoIndex, _smallCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                    orderTrio.Option = _atmOption;
                                    orderTrio.StopLoss = _uptrendhighswingindexvalue;
                                    _initialStopLoss = _uptrendhighswingindexvalue;
                                    orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                    //first target is 1:1 R&R
                                    orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _uptrendhighswingindexvalue;
                                    _lastOrderTransactionType = "sell";

                                    OnTradeExit(orderTrio.Order);

                                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                                    _callorderTrios.Add(orderTrio);
                                }
                                else
                                {
                                    int totalTradedQty = _callorderTrios.Sum(x => x.Order.Quantity);
                                    int shouldbeQty = GetTradedQuantity(_baseInstrumentPrice, _uptrendhighswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdCETargetActive);

                                    if (shouldbeQty > totalTradedQty)
                                    {
                                        int addtionalQty = shouldbeQty - totalTradedQty;
                                        orderTrio = new OrderTrio();
                                        //sell addtionalqty
                                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                                           option.KToken, false, addtionalQty, algoIndex, _smallCandle.CloseTime,
                                           Tag: "",
                                           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                           broker: Constants.KOTAK,
                                           httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                                        orderTrio.Option = option;
                                        orderTrio.StopLoss = _callorderTrios[0].StopLoss;
                                        _initialStopLoss = orderTrio.StopLoss;
                                        orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                        //first target is 1:1 R&R
                                        orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _uptrendhighswingindexvalue;
                                        _lastOrderTransactionType = "sell";

                                        OnTradeExit(orderTrio.Order);

                                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                                        DataLogic dl = new DataLogic();
                                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);

                                        _callProfitBookedQty = 0;
                                        _downtrendlowswingindexvalue = 0;

                                        _callorderTrios.Add(orderTrio);
                                    }
                                }
                            }
                        }
                        //This else is to be used only for first trade where the first trend is persistent.

                        if (_longTermTrendEstablished && htred1 == 0 && previoushtred == 1 && htred == 0)
                        {
                            htred1 = _stockTokenHalfTrendLong[token].GetTrend(); // 0 is up trend
                            htvalue1 = _stockTokenHalfTrendLong[token].GetCurrentValue<decimal>(); // 0 is up trend
                            _uptrendhighswingindexvalue = _smallCandle.HighPrice;
                            //if (_longTermTrendEstablished && htred1 == 0 && (_smallCandle.ClosePrice - htvalue1) < (_candleTimeSpanLong.Minutes == 5 ? 51 : 11))
                            //{
                                _putTriggered = true;
                                _peReferencePrice = _smallCandle.HighPrice;
                            //}
                        }
                        else if (_longTermTrendEstablished && htred1 == 1 && previoushtred == 0 && htred == 1 )
                        {
                            htred1 = _stockTokenHalfTrendLong[token].GetTrend(); // 0 is up trend
                            htvalue1 = _stockTokenHalfTrendLong[token].GetCurrentValue<decimal>(); // 0 is up trend

                            _downtrendlowswingindexvalue = _smallCandle.LowPrice;

                            //if (_longTermTrendEstablished && htred1 == 1 && (htvalue1 - _smallCandle.ClosePrice) < (_candleTimeSpanLong.Minutes == 5 ? 51 : 11))
                            //{
                                _callTrigerred = true;
                                _ceReferencePrice = _smallCandle.LowPrice;
                            //}
                        }
                        else
                        {
                            //if (_orderTrios.Count != 0)
                            //{
                            //    if (_orderTrios[0].Order.TransactionType.ToLower() == "sell")
                            //    {
                            //        _orderTrios[0].StopLoss = Math.Min(_swingHighPrice, _orderTrios[0].StopLoss);
                            //    }
                            //    else
                            //    {
                            //        _orderTrios[0].StopLoss = Math.Max(_swingLowPrice, _orderTrios[0].StopLoss);
                            //    }
                            //}
                        }
                        // }
                        if (_callorderTrios.Count != 0)
                        {
                            htvalue1 = _stockTokenHalfTrendLong[token].GetCurrentValue<decimal>(); // 0 is up trend

                            for (int i = 0; i < _callorderTrios.Count; i++)
                            {
                                var orderTrio = _callorderTrios[i];
                                if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" && _baseInstrumentPrice > Math.Min(orderTrio.StopLoss, htvalue1) && htred == 0)
                                {
                                    Instrument option = orderTrio.Option;

                                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, _smallCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                    OnTradeExit(order);

                                    _pnl += order.Quantity * order.AveragePrice * -1;

                                    orderTrio.isActive = false;
                                    orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                                    DataLogic dl = new DataLogic();
                                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                                    _callorderTrios.Remove(orderTrio);
                                    i--;
                                    //Since all ordertrios have same stoploss, all will get closed simultaneously
                                    _callProfitBookedQty = 0;
                                    _uptrendhighswingindexvalue = 0;
                                }
                            }
                        }
                        if (_putorderTrios.Count != 0)
                        {
                            htvalue1 = _stockTokenHalfTrendLong[token].GetCurrentValue<decimal>(); // 0 is up trend
                            for (int i = 0; i < _putorderTrios.Count; i++)
                            {
                                var orderTrio = _putorderTrios[i];
                                if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" && _baseInstrumentPrice < Math.Max(orderTrio.StopLoss, htvalue1) && htred == 1)
                                {
                                    Instrument option = orderTrio.Option;

                                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, _smallCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                    OnTradeExit(order);

                                    _pnl += order.Quantity * order.AveragePrice * -1;

                                    orderTrio.isActive = false;
                                    orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                                    DataLogic dl = new DataLogic();
                                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                                    _putorderTrios.Remove(orderTrio);
                                    i--;

                                    //Since all ordertrios have same stoploss, all will get closed simultaneously
                                    _putProfitBookedQty = 0;
                                    ///TODO: Check the impact of this time. THis is causing downtrend to not set again as it is setting to 0 here
                                    //_downtrendlowswingindexvalue = 0;
                                    _putTriggered = false;
                                }
                            }
                        }
                        //_previoushtred = htred;
                    }

                    if (_intraday || _expiryDate.Value.Date == _smallCandle.CloseTime.Date)
                    {
                        TriggerEODPositionClose(_smallCandle.CloseTime, _smallCandle.ClosePrice);
                    }
                    _baseCandleLoaded = false;
                    _referenceCandleLoaded = false;
                    _prCandle = _smallCandle;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.StackTrace);
            }
        }

        //private void CandleManager1_TimeCandleFinished(object sender, Candle e)
        //{
        //    try
        //    {
        //        if (e.InstrumentToken == _baseInstrumentToken)
        //        {
        //            _baseCandleLoaded = true;
        //            _rCandle = e;

        //            decimal htred1 = 0;
        //            decimal previoushtred1 = 0;
        //            if (_stockTokenHalfTrendLong.ContainsKey(_rCandle.InstrumentToken) )//&& (_rCandle.CloseTime.Minute - MARKET_START_TIME.Minutes) % _candleTimeSpanLong.Minutes == 0)
        //            {
        //                //_rCandle = GetLastCandle(_candleTimeSpanLong.Minutes / _candleTimeSpan.Minutes, _bCandle.CloseTime);
        //                previoushtred1 = _stockTokenHalfTrendLong[_rCandle.InstrumentToken].GetTrend(); // 0 is up trend
        //                _stockTokenHalfTrendLong[_rCandle.InstrumentToken].Process(_rCandle);
        //                htred1 = _stockTokenHalfTrendLong[_rCandle.InstrumentToken].GetTrend(); // 0 is up trend
        //                _longTermTrendEstablished = true;

        //                //if (TimeCandles[_rCandle.InstrumentToken].Count == _candleTimeSpanLong.Minutes/ _candleTimeSpan.Minutes)
        //                if (TimeCandles1[_rCandle.InstrumentToken].Count == 0)
        //                {
        //                    _downtrendlowswingindexvalue1 = _rCandle.LowPrice;
        //                    _uptrendhighswingindexvalue1 = _rCandle.HighPrice;
        //                }

        //                if (htred1 == 0 && _rCandle.HighPrice > _uptrendhighswingindexvalue1)
        //                {
        //                    _uptrendhighswingindexvalue1 = _rCandle.HighPrice;
        //                }
        //                if (htred1 == 1 && (_rCandle.LowPrice < _downtrendlowswingindexvalue1 || _downtrendlowswingindexvalue1 == 0))
        //                {
        //                    _downtrendlowswingindexvalue1 = _rCandle.LowPrice;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogWrite(ex.StackTrace);
        //    }
        //}
        private void ExecuteTrade(Tick tick, DateTime currentTime)
        {
            if (_stockTokenHalfTrend.ContainsKey(_baseInstrumentToken)
                && TimeCandles.ContainsKey(_baseInstrumentToken)
                 && (currentTime.TimeOfDay >= TRADING_WINDOW_START && currentTime.TimeOfDay <= new TimeSpan(15, 12, 00))
                )
            {
                decimal htred = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend(); // 0 is up trend
                decimal htvalue = _stockTokenHalfTrend[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend
                decimal htvalue1 = _stockTokenHalfTrendLong[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                int tradeQty = 0;
                if (htred == 0 )//&& _putTriggered)
                {
                    decimal stopLoss = 0;
                    if (_putorderTrios.Count == 0 && htvalue >= _baseInstrumentPrice - (_candleTimeSpan.Minutes == 5?31:11) && _putTriggered && _triggerPutTrade && _baseInstrumentPrice > htvalue1)
                    {
                        //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                        _firstPETargetActive = true;
                        _secondPETargetActive = true;
                        _thirdPETargetActive = true;

                        stopLoss = _downtrendlowswingindexvalue;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdPETargetActive);

                    }
                    else if (_putorderTrios.Count != 0 && (_baseInstrumentPrice - _putorderTrios[0].StopLoss < 300))
                    {
                        //// TODO: COMMENTED FOR NOT INCREASING QTY LATER
                        stopLoss = _putorderTrios[0].StopLoss;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), _putProfitBookedQty, _thirdPETargetActive) - _putorderTrios.Sum(x => x.Order.Quantity);

                        tradeQty = 0;

                        if (_putorderTrios.Sum(x => x.Order.Quantity) == _tradeQty * Convert.ToInt32(_lotSize))
                        {
                            _putTriggered = false;
                            _triggerPutTrade = false;
                            return;
                        }

                    }
                    //No ITM selling also
                    if (tradeQty <= 0 || _baseInstrumentPrice < stopLoss)
                    {
                        return;
                    }


                    //this line can be updated for postion sizing, based on the SL.
                    int qty = tradeQty;// _tradeQty;
                    //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);
                    OrderTrio orderTrio = new OrderTrio();

                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][GetPEStrike(_baseInstrumentPrice, stopLoss)]; //Change -  Take position 50 points farther to avoid gamma loss

                    //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                    //{
                    //    _atmOption = OptionUniverse[(int)InstrumentType.PE][Math.Floor((stopLoss - 25) / 50) * 50]; //Change -  Take position 50 points farther to avoid gamma loss
                    //}

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
                       _atmOption.KToken, false, qty, algoIndex, currentTime,
                       Tag: "",
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);
                    orderTrio.Option = _atmOption;
                    orderTrio.StopLoss = stopLoss;
                    _initialStopLoss = stopLoss;
                    orderTrio.EntryTradeTime = currentTime;
                    //first target is 1:1 R&R
                    orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - stopLoss;
                    _lastOrderTransactionType = "sell";
                    //_orderTrios.Clear();
                    //if (!suspensionWindow)
                    //{
                    //    _orderTrios.Add(orderTrio);
                    //}
                    orderTrio.Order.Quantity = qty;
                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);
                    _putorderTrios.Add(orderTrio);
                }
                else if (htred == 1)// && _callTrigerred)
                {
                    decimal stopLoss = 0;
                    if (_callorderTrios.Count == 0 && (htvalue <= _baseInstrumentPrice + (_candleTimeSpan.Minutes == 5 ? 31 : 11)) && _callTrigerred && _triggerCallTrade && _baseInstrumentPrice < htvalue1)
                    {
                        //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);
                        _firstCETargetActive = true;
                        _secondCETargetActive= true;
                        _thirdCETargetActive= true; 

                        stopLoss = _uptrendhighswingindexvalue;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdCETargetActive);
                    }
                    else if (_callorderTrios.Count != 0 && (_callorderTrios[0].StopLoss - _baseInstrumentPrice < 300))
                    {
                        //TODO: COMMENTED FOR NOT INCREASING QTY LATER
                        stopLoss = _callorderTrios[0].StopLoss;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), _callProfitBookedQty, _thirdCETargetActive) - _callorderTrios.Sum(x => x.Order.Quantity);

                        tradeQty = 0;
                        if (_callorderTrios.Sum(x => x.Order.Quantity) == _tradeQty * Convert.ToInt32(_lotSize))
                        {
                            _callTrigerred = false;
                            _triggerCallTrade = false;
                            return;
                        }
                    }
                    if (tradeQty <= 0 || _baseInstrumentPrice > stopLoss)
                    {
                        return;
                    }


                    //this line can be updated for postion sizing, based on the SL.
                    int qty = tradeQty;
                    //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                    OrderTrio orderTrio = new OrderTrio();
                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.CE][GetCEStrike(_baseInstrumentPrice, stopLoss)]; //Change -  Take position 50 points farther to avoid gamma loss

                    //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                    //{
                    //    _atmOption = OptionUniverse[(int)InstrumentType.CE][GetCEStrike(_baseInstrumentPrice, stopLoss)]; //Change -  Take position 50 points farther to avoid gamma loss
                    //}

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
                       _atmOption.KToken, false, qty, algoIndex, currentTime,
                       Tag: "",
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);
                    orderTrio.Option = _atmOption;
                    orderTrio.StopLoss = stopLoss;
                    _initialStopLoss = stopLoss;
                    orderTrio.EntryTradeTime = currentTime;
                    //first target is 1:1 R&R
                    orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - stopLoss;
                    _lastOrderTransactionType = "sell";

                    orderTrio.Order.Quantity = qty;
                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    _callorderTrios.Add(orderTrio);
                }
            }
        }

        private void CheckMaxLoss(DateTime currentTime)
        {
            decimal totalPnl = _pnl;
            if (_callorderTrios.Count > 0)
            {
                for (int i = 0; i < _callorderTrios.Count; i++)
                {
                    OrderTrio orderTrio = _callorderTrios[i];
                    Instrument option = orderTrio.Option;

                    totalPnl += orderTrio.Order.Quantity * OptionUniverse[(int)InstrumentType.CE][option.Strike].LastPrice * -1;
                }
            }
            if (_putorderTrios.Count > 0)
            {
                for (int i = 0; i < _putorderTrios.Count; i++)
                {
                    OrderTrio orderTrio = _putorderTrios[i];
                    Instrument option = orderTrio.Option;

                    totalPnl += orderTrio.Order.Quantity * OptionUniverse[(int)InstrumentType.PE][option.Strike].LastPrice * -1;
                }
            }

            if(totalPnl * -1 > _maxLossForDay)
            {
                TriggerEODPositionClose(currentTime, _smallCandle.ClosePrice, true);
            }
        }
        private decimal GetPEStrike(decimal bPrice, decimal stoploss)
        {
            decimal strike;

            if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
            {
                strike = Math.Floor((Math.Min(stoploss, bPrice) - 25) / 50) * 50;
            }
            else
            {
                strike = Math.Floor((Math.Min(stoploss, bPrice) - 50) / 100) * 100;
            }
            
            while (OptionUniverse[(int)InstrumentType.PE].ContainsKey(strike) && OptionUniverse[(int)InstrumentType.PE][strike].LastPrice < 120)
            {
                strike += 100;
            }
            return strike;
        }
        private decimal GetCEStrike(decimal bPrice, decimal stoploss)
        {
            decimal strike;

            if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
            {
                strike = Math.Ceiling((Math.Max(stoploss, bPrice) + 25) / 50) * 50;
            }
            else
            {
                strike = Math.Ceiling((Math.Max(stoploss, bPrice) + 50) / 100) * 100;

            }

            while (OptionUniverse[(int)InstrumentType.CE].ContainsKey(strike) && OptionUniverse[(int)InstrumentType.CE][strike].LastPrice < 120)
            {
                strike -= 100;
            }

            return strike;
        }
        private bool BookStopLoss(List<OrderTrio> orderTrios, bool supportingTrend, DateTime currentTime)
        {
            int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
            decimal currentLoss = orderTrios.Sum(x => x.Order.Quantity * (x.Option.LastPrice - x.Order.AveragePrice));
            //decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * STOP_LOSS_POINTS;
            decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * (supportingTrend ? STOP_LOSS_POINTS_GAMMA : STOP_LOSS_POINTS);

            bool lossBooked = false;
            //if (currentLoss > maxLoss)
            //{
            for (int i = 0; i < orderTrios.Count; i++)
            {
                var orderTrio = orderTrios[i];
                Instrument option = orderTrio.Option;

                if (option.InstrumentType.ToLower().Trim() == "ce" ? (orderTrio.StopLoss - _baseInstrumentPrice) < 10 : (_baseInstrumentPrice - orderTrio.StopLoss) < 10)
                {
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: "",
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK,
                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeExit(order);

                    _pnl += order.Quantity * order.AveragePrice * -1;

                    orderTrio.isActive = false;
                    orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);
                    orderTrios.Remove(orderTrio);
                    i--;

                    lossBooked = true;
                }
            }

                
            //}
            return lossBooked;
        }
        //private bool BookStopLoss(List<OrderTrio> orderTrios, bool supportingTrend)
        //{
        //    int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //    decimal currentLoss = orderTrios.Sum(x => x.Order.Quantity * (x.Option.LastPrice - x.Order.AveragePrice));
        //    //decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * STOP_LOSS_POINTS;
        //    decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * (supportingTrend ? STOP_LOSS_POINTS_GAMMA: STOP_LOSS_POINTS);

        //    bool lossBooked = false;
        //    if (currentLoss > maxLoss)
        //    {
        //        for (int i = 0; i < orderTrios.Count; i++)
        //        {
        //            var orderTrio = orderTrios[i];
        //            Instrument option = orderTrio.Option;
        //            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
        //               option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime, Tag: "",
        //               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //               broker: Constants.KOTAK,
        //               httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //            OnTradeExit(order);

        //            _pnl += order.Quantity * order.AveragePrice * -1;

        //            orderTrio.isActive = false;
        //            orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
        //            DataLogic dl = new DataLogic();
        //            orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //            dl.UpdateAlgoPnl(_algoInstance, _pnl);
        //            orderTrios.Remove(orderTrio);
        //            i--;
        //        }

        //        lossBooked = true;
        //    }
        //    return lossBooked;
        //}

        private int BookPartialProfit(List<OrderTrio> orderTrios, Tick tick, 
            bool trendChanged, DateTime currentTime, /* ref bool preFirstTradeActive,*/ ref bool firstTradeActive, ref bool secondTradeActive,
    ref bool thirdTradeActive)
        {
            //private int BookPartialProfit(List<OrderTrio> orderTrios, Tick tick)
            //{
            //int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
            //int qty = _tradeQty * Convert.ToInt32(_lotSize) / 3;
            //qty -= qty % 25;
            //int quantityTraded = 0;

            int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
            int totalQty = _tradeQty * Convert.ToInt32(_lotSize);
            int qty = 0;
            
            int quantityTraded = 0;

            for (int i = 0; i < orderTrios.Count; i++)
            {
                var orderTrio = orderTrios[i];
                if (orderTrio.Option.InstrumentToken == tick.InstrumentToken)
                {
                    if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell")
                    //&&
                    //// 25% quantity at first target
                    //(
                    //((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET)
                    //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)) ||
                    //// 50% quantity at second target
                    //((orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET)
                    //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4 )) ||

                    //// 75% quantity at third target
                    //((orderTrio.Order.AveragePrice > tick.LastPrice + THIRD_TARGET)
                    //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4))
                    //||

                    //// Trend change , book 1/4 provided it is in profit
                    //((orderTrio.Order.AveragePrice > tick.LastPrice && trendChanged)
                    //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4))
                    //))
                    {
                       // if ((orderTrio.Order.AveragePrice > tick.LastPrice + PRE_FIRST_TARGET)
                       //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)
                       //&& preFirstTradeActive)
                       // {
                       //     preFirstTradeActive = false;
                       //     qty = Convert.ToInt32(_preFirstTargetQty * totalQty);
                       // }
                       // else
                        if ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET)
                        && totalTradedQty > (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)
                        && firstTradeActive)
                        {
                            firstTradeActive = false;
                            qty = Convert.ToInt32(_firstTargetQty * totalQty);
                        }
                        else if ((orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET)
                        && totalTradedQty > (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4)
                        && secondTradeActive)
                        {
                            qty = Convert.ToInt32(_secondTargetQty * totalQty);
                            //secondTradeActive = false;
                            //if (totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4))
                            //{
                            //    //secondTradeActive = false;
                            //    qty = Convert.ToInt32(_secondTargetQty * totalQty);
                            //}
                            //else
                            //{
                            //    //secondTradeActive = false;
                            //    qty = Convert.ToInt32(_secondTargetQty * totalQty) / 2;
                            //}
                        }
                        else if ((orderTrio.Order.AveragePrice > tick.LastPrice + THIRD_TARGET)
                        && totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4)
                        && thirdTradeActive)
                        {
                            //thirdTradeActive = false;
                            qty = Convert.ToInt32(_thirdTargetQty * totalQty);
                        }
                        // Trend change , book 1/4 provided it is in profit
                        else if ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET && trendChanged)
                        && totalTradedQty == (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4))
                        {
                            qty = Convert.ToInt32(_thirdTargetQty * totalQty);
                        }
                        if (qty > 0)
                        {
                            qty -= qty % 25;
                            Instrument option = orderTrio.Option;

                            if (orderTrio.Order.Quantity <= qty)
                            {
                                qty = orderTrio.Order.Quantity;
                            }

                            if (orderTrio.Order.Quantity >= qty)
                            {
                                quantityTraded = qty;
                                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                                   option.KToken, true, qty, algoIndex, currentTime, Tag: "",
                                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                   broker: Constants.KOTAK,
                                   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                OnTradeExit(order);

                                _pnl += order.Quantity * order.AveragePrice * -1;

                                orderTrio.isActive = false;
                                orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                                DataLogic dl = new DataLogic();
                                orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                            }
                            if (orderTrio.Order.Quantity <= qty)
                            {
                                orderTrios.Remove(orderTrio);
                                i--;
                            }
                            else
                            {
                                orderTrio.Order.Quantity -= qty;
                            }

                            totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
                        }
                    }
                    //if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" 
                    //    &&
                    //    // 25% quantity at first target
                    //    ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET
                    //    && totalTradedQty >= _tradeQty * Convert.ToInt32(_lotSize) * 2 / 3) ||
                    //    // 50% quantity at first target
                    //    (orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET
                    //    && totalTradedQty >= _tradeQty * Convert.ToInt32(_lotSize) * 1 / 3
                    //    )))
                    //{
                    //    Instrument option = orderTrio.Option;

                    //    if (orderTrio.Order.Quantity <= qty)
                    //    {
                    //        qty = orderTrio.Order.Quantity;
                    //    }

                    //    if (orderTrio.Order.Quantity >= qty)
                    //    {
                    //        quantityTraded = qty;
                    //        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                    //           option.KToken, true, qty, algoIndex, _bCandle.CloseTime, Tag: "",
                    //           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                    //           broker: Constants.KOTAK,
                    //           httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    //        OnTradeExit(order);

                    //        _pnl += order.Quantity * order.AveragePrice * -1;

                    //        orderTrio.isActive = false;
                    //        orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                    //        DataLogic dl = new DataLogic();
                    //        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    //        dl.UpdateAlgoPnl(_algoInstance, _pnl);
                    //    }
                    //    if (orderTrio.Order.Quantity <= qty)
                    //    {
                    //        orderTrios.Remove(orderTrio);
                    //        i--;
                    //    }
                    //    else
                    //    {
                    //        orderTrio.Order.Quantity -= qty;
                    //    }

                    //    totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
                    //}
                    //break;
                }
            }
            return quantityTraded;
        }

        
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
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
#endif
                    //Load options asynchronously

                    Dictionary<uint, uint> mTokens;
                    DataLogic dl = new DataLogic();
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out mTokens);

                    _lotSize = OptionUniverse[0].First().Value.LotSize;

                    MappedTokens = mTokens;

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
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
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

        //private int GetTradedQuantity(decimal triggerLevel, decimal SL, int totalQuantity, int bookedQty, bool thirdTargetActive)
        //{
        //    int tradedQuantity = 0;

        //    if (Math.Abs(triggerLevel - SL) < 30)
        //    {
        //        tradedQuantity = totalQuantity;
        //        tradedQuantity -= tradedQuantity % 25;
        //    }
        //    else if (Math.Abs(triggerLevel - SL) < 60)
        //    {
        //        tradedQuantity = totalQuantity * 3 / 4;
        //        tradedQuantity -= tradedQuantity % 25;
        //    }
        //    else if (Math.Abs(triggerLevel - SL) < 80)
        //    {
        //        tradedQuantity = totalQuantity * 2 / 4;
        //        tradedQuantity -= tradedQuantity % 25;
        //    }
        //    else if (Math.Abs(triggerLevel - SL) < 100)
        //    {
        //        tradedQuantity = totalQuantity * 1 / 4;
        //        tradedQuantity -= tradedQuantity % 25;
        //    }

        //    return tradedQuantity;
        //}
        private int GetTradedQuantity(decimal triggerLevel, decimal SL, int totalQuantity, int bookedQty, bool thirdTargetActive)
        {
            int tradedQuantity = 0;

            //if (Math.Abs(triggerLevel - SL) < 90)
            //{
            //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //    tradedQuantity = totalQuantity;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            ////else if (Math.Abs(triggerLevel - SL) < 90)
            ////{
            ////    tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            ////    //tradedQuantity = totalQuantity * 3 / 4;
            ////    tradedQuantity -= tradedQuantity % 25;
            ////}
            //else if (Math.Abs(triggerLevel - SL) < 150)
            //{
            //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
            //    tradedQuantity = totalQuantity * 2 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 400 && thirdTargetActive)
            //{
            //    tradedQuantity = totalQuantity * 1 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}

            if (_candleTimeSpan.Minutes == 1)
            {
                if (Math.Abs(triggerLevel - SL) > 20 && Math.Abs(triggerLevel - SL) < 90)
                {
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
                    tradedQuantity = totalQuantity;
                    tradedQuantity -= tradedQuantity % 25;
                }
                else if (Math.Abs(triggerLevel - SL) > 20 && Math.Abs(triggerLevel - SL) < 90 && thirdTargetActive)
                {
                    tradedQuantity = totalQuantity * 2 / 4;
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
                    tradedQuantity -= tradedQuantity % 25;
                }
            }
            else if (_candleTimeSpan.Minutes == 3)
            {
                if (Math.Abs(triggerLevel - SL) < 90)
                {
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
                    tradedQuantity = totalQuantity;
                    tradedQuantity -= tradedQuantity % 25;
                }
                else if (Math.Abs(triggerLevel - SL) < 150 && thirdTargetActive)
                {
                    tradedQuantity = totalQuantity * 3 / 4;
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
                    tradedQuantity -= tradedQuantity % 25;
                }
                else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
                {
                    tradedQuantity = totalQuantity * 2 / 4;
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
                    tradedQuantity -= tradedQuantity % 25;
                }
            }
            else
            {
                if (Math.Abs(triggerLevel - SL) < 150)
                {
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
                    tradedQuantity = totalQuantity;
                    tradedQuantity -= tradedQuantity % 25;
                }
                //else if (Math.Abs(triggerLevel - SL) < 150)
                //{
                //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
                //    tradedQuantity = totalQuantity * 3 / 4;
                //    tradedQuantity -= tradedQuantity % 25;
                //}
                else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
                {
                    tradedQuantity = totalQuantity * 2 / 4;
                    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
                    tradedQuantity -= tradedQuantity % 25;
                }
            }

            //tradedQuantity = totalQuantity;

            return tradedQuantity - bookedQty;
        }

        private void LoadHistoricalHT(DateTime currentTime, TimeSpan candleSpan, List<uint> htLoaded, 
            List<uint> SQLLoadingHT, Dictionary<uint, bool> firstCandleOpenPriceNeeded, Dictionary< uint, 
                HalfTrend> stockTokenHalfTrend, Dictionary<uint, List<Candle>> timeCandles)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, candleSpan, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => x == _baseInstrumentToken && !htLoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                List<uint> newTokens = new List<uint>();
                foreach (uint t in tokens)
                {
                    if (!firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!stockTokenHalfTrend.ContainsKey(t) && !SQLLoadingHT.Contains(t))
                    {
                        newTokens.Add(t);
                        sb.AppendFormat("{0},", t);
                        SQLLoadingHT.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //_SQLLoading.Add(token);
                                           //if (tokenList != string.Empty)
                if (newTokens.Count > 0)
                {
                    //Task task = Task.Run(() => LoadHistoricalCandlesForADX(tokenList, 28, lastCandleEndTime));
                    LoadHistoricalCandlesForHT(newTokens, 28, lastCandleEndTime, candleSpan, stockTokenHalfTrend);
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    if (timeCandles.ContainsKey(tkn) && stockTokenHalfTrend.ContainsKey(tkn))
                    {
                        if (firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            stockTokenHalfTrend[tkn].Process(timeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (timeCandles[tkn].Count > 1)
                        {
                            foreach (var price in timeCandles[tkn])
                            {
                                stockTokenHalfTrend[tkn].Process(timeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !firstCandleOpenPriceNeeded[tkn]) && stockTokenHalfTrend.ContainsKey(tkn))
                    {
                        htLoaded.Add(tkn);
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("ADX loaded from DB for {0}", tkn), "MonitorCandles");
#endif
                    }
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

        //private void LoadHistoricalCandlesForHT(List<uint> tokenList, int candlesCount, 
        //    DateTime lastCandleEndTime, Dictionary<uint, HalfTrend> stockTokenHalfTrend, TimeSpan candleTimeSpan)
        //{
        //    LoadHistoricalCandlesForHT(tokenList, candlesCount, lastCandleEndTime, candleTimeSpan, stockTokenHalfTrend);
        //    //LoadHistoricalCandlesForHT(tokenList, candlesCount, lastCandleEndTime, _candleTimeSpanLong, _stockTokenHalfTrendLong);
        //}
        
        private void LoadHistoricalCandlesForHT(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime, 
            TimeSpan timeSpan, Dictionary<uint, HalfTrend> stockTokenHalfTrend)
        {
            //CandleSeries cs = new CandleSeries();
            //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            HalfTrend ht;
            lock (stockTokenHalfTrend)
            {

                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);// timeSpan.Minutes == 5 ? 1 : timeSpan.Minutes);

                foreach (uint token in tokenList)
                {
                    List<Historical> historicals;
                    if (timeSpan.Minutes == 1)
                    {
                        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
                    }
                    else
                    {
                        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", timeSpan.Minutes));
                    }
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicals)
                    {
                        TimeFrameCandle tc = new TimeFrameCandle();
                        tc.TimeFrame = timeSpan;
                        tc.ClosePrice = price.Close;
                        tc.OpenPrice = price.Open;
                        tc.HighPrice = price.High;
                        tc.LowPrice = price.Low;
                        tc.TotalVolume = price.Volume;
                        tc.OpenTime = price.TimeStamp;
                        tc.InstrumentToken = token;
                        tc.Final = true;
                        tc.State = CandleStates.Finished;
                        if (stockTokenHalfTrend.ContainsKey(tc.InstrumentToken))
                        {
                            stockTokenHalfTrend[tc.InstrumentToken].Process(tc);
                        }
                        else
                        {
                            ht = new HalfTrend(2, 2);
                            ht.Process(tc);
                            stockTokenHalfTrend.TryAdd(tc.InstrumentToken, ht);
                        }
                    }
                }
            }
        }


        private Candle GetLastCandle(Candle c1, Candle c2)
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
        private Candle GetLastCandle(int minutes, DateTime currentTime)
        {
            if (currentTime.TimeOfDay.TotalMinutes - MARKET_START_TIME.TotalMinutes < minutes)
            {
                return null;
            }

            var lastCandles = TimeCandles[_baseInstrumentToken].TakeLast(minutes);
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.InstrumentToken = _baseInstrumentToken;
            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
            tC.ClosePrice = lastCandles.Last().ClosePrice;
            tC.CloseTime = lastCandles.Last().CloseTime;
            tC.HighPrice = lastCandles.Max(x => x.HighPrice);
            tC.LowPrice = lastCandles.Min(x => x.LowPrice);
            tC.Final = true;
            tC.State = CandleStates.Finished;
            return tC;
        }




        private void TriggerEODPositionClose(DateTime currentTime, decimal lastPrice, bool closeAll = false)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 15, 00) || closeAll)
            {
                DataLogic dl = new DataLogic();

                if (_callorderTrios.Count > 0)
                {
                    for (int i = 0; i < _callorderTrios.Count; i++)
                    {
                        OrderTrio orderTrio = _callorderTrios[i];
                        Instrument option = orderTrio.Option;

                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                           option.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime,
                           Tag: "closed",
                           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                           broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        OnTradeEntry(orderTrio.Order);
                       
                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
                        orderTrio.EntryTradeTime = currentTime;
                        orderTrio.isActive = false;
                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance); ;
                        _callorderTrios.Remove(orderTrio);
                        i--;
                    }
                }
                if (_putorderTrios.Count > 0)
                {
                    for (int i = 0; i < _putorderTrios.Count; i++)
                    {
                        OrderTrio orderTrio = _putorderTrios[i];
                        Instrument option = orderTrio.Option;

                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                           option.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime,
                           Tag: "",
                           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                           broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        OnTradeEntry(orderTrio.Order);

                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
                        orderTrio.EntryTradeTime = currentTime;
                        orderTrio.isActive = false;
                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                        _putorderTrios.Remove(orderTrio);
                        i--;
                        
                    }
                }

                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                dl.DeActivateAlgo(_algoInstance);
                _pnl = 0;
                _stopTrade = true;
            }
        }

        private void MonitorCandles(Tick tick, DateTime currentTime, TimeSpan candleTimeSpan, Dictionary<uint, List<Candle>> candles, CandleManger cm)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (candles.ContainsKey(token))
                {
                    cm.StreamingTimeFrameCandle(tick, token, candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, candleTimeSpan, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
#endif
                        //candle starts from there
                        cm.StreamingTimeFrameCandle(tick, token, candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

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


        private DateTime? CheckCandleStartTime(DateTime currentTime, TimeSpan candleTimeSpan, out DateTime lastEndTime)
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

                    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % candleTimeSpan.TotalMilliseconds;

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

        //private void TakeTrade(DateTime currentTime)
        //{
        //    if (_stockTokenHalfTrend.ContainsKey(_baseInstrumentToken)
        //        && TimeCandles.ContainsKey(_baseInstrumentToken)
        //        )
        //    {
        //        decimal htred = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend(); // 0 is up trend
        //        decimal htvalue = _stockTokenHalfTrend[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

        //        if (htred == 0 && _putorderTrios.Count == 0
        //            && (htvalue >= _baseInstrumentPrice - 11 /*|| (_prCandle !=null && _prCandle.LowPrice >= _baseInstrumentPrice - 5)*/)
        //            && _putTriggered)
        //        {
        //            _putProfitBookedQty = 0;
        //            _putTriggered = false;
        //            //this line can be updated for postion sizing, based on the SL.
        //            int qty = _tradeQty;
        //           // LastSwingHighlow(currentTime, _baseInstrumentToken, out _uptrendhighswingindexvalue, out _swingLowPrice);
        //            OrderTrio orderTrio = new OrderTrio();

        //            //No ITM selling
        //            if (_downtrendlowswingindexvalue > _baseInstrumentPrice)
        //            {
        //                return;
        //            }

        //            Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][GetPEStrike(_baseInstrumentPrice, _downtrendlowswingindexvalue)]; //Change -  Take position 50 points farther to avoid gamma loss

        //            //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
        //            //{
        //            //    _atmOption = OptionUniverse[(int)InstrumentType.PE][Math.Floor((_downtrendlowswingindexvalue - 25) / 50) * 50]; //Change -  Take position 50 points farther to avoid gamma loss
        //            //}

        //            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
        //               _atmOption.KToken, false, qty * Convert.ToInt32(_atmOption.LotSize), algoIndex, currentTime,
        //               Tag: "",
        //               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //               broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //            OnTradeEntry(orderTrio.Order);
        //            orderTrio.Option = _atmOption;
        //            orderTrio.StopLoss = _downtrendlowswingindexvalue;
        //            _initialStopLoss = _downtrendlowswingindexvalue;
        //            orderTrio.EntryTradeTime = currentTime;
        //            //first target is 1:1 R&R
        //            orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _downtrendlowswingindexvalue;
        //            _lastOrderTransactionType = "sell";
        //            //_orderTrios.Clear();
        //            //if (!suspensionWindow)
        //            //{
        //            //    _orderTrios.Add(orderTrio);
        //            //}
        //            orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_atmOption.LotSize);
        //            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

        //            DataLogic dl = new DataLogic();
        //            orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //            dl.UpdateAlgoPnl(_algoInstance, _pnl);

        //            _putorderTrios.Add(orderTrio);
        //        }
        //        else if (htred == 1
        //            && _callorderTrios.Count == 0
        //            && (htvalue <= _baseInstrumentPrice + 11 /*|| (_prCandle != null && _prCandle.HighPrice <= _baseInstrumentPrice + 5)*/)
        //            && _callTrigerred)
        //        {
        //            _callProfitBookedQty = 0;
        //            _callTrigerred = false;
        //            //this line can be updated for postion sizing, based on the SL.
        //            int qty = _tradeQty;
        //            LastSwingHighlow(currentTime, _baseInstrumentToken, out _uptrendhighswingindexvalue, out _downtrendlowswingindexvalue);


        //            //No ITM selling
        //            if (_baseInstrumentPrice > _uptrendhighswingindexvalue)
        //            {
        //                return;
        //            }
        //            OrderTrio orderTrio = new OrderTrio();
        //            Instrument _atmOption = OptionUniverse[(int)InstrumentType.CE][GetCEStrike(_baseInstrumentPrice, _uptrendhighswingindexvalue)]; //Change -  Take position 50 points farther to avoid gamma loss

        //            //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
        //            //{
        //            //    _atmOption = OptionUniverse[(int)InstrumentType.CE][GetCEStrike(_baseInstrumentPrice, _uptrendhighswingindexvalue)]; //Change -  Take position 50 points farther to avoid gamma loss
        //            //}

        //            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
        //               _atmOption.KToken, false, qty * Convert.ToInt32(_atmOption.LotSize), algoIndex, currentTime,
        //               Tag: "",
        //               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //               broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //            OnTradeEntry(orderTrio.Order);
        //            orderTrio.Option = _atmOption;
        //            orderTrio.StopLoss = _uptrendhighswingindexvalue;
        //            _initialStopLoss = _uptrendhighswingindexvalue;
        //            orderTrio.EntryTradeTime = currentTime;
        //            //first target is 1:1 R&R
        //            orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _uptrendhighswingindexvalue;
        //            _lastOrderTransactionType = "sell";

        //            orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_atmOption.LotSize);
        //            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

        //            DataLogic dl = new DataLogic();
        //            orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //            dl.UpdateAlgoPnl(_algoInstance, _pnl);

        //            _callorderTrios.Add(orderTrio);
        //        }
        //    }
        //}

//        private void LoadBInstrumentEMA(uint bToken, int candleCount, DateTime currentTime)
//        {
//            DateTime lastCandleEndTime;
//            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
//            try
//            {
//                lock (_indexEMA)
//                {
//                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
//                    {
//                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
//                    }
//                    int firstCandleFormed = 0;
//                    if (!_indexEMALoading)
//                    {
//                        _indexEMALoading = true;
//                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
//                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
//                    }


//                    if (TimeCandles.ContainsKey(bToken) && _indexEMALoadedFromDB)
//                    {
//                        if (_firstCandleOpenPriceNeeded[bToken])
//                        {
//                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
//                            _indexEMA.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

//                            firstCandleFormed = 1;
//                        }
//                        //In case SQL loading took longer then candle time frame, this will be used to catch up
//                        if (TimeCandles[bToken].Count > 1)
//                        {
//                            foreach (var price in TimeCandles[bToken])
//                            {
//                                _indexEMA.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
//                            }
//                        }
//                    }

//                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexEMALoadedFromDB)
//                    {
//                        _indexEMALoaded = true;
//#if !BACKTEST
//                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
//                            String.Format("{0} EMA loaded from DB for Base Instrument", 20), "LoadBInstrumentEMA");
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
        //        lock (_indexEMA)
        //        {
        //            DataLogic dl = new DataLogic();
        //            DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
        //            List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
        //            //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

        //            foreach (var price in historicals)
        //            {
        //                _indexEMA.Process(price.Close, isFinal: true);
        //            }
        //            _indexEMALoadedFromDB = true;
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
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    return;

                }

#if local
                if (tick.Timestamp.HasValue && _currentDate.HasValue && tick.Timestamp.Value.Date != _currentDate)
                {
                    ResetAlgo(tick.Timestamp.Value.Date);
                }
#endif
                ActiveTradeIntraday(tick);
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
            DataLogic dl = new DataLogic();
            DateTime? nextExpiry = dl.GetCurrentMonthlyExpiry(_currentDate.Value);
            SetUpInitialData(nextExpiry);
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

        //        private void CheckTPSL(DateTime currentTime, decimal lastPrice, bool closeAll = false)
        //        {
        //            if (_orderTrios.Count > 0)
        //            {
        //                for (int i = 0; i < _orderTrios.Count; i++)
        //                {
        //                    var orderTrio = _orderTrios[i];

        //                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (orderTrio.TPFlag))
        //                    {
        //                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
        //                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //                        OnTradeEntry(order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif

        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: _httpClientFactory.CreateClient());
        //                        _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

        //                        _orderTrios.Remove(orderTrio);
        //                        i--;
        //                    }
        //                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (orderTrio.TPFlag))
        //                    {
        //                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
        //                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //                        OnTradeEntry(order);

        //                        _pnl += (orderTrio.Order.AveragePrice - order.AveragePrice) * _tradeQty * _activeFuture.LotSize;


        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif
        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: _httpClientFactory.CreateClient());

        //                        _orderTrios.Remove(orderTrio);
        //                        i--;
        //                    }
        //                }
        //            }
        //        }

        //        private void CheckSL(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        //        {
        //            if (_orderTrios.Count > 0)
        //            {
        //                for (int i = 0; i < _orderTrios.Count; i++)
        //                {
        //                    var orderTrio = _orderTrios[i];

        //                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && ((lastPrice < orderTrio.StopLoss || closeAll) || (pCandle != null && (lastPrice < pCandle.LowPrice) && orderTrio.TPFlag)))
        //                    {
        //                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
        //                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //                        OnTradeEntry(order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif
        //                        _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: _httpClientFactory.CreateClient());


        //                        _orderTrios.Remove(orderTrio);
        //                        i--;
        //                    }
        //                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && ((lastPrice > orderTrio.StopLoss || closeAll) || (pCandle != null && (lastPrice > pCandle.HighPrice) && orderTrio.TPFlag)))
        //                    {
        //                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
        //                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //                        OnTradeEntry(order);

        //#if !BACKTEST
        //                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
        //#endif
        //                        ////Cancel Target profit Order
        //                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
        //                        //httpClient: _httpClientFactory.CreateClient());

        //                        _pnl += (orderTrio.Order.AveragePrice - order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

        //                        _orderTrios.Remove(orderTrio);
        //                        i--;
        //                    }
        //                }
        //            }
        //        }
        //        private void CheckSLForEMABasedOrders(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        //        {
        //            if (_orderTriosFromEMATrades.Count > 0)
        //            {
        //                for (int i = 0; i < _orderTriosFromEMATrades.Count; i++)
        //                {
        //                    var orderTrio = _orderTriosFromEMATrades[i];

        //                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (lastPrice < _indexEMAValue.GetValue<decimal>() || closeAll))
        //                    {
        //                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
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
        //                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (lastPrice > _indexEMAValue.GetValue<decimal>() || closeAll))
        //                    {
        //                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
        //                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
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
        //                //if (c.ClosePrice > _criticalLevels.Keys.Max() || c.ClosePrice < _criticalLevels.Keys.Min())
        //                if (c.ClosePrice > _previousDayOHLC.High || c.ClosePrice < _previousDayOHLC.Low)
        //                {
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 17);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 18);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 19);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 20);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 21);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 22);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R4], 23);
        //                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S4], 24);
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
        //                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0 && level.Key - plevel > 10)
        //                        {
        //                            if ((level.Key - c.ClosePrice) / (c.ClosePrice - c.LowPrice /*plevel*/) > 2.0M)
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
        //                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0 && level.Key - plevel > 10)
        //                        {
        //                            if (Math.Abs((c.ClosePrice - c.HighPrice /*plevel*/) / (level.Key - c.ClosePrice)) > 2.0M)
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




        //private void LoadCriticalLevels(uint token, uint rToken, DateTime currentTime)
        //{
        //    DataLogic dl = new DataLogic();
        //    DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
        //    DateTime previousWeeklyExpiry = dl.GetPreviousWeeklyExpiry(currentTime, 2);
        //    List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
        //    List<Historical> rpdOHLCList = ZObjects.kite.GetHistoricalData(rToken.ToString(), previousTradingDate, currentTime.Date, "hour");
        //    List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");
        //    List<Historical> rpdOHLCDay = ZObjects.kite.GetHistoricalData(rToken.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

        //    //DateTime fromDate = currentTime.AddMonths(-1);
        //    //DateTime toDate = currentTime.AddMonths(-1).Date;
        //    //List<Historical> pdOHLCMonth = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

        //    //_criticalLevels.TryAdd(pdOHLCMonth.Max(x=>x.High), 50);
        //    //_criticalLevels.TryAdd(pdOHLCMonth.Min(x => x.Low), 51);
        //    //_criticalLevels.TryAdd(pdOHLCMonth.Last().Close, 52);

        //    //toDate = currentTime.AddDays(-(int)currentTime.DayOfWeek - 6);

        //    //List<Historical> pdOHLCWeek = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

        //    //_criticalLevels.TryAdd(pdOHLCWeek.Max(x => x.High), 40);
        //    //_criticalLevels.TryAdd(pdOHLCWeek.Min(x => x.Low), 41);
        //    //_criticalLevels.TryAdd(pdOHLCWeek.Last().Close, 42);

        //    List<Historical> historicalOHLC = ZObjects.kite.GetHistoricalData(token.ToString(), previousWeeklyExpiry, previousTradingDate + MARKET_CLOSE_TIME, "day");
        //    //List<string> dates = dl.GetActiveTradingDays(fromDate, currentTime);

        //    int i = 30;
        //    foreach (var ohlc in historicalOHLC)
        //    {
        //        _criticalLevels.TryAdd(ohlc.High, i++);
        //        _criticalLevels.TryAdd(ohlc.Low, i++);
        //        //_criticalLevels.TryAdd(ohlc.Close, i++);
        //    }

        //    #region Reference Critical Values
        //    List<Historical> rhistoricalOHLC = ZObjects.kite.GetHistoricalData(rToken.ToString(), previousWeeklyExpiry, previousTradingDate + MARKET_CLOSE_TIME, "day");
        //    //List<string> dates = dl.GetActiveTradingDays(fromDate, currentTime);

        //    int r = 30;
        //    foreach (var ohlc in rhistoricalOHLC)
        //    {
        //        _rcriticalLevels.TryAdd(ohlc.High, r++);
        //        _rcriticalLevels.TryAdd(ohlc.Low, r++);
        //        // _rcriticalLevels.TryAdd(ohlc.Close, r++);

        //        OHLC rpdOHLC = new OHLC() { Close = rpdOHLCDay.Last().Close, Open = rpdOHLCDay.First().Open, High = rpdOHLCDay.Max(x => x.High), Low = rpdOHLCDay.Min(x => x.Low), InstrumentToken = rToken };
        //        _cpr = new CentralPivotRange(rpdOHLC);
        //        _rpreviousDayOHLC = rpdOHLC;
        //        _rpreviousDayOHLC.Close = rpdOHLCList.Last().Close;

        //        List<Historical> rpwOHLCList = ZObjects.kite.GetHistoricalData(rToken.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
        //        _rpreviousWeekOHLC = new OHLC(rpwOHLCList.First(), rToken);
        //        _rweeklycpr = new CentralPivotRange(_rpreviousWeekOHLC);


        //        //load Previous Day and previous week data
        //        _rcriticalLevels.TryAdd(_rpreviousDayOHLC.Close, 0);
        //        _rcriticalLevels.TryAdd(_rpreviousDayOHLC.High, 1);
        //        _rcriticalLevels.TryAdd(_rpreviousDayOHLC.Low, 2);
        //        // _criticalLevels.TryAdd(_previousDayBodyHigh, 0);
        //        //_criticalLevels.TryAdd(_previousDayBodyLow, 0);
        //        _rcriticalLevels.Remove(0);

        //        //Clustering of _critical levels
        //        _rcriticalLevelsWeekly.TryAdd(_rpreviousWeekOHLC.Close, 0);
        //        _rcriticalLevelsWeekly.TryAdd(_rpreviousWeekOHLC.High, 1);
        //        _rcriticalLevelsWeekly.TryAdd(_rpreviousWeekOHLC.Low, 2);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.CPR], 3);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.R1], 6);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.R2], 7);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.R3], 8);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.S3], 9);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.S2], 10);
        //        _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.S1], 11);
        //        _rcriticalLevelsWeekly.TryAdd(_rpreviousDayOHLC.High, 12);
        //        _rcriticalLevelsWeekly.TryAdd(_rpreviousDayOHLC.Low, 13);
        //        _rcriticalLevelsWeekly.Remove(0);
        //    }
        //    #endregion


        //    //OHLC pdOHLC = new OHLC() { Close = pdOHLCList.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

        //    OHLC pdOHLC = new OHLC() { Close = pdOHLCDay.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };
        //    _cpr = new CentralPivotRange(pdOHLC);
        //    _previousDayOHLC = pdOHLC;
        //    _previousDayOHLC.Close = pdOHLCList.Last().Close;

        //    List<Historical> pwOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
        //    _previousWeekOHLC = new OHLC(pwOHLCList.First(), token);
        //    _weeklycpr = new CentralPivotRange(_previousWeekOHLC);

        //    //load Previous Day and previous week data
        //    //_criticalLevels = new SortedList<decimal, int>();
        //    _criticalLevels.TryAdd(_previousDayOHLC.Close, 0);
        //    _criticalLevels.TryAdd(_previousDayOHLC.High, 1);
        //    _criticalLevels.TryAdd(_previousDayOHLC.Low, 2);
        //    // _criticalLevels.TryAdd(_previousDayBodyHigh, 0);
        //    //_criticalLevels.TryAdd(_previousDayBodyLow, 0);



        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.CPR], 7);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.UCPR], 8);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.LCPR], 9);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 10);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 11);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 12);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 13);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 14);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 15);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R4], 16);
        //    //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S4], 17);
        //    _criticalLevels.Remove(0);


        //    //Clustering of _critical levels
        //    decimal _thresholdDistance = 5;




        //    _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.Close, 0);
        //    _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.High, 1);
        //    _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.Low, 2);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.CPR], 3);
        //    //_criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.UCPR], 4);
        //    //_criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.LCPR], 5);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R1], 6);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R2], 7);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R3], 8);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S3], 9);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S2], 10);
        //    _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S1], 11);
        //    _criticalLevelsWeekly.TryAdd(_previousDayOHLC.High, 12);
        //    _criticalLevelsWeekly.TryAdd(_previousDayOHLC.Low, 13);
        //    _criticalLevelsWeekly.Remove(0);
        //}

        //private void LoadHistoricalCandlesForHTLong(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        //{
        //    //CandleSeries cs = new CandleSeries();
        //    //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

        //    HalfTrend ht;
        //    lock (_stockTokenHalfTrendLong)
        //    {
        //        DataLogic dl = new DataLogic();
        //        DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 2);

        //        foreach (uint token in tokenList)
        //        {
        //            //List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "day");
        //            List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", timeSpan.Minutes));
        //            //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

        //            foreach (var price in historicals)
        //            {
        //                TimeFrameCandle tc = new TimeFrameCandle();
        //                tc.TimeFrame = timeSpan;
        //                tc.ClosePrice = price.Close;
        //                tc.OpenPrice = price.Open;
        //                tc.HighPrice = price.High;
        //                tc.LowPrice = price.Low;
        //                tc.TotalVolume = price.Volume;
        //                tc.OpenTime = price.TimeStamp;
        //                tc.InstrumentToken = token;
        //                tc.Final = true;
        //                tc.State = CandleStates.Finished;
        //                if (_stockTokenHalfTrendLong.ContainsKey(tc.InstrumentToken))
        //                {
        //                    _stockTokenHalfTrendLong[tc.InstrumentToken].Process(tc);
        //                }
        //                else
        //                {
        //                    ht = new HalfTrend(2, 2);
        //                    ht.Process(tc);
        //                    _stockTokenHalfTrendLong.TryAdd(tc.InstrumentToken, ht);
        //                }
        //            }
        //        }
        //    }
        //}

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
    }
}
