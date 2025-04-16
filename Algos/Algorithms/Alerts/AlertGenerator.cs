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
using static Algorithms.Utilities.Utility;
using Algorithm.Algorithm;


namespace Algorithms.Algorithms
{
    public class AlertGenerator : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(AlertGenerator source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnCriticalEventsHandler(uint instrumentToken, IIndicator indicator, int timeFrame, decimal lastTradePrice);
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

        private decimal _previousStrikePEProfit;
        private decimal _previousStrikeCEProfit;
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

        TimeSpan _candleTimeSpan = new TimeSpan(0, 1, 0);
        //InstrumentToken is the key, and InstrumentToken, and indicator list are values
        Dictionary<uint, AlertInstrumentTokenWithIndicators> _alertTriggerData = null;


        public AlertGenerator(Dictionary<uint, AlertInstrumentTokenWithIndicators>  alertTriggerData)
        {
            _alertTriggerData = alertTriggerData;

            //Load Instrument detail from database in the setup function
            SetUpInitialData();

            

        }

        private void SetUpInitialData(int algoInstance = 0)
        {
            _HTLoaded = new List<uint>();
            _SQLLoadingHT = new List<uint>();


            SubscriptionTokens = [.. _alertTriggerData.Keys];


            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                DateTime.Now, _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, _intraday ? 1 : 0,
                0, 0, Arg9: "_user.UserId", positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

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
                if (currentTime.TimeOfDay > MARKET_START_TIME)
                {
                    uint token = tick.InstrumentToken;
                    lock (tradeLock)
                    {
#if BACKTEST
                        if (SubscriptionTokens.Contains(token))
                        {
#endif
                        //Load Historical Data so that indicator values can be calculated
                        //Keep calculating indicator values based on time candles

                        if (!_HTLoaded.Contains(token))
                        {
//                            LoadHistoricalHT(currentTime);
                        }
                        //No live alerts for now.

                        //Generate 1 min candles for all instruments
                        MonitorCandles(tick, currentTime);
                       
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
                _alertTriggerData[e.InstrumentToken].TimeFrameWithIndicators.ForEach(t => {
                    t.Indicators.ForEach(i => {

                        // Step 1: Get all the candles.
                        //Below logic runs at t mins candle
                        if ((e.CloseTime.TimeOfDay.TotalMinutes - MARKET_START_TIME.TotalMinutes) % t.TimeFrame.Minutes == 0)
                        {
                            Candle c = GetLastCandle(t.TimeFrame.Minutes, e.CloseTime, e.InstrumentToken);
                            i.ProcessData(new CandleIndicatorValue(i, c));
                            
                            //Checking for trigger here is better i think, in an async manner
                            //This event handler will check for triggers registered by client for this instrument token, indicator, and timeframe
                            OnCriticalEvents(e.InstrumentToken, i, t.TimeFrame.Minutes, e.ClosePrice);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.StackTrace);
            }
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

        private void LoadOptionsToTrade(DateTime currentTime, bool forceReload = false)
        {
            try
            {
                //PE and CE collection will have same strikes.
                if (OptionUniverse == null ||
                    (OptionUniverse[(int)InstrumentType.CE].Keys.First() > _baseInstrumentPrice - _minDistanceFromBInstrument
                    || OptionUniverse[(int)InstrumentType.CE].Keys.Last() < _baseInstrumentPrice + _minDistanceFromBInstrument)
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
                        }
                    }

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


        //private Candle GetLastCandle(Candle c1, Candle c2)
        //{
        //    TimeFrameCandle tC = new TimeFrameCandle();
        //    tC.InstrumentToken = c1.InstrumentToken;
        //    tC.OpenPrice = c1.OpenPrice;
        //    tC.OpenTime = c1.OpenTime;
        //    tC.ClosePrice = c2.ClosePrice;
        //    tC.CloseTime = c2.CloseTime;
        //    tC.Final = true;
        //    tC.State = CandleStates.Finished;
        //    tC.HighPrice = Math.Max(c1.HighPrice, c2.HighPrice);
        //    tC.LowPrice = Math.Min(c1.LowPrice, c2.LowPrice);
        //    return tC;
        //}
        private Candle GetLastCandle(int minutes, DateTime currentTime, uint token)
        {
            if (currentTime.TimeOfDay.TotalMinutes - MARKET_START_TIME.TotalMinutes < minutes)
            {
                return null;
            }

            var lastCandles = TimeCandles[token].TakeLast(minutes);
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.InstrumentToken = token;
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
        //private void ResetAlgo(DateTime tradeDate)
        //{
        //    _currentDate = tradeDate;
        //    _stopTrade = false;
        //    DataLogic dl = new DataLogic();
        //    DateTime? nextExpiry = dl.GetCurrentMonthlyExpiry(_currentDate.Value, _baseInstrumentToken);
        //    SetUpInitialData(nextExpiry);
        //}
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
