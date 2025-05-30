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
using Google.Apis.Upload;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;

namespace Algorithms.Algorithms
{
    public class TJ2 : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(TJ2 source);
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

        List<uint> _HTLoaded, _cprLoaded;
        List<uint> _SQLLoadingHT;
        private Instrument _activeFuture, _referenceFuture;
        private decimal _indexVWAP;
        private decimal _indexVolume;
        private bool _aboveVWAP = true;
        public Dictionary<uint, Instrument> AllStocks { get; set; }
        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        decimal _downtrendlowswingindexvalue = 0;
        decimal _uptrendhighswingindexvalue = 0;
        decimal _downtrendlowswingindexvalueWIP = 0;
        decimal _uptrendhighswingindexvalueWIP = 0;
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
        private decimal _previousStrikePEProfit;
        private decimal _previousStrikeCEProfit;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private bool _ibLoaded = false;
        public decimal _minDistanceFromBInstrument;
        public decimal _maxDistanceFromBInstrument;
        public Dictionary<uint, uint> MappedTokens { get; set; }
        private OrderTrio _orderTrio;
        private bool _peSLHit = false;
        private bool _ceSLHit = false;
        private decimal _previousUpswingValue = 0;
        private decimal _previousDownswingValue = 0;
        public SuperTrend _bSuperTrendSlow { get; set; }
        public SuperTrend _bSuperTrendFast { get; set; }
        private int _stMultiplier = 3;
        private int _stLength = 10;
        private decimal _stSlowFastMultiplier = 5;
        private decimal _stCounter = 0;
        private const int BASE_ST_LENGTH = 32;
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

        private readonly TimeSpan TRADING_WINDOW_START = new TimeSpan(09, 45, 0);
        //11 AM to 1Pm suspension
        private readonly TimeSpan SUSPENSION_WINDOW_START = new TimeSpan(17, 30, 0);
        private readonly TimeSpan SUSPENSION_WINDOW_END = new TimeSpan(19, 30, 0);
        private readonly int STOP_LOSS_POINTS = 1130;//50;//30; //40 points max loss on an average 
        private readonly int STOP_LOSS_POINTS_GAMMA = 1130;//50;//30;
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
        private decimal FIRST_TARGET = 600;
        bool _firstPETargetActive = true;
        bool _firstCETargetActive = true;
        decimal _firstTargetQty = 0.25m;

        private decimal SECOND_TARGET = 500;
        bool _secondPETargetActive = true;
        bool _secondCETargetActive = true;
        decimal _secondTargetQty = 0.5m;

        private decimal THIRD_TARGET = 700;
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
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private SortedList<decimal, int> _rcriticalLevels;
        private SortedList<decimal, int> _rcriticalLevelsWeekly;
        private bool _putTriggered = false;
        private bool _callTrigerred = false;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.TJ2;
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


        private bool _bSTLoaded = false, _bSTLoadedFromDB = false;
        private bool _bSTLoading = false;

        ExponentialMovingAverage _indexEMA;
        ExponentialMovingAverage _rindexEMA;
        bool _indexEMALoaded = false;
        bool _rindexEMALoaded = false;
        bool _indexEMALoading = false;
        bool _indexEMALoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;
        private IIndicatorValue _rindexEMAValue;
        private bool _intraday;
        private decimal nextTriggerLevel;
        IEnumerable<Candle> _historicalcandles = null;
        public TJ2(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            bool intraday, decimal pnl,
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;

            //ZConnect.Login();
            //_user = KoConnect.GetUser(userId: uid);

            _candleTimeSpan = candleTimeSpan;
            _candleTimeSpanLong = new TimeSpan(0, 15, 0);

            //if (_candleTimeSpan.TotalMinutes == 5)
            //{
            //    STOP_LOSS_POINTS = 50;
            //    STOP_LOSS_POINTS_GAMMA = 50;

            //    FIRST_TARGET = 30;
            //    SECOND_TARGET = 50;
            //    THIRD_TARGET = 65;
            //}


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
            _stSlowFastMultiplier = _candleTimeSpanLong.Minutes / _candleTimeSpan.Minutes;
            _bSuperTrendSlow = new SuperTrend(_stMultiplier, _stLength);
            _bSuperTrendFast = new SuperTrend(_stMultiplier, _stLength);

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

            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();
            _rcriticalLevels = new SortedList<decimal, int>();
            _rcriticalLevelsWeekly = new SortedList<decimal, int>();

            _indexEMA = new ExponentialMovingAverage(length: 20);

            _maxDistanceFromBInstrument = 1000;
            _minDistanceFromBInstrument = 500;

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

        //public void LoadActiveOrders(List<OrderTrio> activeOrderTrios)
        //{
        //    if (activeOrderTrios != null)
        //    {
        //        DataLogic dl = new DataLogic();
        //        foreach (OrderTrio orderTrio in activeOrderTrios)
        //        {
        //            Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
        //            orderTrio.Option = option;
        //            if (option.InstrumentType.ToLower() == "ce")
        //            {
        //                _callorderTrios.Add(orderTrio);
        //            }
        //            else if (option.InstrumentType.ToLower() == "pe")
        //            {
        //                _putorderTrios.Add(orderTrio);
        //            }
        //        }
        //    }
        //}


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

                        LoadFutureToTrade(currentTime);
                        if (_activeFuture != null)
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
                            if (token == _baseInstrumentToken && !_bSTLoaded)
                            {
                                LoadBInstrumentST(token, BASE_ST_LENGTH, currentTime);
                            }

                            if (_activeFuture != null || !_stockTokenHalfTrend.ContainsKey(_baseInstrumentToken))
                            {
                                UpdateFuturePrice(tick.LastPrice, tick.InstrumentToken);
                                decimal htrend = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend();
                                if (tick.InstrumentToken == _baseInstrumentToken || tick.InstrumentToken == _activeFuture.InstrumentToken)
                                {
                                    MonitorCandles(tick, currentTime);
                                    ExecuteTrade(tick, currentTime);
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
                if (e.InstrumentToken == _activeFuture.InstrumentToken)
                {
                    if (e.TotalVolume != 0)
                    {
                        decimal volPrice = (_indexVWAP * _indexVolume + (e.ClosePrice * e.TotalVolume.Value));
                        _indexVolume += e.TotalVolume.Value;
                        _indexVWAP = volPrice / _indexVolume;
                        _aboveVWAP = e.ClosePrice > _indexVWAP;
                    }
                }
                if (e.InstrumentToken == _baseInstrumentToken)
                {
                    _baseCandleLoaded = true;
                    _bCandle = e;

                    if (e.TotalVolume != 0)
                    {
                        _indexVolume += e.TotalVolume.Value;
                        _indexVWAP = (_indexVWAP + (e.ClosePrice * e.TotalVolume.Value)) / _indexVolume;
                    }

                    _bSuperTrendFast.Process(e);
                    _stCounter++;
                    if (_stCounter == _stSlowFastMultiplier)
                    {
                        Candle c5 = GetLastCandle(Convert.ToInt32(_stSlowFastMultiplier), e.CloseTime);
                        _bSuperTrendSlow.Process(c5);
                        _stCounter = 0;
                    }

                    if (_stockTokenHalfTrend.ContainsKey(_bCandle.InstrumentToken))
                    {
                        decimal previoushtred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend

                        _stockTokenHalfTrend[_bCandle.InstrumentToken].Process(_bCandle);

                        decimal htred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend
                        decimal htvalue = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                        bool suspensionWindow = _bCandle.CloseTime.TimeOfDay > SUSPENSION_WINDOW_START && _bCandle.CloseTime.TimeOfDay < SUSPENSION_WINDOW_END;
                        suspensionWindow = true;


                        if (previoushtred != 1 && htred == 0 && _bCandle.HighPrice > _uptrendhighswingindexvalue)
                        {
                            _uptrendhighswingindexvalue = _bCandle.HighPrice;
                        }
                        //if (htred == 1 && _bCandle.LowPrice < _downtrendlowswingindexvalue)
                        //{
                        //    _downtrendlowswingindexvalue = _bCandle.LowPrice;
                        //}
                        if (previoushtred != 0 && htred == 1 && (_bCandle.LowPrice < _downtrendlowswingindexvalue || _downtrendlowswingindexvalue == 0))
                        {
                            _downtrendlowswingindexvalue = _bCandle.LowPrice;
                        }

                        if (!(_bSuperTrendSlow.isFormed && _bSuperTrendFast.isFormed))
                        {
                            return;
                        }

                        //if (_bCandle.CloseTime.TimeOfDay > TRADING_WINDOW_START)
                        //{
                        //Uptrend
                        //Sell Put if not sold. do not close call unless SL hit
                        if (suspensionWindow && previoushtred == 1 && htred == 0 && e.OpenTime.TimeOfDay > MARKET_START_TIME)
                        {

                            _previousUpswingValue = _uptrendhighswingindexvalue;
                            _uptrendhighswingindexvalue = _bCandle.HighPrice;

                            //_firstPETargetActive = true;
                            //_secondPETargetActive = true;
                            //_thirdPETargetActive = true;

                            _peSL = _prCandle != null ? _prCandle.LowPrice : _bCandle.LowPrice;


                            if (e.ClosePrice > _bSuperTrendSlow.GetValue() && e.ClosePrice > _bSuperTrendFast.GetValue())// && _aboveVWAP)
                            {
                                _putTriggered = true;
                            }
                        }
                        else if (suspensionWindow && previoushtred == 0 && htred == 1 && e.OpenTime.TimeOfDay > MARKET_START_TIME)
                        {

                            _previousDownswingValue = _downtrendlowswingindexvalue;
                            _downtrendlowswingindexvalue = _bCandle.LowPrice;
                            //_downtrendlowswingindexvalueWIP = _bCandle.LowPrice;
                            //_uptrendhighswingindexvalue = _uptrendhighswingindexvalueWIP;

                            _ceSL = _prCandle != null ? Math.Max(_prCandle.HighPrice, _bCandle.HighPrice) : _bCandle.HighPrice;

                            if (e.ClosePrice < _bSuperTrendSlow.GetValue() && e.ClosePrice < _bSuperTrendFast.GetValue())// && !_aboveVWAP)
                            {
                                _callTrigerred = true;
                            }
                        }

                        if(e.ClosePrice > _bSuperTrendSlow.GetValue() || e.ClosePrice > _bSuperTrendFast.GetValue())
                        {
                            _callTrigerred = false;
                        }
                        else if (e.ClosePrice < _bSuperTrendSlow.GetValue() || e.ClosePrice < _bSuperTrendFast.GetValue())
                        {
                            _putTriggered = false;
                        }

                        if (_orderTrio != null)
                        {
                            if (_orderTrio.Order.TransactionType.ToLower().Trim() == "sell" && _bCandle.ClosePrice > _bSuperTrendFast.GetValue() && htred == 0)
                            {
                                Instrument option = _orderTrio.Option;

                                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "fut", option.LastPrice,
                                   option.KToken, true, _orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
                                   Tag: "",
                                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                   broker: Constants.KOTAK,
                                   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                OnTradeExit(order);

                                _pnl += order.Quantity * order.AveragePrice * -1;

                                _orderTrio.isActive = false;
                                _orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                                DataLogic dl = new DataLogic();
                                _orderTrio.Id = dl.UpdateOrderTrio(_orderTrio, _algoInstance);
                                dl.UpdateAlgoPnl(_algoInstance, _pnl);

                                _ceCurrentProfit = 0;
                                //Since all ordertrios have same stoploss, all will get closed simultaneously
                                _callProfitBookedQty = 0;
                                _uptrendhighswingindexvalue = 0;
                                _ceSLHit = true;
                                _callTrigerred = false;
                                _orderTrio = null;
                            }
                            else if (_orderTrio.Order.TransactionType.ToLower().Trim() == "buy" && _bCandle.ClosePrice < _bSuperTrendFast.GetValue() && htred == 0)
                            {
                                Instrument option = _orderTrio.Option;

                                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "fut", option.LastPrice,
                                   option.KToken, false, _orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
                                   Tag: "",
                                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                                   broker: Constants.KOTAK,
                                   httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                OnTradeExit(order);

                                _pnl += order.Quantity * order.AveragePrice * 1;

                                _orderTrio.isActive = false;
                                _orderTrio.EntryTradeTime = order.OrderTimestamp.Value;
                                DataLogic dl = new DataLogic();
                                _orderTrio.Id = dl.UpdateOrderTrio(_orderTrio, _algoInstance);
                                dl.UpdateAlgoPnl(_algoInstance, _pnl);

                                _peCurrentProfit = 0;
                                //Since all ordertrios have same stoploss, all will get closed simultaneously
                                _putProfitBookedQty = 0;
                                _peSLHit = true;
                                _putTriggered = false;
                                _orderTrio = null;
                            }
                            //}
                        }
                    }

                    if (_intraday || _expiryDate.Value.Date == _bCandle.CloseTime.Date)
                    {
                        TriggerEODPositionClose(_bCandle.CloseTime, false);
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
                 && (currentTime.TimeOfDay >= TRADING_WINDOW_START && currentTime.TimeOfDay <= new TimeSpan(15, 12, 00))
                )
            {
                decimal htred = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend(); // 0 is up trend
                decimal htvalue = _stockTokenHalfTrend[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                int tradeQty = 0;
                if (htred == 0 && (!_peSLHit || (htvalue > _previousUpswingValue && _peSLHit)))//&& _putTriggered)
                {
                    decimal stopLoss = 0;
                    if (_putTriggered && _aboveVWAP && _orderTrio == null && (htvalue >= _baseInstrumentPrice - (_candleTimeSpan.Minutes == 5 ? 31 : 11)))
                    {
                        _firstPETargetActive = true;
                        _secondPETargetActive = true;
                        _thirdPETargetActive = true;
                        stopLoss = htred == 0 ? _downtrendlowswingindexvalue : _previousDownswingValue;
                        stopLoss -= SL_BUFFER;

                        tradeQty = _tradeQty;// GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdPETargetActive);
                    }
                    //No ITM selling also
                    if (tradeQty > 0)// && _baseInstrumentPrice > stopLoss + 100)
                    {
                        //this line can be updated for postion sizing, based on the SL.
                        int qty = tradeQty;// _tradeQty;
                                           //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);
                        OrderTrio orderTrio = new OrderTrio();


                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                           _activeFuture.KToken, true, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime, Tag: "",
                           product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "pe", _atmOption.LastPrice,
                        //   _atmOption.KToken, false, qty, algoIndex, currentTime,
                        //   Tag: "",
                        //   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                        //   broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        OnTradeEntry(orderTrio.Order);
                        orderTrio.Option = _activeFuture;

                        //SL SHIFTED TO LOW OF PREVIOUS CANDLE
                        //orderTrio.BaseInstrumentStopLoss = stopLoss;
                        orderTrio.BaseInstrumentStopLoss = _peSL != 0 ? _peSL : stopLoss;


                        _initialStopLoss = stopLoss;
                        orderTrio.EntryTradeTime = currentTime;
                        //first target is 1:1 R&R
                        orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - stopLoss;
                        _lastOrderTransactionType = "buy";
                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

                        DataLogic dl = new DataLogic();
                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                        dl.UpdateAlgoPnl(_algoInstance, _pnl);

                        _orderTrio = orderTrio;
                        _peSLHit = false;
                    }
                }
                if (htred == 1 && (!_ceSLHit || (_ceSLHit && htvalue < _previousDownswingValue)))// && _callTrigerred)
                {
                    decimal stopLoss = 0;
                    if (_orderTrio == null && !_aboveVWAP && _callTrigerred && (htvalue <= _baseInstrumentPrice + (_candleTimeSpan.Minutes == 5 ? 31 : 11)))
                    {
                        //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);
                        _firstCETargetActive = true;
                        _secondCETargetActive = true;
                        _thirdCETargetActive = true;

                        //stopLoss = _uptrendhighswingindexvalue + SL_BUFFER;

                        stopLoss = htred == 1 ? _uptrendhighswingindexvalue : _previousUpswingValue;
                        stopLoss += SL_BUFFER;


                        tradeQty = _tradeQty;// GetTradedQuantity(_baseInstrumentPrice, stopLoss, _tradeQty * Convert.ToInt32(_lotSize), 0, _thirdCETargetActive);

                    }
                    if (tradeQty > 0)// && _baseInstrumentPrice < stopLoss - 100)
                    {
                        //this line can be updated for postion sizing, based on the SL.
                        int qty = tradeQty;
                        //LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                        OrderTrio orderTrio = new OrderTrio();


                        //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _atmOption.TradingSymbol, "ce", _atmOption.LastPrice,
                        //   _atmOption.KToken, false, qty, algoIndex, currentTime,
                        //   Tag: "",
                        //   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                        //   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                          _activeFuture.KToken, false, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime, Tag: "",
                          product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId, 
                          httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                        OnTradeEntry(orderTrio.Order);
                        orderTrio.Option = _activeFuture;

                        //SL SHIFTED TO HIGH OF PREVIOUS CANDLE
                        //orderTrio.BaseInstrumentStopLoss = stopLoss;
                        orderTrio.BaseInstrumentStopLoss = _ceSL != 0 ? _ceSL : stopLoss;


                        _initialStopLoss = stopLoss;
                        orderTrio.EntryTradeTime = currentTime;
                        //first target is 1:1 R&R
                        orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - stopLoss;
                        _lastOrderTransactionType = "sell";
                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;
                        DataLogic dl = new DataLogic();
                        orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                        dl.UpdateAlgoPnl(_algoInstance, _pnl);

                        _orderTrio = orderTrio;
                        _ceSLHit = false;
                    }
                }
            }
        }


        private void LoadFutureToTrade(DateTime currentTime)
        {
            try
            {
                if (_activeFuture == null)
                {

#if BACKTEST
                    DataLogic dl = new DataLogic();
                    //_activeFuture = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");

                    _activeFuture.LotSize = (uint)(_activeFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50 : 15);
                    _lotSize = 15;
                    //_referenceFuture = dl.GetInstrument(null, _referenceBToken, 0, "EQ");
                    //_referenceFuture.LotSize = (uint)(_referenceFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50 : 25);

#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
                    //_referenceFuture = dl.GetInstrument(_expiryDate.Value, _referenceBToken, 0, "Fut");
                    _lotSize = 15;
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadFutureToTrade");
                    OnCriticalEvents(currentTime.ToShortTimeString() , "Trade Started. Future Loaded.");
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

        private void LoadBInstrumentST(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_firstCandleOpenPriceNeeded)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_bSTLoading)
                    {
                        _bSTLoading = true;
                        //Task task = Task.Run(() => LoadBaseInstrumentST(bToken, candleCount, lastCandleEndTime, _candleTimeSpan));
                        //LoadBaseInstrumentST(bToken, candleCount, lastCandleEndTime, _candleTimeSpan);
                        LoadBaseInstrumentST(bToken, lastCandleEndTime);
                    }

                    if (TimeCandles.ContainsKey(bToken) && _bSTLoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.

                            _bSuperTrendFast ??= new SuperTrend(_stMultiplier, _stLength);
                            _bSuperTrendSlow ??= new SuperTrend(_stMultiplier, _stLength);

                            _bSuperTrendFast.Process(TimeCandles[bToken].First());
                            _bSuperTrendSlow.Process(TimeCandles[bToken].First());

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            int stCounter = 0;
                            foreach (var candle in TimeCandles[bToken].Skip(1))
                            {
                                stCounter++;
                                _bSuperTrendFast.Process(candle);

                                if (stCounter == _stSlowFastMultiplier)
                                {
                                    _bSuperTrendSlow.Process(candle);
                                    stCounter = 0;
                                }

                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _bSTLoadedFromDB)
                    {
                        _bSTLoaded = true;
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} ST loaded from DB for Base Instrument", BASE_ST_LENGTH), "LoadBInstrumentST");
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
        private void LoadBaseInstrumentST(uint bToken, DateTime lastCandleEndTime)
        {
            LoadHT(bToken, lastCandleEndTime, _candleTimeSpan, _bSuperTrendFast);
            LoadHT(bToken, lastCandleEndTime, _candleTimeSpanLong, _bSuperTrendSlow);

            _bSTLoadedFromDB = true;
        }
        private void LoadHT(uint bToken, DateTime lastCandleEndTime, TimeSpan timeSpan, SuperTrend superTrend)
        {
            try
            {
                DataLogic dl = new DataLogic();
                DateTime fromDate = dl.GetPreviousTradingDate(lastCandleEndTime, timeSpan.Minutes <= 3 ? 2 : 15);

                CandleSeries cs = new CandleSeries();
                List<Historical> historicals;

                if (timeSpan.Minutes == 1)
                {
                    historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), fromDate, lastCandleEndTime.AddSeconds(-10), "minute");
                }
                else
                {
                    historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), fromDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", timeSpan.Minutes));
                }

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
                    tc.InstrumentToken = bToken;
                    tc.Final = true;
                    tc.State = CandleStates.Finished;

                    superTrend.Process(tc);
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
        void UpdateFuturePrice(decimal lastPrice, uint token)
        {
            if (_activeFuture != null && _activeFuture.InstrumentToken == token)
            {
                _activeFuture.LastPrice = lastPrice;
            }
        }
        private int GetTradedQuantity(decimal triggerLevel, decimal SL, int totalQuantity,
            int bookedQty, bool thirdTargetActive)
        {
            int tradedQuantity = 0;

            //if (Math.Abs(triggerLevel - SL) > 100)
            //{
            //if (Math.Abs(triggerLevel - SL) < 90)//50) //wednesday 20
            //{
            //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    tradedQuantity = totalQuantity;// * 2 / 4;
            //    tradedQuantity -= tradedQuantity % Convert.ToInt32(_lotSize);
            //}
            //else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
            //{
            //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    tradedQuantity = totalQuantity * 2 / 4;
            //    tradedQuantity -= tradedQuantity % Convert.ToInt32(_lotSize);
            //}
            tradedQuantity = totalQuantity;// * 2 / 4;
            tradedQuantity -= tradedQuantity % Convert.ToInt32(_lotSize);
            //if (Math.Abs(triggerLevel - SL) < 130)
            //{
            //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //    tradedQuantity = totalQuantity;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 170)
            //{
            //    //tradedQuantity = bookedQty != 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    tradedQuantity = totalQuantity * 2 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 230)
            //{
            //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
            //    tradedQuantity = totalQuantity * 1 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            // }
            //else if (Math.Abs(triggerLevel - SL) < 400 && thirdTargetActive)
            //{
            //    tradedQuantity = totalQuantity * 1 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}

            //if (Math.Abs(triggerLevel - SL) < 50)
            //{
            //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //    tradedQuantity = totalQuantity;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 150 && thirdTargetActive)
            //{
            //    tradedQuantity = totalQuantity * 2 / 4;
            //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}

            //if (Math.Abs(triggerLevel - SL) < 90)
            //{
            //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //    tradedQuantity = totalQuantity;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 150)
            //{
            //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
            //    tradedQuantity = totalQuantity;
            //    tradedQuantity -= tradedQuantity % 25;
            //}
            //else if (Math.Abs(triggerLevel - SL) < 90 && thirdTargetActive)
            //{
            //    tradedQuantity = totalQuantity * 2 / 4;
            //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    tradedQuantity -= tradedQuantity % 25;
            //}

            //if (_candleTimeSpan.Minutes == 1)
            //{
            //    if (Math.Abs(triggerLevel - SL) < 150)
            //    {
            //        //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //        tradedQuantity = totalQuantity;
            //        tradedQuantity -= tradedQuantity % 25;
            //    }
            //    //else if (Math.Abs(triggerLevel - SL) < 150)
            //    //{
            //    //    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
            //    //    tradedQuantity = totalQuantity * 3 / 4;
            //    //    tradedQuantity -= tradedQuantity % 25;
            //    //}
            //    else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
            //    {
            //        tradedQuantity = totalQuantity * 2 / 4;
            //        //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //        tradedQuantity -= tradedQuantity % 25;
            //    }
            //}
            //else
            //{
            //    //if (Math.Abs(triggerLevel - SL) < 150)
            //    //{
            //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 3 / 4 : totalQuantity * 4 / 4;
            //    //    tradedQuantity = totalQuantity;
            //    //    tradedQuantity -= tradedQuantity % 25;
            //    //}
            //    ////else if (Math.Abs(triggerLevel - SL) < 150)
            //    ////{
            //    ////    // tradedQuantity = bookedQty != 0 ? totalQuantity * 1 / 4 : totalQuantity * 2 / 4;
            //    ////    tradedQuantity = totalQuantity * 3 / 4;
            //    ////    tradedQuantity -= tradedQuantity % 25;
            //    ////}
            //    //else if (Math.Abs(triggerLevel - SL) < 270 && thirdTargetActive)
            //    //{
            //    //    tradedQuantity = totalQuantity * 2 / 4;
            //    //    //tradedQuantity = bookedQty == 0 ? totalQuantity * 2 / 4 : totalQuantity * 3 / 4;
            //    //    tradedQuantity -= tradedQuantity % 25;
            //    //}
            //}

            //tradedQuantity = totalQuantity;

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
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);// timeSpan.Minutes == 5 ? 1 : timeSpan.Minutes);
                List<Historical> historicals;
                foreach (uint token in tokenList)
                {
                    if (timeSpan.Minutes == 1)
                    {
                        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
                    }
                    else
                    {
                        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", timeSpan.Minutes));
                    }
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    decimal lastprice = 0;
                    decimal previoustrend, currenttrend;

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
                            previoustrend = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend();
                            _stockTokenHalfTrend[tc.InstrumentToken].Process(tc);
                            currenttrend = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend();

                            if (previoustrend == 0 && currenttrend == 1)
                            {
                                _downtrendlowswingindexvalue = 0;
                            }
                            else if (previoustrend == 1 && currenttrend == 0)
                            {
                                _uptrendhighswingindexvalue = 0;
                            }
                        }
                        else
                        {
                            ht = new HalfTrend(2, 2);
                            ht.Process(tc);
                            _stockTokenHalfTrend.TryAdd(tc.InstrumentToken, ht);
                        }

                        _downtrendlowswingindexvalue = _downtrendlowswingindexvalue == 0 ? price.Low : Math.Min(_downtrendlowswingindexvalue, price.Low);
                        _uptrendhighswingindexvalue = Math.Max(_uptrendhighswingindexvalue, price.High);

                    }
                }

                _previousDownswingValue = _downtrendlowswingindexvalue;
                _previousUpswingValue = _uptrendhighswingindexvalue;
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
            IEnumerable<Candle> candles = null;
            //max and min for last 1 hour
            if (TimeCandles[token].Count >= 13 || _candleTimeSpan.Minutes == 5)
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

                if (swingHighFound && swingLowFound)
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


        private void TriggerEODPositionClose(DateTime currentTime, bool closeAll = false)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 15, 00) || closeAll)
            {
                DataLogic dl = new DataLogic();

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

                if (_orderTrio != null)
                {

                    OrderTrio orderTrio = _orderTrio;
                    Instrument option = orderTrio.Option;

                    bool buy = orderTrio.Order.TransactionType.ToLower() == "sell";
                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, "ce", option.LastPrice,
                       option.KToken, buy, orderTrio.Order.Quantity, algoIndex, _bCandle.CloseTime,
                       Tag: "closed",
                       product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);

                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (buy ? -1 : 1);
                    orderTrio.EntryTradeTime = currentTime;
                    orderTrio.isActive = false;
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                    _orderTrio = null;
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

                if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                {
                    SubscriptionTokens.Add(_baseInstrumentToken);
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(_activeFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeFuture.InstrumentToken);
                    dataUpdated = true;
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
                //if (tick.Timestamp.HasValue && _currentDate.HasValue && tick.Timestamp.Value.Date != _currentDate)
                //{
                //    ResetAlgo(tick.Timestamp.Value.Date);
                //}
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
