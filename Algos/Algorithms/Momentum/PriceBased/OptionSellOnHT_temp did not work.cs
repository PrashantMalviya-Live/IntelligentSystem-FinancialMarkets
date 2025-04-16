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

        List<uint> _HTLoaded, _cprLoaded;
        List<uint> _SQLLoadingHT;

        public Dictionary<uint, Instrument> AllStocks { get; set; }
        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        decimal _downtrendlowswingindexvalue = 0;
        decimal _uptrendhighswingindexvalue = 0;
        //Dictionary<uint, HalfTrend> _stockTokenHalfTrendLong;
        //Dictionary<uint, HalfTrend> _stockTokenHT;
        TimeSpan _candleTimeSpanShort, _candleTimeSpanLong;

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
        //11 AM to 1Pm suspension
        private readonly TimeSpan SUSPENSION_WINDOW_START = new TimeSpan(17, 00, 0);
        private readonly TimeSpan SUSPENSION_WINDOW_END = new TimeSpan(18, 00, 0);
        

        //25% quantity booked in each target
        private const decimal FIRST_TARGET = 20;
        bool _firstPETargetActive = true;
        bool _firstCETargetActive = true;
        decimal _firstTargetQty = 0.25m;
        
        private const decimal SECOND_TARGET = 40;
        bool _secondPETargetActive = true;
        bool _secondCETargetActive = true;
        decimal _secondTargetQty = 0.50m;

        private const decimal THIRD_TARGET = 60;
        bool _thirdPETargetActive = true;
        bool _thirdCETargetActive = true;
        decimal _thirdTargetQty = 0.25m;

        private const int TRANCHES_COUNT = 10;

        //SL for this
        private const int STOP_LOSS_POINTS = 30; //40 points max loss on an average 
        private const int STOP_LOSS_POINTS_GAMMA = 40; //40 points max loss on an average 
       
        private const int DAY_STOP_LOSS_PERCENT = 1; //40 points max loss on an average 

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
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private SortedList<decimal, int> _rcriticalLevels;
        private SortedList<decimal, int> _rcriticalLevelsWeekly;
        private bool _putTriggered = false;
        private bool _callTrigerred = false;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.OptionSellonHT;
        private decimal _targetProfit;
        private decimal _stopLoss;
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
            //_stockTokenHalfTrendLong = new Dictionary<uint, HalfTrend>();

            _HTLoaded = new List<uint>();
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
                            LoadHistoricalHT(currentTime);
                        }

                        if (OptionUniverse != null)
                        {
                            UpdateOptionPrice(tick);
                            decimal htrend = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend();
                            if (tick.InstrumentToken == _baseInstrumentToken)
                            {
                                MonitorCandles(tick, currentTime);

                                //TakeTrade(currentTime);
                                ExecuteTrade(tick, currentTime);


                                //Stop loss points and day stop loss
                                if (_putorderTrios.Count > 0
                                && BookStopLoss(_putorderTrios, htrend == 0)
                                )
                                {
                                    _putProfitBookedQty = 0;
                                    _putTriggered = false;
                                }
                                if (_callorderTrios.Count > 0
                                && BookStopLoss(_callorderTrios, htrend == 1)
                                )
                                {
                                    _callProfitBookedQty = 0;
                                    _callTrigerred = false;
                                }
                            }
                            else
                            {
                                _putProfitBookedQty += BookPartialProfit(_putorderTrios, tick, htrend == 1, 
                                    ref _firstPETargetActive, ref _secondPETargetActive, ref _thirdPETargetActive);
                                _callProfitBookedQty += BookPartialProfit(_callorderTrios, tick, htrend == 0,
                                    ref _firstCETargetActive, ref _secondCETargetActive, ref _thirdCETargetActive);
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

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (e.InstrumentToken == _baseInstrumentToken)
                {
                    _baseCandleLoaded = true;
                    _bCandle = e;
                }
                //else if (e.InstrumentToken == _referenceBToken)
                //{
                //    _referenceCandleLoaded = true;
                //    _rCandle = e;
                //}

                if (_baseCandleLoaded)// && _referenceCandleLoaded)
                {
                    #region Halftrend (5 mins), Candle time frame 5 mins.

                    if (_stockTokenHalfTrend.ContainsKey(_bCandle.InstrumentToken))
                    {
                        decimal previoushtred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend
                        
                        _stockTokenHalfTrend[_bCandle.InstrumentToken].Process(_bCandle);

                        decimal htred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend
                        decimal htvalue = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                        bool suspensionWindow = _bCandle.CloseTime.TimeOfDay > SUSPENSION_WINDOW_START && _bCandle.CloseTime.TimeOfDay < SUSPENSION_WINDOW_END;


                        if(TimeCandles[_bCandle.InstrumentToken].Count == 1)
                        {
                            _downtrendlowswingindexvalue = _bCandle.LowPrice;
                            _uptrendhighswingindexvalue = _bCandle.HighPrice;
                        }

                        if (htred == 0 && _bCandle.HighPrice > _uptrendhighswingindexvalue)
                        {
                            _uptrendhighswingindexvalue = _bCandle.HighPrice;
                        }
                        if (htred == 1 && (_bCandle.LowPrice < _downtrendlowswingindexvalue || _downtrendlowswingindexvalue == 0))
                        {
                            _downtrendlowswingindexvalue = _bCandle.LowPrice;
                        }

                        //Uptrend
                        //Sell Put if not sold. do not close call unless SL hit
                        if (previoushtred == 1 && htred == 0)
                        {
                            _callTrigerred = false; 
                            _putTriggered = true;
                            //_uptrendhighswingindexvalue = Math.Max(TimeCandles[_baseInstrumentToken].TakeLast(5).Max(x=>x.HighPrice), _bCandle.HighPrice);
                            _uptrendhighswingindexvalue = _bCandle.HighPrice;

                            if (_putorderTrios.Count == 0)
                            {
                                _putTriggered = true;
                                ////this line can be updated for postion sizing, based on the SL.
                                //int qty = _tradeQty;

                                //OrderTrio orderTrio = new OrderTrio();
                                //Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][Math.Floor(_swingLowPrice / 100) * 100];

                                //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
                                //   _atmOption.KToken, false, qty * Convert.ToInt32(_atmOption.LotSize), algoIndex, _bCandle.CloseTime,
                                //   Tag: "",
                                //   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                //   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null :KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                                //OnTradeEntry(orderTrio.Order);
                                //orderTrio.Option = _atmOption;
                                //orderTrio.StopLoss = _swingLowPrice;
                                //_initialStopLoss = _swingLowPrice;
                                ////first target is 1:1 R&R
                                //orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _swingLowPrice;
                                //_lastOrderTransactionType = "sell";
                                ////_orderTrios.Clear();
                                ////if (!suspensionWindow)
                                ////{
                                ////    _orderTrios.Add(orderTrio);
                                ////}
                                //orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_atmOption.LotSize);
                                //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                                //_putorderTrios.Add(orderTrio);
                            }
                            else
                            {
                                //LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                                OrderTrio orderTrio = _putorderTrios[0];
                                Instrument option = orderTrio.Option;

                                decimal atmStrike = GetPEStrike(_baseInstrumentPrice, _downtrendlowswingindexvalue);// Math.Floor((_downtrendlowswingindexvalue - 50) / 100) * 100;// Math.Floor(_swingLowPrice / 100) * 100;

                                //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                                //{
                                //    atmStrike = Math.Floor((_downtrendlowswingindexvalue - 25) / 50) * 50;// Math.Floor(_swingLowPrice / 50) * 50; 
                                //}

                                int qty = orderTrio.Order.Quantity;
                                
                                if (option.Strike != atmStrike)
                                {
                                    //buyback existing option, and sell at new swing low
                                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                                       option.KToken, true, qty, algoIndex, _bCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK, 
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                    OnTradeExit(orderTrio.Order);

                                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

                                    orderTrio.isActive = false;
                                    orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                    DataLogic dl = new DataLogic();
                                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);

                                    _putorderTrios.Remove(orderTrio);
                                    _putProfitBookedQty = 0;
                                    _uptrendhighswingindexvalue = 0;
                                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                                    qty = GetTradedQuantity(_baseInstrumentPrice, _downtrendlowswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0);

                                    if (qty < _tradeQty * Convert.ToInt32(_lotSize))
                                    {
                                        _putTriggered = true;
                                    }

                                    orderTrio = new OrderTrio();
                                    //buyback existing option, and sell at new swing low
                                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
                                       _atmOption.KToken, false, qty, algoIndex, _bCandle.CloseTime,
                                       Tag:"",
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
                                    int shouldbeQty = GetTradedQuantity(_baseInstrumentPrice, _downtrendlowswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0);

                                    if(shouldbeQty > totalTradedQty)
                                    {
                                        int addtionalQty = shouldbeQty - totalTradedQty;

                                        //sell addtionalqty
                                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                                           option.KToken, false, addtionalQty, algoIndex, _bCandle.CloseTime,
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
                        else if (previoushtred == 0 && htred == 1)
                        {
                            _putTriggered = false;
                            _callTrigerred = true;
                            _downtrendlowswingindexvalue = _bCandle.LowPrice;
                            //_downtrendlowswingindexvalue = Math.Min(TimeCandles[_baseInstrumentToken].TakeLast(5).Max(x => x.LowPrice), _bCandle.LowPrice);

                            if (_callorderTrios.Count == 0)
                            {
                                _callTrigerred = true;
                                ////this line can be updated for postion sizing, based on the SL.
                                //int qty = _tradeQty;

                                //OrderTrio orderTrio = new OrderTrio();
                                //Instrument _atmOption = OptionUniverse[(int)InstrumentType.CE][Math.Ceiling(_swingHighPrice / 100) * 100];

                                //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
                                //   _atmOption.KToken, false, qty * Convert.ToInt32(_atmOption.LotSize), algoIndex, _bCandle.CloseTime,
                                //   Tag: "",
                                //   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                //   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                                //OnTradeEntry(orderTrio.Order);
                                //orderTrio.Option = _atmOption;
                                //orderTrio.StopLoss = _swingHighPrice;
                                //_initialStopLoss = _swingHighPrice;
                                ////first target is 1:1 R&R
                                //orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _swingHighPrice;
                                //_lastOrderTransactionType = "sell";

                                //orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_atmOption.LotSize);
                                //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                                //_callorderTrios.Add(orderTrio);
                            }
                            else
                            {
                                //LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                                OrderTrio orderTrio = _callorderTrios[0];
                                Instrument option = orderTrio.Option;

                                decimal atmStrike = GetCEStrike(_baseInstrumentPrice, _uptrendhighswingindexvalue);// Math.Ceiling((_uptrendhighswingindexvalue + 50) / 100) * 100;// Math.Ceiling(_swingHighPrice / 100) * 100;
                                //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                                //{
                                //    atmStrike = Math.Ceiling((_uptrendhighswingindexvalue + 25) / 50) * 50;// Math.Ceiling(_swingHighPrice / 50) * 50;
                                //}

                                int qty = orderTrio.Order.Quantity;

                                if (option.Strike != atmStrike)
                                {
                                    //buyback existing option, and sell at new swing low
                                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                                       option.KToken, true, qty, algoIndex, _bCandle.CloseTime,
                                       Tag: "",
                                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                       broker: Constants.KOTAK,
                                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                    OnTradeExit(orderTrio.Order);

                                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

                                    orderTrio.isActive = false;
                                    orderTrio.EntryTradeTime = orderTrio.Order.OrderTimestamp.Value;
                                    DataLogic dl = new DataLogic();
                                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);

                                    _callorderTrios.Remove(orderTrio);
                                    _callProfitBookedQty = 0;
                                    _downtrendlowswingindexvalue = 0;
                                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                                    qty = GetTradedQuantity(_baseInstrumentPrice, _uptrendhighswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0);

                                    if (qty < _tradeQty * Convert.ToInt32(_lotSize))
                                    {
                                        _callTrigerred = true;
                                    }

                                    orderTrio = new OrderTrio();
                                    //buyback existing option, and sell at new swing low
                                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
                                       _atmOption.KToken, false, qty, algoIndex, _bCandle.CloseTime,
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
                                    int shouldbeQty = GetTradedQuantity(_baseInstrumentPrice, _uptrendhighswingindexvalue, _tradeQty * Convert.ToInt32(_lotSize), 0);

                                    if (shouldbeQty > totalTradedQty)
                                    {
                                        int addtionalQty = shouldbeQty - totalTradedQty;

                                        //sell addtionalqty
                                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                                           option.KToken, false, addtionalQty, algoIndex, _bCandle.CloseTime,
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
                        if(_callorderTrios.Count != 0)
                        {
                            for(int i = 0; i< _callorderTrios.Count; i++)
                            {
                                var orderTrio = _callorderTrios[i];
                                if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" && _baseInstrumentPrice > orderTrio.StopLoss && htred == 0)
                                {
                                    Instrument option = orderTrio.Option;

                                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
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
                                    ///TODO: Check the impact of this time. THis is causing downtrend to not set again as it is setting to 0 here
                                    //_uptrendhighswingindexvalue = 0;
                                    _callTrigerred = false;
                                }
                            }
                        }
                        if (_putorderTrios.Count != 0)
                        {
                            for (int i = 0; i < _putorderTrios.Count; i++)
                            {
                                var orderTrio = _putorderTrios[i];
                                if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" && _baseInstrumentPrice < orderTrio.StopLoss && htred == 1)
                                {
                                    Instrument option = orderTrio.Option;

                                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "pe", option.LastPrice,
                                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
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
                    #endregion

                    if (_intraday || _expiryDate.Value.Date == _bCandle.CloseTime.Date)
                    {
                        TriggerEODPositionClose(_bCandle.CloseTime, _bCandle.ClosePrice);
                    }
                    _baseCandleLoaded = false;
                    _referenceCandleLoaded = false;
                    _prCandle = _bCandle;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.StackTrace);
            }
        }

        private void ExecuteTrade(Tick tick, DateTime currentTime)
        {
            if (_stockTokenHalfTrend.ContainsKey(_baseInstrumentToken)
                && TimeCandles.ContainsKey(_baseInstrumentToken)
                 && (currentTime.TimeOfDay <= new TimeSpan(15, 12, 00))
                )
            {
                decimal htred = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend(); // 0 is up trend
                decimal htvalue = _stockTokenHalfTrend[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                int tradeQty = 0;
                if (htred == 0 && _putTriggered)
                {
                    decimal stopLoss = 0;
                    if (_putorderTrios.Count == 0 && htvalue >= _baseInstrumentPrice - 11)
                    {
                        //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                        stopLoss = _downtrendlowswingindexvalue;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), 0);
                    }
                    else if (_putorderTrios.Count != 0 && (_baseInstrumentPrice - _putorderTrios[0].StopLoss < 300))
                    {
                        stopLoss = _putorderTrios[0].StopLoss;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), _putProfitBookedQty) - _putorderTrios.Sum(x => x.Order.Quantity);
                        
                        if (_putorderTrios.Sum(x => x.Order.Quantity) == _tradeQty * Convert.ToInt32(_lotSize))
                        {
                            _putTriggered = false;
                            return;
                        }
                    }
                    if (tradeQty <= 0)
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
                else if (htred == 1 && _callTrigerred)
                {
                    decimal stopLoss = 0;
                    if (_callorderTrios.Count == 0 && (htvalue <= _baseInstrumentPrice + 11))
                    {
                        //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);
                        stopLoss = _uptrendhighswingindexvalue;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), 0);
                    }
                    else if (_callorderTrios.Count != 0 && (_callorderTrios[0].StopLoss - _baseInstrumentPrice < 300))
                    {
                        stopLoss = _callorderTrios[0].StopLoss;
                        tradeQty = GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), _callProfitBookedQty) - _callorderTrios.Sum(x => x.Order.Quantity);

                        if (_callorderTrios.Sum(x => x.Order.Quantity) == _tradeQty * Convert.ToInt32(_lotSize))
                        {
                            _callTrigerred = false;
                            return;
                        }
                    }
                    if (tradeQty <= 0)
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

        private decimal GetPEStrike(decimal btoken, decimal stoploss)
        {
            decimal strike;

            if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
            {
                strike = Math.Floor((stoploss - 25) / 50) * 50;
            }
            else
            {
                strike = Math.Floor((stoploss - 50) / 100) * 100;

            }
            return strike;
        }
        private decimal GetCEStrike(decimal bPrice, decimal stoploss)
        {
            decimal strike;

            if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
            {
                strike = Math.Ceiling((stoploss + 25) / 50) * 50;
            }
            else
            {
                strike = Math.Ceiling((stoploss + 50) / 100) * 100;

            }
            return strike;
        }
        private bool BookStopLoss(List<OrderTrio> orderTrios, bool supportingTrend)
        {
            int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
            decimal currentLoss = orderTrios.Sum(x => x.Order.Quantity * (x.Option.LastPrice - x.Order.AveragePrice));
            decimal maxLoss = _tradeQty * Convert.ToInt32(_lotSize) * (supportingTrend? STOP_LOSS_POINTS_GAMMA : STOP_LOSS_POINTS);
            bool lossBooked = false;
            if (currentLoss > maxLoss)
            {
                for (int i = 0; i < orderTrios.Count; i++)
                {
                    var orderTrio = orderTrios[i];
                    Instrument option = orderTrio.Option;
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                       option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime, Tag: "",
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
                }

                lossBooked = true;
            }
            return lossBooked;
        }

        private int BookPartialProfit(List<OrderTrio> orderTrios, Tick tick, bool trendChanged, ref bool firstTradeActive, ref bool secondTradeActive,
            ref bool thirdTradeActive)
        {
            //Case 1: Traded Qty =  Max qty
            // First, second and third target active. and all close then last one at trend change
            // Case 2: Traded Qty = 3 quarters of max qty
            // first and third target active, and all close and then trend change for last one
            // Case 3: Traded Qty = 2 quarters of max qty
            // only second target active. All close and then trend change for last one
            // Case 4: Traded Qty = 1 quarters of max qty
            // Wait to increase till second target/third target and trend change

            int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
            int totalQty = _tradeQty * Convert.ToInt32(_lotSize);
            int qty = 0;
            qty -= qty % 25;
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
                        if ((orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET)
                        && totalTradedQty > (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 4)
                        && firstTradeActive)
                        {
                            //firstTradeActive = false;
                            qty = Convert.ToInt32(_firstTargetQty * totalQty);
                        }
                        else if ((orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET)
                        && totalTradedQty > (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4)
                        && secondTradeActive)
                        {
                            qty = Convert.ToInt32(_secondTargetQty * totalQty);

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
                        && totalTradedQty == (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 4)
                        && thirdTradeActive)
                        {
                            //thirdTradeActive = false;
                            qty = Convert.ToInt32(_thirdTargetQty * totalQty);
                        }
                        if (qty > 0)
                        {
                            Instrument option = orderTrio.Option;

                            if (orderTrio.Order.Quantity <= qty)
                            {
                                qty = orderTrio.Order.Quantity;
                            }

                            if (orderTrio.Order.Quantity >= qty)
                            {
                                quantityTraded = qty;
                                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                                   option.KToken, true, qty, algoIndex, _bCandle.CloseTime, Tag: "",
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
                    //break;
                }
            }
            return quantityTraded;
        }

        //private int BookPartialProfit(List<OrderTrio> orderTrios, Tick tick)
        //{
        //    //Case 1: Traded Qty =  Max qty
        //    // First, second and third target active. and all close then last one at trend change
        //    // Case 2: Traded Qty = 3 quarters of max qty
        //    // first and third target active, and all close and then trend change for last one
        //    // Case 3: Traded Qty = 2 quarters of max qty
        //    // only second target active. All close and then trend change for last one
        //    // Case 4: Traded Qty = 1 quarters of max qty
        //    // Wait to increase till second target/third target and trend change

        //    int totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //    int qty = _tradeQty * Convert.ToInt32(_lotSize) / 3;
        //    qty -= qty % 25;
        //    int quantityTraded = 0;
        //    for (int i = 0; i < orderTrios.Count; i++)
        //    {
        //        var orderTrio = orderTrios[i];
        //        if (orderTrio.Option.InstrumentToken == tick.InstrumentToken)
        //        {
        //            if (orderTrio.Order.TransactionType.ToLower().Trim() == "sell" 
        //                &&
        //                // 25% quantity at first target
        //                ((
        //                (orderTrio.Order.AveragePrice > tick.LastPrice + FIRST_TARGET && _firstTargetActive)
        //                && totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 2 / 3) - Convert.ToInt32(_lotSize)) ||
        //                // 50% quantity at first target
        //                (orderTrio.Order.AveragePrice > tick.LastPrice + SECOND_TARGET
        //                && totalTradedQty >= (_tradeQty * Convert.ToInt32(_lotSize) * 1 / 3 - -Convert.ToInt32(_lotSize))
        //                )))
        //            {
        //                Instrument option = orderTrio.Option;

        //                if (orderTrio.Order.Quantity <= qty)
        //                {
        //                    qty = orderTrio.Order.Quantity;
        //                }

        //                if (orderTrio.Order.Quantity >= qty)
        //                {
        //                    quantityTraded = qty;
        //                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
        //                       option.KToken, true, qty, algoIndex, _bCandle.CloseTime, Tag: "",
        //                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
        //                       broker: Constants.KOTAK,
        //                       httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

        //                    OnTradeExit(order);

        //                    _pnl += order.Quantity * order.AveragePrice * -1;

        //                    orderTrio.isActive = false;
        //                    orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
        //                    DataLogic dl = new DataLogic();
        //                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
        //                    dl.UpdateAlgoPnl(_algoInstance, _pnl);
        //                }
        //                if (orderTrio.Order.Quantity <= qty)
        //                {
        //                    orderTrios.Remove(orderTrio);
        //                    i--;
        //                }
        //                else
        //                {
        //                    orderTrio.Order.Quantity -= qty;
        //                }

        //                totalTradedQty = orderTrios.Sum(x => x.Order.Quantity);
        //            }
        //            //break;
        //        }
        //    }
        //    return quantityTraded;
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

        private int GetTradedQuantity(decimal triggerLevel, decimal SL, int totalQuantity, int bookedQty)
        {
            int tradedQuantity = 0;

            if (Math.Abs(triggerLevel - SL) < 50)
            {
                //tradedQuantity = totalQuantity;
                tradedQuantity = totalQuantity * (bookedQty != 0 ? 4 : 3) / 4;
            }
            else if (Math.Abs(triggerLevel - SL) < 90)
            {
                tradedQuantity = totalQuantity * (bookedQty != 0 ? 3 : 2) / 4;
            }
            else if (Math.Abs(triggerLevel - SL) < 150)
            {
                //tradedQuantity = totalQuantity * 2 / 4;
                tradedQuantity = totalQuantity * (bookedQty != 0 ? 2 : 1) / 4;
                tradedQuantity -= tradedQuantity % 25;
            }
            else if (Math.Abs(triggerLevel - SL) < 250)
            {
                //tradedQuantity = totalQuantity * 1 / 4;
                tradedQuantity = totalQuantity * (bookedQty != 0 ? 1 : 0) / 4;
                tradedQuantity -= tradedQuantity % 25;
            }
            
            return tradedQuantity - bookedQty;
        }

        private void LoadHistoricalHT(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => x == _baseInstrumentToken && !_HTLoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                List<uint> newTokens = new List<uint>();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!_stockTokenHalfTrend.ContainsKey(t) && !_SQLLoadingHT.Contains(t))
                    {
                        newTokens.Add(t);
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingHT.Add(t);
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
                    LoadHistoricalCandlesForHT(newTokens, 28, lastCandleEndTime);
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _stockTokenHalfTrend.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            //_stockTokenHT[tkn].Process(TimeCandles[tkn].First());
                            _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            //_stockTokenHalfTrendLong[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                //_stockTokenHT[tkn].Process(TimeCandles[tkn].First());
                                _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                                //_stockTokenHalfTrendLong[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenHalfTrend.ContainsKey(tkn))
                    {
                        _HTLoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("ADX loaded from DB for {0}", tkn), "MonitorCandles");
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

        private void LoadHistoricalCandlesForHT(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            LoadHistoricalCandlesForHT(tokenList, candlesCount, lastCandleEndTime, _candleTimeSpan);
            //LoadHistoricalCandlesForHTLong(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(1, 0, 30, 0));
            //LoadHistoricalCandlesForADX(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(0, 15, 0));
        }
        
        private void LoadHistoricalCandlesForHT(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            //CandleSeries cs = new CandleSeries();
            //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            HalfTrend ht;
            lock (_stockTokenHalfTrend)
            {

                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, timeSpan.Minutes == 5 ? 1 : timeSpan.Minutes);

                foreach (uint token in tokenList)
                {
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", timeSpan.Minutes));
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
                        if (_stockTokenHalfTrend.ContainsKey(tc.InstrumentToken))
                        {
                            _stockTokenHalfTrend[tc.InstrumentToken].Process(tc);
                        }
                        else
                        {
                            ht = new HalfTrend(2, 2);
                            ht.Process(tc);
                            _stockTokenHalfTrend.TryAdd(tc.InstrumentToken, ht);
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

        private void LastSwingHighlow(DateTime currentTime, uint token, out decimal swingHigh, out decimal swingLow)
        {
            IEnumerable<Candle> candles= null;
            //max and min for last 1 hour
            if (TimeCandles[token].Count >= 13 || _candleTimeSpan.Minutes == 5 )
            {
                candles = TimeCandles[token].TakeLast(Math.Min(TimeCandles[token].Count, 25)).SkipLast(1);
            }
            else
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime, 2);
                List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime, string.Format("{0}minute", _candleTimeSpan.Minutes));
                historicals = historicals.OrderByDescending(x => x.TimeStamp).ToList();
                List<Candle> historicalCandles = new List<Candle>();
                foreach (var price in historicals)
                {
                    TimeFrameCandle tc = new TimeFrameCandle();
                    tc.TimeFrame = _candleTimeSpan;
                    tc.ClosePrice = price.Close;
                    tc.OpenPrice = price.Open;
                    tc.HighPrice = price.High;
                    tc.LowPrice = price.Low;
                    tc.TotalVolume = price.Volume;
                    tc.OpenTime = price.TimeStamp;
                    tc.InstrumentToken = token;
                    tc.Final = true;
                    tc.State = CandleStates.Finished;
                    historicalCandles.Add(tc);
                }
                candles = historicalCandles;
            }

            //do the analysis with double candle size
            List<Candle> dcandles = new List<Candle>();
            for (int i = 0; i < candles.Count(); i = i + 2)
            {
                Candle e = GetLastCandle(candles.ElementAt(i), i + 1 < candles.Count() ? candles.ElementAt(i + 1) : candles.ElementAt(i));
                dcandles.Add(e);
            }
            

            swingHigh = 0;
            swingLow = 1000000;
            decimal minPrice = 1000000, maxPrice = 0;

            decimal lastminPrice = 1000000, secondlastminprice = 1000000;
            decimal lastmaxPrice = 0, secondlastmaxprice = 0;
            //int highCounter = 0, lowCounter = 0, higerlowCounter = 0, lowerhighCounter = 0;
            int candleCounter = 0;
            bool swingHighFound = false, swingLowFound = false;
            //int beforehighCounter = 0, afterhighCounter = 0, beforelowCounter = 0, afterhighcounter = 0;
            foreach (var ohlc in dcandles.OrderByDescending(x => x.CloseTime))
            {
                minPrice = minPrice > ohlc.LowPrice ? ohlc.LowPrice : minPrice;
                maxPrice = maxPrice < ohlc.HighPrice ? ohlc.HighPrice : maxPrice;
                candleCounter++;
                if (ohlc.HighPrice > swingHigh && !swingHighFound && candleCounter > 2)
                {
                    swingHigh = ohlc.HighPrice;
                    swingHighFound = true;
                }

                if (ohlc.LowPrice < swingLow && !swingLowFound && candleCounter > 2)
                {
                    swingLow = ohlc.LowPrice;
                    swingLowFound = true;
                }

                if (swingHighFound && swingLowFound )
                {
                    break;
                }

                secondlastmaxprice = lastmaxPrice;
                secondlastminprice = lastminPrice;
                lastminPrice = ohlc.LowPrice;
                lastmaxPrice = ohlc.HighPrice;
            }

            ////int beforehighCounter = 0, afterhighCounter = 0, beforelowCounter = 0, afterhighcounter = 0;
            //foreach (var ohlc in dcandles.OrderByDescending(x => x.CloseTime))
            //{
            //    minPrice = minPrice > ohlc.LowPrice ? ohlc.LowPrice : minPrice;
            //    maxPrice = maxPrice < ohlc.HighPrice ? ohlc.HighPrice : maxPrice;

            //    if (ohlc.HighPrice > lastmaxPrice && ohlc.HighPrice > secondlastmaxprice && highCounter < 2)
            //    {
            //        swingHigh = ohlc.HighPrice;
            //        highCounter = 0;
            //    }
            //    else if (ohlc.HighPrice < swingHigh)
            //    {
            //        highCounter++;
            //    }

            //    if (ohlc.LowPrice < lastminPrice && ohlc.LowPrice < secondlastminprice && lowCounter < 2)
            //    {
            //        swingLow = ohlc.LowPrice;
            //        lowCounter = 0;
            //    }
            //    else if (ohlc.LowPrice > swingLow)
            //    {
            //        lowCounter++;
            //    }
            //    if (lowCounter > 2 && highCounter > 2)
            //    {
            //        break;
            //    }

            //    secondlastmaxprice = lastmaxPrice;
            //    secondlastminprice = lastminPrice;
            //    lastminPrice = ohlc.LowPrice;
            //    lastmaxPrice = ohlc.HighPrice;
            //}
        }
        private void LastSwingHigh(DateTime currentTime, decimal token, decimal lastPrice, bool high, out decimal swingHigh, out decimal swingLow)
        {
            //DataLogic dl = new DataLogic();
            //DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
            //DateTime previousWeeklyExpiry = dl.GetPreviousWeeklyExpiry(currentTime, 2);
            //List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
            ////List<Historical> rpdOHLCList = ZObjects.kite.GetHistoricalData(_referenceBToken.ToString(), previousTradingDate, currentTime.Date, "hour");
            //List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");
            ////List<Historical> rpdOHLCDay = ZObjects.kite.GetHistoricalData(_referenceBToken.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

            DateTime fromDate = currentTime.AddMonths(-1);
            DateTime toDate = currentTime.AddDays(-1).Date;
            List<Historical> pdOHLCMonth = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

            swingHigh = 0;
            swingLow = 1000000;

            if (high)
            {
                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.High < swingHigh)
                    {
                        swingHigh = ohlc.High;
                    }
                    if (ohlc.Low > swingLow)
                    {
                        swingLow = ohlc.Low;
                    }
                    else if (ohlc.High > lastPrice)
                    {
                        swingHigh = ohlc.High;
                        continue;
                    }
                }


                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.Low > swingLow)
                    {
                        break;
                    }
                    //else if (ohlc.High < lastPrice)
                    //{
                    //    swingLow = ohlc.High;
                    //    continue;
                    //}
                    else if (ohlc.Low > lastPrice)
                    {
                        swingLow = ohlc.Low;
                        continue;
                    }
                }
            }
            else
            {
                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.High < swingHigh)
                    {
                        break;
                    }
                    //else if (ohlc.Low > lastPrice)
                    //{
                    //    swingHigh = ohlc.Low;
                    //    continue;
                    //}
                    else if (ohlc.High < lastPrice)
                    {
                        swingHigh = ohlc.High;
                        continue;
                    }
                }


                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.Low > swingLow)
                    {
                        break;
                    }
                    //else if (ohlc.High < lastPrice)
                    //{
                    //    swingLow = ohlc.High;
                    //    continue;
                    //}
                    else if (ohlc.Low < lastPrice)
                    {
                        swingLow = ohlc.Low;
                        continue;
                    }
                }
            }
            return;
        }


        private void TriggerEODPositionClose(DateTime currentTime, decimal lastPrice)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 15, 00))
            {
                DataLogic dl = new DataLogic();

                if (_callorderTrios.Count > 0)
                {
                    for (int i = 0; i < _callorderTrios.Count; i++)
                    {
                        OrderTrio orderTrio = _callorderTrios[i];
                        Instrument option = orderTrio.Option;

                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                           option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
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
                           option.KToken, true, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
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

        private void TakeTrade(DateTime currentTime)
        {
            if (_stockTokenHalfTrend.ContainsKey(_baseInstrumentToken)
                && TimeCandles.ContainsKey(_baseInstrumentToken)
                )
            {
                decimal htred = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend(); // 0 is up trend
                decimal htvalue = _stockTokenHalfTrend[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                if (htred == 0 && _putorderTrios.Count == 0
                    && (htvalue >= _baseInstrumentPrice - 11 /*|| (_prCandle !=null && _prCandle.LowPrice >= _baseInstrumentPrice - 5)*/)
                    && _putTriggered)
                {
                    _putProfitBookedQty = 0;
                    _putTriggered = false;
                    //this line can be updated for postion sizing, based on the SL.
                    int qty = _tradeQty;
                   // LastSwingHighlow(currentTime, _baseInstrumentToken, out _uptrendhighswingindexvalue, out _swingLowPrice);
                    OrderTrio orderTrio = new OrderTrio();

                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.PE][GetPEStrike(_baseInstrumentPrice, _downtrendlowswingindexvalue)]; //Change -  Take position 50 points farther to avoid gamma loss

                    //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                    //{
                    //    _atmOption = OptionUniverse[(int)InstrumentType.PE][Math.Floor((_downtrendlowswingindexvalue - 25) / 50) * 50]; //Change -  Take position 50 points farther to avoid gamma loss
                    //}

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
                       _atmOption.KToken, false, qty * Convert.ToInt32(_atmOption.LotSize), algoIndex, currentTime,
                       Tag: "",
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);
                    orderTrio.Option = _atmOption;
                    orderTrio.StopLoss = _downtrendlowswingindexvalue;
                    _initialStopLoss = _downtrendlowswingindexvalue;
                    orderTrio.EntryTradeTime = currentTime;
                    //first target is 1:1 R&R
                    orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _downtrendlowswingindexvalue;
                    _lastOrderTransactionType = "sell";
                    //_orderTrios.Clear();
                    //if (!suspensionWindow)
                    //{
                    //    _orderTrios.Add(orderTrio);
                    //}
                    orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_atmOption.LotSize);
                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    _putorderTrios.Add(orderTrio);
                }
                else if (htred == 1
                    && _callorderTrios.Count == 0
                    && (htvalue <= _baseInstrumentPrice + 11 /*|| (_prCandle != null && _prCandle.HighPrice <= _baseInstrumentPrice + 5)*/)
                    && _callTrigerred)
                {
                    _callProfitBookedQty = 0;
                    _callTrigerred = false;
                    //this line can be updated for postion sizing, based on the SL.
                    int qty = _tradeQty;
                    LastSwingHighlow(currentTime, _baseInstrumentToken, out _uptrendhighswingindexvalue, out _downtrendlowswingindexvalue);

                    OrderTrio orderTrio = new OrderTrio();
                    Instrument _atmOption = OptionUniverse[(int)InstrumentType.CE][GetCEStrike(_baseInstrumentPrice, _uptrendhighswingindexvalue)]; //Change -  Take position 50 points farther to avoid gamma loss

                    //if (_baseInstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN))
                    //{
                    //    _atmOption = OptionUniverse[(int)InstrumentType.CE][GetCEStrike(_baseInstrumentPrice, _uptrendhighswingindexvalue)]; //Change -  Take position 50 points farther to avoid gamma loss
                    //}

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
                       _atmOption.KToken, false, qty * Convert.ToInt32(_atmOption.LotSize), algoIndex, currentTime,
                       Tag: "",
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);
                    orderTrio.Option = _atmOption;
                    orderTrio.StopLoss = _uptrendhighswingindexvalue;
                    _initialStopLoss = _uptrendhighswingindexvalue;
                    orderTrio.EntryTradeTime = currentTime;
                    //first target is 1:1 R&R
                    orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _uptrendhighswingindexvalue;
                    _lastOrderTransactionType = "sell";

                    orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_atmOption.LotSize);
                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;

                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    _callorderTrios.Add(orderTrio);
                }
            }
        }

        private void LoadBInstrumentEMA(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_indexEMA)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_indexEMALoading)
                    {
                        _indexEMALoading = true;
                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _indexEMALoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _indexEMA.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var price in TimeCandles[bToken])
                            {
                                _indexEMA.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexEMALoadedFromDB)
                    {
                        _indexEMALoaded = true;
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} EMA loaded from DB for Base Instrument", 20), "LoadBInstrumentEMA");
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
                lock (_indexEMA)
                {
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicals)
                    {
                        _indexEMA.Process(price.Close, isFinal: true);
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
