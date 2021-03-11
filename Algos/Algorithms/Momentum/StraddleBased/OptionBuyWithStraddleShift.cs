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
using ZConnectWrapper;
using ZMQFacade;
using System.Timers;
using System.Threading;
using System.Net.Sockets;

namespace Algorithms.Algorithms
{
    public class OptionBuyWithStraddleShift : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionBuyWithStraddleShift source);
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

        public OrderTrio _soloCallOrderTrio;
        public OrderTrio _soloPutOrderTrio;
        private bool _tradeexit = false;
        private bool _tpHit = false;
        private bool _callBelowThrehold = true;
        private bool _putBelowThreshold = true;

        private decimal _referenceStraddleValue;
        private decimal _referenceValueForStraddleShift;
        private Strangle _referenceStrangle;
        public List<Order> _pastOrders;
        private bool _stopTrade;

        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;

        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        public const int CANDLE_COUNT = 30;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public readonly int _emaLength;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;

        private Instrument _activeCall;
        private Instrument _activePut;
        public const AlgoIndex algoIndex = AlgoIndex.OptionBuyWithStraddle;
        //TimeSpan candletimeframe;
        private bool _straddleShift;
        bool callLoaded = false;
        bool putLoaded = false;
        bool referenceCallLoaded = false;
        bool referencePutLoaded = false;
        private decimal _targetProfit;
        private decimal _stopLoss;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        public OptionBuyWithStraddleShift(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength, 
            decimal targetProfit, decimal stopLoss, bool straddleShift, int algoInstance = 0, 
            bool positionSizing = false, decimal maxLossPerTrade = 0)
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _emaLength = emaLength;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            _straddleShift = straddleShift;
            _maxDistanceFromBInstrument = 800;
            _minDistanceFromBInstrument = 0;

            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();

            CandleSeries candleSeries = new CandleSeries();
            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);
            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            ZConnect.Login();

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
                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);

                    //Update last price on options
                    if (OptionUniverse[0].All(x => x.Value != null) && OptionUniverse[0].Any(x => x.Value.InstrumentToken == token))
                    {
                        Instrument option = OptionUniverse[0].Values.First(x => x.InstrumentToken == token);
                        option.LastPrice = tick.LastPrice;
                        if(_referenceStrangle != null && _referenceStrangle.Call.InstrumentToken == token)
                        {
                            _referenceStrangle.Call.LastPrice = tick.LastPrice;
                        }
                    }
                    else if (OptionUniverse[1].All(x => x.Value != null) && OptionUniverse[1].Any(x => x.Value.InstrumentToken == token))
                    {
                        Instrument option = OptionUniverse[1].Values.First(x => x.InstrumentToken == token);
                        option.LastPrice = tick.LastPrice;
                        if (_referenceStrangle != null && _referenceStrangle.Put.InstrumentToken == token)
                        {
                            _referenceStrangle.Put.LastPrice = tick.LastPrice;
                        }
                    }

                   

                    ////Take trade after 9:20 AM only
                    //if (currentTime.TimeOfDay <= new TimeSpan(09, 20, 00))
                    //{
                    //    return;
                    //}

                    decimal thresholdRatio = 2m;
                    decimal stopLossRatio = 2m;

                    if (_expiryDate.GetValueOrDefault(DateTime.Now).Date == currentTime.Date)
                    {
                        thresholdRatio = 2.5m;
                        stopLossRatio = 1.67m;
                    }

                    if (_referenceStrangle == null || (_tradeexit && _tpHit && _straddleShift && _soloCallOrderTrio == null && _soloPutOrderTrio == null))
                    {
                        _tradeexit = false;
                        decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

                        if (_referenceStrangle == null || atmStrike != _referenceStrangle.Call.Strike)
                        {
                            _referenceStrangle = new Strangle();
                            _activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                            _activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];
                            _referenceStrangle.Call = _activeCall;
                            _referenceStrangle.Put = _activePut;
                        }
                    }
                    //update last trade prices
                    if (_referenceStrangle.Call.InstrumentToken == token)
                    {
                        _referenceStrangle.Call.LastPrice = tick.LastPrice;
                    }
                    else if (_referenceStrangle.Put.InstrumentToken == token)
                    {
                        _referenceStrangle.Put.LastPrice = tick.LastPrice;
                    }

                    if (_referenceStrangle.Call.LastPrice < _referenceStrangle.Put.LastPrice * thresholdRatio)
                    {
                        _callBelowThrehold = true;
                    }
                    else if (_referenceStrangle.Put.LastPrice < _referenceStrangle.Call.LastPrice * thresholdRatio)
                    {
                        _putBelowThreshold = true;
                    }

                    if (_referenceStrangle.Call.LastPrice * _referenceStrangle.Put.LastPrice != 0)
                    {
                        if (_soloCallOrderTrio == null && _referenceStrangle.Call.LastPrice > _referenceStrangle.Put.LastPrice * thresholdRatio && _callBelowThrehold)
                        {
                            _callBelowThrehold = false;
                            _soloCallOrderTrio = TradeEntry(_referenceStrangle.Call, currentTime, _referenceStrangle.Call.LastPrice, _tradeQty, true);
                        }
                        else if (_soloPutOrderTrio == null && _referenceStrangle.Put.LastPrice > _referenceStrangle.Call.LastPrice * thresholdRatio && _putBelowThreshold)
                        {
                            _soloPutOrderTrio = TradeEntry(_referenceStrangle.Put, currentTime, _referenceStrangle.Put.LastPrice, _tradeQty, true);
                            _putBelowThreshold = false;
                        }
                    }

                    

                        if (_soloCallOrderTrio != null)
                    {
                        _tradeexit = CheckExit(_soloCallOrderTrio, _referenceStrangle.Call.LastPrice, _referenceStrangle.Put.LastPrice * thresholdRatio, currentTime);

                        if (_tradeexit)
                        {
                            _soloCallOrderTrio = null;
                        }
                    }
                    else if (_soloPutOrderTrio != null)
                    {
                        _tradeexit = CheckExit(_soloPutOrderTrio, _referenceStrangle.Put.LastPrice, _referenceStrangle.Call.LastPrice * thresholdRatio, currentTime);
                        if (_tradeexit)
                        {
                            _soloPutOrderTrio = null;
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
#endif
                MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, _orderTrio.TPOrder);
                OnTradeExit(_orderTrio.TPOrder);
                exit = true;
                _tpHit = true;
                _orderTrio = null;
            }
            //check for stoploss
            else if (currentPrice <= _orderTrio.Order.AveragePrice - _stopLoss
                //|| ((currentTime - _orderTrio.EntryTradeTime).TotalMinutes >= 5 && currentPrice < _orderTrio.Order.AveragePrice && currentPrice < thresholdPrice))
                || (currentPrice < thresholdPrice))
            //else if (currentPrice < _orderTrio.Order.AveragePrice)
            {
                _orderTrio.TPOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, _orderTrio.TPOrder, currentTime).Result;
                OnTradeExit(_orderTrio.TPOrder);
                TradeExit(_orderTrio.Option, currentTime, currentPrice, _tradeQty, false);
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

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty/_tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder?"Bought":"Sold"), "TradeEntry");

                Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, Math.Round((lastPrice + _targetProfit) * 20) / 20,
                    option.InstrumentToken, !buyOrder, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tpOrder.OrderTimestamp.Value,
                    string.Format("Placed Target Profit for {0} @ {1}", option.TradingSymbol, tpOrder.AveragePrice), "TradeEntry");


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

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("EXIT!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeExit");

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
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument);


                    if (_soloCallOrderTrio != null && _soloCallOrderTrio.Option != null 
                        && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_soloCallOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.CE].Add(_soloCallOrderTrio.Option.Strike, _soloCallOrderTrio.Option);
                    }
                    if (_soloPutOrderTrio != null && _soloPutOrderTrio.Option != null 
                        && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_soloPutOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.PE].Add(_soloPutOrderTrio.Option.Strike, _soloPutOrderTrio.Option);
                    }

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

        public Task<bool> OnNext(Tick[] ticks)
        {
            try
            {
                if (_stopTrade || !ticks[0].Timestamp.HasValue)
                {
                    return Task.FromResult(false);
                }
                ActiveTradeIntraday(ticks[0]);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, ticks[0].Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                return Task.FromResult(false);
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
            if (_soloCallOrderTrio != null && _soloCallOrderTrio.Order != null
                && _soloPutOrderTrio != null && _soloPutOrderTrio.Order != null)
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
                String.Format("Call: {0}, Put: {1}. Straddle Profit: {2}. Current Ratio: {3}", _activeCall.LastPrice, _activePut.LastPrice,
                _soloCallOrderTrio.Order.AveragePrice + _soloPutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice,
                _activeCall.LastPrice > _activePut.LastPrice ? Decimal.Round(_activeCall.LastPrice / _activePut.LastPrice, 2) : Decimal.Round(_activePut.LastPrice / _activeCall.LastPrice, 2)),
                "Log_Timer_Elapsed");

                Thread.Sleep(100);
            }
        }

    }
}
