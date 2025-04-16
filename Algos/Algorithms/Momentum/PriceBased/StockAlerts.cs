using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using BrokerConnectWrapper;
using FirebaseAdmin.Messaging;
using GlobalCore;
using GlobalLayer;
//using KafkaFacade;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ZMQFacade;

namespace Algorithms.Algorithms
{
    public class StockAlerts : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(StockAlerts source);
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

        public List<Instrument> ActiveOptions { get; set; }
        //public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        public Dictionary<uint, Instrument> TradedOptions { get; set; }
        public Dictionary<uint, Instrument> AllStocks { get; set; }

        public SortedList<decimal, Instrument> CallOptionsByStrike { get; set; }
        public SortedList<decimal, Instrument> PutOptionsByStrike { get; set; }
        
        Dictionary<uint, RelativeStrengthIndex> _stockTokenLongRSI;
        Dictionary<uint, RelativeStrengthIndex> _stockTokenShortRSI;

        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        Dictionary<uint, AverageDirectionalIndex> _stockTokenADX;
        Dictionary<uint, StochasticOscillator> _stockTokenStochastic;
        Dictionary<uint, CentralPivotRange> _stockTokenCPR;
        //Dictionary<uint, > _stockTokenCPR;

        private User _user;
        TimeSpan _candleTimeSpanShort, _candleTimeSpanLong;
        Dictionary<uint, Instrument> _stockUniverse;
        private Candle _pCandle1;
        private Candle _pCandle2;
        private bool _slUpdated = false;
        private List<OrderTrio> _orderTrios;
        private List<OrderTrio> _orderTriosFromEMATrades;
        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        
        private CentralPivotRange _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private decimal _baseInstrumentStartPrice;
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
        private Dictionary<uint, Candle> _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.MA;
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

        private const decimal CANDLE_SIZE_PERCENT = 0.008m;
        private const decimal CANDLE_BODY_PERCENT = 0.7m;
        private const decimal CANDLE_BODY_MIN_PERCENT = 0.1m;
        private const decimal CANDLE_BODY_MIN_CANDLE_PERCENT = 2.6m;

        //StochasticOscillator _indexSch;

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
        List<uint> _RSILoaded, _ADXLoaded, _schLoaded, _cprLoaded;
        List<uint> _SQLLoadingRSI, _SQLLoadingAdx, _SQLLoadingSch;
        //bool _indexSchLoaded = false;
        //bool _indexSchLoading = false;
        //bool _indexSchLoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;

        public StockAlerts(TimeSpan candleTimeSpanShort, TimeSpan candleTimeSpanLong, uint baseInstrumentToken,
            int quantity, string uid, int algoInstance = 0, IHttpClientFactory httpClientFactory = null)
        {
#if !BACKTEST
            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);
#endif

            _httpClientFactory = httpClientFactory;
            _candleTimeSpanShort = candleTimeSpanShort;
            _candleTimeSpanLong = candleTimeSpanLong;

            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _tradeQty = quantity;
            SetUpInitialData(algoInstance);
        }
        private void SetUpInitialData(int algoInstance = 0)
        {
            //_expiryDate = expiry;
            _orderTrios = new List<OrderTrio>();
            _orderTriosFromEMATrades = new List<OrderTrio>();
            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();
            _pCandle = new Dictionary<uint, Candle>();
            //_indexSch = new StochasticOscillator();

            _stockUniverse = new Dictionary<uint, Instrument>();

            _stockTokenLongRSI = new Dictionary<uint, RelativeStrengthIndex>();
            _stockTokenShortRSI = new Dictionary<uint, RelativeStrengthIndex>();
            _stockTokenADX = new Dictionary<uint, AverageDirectionalIndex>(13);
            _stockTokenHalfTrend = new Dictionary<uint, HalfTrend>();
            _stockTokenStochastic = new Dictionary<uint, StochasticOscillator>();
            _stockTokenCPR = new Dictionary<uint, CentralPivotRange>();

            _RSILoaded = new List<uint>();
            _ADXLoaded = new List<uint>();
            _schLoaded = new List<uint>();
            _cprLoaded = new List<uint>();

            _SQLLoadingRSI = new List<uint>();
            _SQLLoadingAdx = new List<uint>();
            _SQLLoadingSch = new List<uint>();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now, DateTime.Now,
                //expiry.GetValueOrDefault(DateTime.Now), 
                _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpanShort.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                (decimal)_candleTimeSpanLong.TotalMinutes, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

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

                    if (_baseInstrumentStartPrice == 0
                        && currentTime.TimeOfDay >= new TimeSpan(09, 15, 00))
                    {
                        _baseInstrumentStartPrice = _baseInstrumentPrice;
                    }
                    LoadStocksToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);

                    if (!_RSILoaded.Contains(token) && token != _baseInstrumentToken)
                    {
                        LoadHistoricalRSIs(currentTime);
                    }

                    //Alert 2: ADX with 15 min time frame and candle with 5 mins time frame
                    if (!_ADXLoaded.Contains(token) && token != _baseInstrumentToken)
                    {
                        LoadHistoricalADX(currentTime);
                    }

                    if (!_schLoaded.Contains(token) && token != _baseInstrumentToken)
                    {
                        LoadHistoricalStochastics(currentTime);
                    }

                    if (!_cprLoaded.Contains(token) && token != _baseInstrumentToken)
                    {
                        LoadStockCPRs(currentTime);
                    }

#if local

                    if (SubscriptionTokens.Contains(token))
                    {
#endif
                    if (_baseInstrumentToken != token)
                    {
                        MonitorCandles(tick, currentTime);
                    }
#if local
                    }
#endif
                    //SetTargetHitFlag(tick.InstrumentToken, tick.LastPrice);
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
                #region Alert 1: Filter stocks that are below 40 RSI in 2 hours and > 60 RSI in 5 mins.
                if (_stockTokenShortRSI.ContainsKey(e.InstrumentToken) && _stockTokenLongRSI.ContainsKey(e.InstrumentToken))
                {
                    _stockTokenShortRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);

                    //Below logic runs at 2 hours candle
                    if (e.CloseTime.TimeOfDay.Minutes % 120 == 0)
                    {
                        _stockTokenLongRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                    }

                    if (_stockTokenLongRSI[e.InstrumentToken].GetCurrentValue<decimal>() < 40
                            && _stockTokenShortRSI[e.InstrumentToken].GetCurrentValue<decimal>() > 60
                            && e.ClosePrice < e.OpenPrice
                            && _pCandle1 != null
                            && _pCandle2 != null
                            && e.ClosePrice < _pCandle1.ClosePrice
                            && e.ClosePrice < _pCandle2.ClosePrice)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                       String.Format(@"Short {0} @ {3} - 2 hour RSI: {1} & 5 mins RSI: {2}", AllStocks[e.InstrumentToken].TradingSymbol,
                       _stockTokenLongRSI[e.InstrumentToken].GetCurrentValue<decimal>(),
                       _stockTokenShortRSI[e.InstrumentToken].GetCurrentValue<decimal>(), e.ClosePrice), "Candle Closure");
                    }
                    else if (_stockTokenLongRSI[e.InstrumentToken].GetCurrentValue<decimal>() > 60
                            && _stockTokenShortRSI[e.InstrumentToken].GetCurrentValue<decimal>() < 40
                            && e.ClosePrice > e.OpenPrice
                            && _pCandle1 != null
                            && _pCandle2 != null
                            && e.ClosePrice > _pCandle1.ClosePrice
                            && e.ClosePrice > _pCandle2.ClosePrice)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                       String.Format(@"Long {0} @ {3} - 2 hour RSI: {1} & 5 mins RSI: {2}", AllStocks[e.InstrumentToken].TradingSymbol,
                       _stockTokenLongRSI[e.InstrumentToken].GetCurrentValue<decimal>(),
                       _stockTokenShortRSI[e.InstrumentToken].GetCurrentValue<decimal>(), e.ClosePrice), "Candle Closure");
                    }
                }
                _pCandle2 = _pCandle1;
                _pCandle1 = e;

                #endregion
                #region Alert 2: Halftrend (5 mins) and ADX (15 mins, Length 13). Candle time frame 5 mins.
                if (_stockTokenADX.ContainsKey(e.InstrumentToken))
                {
                    _stockTokenHalfTrend[e.InstrumentToken].Process(e);
                    if(e.CloseTime.TimeOfDay.Minutes % 15 == 0)
                    {
                        //_stockTokenADX[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                        _stockTokenADX[e.InstrumentToken].Process(e);
                    }

                    if (_stockTokenADX[e.InstrumentToken].MovingAverage.GetCurrentValue<decimal>() > 23)
                    {
                        if (_stockTokenADX[e.InstrumentToken].Dx.Plus.GetCurrentValue<decimal>() > _stockTokenADX[e.InstrumentToken].Dx.Minus.GetCurrentValue<decimal>()
                           && _stockTokenHalfTrend[e.InstrumentToken].GetTrend() == 0 //up 
                           )
                        {
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                           String.Format(@"Long {4} @ {5}. ADX {0}, DI+ {1}, DI- {2}, HT {3}", _stockTokenADX[e.InstrumentToken].GetCurrentValue<decimal>(), 
                           _stockTokenADX[e.InstrumentToken].Dx.Plus.GetCurrentValue<decimal>(),
                           _stockTokenADX[e.InstrumentToken].Dx.Minus.GetCurrentValue<decimal>(), 
                           _stockTokenHalfTrend[e.InstrumentToken].GetCurrentValue<decimal>(), AllStocks[e.InstrumentToken].TradingSymbol, e.ClosePrice), "Candle Closure");
                        }
                        else if (_stockTokenADX[e.InstrumentToken].Dx.Plus.GetCurrentValue<decimal>() < _stockTokenADX[e.InstrumentToken].Dx.Minus.GetCurrentValue<decimal>()
                           && _stockTokenHalfTrend[e.InstrumentToken].GetTrend() == 1 //down 
                           )
                        {
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                           String.Format(@"Short {4} @ {5}. ADX {0}, DI+ {1}, DI- {2}, HT {3}", _stockTokenADX[e.InstrumentToken].GetCurrentValue<decimal>(),
                           _stockTokenADX[e.InstrumentToken].Dx.Plus.GetCurrentValue<decimal>(),
                           _stockTokenADX[e.InstrumentToken].Dx.Minus.GetCurrentValue<decimal>(),
                           _stockTokenHalfTrend[e.InstrumentToken].GetCurrentValue<decimal>(), AllStocks[e.InstrumentToken].TradingSymbol, e.ClosePrice), "Candle Closure");
                        }
                    }
                }
                #endregion

                #region Alert 3: //Check for 1% or 0.9% target hit. CPR cross, Sch and VWAP confirm.
               
                if(e.OpenTime.TimeOfDay >= new TimeSpan(10,45,0) && _stockTokenCPR.ContainsKey(e.InstrumentToken))
                {
                    foreach (decimal price in _stockTokenCPR[e.InstrumentToken].Prices)
                    {
                        if (e.ClosePrice < price && e.OpenPrice > price && price != _stockTokenCPR[e.InstrumentToken].Prices[(int)PivotLevel.LCPR]
                            && price != _stockTokenCPR[e.InstrumentToken].Prices[(int)PivotLevel.UCPR]
                            && _stockTokenStochastic[e.InstrumentToken].K.GetCurrentValue<decimal>() < _stockTokenStochastic[e.InstrumentToken].D.GetCurrentValue<decimal>()
                            //&& below VWAP
                                )
                        {
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                          String.Format(@"Short {0} @ {1}.Sch {2}", AllStocks[e.InstrumentToken].TradingSymbol, e.ClosePrice, _stockTokenStochastic[e.InstrumentToken].K.GetCurrentValue<decimal>()), "Candle Closure");
                        }
                        else if (e.ClosePrice > price && e.OpenPrice < price && price != _stockTokenCPR[e.InstrumentToken].Prices[(int)PivotLevel.LCPR]
                            && price != _stockTokenCPR[e.InstrumentToken].Prices[(int)PivotLevel.UCPR]
                            && _stockTokenStochastic[e.InstrumentToken].K.GetCurrentValue<decimal>() > _stockTokenStochastic[e.InstrumentToken].D.GetCurrentValue<decimal>()
                                //&& below VWAP
                                )
                        {
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                          String.Format(@"Long {0} @ {1}. Sch {2}", AllStocks[e.InstrumentToken].TradingSymbol, e.ClosePrice, _stockTokenStochastic[e.InstrumentToken].K.GetCurrentValue<decimal>()), "Candle Closure");
                        }
                    }
                }
                #endregion


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

        private void LoadHistoricalRSIs(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_RSILoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
    
                    if (!_stockTokenShortRSI.ContainsKey(t) && !_SQLLoadingRSI.Contains(t))
                    {
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingRSI.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //_SQLLoading.Add(token);
                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandlesForRSI(tokenList, 28, lastCandleEndTime));
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _stockTokenShortRSI.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _stockTokenShortRSI[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                _stockTokenShortRSI[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenShortRSI.ContainsKey(tkn))
                    {
                        _RSILoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("RSI loaded from DB for {0}", tkn), "MonitorCandles");
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
        private void LoadHistoricalADX(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_ADXLoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!_stockTokenADX.ContainsKey(t) && !_SQLLoadingAdx.Contains(t))
                    {
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingAdx.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //_SQLLoading.Add(token);
                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandlesForADX(tokenList, 28, lastCandleEndTime));
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _stockTokenADX.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _stockTokenADX[tkn].Process(TimeCandles[tkn].First());
                            _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                _stockTokenADX[tkn].Process(TimeCandles[tkn].First());
                                _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenADX.ContainsKey(tkn))
                    {
                        _ADXLoaded.Add(tkn);
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

        private void LoadHistoricalStochastics(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_schLoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!_stockTokenStochastic.ContainsKey(t) && !_SQLLoadingSch.Contains(t))
                    {
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingSch.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //_SQLLoading.Add(token);
                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandlesForSch(tokenList, 30, lastCandleEndTime, _candleTimeSpanShort));
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _stockTokenStochastic.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _stockTokenStochastic[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                _stockTokenStochastic[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenStochastic.ContainsKey(tkn))
                    {
                        _schLoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("RSI loaded from DB for {0}", tkn), "MonitorCandles");
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
                        DataLogic dl = new DataLogic();
                        DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
                        List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
                        List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

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

        void SetTargetHitFlag(uint token, decimal lastPrice)
        {
            if (_orderTrios.Count > 0 & TradedOptions.ContainsKey(token))
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if (orderTrio.Option.InstrumentToken == token)
                    {
                        if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (TradedOptions[token].LastPrice > orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                        }
                        else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (TradedOptions[token].LastPrice < orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                        }
                    }
                }
            }
        }
        private void UpdateStopLoss()
        {
            for (int i = 0; i < _orderTrios.Count; i++)
            {
                var orderTrio = _orderTrios[i];
                orderTrio.StopLoss = orderTrio.Order.AveragePrice;
            }
        }

        //private bool QualifyCandle(uint instrumentToken, bool bullish)
        //{
        //    if (TimeCandles[instrumentToken].Count < 3)
        //    {
        //        return false;
        //    }
        //    var lastCandles = TimeCandles[instrumentToken].TakeLast(3);
        //    TimeFrameCandle tC = new TimeFrameCandle();

        //    lastCandles.ElementAt(0)

        //    tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
        //    tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
        //    tC.ClosePrice = lastCandles.ElementAt(2).ClosePrice;
        //    tC.CloseTime = lastCandles.ElementAt(2).CloseTime;
        //    tC.HighPrice = Math.Max(Math.Max(lastCandles.ElementAt(0).HighPrice, lastCandles.ElementAt(1).HighPrice), lastCandles.ElementAt(2).HighPrice);
        //    tC.LowPrice = Math.Min(Math.Min(lastCandles.ElementAt(0).LowPrice, lastCandles.ElementAt(1).LowPrice), lastCandles.ElementAt(2).LowPrice);
        //    return tC;
        //}

        #region Historical Candle 
        private void LoadHistoricalCandlesForRSI(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            LoadHistoricalCandles(tokenList, candlesCount, lastCandleEndTime, _candleTimeSpanShort, _stockTokenShortRSI);
            LoadHistoricalCandles(tokenList, candlesCount, lastCandleEndTime, _candleTimeSpanLong, _stockTokenLongRSI);
        }
        private void LoadHistoricalCandles(string tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan, Dictionary<uint, RelativeStrengthIndex> tokenRSI)
        {

            DataLogic dl = new DataLogic();

            Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, timeSpan);
            RelativeStrengthIndex rsi;

            lock (tokenRSI)
            {
                foreach (uint t in historicalCandlePrices.Keys)
                {
                    rsi = new RelativeStrengthIndex();
                    foreach (var price in historicalCandlePrices[t])
                    {
                        rsi.Process(price, isFinal: true);
                    }
                    tokenRSI.Add(t, rsi);
                }
            }
        }

        private void LoadHistoricalCandlesForADX(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            LoadHistoricalCandlesForHT(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(0, 5, 0));
            LoadHistoricalCandlesForADX(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(0,15,0));
        }
        private void LoadHistoricalCandlesForADX(string tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            AverageDirectionalIndex adx;

            CandleSeries cs = new CandleSeries();
            List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            lock (_stockTokenADX)
            {
                foreach (var candle in historicalCandles)
                {
                    if(_stockTokenADX.ContainsKey(candle.InstrumentToken))
                    {
                        _stockTokenADX[candle.InstrumentToken].Process(candle);
                    }
                    else
                    {
                        adx = new AverageDirectionalIndex();
                        adx.Process(candle);
                        adx.Process(candle);
                        _stockTokenADX.TryAdd(candle.InstrumentToken, adx);
                    }
                }
            }
        }
        private void LoadHistoricalCandlesForHT(string tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            CandleSeries cs = new CandleSeries();
            List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            HalfTrend ht;

            lock (_stockTokenHalfTrend)
            {
                foreach (var candle in historicalCandles)
                {
                    if (_stockTokenHalfTrend.ContainsKey(candle.InstrumentToken))
                    {
                        _stockTokenHalfTrend[candle.InstrumentToken].Process(candle);
                    }
                    else
                    {
                        ht = new HalfTrend(13, 13);
                        ht.Process(candle);
                        _stockTokenHalfTrend.TryAdd(candle.InstrumentToken, ht);
                    }
                }
            }
        }

        private void LoadHistoricalCandlesForSch(string tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            CandleSeries cs = new CandleSeries();
            List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            //DataLogic dl = new DataLogic();
            //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, new TimeSpan(0,5,0));

            StochasticOscillator sch;

            lock (_stockTokenStochastic)
            {
                foreach(var candle in historicalCandles)
                {
                    if (_stockTokenStochastic.ContainsKey(candle.InstrumentToken))
                    {
                        _stockTokenStochastic[candle.InstrumentToken].Process(candle);
                    }
                    else
                    {
                        sch = new StochasticOscillator();
                        sch.Process(candle);
                        _stockTokenStochastic.TryAdd(candle.InstrumentToken, sch);
                    }
                }
            }
        }

        #endregion

        bool QualifyCandle(Candle e, bool bullish)
        {
            return bullish ? ValidateBullishCandle(e) : ValidateBearishCandle(e);
        }
        private bool ValidateBullishCandle(Candle c)
        {
            bool validCandle = false;

            if (Math.Abs(c.HighPrice - c.LowPrice) < CANDLE_SIZE_PERCENT * c.ClosePrice)
            {
                if ((c.ClosePrice - c.OpenPrice) > CANDLE_BODY_PERCENT * (c.HighPrice - c.LowPrice))
                {
                    //Upper wick is small
                    // if ((c.HighPrice - c.ClosePrice) < (c.OpenPrice - c.LowPrice))
                    //{
                    validCandle = true;
                    //}
                }
                else if ((c.ClosePrice - c.OpenPrice) > CANDLE_BODY_MIN_PERCENT * (c.HighPrice - c.LowPrice))
                {
                    //Lower wick is big
                    if ((c.OpenPrice - c.LowPrice) > (c.HighPrice - c.OpenPrice) * 1.5m)
                    {
                        validCandle = true;
                    }
                }
            }
            return validCandle;
        }
        private bool ValidateBearishCandle(Candle c)
        {
            bool validCandle = false;
            if (Math.Abs(c.HighPrice - c.LowPrice) < CANDLE_SIZE_PERCENT * c.ClosePrice)
            {
                if ((c.OpenPrice - c.ClosePrice) > CANDLE_BODY_PERCENT * (c.HighPrice - c.LowPrice))
                {
                    //Lower wick is small
                    // if ((c.ClosePrice - c.LowPrice) < (c.HighPrice - c.OpenPrice))
                    //{
                    validCandle = true;
                    //}
                }
                else if ((c.OpenPrice - c.ClosePrice) > CANDLE_BODY_MIN_PERCENT * (c.HighPrice - c.LowPrice))
                {
                    //Upper wick is big
                    if ((c.OpenPrice - c.LowPrice) * 1.5m < (c.HighPrice - c.OpenPrice))
                    {
                        validCandle = true;
                    }
                }
            }
            return validCandle;
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

        private bool IsWickQualified(decimal wick)
        {
            return wick > QUALIFICATION_ZONE_THRESHOLD;
        }
        void UpdateFuturePrice(decimal lastPrice)
        {
            atmOption.LastPrice = lastPrice;
        }
        private void TakeTrade(string instrumentType, uint bToken, decimal bPrice, DateTime currentTime, decimal targetLevel, decimal stopLevel)
        {
            int qty = _tradeQty;
            //if (_orderTrios.Count > 0)
            //{
            if (!TradedOptions.ContainsKey(bToken))// && _orderTrios[0].Option.InstrumentType.Trim(' ').ToLower() != instrumentType.Trim(' ').ToLower())
            {
                //#if market
                //                    CloseTrade(currentTime, _orderTrios[0].Option);
                //#elif local
                //                    CloseTrade(currentTime, TradedOptions[_orderTrios[0].Option.InstrumentToken]);
                //#endif
                //    }
                //    else
                //    {
                //        qty = 0;
                //    }
                //}

                //if (qty > 0)
                //{
                OrderTrio orderTrio = new OrderTrio();

                //#if market
                //                DataLogic dl = new DataLogic();
                //                Instrument otmOption = dl.GetOTMInstrument(_expiryDate, bToken, bPrice, instrumentType);

                //#elif BACKTEST
                Instrument otmOption = AllStocks[bToken];
                otmOption.LastPrice = bPrice;
                otmOption.InstrumentType = instrumentType;
                if (bPrice == 0)
                {
                    return;
                }
                //#endif

                //#if market
                //                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, otmOption.TradingSymbol, otmOption.InstrumentType.ToLower(), otmOption.LastPrice, //e.ClosePrice,
                //                   otmOption.KToken, true, _tradeQty * Convert.ToInt32(otmOption.LotSize), algoIndex, currentTime, Tag: "SM",
                //                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                //                orderTrio.StopLoss = orderTrio.Order.AveragePrice - stopLevel;
                //                orderTrio.TargetProfit = orderTrio.Order.AveragePrice + Math.Max(targetLevel, 5); //at least 5 points scalping
                //#elif BACKTEST

                if (instrumentType.ToLower() == "ce")
                {
                    qty = _tradeQty * Convert.ToInt32(otmOption.LotSize);
                    qty = 10;
                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, otmOption.TradingSymbol, otmOption.InstrumentType.ToLower(), otmOption.LastPrice, //e.ClosePrice,
                    otmOption.KToken, true, qty, algoIndex, currentTime, Tag: "SM",
                    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                    orderTrio.StopLoss = orderTrio.Order.AveragePrice - stopLevel;
                    orderTrio.TargetProfit = orderTrio.Order.AveragePrice + Math.Max(targetLevel, 5); //at least 5 points scalping
                }
                else if (instrumentType.ToLower() == "pe")
                {
                    qty = _tradeQty * Convert.ToInt32(otmOption.LotSize);
                    qty = 10;

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, otmOption.TradingSymbol, otmOption.InstrumentType.ToLower(), otmOption.LastPrice, //e.ClosePrice,
                                otmOption.KToken, false, qty, algoIndex, currentTime, Tag: "SM",
                                broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                    orderTrio.StopLoss = orderTrio.Order.AveragePrice + stopLevel;
                    orderTrio.TargetProfit = orderTrio.Order.AveragePrice - Math.Max(targetLevel, 5); //at least 5 points scalping

                }
                //#endif

                //orderTrio.TPFlag = false;
                ////atmOption.InstrumentToken = _baseInstrumentToken;
                otmOption.LastPrice = orderTrio.Order.AveragePrice;
                orderTrio.Option = otmOption;
                _orderTrios.Add(orderTrio);

                TradedOptions ??= new Dictionary<uint, Instrument>();
                TradedOptions.Add(otmOption.InstrumentToken, otmOption);
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Bought ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                //}
            }
            //}
        }
        private decimal CloseTrade(DateTime currentTime, Instrument option, bool buy)
        {
            int qty;
            if (_orderTrios.Count > 0)
            {
                OrderTrio orderTrio = new OrderTrio();

                qty = _tradeQty * Convert.ToInt32(option.LotSize);
                qty = 10;

                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice, //e.ClosePrice,
                   option.KToken, buy, qty, algoIndex, currentTime, Tag: "SM",
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                //orderTrio.TPFlag = false;
                ////atmOption.InstrumentToken = _baseInstrumentToken;
                orderTrio.Option = option;
                // _orderTrios.RemoveAt(0);
                //_orderTrios.Remove(orderTrio);
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Sold", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                return orderTrio.Order.AveragePrice;
            }
            return 0;
        }


        private void TradeTPSL(DateTime currentTime, bool tp, bool sl, Candle pCandle, decimal lastPrice, string instrumentType)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    decimal tradePrice = 0;

                    Instrument tradedOption = orderTrio.Option;

                    //#if market
                    //                    if ( (pCandle != null && (lastPrice < pCandle.LowPrice) && orderTrio.TPFlag)
                    //                        || ((TradedOptions[tradedOption.InstrumentToken].LastPrice < orderTrio.StopLoss) && sl))
                    //                    {
                    //                        tradePrice = CloseTrade(currentTime, orderTrio.Option, false);
                    //                        _pnl += (tradePrice - orderTrio.Order.AveragePrice) * orderTrio.Option.LotSize;
                    //                        _orderTrios.Remove(orderTrio);

                    //                        //OnTradeEntry(orderTrio.Order);
                    //                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Closed", Math.Round(tradePrice, 2)));
                    //                    }
                    //#elif BACKTEST
                    if (orderTrio.Order.TransactionType.ToLower() == "buy")
                    {
                        if ((pCandle != null && (lastPrice < pCandle.LowPrice) && orderTrio.TPFlag)
                          || ((TradedOptions[tradedOption.InstrumentToken].LastPrice < orderTrio.StopLoss) && sl))
                        {
                            tradePrice = CloseTrade(currentTime, orderTrio.Option, false);
                            _pnl += (tradePrice - orderTrio.Order.AveragePrice) * orderTrio.Option.LotSize;
                            _orderTrios.Remove(orderTrio);

                        }
                    }
                    else
                    {
                        if ((pCandle != null && (lastPrice > pCandle.HighPrice) && orderTrio.TPFlag)
                                                  || ((TradedOptions[tradedOption.InstrumentToken].LastPrice > orderTrio.StopLoss) && sl))
                        {
                            tradePrice = CloseTrade(currentTime, orderTrio.Option, true);
                            _pnl += (orderTrio.Order.AveragePrice - tradePrice) * orderTrio.Option.LotSize;
                            _orderTrios.Remove(orderTrio);

                        }
                    }
                    //#endif


                    //                    if ((((_baseInstrumentPrice > orderTrio.TargetProfit) && tp) || ((_baseInstrumentPrice < orderTrio.StopLoss) && sl)))
                    //                    {
                    //                        tradePrice = CloseTrade(currentTime, orderTrio.Option);
                    //                        _pnl += (tradePrice - orderTrio.Order.AveragePrice);
                    //                        _orderTrios.Remove(orderTrio);

                    //#if !BACKTEST
                    //                    OnTradeEntry(orderTrio.Order);
                    //                    OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Closed", Math.Round(tradePrice, 2)));
                    //#endif
                    //                    }
                    //                    else if ((((_baseInstrumentPrice < orderTrio.TargetProfit) && tp) || ((_baseInstrumentPrice > orderTrio.StopLoss) && sl)))
                    //                    {
                    //                        tradePrice = CloseTrade(currentTime, orderTrio.Option);
                    //                        _pnl += (orderTrio.Order.AveragePrice - tradePrice);
                    //                        _orderTrios.Remove(orderTrio);

                    //#if !BACKTEST
                    //                    OnTradeEntry(orderTrio.Order);
                    //                    OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Closed", Math.Round(tradePrice, 2)));
                    //#endif
                    //                    }


                }
            }
        }

        /// <summary>
        /// Load Nifty Stocks
        /// </summary>
        /// <param name="currentTime"></param>
        private void LoadStocksToTrade(DateTime currentTime)
        {
            try
            {
                if (AllStocks == null)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Nifty Stock Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, uint> mappedTokens;
                    SortedList<decimal, Instrument> calls, puts;

                    DataLogic dl = new DataLogic();
                    var allStocks = dl.LoadIndexStocks(_baseInstrumentToken);

                    if (allStocks.Count == 0)
                    {
                        return;
                    }
                    AllStocks ??= new Dictionary<uint, Instrument>();
                    TradedOptions ??= new Dictionary<uint, Instrument>();
                    foreach (var stockItem in allStocks)
                    {
                        AllStocks.TryAdd(stockItem.InstrumentToken, stockItem);

                        //_stockTokenShortRSI.TryAdd(stockItem.InstrumentToken, new RelativeStrengthIndex());
                        //_stockTokenLongRSI.TryAdd(stockItem.InstrumentToken, new RelativeStrengthIndex());

                        //_stockTokenADX.TryAdd(stockItem.InstrumentToken, new AverageDirectionalIndex());
                        //_stockTokenHalfTrend.TryAdd(stockItem.InstrumentToken, new HalfTrend(13, 13));

                        //_stockTokenStochastic.TryAdd(stockItem.InstrumentToken, new StochasticOscillator());

                        //_stockTokenCPR.TryAdd(stockItem.InstrumentToken, null);
                    }

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Stocks Loaded", "LoadStocksToTrade");
                }

            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadStocksToTrade");
                Thread.Sleep(100);
            }
        }
        //private void LoadOptionsToTrade(DateTime currentTime)
        //{
        //    try
        //    {
        //        if (CallOptionsByStrike == null ||
        //        (CallOptionsByStrike.Keys.Last() < _baseInstrumentPrice + _minDistanceFromBInstrument
        //        || CallOptionsByStrike.Keys.First() > _baseInstrumentPrice - _minDistanceFromBInstrument))
        //        {
        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
        //            //Load options asynchronously

        //            Dictionary<uint, uint> mappedTokens;
        //            SortedList<decimal, Instrument> calls, puts;

        //            DataLogic dl = new DataLogic();
        //            _expiryDate ??= dl.GetCurrentWeeklyExpiry(currentTime);
        //            var allOptions = dl.LoadOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out calls, out puts, out mappedTokens);

        //            if (allOptions.Count == 0)
        //            {
        //                return;
        //            }
        //            AllOptions ??= new Dictionary<uint, Instrument>();
        //            foreach (var optionItem in allOptions)
        //            {
        //                AllOptions.TryAdd(optionItem.InstrumentToken, optionItem);
        //            }

        //            if (CallOptionsByStrike == null)
        //            {
        //                CallOptionsByStrike = calls;
        //                PutOptionsByStrike = puts;
        //            }
        //            else
        //            {
        //                foreach (var callItems in calls)
        //                {
        //                    CallOptionsByStrike.TryAdd(callItems.Key, callItems.Value);
        //                }
        //                foreach (var putItems in puts)
        //                {
        //                    PutOptionsByStrike.TryAdd(putItems.Key, putItems.Value);
        //                }
        //            }
        //            if (MappedTokens == null)
        //            {
        //                MappedTokens = mappedTokens;
        //            }
        //            else
        //            {
        //                foreach (var mToken in mappedTokens)
        //                {
        //                    MappedTokens.TryAdd(mToken.Key, mToken.Value);
        //                }
        //            }

        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
        //        Thread.Sleep(100);
        //    }
        //}

        private uint GetKotakToken(uint kiteToken)
        {
            return MappedTokens[kiteToken];
        }
        private void UpdateOptionPrice(Tick tick)
        {
            if (TradedOptions.ContainsKey(tick.InstrumentToken))
            {
                TradedOptions[tick.InstrumentToken].LastPrice = tick.LastPrice;
            }
        }
        private decimal GetATMStrike(decimal bPrice, string instrumentType)
        {
            return instrumentType.ToLower() == "ce" ? Math.Floor(bPrice / 100) * 100 : Math.Ceiling(bPrice / 100) * 100;
        }

        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00))
            {
                CheckSL(currentTime);
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
                    candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpanShort, true); // TODO: USING LOCAL VERSION RIGHT NOW
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
                        candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpanShort, true, candleStartTime); // TODO: USING LOCAL VERSION

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

                    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpanShort.TotalMilliseconds;

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


        
        private void CheckSL(DateTime currentTime)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    decimal oldPrice = orderTrio.Order.AveragePrice;
                    bool buy = orderTrio.Order.TransactionType.ToLower() == "sell";

                    Instrument option = orderTrio.Option;
                    int qty = _tradeQty * Convert.ToInt32(option.LotSize);
                    qty = 10;

                    orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType.ToLower(), option.LastPrice,
                        option.KToken, buy, qty, algoIndex, currentTime, Tag: "SM",
                        broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                    _pnl += buy ? (oldPrice - orderTrio.Order.AveragePrice) * orderTrio.Option.LotSize : (orderTrio.Order.AveragePrice - oldPrice) * orderTrio.Option.LotSize;

                    OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                    //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                    ////Cancel Target profit Order
                    //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                    //httpClient: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));


                    _orderTrios.Remove(orderTrio);
                    i--;
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
        //    DataSet dsPAInputs = dl.LoadAlgoInputs(AlgoIndex.CandleWickScalpingOptions, Convert.ToDateTime("2021-11-30"), Convert.ToDateTime("2021-12-30"));

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
                if (AllStocks != null)
                {
                    foreach (var stock in AllStocks)
                    {
                        if (!SubscriptionTokens.Contains(stock.Key))
                        {
                            SubscriptionTokens.Add(stock.Key);
                            dataUpdated = true;
                        }
                    }
                }
                if (TradedOptions != null)
                {
                    foreach (var stock in TradedOptions)
                    {
                        if (!SubscriptionTokens.Contains(stock.Key))
                        {
                            SubscriptionTokens.Add(stock.Key);
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
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
#endif
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

                //if (_baseInstrumentStartPrice == 0)
                //{
                //    _baseInstrumentStartPrice = _baseInstrumentPrice;
                //}
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
#if market
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
