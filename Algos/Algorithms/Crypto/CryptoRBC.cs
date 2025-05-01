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
//using FirebaseAdmin.Messaging;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Net.NetworkInformation;
using System.Drawing;
using System.Collections;
using System.Reflection.Metadata;
using Algos.Utilities;
//using InfluxDB.Client.Api.Domain;

namespace Algorithms.Algorithms
{
    public class CryptoRBC : ICZMQ//, IObserver<Tick>
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(CryptoRBC source);
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

        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        private Dictionary<int, List<CryptoOrders>> _cryptoOrders;

        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public Queue<decimal> _indexValues;

        List<decimal> _downlowswingindexvalues;
        List<decimal> _uphighswingindexvalues;
        decimal _downswingindexvalue = 0;
        decimal _upwingindexvalue = 0;

        //public class PriceTime
        //{
        //    public decimal LastPrice;
        //    public DateTime? TradeTime;
        //}

        public List<PriceTime> _bValues;
        //public Dictionary<decimal, DateTime> _bValues;


        //public decimal _minDistanceFromBInstrument;
        //public decimal _maxDistanceFromBInstrument;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private int _lotSize = 15;
        //private Instrument _activeFuture;
        private Instrument _referenceIndex;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);

        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);

        public List<Trigger> _triggers = new List<Trigger>();
        public class Trigger
        {
            public decimal tradePrice;
            public bool up;
            public decimal sl;
            public decimal tp;
            public DateTime? lastBreakoutTime;
            public bool slOrder = false;
        }
        public const decimal BAND_THRESHOLD = 10m;
        //public const decimal MINIMUM_TARGET = 50m;// 5 mins : 30; 15 mins: 40m;
        private decimal _minimumTarget;
        public readonly TimeSpan TIME_THRESHOLD = new TimeSpan(0, 1, 0);
        public readonly TimeSpan BREAKOUT_TIME_THRESHOLD = new TimeSpan(0, 1, 5);
        public readonly TimeSpan BREAKOUT_TIME_LIMIT = new TimeSpan(0, 7, 0);

        public const decimal PERCENT_RETRACEMENT = 0.0008m; //0.08% retracement of index value or 50 points

        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private const decimal SCH_UPPER_THRESHOLD = 70;
        private const decimal THRESHOLD = 40;
        //private TimeSpan TIME_THRESHOLD = new TimeSpan(0, 3, 0);
        private const decimal PROFIT_TARGET = 50;
        private const decimal STOP_LOSS = 50;
        private const decimal VALUES_THRESHOLD = 25000;
        private const decimal SCH_LOWER_THRESHOLD = 30;
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private DateTime? _upBreakoutTime;
        private bool _putTriggered = false;
        private bool _callTrigered = false;
        private bool _tradeSuspended = false;
        private DateTime? _downBreakoutTime;
        private Candle _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.RangeBreakoutCandle;
        private decimal _targetProfitForUpperBreakout;
        private decimal _stopLossForUpperBreakout;
        private decimal _targetProfitForLowerBreakout;
        private decimal _stopLossForLowerBreakout;
        private bool? _firstCandleOutsideRange;
        CandleManger candleManger;
        Dictionary<string, List<Candle>> FutureTimeCandles;
        private IHttpClientFactory _httpClientFactory;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        private List<Trigger> _longTrigerred;
        private List<Trigger> _shortTrigerred;
        //private decimal _shortSL, _shortTP;
        private decimal _firstBottomPrice = 0;
        private decimal _firstTopPrice = 0;
        private User _user;
        private DateTime _slhitTime;


        private const decimal QUALIFICATION_ZONE_THRESHOLD = 15;
        private const decimal TRADING_ZONE_THRESHOLD = 8; // This should be % of qualification zone. 50% is good.
        private const decimal RR_BREAKDOWN = 1; // Risk Reward
        private const decimal RR_BREAKOUT = 2; // Risk Reward

        private const decimal MINIMUM_TARGET = 100;
        private decimal _pnl = 0;
        private Instrument _activeFuture;
        private PriceRange _resistancePriceRange;
        private PriceRange _supportPriceRange;

        //Arg 3: current range uppper, Arg4: current range lower, Arg 5: current breakoutmode
        //private PriceRange _rbcIndicator.CurrentPriceRange;
        //private PriceRange _rbcIndicator.PreviousPriceRange;
        private Breakout _currentBreakOutMode;

        private Dictionary<string, RangeBreakoutRetraceIndicator> _futureRBCIndicator;

        private Dictionary<string, TradedCryptoFuture> _futures;
        private Dictionary<string, List<CryptoOrderTrio>> _cryptoOrderTrios;

        private int _netQty = 0;

        public CryptoRBC(string baseSymbols, DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss, AlgoIndex algoIndex,
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {

            //DEConnect.Login();
            //_user = DEConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            //_candleTimeSpan = candleTimeSpan;


            _stopTrade = true;
            _trailingStopLoss = _stopLossForLowerBreakout = _stopLossForUpperBreakout = stopLoss;
            _stopLossForLowerBreakout = _stopLossForUpperBreakout = targetProfit;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            SetUpInitialData(expiry, baseSymbols, algoInstance);

            _minimumTarget = targetProfit;


            //#if local
            //            _dateLoaded = new List<DateTime>();
            //            LoadPAInputsForTest();
            //#endif

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }
        private void SetUpInitialData(DateTime? expiry, string bSymbol, int algoInstance = 0)
        {
            _expiryDate = expiry;
            _cryptoOrders = new Dictionary<int, List<CryptoOrders>>();

            _uphighswingindexvalues = new List<decimal>();
            _downlowswingindexvalues = new List<decimal>();

            _futures = new Dictionary<string, TradedCryptoFuture>();
            _futureRBCIndicator = new Dictionary<string, RangeBreakoutRetraceIndicator>();
            //_rbcIndicator = new RangeBreakoutRetraceIndicator();
            //_rbcIndicator.Changed += BreakOutEvent;

            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();

            //_indexSch = new StochasticOscillator();
            _longTrigerred = new List<Trigger>();
            _shortTrigerred = new List<Trigger>();
            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();

            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            FutureTimeCandles = new Dictionary<string, List<Candle>>();

            _cryptoOrderTrios = new Dictionary<string, List<CryptoOrderTrio>>();

            //Arg
            //arg1: Convert.ToInt32(breakout), upperLimit: _rbcIndicator.CurrentPriceRange.Upper, lowerLimit: _rbcIndicator.CurrentPriceRange.Lower
            //arg2: _rbcIndicator.CurrentPriceRange.NextUpper, arg3: _rbcIndicator.CurrentPriceRange.NextLower
            //arg4: _rbcIndicator.PreviousPriceRange.Upper, arg5: _rbcIndicator.PreviousPriceRange.Lower
            //arg6: _rbcIndicator.PreviousPriceRange.NextUpper, arg7: _rbcIndicator.CurrentPriceRange.NextLower



            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfitForUpperBreakout, _stopLossForUpperBreakout, 0,
                0, 0, Arg9: _user==null ? "": _user.UserId, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

#if !BACKTEST
            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
#endif
        }

        private void BreakOutEvent(IIndicatorValue arg1, IIndicatorValue arg2)
        {
            var b = (arg2 as BreakOutIndicatorValue).Value;

            if (b != Breakout.NONE)// && b != _currentBreakOutMode)
            {
                _currentBreakOutMode = b;
                //if (b == Breakout.UP)
                //{
                //    _putTriggered = true;

                //}
                //else if (b == Breakout.DOWN)
                //{
                //    _callTrigered = true;
                //}
            }

            //DataLogic dl = new DataLogic();
            //dl.UpdateAlgoParamaters(_algoInstance, arg1: Convert.ToInt32(_currentBreakOutMode),
            //    upperLimit: _rbcIndicator.CurrentPriceRange.Upper,
            //    lowerLimit: _rbcIndicator.CurrentPriceRange.Lower,
            //    arg2: _rbcIndicator.CurrentPriceRange.NextUpper,
            //    arg3: _rbcIndicator.CurrentPriceRange.NextLower,
            //    arg4: _rbcIndicator.PreviousPriceRange.Upper,
            //    arg5: _rbcIndicator.PreviousPriceRange.Lower,
            //    arg6: _rbcIndicator.PreviousPriceRange.NextUpper,
            //    arg7: _rbcIndicator.CurrentPriceRange.NextLower);
        }

        //public void LoadActiveOrders(PriceActionInput paInput)
        //{
        //    List<OrderTrio> activeOrderTrios = paInput.ActiveOrderTrios;
        //    if (activeOrderTrios != null)
        //    {
        //        DataLogic dl = new DataLogic();
        //        foreach (OrderTrio orderTrio in activeOrderTrios)
        //        {
        //            Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
        //            orderTrio.Option = option;
        //            _orderTrios ??= new List<OrderTrio>();
        //            _orderTrios.Add(orderTrio);

        //            if (orderTrio.Order.TransactionType.ToLower() == "buy")
        //            {
        //                _netQty += orderTrio.Order.Quantity;
        //            }
        //            else
        //            {
        //                _netQty -= orderTrio.Order.Quantity;
        //            }

        //            //if (option.InstrumentType.ToLower() == "ce")
        //            //{
        //            //    _callorderTrios.Add(orderTrio);
        //            //}
        //            //else if (option.InstrumentType.ToLower() == "pe")
        //            //{
        //            //    _putorderTrios.Add(orderTrio);
        //            //}
        //        }
        //        _pnl = paInput.PnL;
        //        //_rbcIndicator.CurrentPriceRange = paInput.CPR;
        //        //_rbcIndicator.PreviousPriceRange = paInput.PPR;

        //        ///TODO: Is this really needed?
        //        //_rbcIndicator = new RangeBreakoutRetraceIndicator();
        //        //_rbcIndicator.CurrentPriceRange = paInput.CPR;
        //        //_rbcIndicator.PreviousPriceRange = paInput.PPR;
        //        //_currentBreakOutMode = paInput.BM;
        //    }
        //}
        private void ActiveTradeIntraday(string channel, string data)
        {
            CandleStick candleStick = CandleStick.FromJson(data);
            DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(candleStick.Timestamp / 1000).LocalDateTime;

            //other algo
            //    //Senario 1: Current red candle and previous red candle
            //    //2: Current Red candle and previous green candle
            //    //3: Current Green Candle and previous green candle
            //    //4: Current Green Candle and previous red candle

            try
            {
                lock (tradeLock)
                {
                    UpdateFuture(candleStick);

                    var future = _futures[candleStick.Symbol];
                    var rbi = _futureRBCIndicator[candleStick.Symbol];
                    var orderTrios = _cryptoOrderTrios[candleStick.Symbol];

                    if (!_tradeSuspended)
                    {
                        ExecuteTrade(future, rbi, orderTrios, currentTime);
                    }

                    //TradeTP(currentTime, _callOrderTrios);
                    //TradeTP(currentTime, _putOrderTrios);

                    //PurgeTriggers(_shortTrigerred, _referenceIndex.LastPrice, false);
                    //PurgeTriggers(_longTrigerred, _referenceIndex.LastPrice, true);
                    //This should be done live
                    CheckTP(currentTime, orderTrios, future.CurrentPrice);

                   
                    ///This is to be done at candle closure
                    CheckSL(currentTime, candleStick, false);
                    //TriggerEODPositionClose(currentTime);// candleStick.CloseTime);

                    

                    //UpdateFuturePrice(tick.LastPrice, token);
                    //if (_referenceIndex.InstrumentToken == token)
                    //{
                    //    MonitorCandles(tick, currentTime);
                    //    if (!_tradeSuspended) 
                    //        TakeTrade(currentTime);

                    //    TradeTP(currentTime);

                    //    PurgeTriggers(_shortTrigerred, _referenceIndex.LastPrice, false);
                    //    PurgeTriggers(_longTrigerred, _referenceIndex.LastPrice, true);

                }

                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, DateTimeOffset.FromUnixTimeMilliseconds(candleStick.Timestamp / 1000).LocalDateTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
            }
        }
        private void UpdateFuture(CandleStick candlestick)
        {
            if(_futures.ContainsKey(candlestick.Symbol))
            {
                _futures[candlestick.Symbol].CurrentPrice = candlestick.ClosePrice;
                FutureTimeCandles[candlestick.Symbol].Add(candlestick);

                _futureRBCIndicator[candlestick.Symbol].Process(candlestick);
            }
            else
            {
                _futures.Add(candlestick.Symbol, new TradedCryptoFuture()
                {
                    ProductId = 27,
                    Symbol = "BTCUSD"
                });
                var rbcIndicator = new RangeBreakoutRetraceIndicator();
                rbcIndicator.InstrumentSymbol = candlestick.Symbol;
                rbcIndicator.Changed += BreakOutEvent;
                _futureRBCIndicator.Add(candlestick.Symbol, rbcIndicator);

                var candles = new List<Candle>();
                candles.Add(candlestick);
                FutureTimeCandles.Add(candlestick.Symbol, candles);

                _cryptoOrderTrios.Add(candlestick.Symbol, new List<CryptoOrderTrio>());
            }
        } 
        private void ExecuteTrade(TradedCryptoFuture future, RangeBreakoutRetraceIndicator rbi, 
            List<CryptoOrderTrio> orderTrios, DateTime currentTime)
        {
            //DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(candleStick.Timestamp / 1000).LocalDateTime;
            //TradedCryptoFuture future = _futures[candleStick.Symbol];
            //RangeBreakoutRetraceIndicator rbi = _futureRBCIndicator[candleStick.Symbol];
            //List<CryptoOrderTrio> orderTrios = _cryptoOrderTrios[candleStick.Symbol];

            int qty = _tradeQty;

            if (_currentBreakOutMode == Breakout.UP)
            {
                //check if future has already reached the target;
                //decimal targetProfit = 2 * rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower;

                //Changed target profit to 1.618x rather than 2x
                decimal targetProfit = rbi.PreviousPriceRange.Upper + 0.61m * (rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower);

                //decimal sl = rbi.CurrentPriceRange.NextLower;
                decimal sl = rbi.PreviousPriceRange.NextLower;

                //1:2 RR
                if ((targetProfit - future.CurrentPrice >= 2 * (future.CurrentPrice - sl)) &&  sl < future.CurrentPrice)
                {
                    if (future.CurrentPrice - sl > MINIMUM_TARGET)
                    {
                        _putTriggered = true;
                        _currentBreakOutMode = Breakout.NONE;
                    }
                }

                if (future.CurrentPrice > targetProfit)
                {
                    _putTriggered = false;
                    _currentBreakOutMode = Breakout.NONE;
                    return;
                }


            }
            else if (_currentBreakOutMode == Breakout.DOWN)
            {
                //check if future has already reached the target;
                //decimal targetProfit = 2 * rbi.PreviousPriceRange.Lower - rbi.PreviousPriceRange.Upper;
                
                
                //Changed target profit to 1.618x rather than 2x
                decimal targetProfit = rbi.PreviousPriceRange.Lower - 0.61m * (rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower);
                decimal sl = rbi.PreviousPriceRange.NextUpper;

                //1:2 RR
                if (((future.CurrentPrice - targetProfit) >= 2 * (sl - future.CurrentPrice)) && sl > future.CurrentPrice)
                {
                    if (sl - future.CurrentPrice > MINIMUM_TARGET)
                    {
                        _callTrigered = true;
                        _currentBreakOutMode = Breakout.NONE;
                    }
                }

                if (future.CurrentPrice < targetProfit)
                {
                    _callTrigered = false;
                    _currentBreakOutMode = Breakout.NONE;
                    return;
                }
            }

            if (_putTriggered)
            {
                _putTriggered = false;

                //check if future has already reached the target;
                //decimal targetProfit = 2 * rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower;

                decimal targetProfit = rbi.PreviousPriceRange.Upper + 0.61m * (rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower);

                //decimal sl = rbi.CurrentPriceRange.NextLower;
                decimal sl = rbi.PreviousPriceRange.NextLower;


                if (future.CurrentPrice > targetProfit)
                {
                    return;
                }

                //if (!_tradeSuspended && orderTrios != null && orderTrios.Count > 0 && orderTrios.Any(x => x.Order.Side.ToLower() == "buy"))
                //{
                //    return;
                //}
                if (orderTrios != null && orderTrios.Count > 0 && orderTrios.Any(x=>x.Order.Side.ToLower() == "sell"))
                {
                   // qty *= 2;

                    //COMMENTED as quantity is made single
                    //remove the sell ordertrio from the collection, as it is being overridden with double qty
                    //_orderTrios.Remove(_orderTrios.Last(x => x.Order.TransactionType.ToLower() == "sell"));

                    //qty = 2 * _orderTrios.Where(x => x.Order.TransactionType.ToLower() == "sell").First().Order.Quantity / Convert.ToInt32(_activeFuture.LotSize); 
                }



                CryptoOrderTrio orderTrio = new CryptoOrderTrio();

                orderTrio.Order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, qty, productId:0, buyOrder: true, algoIndex, user: _user, limitPrice: future.CurrentPrice, timeStamp: currentTime, 
                  httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


                //OnTradeEntry(orderTrio.Order);
                orderTrio.Future = future;

                //SL SHIFTED TO HIGH OF PREVIOUS CANDLE
                //orderTrio.BaseInstrumentStopLoss = stopLoss;
                orderTrio.BaseInstrumentStopLoss = sl;// rbi.CurrentPriceRange.NextLower;
                orderTrio.StopLoss = sl;// rbi.CurrentPriceRange.Lower;
                orderTrio.EntryTradeTime = currentTime;
                ////first target is 1:1 R&R
                //orderTrio.TargetProfit = 2 * rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower;

                //first target is 1:1 R&R
                orderTrio.TargetProfit = targetProfit;// 2 * rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower;

                //_pnl += ((Convert.ToDecimal(orderTrio.Order.Size) /1000) * Convert.ToDecimal(orderTrio.Order.AverageFilledPrice) * -1);

                _netQty += orderTrio.Order.Size;

                //CryptoDataLogic dl = new CryptoDataLogic();
                //dl.UpdateAlgoPnl(_algoInstance, _pnl, currentTime);

                orderTrios.Add(orderTrio);
            }
            else if(_callTrigered)
            {
                _callTrigered = false;

                //check if future has already reached the target;
                //decimal targetProfit = 2 * rbi.PreviousPriceRange.Lower -  rbi.PreviousPriceRange.Upper;

                //Changed target profit to 1.618x rather than 2x
                decimal targetProfit = rbi.PreviousPriceRange.Lower - 0.61m * (rbi.PreviousPriceRange.Upper - rbi.PreviousPriceRange.Lower);
                decimal sl = rbi.PreviousPriceRange.NextUpper;


                if (future.CurrentPrice < targetProfit)
                {
                    return;
                }

                //if (!_tradeSuspended && orderTrios != null && orderTrios.Count > 0 && orderTrios.Any(x=>x.Order.Side.ToLower() == "sell"))
                //{
                //    return;
                //}
                if (orderTrios != null && orderTrios.Count > 0 && orderTrios.Any(x => x.Order.Side.ToLower() == "buy"))
                {
                    //qty *= 2;

                    //remove the buy ordertrio from the collection, as it is being overridden with double qty
                    //COMMENTED as quantity is made single
                    //orderTrios.Remove(orderTrios.Last(x => x.Order.TransactionType.ToLower() == "buy"));
                }
                CryptoOrderTrio orderTrio = new CryptoOrderTrio();

                orderTrio.Order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, qty, productId: 0, buyOrder: false, algoIndex, 
                    user: _user, limitPrice: future.CurrentPrice, timeStamp: currentTime,
                  httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                //OnTradeEntry(orderTrio.Order);
                orderTrio.Future = future;

                //SL SHIFTED TO HIGH OF PREVIOUS CANDLE
                //orderTrio.BaseInstrumentStopLoss = stopLoss;
                orderTrio.BaseInstrumentStopLoss = sl;// rbi.CurrentPriceRange.NextUpper;
                orderTrio.StopLoss = sl;
                orderTrio.EntryTradeTime = currentTime;

                //first target is 1:1 R&R
                //orderTrio.TargetProfit = 2 * rbi.PreviousPriceRange.Lower - rbi.PreviousPriceRange.Upper;
                //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * 1;
                orderTrio.TargetProfit = targetProfit;// 2 * rbi.PreviousPriceRange.Lower - rbi.PreviousPriceRange.Upper;
           //     _pnl += ((Convert.ToDecimal(orderTrio.Order.Size) / 1000) * Convert.ToDecimal(orderTrio.Order.AverageFilledPrice) * 1);


                //CryptoDataLogic dl = new CryptoDataLogic();
                //dl.UpdateAlgoPnl(_algoInstance, _pnl, currentTime);

           //     _netQty -= orderTrio.Order.Size;

                orderTrios.Add(orderTrio);
            }
        }
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }


        private void CheckTP(DateTime currentTime, List<CryptoOrderTrio> orderTrios, decimal lastPrice)
        {
            if (orderTrios != null && orderTrios.Count > 0)
            {
                for (int i = 0; i < orderTrios.Count; i++)
                {
                    var orderTrio = orderTrios[i];
                    var future = orderTrio.Future;
                    if (orderTrio.Order.Side.ToLower() == "buy" && (lastPrice > orderTrio.TargetProfit))
                    {
                        //book only net qty order
                        CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, orderTrio.Order.Size, productId: 0, 
                            buyOrder: false, algoIndex, user: _user, limitPrice: future.CurrentPrice, timeStamp: currentTime,
                          httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        //OnTradeEntry(order);

                        _pnl += ((Convert.ToDecimal(order.Size)/1000) * (Convert.ToDecimal(order.AverageFilledPrice) - Convert.ToDecimal(orderTrio.Order.AverageFilledPrice)));

#if !BACKTEST
                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        //DataLogic dl = new DataLogic();
                        // dl.DeActivateOrderTrio(orderTrio);

                        CryptoDataLogic dl = new CryptoDataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl, currentTime, "");
                       // if (_netQty == 0)
                      //  {
                            Console.WriteLine(+_pnl);
                       // }

                        orderTrios.Remove(orderTrio);
                        _netQty = 0;
                        i--;
                    }
                    else if (orderTrio.Order.Side.ToLower() == "sell" && (lastPrice < orderTrio.TargetProfit))
                    {
                        CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, orderTrio.Order.Size, productId: 0,
                            buyOrder: true, algoIndex, user: _user, limitPrice: future.CurrentPrice, timeStamp: currentTime,
                          httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                       // Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       //_activeFuture.KToken, true, orderTrio.Order.Quantity, algoIndex, currentTime, Tag: Convert.ToString(orderTrio.Order.OrderId),
                       //product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                       //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        //OnTradeEntry(order);

#if !BACKTEST
                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _pnl += ((Convert.ToDecimal(order.Size) / 1000) * (Convert.ToDecimal(orderTrio.Order.AverageFilledPrice)- Convert.ToDecimal(order.AverageFilledPrice)));

                        CryptoDataLogic dl = new CryptoDataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl, currentTime,"");
                       // if (_netQty == 0)
                       // {
                            Console.WriteLine(+_pnl);
                      //  }
                        orderTrios.Remove(orderTrio);
                        _netQty = 0;
                        i--;
                    }
                }
            }
        }
        private void CheckSL(DateTime currentTime, CandleStick candlestick, bool closeAll = false)
        {
            //DateTime currentTime = candlestick.CloseTime;
            List<CryptoOrderTrio> orderTrios = _cryptoOrderTrios[candlestick.Symbol];

            if (orderTrios != null && orderTrios.Count > 0)
            {
                for (int i = 0; i < orderTrios.Count; i++)
                {
                    var orderTrio = orderTrios[i];
                    TradedCryptoFuture future = orderTrio.Future;

                    if (orderTrio.Order.Side.ToLower() == "buy" && (closeAll || candlestick.ClosePrice < orderTrio.StopLoss))
                    {
                        //int qty = closeAll ? orderTrio.Order.Size : 2 * orderTrio.Order.Size;
                        int qty = closeAll ? orderTrio.Order.Size : orderTrio.Order.Size;

                        CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, orderTrio.Order.Size, productId: 0,
                           buyOrder: false, algoIndex, user: _user, limitPrice: future.CurrentPrice, timeStamp: currentTime,
                         httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                       // Order order = MarketDEOrders.PlaceDEOrder(_algoInstance, future.Symbol, option.InstrumentType.ToLower(), option.LastPrice,
                       //option.KToken, false, qty, algoIndex, currentTime, Tag: Convert.ToString(orderTrio.Order.OrderId),
                       //product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user.HsServerId,
                       //httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                        //OnTradeEntry(order);

                        _pnl += (Convert.ToDecimal(order.Size)/1000 * (Convert.ToDecimal(order.AverageFilledPrice) - Convert.ToDecimal(orderTrio.Order.AverageFilledPrice)));
                        _netQty -= qty;

#if !BACKTEST
                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        CryptoDataLogic dl = new CryptoDataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl, currentTime, "");

                     //   if (_netQty == 0)
                      //  {
                            Console.WriteLine(+_pnl);
                     //   }
                        orderTrios.Remove(orderTrio);
                        //DataLogic dl = new DataLogic();
                        //dl.DeActivateOrderTrio(orderTrio);

                        ////Take opposite trade with double qty
                        //if (!closeAll)
                        //{
                        //    OrderTrio orderTrio1 = new OrderTrio();
                        //    orderTrio1.Order = order;
                        //    orderTrio1.Option = _activeFuture;
                        //    orderTrio1.BaseInstrumentStopLoss = orderTrio.Order.AveragePrice;
                        //    orderTrio1.StopLoss = orderTrio.Order.AveragePrice;
                        //    orderTrio1.EntryTradeTime = currentTime;
                        //    //first target is 1:1 R&R
                        //    orderTrio1.TargetProfit = order.AveragePrice + orderTrio.Order.AveragePrice - orderTrio.TargetProfit;

                        //    orderTrio1.Id = dl.UpdateOrderTrio(orderTrio1, _algoInstance);
                        //    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                        //    _orderTrios ??= new List<OrderTrio>();
                        //    _orderTrios.Add(orderTrio1);
                        //    orderTrio1.Option = option;
                        //}

                        i--;
                    }
                    if (orderTrio.Order.Side.ToLower() == "sell" && (closeAll || candlestick.ClosePrice > orderTrio.BaseInstrumentStopLoss))
                    {
                        //int qty = closeAll ? orderTrio.Order.Quantity : 2 * orderTrio.Order.Quantity;
                        int qty = closeAll ? orderTrio.Order.Size : orderTrio.Order.Size;

                        CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, orderTrio.Order.Size, productId: 0,
                           buyOrder: true, algoIndex, user: _user, limitPrice: future.CurrentPrice, timeStamp: currentTime,
                         httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


                        //OnTradeEntry(order);

#if !BACKTEST
                        //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _pnl += (Convert.ToDecimal(order.Size) / 1000 * (Convert.ToDecimal(orderTrio.Order.AverageFilledPrice) - Convert.ToDecimal(order.AverageFilledPrice)));

                        orderTrios.Remove(orderTrio);
                        //DataLogic dl = new DataLogic();
                        //dl.DeActivateOrderTrio(orderTrio);
                        //_netQty += qty;

                        CryptoDataLogic dl = new CryptoDataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl, currentTime, "");
                      //  if (_netQty == 0)
                      //  {
                            Console.WriteLine(+_pnl);
                      //  }
                        ////Take opposite trade with double qty
                        //if (!closeAll)
                        //{
                        //    OrderTrio orderTrio1 = new OrderTrio();
                        //    orderTrio1.Order = order;
                        //    orderTrio1.Option = _activeFuture;
                        //    orderTrio1.BaseInstrumentStopLoss = orderTrio.Order.AveragePrice;
                        //    orderTrio1.StopLoss = orderTrio.Order.AveragePrice;
                        //    orderTrio1.EntryTradeTime = currentTime;
                        //    //first target is 1:1 R&R
                        //    orderTrio1.TargetProfit = order.AveragePrice + orderTrio.Order.AveragePrice - orderTrio.TargetProfit;

                        //    orderTrio1.Id = dl.UpdateOrderTrio(orderTrio1, _algoInstance);

                        //    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                        //    _orderTrios ??= new List<OrderTrio>();
                        //    _orderTrios.Add(orderTrio1);
                        //    orderTrio1.Option = option;
                        //}

                        i--;
                    }
                }
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
                if (!SubscriptionTokens.Contains(_referenceIndex.InstrumentToken))
                {
                    SubscriptionTokens.Add(_referenceIndex.InstrumentToken);
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(_activeFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeFuture.InstrumentToken);
                    dataUpdated = true;
                }
                //foreach (var optionUniverse in OptionUniverse)
                //{
                //    foreach (var instrument in optionUniverse.Values)
                //    {
                //        if (!SubscriptionTokens.Contains(instrument.InstrumentToken))
                //        {
                //            SubscriptionTokens.Add(instrument.InstrumentToken);
                //            dataUpdated = true;
                //        }
                //    }
                //}
                if (dataUpdated)
                {
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
#if !BACKTEST
                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Options", "UpdateInstrumentSubscription");
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

        public virtual void OnNext(string channel, string data )
        {
            try
            {
#if MARKET || AWSMARKET
                if (_stopTrade)
                {
                    return;

                }
#endif
                if (!_stopTrade)
                {
                    ActiveTradeIntraday(channel, data);
                }
                //return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, DateTime.UtcNow,
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
               // LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
                Thread.Sleep(100);
            }
            else
            {
                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
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

        //public virtual void Subscribe(IObservable<Tick> provider)
        //{
        //    unsubscriber = provider.Subscribe(this);
        //}

        //public virtual void Unsubscribe()
        //{
        //    unsubscriber.Dispose();
        //}

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
