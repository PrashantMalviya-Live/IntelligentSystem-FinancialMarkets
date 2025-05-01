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
using System.Timers;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;

namespace Algorithms.Algorithms
{
    public class StraddleWithEachSideSL : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        public Dictionary<uint, Instrument> OptionsDictionary { get; set; }
        public SortedList<decimal, Instrument[]> StraddleUniverse { get; set; }
        public Dictionary<uint, uint> MappedTokens { get; set; }

        public Dictionary<decimal, GlobalLayer.Option> StraddleNodes { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(StraddleWithEachSideSL source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;

        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;
        private User _user;
        //public StrangleOrderLinkedList sorderList;
        public OrderTrio _straddleCallOrderTrio;
        public OrderTrio _straddlePutOrderTrio;
        public OrderTrio _soloCallOrderTrio;
        public OrderTrio _soloPutOrderTrio;
        //private Instrument _activeCall;
        //private Instrument _activePut;
        private decimal _strikePriceIncrement = 0;
        private decimal _referenceStraddleValue;
        private decimal _referenceValueForStraddleShift;
        //private Dictionary<decimal, OrderTrio> _callOrderTrios;
        //private Dictionary<decimal, OrderTrio> _putOrderTrios;
        private List<OrderTrio> _callOrderTrios;
        private List<OrderTrio> _putOrderTrios;
        public List<Order> _pastOrders;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        private bool _firstSLHit = false;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        public Dictionary<decimal, int> _tradeStrike;
        private int _maxTradePerStrike;
        private decimal _initialSL = 0.2m;
        private decimal _trailSL = 0.2m;
        private decimal _trailStraddleSL = 0.05m;
        private decimal _straddleSL = 0.1m;
        private int _totalEntries = 1;
        private uint _baseInstrumentToken;
        private const uint VIX_TOKEN = 264969;
        private decimal _baseInstrumentPrice;
        private decimal _baseInstrumentUpperThreshold;
        private decimal _baseInstrumentLowerThreshold;
        private decimal _bInstrumentPreviousPrice;
        public const int CANDLE_COUNT = 30;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public readonly int _emaLength;
        private const int BASE_ADX_LENGTH = 30;
        private const decimal STOP_LOSS_PERCENT = 0.4m;
        private const decimal TSL_PERCENT = 0.2m;


        private decimal _referencePrice = 0;
        private const decimal ENTRY_PERCENT = 0.3m;
        private decimal _slPercent = 0.3m;

        private decimal _callReferencePrice = 0;
        private decimal _putReferencePrice = 0;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private decimal _pnl = 0;
        private decimal _vixRSIThrehold;
        //private RelativeStrengthIndex _vixRSI;
        //private bool _vixRSILoaded = false, _vixRSILoadedFromDB = false;
        //private bool _vixRSILoading = false;

        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        private bool _adxPeaked = false;
        //private Instrument _activeCall;
        //private Instrument _activePut;
        public const AlgoIndex algoIndex = AlgoIndex.StraddleWithEachLegCutOff;
        //TimeSpan candletimeframe;
        private bool _straddleShift;
        bool callLoaded = false;
        bool putLoaded = false;
        bool referenceCallLoaded = false;
        bool referencePutLoaded = false;
        private Dictionary<decimal, bool> _callOptionLoaded;
        private Dictionary<decimal, bool> _putOptionLoaded;
        private decimal _targetProfit;
        private decimal _stopLoss;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        IHttpClientFactory _httpClientFactory;
        private bool _firstEntryDone = false;
        //private List<int> _numberOfShifts = new List<int>();
        private const int MAX_NUMBER_OF_SHIFTS = 2;
        private int _numberofentries = 0;
        private int _numberOfOptions = 0;
        private const int MAX_NUMBER_OF_ENTRIES = 2;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();

        /// <summary>
        /// Smart Straddle Strategy. Take strangle straddle with 100 Rs premium, and 
        /// then enter with 20% discount price, and then put SL 30% on each leg as it enters
        /// </summary>
        /// <param name="baseInstrumentToken"></param>
        /// <param name="quantity"></param>
        /// <param name="uid"></param>
        /// <param name="httpClientFactory"></param>
        public StraddleWithEachSideSL(uint baseInstrumentToken, 
            int quantity, string uid, DateTime expiryDate,
            IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;
            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _maxDistanceFromBInstrument = 1200;
            _minDistanceFromBInstrument = 0;
            _stopLossRatio = 1.3m;
            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();
            //CandleSeries candleSeries = new CandleSeries();
            _tradeQty = quantity;
            //_firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            //TimeCandles = new Dictionary<uint, List<Candle>>();
            //candleManger = new CandleManger(TimeCandles, CandleType.Time);
            //candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _callOptionLoaded = new Dictionary<decimal, bool>();
            _putOptionLoaded = new Dictionary<decimal, bool>();

            _tradeStrike = new Dictionary<decimal, int>();
            //ONLY ON EXPIRY SHOULD THIS BE INCREASED
            _maxTradePerStrike = 2;
            _callOrderTrios = new List<OrderTrio>();
            _putOrderTrios = new List<OrderTrio>();

            DataLogic dl = new DataLogic();
            _expiryDate = expiryDate;// dl.GetCurrentWeeklyExpiry(DateTime.Now, baseInstrumentToken);

            _algoInstance = Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                DateTime.Now, quantity, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)0, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            uid = "PM27031981";
            ////ZConnect.Login();
            ////_user = KoConnect.GetUser(userId: uid);

            MappedTokens = new Dictionary<uint, uint>();

             //health check after 1 mins
             _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            //DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
            //    tick.Timestamp.Value : tick.LastTradeTime.Value;
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken || tick.LastTradeTime == null ?
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
                    UpdateOptionPrice(tick);


                    _numberOfOptions = GetOptionsCount(currentTime);
                    if (_numberOfOptions > 0 && _numberofentries == 0 
                        &&( (_numberOfOptions > _callOrderTrios.Count || _numberOfOptions > _putOrderTrios.Count)
                        && ((_numberOfOptions >= 1 && _callOrderTrios.Count == 0) || (_numberOfOptions > _callOrderTrios[0].Order.Quantity/(_tradeQty * _callOrderTrios[0].Option.LotSize)))))
                    {
                        Instrument activeCall, activePut;
                        //if (_activeCall == null || _activePut == null)
                        //{
                        GetEntryPremium(currentTime);
                        GetActiveOptions(_referencePrice, InstrumentType.ALL, out activeCall, out activePut);
                        //_numberOfShifts.Add(0);
                        //}

                        //Step 1: After 9:30 take the first option with 100 price, and put that as reference.
                        //Once its values drop by 20% enter with 30% SL.

                        //for (int i = 0; i < _callOrderTrios.Count; i++)
                        //{
                        //    Instrument activeCall = _activeCall[i];
                        //    Instrument activePut = _activePut[i];

                        if (tick.LastTradeTime != null && activeCall != null && activePut != null)
                        {
                            if (!activeCall.IsTraded && !_firstSLHit)
                            {
                                TradeEntry(activeCall, currentTime, activeCall.LastPrice, _tradeQty * _numberOfOptions, false);
                            }
                            if (!activePut.IsTraded && !_firstSLHit)
                            {
                                TradeEntry(activePut, currentTime, activePut.LastPrice, _tradeQty * _numberOfOptions, false);
                            }
                        }
                        //}
                    }
                    //if (_numberOfOptions == _callOrderTrios.Count && _numberOfOptions == _putOrderTrios.Count)
                    //{
                       
                        for (int i = 0; i < _callOrderTrios.Count; i++)
                        {
                            OrderTrio orderTrio = _callOrderTrios[i];
                            Instrument activeCall = orderTrio.Option;

                            if (tick.InstrumentToken == activeCall.InstrumentToken)
                            {
                                //int numberOfShifts = _numberOfShifts[i];
                                CheckSL(currentTime, i , _firstSLHit);
                                //_numberOfShifts[i] = numberOfShifts;
                                break;
                            }
                        }
                        for (int i = 0; i < _putOrderTrios.Count; i++)
                        {
                            OrderTrio orderTrio = _putOrderTrios[i];
                            Instrument activePut = orderTrio.Option;

                            if (tick.InstrumentToken == activePut.InstrumentToken)
                            {
                                //int numberOfShifts = _numberOfShifts[i];
                                CheckSL(currentTime, i, _firstSLHit);
                                //_numberOfShifts[i] = numberOfShifts;
                                break;
                            }
                        }

                        //Closes all postions at 3:29 PM
                        TriggerEODPositionClose(currentTime);
                    //}
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
        private int GetOptionsCount(DateTime currentTime)
        {
            int _numberOfOptions = 0;
            if (currentTime.TimeOfDay >= new TimeSpan(14, 40, 00))
            {
                _numberOfOptions = 2;
            }
            if (currentTime.TimeOfDay >= new TimeSpan(12, 30, 00))
            {
                _numberOfOptions = 2;
            }
            else if (currentTime.TimeOfDay >= new TimeSpan(09, 30, 00))
            {
                _numberOfOptions = 1;
            }
            return _numberOfOptions;
        }
        private void GetActiveOptions(decimal referencePrice, InstrumentType instrumenttype, out Instrument activeCall, out Instrument activePut)
        {
            activePut = null;
            activeCall = null;
            if (instrumenttype == InstrumentType.ALL || instrumenttype == InstrumentType.CE)
            {
                var callUniverse = OptionUniverse[(int)InstrumentType.CE];
                int cCount = callUniverse.Count;
                for (int o = 1; o < cCount; o++)
                {
                    Instrument option = callUniverse.ElementAt(o).Value;
                    if (option.LastPrice != 0 && option.LastPrice < referencePrice
                        && callUniverse.ElementAt(o - 1).Value.LastPrice != 0 && callUniverse.ElementAt(o - 1).Value.LastPrice > referencePrice)
                    {
                        option.IsTraded = false;
                        activeCall = option;
                        _callReferencePrice = option.LastPrice;
                        break;
                    }
                }
            }
            if (instrumenttype == InstrumentType.ALL || instrumenttype == InstrumentType.PE)
            {
                var putUniverse = OptionUniverse[(int)InstrumentType.PE];
                int pCount = putUniverse.Count;
                for (int o = 0; o < pCount - 1; o++)
                {
                    Instrument option = putUniverse.ElementAt(o).Value;
                    if (option.LastPrice != 0 && option.LastPrice < referencePrice
                             && putUniverse.ElementAt(o + 1).Value.LastPrice != 0 && putUniverse.ElementAt(o + 1).Value.LastPrice > referencePrice)
                    {
                        option.IsTraded = false;
                        activePut = option;
                        _putReferencePrice = option.LastPrice;
                        break;
                    }
                }
            }


            //for (int i = 0; i < 2; i++)
            //{
            //    int optionCount = OptionUniverse[i].Count;
            //    Instrument option = OptionUniverse[i].ElementAt(o).Value;
            //    if (option.InstrumentType.ToLower() == "ce")
            //    for (int o = 0; o < optionCount - 1; o++)
            //    {
            //        Instrument option = OptionUniverse[i].ElementAt(o).Value;
            //        if (_activeCall == null && option.LastPrice > _referencePrice && (instrumenttype == InstrumentType.ALL || instrumenttype == InstrumentType.CE)
            //            && option.InstrumentType.ToLower() == "ce"
            //            && o != optionCount && OptionUniverse[i].ElementAt(o + 1).Value.LastPrice != 0 && OptionUniverse[i].ElementAt(o + 1).Value.LastPrice < _referencePrice)
            //        {
            //            _activeCall = option;
            //            _callReferencePrice = option.LastPrice;
            //        }
            //        if (_activePut == null && option.LastPrice > _referencePrice && (instrumenttype == InstrumentType.ALL || instrumenttype == InstrumentType.PE)
            //            && option.InstrumentType.ToLower() == "pe"
            //            && o != 0 && OptionUniverse[i].ElementAt(o - 1).Value.LastPrice != 0 && OptionUniverse[i].ElementAt(o - 1).Value.LastPrice < _referencePrice)
            //        {
            //            _activePut = option;
            //            _putReferencePrice = option.LastPrice;
            //        }
            //    }
            //}
        }
        private void GetEntryPremium(DateTime currentTime)
        {
            if (_referencePrice == 0)
            {
                _strikePriceIncrement = Constants.GetStrikePriceIncrement(_baseInstrumentToken);

                decimal lotSize = Constants.GetLotSize(_baseInstrumentToken);

                double dte = Math.Floor((_expiryDate.Value.Date - currentTime.Date).TotalDays);

                if (dte >= 5)
                {
                    _referencePrice = 120 * 15 / lotSize;
                    _slPercent = 0.3m;
                    _stopLoss = 15;
                }
                else if (dte >= 4)
                {
                    _referencePrice = 100 * 15 / lotSize;
                    _slPercent = 0.3m;
                    _stopLoss = 15;
                }
                else if (dte >= 3)
                {
                    _referencePrice = 100 * 15 / lotSize;
                    _slPercent = 0.3m;
                    _stopLoss = 15;
                }
                else if (dte >= 2)
                {
                    _referencePrice = 80 * 15 / lotSize;
                    _slPercent = 0.3m;
                    _stopLoss = 15;
                }
                else if (dte >= 1)
                {
                    _stopTrade = true;
                    //_referencePrice = 30 * 15 / lotSize;
                    //_slPercent = 0.3m;
                    //_stopLoss = 15;
                }
                else    if (dte >= 0)
                {
                    _stopTrade = true;
                    //_referencePrice = 15 * 15 / lotSize;
                    //_slPercent = 0.35m;
                    //_stopLoss = 100;
                }
                else
                {
                    _stopTrade = true;
                }
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
            //if (_activeCall.InstrumentToken == tick.InstrumentToken)
            //{
            //    _activeCall.LastPrice = tick.LastPrice;
            //}
            //else if (_activePut.InstrumentToken == tick.InstrumentToken)
            //{
            //    _activePut.LastPrice = tick.LastPrice;
            //}
        }
        private void CloseStraddle(DateTime? currentTime)
        {
            if (_straddleCallOrderTrio != null)
            {
                Instrument option = _straddleCallOrderTrio.Option;

                //exit trade
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                    option.InstrumentType, option.LastPrice, option.InstrumentToken,
                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                OnTradeEntry(order);
                _straddleCallOrderTrio = null;
            }
            if (_straddlePutOrderTrio != null)
            {
                Instrument option = _straddlePutOrderTrio.Option;

                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                    option.InstrumentType, option.LastPrice, option.InstrumentToken,
                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                OnTradeEntry(order);
                _straddlePutOrderTrio = null;
            }
        }
        private void TriggerEODPositionClose(DateTime? currentTime)
        {
            if (currentTime.Value.TimeOfDay >= new TimeSpan(15, 20, 00))
            {
                for (int i = 0; i < _callOrderTrios.Count; i++)
                {
                    OrderTrio ceOrderTrio = _callOrderTrios[i];
                    OrderTrio peOrderTrio = _putOrderTrios[i];

                    Instrument activeCall = ceOrderTrio.Option;
                    Instrument activePut = peOrderTrio.Option;
                    //if (activeCall != null && activePut != null)
                    //{
                    //    Console.WriteLine((_pnl / (_tradeQty * Convert.ToInt32(activeCall.LotSize))) - (activeCall.LastPrice + activePut.LastPrice));
                    //}
                    if (activeCall != null && activePut != null)
                    {
                        ExitStraddle(currentTime.Value, i, activeCall, activePut);
                        _stopTrade = true;

                        DataLogic dl = new DataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl);

                        i--;
                    }
                }
            }

            if (_callOrderTrios.Count > 0 && _putOrderTrios.Count > 0 && _numberOfOptions > 0)
            {
                decimal currentPnl = (_pnl - _callOrderTrios.Sum(x => x.Option.LastPrice * x.Order.Quantity) - _putOrderTrios.Sum(x => x.Option.LastPrice * x.Order.Quantity))
                    / _callOrderTrios[0].Option.LotSize / (_numberOfOptions * _tradeQty);

                // Console.WriteLine(currentPnl);
                int lotSize = Constants.GetLotSize(_baseInstrumentToken);

                if (currentPnl < -30 * 25 / lotSize)
                {
                    for (int i = 0; i < _callOrderTrios.Count; i++)
                    {
                        OrderTrio ceOrderTrio = _callOrderTrios[i];
                        OrderTrio peOrderTrio = _putOrderTrios[i];

                        Instrument activeCall = ceOrderTrio.Option;
                        Instrument activePut = peOrderTrio.Option;

                        ExitStraddle(currentTime.Value, i, activeCall, activePut);
                        _stopTrade = true;

                        DataLogic dl = new DataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl);

                        i--;
                    }
                }
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
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        //private void TakeATMStraddle(DateTime currentTime)
        //{
        //    decimal callPrice = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
        //    decimal putPrice = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
        //    _referenceStraddleValue = callPrice + putPrice;
        //    _stopLoss = (callPrice + putPrice) * (1 + _initialSL);
        //}

        private void ExitStraddle(DateTime currentTime, int counter, Instrument activeCall, Instrument activePut)
        {
            if (activeCall != null)
            {
                ExitOption(activeCall, currentTime, counter);
            }
            if (activePut != null)
            {
                ExitOption(activePut, currentTime, counter);
            }
        }
        private void ExitOption(Instrument option, DateTime currentTime, int counter)
        {
            OrderTrio ordertrio = null;

            if (option.InstrumentType.Trim(' ').ToLower() == "ce" && _callOrderTrios.Count> counter)
            {
                ordertrio = _callOrderTrios[counter];
                _callOrderTrios.RemoveAt(counter);
            }
            else if (option.InstrumentType.Trim(' ').ToLower() == "pe" && _putOrderTrios.Count > counter)
            {
                ordertrio = _putOrderTrios[counter];
                _putOrderTrios.RemoveAt(counter);
            }
            if (ordertrio != null)
            {
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                    option.KToken, buyOrder: true, ordertrio.Order.Quantity,
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK, 
                    httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", 1,
                   option.TradingSymbol, order.AveragePrice, "Bought"), "TradeEntry");

#endif
                option.TradeExitPrice = order.AveragePrice;
                option.IsTraded = false;

                _pnl += order.AveragePrice * order.Quantity * -1;
               
            }
        }
        private void CheckSL(DateTime currentTime, int counter, bool closeAll = false)
        {
            //if (activeCall.IsActive && _callOrderTrios.ContainsKey(activeCall.Strike) && activePut.IsActive && _putOrderTrios.ContainsKey(activePut.Strike))
            //{
            //    OrderTrio callOrderTrio = _callOrderTrios[activeCall.Strike];
            //    OrderTrio putOrderTrio = _putOrderTrios[activePut.Strike];

            //    decimal tempPnl = _pnl + (activeCall.LastPrice * callOrderTrio.Order.Quantity * -1) + (activePut.LastPrice * putOrderTrio.Order.Quantity * -1);
            //    tempPnl = tempPnl / activeCall.LotSize;

            //    double dte = Math.Floor((_expiryDate.Value.Date - currentTime.Date).TotalDays);
            //    if (tempPnl < _stopLoss * -1 && dte != 0)
            //    {
            //        closeAll = true;

            //        ExitStraddle(currentTime);
            //        _stopTrade = true;
            //        DataLogic dl = new DataLogic();
            //        dl.UpdateAlgoPnl(_algoInstance, _pnl);
            //        _pnl = 0;
            //        _numberofentries++;
            //        return;
            //    }
            //}

            Instrument activeCall = _callOrderTrios[counter].Option;
            Instrument activePut = _putOrderTrios[counter].Option;
            
            int numberOfShifts = Math.Max(_callOrderTrios[counter].flag, _putOrderTrios[counter].flag);
            _callOrderTrios[counter].flag = numberOfShifts;
            _putOrderTrios[counter].flag = numberOfShifts;

            //if SL is hit, book the profitable side and move closure to loosing side at the same premium
            if (activeCall.IsActive)
            {
                if (activeCall.LastPrice > _callOrderTrios[counter].StopLoss)
                {
                    double dte = Math.Floor((_expiryDate.Value.Date - currentTime.Date).TotalDays);
                    //numberOfShifts++;
                    if (!closeAll && (numberOfShifts < MAX_NUMBER_OF_SHIFTS || dte == 0))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, activePut.TradingSymbol, activePut.InstrumentType, activePut.LastPrice,
                      activePut.KToken, true, _callOrderTrios[counter].Order.Quantity,
                      algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                     httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        _pnl += order.AveragePrice * order.Quantity * -1;

                        _putOrderTrios.RemoveAt(counter);
                        activePut = null;
                        int previousQty = order.Quantity;
                        int _numberOfOptions = GetOptionsCount(currentTime);
                        Instrument call = null;
                        GetActiveOptions(activeCall.LastPrice, InstrumentType.PE, out call, out activePut);

                        //activePut = _activePut.Last();

                        if (activePut != null) //&& (activePut.Strike < activeCall.Strike - 5 * _strikePriceIncrement || dte == 0))
                        {
                            order = MarketOrders.PlaceOrder(_algoInstance, activePut.TradingSymbol, activePut.InstrumentType, activePut.LastPrice,
                         activePut.KToken, false, previousQty,
                         algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                        httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                            _pnl += order.AveragePrice * order.Quantity;

                            //_putOrderTrios.Remove(activePut.Strike);
                            activePut.IsActive = true;
                            activePut.IsTraded = true;
                            OrderTrio orderTrio = new OrderTrio();
                            orderTrio.Order = order;
                            orderTrio.Option = activePut;
                            orderTrio.flag = numberOfShifts + 1;
                            orderTrio.StopLoss = order.AveragePrice * (1 + _slPercent);
                            _callOrderTrios[counter].StopLoss = _callOrderTrios[counter].StopLoss * (1 + _slPercent);
                            _putOrderTrios.Insert(counter, orderTrio);
                            //_firstSLHit = true;
                        }
                        else
                        {
                            ExitStraddle(currentTime, counter, activeCall, activePut);
                            
                            //if (activeCall != null)
                            //{
                            //    _activeCall.Remove(activeCall);
                            //}
                            //if (activePut != null)
                            //{
                            //    _activePut.Remove(activePut);
                            //}

                            //_stopTrade = true;

                            //DataLogic dl = new DataLogic();
                            //dl.UpdateAlgoPnl(_algoInstance, _pnl);

                            return;
                        }
                        //}
                    }
                    else if (numberOfShifts >= MAX_NUMBER_OF_SHIFTS)
                    {
                        ExitStraddle(currentTime, counter, activeCall, activePut);
                        //if (activeCall != null)
                        //{
                        //    _activeCall.Remove(activeCall);
                        //}
                        //if (activePut != null)
                        //{
                        //    _activePut.Remove(activePut);
                        //}

                        //if (_activeCall.Count == 0 || _activePut.Count == 0)
                        //{
                        //    _stopTrade = true;
                        //}
                        if (_callOrderTrios.Count == 0 && _putOrderTrios.Count == 0)
                        {
                            _stopTrade = true;
                            DataLogic dl = new DataLogic();
                            dl.UpdateAlgoPnl(_algoInstance, _pnl);
                            //_pnl = 0;
                        }
                        _numberofentries++;
                        return;
                        //    Order order = MarketOrders.PlaceOrder(_algoInstance, activeCall.TradingSymbol, activeCall.InstrumentType, activeCall.LastPrice,
                        // activeCall.KToken, true, _tradeQty * Convert.ToInt32(activeCall.LotSize),
                        // algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                        //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        //    _pnl += order.AveragePrice * order.Quantity * -1;

                        //    _callOrderTrios.Remove(activeCall.Strike);
                        //    activeCall = null;

                        //    if (_putOrderTrios == null || _putOrderTrios.Count == 0)
                        //    {
                        //        _stopTrade = true;
                        //        DataLogic dl = new DataLogic();
                        //        dl.UpdateAlgoPnl(_algoInstance, _pnl);
                        //    }
                        //    else
                        //    {
                        //        _putOrderTrios[activePut.Strike].StopLoss = _putOrderTrios[activePut.Strike].Order.AveragePrice;
                        //    }
                    }
                }
                //else if 
                //{
                //    //ExitStraddle(currentTime);
                //    //_stopTrade = true;

                //    //DataLogic dl = new DataLogic();
                //    //dl.UpdateAlgoPnl(_algoInstance, _pnl);
                //}
            }
            if (activePut.IsActive)
            {
                if (activePut.LastPrice > _putOrderTrios[counter].StopLoss)
                {
                    double dte = Math.Floor((_expiryDate.Value.Date - currentTime.Date).TotalDays);
                    //numberOfShifts++;
                    if (!closeAll && (numberOfShifts <= MAX_NUMBER_OF_SHIFTS || dte == 0))
                    {
                        //close the winning side 
                        Order order = MarketOrders.PlaceOrder(_algoInstance, activeCall.TradingSymbol, activeCall.InstrumentType, activeCall.LastPrice,
                                         activeCall.KToken, true, _putOrderTrios[counter].Order.Quantity,
                                         algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                                        httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        _pnl += order.AveragePrice * order.Quantity * -1;

                        _callOrderTrios.RemoveAt(counter);
                        activeCall = null;
                        int previousQty = order.Quantity;
                        int _numberOfOptions = GetOptionsCount(currentTime);
                        Instrument put = null;
                        GetActiveOptions(activePut.LastPrice, InstrumentType.CE, out activeCall, out put);
                        //activeCall = _activeCall.Last();
                        if (activeCall != null)// && (activePut.Strike < activeCall.Strike - 5 * _strikePriceIncrement || dte == 0))
                        {
                            order = MarketOrders.PlaceOrder(_algoInstance, activeCall.TradingSymbol, activeCall.InstrumentType, activeCall.LastPrice,
                         activeCall.KToken, false, previousQty,
                         algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                        httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                            _pnl += order.AveragePrice * order.Quantity;

                            //_callOrderTrios.Remove(activeCall.Strike);
                            activeCall.IsActive = true;
                            activeCall.IsTraded = true;
                            OrderTrio orderTrio = new OrderTrio();
                            orderTrio.Order = order;
                            orderTrio.Option = activeCall;
                            orderTrio.flag = numberOfShifts + 1;
                            orderTrio.StopLoss = order.AveragePrice * (1 + _slPercent);
                            _putOrderTrios[counter].StopLoss = _putOrderTrios[counter].StopLoss * (1 + _slPercent);
                            _callOrderTrios.Insert(counter, orderTrio);
                            //_firstSLHit = true;
                        }
                        else
                        {
                            ExitStraddle(currentTime, counter, activeCall, activePut);
                            //if (activeCall != null)
                            //{
                            //    _activeCall.Remove(activeCall);
                            //}
                            //if (activePut != null)
                            //{
                            //    _activePut.Remove(activePut);
                            //}

                            //_stopTrade = true;
                            //DataLogic dl = new DataLogic();
                            //dl.UpdateAlgoPnl(_algoInstance, _pnl);

                            return;
                        }
                        //}
                    }
                    else if(numberOfShifts >= MAX_NUMBER_OF_SHIFTS)
                    {
                        ExitStraddle(currentTime, counter, activeCall, activePut);

                        //if (activeCall != null)
                        //{
                        //    _activeCall.Remove(activeCall);
                        //}
                        //if (activePut != null)
                        //{
                        //    _activePut.Remove(activePut);
                        //}

                        //if (_activeCall.Count == 0 || _activePut.Count == 0)
                        //{
                        //    _stopTrade = true;
                        //}
                        if (_callOrderTrios.Count == 0 && _putOrderTrios.Count == 0)
                        {
                            _stopTrade = true;
                            DataLogic dl = new DataLogic();
                            dl.UpdateAlgoPnl(_algoInstance, _pnl);
                            _pnl = 0;
                        }

                        _numberofentries++;
                        return;
                        //      Order order = MarketOrders.PlaceOrder(_algoInstance, activePut.TradingSymbol, activePut.InstrumentType, activePut.LastPrice,
                        // activePut.KToken, true, _tradeQty * Convert.ToInt32(activePut.LotSize),
                        // algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                        //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        //      _pnl += order.AveragePrice * order.Quantity * -1;

                        //      _putOrderTrios.Remove(activePut.Strike);
                        //      activePut = null;

                        //      if (_callOrderTrios == null || _callOrderTrios.Count == 0)
                        //      {
                        //          _stopTrade = true;
                        //          DataLogic dl = new DataLogic();
                        //          dl.UpdateAlgoPnl(_algoInstance, _pnl);
                        //      }
                        //      else
                        //      {
                        //          _callOrderTrios[activeCall.Strike].StopLoss = _callOrderTrios[activeCall.Strike].Order.AveragePrice;
                        //      }
                    }
                }
            }
            //else
            //{
            //    //ExitStraddle(currentTime);
            //    //_stopTrade = true;

            //    //DataLogic dl = new DataLogic();
            //    //dl.UpdateAlgoPnl(_algoInstance, _pnl);
            //}
        }
        private decimal TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty,
            bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                //ENTRY ORDER - Sell ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    option.KToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK, 
                    httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return -1;
                }

#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");

#endif
                option.TradedTime = currentTime;
                option.TradeEntryPrice = order.AveragePrice;
                option.IsTraded = true;
                
                orderTrio = new OrderTrio();

                ////ENTRY SL ORDER - for sell orders
                //Order slorder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, Math.Round(lastPrice * (1 + _slPercent) / 20, 2) * 20,
                //    option.KToken, !buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                //    algoIndex, currentTime, Constants.ORDER_TYPE_SLM, triggerPrice: Math.Round(lastPrice * (1 + _slPercent)/20, 2)*20 - 1, 
                //    broker: Constants.KOTAK, httpClient: _httpClientFactory== null? null: KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                orderTrio.Order = order;
                orderTrio.Option = option;
                //orderTrio.SLOrder = slorder;
                orderTrio.flag = 0;
                orderTrio.StopLoss = lastPrice * (1 + _slPercent);

                if (option.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    _callOrderTrios.Add(orderTrio);
                }
                else
                {
                    _putOrderTrios.Add(orderTrio);
                }
                OnTradeEntry(order);

                _pnl += order.AveragePrice * order.Quantity * (buyOrder ? -1 : 1);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
            }
            return option.TradeEntryPrice;
        }
        private void CancelOrder(DateTime currentTime, Order order)
        {
            MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));
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
        //private void LoadOptionsToTrade(DateTime currentTime)
        //{
        //    if (_activeCall == null && _activePut == null)
        //    {
        //        DayOfWeek wk = currentTime.DayOfWeek;
        //        switch (wk)
        //        {
        //            case DayOfWeek.Friday:
        //                _initialSL = 0.20m;
        //                break;
        //            case DayOfWeek.Monday:
        //                _initialSL = 0.20m;
        //                break;
        //            case DayOfWeek.Tuesday:
        //                _initialSL = 0.25m;
        //                break;
        //            case DayOfWeek.Wednesday:
        //                _initialSL = 0.4m;
        //                break;
        //            case DayOfWeek.Thursday:
        //                _initialSL = 0.4m;
        //                break;
        //            default:
        //                throw (new Exception("Check"));
        //        }

        //        DataLogic dl = new DataLogic();
        //        _expiryDate ??= dl.GetCurrentWeeklyExpiry(currentTime);

        //        decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
        //        _activeCall = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, atmStrike, "ce");
        //        _activePut = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, atmStrike, "pe");

        //    }
        //}
        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (OptionUniverse == null)
                {
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
#endif
                    //Load options asynchronously
                    Dictionary<uint, uint> mTokens;
                    DataLogic dl = new DataLogic();
                    SortedList<decimal, Instrument> ceList, peList;
                    //var OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken,
                    //    _baseInstrumentPrice, _maxDistanceFromBInstrument + 800, out ceList, out peList, out mappedTokens);

                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument + 200, out mTokens);

                    MappedTokens = mTokens;
                    //_activeCall = new List<Instrument>();
                    //_activePut = new List<Instrument>();
                    
                    ////foreach (var item in mappedTokens)
                    ////{
                    ////    MappedTokens.TryAdd(item.Key, item.Value);
                    ////}
                    //StringBuilder instrumentIds = new StringBuilder();
                    //Dictionary<uint, Instrument> optionDictionary = new Dictionary<uint, Instrument>();
                    //foreach(var option in OptionUniverse)
                    //{
                    //    instrumentIds.Append(option.InstrumentToken);
                    //    instrumentIds.Append(",");
                    //    optionDictionary.TryAdd(option.InstrumentToken, option);
                    //}
                    //instrumentIds.ToString().Remove(instrumentIds.ToString().Length - 2, 1);

                    //Dictionary<string, LTP> tokenPrices = ZObjects.kite.GetLTP(instrumentIds.ToString().Split(","));

                    //SortedList<decimal, Instrument> callPriceInstrument = new SortedList<decimal, Instrument>();
                    //SortedList<decimal, Instrument> putPriceInstrument = new SortedList<decimal, Instrument>();
                   
                    //foreach (var tprice in tokenPrices)
                    //{
                    //    optionDictionary[tprice.Value.InstrumentToken].LastPrice = tprice.Value.LastPrice;

                    //    if (optionDictionary[tprice.Value.InstrumentToken].InstrumentType.ToLower() == "ce")
                    //    {
                    //        callPriceInstrument.TryAdd(tprice.Value.LastPrice, optionDictionary[tprice.Value.InstrumentToken]);
                    //    }
                    //    else if (optionDictionary[tprice.Value.InstrumentToken].InstrumentType.ToLower() == "pe")
                    //    {
                    //        putPriceInstrument.TryAdd(tprice.Value.LastPrice, optionDictionary[tprice.Value.InstrumentToken]);
                    //    }
                    //}

                    //_activeCall = callPriceInstrument.First(x => x.Key > 100).Value;
                    //_activePut = putPriceInstrument.First(x => x.Key > 100).Value;
                    //_callReferencePrice = _activeCall.LastPrice;
                    //_putReferencePrice = _activePut.LastPrice;

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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (OptionUniverse != null)
                {
                    foreach (var options in OptionUniverse)
                    {
                        foreach (var option in options)
                        {
                            if (!SubscriptionTokens.Contains(option.Value.InstrumentToken))
                            {
                                SubscriptionTokens.Add(option.Value.InstrumentToken);
                                dataUpdated = true;
                            }
                        }
                    }
                    if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                    {
                        SubscriptionTokens.Add(_baseInstrumentToken);
                    }
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
        //private void UpdateInstrumentSubscription(DateTime currentTime)
        //{
        //    try
        //    {
                

        //        bool dataUpdated = false;
        //        if (OptionsDictionary != null)
        //        {
        //            foreach (var optionPair in OptionsDictionary)
        //            {
        //                if (!SubscriptionTokens.Contains(optionPair.Value.InstrumentToken))
        //                {
        //                    SubscriptionTokens.Add(optionPair.Value.InstrumentToken);
        //                    dataUpdated = true;
        //                }
        //            }
        //            if (!SubscriptionTokens.Contains(_baseInstrumentToken))
        //            {
        //                SubscriptionTokens.Add(_baseInstrumentToken);
        //            }
        //            if (dataUpdated)
        //            {
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
        //                Task task = Task.Run(() => OnOptionUniverseChange(this));
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
        //        Thread.Sleep(100);
        //    }
        //}

        public int AlgoInstance
        {
            get
            { return _algoInstance; }
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
        //private void LoadBaseInstrumentADX(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        //{
        //    try
        //    {
        //        lock (_bADX)
        //        {
        //            CandleSeries cs = new CandleSeries();

        //            //DataLogic dl = new DataLogic();

        //            //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, 
        //            //    lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

        //            List<Candle> historicalCandles = cs.LoadCandles(candlesCount,
        //              CandleType.Time, lastCandleEndTime, bToken.ToString(), _candleTimeSpan);

        //            foreach (var candle in historicalCandles)
        //            {
        //                _bADX.Process(candle);
        //            }
        //            _bADXLoadedFromDB = true;
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

        public void OnNext(Tick tick)
        {
            try
            {
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    //if(_stopTrade && (tick.Timestamp.Value.TimeOfDay <= new TimeSpan(10, 15, 00)))
                    //{
                    //    Reset();
                    //}
                    return;
                }
                ActiveTradeIntraday(tick);
                return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                return;
            }
        }
        //private void Reset()
        //{
        //    _stopTrade = false;
        //    _callOrderTrios = new Dictionary<decimal, OrderTrio>();
        //    _putOrderTrios = new Dictionary<decimal, OrderTrio>();
        //    SubscriptionTokens = new List<uint>();
        //    ActiveOptions = new List<Instrument>();
        //    _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
        //    TimeCandles = new Dictionary<uint, List<Candle>>();
        //    candleManger = new CandleManger(TimeCandles, CandleType.Time);
        //    candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

        //    StraddleUniverse = null;
        //    OptionsDictionary = null;
        //    MappedTokens = null;
        //}
        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
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
        }

        private void PublishLog(object sender, ElapsedEventArgs e)
        {
            //if (_bADX != null && _bADX.MovingAverage != null)
            //{
            //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
            //    String.Format("Current ADX: {0}", Decimal.Round(_bADX.MovingAverage.GetValue<decimal>(0), 2)),
            //    "Log_Timer_Elapsed");
            //}
            //if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Order != null
            //    && _straddlePutOrderTrio != null && _straddlePutOrderTrio.Order != null)
            //{
            //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
            //    String.Format("Call: {0}, Put: {1}. Straddle Profit: {2}. Current Ratio: {3}", _activeCall.LastPrice, _activePut.LastPrice,
            //    _straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice,
            //    _activeCall.LastPrice > _activePut.LastPrice ? Decimal.Round(_activeCall.LastPrice / _activePut.LastPrice, 2) : Decimal.Round(_activePut.LastPrice / _activeCall.LastPrice, 2)),
            //    "Log_Timer_Elapsed");

            //    Thread.Sleep(100);
            //}
        }

    }
}
