using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using Algos.Utilities.Views;
using BrokerConnectWrapper;
using FirebaseAdmin.Messaging;
using GlobalCore;
using GlobalLayer;
using NetMQ;

//using KafkaFacade;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ZMQFacade;

namespace Algorithms.Algorithms
{
    public class StockTrend : IZMQ, IObserver<Tick>//, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(StockTrend source);
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
        
        public Dictionary<uint, Instrument> AllStocks { get; set; }

        private User _user;
        private bool _slUpdated = false;
        private Dictionary<uint, OrderTrio> _orderTrios;
        //private Dictionary<uint, bool> _firstSuperTrendChangeOver;
        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private decimal _baseInstrumentStartPrice;
        private Instrument atmOption;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);
        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        
        //private const decimal CANDLE_WICK_SIZE = 45;//10000;//45;//35
        //private const decimal CANDLE_BODY_MIN = 5;
        //private const decimal CANDLE_BODY = 40;//10000;//40; //25
        //private const decimal CANDLE_BODY_BIG = 35;
        //private const decimal EMA_ENTRY_RANGE = 35;
        //private const decimal RISK_REWARD = 2.3M;
        //private OHLC _previousDayOHLC;
        //private OHLC _previousWeekOHLC;
        //#endif
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private Dictionary<uint, Candle> _pCandle;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.StockInitialMomentum;
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

        private Dictionary<uint, ExponentialMovingAverage> _shortEMAIndicator;
        private Dictionary<uint, ExponentialMovingAverage> _mediumEMAIndicator;
        private Dictionary<uint, ExponentialMovingAverage> _largeEMAIndicator;
        private Dictionary<uint, SuperTrend> _superTrendIndicator;


        private const decimal CANDLE_SIZE_PERCENT = 0.008m;
        private const decimal CANDLE_BODY_PERCENT = 0.7m;
        private const decimal CANDLE_BODY_MIN_PERCENT = 0.1m;
        private const decimal CANDLE_BODY_MIN_CANDLE_PERCENT = 2.6m;

        StochasticOscillator _indexSch;

        public struct PriceRange
        {
            public decimal Upper;
            public decimal Lower;
            public DateTime? CrossingTime;
        }

        private PriceRange _resistancePriceRange;
        private PriceRange _supportPriceRange;

        bool _indexSchLoaded = false;
        Dictionary<uint, bool> _indicatorsLoaded = new Dictionary<uint, bool>();
        bool _indexSchLoading = false;
        bool _indexSchLoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;

        private const decimal MINIMUM_TARGET = 50;
        private decimal _pnl = 0;


        public StockTrend(TimeSpan candleTimeSpan, uint baseInstrumentToken,
           DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
           int algoInstance = 0, bool positionSizing = false,
           decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            //ZConnect.Login();
            //_user = KoConnect.GetUser(userId: uid);

            _httpClientFactory = httpClientFactory;
            _candleTimeSpan = candleTimeSpan;

            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            //_trailingStopLoss = _stopLoss = stopLoss;
            //_targetProfit = targetProfit;

            _tradeQty = quantity;
            //_positionSizing = positionSizing;
            //_maxLossPerTrade = maxLossPerTrade;

            SetUpInitialData(algoInstance);


            //#if local
            //            _dateLoaded = new List<DateTime>();
            //            LoadPAInputsForTest();
            //#endif

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }
        private void SetUpInitialData(int algoInstance = 0)
        {
            //_expiryDate = expiry;
            _orderTrios = new Dictionary<uint, OrderTrio>();
            _pCandle = new Dictionary<uint, Candle>();
            //_firstSuperTrendChangeOver = new Dictionary<uint, bool>();

            _shortEMAIndicator = new Dictionary<uint, ExponentialMovingAverage>();
            _mediumEMAIndicator = new Dictionary<uint, ExponentialMovingAverage>();
            _largeEMAIndicator = new Dictionary<uint, ExponentialMovingAverage>();
            _superTrendIndicator = new Dictionary<uint, SuperTrend>();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            //_algoInstance = algoInstance != 0 ? algoInstance :
            //    Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now, DateTime.Now,
            //    //expiry.GetValueOrDefault(DateTime.Now), 
            //    _tradeQty, 0, 0, 0, 0,
            //    0, 0, 0, 0, candleTimeFrameInMins:
            //    (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
            //    0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);


            //Arg3 for first supertrend change ignore
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                DateTime.Now, _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, Arg9: _user.UserId, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);


#if !BACKTEST
            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
#endif
        }

        public void LoadActiveOrders(PriceActionInput paInput)
        {
            List<OrderTrio> activeOrderTrios = paInput.ActiveOrderTrios;
            if (activeOrderTrios != null)
            {
                DataLogic dl = new DataLogic();
                foreach (OrderTrio orderTrio in activeOrderTrios)
                {
                    Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                    orderTrio.Option = option;
                    _orderTrios ??= new Dictionary<uint, OrderTrio>();

                    uint token = orderTrio.Order.InstrumentToken;
                    if (!_orderTrios.ContainsKey(token))
                    {
                        _orderTrios.Add(token, orderTrio);
                    }
                    else
                    {
                        _orderTrios[token] = orderTrio;
                    }

                    //if (orderTrio.SLFlag != null)
                    //{
                    //    _firstSuperTrendChangeOver.Add(token, orderTrio.SLFlag.Value);
                    //}
                }
                _pnl = paInput.PnL;
            }
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
                    //if (!GetBaseInstrumentPrice(tick))
                    //{
                    //    return;
                    //}
                    
                    //if (_baseInstrumentStartPrice == 0 
                    //    && currentTime.TimeOfDay >= new TimeSpan(09, 15, 00))
                    //{
                    //    _baseInstrumentStartPrice = _baseInstrumentPrice;
                    //}

                    LoadStocksToTrade(currentTime);



                    //Load historicalData
                    if (!_indicatorsLoaded[token])
                    {
                       LoadHistoricalHT(currentTime, token);
                    }
                    

                    UpdateInstrumentSubscription(currentTime);
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
                //Step1: CHeck for 50, 100, 200 EMA , All should be  close by, prefereable in order and supertrend should follow in that range
                //Step2: If Nifty is positive, check for stocks that are positive, and opening range is within 3%
                //Take trend trade with SL at low of candle that is crossing the 3 EMA, or SUper trend.
                //Profit when super trend changes

                _superTrendIndicator[e.InstrumentToken].Process(e);
                _shortEMAIndicator[e.InstrumentToken].Process(e);
                _mediumEMAIndicator[e.InstrumentToken].Process(e);
                _largeEMAIndicator[e.InstrumentToken].Process(e);



                if (_largeEMAIndicator[e.InstrumentToken].IsFormed)
                {

                    decimal shortEMA = _shortEMAIndicator[e.InstrumentToken].GetCurrentValue<Decimal>();
                    decimal mediumEMA = _mediumEMAIndicator[e.InstrumentToken].GetCurrentValue<Decimal>();
                    decimal largeEMA = _largeEMAIndicator[e.InstrumentToken].GetCurrentValue<Decimal>();
                    decimal superTrend = _superTrendIndicator[e.InstrumentToken].GetCurrentValue<Decimal>();

                    if ((e.ClosePrice >= superTrend && e.ClosePrice > shortEMA && e.ClosePrice > mediumEMA && e.ClosePrice > largeEMA)
                        && ((e.ClosePrice - shortEMA) < 0.005M * e.ClosePrice)
                        && ((e.ClosePrice - mediumEMA) < 0.005M * e.ClosePrice)
                        && ((e.ClosePrice - largeEMA) < 0.005M * e.ClosePrice))
                {
                        TakeTrade(e.InstrumentToken, e.ClosePrice, e.CloseTime, superTrend, true);
                    }
                    else if ((e.ClosePrice <= superTrend && e.ClosePrice< shortEMA && e.ClosePrice<mediumEMA && e.ClosePrice<largeEMA)
                            && ((shortEMA) - e.ClosePrice < 0.005M * e.ClosePrice)
                            && ((mediumEMA - e.ClosePrice) < 0.005M * e.ClosePrice)
                            && ((largeEMA - e.ClosePrice) < 0.005M * e.ClosePrice))
                    {
                        TakeTrade(e.InstrumentToken, e.ClosePrice, e.CloseTime, superTrend, false);
                    }
                }

                TradeSL(e.CloseTime, e.InstrumentToken, e.ClosePrice);

                TriggerEODPositionClose(e.CloseTime);
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

        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay > new TimeSpan(15, 00, 00))
            {
                if (currentTime.TimeOfDay >= new TimeSpan(15, 12, 00))
                {
                    DataLogic dl = new DataLogic();
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    if (_orderTrios == null || _orderTrios.Count == 0)
                    {
                        dl.DeActivateAlgo(_algoInstance);
                    }
                    _pnl = 0;
                    _stopTrade = true;
                }
            }
        }

        void UpdateFuturePrice(decimal lastPrice)
        {
            atmOption.LastPrice = lastPrice;
        }
        private void TakeTrade(uint bToken, decimal bPrice, DateTime currentTime, decimal stopLevel, bool buySell)
        {
            int qty = _tradeQty;

            Instrument stock = AllStocks[bToken];

            if (!_orderTrios.ContainsKey(stock.KToken))
            {
                OrderTrio orderTrio = new OrderTrio();
                qty = _tradeQty * Convert.ToInt32(stock.LotSize);
                qty = 10;

                //orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, stock.TradingSymbol, stock.InstrumentType.ToLower(), stock.LastPrice, //e.ClosePrice,
                //stock.KToken, buySell, qty, algoIndex, currentTime, Tag: "FT",
                //broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, stock.TradingSymbol, stock.InstrumentType.ToLower(), bPrice,
                      stock.KToken, buySell, qty, algoIndex, currentTime, Tag: "FT",
                      product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK, HsServerId: _user == null? "1": _user.HsServerId,
                      httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                orderTrio.EntryTradeTime = currentTime;
                orderTrio.StopLoss = stopLevel;
                orderTrio.Option = stock;
                orderTrio.SLFlag = null;
                _orderTrios.Add(stock.KToken, orderTrio);

                //_pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (buySell? - 1: 1);

                DataLogic dl = new DataLogic();
                orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
#if !BACKTEST
                OnTradeEntry(orderTrio.Order);
                //OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("{0} @ {1}", "Bought ", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
            }
            else
            {

            }
        }
        

        private void TradeSL(DateTime currentTime, uint stockToken, decimal stockPrice)
        {
            Instrument stock = AllStocks[stockToken];

            if (_orderTrios.ContainsKey(stock.KToken))
            {
                var orderTrio = _orderTrios[stock.KToken];
                Order order = orderTrio.Order;
                bool buyOrder = order.TransactionType.ToLower() == "buy";

                if((orderTrio.SLFlag == null) && ((buyOrder && stockPrice < _superTrendIndicator[stockToken].GetCurrentValue<decimal>()) 
                    || (!buyOrder && stockPrice > _superTrendIndicator[stockToken].GetCurrentValue<decimal>()))
                    )
                {
                    //store firt super trend over night
                   // if (!_firstSuperTrendChangeOver.ContainsKey(stockToken))
                    //{
                    //    _firstSuperTrendChangeOver[stockToken] = true;

                    //    //TP flag is used to capture first super trend change ignore
                    //    orderTrio.SLFlag = true;
                    //}
                    //else
                    //{
                        //_firstSuperTrendChangeOver.Add(stockToken, false);

                        orderTrio.SLFlag = false;
                   // }

                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                }
                else if (orderTrio.SLFlag.HasValue && !orderTrio.SLFlag.Value && ((buyOrder && stockPrice > _superTrendIndicator[stockToken].GetCurrentValue<decimal>())
                    || (!buyOrder && stockPrice < _superTrendIndicator[stockToken].GetCurrentValue<decimal>()))
                    )
                {
                    //store firt super trend over night
                    //_firstSuperTrendChangeOver[stockToken] = true;
                    //SL flag is used to capture first super trend change ignore
                    orderTrio.SLFlag = true;

                    DataLogic dl = new DataLogic();
                    orderTrio.Id = dl.UpdateOrderTrio(orderTrio, _algoInstance);
                }

                if (orderTrio.SLFlag.HasValue &&
                    (
                    (buyOrder 
                    && ((!orderTrio.SLFlag.Value && stockPrice < orderTrio.StopLoss) || (orderTrio.SLFlag.Value && stockPrice < _superTrendIndicator[stockToken].GetCurrentValue<decimal>()) )
                    ) //orderTrio.StopLoss)
                    || 
                    (!buyOrder 
                    && ((!orderTrio.SLFlag.Value && stockPrice > orderTrio.StopLoss) || (orderTrio.SLFlag.Value && stockPrice > _superTrendIndicator[stockToken].GetCurrentValue<decimal>())) //orderTrio.StopLoss))
                    )
                    ))
                {
                    Order slOrder = MarketOrders.PlaceOrder(_algoInstance, stock.TradingSymbol, stock.InstrumentType.ToLower(), stockPrice, //e.ClosePrice,
                        stock.KToken, !buyOrder, order.Quantity, algoIndex, currentTime, Tag: "SL",
                        broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()));

                    _pnl += ((slOrder.AveragePrice - order.AveragePrice) * stock.LotSize * (buyOrder ? 1 : -1));

                    DataLogic dl = new DataLogic();
                    orderTrio.SLFlag = null;
                    dl.DeActivateOrderTrio(orderTrio);
                    //dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    //_firstSuperTrendChangeOver.Remove(stockToken);
                    _orderTrios.Remove(stock.KToken);
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
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Nifty Stock Tokens from database...", "LoadOptionsToTrade");
#endif
                    //Load options asynchronously

                    Dictionary<uint, uint> mappedTokens;

                    DataLogic dl = new DataLogic();
                    var allStocks = dl.LoadIndexStocks(_baseInstrumentToken);

                    if (allStocks.Count == 0)
                    {
                        return;
                    }
                    AllStocks ??= new Dictionary<uint, Instrument>();
                    foreach (var stockItem in allStocks)
                    {
                        AllStocks.TryAdd(stockItem.InstrumentToken, stockItem);

                        _shortEMAIndicator.TryAdd(stockItem.InstrumentToken, new ExponentialMovingAverage(50));
                        _mediumEMAIndicator.TryAdd(stockItem.InstrumentToken, new ExponentialMovingAverage(100));
                        _largeEMAIndicator.TryAdd(stockItem.InstrumentToken, new ExponentialMovingAverage(200));
                        _superTrendIndicator.TryAdd(stockItem.InstrumentToken, new SuperTrend(3,10));

                        _indicatorsLoaded.TryAdd(stockItem.InstrumentToken, false);

                    }

                    _shortEMAIndicator.TryAdd(_baseInstrumentToken, new ExponentialMovingAverage(50));
                    _mediumEMAIndicator.TryAdd(_baseInstrumentToken, new ExponentialMovingAverage(100));
                    _largeEMAIndicator.TryAdd(_baseInstrumentToken, new ExponentialMovingAverage(200));
                    _superTrendIndicator.TryAdd(_baseInstrumentToken, new SuperTrend(1, 10));

                    _indicatorsLoaded.TryAdd(_baseInstrumentToken, false);
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Stocks Loaded", "LoadStocksToTrade");
#endif
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

        private void LoadHistoricalHT(DateTime currentTime, uint token)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

#if BACKTEST
                LoadHistoricalCandlesForIndicatorsFromDataBase(token, _candleTimeSpan, lastCandleEndTime);
#elif MARKET
                LoadHistoricalCandlesForIndicatorsFromBroker(token, _candleTimeSpan, lastCandleEndTime);
#endif

            
                _indicatorsLoaded[token] = true;
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

        private void LoadHistoricalCandlesForIndicatorsFromDataBase(uint token, TimeSpan candleTimeSpan, DateTime lastCandleEndTime)
        {
            DataLogic dl = new DataLogic();
            int numberOfDaysofData = Convert.ToInt32(200 / (375 / candleTimeSpan.TotalMinutes) * 1.42);
            DateTime previousTradingDate = lastCandleEndTime.Subtract(new TimeSpan(numberOfDaysofData, 0, 0, 0));// dl.GetPreviousTradingDate(lastCandleEndTime, 120, token);

            List<Historical> historicals = new List<Historical>();
            historicals = dl.GetHistoricals(token, previousTradingDate, lastCandleEndTime);
            historicals.Sort();

            historicals = ConvertHistoricals(candleTimeSpan, historicals, 210, lastCandleEndTime, token, previousTradingDate);
            
            foreach (var price in historicals)
            {
                TimeFrameCandle tc = new TimeFrameCandle();
                tc.TimeFrame = candleTimeSpan;
                tc.ClosePrice = price.Close;
                tc.OpenPrice = price.Open;
                tc.HighPrice = price.High;
                tc.LowPrice = price.Low;
                tc.TotalVolume = price.Volume;
                tc.OpenTime = price.TimeStamp;
                tc.CloseTime = tc.OpenTime.Add(candleTimeSpan);
                tc.InstrumentToken = token;
                tc.Final = true;
                tc.State = CandleStates.Finished;

                _shortEMAIndicator[token].Process(tc);
                _mediumEMAIndicator[token].Process(tc);
                _largeEMAIndicator[token].Process(tc);
                _superTrendIndicator[token].Process(tc);
            }
        }

        private List<Candle> LoadHistoricalCandlesForIndicatorsFromBroker(uint token, TimeSpan candleTimeSpan, DateTime lastCandleEndTime)
        {
            DataLogic dl = new DataLogic();
            DateTime previousTradingDate = lastCandleEndTime.Subtract(new TimeSpan(70, 0, 0, 0));// dl.GetPreviousTradingDate(lastCandleEndTime, 120, token);
            List<Historical> historicals;
            if (candleTimeSpan.Minutes == 1)
            {
                historicals = ZObjects.fy.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
            }
            else if (candleTimeSpan.TotalMinutes < 60)
            {
                historicals = ZObjects.fy.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", candleTimeSpan.Minutes));
            }
            else if (candleTimeSpan.TotalMinutes == 60)
            {
                historicals = ZObjects.fy.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "hour");
            }
            else if (candleTimeSpan.TotalMinutes < 375)
            {
                historicals = ZObjects.fy.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}hour", candleTimeSpan.TotalMinutes / 60));
            }
            else //if (candleTimeSpan.Minutes >= 375)
            {
                historicals = ZObjects.fy.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "day");
            }

            List<Candle> historicalCandles = new List<Candle>();
            foreach (var price in historicals)
            {
                TimeFrameCandle tc = new TimeFrameCandle();
                tc.TimeFrame = candleTimeSpan;
                tc.ClosePrice = price.Close;
                tc.OpenPrice = price.Open;
                tc.HighPrice = price.High;
                tc.LowPrice = price.Low;
                tc.TotalVolume = price.Volume;
                tc.OpenTime = price.TimeStamp;
                tc.CloseTime = tc.OpenTime.Add(candleTimeSpan);
                tc.InstrumentToken = token;
                tc.Final = true;
                tc.State = CandleStates.Finished;
                historicalCandles.Add(tc);

                _shortEMAIndicator[token].Process(tc);
                _mediumEMAIndicator[token].Process(tc);
                _largeEMAIndicator[token].Process(tc);
                _superTrendIndicator[token].Process(tc);
            }
            return historicalCandles;
        }

        //private List<Candle> LoadHistoricalCandlesForIndicatorsFromBroker(uint token, TimeSpan candleTimeSpan, DateTime lastCandleEndTime)
        //{
        //    DataLogic dl = new DataLogic();
        //    DateTime previousTradingDate = lastCandleEndTime.Subtract(new TimeSpan(70, 0, 0, 0));// dl.GetPreviousTradingDate(lastCandleEndTime, 120, token);
        //    List<Historical> historicals;
        //    if (candleTimeSpan.Minutes == 1)
        //    {
        //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "minute");
        //    }
        //    else if (candleTimeSpan.TotalMinutes < 60)
        //    {
        //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", candleTimeSpan.Minutes));
        //    }
        //    else if (candleTimeSpan.TotalMinutes == 60)
        //    {
        //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "hour");
        //    }
        //    else if (candleTimeSpan.TotalMinutes < 375)
        //    {
        //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}hour", candleTimeSpan.TotalMinutes / 60));
        //    }
        //    else //if (candleTimeSpan.Minutes >= 375)
        //    {
        //        historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), "day");
        //    }

        //    List<Candle> historicalCandles = new List<Candle>();
        //    foreach (var price in historicals)
        //    {
        //        TimeFrameCandle tc = new TimeFrameCandle();
        //        tc.TimeFrame = candleTimeSpan;
        //        tc.ClosePrice = price.Close;
        //        tc.OpenPrice = price.Open;
        //        tc.HighPrice = price.High;
        //        tc.LowPrice = price.Low;
        //        tc.TotalVolume = price.Volume;
        //        tc.OpenTime = price.TimeStamp;
        //        tc.CloseTime = tc.OpenTime.Add(candleTimeSpan);
        //        tc.InstrumentToken = token;
        //        tc.Final = true;
        //        tc.State = CandleStates.Finished;
        //        historicalCandles.Add(tc);

        //        _shortEMAIndicator[token].Process(tc);
        //        _mediumEMAIndicator[token].Process(tc);
        //        _largeEMAIndicator[token].Process(tc);
        //        _superTrendIndicator[token].Process(tc);
        //    }
        //    return historicalCandles;
        //}

        //private uint GetKotakToken(uint kiteToken)
        //{
        //    return MappedTokens[kiteToken];
        //}
        private decimal GetATMStrike(decimal bPrice, string instrumentType)
        {
            return instrumentType.ToLower() == "ce" ? Math.Floor(bPrice / 100) * 100 : Math.Ceiling(bPrice / 100) * 100;
        }

        private List<Historical> ConvertHistoricals(TimeSpan candleTimeSpan, List<Historical> historicals, int historicalCount, DateTime currentTime, uint token, DateTime startTradingDateforHistoricals)
        {
            if (currentTime.TimeOfDay.TotalMinutes - MARKET_START_TIME.TotalMinutes < 0)
            {
                return null;
            }

            List<Historical> convertedHistoricals = new List<Historical>();
            int historicalCounter = 0;
            DateTime startTime = startTradingDateforHistoricals.Date + MARKET_START_TIME;
            DateTime endTime =  startTime;
            while (startTime < currentTime)
            {
                //startTime = startTime.Date + MARKET_START_TIME;
                endTime = startTime + candleTimeSpan;

                if (endTime.TimeOfDay > MARKET_CLOSE_TIME)
                {
                    endTime = endTime.Date + MARKET_CLOSE_TIME;
                }

                var lastCandles = historicals.Where(x => x.TimeStamp >= startTime && x.TimeStamp < endTime);
                
                if(lastCandles.Count() == 0)
                {
                    startTime = startTime.Date.AddDays(1).Date + MARKET_START_TIME;
                    continue;
                }

                Historical tC = new Historical();
                tC.InstrumentToken = token;
                tC.Open = lastCandles.ElementAt(0).Open;
                tC.Close = lastCandles.Last().Close;
                tC.TimeStamp = lastCandles.First().TimeStamp;
                tC.High = lastCandles.Max(x => x.High);
                tC.Low = lastCandles.Min(x => x.Low);

                convertedHistoricals.Add(tC);

                historicalCounter++;

                if (endTime.TimeOfDay >= MARKET_CLOSE_TIME)
                {
                    startTime = startTime.Date.AddDays(1).Date + MARKET_START_TIME;
                }
                else
                {
                    startTime = endTime;
                }
            }

            convertedHistoricals.Sort();

            return convertedHistoricals.TakeLast(historicalCount).ToList();
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


        private void LoadBInstrumentSch(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_indexSch)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_indexSchLoading)
                    {
                        _indexSchLoading = true;
                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _indexSchLoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _indexSch.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var price in TimeCandles[bToken])
                            {
                                _indexSch.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexSchLoadedFromDB)
                    {
                        _indexSchLoaded = true;
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} EMA loaded from DB for Base Instrument", 20), "LoadBInstrumentSCH");
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
                lock (_indexSch)
                {
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    TimeFrameCandle c;

                    foreach (var price in historicals)
                    {
                        c = new TimeFrameCandle();
                        c.TimeFrame = new TimeSpan(0, _candleTimeSpan.Minutes, 0);
                        c.ClosePrice = price.Close;
                        c.HighPrice = price.High;
                        c.LowPrice = price.Low;
                        c.State = CandleStates.Finished;
                        _indexSch.Process(c);//, isFinal: true);
                    }
                    _indexSchLoadedFromDB = true;
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
