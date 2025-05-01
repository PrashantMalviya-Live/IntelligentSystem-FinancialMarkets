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
using System.Net.NetworkInformation;
using Google.Protobuf.WellKnownTypes;

namespace Algorithms.Algorithms
{
    public class StopAndReverseOptions : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(StopAndReverseOptions source);
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
        public Dictionary<uint, Instrument> AllOptions { get; set; }
        public SortedList<decimal, Instrument> CallOptionsByStrike { get; set; }
        public SortedList<decimal, Instrument> PutOptionsByStrike { get; set; }

        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public Queue<decimal> _indexValues;
        public Dictionary<uint, Queue<decimal>> _optionValues;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private decimal _minDistanceFromBInstrument = 500;
        private decimal _maxDistanceFromBInstrument = 700;
        //private Instrument _activeFuture;
        private Instrument _activeCall, _activePut;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);

        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private const decimal SCH_UPPER_THRESHOLD = 70;
        private const decimal THRESHOLD = 10;
        private const decimal PROFIT_TARGET = 15;
        private const decimal STOP_LOSS = 15;
        private const decimal VALUES_THRESHOLD = 60;
        //private const decimal SCH_LOWER_THRESHOLD = 30;
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
        private Candle _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.SARScalping;
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

        StochasticOscillator _indexSch;

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

        //private PriceRange _resistancePriceRange;
        //private PriceRange _supportPriceRange;

        //bool _indexSchLoaded = false;
        //bool _indexSchLoading = false;
        //bool _indexSchLoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;

        public StopAndReverseOptions(uint baseInstrumentToken,
            DateTime? expiry, int quantity, decimal targetProfit, decimal stopLoss,
            //OHLC previousDayOHLC, OHLC previousWeekOHLC, decimal previousDayBodyHigh, 
            //decimal previousDayBodyLow, decimal previousSwingHigh, decimal previousSwingLow,
            //decimal previousWeekLow, decimal previousWeekHigh, 
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            //ZConnect.Login();
            //KoConnect.Login();

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            //_candleTimeSpan = candleTimeSpan;

            _baseInstrumentToken = baseInstrumentToken;
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

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _indexValues = new Queue<decimal>(60);
            _optionValues = new Dictionary<uint, Queue<decimal>>();

            //_firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            //TimeCandles = new Dictionary<uint, List<Candle>>();
            //candleManger = new CandleManger(TimeCandles, CandleType.Time);
            //candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
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

                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
#if local
                    if (SubscriptionTokens.Contains(token))
                    {
#endif
                        //if(token == _baseInstrumentToken)
                        if (token != _baseInstrumentToken)
                        {
                            //UpdateIndexValues(tick.LastPrice);
                            //TakeTrade(currentTime, tick.LastPrice);
                            UpdateOptionPrice(tick);
                            if (Math.Abs(_baseInstrumentPrice - _activeCall.Strike) > 100 || Math.Abs(_baseInstrumentPrice - _activeCall.Strike) > 100)
                            {
                                UpdateATMInstrument();
                            }
                            UpdateOptionsValue(token, tick.LastPrice);
                            
                            if (CheckOptionReturn(token))
                            {
                                TakeOptionsTrade(token, currentTime);
                            }
                            //}
                            //else
                            //{
                            //UpdateOptionPrice(tick);
                            TradeTPSL(currentTime, tp: true, sl: false);
                        }
#if local
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

        
        private void UpdateOptionPrice(Tick tick)
        {
            AllOptions[tick.InstrumentToken].LastPrice = tick.LastPrice;
            
        }
        private void UpdateIndexValues(decimal lastPrice)
        {
            _indexValues.Enqueue(lastPrice);
            if (_indexValues.Count > VALUES_THRESHOLD)
            {
                _indexValues.Dequeue();
            }
        }
        private void UpdateOptionsValue(uint token, decimal lastPrice)
        {
            if (!_optionValues.ContainsKey(token))
            {
                Queue<decimal> optionValues = new Queue<decimal>(60);
                _optionValues.TryAdd(token, optionValues);
            }
            _optionValues[token].Enqueue(lastPrice);
            if (_optionValues[token].Count > VALUES_THRESHOLD)
            {
                _optionValues[token].Dequeue();
            }
        }
        private decimal GetATMStrike(decimal bPrice, string instrumentType)
        {
            return instrumentType.ToLower() == "ce" ? Math.Ceiling(bPrice / 100) * 100 : Math.Floor(bPrice / 100) * 100;
        }

        private void UpdateATMInstrument()
        {
            decimal ceATMStrike = Math.Ceiling(_baseInstrumentPrice / 100) * 100;
            decimal peATMStrike = Math.Floor(_baseInstrumentPrice / 100) * 100;
            _activeCall = CallOptionsByStrike[ceATMStrike];
            _activePut = PutOptionsByStrike[peATMStrike];
        }


        bool CheckReturn(decimal lastPrice, out string instrumentType)
        {
            instrumentType = String.Empty;
            if (!_orderTrios.Any(x => x.Option.InstrumentType.ToLower() == "pe"))
            {
                for (int i = _indexValues.Count - 1; i >= 0; i--)
                {
                    if (lastPrice - _indexValues.ElementAt(i) > THRESHOLD)
                    {
                        decimal referenceValue = _indexValues.ElementAt(i);
                        for (int j = i; j >= 0; j--)
                        {
                            if (_indexValues.ElementAt(j) - referenceValue > THRESHOLD)
                            {
                                //instrumentType = "ce";
                                instrumentType = "pe";
                                _indexValues.Clear();
                                return true;
                            }
                        }
                    }
                }
            }
            if (!_orderTrios.Any(x => x.Option.InstrumentType.ToLower() == "ce"))
            {
                for (int i = _indexValues.Count - 1; i >= 0; i--)
                {
                    if (_indexValues.ElementAt(i) - lastPrice > THRESHOLD)
                    {
                        decimal referenceValue = _indexValues.ElementAt(i);
                        for (int j = i; j >= 0; j--)
                        {
                            if (referenceValue - _indexValues.ElementAt(j) > THRESHOLD)
                            {
                                //instrumentType = "pe";
                                _indexValues.Clear();
                                instrumentType = "ce";
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        bool CheckOptionReturn(uint token)
        {
            Instrument option = AllOptions[token];

            if (option.InstrumentToken == _activeCall.InstrumentToken || option.InstrumentToken == _activePut.InstrumentToken)
            {
                if (!_orderTrios.Any(x => x.Option.InstrumentType.ToLower() == option.InstrumentType.ToLower()))
                {
                    for (int i = _optionValues[token].Count - 1; i >= 0; i--)
                    {
                        if (option.LastPrice - _optionValues[token].ElementAt(i) > THRESHOLD)
                        {
                            decimal referenceValue = _optionValues[token].ElementAt(i);
                            for (int j = i; j >= 0; j--)
                            {
                                if (_optionValues[token].ElementAt(j) - referenceValue > THRESHOLD)
                                {
                                    _optionValues[token].Clear();
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        private void TakeTrade(string instrumentType, DateTime currentTime)
        {
            int qty = _tradeQty;
            if (_orderTrios.Count > 0)
            {
//                if (_orderTrios[0].Option.InstrumentType.Trim(' ').ToLower() != instrumentType.Trim(' ').ToLower())
//                {
////#if market
////                    CloseTrade(currentTime, _orderTrios[0].Option);
////#elif local
////                    CloseTrade(currentTime, AllOptions[_orderTrios[0].Option.InstrumentToken]);
////#endif
//                }
//                else
//                {
//                    qty = 0;
//                }
                if(_orderTrios.Any(x=>x.Option.InstrumentType.ToLower() == instrumentType.ToLower()))
                {
                    qty = 0;
                }
            }

            if (qty > 0)
            {
                OrderTrio orderTrio = new OrderTrio();
                decimal atmStrike = GetATMStrike(_baseInstrumentPrice, instrumentType);
                Instrument atmOption = (instrumentType == "ce") ? CallOptionsByStrike[atmStrike] : PutOptionsByStrike[atmStrike];

#if BACKTEST
                atmOption = AllOptions[atmOption.InstrumentToken];
                if (atmOption.LastPrice == 0)
                {
                    return;
                }
#endif

                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, atmOption.TradingSymbol, atmOption.InstrumentType.ToLower(), atmOption.LastPrice, //e.ClosePrice,
                   atmOption.KToken, false, _tradeQty * Convert.ToInt32(atmOption.LotSize), algoIndex, currentTime, Tag: "Algo4",
                   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());



                orderTrio.StopLoss = orderTrio.Order.AveragePrice + STOP_LOSS;
                orderTrio.TargetProfit = orderTrio.Order.AveragePrice - PROFIT_TARGET;
                atmOption.LastPrice = orderTrio.Order.AveragePrice;
                orderTrio.Option = atmOption;
                _orderTrios.Add(orderTrio);

#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Bought ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
            }
        }
        private void TakeOptionsTrade(uint token, DateTime currentTime)
        {
            int qty = _tradeQty;
            OrderTrio orderTrio = new OrderTrio();
            //decimal atmStrike = GetATMStrike(_baseInstrumentPrice, instrumentType);
            //Instrument atmOption = (instrumentType == "ce") ? CallOptionsByStrike[atmStrike] : PutOptionsByStrike[atmStrike];

            Instrument atmOption = AllOptions[token];
#if BACKTEST
            if (atmOption.LastPrice == 0)
            {
                return;
            }
#endif

            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, atmOption.TradingSymbol, atmOption.InstrumentType.ToLower(), atmOption.LastPrice, //e.ClosePrice,
               atmOption.KToken, false, _tradeQty * Convert.ToInt32(atmOption.LotSize), algoIndex, currentTime, Tag: "Algo4",
               broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());



            orderTrio.StopLoss = orderTrio.Order.AveragePrice + STOP_LOSS;
            orderTrio.TargetProfit = orderTrio.Order.AveragePrice - PROFIT_TARGET;
            atmOption.LastPrice = orderTrio.Order.AveragePrice;
            orderTrio.Option = atmOption;
            _orderTrios.Add(orderTrio);

#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Bought ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
        }
        private decimal CloseTrade(DateTime currentTime, Instrument option, OrderTrio orderTrio)
        {
            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
               option.KToken, true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Tag: "Algo4",
               broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * -1;
            
#if !BACKTEST
                OnTradeEntry(order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Sold", Math.Round(order.AveragePrice, 2)));
#endif
            return order.AveragePrice;
        }

        private void TakeTrade(DateTime currentTime, decimal lastPrice)
        {
            string instrumentType;
            if(CheckReturn(lastPrice, out instrumentType))
            {
                TakeTrade(instrumentType, currentTime);
            }
        }
       
        private void TradeTPSL(DateTime currentTime, bool tp, bool sl)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    decimal tradePrice = 0;

                    Instrument tradedOption = orderTrio.Option;

                    if ((AllOptions[tradedOption.InstrumentToken].LastPrice < orderTrio.TargetProfit )
                        || (AllOptions[tradedOption.InstrumentToken].LastPrice > orderTrio.StopLoss))
                    {
                        tradePrice = CloseTrade(currentTime, AllOptions[orderTrio.Option.InstrumentToken], orderTrio);
                        _orderTrios.Remove(orderTrio);
                        i--;
#if !BACKTEST
                    //OnTradeEntry(orderTrio.Order);
                    //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Closed", Math.Round(tradePrice, 2)));
#endif
                    }
                }
            }
        }


        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00))
            {
                if (_orderTrios.Count > 0)
                {
                    for (int i = 0; i < _orderTrios.Count; i++)
                    {
                        var orderTrio = _orderTrios[i];
                        CloseTrade(currentTime, AllOptions[orderTrio.Option.InstrumentToken], orderTrio);
                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
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

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (CallOptionsByStrike == null ||
                (CallOptionsByStrike.Keys.Last() < _baseInstrumentPrice + _minDistanceFromBInstrument
                || CallOptionsByStrike.Keys.First() > _baseInstrumentPrice - _minDistanceFromBInstrument))
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, uint> mappedTokens;
                    SortedList<decimal, Instrument> calls, puts;

                    DataLogic dl = new DataLogic();
                    _expiryDate ??= dl.GetCurrentWeeklyExpiry(currentTime, _baseInstrumentToken);
                    var allOptions = dl.LoadOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out calls, out puts, out mappedTokens);

                    if (allOptions.Count == 0)
                    {
                        return;
                    }
                    AllOptions ??= new Dictionary<uint, Instrument>();
                    foreach (var optionItem in allOptions)
                    {
                        AllOptions.TryAdd(optionItem.InstrumentToken, optionItem);
                    }

                    if (CallOptionsByStrike == null)
                    {
                        CallOptionsByStrike = calls;
                        PutOptionsByStrike = puts;
                    }
                    else
                    {
                        foreach (var callItems in calls)
                        {
                            CallOptionsByStrike.TryAdd(callItems.Key, callItems.Value);
                        }
                        foreach (var putItems in puts)
                        {
                            PutOptionsByStrike.TryAdd(putItems.Key, putItems.Value);
                        }
                    }
                    _activeCall = CallOptionsByStrike[GetATMStrike(_baseInstrumentPrice, "ce")];
                    _activePut = PutOptionsByStrike[GetATMStrike(_baseInstrumentPrice, "pe")];
                    //if (MappedTokens == null)
                    //{
                    //    MappedTokens = mappedTokens;
                    //}
                    //else
                    //{
                    //    foreach (var mToken in mappedTokens)
                    //    {
                    //        MappedTokens.TryAdd(mToken.Key, mToken.Value);
                    //    }
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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (AllOptions != null)
                {
                    foreach (var option in AllOptions)
                    {
                        if (!SubscriptionTokens.Contains(option.Key))
                        {
                            SubscriptionTokens.Add(option.Key);
                            dataUpdated = true;
                        }
                    }
                    if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                    {
                        SubscriptionTokens.Add(_baseInstrumentToken);
                    }
                    if (dataUpdated)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                        Task task = Task.Run(() => OnOptionUniverseChange(this));
                    }
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
    }
}
