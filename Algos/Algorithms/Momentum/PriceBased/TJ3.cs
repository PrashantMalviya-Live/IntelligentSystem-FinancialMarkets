using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using BrokerConnectWrapper;
using FirebaseAdmin.Messaging;
using GlobalCore;
using GlobalLayer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ZMQFacade;

namespace Algorithms.Algorithms
{
    public class TJ3 : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(TJ3 source);
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
        private User _user;
        List<uint> _HTLoaded, _SQLLoadingHT;

        public Dictionary<uint, Instrument> AllStocks { get; set; }
        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        Dictionary<uint, HalfTrend> _stockTokenHalfTrendLong;
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
        private Instrument _activeFuture, _referenceFuture;
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
       // decimal _previoushtred;
        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private decimal _ibLowestPrice = 1000000, _ibHighestPrice;
        private decimal _ibrLowestPrice = 1000000, _ibrHighestPrice;
        private decimal _swingLowPrice = 1000000, _swingHighPrice;
        private decimal _rswingLowPrice = 1000000, _rswingHighPrice;
        private bool _putTriggered = false;
        private bool _callTrigerred = false;
        //11 AM to 1Pm suspension
        private readonly TimeSpan SUSPENSION_WINDOW_START = new TimeSpan(17, 00, 0);
        private readonly TimeSpan SUSPENSION_WINDOW_END = new TimeSpan(18, 00, 0);

        private const decimal CANDLE_WICK_SIZE = 25;//45;//10000;//45;//35
        private const decimal CANDLE_BODY_MIN = 5;
        private const decimal CANDLE_BODY = 20;//40; //25
        private const decimal CANDLE_BODY_BIG = 25;//35;
        private const decimal CANDLE_BODY_EXTREME = 45;//95;
        private const decimal EMA_ENTRY_RANGE = 35;
        private const decimal RISK_REWARD = 2.3M;
        private const int SL_THRESHOLD = 2000; //200 for 5 mins; //350 for 15 mins
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        private OHLC _rpreviousDayOHLC;
        private OHLC _rpreviousWeekOHLC;
        private decimal _pnl = 0;
        private Candle _prCandle;
        int _swingCounter = 60;
        int _rswingCounter = 60;
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

        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.TJ3;
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
        public TJ3(TimeSpan candleTimeSpan, uint baseInstrumentToken, uint referenceBaseToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            bool intraday, int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;

            //_candleTimeSpanLong = new TimeSpan(0, 15, 0);
            //_candleTimeSpanShort = new TimeSpan(0, 5, 0);

            _baseInstrumentToken = baseInstrumentToken;
            _referenceBToken = referenceBaseToken;
            //_referenceBToken = Convert.ToUInt32(Constants.BANK_NIFTY_TOKEN);
            _intraday = intraday;
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
            _rcriticalLevels = new SortedList<decimal, int>();
            _rcriticalLevelsWeekly = new SortedList<decimal, int>();

            _indexEMA = new ExponentialMovingAverage(length: 20);

            //_stockTokenADX = new Dictionary<uint, AverageDirectionalIndex>(13);
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
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken || tick.InstrumentToken == _referenceBToken) ?
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
                        UpdateInstrumentSubscription(currentTime);

                        // ADX with 15 min time frame and candle with 5 mins time frame
                        if (!_HTLoaded.Contains(token) && token == _baseInstrumentToken)
                        {
                            LoadHistoricalHT(currentTime);
                        }
                    if (currentTime.TimeOfDay > MARKET_START_TIME)
                    {
#if local

                    // SetParameters(currentTime);
#endif
                        UpdateFuturePrice(tick.LastPrice, token);
                        MonitorCandles(tick, currentTime);
                    
                        TakeTrade(currentTime);
                        //CheckTargetProfit();
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
        private void TakeTrade(DateTime currentTime)
        {
            decimal htred = _stockTokenHalfTrend[_baseInstrumentToken].GetTrend(); // 0 is up trend
            decimal htvalue = _stockTokenHalfTrend[_baseInstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

            if (htred == 0 && _orderTrios.Count == 0
                   && (htvalue >= _baseInstrumentPrice - 11 /*|| (_prCandle != null && _prCandle.LowPrice >= _baseInstrumentPrice - 10)*/)
                   && _putTriggered)
            {
                LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                if (_baseInstrumentPrice - _swingLowPrice > 200)
                {
                    return;
                }

                OrderTrio orderTrio = new OrderTrio();
                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                   _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime,
                   Tag: "",
                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                OnTradeEntry(orderTrio.Order);
                orderTrio.Option = _activeFuture;

                
                orderTrio.StopLoss = _swingLowPrice;
                _initialStopLoss = _swingLowPrice;
                //first target is 1:1 R&R
                orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _swingLowPrice;
                _lastOrderTransactionType = "buy";
                _orderTrios.Clear();
                _orderTrios.Add(orderTrio);
                orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_activeFuture.LotSize);
                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
            }
            else if (htred == 1
                    && _orderTrios.Count == 0
                    && (htvalue <= _baseInstrumentPrice + 11 /*|| (_prCandle != null && _prCandle.HighPrice <= _baseInstrumentPrice + 10)*/)
                    && _callTrigerred)
            {
                //LastSwingHighlow(currentTime, _activeFuture.InstrumentToken, out _swingHighPrice, out _swingLowPrice);
                LastSwingHighlow(currentTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                if (_swingHighPrice - _baseInstrumentPrice > 200)
                {
                    return;
                }

                OrderTrio orderTrio = new OrderTrio();
                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                   _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime,
                   Tag: "",
                   product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                OnTradeEntry(orderTrio.Order);
                orderTrio.Option = _activeFuture;
                //orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_activeFuture.LotSize);
                
                orderTrio.StopLoss = _swingHighPrice;
                _initialStopLoss = _swingHighPrice;
                _lastOrderTransactionType = "sell";
                //first target is 1:1 R&R
                orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - _swingHighPrice;

                _orderTrios.Clear();
                _orderTrios.Add(orderTrio);

                orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_activeFuture.LotSize);
                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice;
            }
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            if (e.InstrumentToken == _baseInstrumentToken)
            {
                _bCandle = e;

                if (_stockTokenHalfTrend.ContainsKey(_bCandle.InstrumentToken))
                {
                    decimal previoushtred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend
                    _stockTokenHalfTrend[_bCandle.InstrumentToken].Process(_bCandle);

                    decimal htred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend
                    decimal htvalue = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend
                    bool suspensionWindow = _bCandle.CloseTime.TimeOfDay > SUSPENSION_WINDOW_START && _bCandle.CloseTime.TimeOfDay < SUSPENSION_WINDOW_END;

                    if (_orderTrios.Count == 0 && !suspensionWindow && _baseInstrumentPrice - _swingLowPrice < SL_THRESHOLD)
                    {
                        switch (htred)
                        {
                            case 0:
                                _putTriggered = true;
                                break;

                            case 1:
                                _callTrigerred = true;
                                break;
                        }
                    }
                    else
                    {
                        if (previoushtred == 1 && htred == 0)
                        {
                            LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                            if (_orderTrios.Count != 0 && _orderTrios[0].Order.TransactionType.ToLower() == "buy")
                            {
                                _orderTrios[0].StopLoss = Math.Max(_orderTrios[0].StopLoss, _swingLowPrice);
                            }
                        }
                        else if (previoushtred == 0 && htred == 1)
                        {
                            LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);

                            if (_orderTrios.Count != 0 && _orderTrios[0].Order.TransactionType.ToLower() == "sell")
                            {
                                _orderTrios[0].StopLoss = Math.Min(_orderTrios[0].StopLoss, _swingHighPrice);
                            }
                        }
                    }
                    //SL check. SL is lowest/highest price of last one hour.
                    if (_orderTrios.Count > 0 && ((_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" && _baseInstrumentPrice > _orderTrios[0].StopLoss && htred == 0)
                        || (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "buy" && _baseInstrumentPrice < _orderTrios[0].StopLoss && htred == 1)
                        ))
                    {
                        bool buyOrder = _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell";
                        int qty = _orderTrios[0].Order.Quantity / Convert.ToInt32(_activeFuture.LotSize) + _tradeQty;
                        LastSwingHighlow(e.CloseTime, _baseInstrumentToken, out _swingHighPrice, out _swingLowPrice);
                        if ((buyOrder && _bCandle.ClosePrice - _swingLowPrice < SL_THRESHOLD) || (!buyOrder && _swingHighPrice - _bCandle.ClosePrice < SL_THRESHOLD))
                        {
                            OrderTrio orderTrio = new OrderTrio();
                            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                               _activeFuture.KToken, buyOrder, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                               Tag: "",
                               product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML,
                               broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                            OnTradeEntry(orderTrio.Order);
                            orderTrio.Option = _activeFuture;
                            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? -1 : 1);

                            int originalQty = _orderTrios[0].Order.Quantity / Convert.ToInt32(_activeFuture.LotSize);
                            _orderTrios.Clear();

                            orderTrio.StopLoss = buyOrder ? _swingLowPrice : _swingHighPrice;

                            _initialStopLoss = orderTrio.StopLoss;
                            _lastOrderTransactionType = buyOrder ? "buy" : "sell";
                            //first target is 1:1 R&R
                            orderTrio.TargetProfit = 2 * orderTrio.Order.AveragePrice - orderTrio.StopLoss;


                            if (qty != originalQty)
                            {
                                orderTrio.Order.Quantity = (qty - originalQty) * Convert.ToInt32(_activeFuture.LotSize);
                                _orderTrios.Add(orderTrio);
                            }
                        }
                    }
                }

                if (_intraday)
                {
                    TriggerEODPositionClose(_bCandle.CloseTime, _bCandle.ClosePrice);
                }
            }

        }
        void UpdateFuturePrice(decimal lastPrice, uint token)
        {
            if (_activeFuture != null && _activeFuture.InstrumentToken == token)
            {
                _activeFuture.LastPrice = lastPrice;
            }
            else if (_referenceFuture != null && _referenceFuture.InstrumentToken == token)
            {
                _referenceFuture.LastPrice = lastPrice;
            }
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
                            //_stockTokenADX[tkn].Process(TimeCandles[tkn].First());
                            _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            //_stockTokenHalfTrendLong[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                //_stockTokenADX[tkn].Process(TimeCandles[tkn].First());
                                _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                                //_stockTokenHalfTrendLong[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenHalfTrend.ContainsKey(tkn))
                    {
                        _HTLoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("HT loaded from DB for {0}", tkn), "MonitorCandles");
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
            TimeSpan timeSpan = _candleTimeSpan;
            HalfTrend ht;
            lock (_stockTokenHalfTrend)
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);

                foreach (uint token in tokenList)
                {
                    string interval = timeSpan.Minutes == 1 ? "minute" : string.Format("{0}minute", timeSpan.Minutes);
                    List <Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), interval);

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


        private void LastSwingHighlow(DateTime currentTime, uint token, out decimal swingHigh, out decimal swingLow, int pullCandles = 0)
        {
            IEnumerable<Candle> candles = null;
            //max and min for last 1 hour
            if ( (TimeCandles[token].Count >= 12 || _candleTimeSpan.Minutes == 5) && pullCandles == 0)
            {
                candles = TimeCandles[token].TakeLast(12);
            }
            else if (pullCandles > 0 && TimeCandles[token].Count >= 12 + pullCandles)
            {
                candles = TimeCandles[token].SkipLast(pullCandles).TakeLast(12);
            }
            else 
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime, 1);
                List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime, string.Format("{0}minute", _candleTimeSpan.Minutes));
                historicals = historicals.OrderByDescending(x => x.TimeStamp).ToList();
                historicals.SkipLast(12 * pullCandles);
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
                Candle e = GetLastCandle(candles.ElementAt(i), i + 1 < candles.Count()? candles.ElementAt(i + 1): candles.ElementAt(i));
                dcandles.Add(e);
            }


            swingHigh = 0;
            swingLow = 1000000;
            decimal minPrice=1000000, maxPrice=0;

            decimal lastminPrice = 1000000, secondlastminprice = 1000000;
            decimal lastmaxPrice=0, secondlastmaxprice = 0;
            int highCounter = 0, lowCounter = 0;
            foreach (var ohlc in dcandles.OrderByDescending(x => x.CloseTime))
            {
                minPrice = minPrice > ohlc.LowPrice ? ohlc.LowPrice : minPrice;
                maxPrice = maxPrice < ohlc.HighPrice ? ohlc.HighPrice : maxPrice;

                if (ohlc.HighPrice > lastmaxPrice && ohlc.HighPrice > secondlastmaxprice && highCounter < 2)
                {
                    swingHigh = ohlc.HighPrice;
                    highCounter = 0;
                }
                else if (ohlc.HighPrice < swingHigh)
                {
                    highCounter++;
                }

                if (ohlc.LowPrice < lastminPrice && ohlc.LowPrice < secondlastminprice && lowCounter < 2)
                {
                    swingLow = ohlc.LowPrice;
                    lowCounter = 0;
                }
                else if (ohlc.LowPrice > swingLow)
                {
                    lowCounter++;
                }
                if(lowCounter > 2 && highCounter > 2)
                {
                    break;
                }

                secondlastmaxprice = lastmaxPrice;
                secondlastminprice = lastminPrice;
                lastminPrice = ohlc.LowPrice;
                lastmaxPrice = ohlc.HighPrice;
            }

            
            if(swingHigh < TimeCandles[token].Last().HighPrice)
            {
                //Pull more candles for high price

                LastSwingHighlow(currentTime, token, out swingHigh, out swingLow, pullCandles + 1  );

            }
            if (swingLow > TimeCandles[token].Last().LowPrice)
            {
                //Pull more candles for low price
                LastSwingHighlow(currentTime, token, out swingHigh, out swingLow, pullCandles + 1);
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

        private void TriggerEODPositionClose(DateTime currentTime, decimal lastPrice)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 15, 00) )
            {
                if (_orderTrios.Count > 0)
                {
                    OrderTrio orderTrio = new OrderTrio();
                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell", _orderTrios[0].Order.Quantity, algoIndex, _bCandle.CloseTime,
                       Tag: "",
                       product: _intraday? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML, 
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                    OnTradeEntry(orderTrio.Order);
                    orderTrio.Option = _activeFuture;
                    _orderTrios.Add(orderTrio);

                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? -1 : 1);
                }

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

        private void LoadFutureToTrade(DateTime currentTime)
        {
            try
            {
                if (_activeFuture == null)
                {
#if BACKTEST
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
                    //_activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");

                    _activeFuture.LotSize = (uint) (_activeFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50: 25);
                    
                    //_referenceFuture = dl.GetInstrument(null, _referenceBToken, 0, "EQ");
                    //_referenceFuture.LotSize = (uint)(_referenceFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50 : 25);

#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
                    //_referenceFuture = dl.GetInstrument(_expiryDate.Value, _referenceBToken, 0, "Fut");

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
                if (_activeFuture != null && !SubscriptionTokens.Contains(_activeFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeFuture.InstrumentToken);
                    dataUpdated = true;
                }
                if (_referenceFuture != null && !SubscriptionTokens.Contains(_referenceFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_referenceFuture.InstrumentToken);
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                {
                    SubscriptionTokens.Add(_baseInstrumentToken);
                    dataUpdated = true;
                }
#if !BACKTEST
                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Future", "UpdateInstrumentSubscription");
#endif
                if (dataUpdated)
                {
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
