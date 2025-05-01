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
using System.Security.Cryptography;

namespace Algorithms.Algorithms
{
    public class TJ4 : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(TJ4 source);
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

        List<uint> _RSILoaded, _ADXLoaded, _emaLoaded, _cprLoaded;
        //List<uint> _SQLLoadingRSI, _SQLLoadingAdx, _SQLLoadingSch;
        //Dictionary<uint, StochasticOscillator> _stockTokenStochastic;

        List<uint> _SQLLoadingRSI, _SQLLoadingAdx, _SQLLoadingEMA;
        Dictionary<uint, ExponentialMovingAverage> _stockTokenEMA;

        Dictionary<uint, decimal> _stockTokenVWAP;
        Dictionary<uint, decimal> _stockTokenVolume;
        Dictionary<uint, CentralPivotRange> _stockTokenCPR;

        public Dictionary<uint, Instrument> AllStocks { get; set; }
        TimeSpan _candleTimeSpanShort, _candleTimeSpanLong;
        private User _user;
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

        private bool _referenceCandleLoaded = false;
        private bool _baseCandleLoaded = false;
        private Candle _bCandle;
        public Candle _rCandle;
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

        private const decimal CANDLE_WICK_SIZE = 25;//45;//10000;//45;//35
        private const decimal CANDLE_BODY_MIN = 5;
        private const decimal CANDLE_BODY = 20;//40; //25
        private const decimal CANDLE_BODY_BIG = 25;//35;
        private const decimal CANDLE_BODY_EXTREME = 45;//95;
        private const decimal EMA_ENTRY_RANGE = 35;
        private const decimal RISK_REWARD = 2.3M;
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

        Dictionary<int, ExponentialMovingAverage> _indexEMAs;
        Dictionary<int, decimal> _indexEMAsPrevCandle;

        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.TJ4;
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

        public TJ4(TimeSpan candleTimeSpan, uint baseInstrumentToken, uint referenceBaseToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            //OHLC previousDayOHLC, OHLC previousWeekOHLC, decimal previousDayBodyHigh, 
            //decimal previousDayBodyLow, decimal previousSwingHigh, decimal previousSwingLow,
            //decimal previousWeekLow, decimal previousWeekHigh, 
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            //ZConnect.Login();
            //_user = KoConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;

            _candleTimeSpanLong = new TimeSpan(0, 30, 0);
            _candleTimeSpanShort = new TimeSpan(0, 5, 0);

            _baseInstrumentToken = baseInstrumentToken;
            _referenceBToken = referenceBaseToken;
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

            _stockTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            _stockTokenCPR = new Dictionary<uint, CentralPivotRange>();

            _emaLoaded = new List<uint>();
            _cprLoaded = new List<uint>();

            _SQLLoadingEMA = new List<uint>();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _indexEMAs = new Dictionary<int, ExponentialMovingAverage>()
            {
                { 20, new ExponentialMovingAverage(length:20) },
                { 50, new ExponentialMovingAverage(length:50) },
                { 100, new ExponentialMovingAverage(length:100) },
                { 200, new ExponentialMovingAverage(length:200) },
                { 400, new ExponentialMovingAverage(length:400) }
            };

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
                    

                    //if (!_schLoaded.Contains(token))// && token != _baseInstrumentToken)
                    //{
                    //    LoadHistoricalStochastics(currentTime);
                    //}

                    if (!_emaLoaded.Contains(token) && token != _baseInstrumentToken)
                    {
                        //LoadFutureInstrumentEMA(_baseInstrumentToken, currentTime);
                        LoadHistoricalEMAs(currentTime);
                    }

                    if (!_cprLoaded.Contains(token))// && token != _baseInstrumentToken)
                    {
                        LoadStockCPRs(currentTime);
                    }
#if local

                    // SetParameters(currentTime);
#endif
                    UpdateFuturePrice(tick.LastPrice, token);
                    
                    MonitorCandles(tick, currentTime);

                    if (tick.InstrumentToken == _baseInstrumentToken)
                    {
                        CheckTPSL(currentTime, tick.LastPrice, closeAll: false);
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
            if (e.InstrumentToken == _baseInstrumentToken)
            {
                _baseCandleLoaded = true;
                _bCandle = e;
                if (e.TotalVolume != 0)
                {
                    _stockTokenVolume[_bCandle.InstrumentToken] += e.TotalVolume.Value;
                    _stockTokenVWAP[_bCandle.InstrumentToken] = (_stockTokenVWAP[_bCandle.InstrumentToken] + (e.ClosePrice * e.TotalVolume.Value)) / _stockTokenVolume[_bCandle.InstrumentToken];
                }
            }
            else if (e.InstrumentToken == _referenceBToken)
            {
                _referenceCandleLoaded = true;
                _rCandle = e;

                if (e.TotalVolume != 0)
                {
                    _stockTokenVolume[_rCandle.InstrumentToken] += e.TotalVolume.Value;
                    _stockTokenVWAP[_rCandle.InstrumentToken] = (_stockTokenVWAP[_rCandle.InstrumentToken] + (e.ClosePrice * e.TotalVolume.Value)) / _stockTokenVolume[_rCandle.InstrumentToken];
                }
            }

            if (_baseCandleLoaded)// && _referenceCandleLoaded)
            {
                #region Alert 4: //Check for a candle that breaks both 20 EMA and VWAP, and then take trade. 1:1 with last swing point, 50% at the candle itselfand 50% at 1:1

                if (_stockTokenEMA.ContainsKey(_bCandle.InstrumentToken))
                {
                    _stockTokenEMA[_bCandle.InstrumentToken].Process(_bCandle);

                    //decimal k = _stockTokenStochastic[e.InstrumentToken].K.GetCurrentValue<decimal>();
                    //decimal d = _stockTokenStochastic[e.InstrumentToken].D.GetCurrentValue<decimal>();

                    if (e.OpenTime.TimeOfDay >= new TimeSpan(9, 30, 0) && _stockTokenCPR.ContainsKey(_bCandle.InstrumentToken))
                    {

                        //Upper breakout in a single candle. need to check with 2 candles as well.
                        if (_bCandle.ClosePrice > _stockTokenEMA[_bCandle.InstrumentToken].GetCurrentValue<decimal>()
                            && _bCandle.OpenPrice < _stockTokenEMA[_bCandle.InstrumentToken].GetCurrentValue<decimal>()
                            && _bCandle.ClosePrice > _stockTokenVWAP[_bCandle.InstrumentToken])
                        {

                            int qty = _orderTrios.Count == 0 ? _tradeQty : _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? 2 * _tradeQty : 0;

                            if (qty > 0)
                            {
                                OrderTrio orderTrio = new OrderTrio();
                                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                                   _activeFuture.KToken, true, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                                   Tag: "",// String.Format("K {0}, D {1}, Pivot Price {2}", k, d, price),
                                   product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                                OnTradeEntry(orderTrio.Order);
                                orderTrio.Option = _activeFuture;
                                orderTrio.StopLoss = _bCandle.LowPrice;
                                orderTrio.TargetProfit = orderTrio.Order.AveragePrice * 1.009m;
                                orderTrio.Order.Quantity = _tradeQty;
                                _orderTrios.Clear();
                                _orderTrios.Add(orderTrio);

                                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
                            }
                        }
                        else if (_bCandle.ClosePrice < _stockTokenEMA[_bCandle.InstrumentToken].GetCurrentValue<decimal>()
                            && _bCandle.OpenPrice > _stockTokenEMA[_bCandle.InstrumentToken].GetCurrentValue<decimal>()
                            && _bCandle.ClosePrice < _stockTokenVWAP[_bCandle.InstrumentToken])
                        {
                            int qty = _orderTrios.Count == 0 ? _tradeQty : _orderTrios[0].Order.TransactionType.ToLower().Trim() == "buy" ? 2 * _tradeQty : 0;
                            if (qty > 0)
                            {
                                OrderTrio orderTrio = new OrderTrio();
                                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                                   _activeFuture.KToken, false, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                                   Tag: "",//String.Format("K {0}, D {1}, Pivot Price {2}", k, d, price),
                                   product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                                OnTradeEntry(orderTrio.Order);
                                orderTrio.Option = _activeFuture;
                                orderTrio.StopLoss = _bCandle.HighPrice;
                                orderTrio.TargetProfit = orderTrio.Order.AveragePrice * 0.991m;
                                _orderTrios.Add(orderTrio);

                                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice;
                            }
                        }
                    }
                }
                #endregion

                TriggerEODPositionClose(_bCandle.CloseTime, _bCandle.ClosePrice);

                _baseCandleLoaded = false;
                _referenceCandleLoaded = false;
            }


        }
        private void LoadFutureInstrumentEMA(uint bToken, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_indexEMAs)
                {
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
                    //List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);
                    //LoadHistoricalEMAs

                    foreach (var price in historicals)
                    {
                        foreach (var indexEMA in _indexEMAs)
                        {
                            indexEMA.Value.Process(price.Close, isFinal: true);
                        }
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

        private void LoadHistoricalEMAs(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => /*x != _baseInstrumentToken && */!_emaLoaded.Contains(x));
                List<uint> newTokens = new List<uint>();
                StringBuilder sb = new StringBuilder();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!_stockTokenEMA.ContainsKey(t) && !_SQLLoadingEMA.Contains(t))
                    {
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingEMA.Add(t);
                        newTokens.Add(t);
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
                    //Task task = Task.Run(() => LoadHistoricalCandlesForSch(tokenList, 30, lastCandleEndTime, _candleTimeSpanShort));
                    LoadHistoricalCandlesForEMA(newTokens, 30, lastCandleEndTime, _candleTimeSpanShort);
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _stockTokenEMA.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _stockTokenEMA[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                _stockTokenEMA[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenEMA.ContainsKey(tkn))
                    {
                        _emaLoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("EMA loaded from DB for {0}", tkn), "MonitorCandles");
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

        //private void LoadHistoricalStochastics(DateTime currentTime)
        //{
        //    try
        //    {
        //        DateTime lastCandleEndTime;
        //        DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

        //        var tokens = SubscriptionTokens.Where(x => /*x != _baseInstrumentToken && */!_schLoaded.Contains(x));
        //        List<uint> newTokens = new List<uint>();
        //        StringBuilder sb = new StringBuilder();
        //        foreach (uint t in tokens)
        //        {
        //            if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
        //            {
        //                _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
        //            }
        //            if (!_stockTokenStochastic.ContainsKey(t) && !_SQLLoadingSch.Contains(t))
        //            {
        //                sb.AppendFormat("{0},", t);
        //                _SQLLoadingSch.Add(t);
        //                newTokens.Add(t);
        //            }
        //        }
        //        string tokenList = sb.ToString().TrimEnd(',');

        //        int firstCandleFormed = 0; //historicalPricesLoaded = 0;
        //                                   //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
        //                                   //{
        //                                   //_SQLLoading.Add(token);
        //                                   //if (tokenList != string.Empty)
        //        if (newTokens.Count > 0)
        //        {
        //            //Task task = Task.Run(() => LoadHistoricalCandlesForSch(tokenList, 30, lastCandleEndTime, _candleTimeSpanShort));
        //            LoadHistoricalCandlesForSch(newTokens, 30, lastCandleEndTime, _candleTimeSpanShort);
        //        }

        //        //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
        //        //historicalPricesLoaded = 1;
        //        //}
        //        foreach (uint tkn in tokens)
        //        {
        //            //if (tk != string.Empty)
        //            //{
        //            // uint tkn = Convert.ToUInt32(tk);


        //            if (TimeCandles.ContainsKey(tkn) && _stockTokenStochastic.ContainsKey(tkn))
        //            {
        //                if (_firstCandleOpenPriceNeeded[tkn])
        //                {
        //                    //The below EMA token input is from the candle that just started, All historical prices are already fed in.
        //                    _stockTokenStochastic[tkn].Process(TimeCandles[tkn].First());
        //                    firstCandleFormed = 1;

        //                }
        //                //In case SQL loading took longer then candle time frame, this will be used to catch up
        //                if (TimeCandles[tkn].Count > 1)
        //                {
        //                    foreach (var price in TimeCandles[tkn])
        //                    {
        //                        _stockTokenStochastic[tkn].Process(TimeCandles[tkn].First());
        //                    }
        //                }
        //            }

        //            if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenStochastic.ContainsKey(tkn))
        //            {
        //                _schLoaded.Add(tkn);
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("RSI loaded from DB for {0}", tkn), "MonitorCandles");
        //            }
        //            //}
        //            // }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalRSIs");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }
        //}

        private void LoadStockCPRs(DateTime currentTime)
        {
            try
            {
                lock (_cprLoaded)
                {
                    var tokens = SubscriptionTokens.Where(x => !_cprLoaded.Contains(x));

                    StringBuilder sb = new StringBuilder();
                    foreach (uint token in tokens)
                    {
#if market
                        DataLogic dl = new DataLogic();
                        DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
                        List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
                        List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");
#elif !market
                        DataLogic dl = new DataLogic();
                        DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
                        
                        //List<Historical> pdOHLCList = dl.GetHistoricalClosePricesFromCandles ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
                        
                        List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

#endif
                        //OHLC pdOHLC = new OHLC() { Close = pdOHLCList.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

                        OHLC pdOHLC = new OHLC() { Close = pdOHLCDay.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

                        CentralPivotRange cpr = new CentralPivotRange(pdOHLC);
                        _stockTokenCPR[token] = cpr;
                        _cprLoaded.Add(token);
                    }
                }
            }
            catch (Exception ex)
            {

            }

        }
        //private void LoadActiveData(DateTime startTime,
        //   DateTime endTime, TimeSpan candleTimeSpan, int numberofCandles)
        //{
        //    AlgoIndex algoIndex = AlgoIndex.VolumeThreshold;
        //    DataLogic dl = new DataLogic();
        //    DataSet ds = dl.LoadCandles(algoIndex, startTime,
        //    endTime, candleTimeSpan, numberofCandles);

        //    foreach (DataRow dr in ds.Tables[0].Rows)
        //    {
        //        tokenVolume.Add(Convert.ToUInt32(dr["InstrumentToken"]), Convert.ToUInt32(dr["Volume"]));
        //    }

        //    foreach (DataRow dr in ds.Tables[1].Rows)
        //    {
        //        tokenSymbol.Add(Convert.ToUInt32(dr["InstrumentToken"]), (string)dr["TradingSymbol"]);
        //    }

        //    OHLC ohlc;
        //    CentralPivotRange cpr;
        //    foreach (DataRow dr in ds.Tables[2].Rows)
        //    {
        //        ohlc = new OHLC();
        //        ohlc.Open = (decimal)dr["Open"];
        //        ohlc.High = (decimal)dr["High"];
        //        ohlc.Low = (decimal)dr["Low"];
        //        ohlc.Close = (decimal)dr["Close"];

        //        cpr = new CentralPivotRange(ohlc);
        //        tokenCPR.Add(Convert.ToUInt32(dr["InstrumentToken"]), cpr);
        //    }

        //}
        private void LoadHistoricalCandlesForEMA(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            //CandleSeries cs = new CandleSeries();
            //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);


            ExponentialMovingAverage ema;

            lock (_stockTokenEMA)
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);

                foreach (uint token in tokenList)
                {
#if market
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpanShort.Minutes));
                    foreach (var price in historicals)
                    {
                        if (_stockTokenEMA.ContainsKey(price.InstrumentToken))
                        {
                            _stockTokenEMA[price.InstrumentToken].Process(price.Close);
                        }
                        else
                        {
                            ema = new ExponentialMovingAverage(length: 20);
                            ema.Process(price.Close);
                            _stockTokenEMA.TryAdd(price.InstrumentToken, ema);
                        }
                    }
#elif BACKTEST
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, token.ToString(), _candleTimeSpan, false);
                    foreach (uint t in historicalCandlePrices.Keys)
                    {
                        ema = new ExponentialMovingAverage(length:20);
                        foreach (var price in historicalCandlePrices[t])
                        {
                            ema.Process(price, isFinal: true);
                        }
                        _stockTokenEMA.TryAdd(t, ema);
                    }
#endif


                }
            }
        }
        void SetTargetHitFlag(uint token, decimal lastPrice)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    if (orderTrio.Option.InstrumentToken == token)
                    {
                        if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (_baseInstrumentPrice > orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                        }
                        else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (_baseInstrumentPrice < orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                        }
                    }
                }
            }
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
                        break;
                    }
                    //else if (ohlc.Low > lastPrice)
                    //{
                    //    swingHigh = ohlc.Low;
                    //    continue;
                    //}
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
        private void CheckTPSL(DateTime currentTime, decimal lastPrice, bool closeAll = false)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (orderTrio.StopLoss > lastPrice || orderTrio.TargetProfit < lastPrice))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));
                        _pnl += order.AveragePrice * _tradeQty * _activeFuture.LotSize;

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (orderTrio.StopLoss < lastPrice || orderTrio.TargetProfit > lastPrice))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(order);

                        _pnl += order.AveragePrice * _tradeQty * _activeFuture.LotSize * -1;


#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }

        private void CheckSL(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && ((lastPrice < orderTrio.StopLoss || closeAll) || (pCandle != null && (lastPrice < pCandle.LowPrice) && orderTrio.TPFlag)))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));


                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && ((lastPrice > orderTrio.StopLoss || closeAll) || (pCandle != null && (lastPrice > pCandle.HighPrice) && orderTrio.TPFlag)))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        _pnl += (orderTrio.Order.AveragePrice - order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }
        private void CheckSLForEMABasedOrders(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        {
            if (_orderTriosFromEMATrades.Count > 0)
            {
                for (int i = 0; i < _orderTriosFromEMATrades.Count; i++)
                {
                    var orderTrio = _orderTriosFromEMATrades[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (lastPrice < _indexEMAValue.GetValue<decimal>() || closeAll))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));


                        _orderTriosFromEMATrades.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (lastPrice > _indexEMAValue.GetValue<decimal>() || closeAll))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        _orderTriosFromEMATrades.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }

        private bool ValidateRiskReward(Candle c, out decimal targetLevel, out decimal stoploss, out bool s2r2level)
        {
            targetLevel = 0;
            stoploss = 0;
            s2r2level = false;
            bool _favourableRiskReward = false;
            if (c.ClosePrice > _cpr.Prices[(int)PivotLevel.LCPR] && c.ClosePrice < _cpr.Prices[(int)PivotLevel.UCPR])
            {
                // Do not take trade within CPR
            }
            else //if (ValidateCandleSize(c))
            {
                //if (c.ClosePrice > _criticalLevels.Keys.Max() || c.ClosePrice < _criticalLevels.Keys.Min())
                if (c.ClosePrice > _previousDayOHLC.High || c.ClosePrice < _previousDayOHLC.Low)
                {
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 17);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 18);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 19);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 20);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 21);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 22);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R4], 23);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S4], 24);
                }
                else
                {
                }

                //bullish scenario
                if (c.ClosePrice > c.OpenPrice)
                {
                    decimal plevel = 0;
                    foreach (var level in _criticalLevels)
                    {
                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0 && level.Key - plevel > 10)
                        {
                            if ((level.Key - c.ClosePrice) / (c.ClosePrice - c.LowPrice /*plevel*/) > 2.0M)
                            {
                                targetLevel = level.Key;
                                stoploss = c.LowPrice;
                                _favourableRiskReward = true;
                                if (plevel == _cpr.Prices[(int)PivotLevel.S2]
                                    //|| plevel == _cpr.Prices[(int)PivotLevel.R2]
                                    || plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.R2]
                                    )
                                {
                                    s2r2level = true;
                                }
                            }
                            break;
                        }
                        plevel = level.Key;
                    }
                }
                else
                {
                    decimal plevel = 0;
                    foreach (var level in _criticalLevels)
                    {
                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0 && level.Key - plevel > 10)
                        {
                            if (Math.Abs((c.ClosePrice - c.HighPrice /*plevel*/) / (level.Key - c.ClosePrice)) > 2.0M)
                            {
                                targetLevel = plevel;
                                stoploss = c.HighPrice;
                                _favourableRiskReward = true;

                                if (//plevel == _cpr.Prices[(int)PivotLevel.S2] ||
                                    level.Key == _cpr.Prices[(int)PivotLevel.R2]
                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
                                    || level.Key == _weeklycpr.Prices[(int)PivotLevel.R2])
                                {
                                    s2r2level = true;
                                }
                                break;
                            }
                        }
                        plevel = level.Key;
                    }
                }
            }
            return _favourableRiskReward;
        }


        private Candle GetLastCandle(uint instrumentToken)
        {
            if (TimeCandles[instrumentToken].Count < 3)
            {
                return null;
            }
            var lastCandles = TimeCandles[instrumentToken].TakeLast(3);
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
            tC.ClosePrice = lastCandles.ElementAt(2).ClosePrice;
            tC.CloseTime = lastCandles.ElementAt(2).CloseTime;
            tC.HighPrice = Math.Max(Math.Max(lastCandles.ElementAt(0).HighPrice, lastCandles.ElementAt(1).HighPrice), lastCandles.ElementAt(2).HighPrice);
            tC.LowPrice = Math.Min(Math.Min(lastCandles.ElementAt(0).LowPrice, lastCandles.ElementAt(1).LowPrice), lastCandles.ElementAt(2).LowPrice);
            return tC;
        }

        private void TriggerEODPositionClose(DateTime currentTime, decimal lastPrice)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00))
            {
                OrderTrio orderTrio = new OrderTrio();
                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                   _activeFuture.KToken, _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell", _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                   Tag: String.Format("ADX {0}, DPlux {1}, DMinus {2} , HtUP {3}", 0, 0, 0, 0),
                   product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                OnTradeEntry(orderTrio.Order);
                orderTrio.Option = _activeFuture;
                _orderTrios.Add(orderTrio);

                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? -1 : 1);

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
                    _activeFuture.LotSize = (uint)(_activeFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50 : 25);

                    //_referenceFuture = dl.GetInstrument(null, _referenceBToken, 0, "EQ");
                    //_referenceFuture = dl.GetInstrument(_expiryDate.Value, _referenceBToken, 0, "Fut");
                    //_referenceFuture.LotSize = (uint)(_referenceFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50 : 25);

#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
                    _referenceFuture = dl.GetInstrument(_expiryDate.Value, _referenceBToken, 0, "Fut");

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
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Future", "UpdateInstrumentSubscription");
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
