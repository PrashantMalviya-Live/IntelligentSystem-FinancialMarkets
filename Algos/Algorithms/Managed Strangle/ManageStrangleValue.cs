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
//using Google.Apis.Http;
using System.Net.Http;
using Google.Protobuf.WellKnownTypes;
using System.Globalization;
using System.Collections;
using System.Drawing;
using System.Reactive;
using System.Runtime.InteropServices;
//using InfluxDB.Client.Api.Domain;


namespace Algorithms.Algorithms
{
    public class ManageStrangleValue : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument> CallUniverse { get; set; }
        public SortedList<decimal, Instrument> PutUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ManageStrangleValue source);
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

        //public StrangleOrderLinkedList sorderList;
        public List<OrderTrio> _callOrderTrios;
        public List<OrderTrio> _putOrderTrios;
        public OrderTrio _hedgeCallOrderTrio;
        public OrderTrio _hedgePutOrderTrio;

        private decimal _referenceStraddleValue = 0;
        private decimal _referenceValueForStraddleShift;
        private const decimal THRESHOLD_FOR_POSITION_CHANGE_REFERENCE_STRADDLE_VALUE = 1.5M;
        public List<Order> _pastOrders;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private bool _intraday = true;
        private uint _baseInstrumentToken;
        private const uint VIX_TOKEN = 264969;
        private decimal _baseInstrumentPrice;
        private decimal _bInstrumentPreviousPrice;
        public const int CANDLE_COUNT = 30;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public readonly int _emaLength;
        private const int BASE_ADX_LENGTH = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;

        private decimal _upperLevel1;
        private decimal _upperLevel2;
        private decimal _upperLevel3;
        private decimal _lowerLevel1;
        private decimal _lowerLevel2;
        private decimal _lowerLevel3;

        private decimal _minDistanceforL1;
        private decimal _minDistanceL1L2;
        private decimal _minDistanceL2L3;

        private int _initialQty;
        private int _stepQty;
        private int _maxQty;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private decimal _targetProfitPoints;
        private decimal _stopLossPoints;
        private decimal _initialDelta;
        private decimal _minDelta;
        private decimal _maxDelta;
        private Instrument _activeCall;
        private int _strikePriceIncrement = 100;
        private Instrument _activePut;
        private IHttpClientFactory _httpClientFactory;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        private bool _adxPeaked = false;
        private User _user;
        public const AlgoIndex algoIndex = AlgoIndex.DeltaStrangleWithLevels;
        //TimeSpan candletimeframe;
        private bool _straddleShift;
        bool callLoaded = false;
        bool putLoaded = false;
        bool referenceCallLoaded = false;
        bool referencePutLoaded = false;
        private decimal _totalPnL = 0;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        //This is more suited for positional trading, as it is done based on levels, but it can be ended on daily basis too.
        public ManageStrangleValue(TimeSpan candleTimeSpan, uint baseInstrumentToken, DateTime? expiry, DateTime currentDate, decimal lowerLevel1,
            decimal lowerLevel2, decimal upperLevel1, decimal _upperLevel2, int initialQty, int stepQty, int maxQty, decimal initialDelta,
            decimal minDelta, decimal maxDelta, string uid, decimal targetProfit, decimal stopLoss, int algoInstance = 0,
            bool positionSizing = false, IHttpClientFactory httpClientFactory = null)
        {
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _httpClientFactory = httpClientFactory;
            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();
            CandleSeries candleSeries = new CandleSeries();
            _positionSizing = positionSizing;
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            
            //_lowerLevel2 = lowerLevel1;
            //_lowerLevel3 = lowerLevel2;
            //_upperLevel2 = upperLevel1;
            //_upperLevel3 = _upperLevel2;
            _initialDelta = initialDelta;
            _minDelta = minDelta;
            _maxDelta = maxDelta;
            _initialQty = initialQty;
            _stepQty = stepQty;
            _maxQty = maxQty;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;

            ZConnect.Login();
            _user = KoConnect.GetUser(userId: uid);

            SetInitialDeltaSLTP(currentDate, _initialDelta);

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _initialQty, _maxQty, _stepQty, _maxDelta, 0, _minDelta, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, _lowerLevel2, _lowerLevel3, _upperLevel2, _upperLevel3, Arg9: _user.UserId,
                positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);



#if !BACKTEST
            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();
#endif
        }

        //public void LoadActiveOrders(List<OrderTrio> activeOrderTrios)
        //{
        //    if (activeOrderTrios != null && activeOrderTrios.Count > 0)
        //    {
        //        foreach (var orderTrio in activeOrderTrios)
        //        {
        //            DataLogic dl = new DataLogic();
        //            Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);

        //            ActiveOptions.Add(option);
        //            orderTrio.Option = option;

        //            if (option.InstrumentType.ToLower() == "ce")
        //            {
        //                _callOrderTrios.Add(orderTrio);
        //            }
        //            else if (option.InstrumentType.ToLower() == "pe")
        //            {
        //                _putOrderTrios.Add(orderTrio);
        //            }
        //        }
        //        _activeCall = _callOrderTrios.Last().Option;
        //        _activePut = _putOrderTrios.Last().Option;
        //    }
        //}


        /// <summary>
        /// Logic:
        ///
        /// Sell strangle based on ATM premium on Friday, and 1.5 times..or 300 points away from total straddle range.
        /// Mark 2 critical levels: Use previous day swing, previous week swing..swing on daily chart, or 2 hrs chart.
        /// Initial levels are used for one adjustment factor, which is high
        /// Outerlevels are used for another adjustment factor which is lower
        /// and once outler level is crossed..book the profit or SL
        /// Target points for each day
        /// Start with small lots and increase in 2 times to better manage the Pnl
        /// </summary>
        /// <param name="tick"></param>
        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken || tick.InstrumentToken == VIX_TOKEN) ?
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
                    LocateLevels(currentTime);
                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    MonitorCandles(tick, currentTime);


                    //Update option price
                    foreach (Instrument option in CallUniverse.Values)
                    {
                        if (option.InstrumentToken == tick.InstrumentToken)
                        {
                            option.LastPrice = tick.LastPrice;
                            break;
                        }
                    }
                    foreach (Instrument option in PutUniverse.Values)
                    {
                        if (option.InstrumentToken == tick.InstrumentToken)
                        {
                            option.LastPrice = tick.LastPrice;
                            break;
                        }
                    }

                    //Take trade after 9:20 AM only
                    if (currentTime.TimeOfDay <= new TimeSpan(09, 20, 00))
                    {
                        return;
                    }

                    ///Logic:
                    ///Take trade based on initial delta and level 2, which ever is more conservative
                    ///Vary quantity based on levels, but not levels
                    if (_baseInstrumentPrice >= _lowerLevel2 && _baseInstrumentPrice <= _upperLevel2)
                    {
                        decimal outerlevelDistance = 0;
                        if (_activeCall == null && _activePut == null)
                        {
                            outerlevelDistance = Math.Min(_upperLevel2 - _baseInstrumentPrice, _baseInstrumentPrice - _lowerLevel2);

                            //if (_activeCall == null)
                            //{

                                decimal initialCallStrike = GetInitialStrike(InstrumentType.CE, currentTime, CallUniverse,
                                    Math.Floor((_baseInstrumentPrice + outerlevelDistance) / _strikePriceIncrement) * _strikePriceIncrement);

                            decimal initialPutStrike = GetInitialStrike(InstrumentType.PE, currentTime, PutUniverse,
                                Math.Ceiling((_baseInstrumentPrice - outerlevelDistance) / _strikePriceIncrement) * _strikePriceIncrement);

                            if(initialCallStrike ==0 || initialPutStrike == 0)
                            {
                                return;
                            }
                            decimal cQty = _initialQty, pQty = _initialQty;
                            if(initialCallStrike - _upperLevel1 > _lowerLevel1 - initialPutStrike)
                            {
                                pQty = Math.Abs(Math.Round(cQty * ((_lowerLevel1 - initialPutStrike) / (initialCallStrike - _upperLevel1)), 0));
                                pQty = pQty == 0 ? _initialQty : pQty;
                            }
                            else
                            {
                                cQty = Math.Abs(Math.Round(pQty * ((initialCallStrike - _upperLevel1) / (_lowerLevel1 - initialPutStrike)), 0));

                                cQty = cQty ==0? _initialQty : cQty;
                            }
                            //cQty = _initialQty; pQty = _initialQty;
                            OrderTrio ot = TradeEntry(CallUniverse[initialCallStrike] , currentTime, Convert.ToInt32(cQty), false);
                                if (ot != null)
                                {
                                    _activeCall = ot.Option;
                                    _callOrderTrios ??= new List<OrderTrio>();
                                    _callOrderTrios.Add(ot);
                                }
                            //}
                            //if (_activePut == null)
                            //{
                                //decimal initialPutStrike = GetInitialStrike(InstrumentType.PE, currentTime, PutUniverse,
                                //    Math.Round((_baseInstrumentPrice - outerlevelDistance) / _strikePriceIncrement) * _strikePriceIncrement);

                                ot = TradeEntry(PutUniverse[initialPutStrike], currentTime, Convert.ToInt32(pQty), false);
                                if (ot != null)
                                {
                                    _activePut = ot.Option;
                                    _putOrderTrios ??= new List<OrderTrio>();
                                    _putOrderTrios.Add(ot);
                                }
                            //}
                            if (_activeCall != null && _activePut != null)
                            {
                                _referenceStraddleValue = _referenceStraddleValue == 0 ? _activeCall.LastPrice + _activePut.LastPrice : _referenceStraddleValue;
                                DataLogic dl = new DataLogic();
                                dl.UpdateArg8(_algoInstance, _referenceStraddleValue);
                            }
                        }
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
            uint token = e.InstrumentToken;
            decimal lastPrice = e.ClosePrice;
            DateTime currentTime = e.CloseTime;

            try
            {
                ///Logic
                ///IF base instrument is within level1, move closure
                ///if banse instrument is outside level 1, move further.
                ///option should never come closure then level2 , else move further.

                if (token == _baseInstrumentToken && _activeCall != null && _activePut != null)
                {
                    //This is not an active trade zone, however manage the mtm swings with threshold basis trade
                    //Use position sizing. Start with small lot and increase, but do not actively manage here
                    //if (_baseInstrumentPrice > _lowerLevel1 - _strikePriceIncrement && _baseInstrumentPrice < _upperLevel1 + _strikePriceIncrement)
                    //{
                        int callTradedQty = _callOrderTrios.Sum(x => x.Order.Quantity) / (int)_activeCall.LotSize;
                        int putTradedQty = _putOrderTrios.Sum(x => x.Order.Quantity) / (int)_activePut.LotSize;

                        //increase position size when it is between the lower levels.

                        if (_activePut.LastPrice > _activeCall.LastPrice * 2m)
                        {
                            //IF base instrument is between level 1 on both side, then move options closure, if they are outside level1.
                            //Never move options closure to level1 as long as base instrument is within level1

                            

                            Instrument nextStrikeCall = CallUniverse.FirstOrDefault(x => x.Value.LastPrice < _activePut.LastPrice && x.Value.LastPrice != 0).Value;
                        decimal cdelta = (decimal)nextStrikeCall.UpdateDelta((double)nextStrikeCall.LastPrice, 0.1, e.CloseTime, (double)_baseInstrumentPrice);

                        if (nextStrikeCall.Strike < _upperLevel2 && cdelta > _minDelta)
                            {
                                //move put further
                                Instrument nextStrikePut = PutUniverse.LastOrDefault(x => x.Value.LastPrice < _activeCall.LastPrice && x.Value.LastPrice != 0).Value;
                                if (nextStrikePut != null)
                                {
                                    int qty = _putOrderTrios.Sum(x => x.Order.Quantity) / (int)_activeCall.LotSize;
                                    DataLogic dl = new DataLogic();
                                    foreach (OrderTrio orderTrio in _putOrderTrios)
                                    {
                                        TradeEntry(orderTrio.Option, currentTime, orderTrio.Order.Quantity / (int)_activeCall.LotSize, true);
                                        dl.DeActivateOrderTrio(orderTrio);
                                    }
                                    _putOrderTrios.Clear();
                                    _activePut = nextStrikePut;
                                    _putOrderTrios.Add(TradeEntry(nextStrikePut, currentTime, qty, false));
                                }
                            }
                            else
                            {
                                nextStrikeCall = CallUniverse.FirstOrDefault(x => x.Value.LastPrice < _activePut.LastPrice && x.Value.LastPrice != 0).Value;
                                if (nextStrikeCall != null)
                                {
                                    int qty = _callOrderTrios.Sum(x => x.Order.Quantity) / (int)_activeCall.LotSize;
                                    DataLogic dl = new DataLogic();
                                    foreach (OrderTrio orderTrio in _callOrderTrios)
                                    {
                                        TradeEntry(orderTrio.Option, currentTime, orderTrio.Order.Quantity / (int)orderTrio.Option.LotSize, true);
                                        dl.DeActivateOrderTrio(orderTrio);
                                    }
                                    _callOrderTrios.Clear();
                                    _activeCall = nextStrikeCall;
                                    _callOrderTrios.Add(TradeEntry(nextStrikeCall, currentTime, qty, false));
                                }
                            }
                        }
                        else if (_activeCall.LastPrice > _activePut.LastPrice * 2m)
                        {
                            //IF base instrument is between level 1 on both side, then move options closure, if they are outside level1.
                            //Never move options closure to level1 as long as base instrument is within level1

                            
                            Instrument nextStrikePut = PutUniverse.LastOrDefault(x => x.Value.LastPrice < _activeCall.LastPrice && x.Value.LastPrice != 0).Value;
                            decimal pdelta = (decimal)nextStrikePut.UpdateDelta((double)nextStrikePut.LastPrice, 0.1, e.CloseTime, (double)_baseInstrumentPrice);

                        if (nextStrikePut.Strike > _lowerLevel2 && pdelta * -1 > _minDelta)
                            {
                                //move call further
                                Instrument nextStrikeCall = CallUniverse.First(x => x.Value.LastPrice < _activePut.LastPrice && x.Value.LastPrice != 0).Value;
                                if (nextStrikeCall != null)
                                {
                                    int qty = _callOrderTrios.Sum(x => x.Order.Quantity) / (int)_activeCall.LotSize;
                                    DataLogic dl = new DataLogic();
                                    foreach (OrderTrio orderTrio in _callOrderTrios)
                                    {
                                        TradeEntry(orderTrio.Option, currentTime, orderTrio.Order.Quantity / (int)orderTrio.Option.LotSize, true);
                                        dl.DeActivateOrderTrio(orderTrio);
                                    }
                                    _callOrderTrios.Clear();
                                    _activeCall = nextStrikeCall;
                                    _callOrderTrios.Add(TradeEntry(nextStrikeCall, currentTime, qty, false));
                                }
                            }
                            else
                            {
                                nextStrikePut = PutUniverse.LastOrDefault(x => x.Value.LastPrice < _activeCall.LastPrice && x.Value.LastPrice != 0).Value;
                                if (nextStrikePut != null)
                                {
                                    int qty = _putOrderTrios.Sum(x => x.Order.Quantity) / (int)_activeCall.LotSize;
                                    DataLogic dl = new DataLogic();
                                    foreach (OrderTrio orderTrio in _putOrderTrios)
                                    {
                                        TradeEntry(orderTrio.Option, currentTime, orderTrio.Order.Quantity / (int)orderTrio.Option.LotSize, true);
                                        dl.DeActivateOrderTrio(orderTrio);
                                    }
                                    _putOrderTrios.Clear();
                                    _activePut = nextStrikePut;
                                    _putOrderTrios.Add(TradeEntry(nextStrikePut, currentTime, qty, false));
                                }
                            }
                        }
                        else if (_activeCall.LastPrice + _activePut.LastPrice > _referenceStraddleValue * THRESHOLD_FOR_POSITION_CHANGE_REFERENCE_STRADDLE_VALUE
                                                && callTradedQty <= _maxQty - _stepQty && putTradedQty <= _maxQty - _stepQty)
                        {
                            OrderTrio orderTrio = TradeEntry(_activeCall, currentTime, _stepQty, false);
                            _callOrderTrios.Add(orderTrio);

                            orderTrio = TradeEntry(_activePut, currentTime, _stepQty, false);
                            _putOrderTrios.Add(orderTrio);
                        }
                    //}
                    //else
                    //{
                    
                        
                    //    //TriggerEODPositionClose(currentTime, true);
                    //}
                }
                //Closes all postions at 3:20 PM
                if (_intraday)
                {
                    TriggerEODPositionClose(currentTime);
                }
                else
                {
                    //Convert to Iron fly at 3:20 PM
                    HedgeStraddle(currentTime);
                }
            }
            catch (Exception ex)
            {
                //throw ex;
            }
        }
        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, int tradeQty, bool buyOrder, string tag = "")
        {
            OrderTrio orderTrio = null;
            try
            {
                //ENTRY ORDER - Sell ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                    option.KToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Tag: tag, product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                 httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                //Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                // option.KToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                // algoIndex, currentTime, Tag: tag, product: _intraday ? Constants.PRODUCT_MIS : Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                // httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }

#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");

#endif
                orderTrio = new OrderTrio();
                orderTrio.Order = order;
                //orderTrio.SLOrder = slOrder;
                orderTrio.Option = option;
                orderTrio.EntryTradeTime = currentTime;
                OnTradeEntry(order);

                DataLogic dl = new DataLogic();
                orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);

                _totalPnL += order.AveragePrice * order.Quantity * (buyOrder ? -1 : 1);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
            }
            return orderTrio;
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

        /// <summary>
        /// Check option at level2 and intial delta, which ever is lower take that trade
        /// </summary>
        /// <param name="instrumentType"></param>
        private decimal GetInitialStrike(InstrumentType instrumentType, DateTime currentTime, SortedList<decimal, Instrument> optionUniverse, decimal outerLevel2)
        {
            double optionDelta = 0;
            Instrument option = null;
            decimal initialStrike = 0;

            //if (optionUniverse.ContainsKey(outerLevel2))
            //{
            //    option = optionUniverse[outerLevel2];
            //    orderTrio = TradeEntry(option, currentTime, _initialQty, false);
            //}

            if (optionUniverse.ContainsKey(outerLevel2))
            {
                option = optionUniverse[outerLevel2];

                if (option.LastPrice != 0)
                {
                    initialStrike = option.Strike;

                    //optionDelta = option.UpdateDelta(Convert.ToDouble(option.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));
                    //if (double.IsNaN(optionDelta))
                    //{
                    //    optionDelta = 0;
                    //}
                }
            }


            //take sell trade
            if (optionDelta != 0 && Math.Abs(optionDelta) < Convert.ToDouble(_initialDelta))
            {
                //Sell option with initial delta
                initialStrike = option.Strike;

                //orderTrio = TradeEntry(option, currentTime, _initialQty, false);
            }
            else
            {
                var optionSubset = instrumentType == InstrumentType.CE ? optionUniverse.Where(x => x.Key > outerLevel2).OrderBy(x => x.Key) : optionUniverse.Where(x => x.Key < outerLevel2).OrderByDescending(x => x.Key);
                foreach (var optionSet in optionSubset)
                {
                    Instrument o = optionSet.Value;

                    if (o.LastPrice != 0)
                    {
                        double od = o.UpdateDelta(Convert.ToDouble(o.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));
                        if (!double.IsNaN(od) && Math.Abs(od) <= Convert.ToDouble(_initialDelta))// && Math.Abs(od) > Convert.ToDouble(_minDelta))
                        {
                            //Sell option with initial delta
                            
                            initialStrike= o.Strike;
                            //orderTrio = TradeEntry(o, currentTime, _initialQty, false);
                            break;
                        }
                    }
                }

                //Sell option at level 2
            }
            return initialStrike;// orderTrio;
        }
        private void LocateLevels(DateTime currentTime)
        {
            if (_lowerLevel1 == 0 || _upperLevel1 == 0)
            {
                //#if MARKET
                List<Historical> bCandles = ZObjects.kite.GetHistoricalData(_baseInstrumentToken.ToString(), currentTime.Date.AddDays(-30), currentTime.Date, "60minute");
                List<TimeFrameCandle> candles = JoinHistoricals(bCandles, 4);

                List<Historical> bCandlesDaily = ZObjects.kite.GetHistoricalData(_baseInstrumentToken.ToString(), currentTime.Date.AddDays(-30), currentTime.Date, "day");

                bool l1 = false, l2 = false, l3 = false, u1 = false, u2 = false, u3 = false;

                foreach (var c in candles)
                {
                    if (!l1)
                    {
                        if (_lowerLevel1 == 0 || c.LowPrice < _lowerLevel1)
                        {
                            _lowerLevel1 = c.LowPrice;
                        }
                        else if (_lowerLevel1 < c.LowPrice && _lowerLevel1 < _baseInstrumentPrice - _minDistanceforL1)
                        {
                            l1 = true;
                        }
                    }
                    else if (!l2)
                    {
                        if (_lowerLevel2 == 0 || c.LowPrice < _lowerLevel2)
                        {
                            _lowerLevel2 = c.LowPrice;
                        }
                        else if (_lowerLevel2 < c.LowPrice && _lowerLevel2 < _lowerLevel1 - _minDistanceL1L2)
                        {
                            l2 = true;
                        }
                    }

                    if (!u1)
                    {
                        if (_upperLevel1 < c.HighPrice || _upperLevel1 == 0)
                        {
                            _upperLevel1 = c.HighPrice;
                        }
                        else if (_upperLevel1 > c.HighPrice && _upperLevel1 > _baseInstrumentPrice + _minDistanceforL1)
                        {
                            u1 = true;
                        }
                    }
                    else if (!u2)
                    {
                        if (_upperLevel2 < c.HighPrice || _upperLevel2 == 0)
                        {
                            _upperLevel2 = c.HighPrice;
                        }
                        else if (_upperLevel2 > c.HighPrice && _upperLevel2 > _upperLevel1 + _minDistanceL1L2)
                        {
                            u2 = true;
                        }
                    }


                }

                _lowerLevel1 = l1 ? _lowerLevel1 : _baseInstrumentPrice - _minDistanceforL1;
                _lowerLevel2 = l2 ? _lowerLevel2 : _lowerLevel1 - _minDistanceforL1;
                _upperLevel1 = u1 ? _upperLevel1 : _baseInstrumentPrice + _minDistanceL1L2;
                _upperLevel2 = u2 ? _upperLevel2 : _upperLevel1 + _minDistanceL1L2;

                foreach (var c in bCandlesDaily)
                {
                    if (!l3)
                    {
                        if (_lowerLevel3 == 0 || c.Low < _lowerLevel3)
                        {
                            _lowerLevel3 = c.Low;
                        }
                        else if (_lowerLevel3 < c.Low && _lowerLevel3 < _lowerLevel2 - _minDistanceL2L3)
                        {
                            l3 = true;
                        }
                    }
                    if (!u3)
                    {
                        if (_upperLevel3 < c.High || _upperLevel3 == 0)
                        {
                            _upperLevel3 = c.High;
                        }
                        else if (_upperLevel3 > c.High && _upperLevel3 > _upperLevel2 + _minDistanceL2L3)
                        {
                            u3 = true;
                        }
                    }
                }

                //_lowerLevel3 = l3 ? _lowerLevel3 : _lowerLevel2 - _minDistanceL2L3;
                //_upperLevel3 = u3 ? _upperLevel3 : _lowerLevel2 + _minDistanceL2L3;

                decimal minimuminacycle = bCandlesDaily.TakeLast(20).Min(x => x.Low);
                if (_lowerLevel3 > minimuminacycle && _lowerLevel3 - minimuminacycle < 250)
                {
                    _lowerLevel3 = minimuminacycle;
                }
                decimal maxinacycle = bCandlesDaily.TakeLast(20).Max(x => x.High);
                if (maxinacycle > _upperLevel3 && maxinacycle - _upperLevel3 < 250)
                {
                    _upperLevel3 = maxinacycle;
                }


                _lowerLevel1 = Math.Floor(_lowerLevel1 / _strikePriceIncrement) * _strikePriceIncrement;
                _lowerLevel2 = Math.Floor(_lowerLevel2 / _strikePriceIncrement) * _strikePriceIncrement;
                _lowerLevel3 = Math.Floor(_lowerLevel3 / _strikePriceIncrement) * _strikePriceIncrement;
                _upperLevel1 = Math.Ceiling(_upperLevel1 / _strikePriceIncrement) * _strikePriceIncrement;
                _upperLevel2 = Math.Ceiling(_upperLevel2 / _strikePriceIncrement) * _strikePriceIncrement;
                _upperLevel3 = Math.Ceiling(_upperLevel3 / _strikePriceIncrement) * _strikePriceIncrement;

                ////put l2 at 200 points away from l1, or at l3
                //_lowerLevel2 = Math.Max(_lowerLevel1 - 2 * _strikePriceIncrement, _lowerLevel3);
                //_upperLevel2 = Math.Min(_upperLevel1 + 2 * _strikePriceIncrement, _upperLevel3);


                DataLogic dl = new DataLogic();
                dl.UpdateAlgoParamaters(algoInstance: _algoInstance, arg1: _lowerLevel1, arg2: _lowerLevel2, arg3: _lowerLevel3, arg4: _upperLevel1, arg5: _upperLevel2, arg6: _upperLevel3);
            }
        }

        private List<TimeFrameCandle> JoinHistoricals(List<Historical> bCandles, int numberOfHistoricalsToJoin)
        {
            List<TimeFrameCandle> c = new List<TimeFrameCandle>();
            TimeFrameCandle tC = null;
            int cc = 0;
            for (int i = bCandles.Count - 1; i >= 0; i--)
            {
                if (cc % numberOfHistoricalsToJoin == 0)
                {
                    Historical h = bCandles[i];
                    tC = new TimeFrameCandle();
                    tC.InstrumentToken = h.InstrumentToken;
                    tC.OpenPrice = h.Open;
                    tC.CloseTime = h.TimeStamp;
                    tC.ClosePrice = h.Close;
                    tC.HighPrice = h.High;
                    tC.LowPrice = h.Low;
                    c.Add(tC);
                }
                else
                {
                    tC = c.Last();
                    tC.HighPrice = Math.Max(tC.HighPrice, bCandles[i].High);
                    tC.LowPrice = Math.Min(tC.LowPrice, bCandles[i].Low);
                    tC.ClosePrice = tC.ClosePrice;
                }
                cc++;
            }
            return c;
        }

        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }


        private void SetInitialDeltaSLTP(DateTime currentTime, decimal initialDelta = 0)
        {
            int dte = (_expiryDate.Value.Date - currentTime.Date).Days;

            if (_baseInstrumentToken.ToString() == Constants.BANK_NIFTY_TOKEN)
            {
                _strikePriceIncrement = 100;
                _minDistanceforL1 = 300;
                _minDistanceL1L2 = 200;
                _minDistanceL2L3 = 200;

                if (dte >= 5)
                {
                    _targetProfit = 30 * _tradeQty;
                    _initialDelta = 0.15m;
                    _maxDelta = 0.45m;
                }
                else if (dte >= 3)
                {
                    _targetProfit = 30 * _tradeQty;
                    _initialDelta = 0.15m;
                    _maxDelta = 0.45m;
                }
                else if (dte >= 2)
                {
                    _targetProfit = 30 * _tradeQty;
                    _initialDelta = 0.15m;
                    _maxDelta = 0.45m;
                }
                else if (dte >= 1)
                {
                    _targetProfit = 40 * _tradeQty;
                    _initialDelta = 0.15m;
                    _maxDelta = 0.45m;
                }
                else
                {
                    _targetProfit = 50 * _tradeQty;
                    _initialDelta = 0.15m;
                    _maxDelta = 0.4m;
                }
            }
            else if (_baseInstrumentToken.ToString() == Constants.NIFTY_TOKEN || _baseInstrumentToken.ToString() == Constants.FINNIFTY_TOKEN)
            {
                _strikePriceIncrement = 50;
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckCandleStartTime");
                Thread.Sleep(100);
                Environment.Exit(0);
                lastEndTime = DateTime.Now;
                return null;
            }
        }

        private void TriggerEODPositionClose(DateTime currentTime, bool closeAll = false)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00) || closeAll)// && _referenceStraddleValue != 0)
            {
                DataLogic dl = new DataLogic();
                _referenceStraddleValue = 0;
                if (_callOrderTrios != null)
                {
                    foreach (OrderTrio orderTrio in _callOrderTrios)
                    {
                        Instrument option = orderTrio.Option;

                        TradeEntry(option, currentTime, orderTrio.Order.Quantity / Convert.ToInt32(option.LotSize), true);
                        dl.DeActivateOrderTrio(orderTrio);
                    }
                    _callOrderTrios.Clear();
                    _callOrderTrios = null;
                }
                if (_putOrderTrios != null)
                {
                    foreach (OrderTrio orderTrio in _putOrderTrios)
                    {
                        Instrument option = orderTrio.Option;

                        TradeEntry(option, currentTime, orderTrio.Order.Quantity / Convert.ToInt32(option.LotSize), true);
                        dl.DeActivateOrderTrio(orderTrio);
                    }
                    _putOrderTrios.Clear();
                    _putOrderTrios = null;
                }

                dl.UpdateAlgoPnl(_algoInstance, _totalPnL);
                _stopTrade = true;
                _stopLossHit = true;

                dl.DeActivateAlgo(_algoInstance);
            }
        }
        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (CallUniverse == null || PutUniverse == null)
                {
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
#endif
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();

                    Dictionary<uint, uint> mappedTokens;
                    SortedList<decimal, Instrument> calls, puts;
                    dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, 2000, out calls, out puts, out mappedTokens);


                    //for(int i= 0;i<calls.Count;)
                    //{
                    //    if (calls.ElementAt(i).Key < _upperLevel2)
                    //    {
                    //        calls.Remove(calls.ElementAt(i).Key);
                    //    }
                    //    else
                    //    {
                    //        i++;
                    //    }
                    //}
                    //for (int i = 0; i < puts.Count;)
                    //{
                    //    if (puts.ElementAt(i).Key > _lowerLevel2)
                    //    {
                    //        puts.Remove(puts.ElementAt(i).Key);
                    //    }
                    //    else
                    //    {
                    //        i++;
                    //    }
                    //}

                    CallUniverse = calls;
                    PutUniverse = puts;

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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
            }
        }

        private void HedgeStraddle(DateTime? currentTime)
        {
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 20, 00))
            {
                //buy call and put at total sum range
                if (_callOrderTrios != null && _putOrderTrios != null)
                {
                    decimal strangleRange = _activeCall.LastPrice + _activePut.LastPrice;
                    decimal ceHedgeStrike = Math.Round((_baseInstrumentPrice + strangleRange) / _strikePriceIncrement, 0) * _strikePriceIncrement;
                    decimal peHedgeStrike = Math.Round((_baseInstrumentPrice - strangleRange) / _strikePriceIncrement, 0) * _strikePriceIncrement;


                    Instrument callHedgeOption = null, putHedgeOption = null;
                    if (!CallUniverse.ContainsKey(ceHedgeStrike))
                    {
                        DataLogic dl = new DataLogic();
                        callHedgeOption = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, ceHedgeStrike, "ce");
                    }
                    if (!PutUniverse.ContainsKey(peHedgeStrike))
                    {
                        DataLogic dl = new DataLogic();
                        putHedgeOption = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, ceHedgeStrike, "pe");
                    }

#if BACKTEST && local
                    List<Historical> futurePrices = ZObjects.kite.GetHistoricalData(callHedgeOption.InstrumentToken.ToString(), currentTime.Value, currentTime.Value, "minute");
                    callHedgeOption.LastPrice = futurePrices[0].Close;
                    futurePrices = ZObjects.kite.GetHistoricalData(putHedgeOption.InstrumentToken.ToString(), currentTime.Value, currentTime.Value, "minute");
                    putHedgeOption.LastPrice = futurePrices[0].Close;

#endif

                    int callQty = _callOrderTrios.Sum(x => x.Order.Quantity);
                    int putQty = _putOrderTrios.Sum(x => x.Order.Quantity);
                    //Hedge Call trade
                    TradeEntry(callHedgeOption, currentTime.Value, callQty, true, tag: "hedge");
                    TradeEntry(callHedgeOption, currentTime.Value, putQty, true, tag: "hedge");

                    _stopTrade = true;
                }
            }
            else if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(09, 20, 00))
            {
                if (_hedgeCallOrderTrio != null && _hedgePutOrderTrio != null)
                {
#if BACKTEST && local
                    List<Historical> futurePrices = ZObjects.kite.GetHistoricalData(_hedgeCallOrderTrio.Option.InstrumentToken.ToString(), currentTime.Value, currentTime.Value, "minute");
                    _hedgeCallOrderTrio.Option.LastPrice = futurePrices[0].Close;
                    futurePrices = ZObjects.kite.GetHistoricalData(_hedgePutOrderTrio.Option.InstrumentToken.ToString(), currentTime.Value, currentTime.Value, "minute");
                    _hedgePutOrderTrio.Option.LastPrice = futurePrices[0].Close;

#endif
                    DataLogic dl = new DataLogic();
                    dl.DeActivateOrderTrio(_hedgeCallOrderTrio);
                    dl.DeActivateOrderTrio(_hedgePutOrderTrio);

                    //Hedge Call trade
                    TradeEntry(_hedgeCallOrderTrio.Option, currentTime.Value, _hedgeCallOrderTrio.Order.Quantity, false, tag: "hedge");
                    TradeEntry(_hedgePutOrderTrio.Option, currentTime.Value, _hedgePutOrderTrio.Order.Quantity, false, tag: "hedge");
                }
            }

        }

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (CallUniverse != null)
                {
                    foreach (var option in CallUniverse)
                    {
                        if (!SubscriptionTokens.Contains(option.Value.InstrumentToken))
                        {
                            SubscriptionTokens.Add(option.Value.InstrumentToken);
                            dataUpdated = true;
                        }
                    }
                }
                if (PutUniverse != null)
                {
                    foreach (var option in PutUniverse)
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
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(VIX_TOKEN))
                {
                    SubscriptionTokens.Add(VIX_TOKEN);
                    dataUpdated = true;
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

        public void OnNext(Tick tick)
        {
            try
            {
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
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

            //if (_activeCall != null && _activePut != null)
            //{
            //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
            //    String.Format("Call Delta: {0}, Put Delta: {1}. Straddle Profit: {2}", Math.Round(_activeCall.Delta, 2) , Math.Round(_activePut.Delta, 2),
            //    _callOrderTrio.Order.AveragePrice + _putOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice), "Log_Timer_Elapsed");
            //}

            //Thread.Sleep(100);
        }

    }
}
