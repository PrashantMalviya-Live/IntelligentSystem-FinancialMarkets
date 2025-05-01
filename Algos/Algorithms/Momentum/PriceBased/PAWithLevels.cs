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

namespace Algorithms.Algorithms
{
    public class PAWithLevels : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(PAWithLevels source);
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
        private User _user;
        private List<OrderTrio> _orderTrios;
        private List<OrderTrio> _orderTriosFromEMATrades;
        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private Dictionary<uint, CentralPivotRange> _cpr;
        private Dictionary<uint, CentralPivotRange> _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan, _candleTimeSpanLong;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private Instrument _activeFuture;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);
        public readonly TimeSpan EXTREME_END_TIME = new TimeSpan(12, 00, 0);
        private decimal _pnl = 0;
        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        List<uint> _HTLoaded, _SQLLoadingHT;

        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private const decimal CANDLE_WICK_SIZE = 45;//10000;//45;//35
        private const decimal CANDLE_BODY_MIN = 5;
        private const decimal CANDLE_BODY = 40;//10000;//40; //25
        private const decimal CANDLE_BODY_BIG = 35;
        private const decimal CANDLE_BODY_EXTREME = 95;
        private const decimal EMA_ENTRY_RANGE= 35;
        private const decimal RISK_REWARD = 2.3M;
        private Dictionary<uint, OHLC> _previousDayOHLC;
        private Dictionary<uint, OHLC> _previousWeekOHLC;
        
        private enum MarketScenario
        {
            Bullish = 1,
            Neutral = 0,
            Bearish = -1
        }
        private MarketScenario _extremeScenario=  MarketScenario.Neutral;
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private Dictionary<uint, Candle> _pCandle;
        private Dictionary<uint, SortedList <decimal, int>> _criticalLevels;
        private Dictionary<uint, SortedList<decimal, int>> _criticalLevelsWeekly;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.PAWithLevels;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private Dictionary<uint, bool?> _firstCandleOutsideRange;
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
        private uint _triggerToken;
        private uint _triggerTokenEMA;
        Dictionary<uint, ExponentialMovingAverage> _indexEMA;
        Dictionary<uint, bool> _indexEMALoaded;
        Dictionary<uint, bool> _indexEMALoading;
        Dictionary<uint, bool> _indexEMALoadedFromDB;
        Dictionary<uint, IIndicatorValue> _indexEMAValue;

        public PAWithLevels(TimeSpan candleTimeSpan, uint baseInstrumentToken, 
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss, 
            //OHLC previousDayOHLC, OHLC previousWeekOHLC, decimal previousDayBodyHigh, 
            //decimal previousDayBodyLow, decimal previousSwingHigh, decimal previousSwingLow,
            //decimal previousWeekLow, decimal previousWeekHigh, 
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            //ZConnect.Login();
            //KoConnect.Login(userId:uid);
            //_user = KoConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;
            _candleTimeSpanLong = new TimeSpan(0, 15, 0);
            _cpr = new Dictionary<uint, CentralPivotRange>();
            _weeklycpr = new Dictionary<uint, CentralPivotRange>();

            _targetProfit = targetProfit;
            _stopLoss = stopLoss;
            _firstCandleOutsideRange = new Dictionary<uint, bool?>();
            _baseInstrumentToken = baseInstrumentToken;

            _stopTrade = true;
            //_trailingStopLoss = _stopLoss = stopLoss;
            //_targetProfit = targetProfit;
            _criticalLevels = new Dictionary<uint, SortedList<decimal, int>>();
            _criticalLevelsWeekly = new Dictionary<uint, SortedList<decimal, int>>();
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

            _criticalLevels = new Dictionary<uint, SortedList<decimal, int>>();
            _criticalLevelsWeekly = new Dictionary<uint, SortedList<decimal, int>>();
            _previousDayOHLC = new Dictionary<uint, OHLC>();
            _previousWeekOHLC = new Dictionary<uint, OHLC>();
            _pCandle = new Dictionary<uint, Candle>();
            _stockTokenHalfTrend = new Dictionary<uint, HalfTrend>();
            _indexEMA = new Dictionary<uint, ExponentialMovingAverage>();
            _indexEMAValue = new Dictionary<uint, IIndicatorValue>();
            _indexEMALoaded = new Dictionary<uint, bool>();
            _indexEMALoadedFromDB = new Dictionary<uint, bool>();
            _indexEMALoading = new Dictionary<uint, bool>();
            _HTLoaded = new List<uint>();
            _SQLLoadingHT = new List<uint>();
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

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
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
                    UpdateInstrumentSubscription(currentTime);

                    if (_baseInstrumentToken == tick.InstrumentToken || _activeFuture.InstrumentToken == tick.InstrumentToken)
                    {
                        if (!_cpr.ContainsKey(token) && !_weeklycpr.ContainsKey(token))
                        {
                            LoadCriticalLevels(token, currentTime);
                        }

                        if (!_indexEMALoaded.ContainsKey(token) || !_indexEMALoaded[token])
                        {
                            LoadBInstrumentEMA(token, 20, currentTime);
                        }
                        else
                        {
                            _indexEMAValue[token] = _indexEMA[token].Process(tick.LastPrice, isFinal: false);
                        }
                    }
                    LoadHistoricalHT(currentTime);
#if local

                    // SetParameters(currentTime);
#endif
                    if (_activeFuture != null && _activeFuture.InstrumentToken == token)
                    {
                        UpdateFuturePrice(tick.LastPrice);
                    }
                    
                    //LoadCriticalValues(currentTime);
                    MonitorCandles(tick, currentTime);

                    SetTargetHitFlag(tick.InstrumentToken, tick.LastPrice, currentTime);
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
            
            if (e.InstrumentToken == _baseInstrumentToken || e.InstrumentToken == _activeFuture.InstrumentToken)
            {
                ProcessTrade(e);
            }
        }

        private void ProcessTrade(Candle e)
        {
            decimal candlelowbody, candlehighbody;
            uint token = e.InstrumentToken;
            _firstCandleOutsideRange.TryAdd(token, null);

            if (e.CloseTime.TimeOfDay == FIRST_CANDLE_CLOSE_TIME)
            {
                candlelowbody = Math.Min(e.OpenPrice, e.ClosePrice);
                candlehighbody = Math.Max(e.OpenPrice, e.ClosePrice);

                //_firstCandleOutsideRange = ((candlelowbody > _previousDayOHLC.High && candlehighbody > _previousDayOHLC.High) || (candlelowbody < _previousDayOHLC.Low && candlehighbody < _previousDayOHLC.Low));
                _firstCandleOutsideRange[token] = ((candlelowbody > _previousDayOHLC[token].High) || (candlehighbody < _previousDayOHLC[token].Low));
            }
            //check for all the candles. The below logic will make it enter anytime when market is outside previous day range
            else if (!_firstCandleOutsideRange[token].HasValue || _firstCandleOutsideRange[token].Value)
            {
                _firstCandleOutsideRange[token] = true;
                foreach (Candle c in TimeCandles[token])
                {
                    candlelowbody = Math.Min(c.OpenPrice, c.ClosePrice);
                    candlehighbody = Math.Max(c.OpenPrice, c.ClosePrice);

                    if ((candlelowbody < _previousDayOHLC[token].High && candlelowbody > _previousDayOHLC[token].Low) || (candlehighbody > _previousDayOHLC[token].Low && candlehighbody < _previousDayOHLC[token].High))
                    {
                        _firstCandleOutsideRange[token] = false;
                        break;
                    }
                }
            }


            //Update Half trend at 5 mins interval
            if (_stockTokenHalfTrend.ContainsKey(token) && e.CloseTime.TimeOfDay > MARKET_START_TIME)
            {
                _stockTokenHalfTrend[token].Process(e);
            }

            //_firstCandleOutsideRange = true;
            //For EMA Logic : market should be open outside of previous day range, and remain so.
            if (_firstCandleOutsideRange[token].GetValueOrDefault(false))
            {
                if (_indexEMALoaded.ContainsKey(token) && _indexEMALoaded[token])
                {
                    _indexEMA[token].Process(e.ClosePrice, isFinal: true);
                }

                decimal tl = 0, sl = 0;
                bool s2r2Level = false;

                decimal htred = _stockTokenHalfTrend[token].GetTrend(); // 0 is up trend
                decimal htvalue = _stockTokenHalfTrend[token].GetCurrentValue<decimal>(); // 0 is up trend

                //Check for valid candle size, and then check for distance from EMA, and 70/30 ratio with respect to weekly pivots
                if ((e.ClosePrice > _previousDayOHLC[token].High || e.ClosePrice < _previousDayOHLC[token].Low)
                    && ValidateCandleForEMABaseEntry(e) && false &&
                    ((e.ClosePrice < _cpr[token].Prices[(int)PivotLevel.R3]
                    || e.ClosePrice > _cpr[token].Prices[(int)PivotLevel.S3]) ? ValidateRiskReward(e, htred, htvalue, out tl, out sl, out s2r2Level) : ValidateRiskRewardFromWeeklyPivots(e))
                    && _orderTriosFromEMATrades.Count == 0)
                {
                    int qty = _tradeQty;
                    _triggerTokenEMA = token;
                    bool buy = htred == 0;// e.ClosePrice > e.OpenPrice; // HTChange: Allow long trade with red candle
                    OrderTrio orderTrio = new OrderTrio();
                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice, //e.ClosePrice,
                       _activeFuture.KToken, buy, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, e.CloseTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                    //orderTrio.TPOrder = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", targetLevel,
                    //   _activeFuture.KToken, e.ClosePrice < e.OpenPrice, qty, algoIndex, e.CloseTime, orderType: "LIMIT",
                    //   broker: Constants.KOTAK, httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                    OnTradeEntry(orderTrio.Order);
                    _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (buy ? -1 : 1);

                    orderTrio.StopLoss = _indexEMAValue[token].GetValue<decimal>();
                    // orderTrio.TargetProfit = tl;
                    //orderTrio.TPFlag = false;
                    //_activeFuture.InstrumentToken = _baseInstrumentToken;
                    orderTrio.Option = _activeFuture;
                    _orderTriosFromEMATrades.Add(orderTrio);

#if !BACKTEST
                    OnCriticalEvents(e.CloseTime.ToShortTimeString(), String.Format("{0} @ {1}", buy ? "Bought " : "Sold ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                }
            }
            //Below logic runs at 15 mins candle
            if (e.CloseTime.TimeOfDay.Minutes % 15 == 0)
            {
                e = GetLastCandle(token);
                if (e == null)
                {
                    return;
                }



                //Check for extreme bullish or bearish scenario, and then in that case no opposite side trade unless there is candle over previous candle.

                if (e.OpenTime.TimeOfDay == MARKET_START_TIME)
                {
                    decimal candleBody = Math.Abs(e.ClosePrice - e.OpenPrice);
                    decimal candleSize = e.HighPrice - e.LowPrice;
                    bool bullish = e.ClosePrice > e.OpenPrice;
                    if ((candleBody > CANDLE_BODY_EXTREME) &&
                        ((candleBody > candleSize * 0.75m) ||
                        ((bullish && ((e.OpenPrice - e.LowPrice) >= 2.3m * (e.HighPrice - e.OpenPrice)))
                        || (!bullish && ((e.OpenPrice - e.LowPrice) * 2.3m <= (e.HighPrice - e.OpenPrice))))))
                    {
                        _extremeScenario = (bullish) ? MarketScenario.Bullish : MarketScenario.Bearish;
                    }
                }
                else if (e.OpenTime.TimeOfDay == EXTREME_END_TIME)
                {
                    _extremeScenario = MarketScenario.Neutral;
                }



                //Logic
                //step 1: Check the size of the candle to qualify
                //Step 2: Check Risk reward ratio to _targetRisk Reward ratio
                //Step 3: Take trade
                decimal targetLevel, stopLevel;
                bool s2r2Level = false;

                decimal htred = _stockTokenHalfTrend[token].GetTrend(); // 0 is up trend
                decimal htvalue = _stockTokenHalfTrend[token].GetCurrentValue<decimal>(); // 0 is up trend

                if (ValidateRiskReward(e, htred, htvalue, out targetLevel, out stopLevel, out s2r2Level)
                    && ValidateCandleSize(e, s2r2Level, htred)
                    && ValidateMarketScenario(e, _pCandle[token], _extremeScenario))
                {
                    //PostOrderInKotak(_activeFuture, e.CloseTime, _tradeQty, e.ClosePrice > e.OpenPrice);

                    if (_orderTrios.Count > 0
                        && ((_orderTrios[0].Order.TransactionType.ToLower() == "buy" && htred == 0) //e.ClosePrice > e.OpenPrice) //HTChange: allow long with red candle and vice versa
                        || (_orderTrios[0].Order.TransactionType.ToLower() == "sell" && htred == 1))) //e.ClosePrice < e.OpenPrice)))
                    {
                    }
                    else
                    {
                        int qty = _tradeQty;
                        //if (_orderTrios.Count > 0
                        //&& ((_orderTrios[0].Order.TransactionType.ToLower() == "sell" && e.ClosePrice > e.OpenPrice) || (_orderTrios[0].Order.TransactionType.ToLower() == "buy" && e.ClosePrice < e.OpenPrice)))
                        //{
                        //    qty = _tradeQty * 2;
                        //}
                        bool buy = e.ClosePrice > e.OpenPrice;

                        if (_orderTrios.Count == 0 && ((buy && htred == 0) || (!buy && htred == 1)))
                        {

                            OrderTrio orderTrio = new OrderTrio();
                            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice, //e.ClosePrice,
                               _activeFuture.KToken, buy, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, e.CloseTime, Tag: token.ToString(),
                               product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                            //orderTrio.TPOrder = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", targetLevel,
                            //   _activeFuture.KToken, e.ClosePrice < e.OpenPrice, qty, algoIndex, e.CloseTime, orderType: "LIMIT",
                            //   broker: Constants.KOTAK, httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                            OnTradeEntry(orderTrio.Order);
                            _triggerToken = token;
                            orderTrio.StopLoss = stopLevel;//token == _baseInstrumentToken ? orderTrio.Order.AveragePrice - (_baseInstrumentPrice - stopLevel) : stopLevel;
                            orderTrio.TargetProfit = targetLevel;//token == _baseInstrumentToken ? orderTrio.Order.AveragePrice + (targetLevel - _baseInstrumentPrice) : targetLevel;
                            orderTrio.TPFlag = false;
                            //_activeFuture.InstrumentToken = _baseInstrumentToken;
                            orderTrio.Option = _activeFuture;
                            _orderTrios.Add(orderTrio);
                            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (buy ? -1 : 1);
#if !BACKTEST
                            OnCriticalEvents(e.CloseTime.ToShortTimeString(), String.Format("{0} @ {1}", buy ? "Bought " : "Sold ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        }
                    }
                }
                if (token == _triggerToken)
                {
                    CheckSL(e.CloseTime, e.ClosePrice, _pCandle[token], htred);
                }

                //EOD closed moved to 5 mins candle duration
                //Closes all postions at 3:20 PM
                //TriggerEODPositionClose(e.CloseTime, e.ClosePrice);

                if (_pCandle.ContainsKey(token))
                {
                    _pCandle[token] = e;
                }
                else
                {
                    _pCandle.Add(token, e);
                }
            }

            if (token == _triggerTokenEMA)
            {
                CheckSLForEMABasedOrders(e.CloseTime, e.ClosePrice, _pCandle[token]);
            }
            //Closes all postions at 3:20 PM
            TriggerEODPositionClose(e.CloseTime, e.ClosePrice);
        }
        void UpdateFuturePrice(decimal lastPrice)
        {
            _activeFuture.LastPrice = lastPrice;
        }

        private void LoadHistoricalHT(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => /*x != _baseInstrumentToken &&*/ !_HTLoaded.Contains(x));

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
                            _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
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
        void SetTargetHitFlag(uint token, decimal lastPrice, DateTime currentTime)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    if (orderTrio.Option.InstrumentToken == token)
                    {
                        if ((orderTrio.Order.TransactionType.ToLower() == "buy") && 
                            (_triggerToken == _baseInstrumentToken ?_baseInstrumentPrice > orderTrio.TargetProfit : lastPrice > orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                            //HT Change: book at the target
                            CheckSL(currentTime, lastPrice, null, 0, true);
                        }
                        else if (orderTrio.Order.TransactionType.ToLower() == "sell" && 
                            (_triggerToken == _baseInstrumentToken ? _baseInstrumentPrice < orderTrio.TargetProfit : lastPrice < orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                            //HT Change: book at the target
                            CheckSL(currentTime, lastPrice, null, 0, true);
                        }
                    }
                }
            }
        }
        private void LoadHistoricalCandlesForHT(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            HalfTrend ht;
            lock (_stockTokenHalfTrend)
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);

                foreach (uint token in tokenList)
                {
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

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

        private void LoadCriticalLevels(uint token, DateTime currentTime)
        {
            DataLogic dl = new DataLogic();
            DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
            List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
            List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

            //OHLC pdOHLC = new OHLC() { Close = pdOHLCList.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

            OHLC pdOHLC = new OHLC() { Close = pdOHLCDay.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

            _cpr.Add(token, new CentralPivotRange(pdOHLC));

            pdOHLC.Close = pdOHLCList.Last().Close;
            _previousDayOHLC.TryAdd(token, pdOHLC);

            List<Historical> pwOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
            _previousWeekOHLC.TryAdd(token, new OHLC(pwOHLCList.First(), token));
            _weeklycpr.Add(token, new CentralPivotRange(_previousWeekOHLC[token]));

            _criticalLevels.TryAdd(token, new SortedList<decimal, int>());

            //load Previous Day and previous week data
            //_criticalLevels = new SortedList<decimal, int>();
            _criticalLevels[token].TryAdd(_previousDayOHLC[token].Close, 0);
            _criticalLevels[token].TryAdd(_previousDayOHLC[token].High, 1);
            _criticalLevels[token].TryAdd(_previousDayOHLC[token].Low, 2);
            //_criticalLevels.TryAdd(_previousDayOHLC.Open, 3);
            //_criticalLevels.TryAdd(_previousSwingHigh, 4);
            //_criticalLevels.TryAdd(_previousSwingLow, 5);
            // _criticalLevels.TryAdd(_previousDayBodyHigh, 0);
            //_criticalLevels.TryAdd(_previousDayBodyLow, 0);

            _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.CPR], 6);
            _criticalLevels[token].Remove(0);

            _criticalLevelsWeekly.Add(token, new SortedList<decimal, int>());
            _criticalLevelsWeekly[token].TryAdd(_previousWeekOHLC[token].Close, 0);
            _criticalLevelsWeekly[token].TryAdd(_previousWeekOHLC[token].High, 1);
            _criticalLevelsWeekly[token].TryAdd(_previousWeekOHLC[token].Low, 2);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.CPR], 3);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.R1], 4);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.R2], 5);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.R3], 6);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.S3], 7);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.S2], 8);
            _criticalLevelsWeekly[token].TryAdd(_weeklycpr[token].Prices[(int)PivotLevel.S1], 9);
            _criticalLevelsWeekly[token].TryAdd(_previousDayOHLC[token].High, 10);
            _criticalLevelsWeekly[token].TryAdd(_previousDayOHLC[token].Low, 11);
            _criticalLevelsWeekly[token].Remove(0);
        }

        private void CheckSL(DateTime currentTime, decimal lastPrice, Candle pCandle, decimal htred, bool closeAll = false)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (((lastPrice < orderTrio.StopLoss || htred == 1) || closeAll) || (pCandle != null && (lastPrice < pCandle.LowPrice) && orderTrio.TPFlag)))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product:Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(orderTrio.Order);

                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice;

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));


                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (((lastPrice > orderTrio.StopLoss || htred == 0) || closeAll) || (pCandle != null && (lastPrice > pCandle.HighPrice) && orderTrio.TPFlag)))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(orderTrio.Order);
                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

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
        private void CheckSLForEMABasedOrders(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        {
            if (_orderTriosFromEMATrades.Count > 0)
            {
                for (int i = 0; i < _orderTriosFromEMATrades.Count; i++)
                {
                    var orderTrio = _orderTriosFromEMATrades[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (lastPrice < _indexEMAValue[orderTrio.Order.InstrumentToken].GetValue<decimal>() || closeAll))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag:"Algo2", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(orderTrio.Order);

                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice;

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));


                        _orderTriosFromEMATrades.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (lastPrice > _indexEMAValue[orderTrio.Order.InstrumentToken].GetValue<decimal>() || closeAll))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                        OnTradeEntry(orderTrio.Order);
                        _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;

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

        private bool ValidateRiskReward (Candle c, decimal htred, decimal htvalue, out decimal targetLevel, out decimal stoploss, out bool s2r2level)
        {
            targetLevel = 0;
            stoploss = 0;
            s2r2level = false;
            bool _favourableRiskReward = false;
            uint token = c.InstrumentToken;
            if (c.ClosePrice > _cpr[token].Prices[(int)PivotLevel.LCPR] && c.ClosePrice < _cpr[token].Prices[(int)PivotLevel.UCPR])
            {
                // Do not take trade within CPR
            }
            else //if (ValidateCandleSize(c))
            {

                if (c.ClosePrice > _criticalLevels[token].Keys.Max())
                {
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.R1], 17);
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.R2], 18);
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.R3], 19);
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.R4], 23);
                }
                else if (c.ClosePrice < _criticalLevels[token].Keys.Min())
                {
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.S3], 20);
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.S2], 21);
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.S1], 22);
                    _criticalLevels[token].TryAdd(_cpr[token].Prices[(int)PivotLevel.S4], 24);
                }
                else
                {
                }

                //bullish scenario
                //if (c.ClosePrice > c.OpenPrice) // HT Change: Allow long with red candle also, and vice versa
                if (htred == 0 && c.ClosePrice > _indexEMAValue[token].GetValue<decimal>() && ((c.ClosePrice > c.OpenPrice) || ((c.ClosePrice < c.OpenPrice) && (c.LowPrice <= htvalue) && (c.ClosePrice< htvalue + 10)))) // bullish scenario
                {
                    decimal plevel = 0;
                    foreach (var level in _criticalLevels[token])
                    {
                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
                        {
                            if ((level.Key - c.ClosePrice) / (c.ClosePrice - plevel) > 2.0M) //2.3 changed to 2.0
                            {
                                targetLevel = level.Key;
                                stoploss = c.LowPrice;
                                _favourableRiskReward = true;
                                if (plevel == _cpr[token].Prices[(int)PivotLevel.S2]
                                    //|| plevel == _cpr.Prices[(int)PivotLevel.R2]
                                    || plevel == _weeklycpr[token].Prices[(int)PivotLevel.S2]
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
                else if (htred == 1 && c.ClosePrice < _indexEMAValue[token].GetValue<decimal>() && ((c.ClosePrice < c.OpenPrice) || ((c.ClosePrice > c.OpenPrice) && (c.HighPrice >= htvalue) && (c.ClosePrice > htvalue - 10)))) // bearish scenario
                {
                    decimal plevel = 0;
                    foreach (var level in _criticalLevels[token])
                    {
                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
                        {
                            if (Math.Abs((c.ClosePrice - plevel) / (level.Key - c.ClosePrice)) > 2.0M) //2.3 changed to 2.0
                            {
                                targetLevel = plevel;
                                stoploss = c.HighPrice;
                                _favourableRiskReward = true;

                                if (//plevel == _cpr.Prices[(int)PivotLevel.S2] ||
                                    level.Key == _cpr[token].Prices[(int)PivotLevel.R2]
                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
                                    || level.Key == _weeklycpr[token].Prices[(int)PivotLevel.R2])
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
        private bool ValidateCandleSize(Candle c, bool s2r2Level, decimal htred)
        {
            bool validCandle = false;

            if (Math.Abs(c.ClosePrice - c.OpenPrice) < CANDLE_BODY)
            {
                if (Math.Abs(c.ClosePrice - c.OpenPrice) <= CANDLE_BODY_MIN)
                {
                    //validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? ((c.OpenPrice - c.LowPrice) >= 2.3m * (c.HighPrice - c.OpenPrice)) : ((c.OpenPrice - c.LowPrice) * 2.3m <= (c.HighPrice - c.OpenPrice));
                 
                    //HTChange: allow long with red candle too. remove all design limits
                    validCandle = true;
                }
                else
                {
                    //validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? (((c.OpenPrice - c.LowPrice) >= (c.HighPrice - c.ClosePrice) * 0.9m && ((c.OpenPrice - c.LowPrice) < CANDLE_WICK_SIZE) ||(s2r2Level))
                    //    || ((c.ClosePrice - c.OpenPrice) >= 0.6m * (c.HighPrice - c.LowPrice))
                    //    || (((c.OpenPrice - c.LowPrice) <= 1.0m) && ((c.ClosePrice - c.OpenPrice) >= 0.5m * (c.HighPrice - c.LowPrice)))
                    //    ) :
                    //    (((c.ClosePrice - c.LowPrice) * 0.9m <= (c.HighPrice - c.OpenPrice) && ((c.HighPrice - c.OpenPrice) < CANDLE_WICK_SIZE) ||(s2r2Level))
                    //    || ((c.OpenPrice - c.ClosePrice) >= 0.6m * (c.HighPrice - c.LowPrice))
                    //    || (((c.HighPrice - c.OpenPrice) <= 1.0m) && ((c.OpenPrice - c.ClosePrice) >= 0.5m * (c.HighPrice - c.LowPrice)))
                    //    );

                    //HTChange: allow long with red candle too. remove all design limits
                    validCandle = true;
                }
            }
            return validCandle;
        }

        private bool ValidateMarketScenario(Candle c, bool s2r2Level)
        {
            bool validCandle = false;

            if (Math.Abs(c.ClosePrice - c.OpenPrice) < CANDLE_BODY)
            {
                if (Math.Abs(c.ClosePrice - c.OpenPrice) <= CANDLE_BODY_MIN)
                {
                    validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? ((c.OpenPrice - c.LowPrice) >= 2.3m * (c.HighPrice - c.OpenPrice)) : ((c.LowPrice - c.OpenPrice) * 2.3m <= (c.HighPrice - c.OpenPrice));
                }
                else
                {
                    validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? (((c.OpenPrice - c.LowPrice) >= (c.HighPrice - c.ClosePrice) * 0.9m && ((c.OpenPrice - c.LowPrice) < CANDLE_WICK_SIZE) || (s2r2Level))
                        || ((c.ClosePrice - c.OpenPrice) >= 0.6m * (c.HighPrice - c.LowPrice))
                        || (((c.OpenPrice - c.LowPrice) <= 1.0m) && ((c.ClosePrice - c.OpenPrice) >= 0.5m * (c.HighPrice - c.LowPrice)))
                        ) :
                        (((c.ClosePrice - c.LowPrice) * 0.9m <= (c.HighPrice - c.OpenPrice) && ((c.HighPrice - c.OpenPrice) < CANDLE_WICK_SIZE) || (s2r2Level))
                        || ((c.OpenPrice - c.ClosePrice) >= 0.6m * (c.HighPrice - c.LowPrice))
                        || (((c.HighPrice - c.OpenPrice) <= 1.0m) && ((c.OpenPrice - c.ClosePrice) >= 0.5m * (c.HighPrice - c.LowPrice)))
                        );
                }
            }
            return validCandle;
        }

        private bool ValidateMarketScenario(Candle e, Candle prevCandle, MarketScenario marketScenario)
        {
            bool validCandle = true;
            //if(_pCandle == null)
            //{
            //    validCandle = true;
            //}
            //else if ((marketScenario == MarketScenario.Bullish) && (e.ClosePrice < e.OpenPrice))
            //{
            //    validCandle = e.ClosePrice < prevCandle.LowPrice;
            //}
            //else if ((marketScenario == MarketScenario.Bearish) && (e.ClosePrice > e.OpenPrice))
            //{
            //    validCandle = e.ClosePrice > prevCandle.HighPrice;
            //}
            //else if(marketScenario == MarketScenario.Neutral)
            //{
            //    validCandle = true;
            //}
            return (validCandle);
        }


        private Candle GetLastCandle(uint instrumentToken)
        {
            if(TimeCandles[instrumentToken].Count < 3)
            {
                return null;
            }
            var lastCandles = TimeCandles[instrumentToken].TakeLast(3);
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.InstrumentToken = instrumentToken;
            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
            tC.ClosePrice = lastCandles.ElementAt(2).ClosePrice;
            tC.CloseTime = lastCandles.ElementAt(2).CloseTime;
            tC.HighPrice = Math.Max(Math.Max(lastCandles.ElementAt(0).HighPrice, lastCandles.ElementAt(1).HighPrice), lastCandles.ElementAt(2).HighPrice);
            tC.LowPrice = Math.Min(Math.Min(lastCandles.ElementAt(0).LowPrice, lastCandles.ElementAt(1).LowPrice), lastCandles.ElementAt(2).LowPrice);
            return tC;
        }
        private bool ValidateRiskRewardFromWeeklyPivots(Candle c)
        {
            bool _favourableRiskReward = false;
            uint token = c.InstrumentToken;
            //bullish scenario
            if (c.ClosePrice > c.OpenPrice)
            {
                decimal plevel = 0;
                foreach (var level in _criticalLevelsWeekly[token])
                {
                    if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
                    {
                        if ((level.Key - c.ClosePrice) / (c.ClosePrice - plevel) > 2.3M)
                        {
                            _favourableRiskReward = true;
                        }
                        break;
                    }
                    plevel = level.Key;
                }
            }
            else
            {
                decimal plevel = 0;
                foreach (var level in _criticalLevelsWeekly[token])
                {
                    if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0)
                    {
                        if (Math.Abs((c.ClosePrice - plevel) / (level.Key - c.ClosePrice)) > 2.3M)
                        {
                            _favourableRiskReward = true;
                            break;
                        }
                    }
                    plevel = level.Key;
                }
            }
            return _favourableRiskReward;
        }

        private bool ValidateCandleForEMABaseEntry(Candle c)
        {
            uint token = c.InstrumentToken;
            bool validCandle = false;
            if (Math.Abs(c.ClosePrice - c.OpenPrice) < CANDLE_BODY_BIG)
            {
                validCandle = (c.ClosePrice - c.OpenPrice) > 0 ? (((c.OpenPrice - c.LowPrice) * 0.9m > (c.HighPrice - c.ClosePrice) && (c.OpenPrice - c.LowPrice) < CANDLE_WICK_SIZE) || ((c.ClosePrice - c.OpenPrice) >= 0.6m * (c.HighPrice - c.LowPrice))) :
                    (((c.ClosePrice - c.LowPrice) < (c.HighPrice - c.OpenPrice) * 0.9m && (c.HighPrice - c.OpenPrice) < CANDLE_WICK_SIZE) || ((c.OpenPrice - c.ClosePrice) >= 0.6m * (c.HighPrice - c.LowPrice)));
            }
            if(validCandle)
            {
                validCandle = (((_indexEMAValue[token].GetValue<decimal>() - c.ClosePrice < EMA_ENTRY_RANGE) && (_indexEMAValue[token].GetValue<decimal>() > c.ClosePrice) && c.ClosePrice < _previousDayOHLC[token].Low && c.ClosePrice < c.OpenPrice) || (
                    (c.ClosePrice - _indexEMAValue[token].GetValue<decimal>() < EMA_ENTRY_RANGE) && (c.ClosePrice > _indexEMAValue[token].GetValue<decimal>()) 
                    && c.ClosePrice > _previousDayOHLC[token].High && c.ClosePrice > c.OpenPrice));
            }

            return validCandle;
        }
        private void TriggerEODPositionClose(DateTime currentTime, decimal lastPrice)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00))
            {
                CheckSL(currentTime, lastPrice, null, 0, true);
                CheckSLForEMABasedOrders(currentTime, lastPrice, null, true);

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
                    if (!_indexEMALoading.ContainsKey(bToken) || !_indexEMALoading[bToken])
                    {
                        _indexEMALoading.TryAdd(bToken, true);
                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _indexEMALoadedFromDB.ContainsKey(bToken) && _indexEMALoadedFromDB[bToken])
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _indexEMA[bToken].Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var price in TimeCandles[bToken])
                            {
                                _indexEMA[bToken].Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexEMALoadedFromDB.ContainsKey(bToken) && _indexEMALoadedFromDB[bToken])
                    {
                        _indexEMALoaded[bToken] = true;
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
                    if (!_indexEMA.ContainsKey(bToken))
                    {
                        ExponentialMovingAverage ema = new ExponentialMovingAverage(length: 20);
                        _indexEMA.Add(bToken, ema);
                    }

                
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicals)
                    {
                        _indexEMA[bToken].Process(price.Close, isFinal: true);
                    }
                    _indexEMALoadedFromDB.TryAdd(bToken, true);
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
        //    DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.PAWithLevels, Convert.ToDateTime("2021-11-30"), Convert.ToDateTime("2021-12-30"));

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
        //        lock (_indexEMA)
        //        {
        //            DataLogic dl = new DataLogic();
        //            Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

        //            foreach (var price in historicalCandlePrices[bToken])
        //            {
        //                _indexEMA.Process(price, isFinal: true);
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

        private void LoadFutureToTrade(DateTime currentTime)
        {
            try
            {
                if (_activeFuture == null)
                {
                    

#if BACKTEST
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "FUT");
                    _activeFuture.LotSize = 50;
#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
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
                if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                {
                    SubscriptionTokens.Add(_baseInstrumentToken);

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
