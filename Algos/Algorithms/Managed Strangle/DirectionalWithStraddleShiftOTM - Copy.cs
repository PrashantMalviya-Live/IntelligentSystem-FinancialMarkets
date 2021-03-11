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
    public class DirectionalWithStraddleShiftOTM : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(DirectionalWithStraddleShiftOTM source);
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

        private decimal _referenceStraddleValue;
        private decimal _referenceValueForStraddleShift;

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

        private uint _baseInstrumentToken;
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
        private decimal _targetProfit;
        private decimal _stopLoss;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        public DirectionalWithStraddleShiftOTM(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength, 
            decimal targetProfit, decimal stopLoss, bool straddleShift, decimal thresholdRatio = 1.67m, 
            int algoInstance = 0, bool positionSizing = false, 
            decimal maxLossPerTrade = 0)
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

                    if (token == _baseInstrumentToken)
                    {
                        MonitorCandles(tick, currentTime);
                        if (!_bADXLoaded)
                        {
                            LoadBInstrumentADX(token, BASE_ADX_LENGTH, currentTime);
                        }
                    }
                    //Update last price on options
                    if (OptionUniverse[0].All(x => x.Value != null) && OptionUniverse[0].Any(x => x.Value.InstrumentToken == token))
                    {
                        Instrument option = OptionUniverse[0].Values.First(x => x.InstrumentToken == token);
                        option.LastPrice = tick.LastPrice;
                    }
                    else if (OptionUniverse[1].All(x => x.Value != null) && OptionUniverse[1].Any(x => x.Value.InstrumentToken == token))
                    {
                        Instrument option = OptionUniverse[1].Values.First(x => x.InstrumentToken == token);
                        option.LastPrice = tick.LastPrice;
                    }

                    //Take trade after 9:20 AM only
                    if (currentTime.TimeOfDay <= new TimeSpan(09, 20, 00))
                    {
                        return;
                    }

                    if (_straddleCallOrderTrio == null 
                        && _straddlePutOrderTrio == null
                        && _bADX.IsFormed
                         )
                    {
                        if (_bADX.MovingAverage.GetValue<decimal>(0) < 45 && !_stopLossHit)
                        {
                            //decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

                            decimal ceStrike = Math.Round((_baseInstrumentPrice + 140) / 100) * 100;
                            decimal peStrike = Math.Round((_baseInstrumentPrice - 140) / 100) * 100;


                            _activeCall = OptionUniverse[(int)InstrumentType.CE][ceStrike];
                            _activePut = OptionUniverse[(int)InstrumentType.PE][peStrike];

                            _straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                            _straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                            _higherProfit = false;
                        }
                        else if (_stopLossHit && _adxPeaked)
                        {
                            _stopLossHit = false;
                            _adxPeaked = false;
                            Candle candle = TimeCandles[_baseInstrumentToken].Skip(1).LastOrDefault();

                            //decimal atmStrike = candle.ClosePrice < _baseInstrumentPrice? Math.Floor(_baseInstrumentPrice / 100) * 100 : Math.Ceiling(_baseInstrumentPrice / 100) * 100;

                            decimal ceStrike = Math.Round((_baseInstrumentPrice + 140) / 100) * 100;
                            decimal peStrike = Math.Round((_baseInstrumentPrice - 140) / 100) * 100;

                            _activeCall = OptionUniverse[(int)InstrumentType.CE][ceStrike];
                            _activePut = OptionUniverse[(int)InstrumentType.PE][peStrike];

                            _straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                            _straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                            _higherProfit = false;
                        }
                        
                        
                    }

                    //decimal thresholdRatio = 1.67m;
                    //decimal stopLossRatio = 1.30m;

                    //if (_expiryDate.GetValueOrDefault(DateTime.Now).Date == currentTime.Date)
                    //{
                    //    thresholdRatio = 2.5m;
                    //    stopLossRatio = 1.67m;
                    //}

                    if (_straddleCallOrderTrio != null && _straddlePutOrderTrio != null)
                    {
                        if (_straddleShift
                            && (_activeCall.LastPrice > _activePut.LastPrice * _thresholdRatio || _activePut.LastPrice > _activeCall.LastPrice * _thresholdRatio
                            || ((_activePut.LastPrice > _activeCall.LastPrice * _stopLossRatio && (_straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice) < 10) && _higherProfit)
                            || ((_activeCall.LastPrice > _activePut.LastPrice * _stopLossRatio && (_straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice) < 10) && _higherProfit))
                            )
                        {
                            //decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

                            decimal ceStrike = Math.Round((_baseInstrumentPrice + 140) / 100) * 100;
                            decimal peStrike = Math.Round((_baseInstrumentPrice - 140) / 100) * 100;

                            var activeCE = OptionUniverse[(int)InstrumentType.CE][ceStrike];
                            var activePE = OptionUniverse[(int)InstrumentType.PE][peStrike];

                            if (ceStrike != _activeCall.Strike && (activePE.LastPrice < activeCE.LastPrice ? activeCE.LastPrice < activePE.LastPrice * _thresholdRatio : activePE.LastPrice < activeCE.LastPrice * _thresholdRatio))
                            {
                                //_activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                                //_activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                                TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, true);
                                TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, true);

                                _activeCall = activeCE;
                                _activePut = activePE;

                                //_straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                                //_straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                                _higherProfit = false;
                                _straddleCallOrderTrio = null;
                                _straddlePutOrderTrio = null;
                            }
                        }
                        if (_straddlePutOrderTrio != null && _straddleCallOrderTrio != null)
                        {
                            if (!_higherProfit && _straddleCallOrderTrio.Order.AveragePrice +
                                _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice > 20)
                            {
                                _higherProfit = true;
                            }

                            if (_straddleCallOrderTrio.Order.AveragePrice +
                                _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice < Math.Abs(_stopLoss) * -1)
                            {
                                _stopLossHit = true;
                                CloseStraddle(currentTime);
                            }
                        }
                    }
                }

                //Closes all postions at 3:29 PM
                //TriggerEODPositionClose(currentTime);

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
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 29, 00))// && _referenceStraddleValue != 0)
            {
                _referenceStraddleValue = 0;
                if(_straddleCallOrderTrio != null)
                {
                    Instrument option = _straddleCallOrderTrio.Option;

                    //exit trade
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                        option.InstrumentType, option.LastPrice, option.InstrumentToken,
                        _straddleCallOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy"? false: true,
                        _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                    OnTradeEntry(order);
                }
                if (_straddlePutOrderTrio != null)
                {
                    Instrument option = _straddlePutOrderTrio.Option;

                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                        option.InstrumentType, option.LastPrice, option.InstrumentToken,
                        _straddlePutOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" ? false : true,
                        _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                    OnTradeEntry(order);
                }
                if (_soloCallOrderTrio != null)
                {
                    Instrument option = _soloCallOrderTrio.Option;

                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                        option.InstrumentType, option.LastPrice, option.InstrumentToken,
                        _soloCallOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" ? false : true,
                        _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                    OnTradeEntry(order);
                }
                if (_soloPutOrderTrio != null)
                {
                    Instrument option = _soloPutOrderTrio.Option;

                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                        option.InstrumentType, option.LastPrice, option.InstrumentToken,
                        _soloPutOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" ? false : true,
                        _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                    OnTradeEntry(order);
                }
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
            if (e.InstrumentToken == _baseInstrumentToken)
            {
                _bADX.Process(e);

                if(_bADX.IsFormed && (_bADX.MovingAverage.GetValue<decimal>(2) < _bADX.MovingAverage.GetValue<decimal>(1) && _bADX.MovingAverage.GetValue<decimal>(1) > _bADX.MovingAverage.GetValue<decimal>(0)))
                {
                    _adxPeaked = true;
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                        String.Format("ADX for Bank Nifty ({4}) :{0}. Candle OHLC: {0} | {1} | {2} | {3}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice,
                        Decimal.Round(_bADX.MovingAverage.GetCurrentValue<decimal>(), 2), _bADX.IsFormed), "CandleManger_TimeCandleFinished");

                Thread.Sleep(100);
            }
        }
        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                //ENTRY ORDER - Sell ALERT
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

                orderTrio = new OrderTrio();
                orderTrio.Order = order;
                //orderTrio.SLOrder = slOrder;
                orderTrio.Option = option;
                orderTrio.EntryTradeTime = currentTime;
                OnTradeEntry(order);
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


                    if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Option != null 
                        && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_straddleCallOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.CE].Add(_straddleCallOrderTrio.Option.Strike, _straddleCallOrderTrio.Option);
                    }
                    if (_straddlePutOrderTrio != null && _straddlePutOrderTrio.Option != null 
                        && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_straddlePutOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.PE].Add(_straddlePutOrderTrio.Option.Strike, _straddlePutOrderTrio.Option);
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
