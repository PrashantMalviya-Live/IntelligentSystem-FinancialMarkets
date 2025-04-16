using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using BrokerConnectWrapper;
using GlobalCore;
using GlobalLayer;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
//using KafkaFacade;
using System.Timers;
using ZMQFacade;
//using Twilio.Jwt.AccessToken;

namespace Algorithms.Algorithms
{
    public class OptionSellOnEMA : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionSellOnEMA source);
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

        private Dictionary<uint, OrderTrio> _orderTrios;

        List<uint> _dataLoaded, _cprLoaded;
        List<uint> _SQLLoadingHT;

        private const int RSI_THRESHOLD = 50;
        Dictionary<uint, ExponentialMovingAverage> _ema;
        Dictionary<uint, RelativeStrengthIndex> _rsi;
        private Dictionary<uint, CentralPivotRange> _cpr;


        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private bool _ibLoaded = false;
        public decimal _minDistanceFromBInstrument;
        public decimal _maxDistanceFromBInstrument;
        public Dictionary<uint, uint> MappedTokens { get; set; }
        public OrderTrio _straddleCallOrderTrio;
        public OrderTrio _straddlePutOrderTrio;
        private bool _peSLHit = false;
        private bool _ceSLHit = false;
        private decimal _previousUpswingValue = 0;
        private decimal _previousDownswingValue = 0;
        public List<Instrument> ActiveOptions { get; set; }
        public Dictionary<uint, Instrument> AllOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        public SuperTrend _bSuperTrendSlow { get; set; }
        public SuperTrend _bSuperTrendFast { get; set; }
        private int _stMultiplier = 1;
        private int _stLength = 10;
        private decimal _stSlowFastMultiplier = 5;

        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);
        public readonly TimeSpan EXTREME_END_TIME = new TimeSpan(12, 00, 0);
        public readonly TimeSpan INITIAL_BALANCE_END_TIME = new TimeSpan(11, 00, 0);

        string _lastOrderTransactionType = "";
        decimal _initialStopLoss = 0;
        private bool _referenceCandleLoaded = false;
        private bool _baseCandleLoaded = false;
        private Candle _bCandle;
        public Candle _rCandle;
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

        private readonly TimeSpan TRADING_WINDOW_START = new TimeSpan(09, 15, 0);
        //11 AM to 1Pm suspension
        private readonly TimeSpan SUSPENSION_WINDOW_START = new TimeSpan(17, 30, 0);
        private readonly TimeSpan SUSPENSION_WINDOW_END = new TimeSpan(19, 30, 0);
        private readonly int STOP_LOSS_POINTS = 1130;//50;//30; //40 points max loss on an average 
        private readonly int STOP_LOSS_POINTS_GAMMA = 1130;//50;//30;
        private const decimal STOP_LOSS_THRESHOLD = 0.2M;//0.15m;//50;//30;
        private  decimal _maxCEProfit = 0;
        private decimal _maxPEProfit = 0;
        private const int DAY_STOP_LOSS_PERCENT = 1; //40 points max loss on an average 

        private decimal _ceSL = 0;
        private decimal _peSL = 0;

        //25% quantity booked in each target
        //private const decimal PRE_FIRST_TARGET = 30;
        //bool _preFirstPETargetActive = true;
        //bool _preFirstCETargetActive = true;
        //decimal _preFirstTargetQty = 0.15m;

        //3 mins: 50,70,100. SL: 50 points
        // 1 min: 30,50,70
        //25% quantity booked in each target
        private decimal FIRST_TARGET = 50;
        bool _firstPETargetActive = true;
        bool _firstCETargetActive = true;
        decimal _firstTargetQty = 0.25m;

        private decimal SECOND_TARGET = 70;
        bool _secondPETargetActive = true;
        bool _secondCETargetActive = true;
        decimal _secondTargetQty = 0.5m;

        private decimal THIRD_TARGET = 100;
        bool _thirdPETargetActive = true;
        bool _thirdCETargetActive = true;
        decimal _thirdTargetQty = 0.25m;


        private bool _firsttargetmovedPE = false;
        private bool _secondtargetmovedPE = false;
        private bool _thirdtargetmovedPE = false;
        private bool _firsttargetmovedCE = false;
        private bool _secondtargetmovedCE = false;
        private bool _thirdtargetmovedCE = false;

        private const decimal SL_BUFFER = 0;
        private const decimal PNL_SL_TRAIL_LIMIT = 15000;
        private const decimal SL_TRAIL_LIMIT_INITIAL = 30;
        private const decimal SL_TRAIL_LIMIT_TRAIL = 15;
        private decimal _peCurrentProfit, _ceCurrentProfit;
        decimal _maxLossForDay = -50000;
        private const decimal CANDLE_WICK_SIZE = 25;//45;//10000;//45;//35
        private const decimal CANDLE_BODY_MIN = 5;
        private const decimal CANDLE_BODY = 20;//40; //25
        private const decimal CANDLE_BODY_BIG = 25;//35;
        private const decimal CANDLE_BODY_EXTREME = 45;//95;
        private const decimal EMA_ENTRY_RANGE = 35;
        private const decimal RISK_REWARD = 2.3M;
        private const int SL_THRESHOLD = 200; //200 for 5 mins; //350 for 15 mins
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        private OHLC _rpreviousDayOHLC;
        private OHLC _rpreviousWeekOHLC;
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
        private bool _putTriggered = false;
        private bool _callTrigerred = false;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.OptionSellOnEMA;
        private decimal _targetProfit;
        private decimal _stopLoss;

        private decimal _ceStopLossPrice;
        private decimal _peStopLossPrice;
        private int _putProfitBookedQty = 0;
        private int _callProfitBookedQty = 0;
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
        ExponentialMovingAverage _indexEMA;
        bool _indexEMALoaded = false;
        bool _rindexEMALoaded = false;
        bool _indexEMALoading = false;
        bool _indexEMALoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;
        private IIndicatorValue _rindexEMAValue;
        private uint _referenceBToken = 0;
        private bool _intraday;
        private decimal nextTriggerLevel;
        IEnumerable<Candle> _historicalcandles = null;
        private bool _ceEntryTriggered = false;
        private Instrument _ceTrigerredOption;
        private bool _peEntryTriggered = false;
        private Instrument _peTrigerredOption;
        
        private int  _numberOfCEEntries = 0;
        private int _numberOfPEEntries = 0;
        private const int MAX_CE_Entries = 1;
        private const int MAX_PE_Entries = 1;
        

        public OptionSellOnEMA(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            bool intraday, decimal pnl,
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;

            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);

            _candleTimeSpan = candleTimeSpan;
            if (_candleTimeSpan.TotalMinutes == 5)
            {
                STOP_LOSS_POINTS = 50;
                STOP_LOSS_POINTS_GAMMA = 50;

                FIRST_TARGET = 30;
                SECOND_TARGET = 50;
                THIRD_TARGET = 65;
            }


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

            //_bSuperTrend = new SuperTrend(3, 10);
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
            _orderTrios = new Dictionary<uint, OrderTrio>();

            _indexEMA = new ExponentialMovingAverage(length: 20);

            _maxDistanceFromBInstrument = 500;
            _minDistanceFromBInstrument = 300;

            _ema = new Dictionary<uint, ExponentialMovingAverage>();
            _rsi = new Dictionary<uint, RelativeStrengthIndex>();
            _cpr = new Dictionary<uint, CentralPivotRange>();

            _dataLoaded = new List<uint>();
            _SQLLoadingHT = new List<uint>();


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
                    _orderTrios.Add(option.InstrumentToken, orderTrio);
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
                            if (!_dataLoaded.Contains(token) && token != _baseInstrumentToken)
                            {
                                LoadHistoricalDataForIndicators(currentTime);
                            }

                            if (OptionUniverse != null)
                            {
                                Instrument option = UpdateOptionPrice(tick);
                                MonitorCandles(tick, currentTime);

                                if (_ceEntryTriggered && _ceTrigerredOption.InstrumentToken == token)
                                {
                                    _ceEntryTriggered = TakeEntry(currentTime, tick.LastPrice, _ceEntryTriggered, _ceSL, option);
                                    _numberOfCEEntries += _ceEntryTriggered ? 0 : 1;
                                }
                                else if (_peEntryTriggered && _peTrigerredOption.InstrumentToken == token)
                                {
                                    _peEntryTriggered = TakeEntry(currentTime, tick.LastPrice, _peEntryTriggered, _peSL, option);
                                    _numberOfPEEntries += _peEntryTriggered ? 0 : 1;
                                }
                                if (option != null)
                                {
                                    TrailStopLoss(option, tick.LastPrice, currentTime);
                                }
                            }
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
        private bool TakeEntry(DateTime currentTime, decimal lastPrice, bool entryTriggered, decimal stopLoss, Instrument option)
        {
            //Take Short Trade
            //decimal stopLoss = TimeCandles[e.InstrumentToken].TakeLast(2).First().HighPrice;
            if (stopLoss < lastPrice * (1 + STOP_LOSS_THRESHOLD))
            {
                OrderTrio orderTrio = ExecuteTrade(currentTime, stopLoss, option);
                _orderTrios.Add(option.InstrumentToken, orderTrio);
                entryTriggered = false;
            }
            return entryTriggered;
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (e.InstrumentToken != _baseInstrumentToken)
                {
                    //_baseCandleLoaded = true;
                    //_bCandle = e;

                    if (_ema.ContainsKey(e.InstrumentToken))
                    {
                        var ema = _ema[e.InstrumentToken].Process(e);
                        var rsi = _rsi[e.InstrumentToken].Process(e);
                        //var cpr = _cpr[e.InstrumentToken].Proce

                        if(!AllOptions.ContainsKey(e.InstrumentToken))
                        {
                            return;
                        }
                        //If candle close is less than 20 EMA, and RSI is less than 50, then short the option, SL is high of previous candle.
                        Instrument option = AllOptions[e.InstrumentToken];

                        if ((option.InstrumentType.ToLower() == "ce" && _baseInstrumentPrice <= option.Strike - 50 && _baseInstrumentPrice >= option.Strike - 120)
                            || (option.InstrumentType.ToLower() == "pe" && _baseInstrumentPrice >= option.Strike + 50 && _baseInstrumentPrice <= option.Strike + 120))
                        {

                            if (rsi.IsFormed
                            && ema.IsFormed
                            && e.CloseTime.TimeOfDay >= TRADING_WINDOW_START && e.CloseTime.TimeOfDay <= new TimeSpan(15, 12, 00)
                            && (ema.GetValue<Decimal>() > e.ClosePrice && ema.GetValue<Decimal>() < e.HighPrice)
                            && (rsi.GetValue<decimal>() < RSI_THRESHOLD && TimeCandles[e.InstrumentToken].Count > 1)
                                && !_orderTrios.Any(x => x.Value.Option.InstrumentType == option.InstrumentType))
                            {
                                decimal stopLoss = TimeCandles[e.InstrumentToken].TakeLast(2).First().HighPrice;

                                if (option.InstrumentType.ToLower() == "ce" && _numberOfCEEntries < MAX_CE_Entries)
                                {
                                    _ceEntryTriggered = true;
                                    _ceSL = stopLoss;
                                    _ceEntryTriggered = CheckTrade(e, option, stopLoss, out _ceTrigerredOption);
                                    _numberOfCEEntries += _ceEntryTriggered ? 0 : 1;
                                }
                                else if (option.InstrumentType.ToLower() == "pe" && _numberOfPEEntries < MAX_PE_Entries)
                                {
                                    _peEntryTriggered = true;
                                    _peSL = stopLoss;
                                    _peEntryTriggered = CheckTrade(e, option, stopLoss, out _peTrigerredOption);
                                    _numberOfPEEntries += _peEntryTriggered ? 0 : 1;
                                }
                            }
                        }
                    }

                    if (_intraday && e.CloseTime.TimeOfDay >= new TimeSpan(15, 10, 00))
                    {
                        TriggerEODPositionClose(e.CloseTime, false);
                    }
                    //_prCandle = _bCandle;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.StackTrace);
            }
        }

        private bool CheckTrade(Candle e, Instrument option, decimal stopLoss, out Instrument trigerredOption)
        {
            bool entryTriggered = true;
            trigerredOption = null;
            //Take Short Trade
            if (stopLoss < e.ClosePrice * (1 + STOP_LOSS_THRESHOLD))
            {
                OrderTrio orderTrio = ExecuteTrade(e.CloseTime, stopLoss, option);
                _orderTrios.Add(option.InstrumentToken, orderTrio);
                entryTriggered = false;
            }
            else
            {
                trigerredOption = option;
            }
            return entryTriggered;
        }

        private Instrument UpdateOptionPrice(Tick tick)
        {
            Instrument option = null;
            bool optionFound = false;
            for (int i = 0; i < 2; i++)
            {
                foreach (var optionVar in OptionUniverse[i])
                {
                    option = optionVar.Value;
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

            return option;
        }

        private void LoadOptionsToTrade(DateTime currentTime, bool forceReload = false)
        {
            try
            {
                //PE and CE collection will have same strikes.
                if (OptionUniverse == null ||
                    (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                    || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument)
                //(OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                //|| OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                //   || (OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                //   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                   || forceReload)
                {
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
#endif

                    Dictionary<uint, uint> mTokens;
                    DataLogic dl = new DataLogic();
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out mTokens);

                    AllOptions = new Dictionary<uint, Instrument>();

                    for (int i = 0; i < 2; i++)
                    {
                        foreach (var optionVar in OptionUniverse[i])
                        {
                            Instrument option = optionVar.Value;
                            AllOptions.TryAdd(option.InstrumentToken, option);
                            _ema.TryAdd(option.InstrumentToken, new ExponentialMovingAverage(length: 20));
                            _rsi.TryAdd(option.InstrumentToken, new RelativeStrengthIndex());
                            //_cpr.TryAdd(option.InstrumentToken, );
                        }
                    }

                    _ema.TryAdd(_baseInstrumentToken, new ExponentialMovingAverage(length: 20));
                    _rsi.TryAdd(_baseInstrumentToken, new RelativeStrengthIndex());

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

        private OrderTrio ExecuteTrade(DateTime currentTime, decimal stoploss, Instrument option)
        {
            //Instrument option = AllOptions[e.InstrumentToken];

            //DateTime currentTime = e.CloseTime;

            OrderTrio orderTrio = new OrderTrio();


            //              Instrument option = GetOptions(currentTime, _baseInstrumentPrice, InstrumentType.PE, _putorderTrios, stopLoss);

            int qty = _tradeQty * (int)option.LotSize;
            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
               option.KToken, false, qty, algoIndex, currentTime,
               Tag: _baseInstrumentPrice.ToString(),
               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
               broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

            OnTradeEntry(orderTrio.Order);
            orderTrio.Option = option;

            //SL SHIFTED TO LOW OF PREVIOUS CANDLE
            //orderTrio.BaseInstrumentStopLoss = stopLoss;
            orderTrio.StopLoss = stoploss;
            orderTrio.EntryTradeTime = currentTime;

            //first target is 1:1 R&R
            //orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - stopLoss;
            //_lastOrderTransactionType = "sell";
            //_orderTrios.Clear();
            //if (!suspensionWindow)
            //{
            //    _orderTrios.Add(orderTrio);
            //}
            orderTrio.Order.Quantity = qty;
            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

            DataLogic dl = new DataLogic();
            //orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
            dl.UpdateAlgoPnl(_algoInstance, _pnl);

            return orderTrio;
        }

        private void TrailStopLoss(Instrument option, decimal lastPrice, DateTime currentTime)
        {
            OrderTrio orderTrio = null;

            if (_orderTrios.TryGetValue(option.InstrumentToken, out orderTrio))
            {
                if (option.InstrumentType.ToLower() == "ce")
                {
                    _maxCEProfit = Math.Max(_maxCEProfit, orderTrio.Order.AveragePrice - lastPrice);
                }
                else if (option.InstrumentType.ToLower() == "pe")
                {
                    _maxPEProfit = Math.Max(_maxPEProfit, orderTrio.Order.AveragePrice - lastPrice);
                }
                if (lastPrice > orderTrio.StopLoss ||
                    ((orderTrio.Order.AveragePrice - lastPrice < _maxCEProfit / 2 && _maxCEProfit >= 30 && option.InstrumentType.ToLower() == "ce")
                    || (orderTrio.Order.AveragePrice - lastPrice < _maxPEProfit / 2 && _maxPEProfit >= 30 && option.InstrumentType.ToLower() == "pe"))
                    )
                {

                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                               option.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime,
                               Tag: _baseInstrumentPrice.ToString(),
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

                    _orderTrios.Remove(option.InstrumentToken);
                }
            }
        }


        private decimal GetPEStrike(decimal bPrice, decimal stoploss)
        {
            decimal strike;

            //strike = Math.Round(bPrice / 100) * 100;
            if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
            {
                strike = Math.Floor((Math.Min(stoploss, bPrice) - 25) / 50) * 50;
            }
            else
            {
                strike = Math.Floor((Math.Min(stoploss, bPrice) - 50) / 100) * 100;
            }

            //while (OptionUniverse[(int)InstrumentType.PE].ContainsKey(strike) && OptionUniverse[(int)InstrumentType.PE][strike].LastPrice < 120)
            //{
            //    strike += 100;
            //}
            return strike;
        }
        private decimal GetCEStrike(decimal bPrice, decimal stoploss)
        {
            decimal strike;
            //strike = Math.Round(bPrice / 100) * 100;

            if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
            {
                strike = Math.Ceiling((Math.Max(stoploss, bPrice) + 25) / 50) * 50;
            }
            else
            {
                strike = Math.Ceiling((Math.Max(stoploss, bPrice) + 50) / 100) * 100;

            }

            //while (OptionUniverse[(int)InstrumentType.CE].ContainsKey(strike) && OptionUniverse[(int)InstrumentType.CE][strike].LastPrice < 120)
            //{
            //    strike -= 100;
            //}

            return strike;
        }


        private void LoadHistoricalDataForIndicators(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => !_dataLoaded.Contains(x) && x != _baseInstrumentToken);

                StringBuilder sb = new StringBuilder();
                List<uint> newTokens = new List<uint>();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!_SQLLoadingHT.Contains(t))
                    {
                        newTokens.Add(t);
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingHT.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; 
                
                //historicalPricesLoaded = 0;
                //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                //{
                //_SQLLoading.Add(token);
                //if (tokenList != string.Empty)

                if (newTokens.Count > 0)
                {
                    //Task task = Task.Run(() => LoadHistoricalCandlesForADX(tokenList, 28, lastCandleEndTime));
                    LoadHistoricalCandlesForHT(tokenList, 50, lastCandleEndTime);
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _ema.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _ema[tkn].Process(TimeCandles[tkn].First());
                            _rsi[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                //_stockTokenHT[tkn].Process(TimeCandles[tkn].First());
                                _ema[tkn].Process(TimeCandles[tkn].First());
                                _rsi[tkn].Process(TimeCandles[tkn].First());
                                //_stockTokenHalfTrendLong[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _ema.ContainsKey(tkn))
                    {
                        _dataLoaded.Add(tkn);
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("ADX loaded from DB for {0}", tkn), "MonitorCandles");
#endif
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

        private void LoadHistoricalCandlesForHT(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            LoadHistoricalCandlesForHT(tokenList, candlesCount, lastCandleEndTime, _candleTimeSpan);
            //LoadHistoricalCandlesForHTLong(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(1, 0, 30, 0));
            //LoadHistoricalCandlesForADX(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(0, 15, 0));
        }

        private void LoadHistoricalCandlesForHT(string tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            //CandleSeries cs = new CandleSeries();
            //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            HalfTrend ht;
            lock (_ema)
            {

                DataLogic dl = new DataLogic();

                Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan, false, bToken: _baseInstrumentToken);

                foreach (var keyvalue in historicalCandlePrices)
                {
                    uint token = keyvalue.Key;

                    foreach (var value in keyvalue.Value)
                    {
                        _ema[token].Process(value);
                        _rsi[token].Process(value);
                    }
                }

                //DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);// timeSpan.Minutes == 5 ? 1 : timeSpan.Minutes);
                //List<Historical> historicals;
                //foreach (uint token in tokenList)
                //{

                //    if (timeSpan.Minutes == 1)
                //    {
                //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
                //    }
                //    else
                //    {
                //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", timeSpan.Minutes));
                //    }


                //    decimal lastprice = 0;
                //    decimal previoustrend, currenttrend;

                //    foreach (var price in historicals)
                //    {
                //        TimeFrameCandle tc = new TimeFrameCandle();
                //        tc.TimeFrame = timeSpan;
                //        tc.ClosePrice = price.Close;
                //        tc.OpenPrice = price.Open;
                //        tc.HighPrice = price.High;
                //        tc.LowPrice = price.Low;
                //        tc.TotalVolume = price.Volume;
                //        tc.OpenTime = price.TimeStamp;
                //        tc.InstrumentToken = token;
                //        tc.Final = true;
                //        tc.State = CandleStates.Finished;
                //        if (_ema.ContainsKey(tc.InstrumentToken))
                //        {
                //            previoustrend = _ema[_baseInstrumentToken].GetTrend();
                //            _ema[tc.InstrumentToken].Process(tc);
                //            currenttrend = _ema[_baseInstrumentToken].GetTrend();

                //            if (previoustrend == 0 && currenttrend == 1)
                //            {
                //                _downtrendlowswingindexvalue = 0;
                //            }
                //            else if (previoustrend == 1 && currenttrend == 0)
                //            {
                //                _uptrendhighswingindexvalue = 0;
                //            }
                //        }
                //        else
                //        {
                //            ht = new HalfTrend(2, 2);
                //            ht.Process(tc);
                //            _ema.TryAdd(tc.InstrumentToken, ht);
                //        }

                //        _downtrendlowswingindexvalue = _downtrendlowswingindexvalue == 0 ? price.Low : Math.Min(_downtrendlowswingindexvalue, price.Low);
                //        _uptrendhighswingindexvalue = Math.Max(_uptrendhighswingindexvalue, price.High);

                //    }
                //}

                // _previousDownswingValue = _downtrendlowswingindexvalue;
                // _previousUpswingValue = _uptrendhighswingindexvalue;
            }
        }

        private void TriggerEODPositionClose(DateTime currentTime, bool closeAll = false)
        {
            //if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00) || closeAll)
            //{
            DataLogic dl = new DataLogic();

            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var keyvalue = _orderTrios.ElementAt(i);

                    OrderTrio orderTrio = keyvalue.Value;

                    Instrument option = orderTrio.Option;

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime,
                       Tag: _baseInstrumentPrice.ToString(),
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);

                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
                    orderTrio.EntryTradeTime = currentTime;
                    orderTrio.isActive = false;
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    _orderTrios.Remove(keyvalue.Key);
                    i--;
                }
            }

            //if (_callorderTrios.Count > 0)
            //{
            //    for (int i = 0; i < _callorderTrios.Count; i++)
            //    {
            //        OrderTrio orderTrio = _callorderTrios[i];
            //        Instrument option = orderTrio.Option;

            //        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
            //           option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
            //           Tag: "closed",
            //           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
            //           broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

            //        OnTradeEntry(orderTrio.Order);

            //        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
            //        orderTrio.EntryTradeTime = currentTime;
            //        orderTrio.isActive = false;
            //        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance); ;
            //        _callorderTrios.Remove(orderTrio);
            //        i--;
            //    }
            //}
            //if (_putorderTrios.Count > 0)
            //{
            //    for (int i = 0; i < _putorderTrios.Count; i++)
            //    {
            //        OrderTrio orderTrio = _putorderTrios[i];
            //        Instrument option = orderTrio.Option;

            //        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
            //           option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
            //           Tag: "",
            //           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
            //           broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

            //        OnTradeEntry(orderTrio.Order);

            //        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
            //        orderTrio.EntryTradeTime = currentTime;
            //        orderTrio.isActive = false;
            //        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
            //        _putorderTrios.Remove(orderTrio);
            //        i--;

            //    }
            //}

            dl.UpdateAlgoPnl(_algoInstance, _pnl);
            dl.DeActivateAlgo(_algoInstance);
            _pnl = 0;
            _stopTrade = true;
            //}
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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;

                //decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

                //Instrument _tradedOption = OptionUniverse[(int)InstrumentType.PE][atmStrike];
                //if (_tradedOption != null && !SubscriptionTokens.Contains(_tradedOption.InstrumentToken))
                //{
                //    SubscriptionTokens.Add(_tradedOption.InstrumentToken);
                //    dataUpdated = true;
                //}
                //_tradedOption = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                //if (_tradedOption != null && !SubscriptionTokens.Contains(_tradedOption.InstrumentToken))
                //{
                //    SubscriptionTokens.Add(_tradedOption.InstrumentToken);
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
            DateTime? nextExpiry = dl.GetCurrentMonthlyExpiry(_currentDate.Value, _baseInstrumentToken);
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

        //        private bool TrailStopLoss(List<OrderTrio> orderTrios, ref decimal tradeCurrentProfit, ref bool optionTrigerred)
        //        {
        //            bool slHit = false;
        //            if (orderTrios.Count > 0)
        //            {
        //                int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //                decimal currentProfit = orderTrios.Sum(x => x.Order.Quantity * (x.Order.AveragePrice - x.Option.LastPrice)) / totalTradedQty;
        //                //decimal stoploss = orderTrios[0].StopLoss;

        //                if (currentProfit > SL_TRAIL_LIMIT_INITIAL && tradeCurrentProfit == 0)
        //                {
        //                    tradeCurrentProfit = 2;
        //                    //tradeCurrentProfit = Math.Max(currentProfit - SL_TRAIL_LIMIT, tradeCurrentProfit);
        //                }

        //                //if (currentProfit - SL_TRAIL_LIMIT > tradeCurrentProfit)
        //                //{
        //                //    tradeCurrentProfit += SL_TRAIL_LIMIT;
        //                //}

        //                if (currentProfit > SL_TRAIL_LIMIT_INITIAL && currentProfit - tradeCurrentProfit > SL_TRAIL_LIMIT_INITIAL + SL_TRAIL_LIMIT_TRAIL)
        //                {
        //                    tradeCurrentProfit += SL_TRAIL_LIMIT_TRAIL;
        //                }
        //                else if (currentProfit < tradeCurrentProfit && tradeCurrentProfit != 0)
        //                {
        //                    BookStopLoss(orderTrios, false, ref tradeCurrentProfit, closeAll: true);
        //                    tradeCurrentProfit = 0;
        //                    slHit = true;
        //                    optionTrigerred = false;
        //                }


        //                //for (int j = 1; j < 100; j++)
        //                //{
        //                //    stoploss = _tradeQty * _lotSize * SL_TRAIL_LIMIT;
        //                //    if (currentProfit > SL_TRAIL_LIMIT * j && currentProfit < SL_TRAIL_LIMIT * (j + 1))
        //                //    {
        //                //        stoploss = Math.Min(stoploss, orderTrio.Order.AveragePrice - SL_TRAIL_LIMIT * (j - 1));
        //                //        break;
        //                //    }
        //                //}
        //                //if (AllOptions[orderTrio.Option.InstrumentToken].LastPrice > orderTrio.StopLoss)
        //                //{
        //                //    BookStopLoss(orderTrios, false, closeAll: true);
        //                //}
        //            }
        //            return slHit;
        //        }

        //        private void TrailOptions(List<OrderTrio> orderTrios, DateTime currentTime,
        //            decimal bPrice, InstrumentType instrumentType, ref decimal tradeProfit)
        //        {
        //            if (orderTrios.Count > 0)
        //            {
        //                Instrument newOption = GetOptions(currentTime, bPrice, instrumentType, orderTrios, 0);


        //#if BACKTEST
        //                if( newOption.LastPrice == 0)
        //                {
        //                    return;
        //                }
        //#endif

        //                if (newOption.Strike != orderTrios[0].Option.Strike)
        //                {
        //                    MoveOption(orderTrios, newOption);
        //                    tradeProfit = 0;
        //                }
        //            }

        //        }

        //        private void MoveOption(List<OrderTrio> orderTrios, Instrument newOption)
        //        {
        //            OrderTrio orderTrio;
        //            DataLogic dl = new DataLogic();
        //            int qty = 0;
        //            Instrument option;
        //            decimal stopLoss = 0;
        //            for (int i = 0; i < orderTrios.Count; i++)
        //            {
        //                orderTrio = orderTrios[i];
        //                option = orderTrio.Option;
        //                stopLoss = orderTrio.BaseInstrumentStopLoss;
        //                //buyback existing option, and sell at new swing low
        //                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //                   option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime, Tag: "",
        //                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
        //                   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //                OnTradeExit(order);

        //                _pnl += order.Quantity * order.AveragePrice * -1;

        //                qty += order.Quantity;
        //                //orderTrio.isActive = true;
        //                orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
        //                orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //            }
        //            orderTrios.Clear();

        //            orderTrio = new OrderTrio();
        //            //buyback existing option, and sell at new swing low
        //            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, newOption.TradingSymbol, newOption.InstrumentType, newOption.LastPrice,
        //               newOption.KToken, false, qty, algoIndex, _bCandle.CloseTime,
        //               Tag: "",
        //               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //               broker: Constants.KOTAK,
        //               httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //            orderTrio.Option = newOption;
        //            orderTrio.BaseInstrumentStopLoss = stopLoss;
        //            orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
        //            //first target is 1:1 R&R
        //            orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - stopLoss;
        //            _lastOrderTransactionType = "sell";

        //            OnTradeEntry(orderTrio.Order);

        //            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

        //            orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //            dl.UpdateAlgoPnl(_algoInstance, _pnl);

        //            orderTrios.Add(orderTrio);
        //        }

        //        /// <summary>
        //        /// Initial option would be 250 points distance.
        //        /// Then move closure only after hitting first target or if option price is lower than 3 * First target and profit is 50% of first target.
        //        /// Move further if bprice comes closure than 150 points.
        //        /// If moved closure, than move back before it eats up all the profit of earlier option.
        //        /// 
        //        /// </summary>
        //        /// <param name="currentTime"></param>
        //        /// <param name="bPrice"></param>
        //        /// <param name="instrumentType"></param>
        //        /// <returns></returns>
        //        private Instrument GetOptions(DateTime currentTime, decimal bPrice, InstrumentType instrumentType,
        //            List<OrderTrio> orderTrios, decimal stopLoss)
        //        {
        //            Instrument activeOption = null;
        //            decimal strike = 0;

        //            if (orderTrios.Count == 0)
        //            {
        //                if (instrumentType == InstrumentType.CE)
        //                {
        //                    strike = GetCEStrike(bPrice, stopLoss);
        //                }
        //                else
        //                {
        //                    strike = GetPEStrike(bPrice, stopLoss);
        //                }

        //                activeOption = OptionUniverse[(int)instrumentType][strike];
        //            }
        //            else
        //            {
        //                OrderTrio orderTrio = orderTrios[orderTrios.Count - 1];
        //                activeOption = orderTrio.Option;

        //                if (instrumentType == InstrumentType.CE)
        //                {
        //                    //check if there is a loss
        //                    if ((_previousStrikeCEProfit == 0 && activeOption.LastPrice - orderTrio.Order.AveragePrice > 20)
        //                        || (_previousStrikeCEProfit != 0 && activeOption.LastPrice - orderTrio.Order.AveragePrice > _previousStrikeCEProfit - 5))
        //                    {
        //                        strike = activeOption.Strike + 100;
        //                        _previousStrikeCEProfit = 0;

        //                        if (!_thirdCETargetActive && !_thirdtargetmovedCE)
        //                            _thirdtargetmovedCE = true;
        //                        else if (!_secondCETargetActive && !_secondtargetmovedCE)
        //                            _secondtargetmovedCE = true;
        //                        else if (!_firstCETargetActive && _firsttargetmovedCE)
        //                            _firsttargetmovedCE = true;
        //                    }

        //                    //Move closure if first target is hit
        //                    if (!_thirdCETargetActive && !_thirdtargetmovedCE)
        //                    {
        //                        strike = GetCEStrike(bPrice, orderTrios[0].BaseInstrumentStopLoss);
        //                        strike = strike - 100;

        //                        _previousStrikeCEProfit = orderTrio.Order.AveragePrice - activeOption.LastPrice;
        //                        _thirdtargetmovedCE = true;

        //                    }
        //                    else if (!_secondCETargetActive && !_secondtargetmovedCE)
        //                    {
        //                        strike = GetCEStrike(bPrice, orderTrios[0].BaseInstrumentStopLoss);
        //                        strike = strike - 100;

        //                        _previousStrikeCEProfit = orderTrio.Order.AveragePrice - activeOption.LastPrice;
        //                        _secondtargetmovedCE = true;
        //                    }
        //                    else if (!_firstCETargetActive && _firsttargetmovedCE)
        //                    {
        //                        strike = GetCEStrike(bPrice, orderTrios[0].BaseInstrumentStopLoss);
        //                        strike = strike - 100;
        //                        _previousStrikeCEProfit = orderTrio.Order.AveragePrice - activeOption.LastPrice;
        //                        _firsttargetmovedCE = true;
        //                    }

        //                    //while (OptionUniverse[(int)InstrumentType.CE].ContainsKey(strike) && OptionUniverse[(int)InstrumentType.CE][strike].LastPrice < 120)
        //                    //{
        //                    //    strike -= 100;
        //                    //}
        //                }
        //                else
        //                {

        //                    //check if there is a loss
        //                    if ((_previousStrikePEProfit == 0 && activeOption.LastPrice - orderTrio.Order.AveragePrice > 20)
        //                        || (_previousStrikePEProfit != 0 && activeOption.LastPrice - orderTrio.Order.AveragePrice > _previousStrikePEProfit - 5))
        //                    {
        //                        strike = activeOption.Strike - 100;
        //                        _previousStrikePEProfit = 0;

        //                        if (!_thirdtargetmovedPE && !_thirdPETargetActive)
        //                            _thirdtargetmovedPE = true;
        //                        else if (!_secondPETargetActive && !_secondtargetmovedPE)
        //                            _secondtargetmovedPE = true;
        //                        else if (!_firstPETargetActive && !_firsttargetmovedPE)
        //                            _firsttargetmovedPE = true;
        //                    }

        //                    //Move closure if first target is hit
        //                    else if (!_thirdPETargetActive && !_thirdtargetmovedPE)
        //                    {
        //                        strike = GetPEStrike(bPrice, orderTrios[0].BaseInstrumentStopLoss);
        //                        strike = strike + 100;
        //                        _previousStrikePEProfit = orderTrio.Order.AveragePrice - activeOption.LastPrice;
        //                        _thirdtargetmovedPE = true;
        //                    }
        //                    else if (!_secondPETargetActive && !_secondtargetmovedPE)
        //                    {
        //                        strike = GetPEStrike(bPrice, orderTrios[0].BaseInstrumentStopLoss);
        //                        strike += 100;
        //                        _previousStrikePEProfit = orderTrio.Order.AveragePrice - activeOption.LastPrice;
        //                        _secondtargetmovedPE = true;
        //                    }
        //                    else if (!_firstPETargetActive && !_firsttargetmovedPE)
        //                    {
        //                        strike = GetPEStrike(bPrice, orderTrios[0].BaseInstrumentStopLoss);
        //                        strike += 100;
        //                        _previousStrikePEProfit = orderTrio.Order.AveragePrice - activeOption.LastPrice;
        //                        _firsttargetmovedPE = true;
        //                    }

        //                    //while (OptionUniverse[(int)InstrumentType.PE].ContainsKey(strike) && OptionUniverse[(int)InstrumentType.PE][strike].LastPrice < 120)
        //                    //{
        //                    //    strike += 100;
        //                    //}
        //                }
        //                if (strike != 0)
        //                {
        //                    if (!OptionUniverse[(int)instrumentType].ContainsKey(strike))
        //                    {
        //                        _maxDistanceFromBInstrument = Math.Max(_maxDistanceFromBInstrument, Math.Abs(bPrice - strike) + 100);
        //                        LoadOptionsToTrade(currentTime, forceReload: true);
        //                    }

        //                    activeOption = OptionUniverse[(int)instrumentType][strike];

        //                }
        //            }
        //            //activeOption = OptionUniverse[(int)instrumentType][strike];

        //            return activeOption;
        //        }


        //        private void TrailPNLStopLoss(DateTime currentTime)
        //        {
        //            decimal currentProfit = _pnl;

        //            for (int i = 0; i < _putorderTrios.Count; i++)
        //            {
        //                var orderTrio = _putorderTrios[i];
        //                Order order = orderTrio.Order;

        //                //currentProfit += (order.AveragePrice - AllOptions[orderTrio.Option.InstrumentToken].LastPrice)* order.Quantity * -1;
        //                currentProfit += AllOptions[orderTrio.Option.InstrumentToken].LastPrice * order.Quantity * -1;
        //            }
        //            for (int i = 0; i < _callorderTrios.Count; i++)
        //            {
        //                var orderTrio = _callorderTrios[i];
        //                Order order = orderTrio.Order;

        //                currentProfit += AllOptions[orderTrio.Option.InstrumentToken].LastPrice * order.Quantity * -1;
        //            }

        //            for (int j = 1; j < 20; j++)
        //            {
        //                if (currentProfit <= PNL_SL_TRAIL_LIMIT)
        //                {
        //                    break;
        //                }
        //                if (currentProfit > PNL_SL_TRAIL_LIMIT * j && currentProfit < PNL_SL_TRAIL_LIMIT * (j + 1))
        //                {
        //                    TriggerEODPositionClose(currentTime, true);

        //                    // _maxLossForDay = Math.Max(_maxLossForDay, PNL_SL_TRAIL_LIMIT * (j - 1));

        //                    break;
        //                }
        //            }

        //            //if (currentProfit < _maxLossForDay)
        //            //{
        //            //    TriggerEODPositionClose(currentTime, true);
        //            //}
        //        }

        //    private bool BookStopLoss(List<OrderTrio> orderTrios, bool supportingTrend, ref decimal tradeCurrentProfit, bool closeAll = false)
        //    {
        //        int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //        decimal currentLoss = orderTrios.Sum(x => x.Order.Quantity * (x.Option.LastPrice - x.Order.AveragePrice));
        //        //decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * STOP_LOSS_POINTS;
        //        //decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * (supportingTrend ? STOP_LOSS_POINTS_GAMMA: STOP_LOSS_POINTS);
        //        decimal maxLoss = totalTradedQty * (supportingTrend ? STOP_LOSS_POINTS_GAMMA : STOP_LOSS_POINTS);

        //        bool lossBooked = false;
        //        if (currentLoss > maxLoss || closeAll)
        //        {
        //            for (int i = 0; i < orderTrios.Count; i++)
        //            {
        //                var orderTrio = orderTrios[i];
        //                Instrument option = orderTrio.Option;
        //                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
        //                   option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime, Tag: "",
        //                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //                   broker: Constants.KOTAK,
        //                   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //                OnTradeExit(order);

        //                _pnl += order.Quantity * order.AveragePrice * -1;

        //                orderTrio.isActive = false;
        //                orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
        //                DataLogic dl = new DataLogic();
        //                orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //                dl.UpdateAlgoPnl(_algoInstance, _pnl);
        //                orderTrios.Remove(orderTrio);
        //                i--;

        //                //this is used to trail SL
        //                tradeCurrentProfit = 0;
        //            }

        //            lossBooked = true;
        //        }
        //        return lossBooked;
        //    }

        //    private int BookPartialProfit(List<OrderTrio> orderTrios, Tick tick,
        //        bool trendChanged,/* ref bool preFirstTradeActive,*/ ref bool firstTradeActive, ref bool secondTradeActive,
        //ref bool thirdTradeActive, ref decimal tradeCurrentProfit, ref bool firstTargetMoved, ref bool secondTargetMoved)
        //    {
        //        //private int BookPartialProfit(List<OrderTrio> orderTrios, Tick tick)
        //        //{
        //        //int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //        //int qty = _tradeQty * Convert.ToInt32(_lotSize) / 3;
        //        //qty -= qty % 25;
        //        //int quantityTraded = 0;

        //        int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //        int totalQty = _tradeQty * Convert.ToInt32(_lotSize);
        //        int qty = 0;

        //        int quantityTraded = 0;

        //        for (int i = 0; i < orderTrios.Count; i++)
        //        {
        //            var orderTrio = orderTrios[i];
        //            if (orderTrio.Option.InstrumentToken == tick.InstrumentToken)
        //            {
        //                if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell")
        //                //&&
        //                //// 25% quantity at first target
        //                //(
        //                //((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET)
        //                //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)) ||
        //                //// 50% quantity at second target
        //                //((orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET)
        //                //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4 )) ||

        //                //// 75% quantity at third target
        //                //((orderTrio.Order.AveragePrice > tick.LastPrice + THIRD_TARGET)
        //                //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4))
        //                //||

        //                //// Trend change , book 1/4 provided it is in profit
        //                //((orderTrio.Order.AveragePrice > tick.LastPrice && trendChanged)
        //                //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4))
        //                //))
        //                {
        //                    // if ((orderTrio.Order.AveragePrice > tick.LastPrice + PRE_FIRST_TARGET)
        //                    //&& totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)
        //                    //&& preFirstTradeActive)
        //                    // {
        //                    //     preFirstTradeActive = false;
        //                    //     qty = Convert.ToInt32(_preFirstTargetQty * totalQty);
        //                    // }
        //                    // else
        //                    if ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET)
        //                    && totalTradedQty > (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)
        //                    && firstTradeActive)
        //                    {
        //                        firstTradeActive = false;
        //                        qty = Convert.ToInt32(_firstTargetQty * totalQty);
        //                    }
        //                    else if (((orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET)

        //                        || (firstTargetMoved && orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET - FIRST_TARGET))

        //                    && totalTradedQty > (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4)
        //                    && secondTradeActive)
        //                    {
        //                        qty = Convert.ToInt32(_secondTargetQty * totalQty);
        //                        secondTradeActive = false;
        //                        //if (totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4))
        //                        //{
        //                        //    //secondTradeActive = false;
        //                        //    qty = Convert.ToInt32(_secondTargetQty * totalQty);
        //                        //}
        //                        //else
        //                        //{
        //                        //    //secondTradeActive = false;
        //                        //    qty = Convert.ToInt32(_secondTargetQty * totalQty) / 2;
        //                        //}
        //                    }
        //                    else if (((orderTrio.Order.AveragePrice > tick.LastPrice + THIRD_TARGET)
        //                         || (secondTargetMoved && orderTrio.Order.AveragePrice > tick.LastPrice + THIRD_TARGET - SECOND_TARGET)
        //                        )
        //                    && totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4)
        //                    && thirdTradeActive)
        //                    {
        //                        thirdTradeActive = false;
        //                        qty = Convert.ToInt32(_thirdTargetQty * totalQty);
        //                    }
        //                    // Trend change , book 1/4 provided it is in profit
        //                    else if ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET && trendChanged)
        //                    && totalTradedQty == (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4))
        //                    {
        //                        qty = Convert.ToInt32(_thirdTargetQty * totalQty);
        //                    }
        //                    if (qty > 0)
        //                    {
        //                        qty -= qty % Convert.ToInt32(_lotSize);
        //                        Instrument option = orderTrio.Option;

        //                        if (orderTrio.Order.Quantity <= qty)
        //                        {
        //                            qty = orderTrio.Order.Quantity;
        //                        }

        //                        if (orderTrio.Order.Quantity >= qty)
        //                        {
        //                            quantityTraded = qty;
        //                            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
        //                               option.KToken, true, qty, algoIndex, _bCandle.CloseTime, Tag: "",
        //                               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //                               broker: Constants.KOTAK,
        //                               httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //                            OnTradeExit(order);

        //                            _pnl += order.Quantity * order.AveragePrice * -1;

        //                            orderTrio.isActive = false;
        //                            orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
        //                            DataLogic dl = new DataLogic();
        //                            orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //                            dl.UpdateAlgoPnl(_algoInstance, _pnl);
        //                        }
        //                        if (orderTrio.Order.Quantity <= qty)
        //                        {
        //                            orderTrios.Remove(orderTrio);
        //                            i--;

        //                            //this is used to trade SL
        //                            tradeCurrentProfit = 0;
        //                        }
        //                        else
        //                        {
        //                            orderTrio.Order.Quantity -= qty;
        //                        }

        //                        totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //                    }
        //                }
        //                //if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" 
        //                //    &&
        //                //    // 25% quantity at first target
        //                //    ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET
        //                //    && totalTradedQty >= _tradeQty * Convert.ToInt32(_lotSize) * 2 / 3) ||
        //                //    // 50% quantity at first target
        //                //    (orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET
        //                //    && totalTradedQty >= _tradeQty * Convert.ToInt32(_lotSize) * 1 / 3
        //                //    )))
        //                //{
        //                //    Instrument option = orderTrio.Option;

        //                //    if (orderTrio.Order.Quantity <= qty)
        //                //    {
        //                //        qty = orderTrio.Order.Quantity;
        //                //    }

        //                //    if (orderTrio.Order.Quantity >= qty)
        //                //    {
        //                //        quantityTraded = qty;
        //                //        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
        //                //           option.KToken, true, qty, algoIndex, _bCandle.CloseTime, Tag: "",
        //                //           product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //                //           broker: Constants.KOTAK,
        //                //           httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //                //        OnTradeExit(order);

        //                //        _pnl += order.Quantity * order.AveragePrice * -1;

        //                //        orderTrio.isActive = false;
        //                //        orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
        //                //        DataLogic dl = new DataLogic();
        //                //        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //                //        dl.UpdateAlgoPnl(_algoInstance, _pnl);
        //                //    }
        //                //    if (orderTrio.Order.Quantity <= qty)
        //                //    {
        //                //        orderTrios.Remove(orderTrio);
        //                //        i--;
        //                //    }
        //                //    else
        //                //    {
        //                //        orderTrio.Order.Quantity -= qty;
        //                //    }

        //                //    totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //                //}
        //                //break;
        //            }
        //        }
        //        return quantityTraded;
        //    }


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
        //private int GetTradedQuantity(decimal triggerLevel, decimal SL, int totalQuantity,
        //    int bookedQty, bool thirdTargetActive)
        //{
        //    int tradedQuantity = 0;

        //    //if (Math.Abs(triggerLevel - SL) > 100)
        //    //{
        //    if (Math.Abs(triggerLevel - SL) < 90)//50) //wednesday 20
        //    {
        //        //tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //        tradedQuantity = totalQuantity;// * 2 / 4;
        //        tradedQuantity -= tradedQuantity % Convert.ToInt32(_lotSize);
        //    }
        //    else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
        //    {
        //        //tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //        tradedQuantity = totalQuantity * 2 / 4;
        //        tradedQuantity -= tradedQuantity % Convert.ToInt32(_lotSize);
        //    }
        //    tradedQuantity = totalQuantity;// * 2 / 4;
        //    tradedQuantity -= tradedQuantity % Convert.ToInt32(_lotSize);
        //    //if (Math.Abs(triggerLevel - SL) < 130)
        //    //{
        //    //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
        //    //    tradedQuantity = totalQuantity;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}
        //    //else if (Math.Abs(triggerLevel - SL) < 170)
        //    //{
        //    //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //    //    tradedQuantity = totalQuantity * 2 / 4;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}
        //    //else if (Math.Abs(triggerLevel - SL) < 230)
        //    //{
        //    //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
        //    //    tradedQuantity = totalQuantity * 1 / 4;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}
        //    // }
        //    //else if (Math.Abs(triggerLevel - SL) < 400 && thirdTargetActive)
        //    //{
        //    //    tradedQuantity = totalQuantity * 1 / 4;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}

        //    //if (Math.Abs(triggerLevel - SL) < 50)
        //    //{
        //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
        //    //    tradedQuantity = totalQuantity;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}
        //    //else if (Math.Abs(triggerLevel - SL) < 150 && thirdTargetActive)
        //    //{
        //    //    tradedQuantity = totalQuantity * 2 / 4;
        //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}

        //    //if (Math.Abs(triggerLevel - SL) < 90)
        //    //{
        //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
        //    //    tradedQuantity = totalQuantity;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}
        //    //else if (Math.Abs(triggerLevel - SL) < 150)
        //    //{
        //    //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
        //    //    tradedQuantity = totalQuantity;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}
        //    //else if (Math.Abs(triggerLevel - SL) < 90 && thirdTargetActive)
        //    //{
        //    //    tradedQuantity = totalQuantity * 2 / 4;
        //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //    //    tradedQuantity -= tradedQuantity % 25;
        //    //}

        //    //if (_candleTimeSpan.Minutes == 1)
        //    //{
        //    //    if (Math.Abs(triggerLevel - SL) < 150)
        //    //    {
        //    //        //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
        //    //        tradedQuantity = totalQuantity;
        //    //        tradedQuantity -= tradedQuantity % 25;
        //    //    }
        //    //    //else if (Math.Abs(triggerLevel - SL) < 150)
        //    //    //{
        //    //    //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
        //    //    //    tradedQuantity = totalQuantity * 3 / 4;
        //    //    //    tradedQuantity -= tradedQuantity % 25;
        //    //    //}
        //    //    else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
        //    //    {
        //    //        tradedQuantity = totalQuantity * 2 / 4;
        //    //        //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //    //        tradedQuantity -= tradedQuantity % 25;
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    //if (Math.Abs(triggerLevel - SL) < 150)
        //    //    //{
        //    //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
        //    //    //    tradedQuantity = totalQuantity;
        //    //    //    tradedQuantity -= tradedQuantity % 25;
        //    //    //}
        //    //    ////else if (Math.Abs(triggerLevel - SL) < 150)
        //    //    ////{
        //    //    ////    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
        //    //    ////    tradedQuantity = totalQuantity * 3 / 4;
        //    //    ////    tradedQuantity -= tradedQuantity % 25;
        //    //    ////}
        //    //    //else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
        //    //    //{
        //    //    //    tradedQuantity = totalQuantity * 2 / 4;
        //    //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
        //    //    //    tradedQuantity -= tradedQuantity % 25;
        //    //    //}
        //    //}

        //    //tradedQuantity = totalQuantity;

        //    return tradedQuantity - bookedQty;
        //}



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

        //private void LastSwingHighlow(DateTime currentTime, uint token, out decimal swingHigh, out decimal swingLow)
        //{
        //    IEnumerable<Candle> candles = null;
        //    //max and min for last 1 hour
        //    if (TimeCandles[token].Count >= 13 || _candleTimeSpan.Minutes == 5)
        //    {
        //        candles = TimeCandles[token].TakeLast(Math.Min(TimeCandles[token].Count, 25)).SkipLast(1);
        //    }
        //    else
        //    {
        //        DataLogic dl = new DataLogic();
        //        DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime, 2);
        //        List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime, string.Format("{0}minute", _candleTimeSpan.Minutes));
        //        historicals = historicals.OrderByDescending(x => x.TimeStamp).ToList();
        //        List<Candle> historicalCandles = new List<Candle>();
        //        foreach (var price in historicals)
        //        {
        //            TimeFrameCandle tc = new TimeFrameCandle();
        //            tc.TimeFrame = _candleTimeSpan;
        //            tc.ClosePrice = price.Close;
        //            tc.OpenPrice = price.Open;
        //            tc.HighPrice = price.High;
        //            tc.LowPrice = price.Low;
        //            tc.TotalVolume = price.Volume;
        //            tc.OpenTime = price.TimeStamp;
        //            tc.InstrumentToken = token;
        //            tc.Final = true;
        //            tc.State = CandleStates.Finished;
        //            historicalCandles.Add(tc);
        //        }
        //        candles = historicalCandles;
        //    }

        //    //do the analysis with double candle size
        //    List<Candle> dcandles = new List<Candle>();
        //    for (int i = 0; i < candles.Count(); i = i + 2)
        //    {
        //        Candle e = GetLastCandle(candles.ElementAt(i), i + 1 < candles.Count() ? candles.ElementAt(i + 1) : candles.ElementAt(i));
        //        dcandles.Add(e);
        //    }


        //    swingHigh = 0;
        //    swingLow = 1000000;
        //    decimal minPrice = 1000000, maxPrice = 0;

        //    decimal lastminPrice = 1000000, secondlastminprice = 1000000;
        //    decimal lastmaxPrice = 0, secondlastmaxprice = 0;
        //    //int highCounter = 0, lowCounter = 0, higerlowCounter = 0, lowerhighCounter = 0;
        //    int candleCounter = 0;
        //    bool swingHighFound = false, swingLowFound = false;
        //    //int beforehighCounter = 0, afterhighCounter = 0, beforelowCounter = 0, afterhighcounter = 0;
        //    foreach (var ohlc in dcandles.OrderByDescending(x => x.CloseTime))
        //    {
        //        minPrice = minPrice > ohlc.LowPrice ? ohlc.LowPrice : minPrice;
        //        maxPrice = maxPrice < ohlc.HighPrice ? ohlc.HighPrice : maxPrice;
        //        candleCounter++;
        //        if (ohlc.HighPrice > swingHigh && !swingHighFound && candleCounter > 2)
        //        {
        //            swingHigh = ohlc.HighPrice;
        //            swingHighFound = true;
        //        }

        //        if (ohlc.LowPrice < swingLow && !swingLowFound && candleCounter > 2)
        //        {
        //            swingLow = ohlc.LowPrice;
        //            swingLowFound = true;
        //        }

        //        if (swingHighFound && swingLowFound)
        //        {
        //            break;
        //        }

        //        secondlastmaxprice = lastmaxPrice;
        //        secondlastminprice = lastminPrice;
        //        lastminPrice = ohlc.LowPrice;
        //        lastmaxPrice = ohlc.HighPrice;
        //    }

        //    ////int beforehighCounter = 0, afterhighCounter = 0, beforelowCounter = 0, afterhighcounter = 0;
        //    //foreach (var ohlc in dcandles.OrderByDescending(x => x.CloseTime))
        //    //{
        //    //    minPrice = minPrice > ohlc.LowPrice ? ohlc.LowPrice : minPrice;
        //    //    maxPrice = maxPrice < ohlc.HighPrice ? ohlc.HighPrice : maxPrice;

        //    //    if (ohlc.HighPrice > lastmaxPrice && ohlc.HighPrice > secondlastmaxprice && highCounter < 2)
        //    //    {
        //    //        swingHigh = ohlc.HighPrice;
        //    //        highCounter = 0;
        //    //    }
        //    //    else if (ohlc.HighPrice < swingHigh)
        //    //    {
        //    //        highCounter++;
        //    //    }

        //    //    if (ohlc.LowPrice < lastminPrice && ohlc.LowPrice < secondlastminprice && lowCounter < 2)
        //    //    {
        //    //        swingLow = ohlc.LowPrice;
        //    //        lowCounter = 0;
        //    //    }
        //    //    else if (ohlc.LowPrice > swingLow)
        //    //    {
        //    //        lowCounter++;
        //    //    }
        //    //    if (lowCounter > 2 && highCounter > 2)
        //    //    {
        //    //        break;
        //    //    }

        //    //    secondlastmaxprice = lastmaxPrice;
        //    //    secondlastminprice = lastminPrice;
        //    //    lastminPrice = ohlc.LowPrice;
        //    //    lastmaxPrice = ohlc.HighPrice;
        //    //}
        //}
        //private void LastSwingHigh(DateTime currentTime, decimal token, decimal lastPrice, bool high, out decimal swingHigh, out decimal swingLow)
        //{
        //    //DataLogic dl = new DataLogic();
        //    //DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
        //    //DateTime previousWeeklyExpiry = dl.GetPreviousWeeklyExpiry(currentTime, 2);
        //    //List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
        //    ////List<Historical> rpdOHLCList = ZObjects.kite.GetHistoricalData(_referenceBToken.ToString(), previousTradingDate, currentTime.Date, "hour");
        //    //List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");
        //    ////List<Historical> rpdOHLCDay = ZObjects.kite.GetHistoricalData(_referenceBToken.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

        //    DateTime fromDate = currentTime.AddMonths(-1);
        //    DateTime toDate = currentTime.AddDays(-1).Date;
        //    List<Historical> pdOHLCMonth = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

        //    swingHigh = 0;
        //    swingLow = 1000000;

        //    if (high)
        //    {
        //        foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
        //        {
        //            if (ohlc.High < swingHigh)
        //            {
        //                swingHigh = ohlc.High;
        //            }
        //            if (ohlc.Low > swingLow)
        //            {
        //                swingLow = ohlc.Low;
        //            }
        //            else if (ohlc.High > lastPrice)
        //            {
        //                swingHigh = ohlc.High;
        //                continue;
        //            }
        //        }


        //        foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
        //        {
        //            if (ohlc.Low > swingLow)
        //            {
        //                break;
        //            }
        //            //else if (ohlc.High < lastPrice)
        //            //{
        //            //    swingLow = ohlc.High;
        //            //    continue;
        //            //}
        //            else if (ohlc.Low > lastPrice)
        //            {
        //                swingLow = ohlc.Low;
        //                continue;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
        //        {
        //            if (ohlc.High < swingHigh)
        //            {
        //                break;
        //            }
        //            //else if (ohlc.Low > lastPrice)
        //            //{
        //            //    swingHigh = ohlc.Low;
        //            //    continue;
        //            //}
        //            else if (ohlc.High < lastPrice)
        //            {
        //                swingHigh = ohlc.High;
        //                continue;
        //            }
        //        }


        //        foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
        //        {
        //            if (ohlc.Low > swingLow)
        //            {
        //                break;
        //            }
        //            //else if (ohlc.High < lastPrice)
        //            //{
        //            //    swingLow = ohlc.High;
        //            //    continue;
        //            //}
        //            else if (ohlc.Low < lastPrice)
        //            {
        //                swingLow = ohlc.Low;
        //                continue;
        //            }
        //        }
        //    }
        //    return;
        //}



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


        //        private Candle GetLastCandle(uint instrumentToken, int length)
        //        {
        //            if (TimeCandles[instrumentToken].Count < length)
        //            {
        //                return null;
        //            }
        //            var lastCandles = TimeCandles[instrumentToken].TakeLast(length);
        //            TimeFrameCandle tC = new TimeFrameCandle();
        //            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
        //            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
        //            tC.ClosePrice = lastCandles.ElementAt(length - 1).ClosePrice;
        //            tC.CloseTime = lastCandles.ElementAt(length - 1).CloseTime;
        //            tC.HighPrice = Math.Max(Math.Max(lastCandles.ElementAt(0).HighPrice, lastCandles.ElementAt(length - 2).HighPrice), lastCandles.ElementAt(length - 1).HighPrice);
        //            tC.LowPrice = Math.Min(Math.Min(lastCandles.ElementAt(0).LowPrice, lastCandles.ElementAt(length - 2).LowPrice), lastCandles.ElementAt(length - 1).LowPrice);
        //            tC.State = CandleStates.Finished;
        //            tC.Final = true;

        //            return tC;
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
