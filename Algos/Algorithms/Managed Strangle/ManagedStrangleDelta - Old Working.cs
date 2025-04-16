using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using GlobalCore;
using KiteConnect;
using BrokerConnectWrapper;
//using Pub_Sub;
//using MarketDataTest;
using System.Data;
using System.Timers;
using System.Threading;
using ZMQFacade;
using Algorithms.Utils;
using System.Net.Http;

namespace Algorithms.Algorithms
{
    public class ManagedStrangleDelta : IZMQ //, IObserver<Tick[]>
    {
        private Dictionary<uint, Instrument> _optionUniverse;
        private SortedDictionary<decimal, Instrument> _callUniverse;
        private SortedDictionary<decimal, Instrument> _putUniverse;

        //StrangleChain _activeStrangle;
        //InstrumentLinkedList _ceInstrumentLinkedList, _peInstrumentLinkedList;
        //private List<Instrument> _optionUniverse;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ManagedStrangleDelta source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        //public List<Order> _activeCallOrders;
        //public List<Order> _activePutOrders;

        public Order _activeCallOrder;
        public Order _activePutOrder;
        public Instrument _activeCall;
        public Instrument _activePut;

        private bool _callLoaded = false;
        private bool _putLoaded = false;

        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;
        private Dictionary<uint, Instrument> _instrumentDict = new Dictionary<uint,Instrument>();
        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;
        int _algoInstance;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        public List<uint> SubscriptionTokens { get; set; }
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public Dictionary<uint, uint> MappedTokens { get; set; }
        int _quantity;
        decimal _targetProfit;
        bool _forceReload = false;
        decimal _stopLoss;
        double _entryDelta;
        double _reEntryDeltaAfterLoss;
        double _maxDelta;
        double _minDelta;
        decimal _maxLossPerTrade;
        decimal _lowerNoTradeZone1;
        decimal _lowerNoTradeZone2;
        decimal _upperNoTradeZone1;
        decimal _upperNoTradeZone2;
        public const AlgoIndex _algoIndex = AlgoIndex.DeltaStrangleWithLevels;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        DateTime? _expiryDate;
        bool _stopTrade;
        int _initialQty;
        int _maxQty;
        int _stepQty;
        int _tradedQty;
        TimeSpan _candleTimeSpan;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        IHttpClientFactory _httpClientFactory;
        string _userId;
        //public const AlgoIndex _algoIndex = AlgoIndex.DeltaStrangleWithLevels;
        decimal _livePnl = 0, _netPnl = 0;
        private Object tradeLock = new Object();
        //public DirectionalWithStraddleShift(DateTime endTime, TimeSpan candleTimeSpan,
        //   uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength,
        //   decimal targetProfit, decimal stopLoss, bool straddleShift, decimal thresholdRatio = 1.67m,
        //   int algoInstance = 0, bool positionSizing = false,
        //   decimal maxLossPerTrade = 0)
        public ManagedStrangleDelta(uint baseInstrumentToken, DateTime? expiry, int initialQty, int stepQty, int maxQty, decimal targetProfit, decimal stopLoss, decimal entryDelta,
            decimal reEntryDeltaAfterLoss, decimal maxDelta, decimal minDelta, decimal lowerNoTradeZone1, decimal lowerNoTradeZone2, decimal upperNoTradeZone1, decimal upperNoTradeZone2, 
            AlgoIndex _algoIndex, TimeSpan candleTimeSpan, IHttpClientFactory httpClientFactory = null, int algoInstance = 0,
            string userid="")

        {
            //LoadActiveStrangles();
            _userId = userid;
            _expiryDate = expiry;
            _maxDistanceFromBInstrument = 400;
            _minDistanceFromBInstrument = 0;

            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            SubscriptionTokens = new List<uint>();
            _quantity = initialQty;
            _candleTimeSpan = candleTimeSpan;
            _initialQty = initialQty * 25;
            _stepQty = stepQty * 25;
            _maxQty = maxQty * 25;
            _maxDelta = (double) maxDelta;
            _entryDelta = (double) entryDelta;
            _reEntryDeltaAfterLoss = (double)reEntryDeltaAfterLoss;
            _minDelta = (double) minDelta;
            _httpClientFactory = httpClientFactory;
            _lowerNoTradeZone1 = lowerNoTradeZone1;
            _lowerNoTradeZone2 = lowerNoTradeZone2;
            _upperNoTradeZone1 = upperNoTradeZone1;
            _upperNoTradeZone2 = upperNoTradeZone2;

            _maxLossPerTrade = stopLoss;

            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(_algoIndex, baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now),
                expiry.GetValueOrDefault(DateTime.Now), _initialQty, _maxQty, _stepQty,(decimal) _maxDelta, 0, (decimal) _minDelta, 0, 0,
                0, 0, 0, 0, lowerNoTradeZone1, lowerNoTradeZone2, upperNoTradeZone1, upperNoTradeZone2, _targetProfit, _stopLoss,
                reEntryDeltaAfterLoss, (decimal)candleTimeSpan.TotalMinutes, positionSizing: false, maxLossPerTrade: _maxLossPerTrade);

            //_optionUniverse = new List<Instrument>();
            //_ceInstrumentLinkedList = new InstrumentLinkedList(null)
            //{
            //    InstrumentType = "ce",
            //    UpperDelta = _maxDelta,
            //    LowerDelta = _minDelta,
            //    BaseInstrumentToken = _baseInstrumentToken,
            //    Current = null,
            //    CurrentInstrumentIndex = 0,
            //    StopLossPoints = (double)_stopLoss
            //};
            //_peInstrumentLinkedList = new InstrumentLinkedList(null)
            //{
            //    InstrumentType = "pe",
            //    UpperDelta = _maxDelta,
            //    LowerDelta = _minDelta,
            //    BaseInstrumentToken = _baseInstrumentToken,
            //    Current = null,
            //    CurrentInstrumentIndex = 0,
            //    StopLossPoints = (double)_stopLoss
            //};

            ZConnect.Login();

            //User user = new User();
            //_userId = "ARIJIT80";
            KoConnect.Login(userId: _userId);
            
            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();
        }
        public IDisposable UnsubscriptionToken;

        //Instrument _bInst;

        private void ReviewStrangle(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
               tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                //Start at 09:20
               // StartTrade(currentTime);

                uint token = tick.InstrumentToken;

                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                   // MoveClosure(currentTime, "ce");
                    LoadOptionsToTrade(currentTime, _forceReload);
                    UpdateInstrumentSubscription(currentTime);
                    if (tick.LastTradeTime.HasValue && tick.InstrumentToken != _baseInstrumentToken)
                    {
                        MonitorCandles(tick, currentTime);

                        UpdateLastTradingPrice(tick);

                        //Take inital trade
                        if (_activeCallOrder == null && _activePutOrder == null)
                        {
                            _forceReload = TakeInitialTrade(currentTime);
                        }
                    }
                }

                //Close all Positions at 3:10
                TriggerEODPositionClose(currentTime);

                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
            }
        }

        private void LoadOptionsToTrade(DateTime currentTime, bool forceReload)
        {
            try
            {
                if (_optionUniverse == null ||
                (_callUniverse.Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument -200
                || _callUniverse.Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument)
                   || (_putUniverse.Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                   || _putUniverse.Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument +200)
                   || forceReload
                    )
                {
                    forceReload = false;
                    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, uint> mTokens;
                    SortedDictionary<decimal, Instrument> ceList, peList;
                    DataLogic dl = new DataLogic();
                    Dictionary<uint, Instrument> allOptions =  dl.LoadOptionsChain(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, 
                        1000, out mTokens, out ceList, out peList);
                    MappedTokens ??= new Dictionary<uint, uint>();
                    if (_optionUniverse == null)
                    {
                        _optionUniverse = allOptions;
                        MappedTokens = mTokens;
                        _callUniverse = ceList;
                        _putUniverse = peList;
                    }
                    else
                    {
                        foreach (var item in allOptions)
                        {
                            _optionUniverse.TryAdd(item.Key, item.Value);
                        }
                        foreach (var item in ceList)
                        {
                            _callUniverse.TryAdd(item.Key, item.Value);
                        }
                        foreach (var item in peList)
                        {
                            _putUniverse.TryAdd(item.Key, item.Value);
                        }
                        foreach (var item in mTokens)
                        {
                            MappedTokens.TryAdd(item.Key, item.Value);
                        }
                    }
                    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
            }
        }
        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (_optionUniverse != null)
                {
                    foreach (var option in _optionUniverse)
                    {
                        if (!SubscriptionTokens.Contains(option.Key))
                        {
                            SubscriptionTokens.Add(option.Key);
                            dataUpdated = true;
                        }
                    }
                    if (dataUpdated)
                    {
                        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                        Task task = Task.Run(() => OnOptionUniverseChange(this));
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

       
       

        private bool TakeInitialTrade(DateTime currentTime)
        {
            Instrument call = null, put = null;
            Instrument prevCall = null, prevPut = null;
            double? prevDelta = null;
            double currentDelta = 0;

            foreach (var option in _optionUniverse)
            {
                if (option.Value.LastPrice == 0)
                {
                    return false;
                }
            }
            foreach (var callref in _callUniverse)
            {
                call = callref.Value;
                if (call.LastPrice != 0)
                {
                    currentDelta = call.UpdateDelta((double)call.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);
                    if (prevDelta != null)
                    {
                        if (currentDelta > _entryDelta && prevDelta <= _entryDelta)
                        {
                            _activeCall = prevCall;
                            break;
                        }
                        else if (currentDelta < _entryDelta && prevDelta >= _entryDelta)
                        {
                            _activeCall = call;
                            break;
                        }
                    }
                }
                prevCall = call;
                prevDelta = currentDelta;
            }

            if (_activeCall == null)
            {
                return true;
            }
            foreach (var putref in _putUniverse)
            {
                put = putref.Value;
                if (put.LastPrice != 0 && prevPut != null)
                {
                    if ((put.LastPrice > call.LastPrice && prevPut.LastPrice <= call.LastPrice)
                        || (put.LastPrice < call.LastPrice && prevPut.LastPrice >= call.LastPrice))
                    {
                        if (Math.Abs(call.LastPrice - put.LastPrice) > Math.Abs(put.LastPrice - prevPut.LastPrice))
                        {
                            _activePut = prevPut;
                        }
                        else
                        {
                            _activePut = put;
                        }
                        break;
                    }
                }
                prevPut = put;
            }

            if (_activePut == null)
            {
                return true;
            }
            Order order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
                          _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                          tickTime: currentTime, httpClient: _httpClientFactory == null? null: _httpClientFactory.CreateClient());
            order.InstrumentToken = _activeCall.InstrumentToken;
            _activeCallOrder = order;

            order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
                          _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                          tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
            order.InstrumentToken = _activePut.InstrumentToken;
            _activePutOrder = order;

            _tradedQty = _initialQty;
            OnTradeEntry(_activeCallOrder);
            OnTradeEntry(_activePutOrder);

            return false;
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
                        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
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
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            if (_activeCallOrder == null)
                return;
            //Step1: if active options are null, take the first trade
            //Step 2: check value between both side.and move ahead to next strike
            //Step 3: if delta threshold breached, the risk off, else risk on
            if ( _activeCallOrder.InstrumentToken == e.InstrumentToken)
            {
                _callLoaded = true;
            }
            if (_activePutOrder.InstrumentToken == e.InstrumentToken)
            {
                _putLoaded = true;
            }
            if(_putLoaded && _callLoaded)
            {
                _callLoaded = false;
                _putLoaded = false;
                //_livePnl = _activeCall.LastPrice + _activePut.LastPrice - _activeCallOrder
                if (_activeCall.LastPrice > _activePut.LastPrice)
                {
                    double delta = _activeCall.UpdateDelta((double)_activeCall.LastPrice, 0.1, e.CloseTime, (double)_baseInstrumentPrice);
                    double pdelta = _activePut.UpdateDelta((double)_activePut.LastPrice, 0.1, e.CloseTime, (double)_baseInstrumentPrice);
                    _maxDelta =  Math.Min(_maxDelta, Math.Max(delta, pdelta * -1) + 0.04);
                    if (delta > _maxDelta)
                    {
                        //Close everything, and restart will happen automatically
                        //CloseStrangle(e.CloseTime);
                        //ExpandStrangle(e.CloseTime, "ce");
                        MoveFurther(e.CloseTime, "ce");
                        //return;
                    }
                    
                    if(pdelta * -1 < _minDelta)
                    {
                        MoveClosure(e.CloseTime, "pe");
                    }

                    //Instrument put = _putUniverse.Select(x => x.Value).Where(x => x.LastPrice > _activeCall.LastPrice).FirstOrDefault();
                    //if (put != null && _putUniverse.ContainsKey(put.Strike - 100))
                    //{
                    //    Instrument previousPut = _putUniverse[put.Strike - 100];
                    //    bool mustChange = false;
                    //    if (previousPut.Strike != _activePut.Strike)
                    //    {
                    //        mustChange = true;
                    //    }
                    //    Instrument newPut;
                    //    Order order = CompareStrangleandBalance(_activeCall.LastPrice, _activePut, put, e.CloseTime, mustChange, previousPut, out newPut);
                    //    if (order != null)
                    //    {
                    //        _activePutOrder = order;
                    //        _activePut = newPut;
                    //        _activePutOrder.InstrumentToken = _activePut.InstrumentToken;
                    //    }
                    //}
                    //else
                    //{
                    //    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, e.CloseTime, "Puts finished in the log", "Candle Closure");
                    //}
                }
                else
                {
                    double delta = _activePut.UpdateDelta((double)_activePut.LastPrice, 0.1, e.CloseTime, (double)_baseInstrumentPrice);
                    double cdelta = _activeCall.UpdateDelta((double)_activeCall.LastPrice, 0.1, e.CloseTime, (double)_baseInstrumentPrice);
                    _maxDelta = Math.Min(_maxDelta, Math.Max(delta * -1, cdelta) + 0.04);
                    if (delta * -1 > _maxDelta)
                    {
                        //Close everything, and restart will happen automatically
                        //CloseStrangle(e.CloseTime);
                        //ExpandStrangle(e.CloseTime, "pe");
                        MoveFurther(e.CloseTime, "pe");
                        //return;
                    }
                    
                    if (cdelta < _minDelta)
                    {
                        MoveClosure(e.CloseTime, "ce");
                    }


                    //Instrument call = _callUniverse.Select(x => x.Value).Where(x => x.LastPrice > _activePut.LastPrice).LastOrDefault();
                    //if (call != null && _callUniverse.ContainsKey(call.Strike + 100))
                    //{
                    //    Instrument previousCall = _callUniverse[call.Strike + 100];
                    //    bool mustChange = false;
                    //    if(previousCall.Strike != _activeCall.Strike)
                    //    {
                    //        mustChange = true;
                    //    }
                    //    Instrument newCall;
                    //    Order order = CompareStrangleandBalance(_activePut.LastPrice, _activeCall, call, e.CloseTime, mustChange, previousCall, out newCall);
                    //    if (order != null)
                    //    {
                    //        _activeCallOrder = order;
                    //        _activeCall = newCall;
                    //        _activeCallOrder.InstrumentToken = _activeCall.InstrumentToken;
                    //    }
                    //}
                    //else
                    //{
                    //    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, e.CloseTime, "Calls finished in the log", "Candle Closure");
                    //}
                }
            }
        }

        private void MoveClosure(DateTime currentTime, string instrumentType)
        {
            Instrument call = null, put = null;
            Instrument prevCall = null, prevPut = null;
            double? prevDelta = null;
            double currentDelta = 0;

            if (instrumentType.ToLower().Trim() == "pe")
            {
                Instrument newPut = null;
                foreach (var putref in _putUniverse)
                {
                    put = putref.Value;
                    if (put.LastPrice != 0)
                    {
                        currentDelta = put.UpdateDelta((double)put.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);
                        if (prevDelta != null)
                        {
                            if (currentDelta * -1 > _entryDelta && prevDelta * -1 <= _entryDelta)
                            {
                                newPut = prevPut;
                                break;
                            }
                            else if (currentDelta * -1 < _entryDelta * -1 && prevDelta * -1 >= _entryDelta)
                            {
                                newPut = put;
                                break;
                            }
                        }
                    }
                    prevPut = put;
                    prevDelta = currentDelta;
                }

                if (newPut == null || newPut.Strike == _activePut.Strike)
                {
                    return;
                }


                Order order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
                    _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
                    tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                OnTradeExit(order);

                _activePut = newPut;

                order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
                              _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                              tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                order.InstrumentToken = _activePut.InstrumentToken;
                _activePutOrder = order;

                OnTradeEntry(_activePutOrder);
            }
            else if (instrumentType.ToLower().Trim() == "ce")
            {
                Instrument newCall = null;
                foreach (var callref in _callUniverse)
                {
                    call = callref.Value;
                    if (call.LastPrice != 0)
                    {
                        currentDelta = call.UpdateDelta((double)call.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);
                        if (prevDelta != null)
                        {
                            if (currentDelta > _entryDelta && prevDelta <= _entryDelta)
                            {
                                newCall = prevCall;
                                break;
                            }
                            else if (currentDelta < _entryDelta && prevDelta >= _entryDelta)
                            {
                                newCall = call;
                                break;
                            }
                        }
                    }
                    prevCall = call;
                    prevDelta = currentDelta;
                }

                if (newCall == null || newCall.Strike == _activeCall.Strike)
                {
                    return;
                }

                Order order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
                    _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
                    tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                OnTradeExit(order);

                _activeCall = newCall;

                order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
                            _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                            tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                order.InstrumentToken = _activeCall.InstrumentToken;
                _activeCallOrder = order;

                OnTradeEntry(_activeCallOrder);
            }
            return;
        }
        private Order CompareStrangleandBalance(decimal referencePrice, 
            Instrument currentOption, Instrument proposedOption, DateTime currentTime, bool mustChange, Instrument mustChangeOption, out Instrument newOption)
        {
            Order order = null;
            newOption = null;
            if (referencePrice == proposedOption.LastPrice
                          || (( Math.Abs(referencePrice - currentOption.LastPrice) / Math.Abs(proposedOption.LastPrice - referencePrice)) > 4))
            {
                order = MarketOrders.PlaceOrder(_algoInstance, currentOption.TradingSymbol, currentOption.InstrumentType,
              currentOption.LastPrice, MappedTokens[currentOption.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
              tickTime: currentTime, httpClient: _httpClientFactory.CreateClient());
                OnTradeExit(order);

                order = MarketOrders.PlaceOrder(_algoInstance, proposedOption.TradingSymbol, proposedOption.InstrumentType,
              proposedOption.LastPrice, MappedTokens[proposedOption.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
              tickTime: currentTime, httpClient: _httpClientFactory.CreateClient());
                
                //_activePutOrder = order;
                currentOption = proposedOption;
                OnTradeEntry(order);
            }
            else if(mustChange)
            {
                order = MarketOrders.PlaceOrder(_algoInstance, currentOption.TradingSymbol, currentOption.InstrumentType,
              currentOption.LastPrice, MappedTokens[currentOption.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
              tickTime: currentTime, httpClient: _httpClientFactory.CreateClient());
                OnTradeExit(order);

                order = MarketOrders.PlaceOrder(_algoInstance, mustChangeOption.TradingSymbol, mustChangeOption.InstrumentType,
              mustChangeOption.LastPrice, MappedTokens[mustChangeOption.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
              tickTime: currentTime, httpClient: _httpClientFactory.CreateClient());

                //_activePutOrder = order;
                currentOption = mustChangeOption;
                OnTradeEntry(order);
            }
            newOption = currentOption;
            return order;
        }
        private void CloseStrangle(DateTime currentTime)
        {
            if (_activePutOrder != null)
            {
                Order order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
            _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
            tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                OnTradeExit(order);
                _activePut = null;
                _activePutOrder = null;
            }
            if (_activeCallOrder != null)
            {
                Order order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
            _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
            tickTime: currentTime, httpClient: _httpClientFactory == null? null: _httpClientFactory.CreateClient());

                OnTradeExit(order);
                _activeCallOrder = null;
                _activeCall = null;
            }
        }
        private void MoveFurther(DateTime currentTime, string instrumentType)
        {
            Instrument call = null, put = null;
            Instrument prevCall = null, prevPut = null;
            double? prevDelta = null;
            double currentDelta = 0;

            if (instrumentType.ToLower().Trim() == "pe")
            {
                Instrument newPut = null;
                foreach (var putref in _putUniverse)
                {
                    put = putref.Value;
                    if (put.LastPrice != 0)
                    {
                        currentDelta = put.UpdateDelta((double)put.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);
                        if (prevDelta != null)
                        {
                            if (currentDelta * -1 > _maxDelta && prevDelta * -1 <= _maxDelta)
                            {
                                newPut = prevPut;
                                break;
                            }
                            else if (currentDelta * -1 < _maxDelta && prevDelta * -1>= _maxDelta)
                            {
                                newPut = put;
                                break;
                            }
                        }
                    }
                    prevPut = put;
                    prevDelta = currentDelta;
                }

                if (newPut == null || newPut.Strike == _activePut.Strike)
                {
                    return;
                }

                Order order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
                    _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
                    tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                OnTradeExit(order);

                _activePut = newPut;

                order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
                              _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                              tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                order.InstrumentToken = _activePut.InstrumentToken;
                _activePutOrder = order;

                OnTradeEntry(_activePutOrder);
            }
            else if (instrumentType.ToLower().Trim() == "ce")
            {
                Instrument newCall = null;
                foreach (var callref in _callUniverse)
                {
                    call = callref.Value;
                    if (call.LastPrice != 0)
                    {
                        currentDelta = call.UpdateDelta((double)call.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);
                        if (prevDelta != null)
                        {
                            if (currentDelta > _maxDelta && prevDelta <= _maxDelta)
                            {
                                newCall = prevCall;
                                break;
                            }
                            else if (currentDelta < _maxDelta && prevDelta >= _maxDelta)
                            {
                                newCall = call;
                                break;
                            }
                        }
                    }
                    prevCall = call;
                    prevDelta = currentDelta;
                }

                if (newCall == null || newCall.Strike == _activeCall.Strike)
                {
                    return;
                }

                Order order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
                    _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], true, _initialQty, algoIndex: _algoIndex,
                    tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                OnTradeExit(order);

                _activeCall = newCall;

                order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
                            _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                            tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                order.InstrumentToken = _activeCall.InstrumentToken;
                _activeCallOrder = order;

                OnTradeEntry(_activeCallOrder);
            }
            return;
        }
        private void ExpandStrangle(DateTime currentTime, string instrumentType)
        {
            if (instrumentType.ToLower().Trim() == "pe")
            {
                Instrument put = _putUniverse.Select(x => x.Value).Where(x => x.LastPrice < _activeCall.LastPrice).FirstOrDefault();
                if (put != null && _putUniverse.ContainsKey(put.Strike + 100))
                {
                    Instrument previousPut = _putUniverse[put.Strike + 100];
                    bool mustChange = false;
                    if (previousPut.Strike != _activePut.Strike)
                    {
                        mustChange = true;
                    }
                    Instrument newPut;

                    Order order = CompareStrangleandBalance(_activeCall.LastPrice, _activePut, put, currentTime, mustChange, previousPut, out newPut);
                    if (order != null)
                    {
                        _activePutOrder = order;
                        _activePut = newPut;
                        _activePutOrder.InstrumentToken = _activePut.InstrumentToken;
                    }
                }
                else
                {
                    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, "Puts finished in the log", "Candle Closure");
                }
            }
            else if (instrumentType.ToLower().Trim() == "ce")
            {
                Instrument call = _callUniverse.Select(x => x.Value).Where(x => x.LastPrice < _activePut.LastPrice).LastOrDefault();
                if (call != null && _callUniverse.ContainsKey(call.Strike - 100))
                {
                    Instrument previousCall = _callUniverse[call.Strike - 100];
                    bool mustChange = false;
                    if (previousCall.Strike != _activeCall.Strike)
                    {
                        mustChange = true;
                    }
                    Instrument newCall;
                    Order order = CompareStrangleandBalance(_activePut.LastPrice, _activeCall, call, currentTime, mustChange, previousCall, out newCall);
                    if (order != null)
                    {
                        _activeCallOrder = order;
                        _activeCall = newCall;
                        _activeCallOrder.InstrumentToken = _activeCall.InstrumentToken;
                    }
                }
                else
                {
                    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, "Calls finished in the log", "Candle Closure");
                }
            }
        }

        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 19, 00))
            {
                CloseStrangle(currentTime);
                //LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime,
                //        "Completed", "TriggerEODPositionClose");

                _stopTrade = true;
            }
        }
        private void StartTrade(DateTime currentTime)
        {
            if (_stopTrade && currentTime.TimeOfDay >= new TimeSpan(09, 20, 00) && currentTime.TimeOfDay <= new TimeSpan(14, 40, 00))
            {
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime,
                        String.Format("Trade Started for {0}", currentTime.Date), "Start Date");

                //_optionUniverse = null;
                //_callUniverse = null;
                //_putUniverse = null;
                //_expiryDate = GetNextExpiry(currentTime);
                _stopTrade = false;
            }
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
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckCandleStartTime");
                Thread.Sleep(100);
                Environment.Exit(0);
                lastEndTime = DateTime.Now;
                return null;
            }
        }
        public void OnNext(Tick tick)
        {
            try
            {
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    //StartTrade(tick.Timestamp.Value);
                    return;
                }
                ReviewStrangle(tick);
                return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                return;
            }
        }
        private void UpdateLastTradingPrice(Tick tick)
        {
            if (_optionUniverse.ContainsKey(tick.InstrumentToken))
            {
                _optionUniverse[tick.InstrumentToken].LastPrice = tick.LastPrice;
            }
        }

        private bool TakeInitialTrade(DateTime currentTime, bool callTrade = true, bool putTrade = true)
        {
            Instrument call = null, put = null;
            Instrument prevCall = null, prevPut = null;
            double? prevDelta = null;
            double currentDelta = 0;

            foreach (var option in _optionUniverse)
            {
                if (option.Value.LastPrice == 0)
                {
                    return false;
                }
            }
            if (callTrade)
            {
                foreach (var callref in _callUniverse)
                {
                    call = callref.Value;
                    if (call.LastPrice != 0)
                    {
                        currentDelta = call.UpdateDelta((double)call.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);
                        if (prevDelta != null)
                        {
                            if (currentDelta > _entryDelta && prevDelta <= _entryDelta)
                            {
                                _activeCall = prevCall;
                                break;
                            }
                            else if (currentDelta < _entryDelta && prevDelta >= _entryDelta)
                            {
                                _activeCall = call;
                                break;
                            }
                        }
                    }
                    prevCall = call;
                    prevDelta = currentDelta;
                }

                if (_activeCall == null)
                {
                    return true;
                }
            }
            if (putTrade)
            {
                foreach (var putref in _putUniverse)
                {
                    put = putref.Value;
                    if (put.LastPrice != 0 && prevPut != null)
                    {
                        if ((put.LastPrice > put.LastPrice && prevPut.LastPrice <= put.LastPrice)
                            || (put.LastPrice < put.LastPrice && prevPut.LastPrice >= put.LastPrice))
                        {
                            if (Math.Abs(put.LastPrice - put.LastPrice) > Math.Abs(put.LastPrice - prevPut.LastPrice))
                            {
                                _activePut = prevPut;
                            }
                            else
                            {
                                _activePut = put;
                            }
                            break;
                        }
                    }
                    prevPut = put;
                }

                if (_activePut == null)
                {
                    return true;
                }
            }
            if (callTrade)
            {
                Order order = MarketOrders.PlaceOrder(_algoInstance, _activeCall.TradingSymbol, _activeCall.InstrumentType,
                              _activeCall.LastPrice, MappedTokens[_activeCall.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                              tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                order.InstrumentToken = _activeCall.InstrumentToken;
                _activeCallOrder = order;

                OnTradeEntry(_activeCallOrder);
            }

            if (putTrade)
            {
                Order order = MarketOrders.PlaceOrder(_algoInstance, _activePut.TradingSymbol, _activePut.InstrumentType,
                              _activePut.LastPrice, MappedTokens[_activePut.InstrumentToken], false, _initialQty, algoIndex: _algoIndex,
                              tickTime: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());
                order.InstrumentToken = _activePut.InstrumentToken;
                _activePutOrder = order;

                OnTradeEntry(_activePutOrder);
            }

            return false;
        }

        //private void UpdateLastPriceOnAllNodes(ref InstrumentListNode currentNode, Tick ticks)
        //{
        //    Instrument option;
        //    Tick optionTick;

        //    int currentNodeIndex = currentNode.Index;

        //    //go to the first node:
        //    while(currentNode.PrevNode != null)
        //    {
        //        currentNode = currentNode.PrevNode;
        //    }

        //    //Update all the price all the way till the end
        //    while (currentNode.NextNode != null)
        //    {
        //        option = currentNode.Instrument;

        //        if(option)
        //        optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
        //        if (optionTick.LastPrice != 0)
        //        {
        //            option.LastPrice = optionTick.LastPrice;
        //            currentNode.Instrument = option;

        //        }
        //        currentNode = currentNode.NextNode;
        //    }

        //    //Come back to the current node
        //    while(currentNode.PrevNode != null)
        //    {
        //        if(currentNode.Index == currentNodeIndex)
        //        {
        //            break;
        //        }
        //        currentNode = currentNode.PrevNode;
        //    }
        //}

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        //private InstrumentListNode MoveToSubsequentNode(InstrumentListNode optionNode, double optionDelta,
        //   double lowerDelta, double upperDelta, decimal bInstPrice, DateTime currentTime)
        //{
        //    bool nodeFound = false;
        //    Instrument currentOption = optionNode.Instrument;
        //    ///TODO: Change this to Case statement.
        //    if (currentOption.InstrumentType.Trim(' ') == "CE")
        //    {
        //        if (optionDelta > upperDelta)
        //        {
        //            while (optionNode.NextNode != null)
        //            {
        //                optionNode = optionNode.NextNode;
        //                currentOption = optionNode.Instrument;
        //                if (currentOption.LastPrice == 0)
        //                {
        //                    return null;
        //                }
        //                ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
        //                double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, currentTime, Convert.ToDouble(bInstPrice));
        //                delta = Math.Abs(delta);

        //                if (double.IsNaN(delta))
        //                {
        //                    return null;
        //                }
        //                //if (delta <= upperDelta)// * 0.75) //90% of upper delta
        //                //if (delta <= _entryDelta)// * 0.75) //90% of upper delta
        //                if (delta <= _reEntryDeltaAfterLoss)// * 0.75) //90% of upper delta
        //                {
        //                    nodeFound = true;
        //                    break;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            while (optionNode.PrevNode != null)
        //            {
        //                optionNode = optionNode.PrevNode;
        //                currentOption = optionNode.Instrument;
        //                if (currentOption.LastPrice == 0)
        //                {
        //                    return null;
        //                }
        //                ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
        //                double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, currentTime, Convert.ToDouble(bInstPrice));
        //                delta = Math.Abs(delta);
        //                //if (delta >= upperDelta)// * 0.75)  // Do not move as the next delta is breaching the upper limit of delta
        //                if (double.IsNaN(delta))
        //                {
        //                    return null;
        //                }
        //                else if (delta >= _entryDelta)
        //                {
        //                    optionNode = optionNode.NextNode;
        //                    nodeFound = true;
        //                    break;
        //                }
        //                if (delta >= lowerDelta)// *1.43) //110% of lower delta
        //                {
        //                    nodeFound = true;
        //                    break;
        //                }
        //            }

        //        }
        //    }
        //    else
        //    {
        //        if (optionDelta > upperDelta)
        //        {
        //            while (optionNode.PrevNode != null)
        //            {
        //                optionNode = optionNode.PrevNode;
        //                currentOption = optionNode.Instrument;
        //                if (currentOption.LastPrice == 0)
        //                {
        //                    return null;
        //                }
        //                ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
        //                double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, currentTime, Convert.ToDouble(bInstPrice));
        //                delta = Math.Abs(delta);
        //                if (double.IsNaN(delta))
        //                {
        //                    return null;
        //                }
        //                //else if (delta <= upperDelta)// * 0.75) //90% of upper delta
        //                //else if (delta <= _entryDelta)// * 0.75) //90% of upper delta
        //                else if (delta <= _reEntryDeltaAfterLoss)// * 0.75) //90% of upper delta
        //                {
        //                    nodeFound = true;
        //                    break;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            while (optionNode.NextNode != null)
        //            {
        //                optionNode = optionNode.NextNode;
        //                currentOption = optionNode.Instrument;
        //                if (currentOption.LastPrice == 0)
        //                {
        //                    return null;
        //                }
        //                ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
        //                double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, currentTime, Convert.ToDouble(bInstPrice));
        //                delta = Math.Abs(delta);
        //                //if(delta >= upperDelta * 0.75)  // Do not move as the next delta is breaching the upper limit of delta
        //                if (delta >= _entryDelta)  // Do not move as the next delta is breaching the upper limit of delta
        //                {
        //                    optionNode = optionNode.PrevNode;
        //                    nodeFound = true;
        //                    break;
        //                }
        //                if (delta >= lowerDelta) //110% of lower delta
        //                {
        //                    nodeFound = true;
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //    if (!nodeFound)
        //    {
        //        InstrumentListNode outNode;
        //        currentOption = optionNode.Instrument;
        //        nodeFound = AssignNextNodes(optionNode, currentOption.BaseInstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value, currentTime, out outNode);
        //        if (nodeFound)
        //            optionNode = MoveToSubsequentNode(optionNode, optionDelta, lowerDelta, upperDelta, bInstPrice, currentTime);
        //        else
        //            if (optionNode.Index > 0)
        //            optionNode.LastNode = true;
        //        else
        //            optionNode.FirstNode = true;
        //    }
        //    return optionNode;
        //}


        ///// <summary>
        ///// Currently Implemented only for delta range.
        ///// </summary>
        ///// <param name="bToken"></param>
        ///// <param name="bValue"></param>
        ///// <param name="peToken"></param>
        ///// <param name="pevalue"></param>
        ///// <param name="ceToken"></param>
        ///// <param name="ceValue"></param>
        ///// <param name="peLowerValue"></param>
        ///// <param name="pelowerDelta"></param>
        ///// <param name="peUpperValue"></param>
        ///// <param name="peUpperDelta"></param>
        ///// <param name="ceLowerValue"></param>
        ///// <param name="celowerDelta"></param>
        ///// <param name="ceUpperValue"></param>
        ///// <param name="ceUpperDelta"></param>
        ///// <param name="stopLossPoints"></param>
        //public void ManageStrangleDelta(Instrument bInst, Instrument currentPE, Instrument currentCE,
        //    double pelowerDelta = 0,  double peUpperDelta = 0, double celowerDelta = 0, 
        //    double ceUpperDelta = 0, double stopLossPoints = 0, int strangleId = 0)
        //{
        //    if (currentPE.LastPrice * currentCE.LastPrice != 0)
        //    {
        //        ///TODO: placeOrder lowerPutValue and upperCallValue
        //        /// Get Executed values on to lowerPutValue and upperCallValue

        //        //Two seperate linked list are maintained with their incides on the linked list.
        //        InstrumentListNode put = new InstrumentListNode(currentPE);
        //        InstrumentListNode call = new InstrumentListNode(currentCE);

        //        //If new strangle, place the order and update the data base. If old strangle monitor it.
        //        if (strangleId == 0)
        //        {
        //            //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
        //            //                                            Convert.ToString(currentPE.InstrumentToken) });

        //            //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
        //            //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;

        //            //TODO -> First price
        //            decimal pePrice = currentPE.LastPrice;
        //            decimal cePrice = currentCE.LastPrice;

        //            put.Prices.Add(pePrice); //put.SellPrice = 100;
        //            call.Prices.Add(cePrice);  // call.SellPrice = 100;

        //            ///Uncomment below for real time orders
        //            //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
        //            //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

        //            //Update Database
        //            DataLogic dl = new DataLogic();
        //            strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, call.Prices.Sum(), 
        //                put.Prices.Sum(), AlgoIndex.DeltaStrangle, celowerDelta, ceUpperDelta, pelowerDelta, peUpperDelta, 
        //                stopLossPoints = 0);
        //        }
        //    }
        //}
        //public void ManageStrangleDelta(uint peToken, uint ceToken,string peSymbol, string ceSymbol,
        //   double pelowerDelta = 0, double peUpperDelta = 0, double celowerDelta = 0,
        //   double ceUpperDelta = 0, double stopLossPoints = 0, int strangleId = 0)
        //{
        //        //If new strangle, place the order and update the data base. If old strangle monitor it.
        //        if (strangleId == 0)
        //        {
        //        //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
        //        //                                            Convert.ToString(currentPE.InstrumentToken) });

        //        //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
        //        //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;

        //        ////TODO -> First price
        //        //decimal pePrice = currentPE.LastPrice;
        //        //decimal cePrice = currentCE.LastPrice;

        //        //put.Prices.Add(pePrice); //put.SellPrice = 100;
        //        //call.Prices.Add(cePrice);  // call.SellPrice = 100;

        //        //Uncomment below for real time orders
        //        decimal pePrice = PlaceOrder(peSymbol, false);
        //        decimal cePrice = PlaceOrder(ceSymbol, false);
        //        //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
        //        //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

        //        //Update Database
        //        DataLogic dl = new DataLogic();
        //            strangleId = dl.StoreStrangleData(ceToken, peToken, cePrice,
        //                pePrice, AlgoIndex.DeltaStrangle, celowerDelta, ceUpperDelta, pelowerDelta, peUpperDelta,
        //                stopLossPoints = 0);
        //        }
        //}

        //bool AssignNextNodesWithDelta(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
        //    decimal currentStrikePrice, DateTime expiry, DateTime? tickTimeStamp)
        //{
        //    DataLogic dl = new DataLogic();
        //    SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
        //        currentStrikePrice, expiry, currentNode.Index);

        //    if(NodeData.Count == 0)
        //    {
        //        return false;
        //    }
        //    Instrument currentInstrument = currentNode.Instrument;

        //    NodeData.Add(currentInstrument.Strike, currentInstrument);

        //    int currentIndex = currentNode.Index;
        //    int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);

        //    InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
        //    baseNode = firstOption;
        //    int index = currentIndex - currentNodeIndex;


        //    for (byte i = 1; i < NodeData.Values.Count; i++)
        //    {
        //        InstrumentListNode option = new InstrumentListNode(NodeData.Values[i]);

        //        baseNode.NextNode = option;
        //        baseNode.Index = index;
        //        option.PrevNode = baseNode;
        //        baseNode = option;
        //        index++;
        //    }
        //    baseNode.Index = index; //to assign index to the last node

        //    if (currentNodeIndex == 0)
        //    {
        //        firstOption.NextNode.PrevNode = currentNode;
        //        currentNode.NextNode = firstOption.NextNode;
        //    }
        //    else if (currentNodeIndex == NodeData.Values.Count - 1)
        //    {
        //        baseNode.PrevNode.NextNode = currentNode;
        //        currentNode.PrevNode = baseNode.PrevNode;
        //    }
        //    else
        //    {
        //        while (baseNode.PrevNode != null)
        //        {
        //            if (baseNode.Index == currentIndex)
        //            {
        //                currentNode.PrevNode = baseNode.PrevNode;
        //                currentNode.NextNode = baseNode.NextNode;
        //                break;
        //            }
        //            baseNode = baseNode.PrevNode;
        //        }
        //    }

        //    return true;
        //}
        /// <summary>
        /// Pulls nodes data from database on both sides
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="baseInstrumentToken"></param>
        /// <param name="instrumentType"></param>
        /// <param name="currentStrikePrice"></param>
        /// <param name="expiry"></param>
        /// <param name="updownboth"></param>
        /// <returns></returns>
        //bool AssignNextNodes(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
        //decimal currentStrikePrice, DateTime expiry, DateTime currentTime, out InstrumentListNode outNode)
        //{
        //    DataLogic dl = new DataLogic();

        //    int searchIndex = 0;

        //    if (currentNode == null)
        //    {
        //        searchIndex = 0;
        //    }
        //    else
        //    {
        //        if (currentNode.NextNode == null)
        //        {
        //            searchIndex++;
        //        }
        //        if (currentNode.PrevNode == null)
        //        {
        //            searchIndex--;
        //        }
        //    }

        //    SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(_baseInstrumentToken, instrumentType,
        //        currentStrikePrice, expiry, searchIndex);
        //    outNode = null;

        //    if (NodeData.Count == 0)
        //    {
        //        if (currentNode != null)
        //        {
        //            if (currentNode.Index > 0)
        //                currentNode.LastNode = true;
        //            else
        //                currentNode.FirstNode = true;
        //        }
        //        return false;
        //    }


        //    //if (currentNode == null)
        //    //{
        //    //    if (instrumentType.Trim(' ').ToLower() == "ce")
        //    //    {
        //    //        for (byte i = 0; i < NodeData.Values.Count; i++)
        //    //        {
        //    //            Instrument instrument = NodeData.Values[i];
        //    //            instrument.UpdateDelta((double) instrument.LastPrice, 0.1, currentTime, (double) _baseInstrumentPrice);

        //    //            if (instrument.Delta <= _entryDelta)
        //    //            {
        //    //                currentNode = new InstrumentListNode(instrument);
        //    //                currentNode.Index = 0;
        //    //                break;
        //    //            }
        //    //        }
        //    //    }
        //    //    else if (instrumentType.Trim(' ').ToLower() == "pe")
        //    //    {
        //    //        for (int i = NodeData.Values.Count - 1; i >= 0; i--)
        //    //        {
        //    //            Instrument instrument = NodeData.Values[i];
        //    //            instrument.UpdateDelta((double) instrument.LastPrice, 0.1, currentTime, (double)_baseInstrumentPrice);

        //    //            if (instrument.Delta * -1 <= _entryDelta)
        //    //            {
        //    //                currentNode = new InstrumentListNode(instrument);
        //    //                currentNode.Index = 0;
        //    //                break;
        //    //            }
        //    //        }
        //    //    }
        //    //}

        //    InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
        //    baseNode = firstOption;


        //    int currentIndex = 0;
        //    int currentNodeIndex = 0;

        //    if (currentNode != null)
        //    {
        //        Instrument currentInstrument = currentNode.Instrument;
        //        NodeData.Add(currentInstrument.Strike, currentInstrument);

        //        currentIndex = currentNode.Index;
        //        currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);
        //    }
        //    else
        //    {
        //        currentNode = baseNode;
        //        outNode = currentNode;
        //    }


        //    int index = currentIndex - currentNodeIndex;
        //    bool dataUpdated = false;
        //    for (byte i = 1; i < NodeData.Values.Count; i++)
        //    {
        //        Instrument instrument = NodeData.Values[i];
        //        InstrumentListNode option = new InstrumentListNode(instrument);

        //        _instrumentDict.TryAdd(instrument.InstrumentToken, instrument);

        //        baseNode.NextNode = option;
        //        baseNode.Index = index;
        //        option.PrevNode = baseNode;
        //        baseNode = option;
        //        index++;

        //        if (!SubscriptionTokens.Contains(instrument.InstrumentToken))
        //        {
        //            SubscriptionTokens.Add(instrument.InstrumentToken);
        //            dataUpdated = true;
        //        }
        //    }

        //    if (dataUpdated)
        //    {
        //        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
        //        Task task = Task.Run(() => OnOptionUniverseChange(this));
        //    }



        //    baseNode.Index = index; //to assign index to the last node

        //    if (currentNodeIndex == 0)
        //    {
        //        firstOption.NextNode.PrevNode = currentNode;
        //        currentNode.NextNode = firstOption.NextNode;
        //    }
        //    else if (currentNodeIndex == NodeData.Values.Count - 1)
        //    {
        //        baseNode.PrevNode.NextNode = currentNode;
        //        currentNode.PrevNode = baseNode.PrevNode;
        //    }
        //    else
        //    {
        //        while (baseNode.PrevNode != null)
        //        {
        //            if (baseNode.Index == currentIndex)
        //            {
        //                baseNode.Prices = currentNode.Prices;
        //                currentNode.PrevNode = baseNode.PrevNode;
        //                currentNode.NextNode = baseNode.NextNode;
        //                break;
        //            }
        //            baseNode = baseNode.PrevNode;
        //        }
        //    }
        //    return true;
        //}


        //private void LoadOptionsToTrade(DateTime currentTime)
        //{
        //    try
        //    {
        //        if (_optionUniverse == null)
        //        {
        //            LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
        //            //Load options asynchronously
        //            DataLogic dl = new DataLogic();
        //            SortedList<decimal, Instrument> ceSortedList, peSortedList;
        //            _optionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, 1000, out ceSortedList, out peSortedList);


        //            Instrument ce, pe;
        //            double delta;
        //            foreach(var item in ceSortedList)
        //            {
        //                ce = item.Value;
        //                ce.UpdateDelta((double) item.Key, 0.1, currentTime, (double) _baseInstrumentPrice);
        //                if(ce.Delta < _entryDelta)
        //                {

        //                }
        //            }



        //            InstrumentLinkedList ceList, peList;

        //            ceList = new InstrumentLinkedList(null)
        //            {
        //                CurrentInstrumentIndex = 0,
        //                UpperDelta = _maxDelta,
        //                LowerDelta = _minDelta,
        //                StopLossPoints = Convert.ToDouble(_stopLoss),
        //                NetPrice = 0,
        //                listID = _algoInstance,
        //                BaseInstrumentToken = _baseInstrumentToken
        //            };
        //            ceList = new InstrumentLinkedList(null)
        //            {
        //                CurrentInstrumentIndex = 0,
        //                UpperDelta = _maxDelta,
        //                LowerDelta = _minDelta,
        //                StopLossPoints = Convert.ToDouble(_stopLoss),
        //                NetPrice = 0,
        //                listID = _algoInstance,
        //                BaseInstrumentToken = _baseInstrumentToken
        //            };

        //            InstrumentListNode strangleTokenNode = null;

        //            foreach (var item in ceSortedList)
        //            {
        //                if (strangleTokenNode == null)
        //                {
        //                    strangleTokenNode = new InstrumentListNode(item.Value)
        //                    {
        //                        Index = 0
        //                    };
        //                    strangleTokenNode.Prices = null;
        //                }
        //                else
        //                {
        //                    InstrumentListNode newNode = new InstrumentListNode(item.Value)
        //                    {
        //                        Index = 0
        //                    };
        //                    newNode.Prices = null;
        //                    strangleTokenNode.AttachNode(newNode);
        //                }
        //            }

        //            ceList.Current = strangleTokenNode;

        //            foreach (Instrument instrument in _optionUniverse)
        //            {
        //                ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
        //                ///Other wise it will create a problem with below logic, as new average gets calculated using
        //                ///last 2 prices, and retrival below is the average price.


        //            }

        //            //int strategyId = (int)strangleRow["Id"];
        //            InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;


        //            Instrument option = new Instrument()
        //            {
        //                BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //                InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
        //                InstrumentType = (string)strangleTokenRow["Type"],
        //                Strike = (Decimal)strangleTokenRow["StrikePrice"],
        //                TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
        //            };
        //            if (strangleTokenRow["Expiry"] != DBNull.Value)
        //                option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

        //            ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
        //            ///Other wise it will create a problem with below logic, as new average gets calculated using
        //            ///last 2 prices, and retrival below is the average price.
        //            List<Decimal> prices = new List<decimal>();
        //            if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
        //                prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

        //            if (strangleTokenNode == null)
        //            {
        //                strangleTokenNode = new InstrumentListNode(option)
        //                {
        //                    Index = (int)strangleTokenRow["InstrumentIndex"]
        //                };
        //                strangleTokenNode.Prices = prices;
        //            }
        //            else
        //            {
        //                InstrumentListNode newNode = new InstrumentListNode(option)
        //                {
        //                    Index = (int)strangleTokenRow["InstrumentIndex"]
        //                };
        //                newNode.Prices = prices;
        //                strangleTokenNode.AttachNode(newNode);
        //            }




        //            InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
        //                    strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
        //            {
        //                CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
        //                MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
        //                MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
        //                StopLossPoints = (double)strangleRow["StopLossPoints"],
        //                NetPrice = (decimal)strangleRow["NetPrice"],
        //                listID = strategyId,
        //                BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
        //            };

        //            if (ActiveStrangles.ContainsKey(strategyId))
        //            {
        //                ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
        //            }
        //            else
        //            {
        //                InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
        //                strangle[(int)instrumentType] = instrumentLinkedList;

        //                ActiveStrangles.Add(strategyId, strangle);
        //            }


        //            ActiveStrangle = new InstrumentLinkedList[2];

        //            InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
        //                        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
        //            {
        //                CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
        //                MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
        //                MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
        //                StopLossPoints = (double)strangleRow["StopLossPoints"],
        //                NetPrice = (decimal)strangleRow["NetPrice"],
        //                listID = strategyId,
        //                BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
        //            };



        //            //foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
        //            //{
        //            InstrumentListNode strangleTokenNode = null;
        //                foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
        //                {
        //                    Instrument option = new Instrument()
        //                    {
        //                        BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
        //                        InstrumentType = (string)strangleTokenRow["Type"],
        //                        Strike = (Decimal)strangleTokenRow["StrikePrice"],
        //                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
        //                    };
        //                    if (strangleTokenRow["Expiry"] != DBNull.Value)
        //                        option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

        //                    ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
        //                    ///Other wise it will create a problem with below logic, as new average gets calculated using
        //                    ///last 2 prices, and retrival below is the average price.
        //                    List<Decimal> prices = new List<decimal>();
        //                    if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
        //                        prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

        //                    if (strangleTokenNode == null)
        //                    {
        //                        strangleTokenNode = new InstrumentListNode(option)
        //                        {
        //                            Index = (int)strangleTokenRow["InstrumentIndex"]
        //                        };
        //                        strangleTokenNode.Prices = prices;
        //                    }
        //                    else
        //                    {
        //                        InstrumentListNode newNode = new InstrumentListNode(option)
        //                        {
        //                            Index = (int)strangleTokenRow["InstrumentIndex"]
        //                        };
        //                        newNode.Prices = prices;
        //                        strangleTokenNode.AttachNode(newNode);
        //                    }
        //                //}
        //                int strategyId = (int)strangleRow["Id"];
        //                InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

        //                InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
        //                        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
        //                {
        //                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
        //                    MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
        //                    MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
        //                    StopLossPoints = (double)strangleRow["StopLossPoints"],
        //                    NetPrice = (decimal)strangleRow["NetPrice"],
        //                    listID = strategyId,
        //                    BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
        //                };

        //                if (ActiveStrangles.ContainsKey(strategyId))
        //                {
        //                    ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
        //                }
        //                else
        //                {
        //                    InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
        //                    strangle[(int)instrumentType] = instrumentLinkedList;

        //                    ActiveStrangles.Add(strategyId, strangle);
        //                }
        //            }








        //            //if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Option != null
        //            //    && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_straddleCallOrderTrio.Option.Strike))
        //            //{
        //            //    OptionUniverse[(int)InstrumentType.CE].Add(_straddleCallOrderTrio.Option.Strike, _straddleCallOrderTrio.Option);
        //            //}
        //            //if (_straddlePutOrderTrio != null && _straddlePutOrderTrio.Option != null
        //            //    && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_straddlePutOrderTrio.Option.Strike))
        //            //{
        //            //    OptionUniverse[(int)InstrumentType.PE].Add(_straddlePutOrderTrio.Option.Strike, _straddlePutOrderTrio.Option);
        //            //}

        //            LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
        //        Thread.Sleep(100);
        //    }
        //}

        //public void LoadOptionsToTrade(DateTime currentTime)
        //{
        //    DataLogic dl = new DataLogic();
        //    DataSet activeStrangles = dl.RetrieveActiveData(AlgoIndex.DeltaStrangle);
        //    DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
        //        new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

        //    foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
        //    {
        //        InstrumentListNode strangleTokenNode = null;
        //        foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
        //        {
        //            Instrument option = new Instrument()
        //            {
        //                BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //                InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
        //                InstrumentType = (string)strangleTokenRow["Type"],
        //                Strike = (Decimal)strangleTokenRow["StrikePrice"],
        //                TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
        //            };
        //            if (strangleTokenRow["Expiry"] != DBNull.Value)
        //                option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

        //            ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
        //            ///Other wise it will create a problem with below logic, as new average gets calculated using
        //            ///last 2 prices, and retrival below is the average price.
        //            List<Decimal> prices = new List<decimal>();
        //            if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
        //                prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

        //            if (strangleTokenNode == null)
        //            {
        //                strangleTokenNode = new InstrumentListNode(option)
        //                {
        //                    Index = (int)strangleTokenRow["InstrumentIndex"]
        //                };
        //                strangleTokenNode.Prices = prices;
        //            }
        //            else
        //            {
        //                InstrumentListNode newNode = new InstrumentListNode(option)
        //                {
        //                    Index = (int)strangleTokenRow["InstrumentIndex"]
        //                };
        //                newNode.Prices = prices;
        //                strangleTokenNode.AttachNode(newNode);
        //            }
        //        }
        //        int strategyId = (int)strangleRow["Id"];
        //        InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

        //        InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
        //                strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
        //        {
        //            CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
        //            MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
        //            MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
        //            StopLossPoints = (double)strangleRow["StopLossPoints"],
        //            NetPrice = (decimal)strangleRow["NetPrice"],
        //            listID = strategyId,
        //            BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
        //        };

        //        if (ActiveStrangles.ContainsKey(strategyId))
        //        {
        //            ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
        //        }
        //        else
        //        {
        //            InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
        //            strangle[(int)instrumentType] = instrumentLinkedList;

        //            ActiveStrangles.Add(strategyId, strangle);
        //        }
        //    }
        //}

        //private void UpdateInstrumentSubscription( DateTime currentTime)
        //{
        //    try
        //    {
        //        bool dataUpdated = false;
        //        if (_optionUniverse != null)
        //        {
        //            foreach (var option in _optionUniverse)
        //            {
        //                //foreach (var option in options)
        //                //{
        //                if (!SubscriptionTokens.Contains(option.InstrumentToken))
        //                {
        //                    SubscriptionTokens.Add(option.InstrumentToken);
        //                    dataUpdated = true;
        //                }
        //                //}
        //            }
        //            if (!SubscriptionTokens.Contains(_baseInstrumentToken))
        //            {
        //                SubscriptionTokens.Add(_baseInstrumentToken);
        //            }
        //            if (dataUpdated)
        //            {
        //                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
        //                Task task = Task.Run(() => OnOptionUniverseChange(this));
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
        //        Thread.Sleep(100);
        //    }
        //}

        //private void UpdateInstrumentSubscription(DateTime currentTime)
        //{
        //    try
        //    {
        //        bool dataUpdated = false;
        //        if (_optionUniverse != null)
        //        {
        //            foreach (var option in _optionUniverse)
        //            {
        //                //foreach (var option in options)
        //                //{
        //                    if (!SubscriptionTokens.Contains(option.InstrumentToken))
        //                    {
        //                        SubscriptionTokens.Add(option.InstrumentToken);
        //                        dataUpdated = true;
        //                    }
        //                //}
        //            }
        //            if (!SubscriptionTokens.Contains(_baseInstrumentToken))
        //            {
        //                SubscriptionTokens.Add(_baseInstrumentToken);
        //            }
        //            if (dataUpdated)
        //            {
        //                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
        //                Task task = Task.Run(() => OnOptionUniverseChange(this));
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
        //        Thread.Sleep(100);
        //    }
        //}
        //public void LoadActiveStrangles()
        //{
        //    DataLogic dl = new DataLogic();
        //    DataSet activeStrangles = dl.RetrieveActiveData(AlgoIndex.DeltaStrangle);
        //    DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
        //        new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

        //    foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
        //    {
        //        InstrumentListNode strangleTokenNode = null;
        //        foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
        //        {
        //            Instrument option = new Instrument()
        //            {
        //                BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //                InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
        //                InstrumentType = (string)strangleTokenRow["Type"],
        //                Strike = (Decimal)strangleTokenRow["StrikePrice"],
        //                TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
        //            };
        //            if (strangleTokenRow["Expiry"] != DBNull.Value)
        //                option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

        //            ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
        //            ///Other wise it will create a problem with below logic, as new average gets calculated using
        //            ///last 2 prices, and retrival below is the average price.
        //            List<Decimal> prices = new List<decimal>();
        //            if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
        //                prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

        //            if (strangleTokenNode == null)
        //            {
        //                strangleTokenNode = new InstrumentListNode(option)
        //                {
        //                    Index = (int)strangleTokenRow["InstrumentIndex"]
        //                };
        //                strangleTokenNode.Prices = prices;
        //            }
        //            else
        //            {
        //                InstrumentListNode newNode = new InstrumentListNode(option)
        //                {
        //                    Index = (int)strangleTokenRow["InstrumentIndex"]
        //                };
        //                newNode.Prices = prices;
        //                strangleTokenNode.AttachNode(newNode);
        //            }
        //        }
        //        int strategyId = (int)strangleRow["Id"];
        //        InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

        //        InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
        //                strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
        //        {
        //            CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
        //            MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
        //            MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
        //            StopLossPoints = (double)strangleRow["StopLossPoints"],
        //            NetPrice = (decimal)strangleRow["NetPrice"],
        //            listID = strategyId,
        //            BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
        //        };

        //        if (ActiveStrangles.ContainsKey(strategyId))
        //        {
        //            ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
        //        }
        //        else
        //        {
        //            InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
        //            strangle[(int)instrumentType] = instrumentLinkedList;

        //            ActiveStrangles.Add(strategyId, strangle);
        //        }
        //    }

        //}

        private Order PlaceOrder(Instrument option, bool buyOrder, DateTime? currentTime)
        {
            //temp
            //decimal price = 0;
            //if (optionList.Current.Instrument.LastPrice == 0)
            //{
            //    price = optionList.Current.Prices.Last();
            //}
            //else
            //{
            //    price = optionList.Current.Instrument.LastPrice;
            //}

            //System.Net.Mail.SmtpClient email = new System.Net.Mail.SmtpClient("smtp.gmail.com");
            //email.SendAsync("prashantholahal@gmail.com", "prashant.malviya@ge.com", "Delta trigerred", string.Format("ICAM Support Automated Message@ {0}: {1} - Buy: {2} @ {3}", DateTime.Now.ToString(),  Symbol, buyOrder.ToString(), price.ToString()), null);

            //return price;


            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, 
                option.InstrumentType.Trim(' '), option.LastPrice, MappedTokens[option.InstrumentToken], buyOrder, _initialQty, _algoIndex, currentTime, 
                httpClient:_httpClientFactory.CreateClient());
            return order;


            //Dictionary<string, dynamic> orderStatus;

            //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = orderStatus["data"]["order_id"];
            //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //return orderInfo[orderInfo.Count - 1].AveragePrice;
        }
        private decimal PlaceOrder(string Symbol, bool buyOrder)
        {
            decimal price = 0;
            //System.Net.Mail.SmtpClient email = new System.Net.Mail.SmtpClient("smtp.gmail.com");
            //email.SendAsync("prashantholahal@gmail.com", "prashant.malviya@ge.com", "Delta trigerred", 
            //    string.Format("ICAM Support Automated Message@ {0}: {1} - Buy: {2} @ {3}", DateTime.Now.ToString(), Symbol, buyOrder.ToString(), price.ToString()), null);

            return 0;
            //Dictionary<string, dynamic> orderStatus;

            //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = orderStatus["data"]["order_id"];
            //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //return orderInfo[orderInfo.Count - 1].AveragePrice;
        }

        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(TickDataStreamer publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}

        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
                Thread.Sleep(100);
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
                Thread.Sleep(100);
            }
        }
        public int AlgoInstance
        {
            get
            { return _algoInstance; }
        }
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }
        private void PublishLog(object sender, ElapsedEventArgs e)
        {
                //LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, e.SignalTime,
                //String.Format("Current PnL: {0}", Decimal.Round(_bADX.MovingAverage.GetValue<decimal>(0), 2)),
                //"Log_Timer_Elapsed");
        }
    }
}
