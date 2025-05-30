﻿using Algorithms.Candles;
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
using System.Runtime.CompilerServices;
//using WebSocketSharp;

namespace Algorithms.Algorithms
{
    public class PriceVolumeRateOfChange : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(PriceVolumeRateOfChange source);
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

        public Dictionary<uint, decimal> tokenLastClose; // This will come from the close in today's ticks
        public Dictionary<uint, CentralPivotRange> tokenCPR;

        private OrderTrio _callOrderTrio;
        private OrderTrio _putOrderTrio;
        private bool _tradeexit;
        private bool _tpHit;
        private OrderTrio _orderTrio;
        private bool _noceTrade = false;
        private bool _nopeTrade = false;
        private decimal _pnl = 0;
        public List<Order> _pastOrders;
        private bool _stopTrade;

        private const int BASE_EMA_LENGTH = 200;
        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        Dictionary<uint, ExponentialMovingAverage> sTokenEMA;
        Dictionary<uint, ExponentialMovingAverage> signalTokenEMA;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;

        Dictionary<uint, IIndicatorValue> stokenEMAIndicator;
        Dictionary<uint, IIndicatorValue> ltokenEMAIndicator;
        Dictionary<uint, IIndicatorValue> signalEMAIndicator;
        private Dictionary<uint, bool> _belowEMA;
        private Dictionary<uint, bool> _aboveEMA;

        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        List<uint> _EMALoaded;
        List<uint> _SQLLoading;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;

        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private ExponentialMovingAverage _bEMA;
        private IIndicatorValue _bEMAValue;
        private bool _bEMALoaded = false, _bEMALoadedFromDB = false;
        private bool _bEMALoading = false;

        public const int CANDLE_COUNT = 30;
        public const int RSI_MID_POINT = 55;

        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;

        private readonly decimal _rsiUpperLimit;
        private readonly decimal _rsiLowerLimit;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly int _tradeQty;
        private readonly bool _positionSizing = false;
        private readonly decimal _maxLossPerTrade = 0;
        private readonly decimal _targetProfit;
        private readonly decimal _rsi;
        private readonly int _sEMALength;
        private readonly int _lEMALength;
        private readonly int _signalEMALength;
        private readonly decimal _stopLoss;

        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        public const int RSI_THRESHOLD = 60;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.EMACross;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        private User _user;
        public readonly decimal _emaBandForExit;
        public readonly decimal _rsiBandForExit;
        public readonly double _timeBandForExit;
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public PriceVolumeRateOfChange(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss, int sEMALength,
            int lEMALength, int signalEMALength = 200, int algoInstance = 0, bool positionSizing = false, 
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
           // _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _sEMALength = sEMALength;
            _lEMALength = lEMALength;
            _targetProfit = targetProfit;
            _stopLoss = stopLoss;
            _stopTrade = true;
            _signalEMALength = signalEMALength;
            _emaBandForExit = 0;
            _timeBandForExit = 2;
            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            _pastOrders = new List<Order>();

            SubscriptionTokens = new List<uint>();

            ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            stokenEMAIndicator = new Dictionary<uint, IIndicatorValue>();
            ltokenEMAIndicator = new Dictionary<uint, IIndicatorValue>();
            signalEMAIndicator = new Dictionary<uint, IIndicatorValue>();
            _belowEMA = new Dictionary<uint, bool>();
            _aboveEMA = new Dictionary<uint, bool>();
            //EMAs
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            sTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();
            signalTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            _bEMA = new ExponentialMovingAverage(BASE_EMA_LENGTH);
            _EMALoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            CandleSeries candleSeries = new CandleSeries();

            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _minDistanceFromBInstrument = 200;
            _maxDistanceFromBInstrument = 500;

        _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, Arg1: _sEMALength, Arg2: lEMALength,
                Arg3: _targetProfit, Arg4: _stopLoss, Arg5: _signalEMALength);

            ZConnect.Login();
            //KoConnect.Login(userId:uid);
            _user = KoConnect.GetUser(userId: uid);

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
        }

        public void LoadActiveOrders(Order activeCallOrder, Order activePutOrder)
        {
            if (activeCallOrder != null && activeCallOrder.OrderId != "")
            {
                _callOrderTrio = new OrderTrio();
                _callOrderTrio.Order = activeCallOrder;

                DataLogic dl = new DataLogic();
                Instrument option = dl.GetInstrument(activeCallOrder.Tradingsymbol);
                ActiveOptions.Add(option);
            }

            if (activePutOrder != null && activePutOrder.OrderId != "")
            {
                _putOrderTrio = new OrderTrio();
                _putOrderTrio.Order = activePutOrder;

                DataLogic dl = new DataLogic();
                Instrument option = dl.GetInstrument(activePutOrder.Tradingsymbol);
                ActiveOptions.Add(option);
            }
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                uint token = tick.InstrumentToken;
                lock (TimeCandles)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    MonitorCandles(tick, currentTime);

                    if (TimeCandles.ContainsKey(token) && ActiveOptions.All(x => x != null) && ActiveOptions.Any(x => x.InstrumentToken == token))
                    {
                        Instrument option = ActiveOptions.Find(x => x.InstrumentToken == token);

                        bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                        OrderTrio orderTrio = isOptionCall ? _callOrderTrio : _putOrderTrio;

                        TimeFrameCandle ct = TimeCandles[token].Last() as TimeFrameCandle;
                        TimeFrameCandle pt = TimeCandles[token].LastOrDefault(x => x.State == CandleStates.Finished) as TimeFrameCandle;

                        //check after 1 min from start of new candle
                        if (orderTrio == null 
                            && TimeCandles[token].Count > 5
                          //  && (currentTime - ct.OpenTime).TotalMinutes >= 1
                            && (currentTime - ct.OpenTime).TotalMinutes < 0.5//1.2
                            && tick.LastPrice > ct.OpenPrice
                            && ct.TotalVolume > TimeCandles[token].SkipLast(1).TakeLast(3).Average(x => x.TotalVolume) * 1.5m / 1
                            && currentTime.TimeOfDay <= new TimeSpan(15, 00, 00)
                            && (currentTime.TimeOfDay <= new TimeSpan(11, 00, 00) || currentTime.TimeOfDay >= new TimeSpan(12, 30, 00))
                            //&& ((isOptionCall && !_noceTrade) || (!isOptionCall && !_nopeTrade))
                            )
                        {
                            orderTrio = TradeEntry(option, currentTime, tick.LastPrice, _tradeQty, true);
                        }
                        if (orderTrio != null)
                        {
                            _tradeexit = CheckExit(orderTrio, tick.LastPrice, pt.LowPrice, currentTime);

                            if (_tradeexit)
                            {
                                orderTrio = null;
                            }
                        }

                        if(option.InstrumentType.Trim(' ').ToLower() == "ce")
                        {
                            _callOrderTrio = orderTrio;
                        }
                        else
                        {
                            _putOrderTrio = orderTrio;
                        }

                        //Put a hedge at 3:15 PM
                        TriggerEODPositionClose(tick.LastTradeTime, tick.LastPrice, tick.InstrumentToken);
                    }

                    //Put a hedge at 3:15 PM
                    // TriggerEODPositionClose(tick.LastTradeTime);
                }
                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private void TriggerEODPositionClose(DateTime? currentTime, decimal currentPrice, uint token)
        {
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 15, 00))
            {
                if(_putOrderTrio != null && _putOrderTrio.Option.InstrumentToken == token)
                {
                    _putOrderTrio.TPOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, _putOrderTrio.TPOrder, currentTime.Value).Result;
                    OnTradeExit(_putOrderTrio.TPOrder);
                    TradeExit(_putOrderTrio.Option, currentTime.Value, currentPrice, _tradeQty, false);
                    _putOrderTrio = null;
                    _tpHit = false;
                }
                if (_callOrderTrio != null && _callOrderTrio.Option.InstrumentToken == token)
                {
                    _callOrderTrio.TPOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, _callOrderTrio.TPOrder, currentTime.Value).Result;
                    OnTradeExit(_callOrderTrio.TPOrder);
                    TradeExit(_callOrderTrio.Option, currentTime.Value, currentPrice, _tradeQty, false);
                    _callOrderTrio = null;
                    _tpHit = false;
                }
                if (_callOrderTrio == null && _putOrderTrio == null)
                {
                    DataLogic dl = new DataLogic();
                    dl.UpdateAlgoPnl(_algoInstance, _pnl);

                    _stopTrade = true;
                    _pnl = 0;
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
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
#endif
                        //candle starts from there
                        candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION RIGHT NOW

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
        private bool CheckExit(OrderTrio _orderTrio, decimal currentPrice, decimal thresholdPrice, DateTime currentTime)
        {
            bool exit = false;
            //check for target profit
            if (currentPrice >= _orderTrio.Order.AveragePrice + _targetProfit)
            {
#if market
               _orderTrio.TPOrder = MarketOrders.GetOrder(_orderTrio.TPOrder.OrderId, _algoInstance, algoIndex, Constants.ORDER_STATUS_COMPLETE);
#elif local
                _orderTrio.TPOrder.Status = Constants.ORDER_STATUS_COMPLETE;
                _orderTrio.TPOrder.AveragePrice = currentPrice;
                _orderTrio.TPOrder.OrderTimestamp = currentTime;
                _orderTrio.TPOrder.ExchangeTimestamp = currentTime;
#endif
                MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, _orderTrio.TPOrder);
                OnTradeExit(_orderTrio.TPOrder);

                _pnl += _orderTrio.TPOrder.Quantity * _orderTrio.TPOrder.AveragePrice * (1);

                exit = true;
                _tpHit = true;
                _orderTrio = null;
                
            }
            //check for stoploss
            else if (currentPrice <= _orderTrio.Order.AveragePrice - _stopLoss
                || ((currentTime - _orderTrio.EntryTradeTime).TotalMinutes >= 15 )
                //|| (currentPrice < thresholdPrice)
                )
            //else if (currentPrice < _orderTrio.Order.AveragePrice)
            {

                _orderTrio.TPOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, _orderTrio.TPOrder, currentTime).Result;
                OnTradeExit(_orderTrio.TPOrder);
                TradeExit(_orderTrio.Option, currentTime, currentPrice, _tradeQty, false);
                
                if(_orderTrio.Option.InstrumentType.ToLower() == "ce")
                {
                    _noceTrade = true;
                }
                else
                {
                    _nopeTrade = true;
                }
                _orderTrio = null;
                _tpHit = false;
                exit = true;
            }
            return exit;
        }
        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                //ENTRY ORDER - BUY ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }

#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");
#endif

                Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, Math.Round((lastPrice + _targetProfit) * 20) / 20,
                    option.InstrumentToken, !buyOrder, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tpOrder.OrderTimestamp.Value,
                    string.Format("Placed Target Profit for {0} @ {1}", option.TradingSymbol, tpOrder.AveragePrice), "TradeEntry");
#endif


                _pnl += order.Quantity * order.AveragePrice * (buyOrder ? -1 : 1);
                orderTrio = new OrderTrio();
                orderTrio.Order = order;
                orderTrio.TPOrder = tpOrder;
                //orderTrio.SLOrder = slOrder;
                orderTrio.Option = option;
                orderTrio.EntryTradeTime = currentTime;
                OnTradeEntry(order);
                OnTradeEntry(tpOrder);
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
        private void TradeExit(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        {
            try
            {
                //ENTRY ORDER - BUY ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("EXIT!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeExit");
#endif

                _pnl += order.Quantity * order.AveragePrice * (buyOrder ? -1 : 1);
                OnTradeExit(order);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
            }
        }
        private void LoadBInstrumentEMA(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_bEMA)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_bEMALoading)
                    {
                        _bEMALoading = true;
                        Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _bEMALoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _bEMA.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var price in TimeCandles[bToken])
                            {
                                _bEMA.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _bEMALoadedFromDB)
                    {
                        _bEMALoaded = true;
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} EMA loaded from DB for Base Instrument", BASE_EMA_LENGTH), "LoadBInstrumentEMA");
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
        private void LoadHistoricalEMAs(DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

            try
            {
                var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_EMALoaded.Contains(x));
                StringBuilder sb = new StringBuilder();

                lock (lTokenEMA)
                {
                    foreach (uint t in tokens)
                    {
                        if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                        {
                            _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                        }

                        if (!lTokenEMA.ContainsKey(t) && !_SQLLoading.Contains(t))
                        {
                            sb.AppendFormat("{0},", t);
                            _SQLLoading.Add(t);
                        }
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');
                int firstCandleFormed = 0;

                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, _signalEMALength * 2, lastCandleEndTime));
                }
                foreach (uint tkn in tokens)
                {
                    if (TimeCandles.ContainsKey(tkn) && lTokenEMA.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            sTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            lTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            signalTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                sTokenEMA[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                                lTokenEMA[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                                signalTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            }
                        }
                    }
                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && lTokenEMA.ContainsKey(tkn))
                    {
                        _EMALoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("EMAs loaded from DB for {0}", tkn), "MonitorCandles");
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalEMAs");
                Thread.Sleep(100);
            }
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (e.InstrumentToken == _baseInstrumentToken)
                {
                    //if (_bEMALoaded)
                    //{
                    //    _bEMA.Process(e.ClosePrice, isFinal: true);
                    //}
                }
                else if (_EMALoaded.Contains(e.InstrumentToken))
                {
                    if (!lTokenEMA.ContainsKey(e.InstrumentToken) || !sTokenEMA.ContainsKey(e.InstrumentToken) || !signalTokenEMA.ContainsKey(e.InstrumentToken))
                    {
                        return;
                    }

                    sTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                    lTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                    signalTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);

                    if (ActiveOptions.Any(x => x.InstrumentToken == e.InstrumentToken))
                    {
                        Instrument option = ActiveOptions.Find(x => x.InstrumentToken == e.InstrumentToken);

                        decimal sema = sTokenEMA[e.InstrumentToken].GetValue<decimal>(0);
                        decimal lema = lTokenEMA[e.InstrumentToken].GetValue<decimal>(0);
                        decimal signalema = signalTokenEMA[e.InstrumentToken].GetValue<decimal>(0);

#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                            String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. sEMA:{6}. lEMA:{5}. Signal EMA: {7}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
                            , option.TradingSymbol, Decimal.Round(sema, 2), Decimal.Round(lema, 2), Decimal.Round(signalema, 2)), "CandleManger_TimeCandleFinished");
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CandleManger_TimeCandleFinished");
                Thread.Sleep(100);
            }
        }
        //private OrderTrio TrailMarket(uint token, bool isOptionCall, DateTime currentTime,
        //     decimal lastPrice, IIndicatorValue bema, OrderTrio orderTrio)
        //{
        //    if (orderTrio.Option != null && orderTrio.Option.InstrumentToken == token)
        //    {
        //        Instrument option = orderTrio.Option;

        //        var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
        //        var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);
        //        OrderTrio newOrderTrio;
        //        IIndicatorValue sema, lema;
        //        decimal entryRSI;

        //        if (isOptionCall && option.Strike < _baseInstrumentPrice - _maxDistanceFromBInstrument
        //            && stokenEMAIndicator.TryGetValue(activeCE.Value.InstrumentToken, out sema)
        //            && ltokenEMAIndicator.TryGetValue(activeCE.Value.InstrumentToken, out lema)
        //            && TimeCandles.ContainsKey(activeCE.Value.InstrumentToken)
        //            && CheckEntryCriteria(activeCE.Value.InstrumentToken, currentTime, sema, lema, bema, isOptionCall, out entryRSI))
        //        {

        //            newOrderTrio = PlaceTrailingOrder(option, activeCE.Value, lastPrice, currentTime, orderTrio, entryRSI);
        //            newOrderTrio.EntryRSI = entryRSI;

        //            ActiveOptions.Remove(option);
        //            ActiveOptions.Add(activeCE.Value);
        //            return newOrderTrio;
        //        }
        //        else if (!isOptionCall && option.Strike > _baseInstrumentPrice + _maxDistanceFromBInstrument
        //            && stokenEMAIndicator.TryGetValue(activePE.Value.InstrumentToken, out sema)
        //            && ltokenEMAIndicator.TryGetValue(activePE.Value.InstrumentToken, out lema)
        //            && TimeCandles.ContainsKey(activePE.Value.InstrumentToken)
        //            && CheckEntryCriteria(activePE.Value.InstrumentToken, currentTime, sema, lema, bema, isOptionCall, out entryRSI))
        //        {
        //            newOrderTrio = PlaceTrailingOrder(option, activePE.Value, lastPrice, currentTime, orderTrio, entryRSI);
        //            newOrderTrio.EntryRSI = entryRSI;

        //            ActiveOptions.Remove(option);
        //            ActiveOptions.Add(activePE.Value);
        //            return newOrderTrio;
        //        }
        //    }
        //    return orderTrio;
        //}
        private OrderTrio PlaceTrailingOrder(Instrument option, Instrument newInstrument, decimal lastPrice,
            DateTime currentTime, OrderTrio orderTrio, decimal entryRSI)
        {
            try
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                        "Trailing market...", "PlaceTrailingOrder");

                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType,
                    lastPrice, option.InstrumentToken, false,
                    _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Closed Option {0}. Bought {1} lots @ {2}.", option.TradingSymbol,
                        _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                OnTradeExit(order);

                //Order slorder = orderTrio.SLOrder;
                //if (slorder != null)
                //{
                //    slorder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slorder, currentTime).Result;
                //    OnTradeExit(slorder);
                //}
                //Order tporder = orderTrio.TPOrder;
                //if (tporder != null)
                //{
                //    tporder = MarketOrders.CancelOrder(_algoInstance, algoIndex, tporder, currentTime).Result;
                //    OnTradeExit(tporder);
                //}

                decimal stopLoss = 0, targetProfit = 0;
                GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);


                lastPrice = TimeCandles[newInstrument.InstrumentToken].Last().ClosePrice;
                order = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, lastPrice, //newInstrument.LastPrice,
                    newInstrument.InstrumentToken, true, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       string.Format("Traded Option {0}. Sold {1} lots @ {2}.", newInstrument.TradingSymbol,
                       _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                //slorder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, stopLoss,
                //   newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                //   algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                ////target profit
                //tporder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, targetProfit,
                //    newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tporder.OrderTimestamp.Value,
                //    string.Format("Placed Target Profit for {0} lots of {1} @ {2}", _tradeQty, newInstrument.TradingSymbol, tporder.AveragePrice), "TradeEntry");

                orderTrio = new OrderTrio();
                orderTrio.Option = newInstrument;
                orderTrio.Order = order;
                //orderTrio.SLOrder = slorder;
                //orderTrio.TPOrder = tporder;
                orderTrio.StopLoss = stopLoss;
                orderTrio.TargetProfit = targetProfit;
                orderTrio.EntryRSI = entryRSI;
                orderTrio.EntryTradeTime = currentTime;

                OnTradeEntry(order);
                //OnTradeEntry(slorder);
                //OnTradeEntry(tporder);

                return orderTrio;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "PlaceTrailingOrder");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
            return orderTrio;

        }
        private OrderTrio TradeEntry(uint token, decimal lastPrice, DateTime currentTime,
            IIndicatorValue sema, IIndicatorValue lema, IIndicatorValue bema, IIndicatorValue signalEMA, bool isOptionCall)
        {
            OrderTrio orderTrio = null;
            try
            {
                Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);
                decimal entryRSI = 0;
                if (option != null && CheckEntryCriteria(token, currentTime, sema, lema, bema, signalEMA, isOptionCall, lastPrice, out entryRSI))
                {
                    decimal stopLoss = 0, targetProfit = 0;
                    GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);

                    int tradeQty = GetTradeQty(lastPrice - stopLoss, option.LotSize);
                    //ENTRY ORDER - BUY ALERT
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                        token, true, tradeQty * Convert.ToInt32(option.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, option.TradingSymbol, order.AveragePrice), "TradeEntry");

                    ////SL for first orders
                    //Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, stopLoss,
                    //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
                    //    string.Format("Placed Stop Loss for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, slOrder.AveragePrice), "TradeEntry");

                    ////target profit
                    //Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, targetProfit,
                    //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
                    //    string.Format("Placed Target Profit for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, tpOrder.AveragePrice), "TradeEntry");

                    orderTrio = new OrderTrio();
                    orderTrio.Option = option;
                    orderTrio.Order = order;
                    //orderTrio.SLOrder = slOrder;
                    //orderTrio.TPOrder = tpOrder;
                    orderTrio.StopLoss = stopLoss;
                    orderTrio.TargetProfit = targetProfit;
                    orderTrio.EntryRSI = entryRSI;
                    orderTrio.EntryTradeTime = currentTime;

                    OnTradeEntry(order);
                    //OnTradeEntry(slOrder);
                    //OnTradeEntry(tpOrder);
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading stopped");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
            return orderTrio;
        }
        private int GetTradeQty(decimal maxlossInPoints, uint lotSize)
        {
            return _tradeQty;
        }
        private void GetOrder2PriceQty(int firstLegQty, decimal firstMaxLoss,
            Candle previousCandle, uint lotSize, out int qty, out decimal price)
        {
            decimal buffer = _maxLossPerTrade - firstLegQty * lotSize * firstMaxLoss;

            decimal candleSize = previousCandle.ClosePrice - previousCandle.OpenPrice;

            price = previousCandle.ClosePrice - (candleSize * 0.2m);
            price = Math.Round(price * 20) / 20;

            qty = Convert.ToInt32(Math.Ceiling((buffer / price) / lotSize));
        }
        public void StopTrade()
        {
            _stopTrade = true;
        }

        /// <summary>
        ///Entry Logic:
        ///1) Delta should be less than MIN Delta
        ///2) Distance from strike price greater than minimum distance
        ///3) RSI should be lower than EMA on RSI
        ///4) RSI should be in the range of 55 to 40
        /// </summary>
        /// <param name="token"></param>
        /// <param name="previousCandle"></param>
        /// <returns></returns>
        private bool CheckEntryCriteria(uint token, DateTime currentTime, IIndicatorValue sema,
            IIndicatorValue lema, IIndicatorValue bema, IIndicatorValue signalEMA, bool isOptionCall, decimal currentPrice, out decimal entryRSI)
        {
            entryRSI = 0;
            try
            {
                if (!sTokenEMA.ContainsKey(token) || !lTokenEMA.ContainsKey(token) || !signalTokenEMA.ContainsKey(token))
                {
                    return false;
                }

                decimal semaValue = sema.GetValue<decimal>();
                decimal lemaValue = lema.GetValue<decimal>();
                decimal bemaValue = bema == null ? 0 : bema.GetValue<decimal>();
                decimal signalemaValue = signalEMA.GetValue<decimal>();
                //entryRSI = rsiValue;

                if (semaValue < lemaValue)
                {
                    _belowEMA[token] = true;
                }

                /// Entry Criteria:
                /// RSI Cross EMA from below, below 40 and both crosses above 40
                /// RSI Cross EMA from below between 40 and 50

                if (sema.IsFormed && lema.IsFormed
                    && semaValue > lemaValue
                    && _belowEMA[token]
                    && currentPrice < semaValue + 2
                    //&& sTokenEMA[token].GetValue<decimal>(0) < lTokenEMA[token].GetValue<decimal>(0)
                    && ((isOptionCall && (_baseInstrumentPrice >= bemaValue || bemaValue == 0))
                    || (!isOptionCall && (_baseInstrumentPrice <= bemaValue || bemaValue == 0)))
                    && (lemaValue > signalemaValue)
                    )
                {
                    _belowEMA[token] = false;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
                Thread.Sleep(100);
            }
            return false;
        }
        private bool CheckExitCriteria(uint token, decimal lastPrice, DateTime currentTime,
            OrderTrio orderTrio, IIndicatorValue sema, IIndicatorValue lema, out bool tpHit)
        {
            /// Exit Criteria:
            /// RSI cross EMA from above and both crosses below 60, and both comes below 60
            /// RSI cross EMA from above between 50 and 60.
            /// Target profit hit
            tpHit = false;
            try
            {
                decimal semaValue = sema.GetValue<decimal>();
                decimal lemaValue = lema.GetValue<decimal>();

                if (lastPrice > orderTrio.TargetProfit)
                {
                    tpHit = true;
                    return true;
                }

                if ((lastPrice <= orderTrio.StopLoss)
                    || ((currentTime - orderTrio.EntryTradeTime).TotalMinutes > _timeBandForExit && semaValue < lemaValue - _emaBandForExit)
                    || ((currentTime - orderTrio.EntryTradeTime).TotalMinutes > 15)
                    )
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
                Thread.Sleep(100);
            }
            return false;
        }

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
                var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

                //if (ActiveOptions.Count > 1)
                //{
                //    Instrument ce = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "ce");
                //    Instrument pe = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

                //    if (
                //       ((pe.Strike >= _baseInstrumentPrice + _minDistanceFromBInstrument && pe.Strike <= _baseInstrumentPrice +_maxDistanceFromBInstrument)
                //           || (_putOrderTrio != null && _putOrderTrio.Option != null && _putOrderTrio.Option.InstrumentToken == pe.InstrumentToken))
                //       && ((ce.Strike <= _baseInstrumentPrice - _minDistanceFromBInstrument && ce.Strike >= _baseInstrumentPrice -_maxDistanceFromBInstrument)
                //       || (_callOrderTrio != null && _callOrderTrio.Option != null && _callOrderTrio.Option.InstrumentToken == ce.InstrumentToken))
                //       )
                //    {
                //        return;
                //    }

                //}
                DataLogic dl = new DataLogic();
                Dictionary<uint, uint> mappedTokens;
                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                   || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
#endif
                    //Load options asynchronously
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out mappedTokens);

#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
#endif
                }

                var activePE = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
                var activeCE = OptionUniverse[(int)InstrumentType.CE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);


                //First time.
                if (ActiveOptions.Count == 0)
                {
                    ActiveOptions.Add(activeCE.Value);
                    ActiveOptions.Add(activePE.Value);
                }
                //Already loaded from last run
                else if (ActiveOptions.Count == 1)
                {
                    ActiveOptions.Add(ActiveOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value);
                }
                else if (ActiveOptions[0] == null)
                {
                    ActiveOptions[0] = ActiveOptions[1].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value;
                }
                else if (ActiveOptions[1] == null)
                {
                    ActiveOptions[1] = ActiveOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value;
                }
                else
                {
                    for (int i = 0; i < ActiveOptions.Count; i++)
                    {
                        Instrument option = ActiveOptions[i];
                        bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                        if (isOptionCall && _callOrderTrio == null)
                        {
                            ActiveOptions[i] = activeCE.Value;
                        }
                        if (!isOptionCall && _putOrderTrio == null)
                        {
                            ActiveOptions[i] = activePE.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        private bool TradeExit(uint token, DateTime currentTime, decimal lastPrice, OrderTrio orderTrio, IIndicatorValue sema, IIndicatorValue lema)
        {
            try
            {
                if (orderTrio.Option != null && orderTrio.Option.InstrumentToken == token)
                {
                    Instrument option = orderTrio.Option;
                    bool tpHit = false;

                    if (CheckExitCriteria(token, lastPrice, currentTime, orderTrio, sema, lema, out tpHit))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                               token, false, _tradeQty * Convert.ToInt32(option.LotSize),
                               algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                        if (tpHit)
                        {
                            //orderTrio.TPOrder = UpdateOrder(orderTrio.TPOrder, lastPrice, currentTime);
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            string.Format("Target profit Triggered. Exited the trade @ {0}", order.AveragePrice), "TradeExit");
                        }
                        else
                        {
                            //orderTrio.TPOrder = ModifyOrder(orderTrio.TPOrder, lastPrice, currentTime);
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            string.Format("Stop Loss Triggered. Exited the trade @ {0}", order.AveragePrice), "TradeExit");
                        }

                        //orderTrio.SLOrder = CancelSLOrder(orderTrio.SLOrder, currentTime);
                        //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Cancelled Stop Loss order", "TradeExit");

                        //OnTradeExit(orderTrio.SLOrder);
                        //OnTradeExit(orderTrio.TPOrder);
                        OnTradeExit(order);
                        //orderTrio.TPOrder = null;
                        //orderTrio.SLOrder = null;
                        orderTrio.Option = null;

                        ActiveOptions.Remove(option);

                        return true;
                    }
                }
            }
            catch (Exception exp)
            {
                _stopTrade = true;
                Logger.LogWrite(exp.Message + exp.StackTrace);
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
                Thread.Sleep(100);
            }
            return false;
        }

        private Order CancelSLOrder(Order slOrder, DateTime currentTime)
        {
#if market
                            
            //Cancel the target profit limit order
            slOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slOrder, currentTime).Result;
#elif local
            slOrder.ExchangeTimestamp = currentTime;
            slOrder.OrderTimestamp = currentTime;
            slOrder.Tag = "Test";
            slOrder.Status = Constants.ORDER_STATUS_CANCELLED;
#endif
            return slOrder;
        }
        private Order UpdateOrder(Order completedOrder, decimal lastPrice, DateTime currentTime)
        {
#if market
            completedOrder = MarketOrders.GetOrder(completedOrder.OrderId, _algoInstance, algoIndex, Constants.ORDER_STATUS_COMPLETE);
#elif local
            completedOrder.AveragePrice = lastPrice;
            completedOrder.Price = lastPrice;
            completedOrder.OrderType = Constants.ORDER_TYPE_LIMIT;
            completedOrder.ExchangeTimestamp = currentTime;
            completedOrder.OrderTimestamp = currentTime;
            completedOrder.Tag = "Test";
            completedOrder.Status = Constants.ORDER_STATUS_COMPLETE;
#endif
            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, completedOrder);
            return completedOrder;
        }

        private void GetCriticalLevels(decimal lastPrice, out decimal stopLoss, out decimal targetProfit)
        {
            stopLoss = 0; // Math.Round((lastPrice > _targetProfit ? lastPrice - _targetProfit : 2.0m) * 20) / 20;
            targetProfit = Math.Round((lastPrice + _targetProfit) * 20) / 20;
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            try
            {
                double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
                DateTime? candleStartTime = null;
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
                    if (dataUpdated)
                    {
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
#endif
                        Task task = Task.Run(() => OnOptionUniverseChange(this));
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        #region Historical Candle 
        private void LoadBaseInstrumentEMA(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_bEMA)
                {
                    DataLogic dl = new DataLogic();
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicalCandlePrices[bToken])
                    {
                        _bEMA.Process(price, isFinal: true);
                    }
                    _bEMALoadedFromDB = true;
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
        private void LoadHistoricalCandles(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (lTokenEMA)
                {
                    DataLogic dl = new DataLogic();

                    //The below is from ticks
                    //List<decimal> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, token.ToString(), _candleTimeSpan);
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan, false);

                    //The below is from candles
                    //List<decimal> historicalCandlePrices = dl.GetHistoricalClosePricesFromCandles(candlesCount, lastCandleEndTime, token, _candleTimeSpan);
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalClosePricesFromCandles(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);

                    ExponentialMovingAverage lema, sema, signalEMA; //.Process(candle.ClosePrice, isFinal: true)

                    foreach (uint t in historicalCandlePrices.Keys)
                    {
                        sema = new ExponentialMovingAverage(_sEMALength);
                        lema = new ExponentialMovingAverage(_lEMALength);
                        signalEMA = new ExponentialMovingAverage(_signalEMALength);
                        foreach (var price in historicalCandlePrices[t])
                        {
                            sema.Process(price, isFinal: true);
                            lema.Process(price, isFinal: true);
                            signalEMA.Process(price, isFinal: true);
                        }
                        sTokenEMA.Add(t, sema);
                        lTokenEMA.Add(t, lema);
                        signalTokenEMA.Add(t, signalEMA);
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error,
                    lastCandleEndTime, String.Format(@"Error occurred! Trading has stopped. {0}", ex.Message), "LoadHistoricalCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }

        }
        #endregion

        public int AlgoInstance
        {
            get
            { return _algoInstance; }
        }
        private bool GetBaseInstrumentPrice(Tick tick)
        {
            Tick baseInstrumentTick = tick.InstrumentToken == _baseInstrumentToken ? tick : null;
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)  //(strangleNode.BaseInstrumentPrice == 0)// * callOption.LastPrice * putOption.LastPrice == 0)
            {
                _baseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }

        public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry, int strikePriceIncrement)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);
            return nodeData;
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
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error,
                    tick.Timestamp.GetValueOrDefault(DateTime.UtcNow), String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                // Environment.Exit(0);
                return;
            }
        }

        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
            //expecting atleast 30 ticks in 1 min
            if (_healthCounter >= 30)
            {
                _healthCounter = 0;
#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
#endif
            }
            else
            {
#if !BACKTEST
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
#endif
            }
        }

        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        /// <summary>
        /// Modify TP LIMIT order to MARKET order
        /// </summary>
        /// <param name="instrument_tradingsymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <returns></returns>
        private Order ModifyOrder(Order tpOrder, decimal lastPrice, DateTime currentTime)
        {
            try
            {
                Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, 0, tpOrder, currentTime, currentmarketPrice: lastPrice);

                OnTradeEntry(order);
                return order;

            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ModifyOrder");
                Thread.Sleep(100);
                return null;
            }

        }


        #region CPR
        //private void LoadCPR()
        //{
        //    try
        //    {
        //        DataLogic dl = new DataLogic();
        //        DataSet dsDailyOHLC = dl.GetDailyOHLC(ActiveOptions.Select(x => x.InstrumentToken), _startDateTime);

        //        OHLC ohlc;
        //        CentralPivotRange cpr;
        //        foreach (DataRow dr in dsDailyOHLC.Tables[0].Rows)
        //        {
        //            ohlc = new OHLC();
        //            ohlc.Open = (decimal)dr["Open"];
        //            ohlc.High = (decimal)dr["High"];
        //            ohlc.Low = (decimal)dr["Low"];
        //            ohlc.Close = (decimal)dr["Close"];

        //            cpr = new CentralPivotRange(ohlc);

        //            if (!tokenCPR.ContainsKey(Convert.ToUInt32(dr["InstrumentToken"])))
        //                tokenCPR.Add(Convert.ToUInt32(dr["InstrumentToken"]), cpr);
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}

        /// <summary>
        /// Check if CPR is near by in the direction of breakout
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="up"></param>
        /// <returns></returns>
        //private bool CheckNoCPRNearBy(uint instrumentToken, decimal currentPrice, bool up)
        //{
        //    CentralPivotRange cpr;
        //    if (tokenCPR.TryGetValue(instrumentToken, out cpr))
        //    {
        //        decimal price = currentPrice * (1 + CPR_DISTANCE);
        //        if (up && ((price < cpr.Prices[(int)PivotLevel.CPR] && currentPrice > cpr.Prices[(int)PivotLevel.S1])
        //            || (price < cpr.Prices[(int)PivotLevel.S1] && currentPrice > cpr.Prices[(int)PivotLevel.S2])
        //            || (price < cpr.Prices[(int)PivotLevel.S2] && currentPrice > cpr.Prices[(int)PivotLevel.S3])
        //            || (price < cpr.Prices[(int)PivotLevel.R2] && currentPrice > cpr.Prices[(int)PivotLevel.R1])
        //            || (price < cpr.Prices[(int)PivotLevel.R3] && currentPrice > cpr.Prices[(int)PivotLevel.R2])
        //            || (price < cpr.Prices[(int)PivotLevel.R1] && currentPrice > cpr.Prices[(int)PivotLevel.CPR])
        //            || (currentPrice > cpr.Prices[(int)PivotLevel.UR3])
        //            ))
        //        {
        //            return true;
        //        }
        //        price = currentPrice * (1 - CPR_DISTANCE);
        //        if (!up && ((price > cpr.Prices[(int)PivotLevel.CPR] && currentPrice < cpr.Prices[(int)PivotLevel.R1])
        //            || (price > cpr.Prices[(int)PivotLevel.R1] && currentPrice < cpr.Prices[(int)PivotLevel.R2])
        //            || (price > cpr.Prices[(int)PivotLevel.R2] && currentPrice < cpr.Prices[(int)PivotLevel.R3])
        //            || (price > cpr.Prices[(int)PivotLevel.S1] && currentPrice < cpr.Prices[(int)PivotLevel.CPR])
        //            || (price > cpr.Prices[(int)PivotLevel.S2] && currentPrice < cpr.Prices[(int)PivotLevel.S1])
        //            || (price > cpr.Prices[(int)PivotLevel.S3] && currentPrice < cpr.Prices[(int)PivotLevel.S2])
        //            || (currentPrice < cpr.Prices[(int)PivotLevel.LS3])
        //            ))
        //        {
        //            return true;
        //        }
        //    }

        //return false;
        //}
        #endregion
    }
}
