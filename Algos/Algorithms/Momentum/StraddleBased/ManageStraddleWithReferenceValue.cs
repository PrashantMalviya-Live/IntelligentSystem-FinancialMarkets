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

namespace Algorithms.Algorithms
{
    public class ManageStraddleWithReferenceValue : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ManageStraddleWithReferenceValue source);
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
        private decimal _initialValueOfStraddle;
        private decimal _currentValueOfStraddle;
        private decimal _initialValueOfCall;
        private decimal _initialValueOfPut;

        private decimal _referenceStraddleValue;
        private decimal _referenceValueForStraddleShift;

        private bool _buyMode = false;
        private bool _sellMode = false;
        

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
        private decimal _bnfReference;
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

        private decimal _strangleCallReferencePrice;
        private decimal _stranglePutReferencePrice;
        private Instrument _activeCall;
        private Instrument _activePut;
        private Instrument _referenceActiveCall;
        private Instrument _referenceActivePut;
        DateTime _referenceStraddleTime;
        //public const int SHORT_EMA = 5;
        //public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        //public const int RSI_THRESHOLD = 60;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.ManageReferenceStraddle;
        //TimeSpan candletimeframe;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        private bool _straddleShift;
        bool callLoaded = false;
        bool putLoaded = false;
        bool referenceCallLoaded = false;
        bool referencePutLoaded = false;
        private decimal _targetProfit;
        private decimal _stopLoss;
        public List<uint> SubscriptionTokens { get; set; }
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        
        private bool _tpHit = false;
        private decimal _lossbooked = 0;
        private decimal _initialSellingPrice = 0;
        private decimal _initialCallSellingPrice = 0;
        private decimal _initialPutSellingPrice = 0;
        public ManageStraddleWithReferenceValue(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength,
            decimal targetProfit, decimal stopLoss, int algoInstance = 0,
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
            //_straddleShift = straddleShift;
            _maxDistanceFromBInstrument = 300;
            _minDistanceFromBInstrument = 0;

            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            CandleSeries candleSeries = new CandleSeries();
            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);
            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            //ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }

        /// <summary>
        /// No shifting on this one. Take the code from DirectionwithStraddle_working 20201210 for shifting code reference.
        /// </summary>
        /// <param name="tick"></param>
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

                    //Check the straddle value at the start. and then check the difference after 5 mins
                    //Depending on change in the straddle value. Sell ATM options at the start AND store the straddle value OR buy/CE PE
                    //Monitor straddle value for the change in sign., then swithch between strandle and ce/pe buy

                    if (_referenceStraddleValue == 0 && _activeCall != null && _activePut != null)
                    {
                        if (_activeCall.InstrumentToken == token)
                        {
                            _activeCall.LastPrice = tick.LastPrice;
                            callLoaded = true;
                            _strangleCallReferencePrice = _activeCall.LastPrice;
                        }
                        else if (_activePut.InstrumentToken == token)
                        {
                            _activePut.LastPrice = tick.LastPrice;
                            putLoaded = true;
                            _stranglePutReferencePrice = _activePut.LastPrice;
                        }

                        if (callLoaded && putLoaded)
                        {
                            _referenceStraddleValue = _strangleCallReferencePrice + _stranglePutReferencePrice;
                            _bnfReference = _baseInstrumentPrice;
                            callLoaded = false;
                            putLoaded = false;
                            _referenceStraddleTime = currentTime;
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                                String.Format("Reference Straddle: {0} & {1}. Value: {2}",
                                _activeCall.TradingSymbol, _activePut.TradingSymbol,
                                _referenceStraddleValue), "CandleManger_TimeCandleFinished");
                        }
                    }

                    //Closes all postions at 3:29 PM
                   TriggerEODPositionClose(currentTime);

                    Interlocked.Increment(ref _healthCounter);
                }
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

        private void TriggerEODPositionClose(DateTime? currentTime)
        {
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 20, 00) && _referenceStraddleValue != 0)
            {
                _referenceStraddleValue = 0;
                if (_straddleCallOrderTrio != null)
                {
                    Instrument option = _straddleCallOrderTrio.Option;

                    //exit trade
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                        option.InstrumentType, option.LastPrice, option.InstrumentToken,
                        _straddleCallOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" ? false : true,
                        _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                    OnTradeEntry(order);
                    _straddleCallOrderTrio = null;
                }
                if (_straddlePutOrderTrio != null)
                {
                    Instrument option = _straddlePutOrderTrio.Option;

                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                        option.InstrumentType, option.LastPrice, option.InstrumentToken,
                        _straddlePutOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" ? false : true,
                        _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                    OnTradeEntry(order);
                    _straddlePutOrderTrio = null;
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
        private void MonitorCandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (TimeCandles.ContainsKey(token))
                {
                    candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
                        //candle starts from there
                        candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
            }
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                uint token = e.InstrumentToken;
                decimal lastPrice = e.ClosePrice;
                DateTime currentTime = e.CloseTime;
                //Candle Logic
                if (_referenceStraddleValue != 0 )
                {
                    //Step 2: In the next candle after refence straddle value has been set. Take decision to buy or sell
                    if (_activeCall.InstrumentToken == token)
                    {
                        _activeCall.LastPrice = e.ClosePrice;
                        callLoaded = true;
                    }
                    else if (_activePut.InstrumentToken == token)
                    {
                        _activePut.LastPrice = e.ClosePrice;
                        putLoaded = true;
                    }

                    if (callLoaded && putLoaded)
                    {
                        callLoaded = false;
                        putLoaded = false;
                        decimal currentValue = _activeCall.LastPrice + _activePut.LastPrice;

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                            String.Format("Straddle Variance: {0}", _referenceStraddleValue - currentValue), "CandleManger_TimeCandleFinished");

                        if (currentValue > _referenceStraddleValue + 5)
                        {
                            //if (!_buyMode)
                            //{
                            //    _buyMode = true;
                                _tpHit = false;
                                if (_baseInstrumentPrice > _bnfReference)
                                {
                                    if (_straddlePutOrderTrio == null)
                                    {
                                        _straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                                    }
                                    if (_straddleCallOrderTrio != null)
                                    {
                                        _initialSellingPrice = _straddleCallOrderTrio.Order.AveragePrice;
                                        _straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, true);
                                        
                                        if(_initialSellingPrice != 0)
                                        {
                                            _lossbooked = _initialSellingPrice - _straddleCallOrderTrio.Order.AveragePrice;
                                        }
                                        _straddleCallOrderTrio = null;
                                    }
                                }
                                else
                                {
                                    if (_straddleCallOrderTrio == null)
                                    {
                                        _straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                                    }
                                    if (_straddlePutOrderTrio != null)
                                    {
                                        _initialSellingPrice = _straddlePutOrderTrio.Order.AveragePrice;
                                        _straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, true);
                                        if (_initialSellingPrice != 0)
                                        {
                                            _lossbooked = _initialSellingPrice - _straddlePutOrderTrio.Order.AveragePrice;
                                        }
                                        _straddlePutOrderTrio = null;
                                    }
                                }

                            //}
                        }
                        else
                        {
                            //if (_buyMode)
                            //{
                            //    _buyMode = false;
                            //}
                            _tpHit = false;
                            if (!_tpHit)
                            {
                                if (_straddleCallOrderTrio == null)
                                {
                                    _straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                                    if(_straddlePutOrderTrio != null)
                                    {
                                        _initialPutSellingPrice = _straddlePutOrderTrio.Order.AveragePrice;
                                    }
                                    //_initialSellingPrice = _straddleCallOrderTrio.Order.AveragePrice;
                                }
                                if (_straddlePutOrderTrio == null)
                                {
                                    _straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                                    if (_straddleCallOrderTrio != null)
                                    {
                                        _initialCallSellingPrice = _straddleCallOrderTrio.Order.AveragePrice;
                                    }
                                    //_initialSellingPrice = _straddlePutOrderTrio.Order.AveragePrice;
                                }
                                _lossbooked = 0;
                                //if (Math.Min(_straddlePutOrderTrio.Order.AveragePrice, _initialPutSellingPrice) + Math.Min(_straddleCallOrderTrio.Order.AveragePrice, _initialCallSellingPrice) + _lossbooked - currentValue > 20)
                                //{
                                //    _tpHit = true;
                                //    _lossbooked = 0;
                                //    _initialCallSellingPrice = 0;
                                //    _initialPutSellingPrice = 0;
                                //    TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, true);
                                //    TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, true);
                                //    _straddlePutOrderTrio = null;
                                //    _straddleCallOrderTrio = null;
                                //}
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CandleManger_TimeCandleFinished");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                //Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);

                decimal entryRSI = 0;
                ///TODO: Both previouscandle and currentcandle would be same now, as trade is getting generated at 
                ///candle close only. _pastorders check below is not needed anymore.
                //ENTRY ORDER - Sell ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }

                ////SL for first orders
                //Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,  option.InstrumentType, Math.Round(order.AveragePrice * 3 * 20) / 20,
                //    token, true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                //if (slOrder.Status == Constants.ORDER_STATUS_REJECTED)
                //{
                //    _stopTrade = true;
                //    return orderTrio;
                //}
                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                //   string.Format("TRADE!! Sold {0} lots of {1} @ {2}. Stop Loss @ {3}", _tradeQty, 
                //   option.TradingSymbol, order.AveragePrice, slOrder.AveragePrice), "TradeEntry");

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");

                orderTrio = new OrderTrio();
                orderTrio.Order = order;
                //orderTrio.SLOrder = slOrder;
                orderTrio.Option = option;
                orderTrio.EntryTradeTime = currentTime;

                OnTradeEntry(order);
                //OnTradeEntry(slOrder);

                //_pastOrders.Add(order);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
            }
            return orderTrio;
        }
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }


        private async void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (_activeCall == null || _activePut == null)
                {
                    decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    //OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument);

                    _activeCall = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, atmStrike, "ce");
                    _activePut = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, atmStrike, "pe");

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
                //Environment.Exit(0);
            }
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
                //Environment.Exit(0);
                lastEndTime = DateTime.Now;
                return null;
            }
        }

        private async void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                
                if (!SubscriptionTokens.Contains(_activeCall.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeCall.InstrumentToken);
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(_activePut.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activePut.InstrumentToken);
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                {
                    SubscriptionTokens.Add(_baseInstrumentToken);
                    dataUpdated = true;
                }
                if (dataUpdated)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, 
                        "Subscribing to new tokens", "UpdateInstrumentSubscription");
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), 
                    "UpdateInstrumentSubscription");
                Thread.Sleep(100);
                //Environment.Exit(0);
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow), String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
                Thread.Sleep(100);
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
                Thread.Sleep(100);
            }
        }

        //private void PublishLog(object sender, ElapsedEventArgs e)
        //{
        //    Instrument[] localOptions = new Instrument[ActiveOptions.Count];
        //    ActiveOptions.CopyTo(localOptions);
        //    foreach (Instrument option in localOptions)
        //    {
        //        IIndicatorValue rsi, ema;

        //        if (option != null && tokenRSIIndicator.TryGetValue(option.InstrumentToken, out rsi) && tokenEMAIndicator.TryGetValue(option.InstrumentToken, out ema))
        //        {
        //            decimal optionrsi = rsi.GetValue<decimal>();
        //            decimal optionema = ema.GetValue<decimal>();

        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
        //            String.Format("Active Option: {0}, RSI({3}):{1}, EMA on RSI({4}): {2}", option.TradingSymbol,
        //            Decimal.Round(optionrsi, 2), Decimal.Round(optionema, 2), rsi.IsFormed, ema.IsFormed), "Log_Timer_Elapsed");

        //            Thread.Sleep(100);
        //        }
        //    }
        //}

    }
}
