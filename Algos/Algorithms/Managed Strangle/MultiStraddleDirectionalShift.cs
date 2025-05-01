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

namespace Algorithms.Algorithms
{
    public class MultiStraddleDirectionalShift : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }
        public Dictionary<uint, Instrument> OptionsDictionary { get; set; }
        public SortedList<decimal, Instrument[]> StraddleUniverse { get; set; }
        public Dictionary<uint, uint> MappedTokens { get; set; }

        public Dictionary<decimal, Option> StraddleNodes { get; set;}

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(MultiStraddleDirectionalShift source);
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
        public OrderTrio _straddleCallOrderTrio;
        public OrderTrio _straddlePutOrderTrio;
        public OrderTrio _soloCallOrderTrio;
        public OrderTrio _soloPutOrderTrio;
        private Instrument _call;
        private Instrument _put;
        private decimal _referenceStraddleValue;
        private decimal _referenceValueForStraddleShift;
        private Dictionary<decimal, OrderTrio> _callOrderTrios;
        private Dictionary<decimal, OrderTrio> _putOrderTrios;
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
        public Dictionary<decimal, int> _tradeStrike;
        private int _maxTradePerStrike;
        private decimal _entryRatio = 1.2m;//1.2//1.45
        private decimal _reEntryRatio = 0.8m;//1.45
        //private decimal _entryRatioHigh = 1.4m;
        private decimal _exitRatio = 1.5m;//2.0m; //1.7m;//1.4//2.0
        private int _totalEntries = 2;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private decimal _bInstrumentPreviousPrice;
        public const int CANDLE_COUNT = 30;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public readonly int _emaLength;
        private const int BASE_ADX_LENGTH = 30;
        private const decimal STOP_LOSS_PERCENT = 0.4m;
        private const decimal TSL_PERCENT = 0.2m;
        
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private AverageDirectionalIndex _bADX;
        private bool _bADXLoaded = false, _bADXLoadedFromDB = false;
        private bool _bADXLoading = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        private bool _adxPeaked = false;
        private Instrument _activeCall;
        private Instrument _activePut;
        public const AlgoIndex algoIndex = AlgoIndex.MomentumBuyWithStraddle;
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
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        public MultiStraddleDirectionalShift(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength, 
            decimal targetProfit, decimal stopLoss, bool straddleShift, decimal thresholdRatio = 1.67m, 
            int algoInstance = 0, bool positionSizing = false, 
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _emaLength = emaLength;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            _straddleShift = straddleShift;
            _maxDistanceFromBInstrument = 500;
            _minDistanceFromBInstrument = 0;
            _thresholdRatio = thresholdRatio;
            _stopLossRatio = 1.3m;
            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();
            _bADX = new AverageDirectionalIndex();
            CandleSeries candleSeries = new CandleSeries();
            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);
            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _callOptionLoaded = new Dictionary<decimal, bool>();
            _putOptionLoaded = new Dictionary<decimal, bool>();

            _tradeStrike = new Dictionary<decimal, int>();
            //ONLY ON EXPIRY SHOULD THIS BE INCREASED
            _maxTradePerStrike = 2;
            _callOrderTrios = new Dictionary<decimal, OrderTrio>();
            _putOrderTrios = new Dictionary<decimal, OrderTrio>();

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            ////ZConnect.Login();
            ////KoConnect.Login();

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
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                _entryRatio = 1.2m;
                _exitRatio = 1.6m;

                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    if (tick.LastTradeTime != null)
                    {
                        UpdateOptionPrice(tick);
                        MonitorCandles(tick, currentTime);

                        CheckSL(token, currentTime);
                    }
                }

                //Closes all postions at 3:29 PM
                TriggerEODPositionClose(currentTime);

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
            Instrument option = OptionsDictionary[tick.InstrumentToken];
            Instrument call, put;

            call = StraddleUniverse[option.Strike][Constants.CE];
            put = StraddleUniverse[option.Strike][Constants.PE];

            if (option.InstrumentType.Trim(' ').ToLower() == "ce")
            {
                call.LastPrice = tick.LastPrice;
            }
            else
            {
                put.LastPrice = tick.LastPrice;
            }
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
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 10, 00))// && _referenceStraddleValue != 0)
            {
                foreach(var tradedStraddle in StraddleUniverse)
                {
                    //(tradedStraddle.Value[0].IsTraded)
                    //
                    if (_callOrderTrios.ContainsKey(tradedStraddle.Value[0].Strike))
                    {
                        CancelOrder(currentTime.Value, _callOrderTrios[tradedStraddle.Value[0].Strike].SLOrder);
                        TradeEntry(tradedStraddle.Value[0], currentTime.Value, tradedStraddle.Value[0].LastPrice, _tradeQty, true);
                    }
                    //
                    // (tradedStraddle.Value[1].IsTraded)
                    //{
                    if (_putOrderTrios.ContainsKey(tradedStraddle.Value[0].Strike))
                    {
                        CancelOrder(currentTime.Value, _putOrderTrios[tradedStraddle.Value[1].Strike].SLOrder);
                        TradeEntry(tradedStraddle.Value[1], currentTime.Value, tradedStraddle.Value[1].LastPrice, _tradeQty, true);
                    }
                    //}
                }
                _stopTrade = true;
            }
        }

        private void LoadBInstrumentADX(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_bADX)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_bADXLoading)
                    {
                        _bADXLoading = true;
                        Task task = Task.Run(() => LoadBaseInstrumentADX(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _bADXLoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            //_bADX.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);
                            _bADX.Process(TimeCandles[bToken].First());

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var candle in TimeCandles[bToken].Skip(1))
                            {
                                _bADX.Process(candle);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _bADXLoadedFromDB)
                    {
                        _bADXLoaded = true;
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} ADX loaded from DB for Base Instrument", BASE_ADX_LENGTH), "LoadBInstrumentEMA");
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
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            if (e.InstrumentToken != _baseInstrumentToken)
            {
                Instrument option = OptionsDictionary[e.InstrumentToken];
                Instrument call, put;
                DateTime currentTime = e.CloseTime;

                call = StraddleUniverse[option.Strike][Constants.CE];
                put = StraddleUniverse[option.Strike][Constants.PE];

                if (option.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    call.LastPrice = e.ClosePrice;
                    _callOptionLoaded[call.Strike] = true;
                }
                else
                {
                    put.LastPrice = e.ClosePrice;
                    _putOptionLoaded[put.Strike] = true;
                }
                if (call.LastPrice * put.LastPrice != 0 && _callOptionLoaded[call.Strike] && _putOptionLoaded[put.Strike])
                {
                    _callOptionLoaded[call.Strike] = false;
                    _putOptionLoaded[put.Strike] = false;
                    decimal premiumRatio = call.LastPrice > put.LastPrice ? call.LastPrice / put.LastPrice : put.LastPrice / call.LastPrice;

                   // _totalEntries = 6;
                    if (premiumRatio < _entryRatio && !call.IsTraded && !put.IsTraded 
                        && _tradeStrike[option.Strike] < _maxTradePerStrike && _tradeStrike.Sum(x=>x.Value) <= _totalEntries)
                    {
                        TradeEntry(call, currentTime, call.LastPrice, _tradeQty, false);
                        TradeEntry(put, currentTime, put.LastPrice, _tradeQty, false);
                        _tradeStrike[option.Strike]++;
                    }
                    else if (call.IsTraded && !put.IsTraded)
                    {
                        if (call.LastPrice / put.LastPrice > _entryRatio)
                        {
                            TradeEntry(put, currentTime, put.LastPrice, _tradeQty, false);
                            _tradeStrike[option.Strike]++;
                        }
                        else
                        {
                            TrailSL(call, currentTime, _tradeQty);
                        }
                    }
                    else if (put.IsTraded && !call.IsTraded)
                    {
                        if (put.LastPrice / call.LastPrice > _entryRatio)
                        {
                            TradeEntry(call, currentTime, call.LastPrice, _tradeQty, false);
                            _tradeStrike[option.Strike]++;
                        }
                        else
                        {
                            TrailSL(put, currentTime, _tradeQty);
                        }
                    }
                    else if (premiumRatio > _exitRatio || 
                        ((_callOrderTrios.ContainsKey(call.Strike) && _putOrderTrios.ContainsKey(put.Strike)) 
                        && ((call.LastPrice + put.LastPrice) - (_callOrderTrios[call.Strike].Order.AveragePrice + _putOrderTrios[put.Strike].Order.AveragePrice) > 50)))
                    {
                        //Buy back the one with high premium only

                        if (call.LastPrice > put.LastPrice && call.IsTraded)
                        {

                            //First Cancel the earlier SLM and the buy
                            CancelOrder(currentTime, _callOrderTrios[call.Strike].SLOrder);
                            //buy back call
                            TradeEntry(call, currentTime, call.LastPrice, _tradeQty, true, put);
                        }
                        if (put.LastPrice > call.LastPrice && put.IsTraded)
                        {
                            //First Cancel the earlier SLM and the buy
                            CancelOrder(currentTime, _putOrderTrios[put.Strike].SLOrder);

                            //buy back put
                            TradeEntry(put, currentTime, put.LastPrice, _tradeQty, true, call);
                        }
                    }
                }
            }
        }
        private void TrailSL(Instrument option, DateTime currentTime, int quantity)
        {
            if (option.TSL >= option.LastPrice * (1 + TSL_PERCENT) && option.StopLoss > option.TSL)
            {
                option.StopLoss = option.LastPrice * (1 + TSL_PERCENT);
            }
            else if (option.StopLoss > option.LastPrice * (1 + STOP_LOSS_PERCENT) || option.StopLoss == 0)
            {
                option.StopLoss = option.StopLoss == 0 ? option.LastPrice * (1 + STOP_LOSS_PERCENT) : Math.Min(option.LastPrice * (1 + STOP_LOSS_PERCENT), option.StopLoss);
            }
            else
            {
                return;
            }





            //    if (option.StopLoss > option.LastPrice * (1+ STOP_LOSS_PERCENT) || option.StopLoss == 0 || option.TSL > option.LastPrice * (1 + TSL_PERCENT))
            //{
            //    if(option.LastPrice * (1 + STOP_LOSS_PERCENT) < option.TSL || (option.TSL < option.LastPrice * (1 + TSL_PERCENT) && option.StopLoss > option.TSL) || option.StopLoss == 0)
            //    {
            //        //Set SL to stoploss percent
            //        option.StopLoss = option.StopLoss == 0 ? option.LastPrice * (1 + STOP_LOSS_PERCENT) : Math.Min(option.LastPrice * (1 + STOP_LOSS_PERCENT), option.StopLoss);
            //    }
            //    else if (option.TSL >= option.LastPrice * (1 + TSL_PERCENT) && option.StopLoss > option.TSL)
            //    {
            //        option.StopLoss = option.LastPrice * (1 + TSL_PERCENT);
            //    }
            //    else
            //    {

            //    }

            OrderTrio ordertrio = (option.InstrumentType.Trim(' ').ToLower() == "ce") ? _callOrderTrios[option.Strike] : _putOrderTrios[option.Strike];

            //Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.StopLoss,
            //    MappedTokens[option.InstrumentToken], buyOrder:true, _tradeQty * Convert.ToInt32(option.LotSize),
            //    algoIndex, currentTime, Constants.ORDER_TYPE_SLM,triggerPrice: option.StopLoss, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());

            option.StopLoss = Math.Round(option.StopLoss * 20) / 20;
            if (ordertrio.SLOrder != null)
            {
                ordertrio.SLOrder = MarketOrders.ModifyKotakOrder(_algoInstance, algoIndex, option.StopLoss, ordertrio.SLOrder, currentTime, 
                    quantity:quantity * Convert.ToInt32(option.LotSize), httpClient: _httpClientFactory.CreateClient());
                OnTradeEntry(ordertrio.SLOrder);
            }

            //}
        }
        private void CheckSL(uint token, DateTime currentTime)
        {
            Instrument option = OptionsDictionary[token];

            if (option.StopLoss < option.LastPrice && option.StopLoss > 0)
            {

                Dictionary<decimal, OrderTrio> ordertrios = (option.InstrumentType.Trim(' ').ToLower() == "ce") ? _callOrderTrios : _putOrderTrios;

                if (ordertrios.ContainsKey(option.Strike))
                {
                    OrderTrio ordertrio = ordertrios[option.Strike];
                    //Set SL to stoploss percent
                    option.StopLoss = option.LastPrice;// * (1 + STOP_LOSS_PERCENT);

                    option.TradeExitPrice = option.StopLoss;
                    option.IsTraded = false;
                    option.TSL = 0;
                    //ordertrio.SLOrder.AveragePrice = option.TradeExitPrice;
                    //ordertrio.SLOrder.Price = option.TradeExitPrice;
                    //ordertrio.SLOrder.Status = Constants.ORDER_STATUS_CANCELLED;
                    //ordertrio.SLOrder.OrderTimestamp = currentTime;
                    MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, ordertrio.SLOrder);

#if local
                    Order order = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, ordertrio.SLOrder, currentTime, tag:"SL Hit Closure", httpClient: _httpClientFactory.CreateClient());
#endif
                    OnTradeEntry(ordertrio.SLOrder);
                    option.IsTraded = false;
                    option.StopLoss = 0;
                    ordertrios.Remove(option.Strike);
                }
            }
        }
        private Instrument TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder, Instrument oppositeOption = null)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                //ENTRY ORDER - Sell ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    MappedTokens[option.InstrumentToken], buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());


                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return option;
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty/_tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder?"Bought":"Sold"), "TradeEntry");

                option.TradedTime = currentTime;
                
                

                if (buyOrder)
                {
                    option.TradeExitPrice = order.AveragePrice;
                    option.IsTraded = false;
                    decimal pnl = Math.Max(option.TradeExitPrice - option.TradeEntryPrice, 0);
                    if (option.InstrumentType.Trim(' ').ToLower() == "ce")
                    {
                        _callOrderTrios.Remove(option.Strike);
                    }
                    else
                    {
                        _putOrderTrios.Remove(option.Strike);
                    }


                    if (oppositeOption != null)
                    {
                        oppositeOption.TSL = oppositeOption.TradeEntryPrice - pnl;
                        TrailSL(oppositeOption, currentTime, _tradeQty);
                    }
                }
                else
                {
                    option.TradeEntryPrice = order.AveragePrice;
                    option.IsTraded = true;
                    orderTrio = new OrderTrio();
                    
                    
                    //ENTRY SL ORDER - Sell ALERT
                    Order slorder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice * 2,
                        MappedTokens[option.InstrumentToken], !buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_SLM, triggerPrice:lastPrice*2, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());

                    orderTrio.Order = order;
                    orderTrio.SLOrder = slorder;

                    if (option.InstrumentType.Trim(' ').ToLower() == "ce")
                    {
                        _callOrderTrios.TryAdd(option.Strike, orderTrio);
                    }
                    else
                    {
                        _putOrderTrios.TryAdd(option.Strike, orderTrio);
                    }
                }
                //orderTrio = new OrderTrio();
                //orderTrio.Order = order;
                ////orderTrio.SLOrder = slOrder;
                //orderTrio.Option = option;
                //orderTrio.EntryTradeTime = currentTime;
                OnTradeEntry(order);
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
            return option;
        }
        private void CancelOrder(DateTime currentTime, Order order)
        {
            MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, httpClient: _httpClientFactory.CreateClient());
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
                if (StraddleUniverse == null 
                    || StraddleUniverse.Keys.First() >= _baseInstrumentPrice - _maxDistanceFromBInstrument
                    || StraddleUniverse.Keys.Last() <= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    Dictionary<uint, uint> mappedTokens;
                    DataLogic dl = new DataLogic();
                    Dictionary<uint, Instrument> optionsDictionary;
                    var OptionUniverse = dl.LoadCloseByStraddleOptions(_expiryDate, _baseInstrumentToken, 
                        _baseInstrumentPrice, _maxDistanceFromBInstrument + 300, out optionsDictionary, out mappedTokens);

                    
                    //OptionsDictionary = optionsDictionary;
                    StraddleUniverse ??= new SortedList<decimal, Instrument[]>();
                    OptionsDictionary ??= new Dictionary<uint, Instrument>();
                    MappedTokens ??= new Dictionary<uint, uint>();
                    foreach (var pair in OptionUniverse)
                    {
                        StraddleUniverse.TryAdd(pair.Key, pair.Value);
                        _callOptionLoaded.TryAdd(pair.Key, false);
                        _putOptionLoaded.TryAdd(pair.Key, false);
                        _tradeStrike.TryAdd(pair.Key, 0);
                    }
                    foreach (var pair in optionsDictionary)
                    {
                        OptionsDictionary.TryAdd(pair.Value.InstrumentToken, pair.Value);
                    }
                    foreach (var item in mappedTokens)
                    {
                        MappedTokens.TryAdd(item.Key, item.Value);
                    }

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
                if (OptionsDictionary != null)
                {
                    foreach (var optionPair in OptionsDictionary)
                    {
                        if (!SubscriptionTokens.Contains(optionPair.Value.InstrumentToken))
                        {
                            SubscriptionTokens.Add(optionPair.Value.InstrumentToken);
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
        private void LoadBaseInstrumentADX(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_bADX)
                {
                    CandleSeries cs = new CandleSeries();

                    //DataLogic dl = new DataLogic();

                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, 
                    //    lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    List<Candle> historicalCandles = cs.LoadCandles(candlesCount,
                      CandleType.Time, lastCandleEndTime, bToken.ToString(), _candleTimeSpan);

                    foreach (var candle in historicalCandles)
                    {
                        _bADX.Process(candle);
                    }
                    _bADXLoadedFromDB = true;
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
        private void Reset()
        {
            _stopTrade = false;
            _callOrderTrios = new Dictionary<decimal, OrderTrio>();
            _putOrderTrios = new Dictionary<decimal, OrderTrio>();
            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            StraddleUniverse = null;
            OptionsDictionary = null;
            MappedTokens = null;
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
            if(_bADX != null && _bADX.MovingAverage != null)
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
                String.Format("Current ADX: {0}", Decimal.Round(_bADX.MovingAverage.GetValue<decimal>(0), 2)),
                "Log_Timer_Elapsed");
            }
            if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Order != null
                && _straddlePutOrderTrio != null && _straddlePutOrderTrio.Order != null)
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
                String.Format("Call: {0}, Put: {1}. Straddle Profit: {2}. Current Ratio: {3}", _activeCall.LastPrice, _activePut.LastPrice,
                _straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice,
                _activeCall.LastPrice > _activePut.LastPrice ? Decimal.Round(_activeCall.LastPrice / _activePut.LastPrice, 2) : Decimal.Round(_activePut.LastPrice / _activeCall.LastPrice, 2)),
                "Log_Timer_Elapsed");

                Thread.Sleep(100);
            }
        }

    }
}
