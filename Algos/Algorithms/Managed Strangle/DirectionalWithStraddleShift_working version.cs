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
    public class DirectionalWithStraddleShift : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(DirectionalWithStraddleShift source);
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

        //public const int SHORT_EMA = 5;
        //public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        //public const int RSI_THRESHOLD = 60;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.MomentumBuyWithStraddle;
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
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;

        public DirectionalWithStraddleShift(DateTime endTime, TimeSpan candleTimeSpan,
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

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
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

                    //Check the straddle value at the start. and then check the difference after 5 mins
                    //Depending on change in the straddle value. Sell ATM options at the start AND store the straddle value OR buy/CE PE
                    //Monitor straddle value for the change in sign., then swithch between strandle and ce/pe buy

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
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 29, 00) && _referenceStraddleValue != 0)
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
                //Step 1: Set the reference straddle price at the start
                if (_referenceStraddleValue == 0 && e.CloseTime.TimeOfDay <= new TimeSpan(12, 30, 00) && _activeCall != null && _activePut != null)
                {
                    if (_activeCall.InstrumentToken == e.InstrumentToken)
                    {
                        _activeCall.LastPrice = e.ClosePrice;
                        callLoaded = true;
                        _strangleCallReferencePrice = _activeCall.LastPrice;
                        _referenceActiveCall = _activeCall;
                    }
                    else if (_activePut.InstrumentToken == e.InstrumentToken)
                    {
                        _activePut.LastPrice = e.ClosePrice;
                        putLoaded = true;
                        _stranglePutReferencePrice = _activePut.LastPrice;
                        _referenceActivePut = _activePut;
                    }

                    if (callLoaded && putLoaded)
                    {
                        _referenceStraddleValue = _strangleCallReferencePrice + _stranglePutReferencePrice;
                        callLoaded = false;
                        putLoaded = false;

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                            String.Format("Reference Straddle: {0} & {1}. Value: {2}",
                            _activeCall.TradingSymbol, _activePut.TradingSymbol,
                            _referenceStraddleValue), "CandleManger_TimeCandleFinished");

                        Thread.Sleep(100);
                    }
                }
                else if (_referenceStraddleValue != 0)
                {
                    //Step 2: In the next candle after refence straddle value has been set. Take decision to buy or sell
                    if (_activeCall.InstrumentToken == e.InstrumentToken)
                    {
                        _activeCall.LastPrice = e.ClosePrice;
                        callLoaded = true;
                    }
                    else if (_activePut.InstrumentToken == e.InstrumentToken)
                    {
                        _activePut.LastPrice = e.ClosePrice;
                        putLoaded = true;
                    }
                    if (_referenceActiveCall.InstrumentToken == e.InstrumentToken)
                    {
                        _referenceActiveCall.LastPrice = e.ClosePrice;
                        referenceCallLoaded = true;
                    }
                    else if (_referenceActivePut.InstrumentToken == e.InstrumentToken)
                    {
                        _referenceActivePut.LastPrice = e.ClosePrice;
                        referencePutLoaded = true;
                    }

                    if (callLoaded && putLoaded && referenceCallLoaded && referencePutLoaded)
                    {
                        callLoaded = false;
                        putLoaded = false;
                        referenceCallLoaded = false;
                        referencePutLoaded = false;

                        decimal currentValue = _referenceActiveCall.LastPrice + _referenceActivePut.LastPrice;
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                            String.Format("Straddle Current Value: {0}. Variance:{1}", currentValue, currentValue - _referenceStraddleValue), "CandleManger_TimeCandleFinished");

                        if (false && currentValue > _referenceStraddleValue)
                        {
                            if (!_buyMode)
                            {
                                _buyMode = true;
                                // _sellMode = false;
                                decimal callPriceDelta = _referenceActiveCall.LastPrice - _strangleCallReferencePrice;
                                decimal putPriceDelta = _referenceActivePut.LastPrice - _stranglePutReferencePrice;

                                //Buy the more expensive option
                                if (callPriceDelta >= putPriceDelta)
                                {
                                    if (_referenceActiveCall.InstrumentToken == _activeCall.InstrumentToken && _referenceActivePut.InstrumentToken == _activePut.InstrumentToken)
                                    {
                                        if (_soloCallOrderTrio == null)
                                        {
                                            if (_straddleCallOrderTrio != null)
                                            {
                                                _soloCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, 2 * _tradeQty, true);
                                                _straddleCallOrderTrio = null;
                                            }
                                            else
                                            {
                                                _soloCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, true);
                                            }
                                        }
                                        if (_straddlePutOrderTrio != null)
                                        {
                                            TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, true);
                                            _straddlePutOrderTrio = null;
                                        }
                                    }
                                    else if (_soloCallOrderTrio == null)
                                    {
                                        _soloCallOrderTrio = TradeEntry(_referenceActiveCall, e.CloseTime, _referenceActiveCall.LastPrice, _tradeQty, true);
                                    }
                                }
                                else
                                {
                                    if (_referenceActiveCall.InstrumentToken == _activeCall.InstrumentToken && _referenceActivePut.InstrumentToken == _activePut.InstrumentToken)
                                    {
                                        if (_soloPutOrderTrio == null)
                                        {
                                            if (_straddlePutOrderTrio != null)
                                            {
                                                _soloPutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, 2 * _tradeQty, true);
                                                _straddlePutOrderTrio = null;
                                            }
                                            else
                                            {
                                                _soloPutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, true);
                                            }
                                        }
                                        if (_straddleCallOrderTrio != null)
                                        {
                                            TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, true);
                                            _straddleCallOrderTrio = null;
                                        }
                                    }
                                    else if (_soloPutOrderTrio == null)
                                    {
                                        _soloPutOrderTrio = TradeEntry(_referenceActivePut, e.CloseTime, _referenceActivePut.LastPrice, _tradeQty, true);
                                    }
                                }
                                
                            }
                        }
                        else
                        {
                            if(_buyMode)
                            {
                                _buyMode = false;
                            }
                            _strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            _stranglePutReferencePrice = _referenceActivePut.LastPrice;
                            if (_straddleCallOrderTrio == null)
                            {
                                if (_soloCallOrderTrio != null && _referenceActiveCall.InstrumentToken == _activeCall.InstrumentToken)
                                {
                                    _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, 2 * _tradeQty, false);
                                    //_strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                                    _soloCallOrderTrio = null;
                                    //_initialValueOfCall = _straddleCallOrderTrio.Order.AveragePrice;
                                    _higherProfit = false;
                                }
                                else if (_soloCallOrderTrio != null)
                                {
                                    TradeEntry(_referenceActiveCall, e.CloseTime, _referenceActiveCall.LastPrice, _tradeQty, false);
                                    _soloCallOrderTrio = null;
                                }
                                else
                                {
                                    _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, false);
                                    //_strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                                    //_initialValueOfCall = _straddleCallOrderTrio.Order.AveragePrice;
                                    _higherProfit = false;
                                }
                            }
                            else if (_soloCallOrderTrio != null)
                            {
                                TradeEntry(_referenceActiveCall, e.CloseTime, _referenceActiveCall.LastPrice, _tradeQty, false);
                                _soloCallOrderTrio = null;
                            }
                            if (_straddlePutOrderTrio == null)
                            {
                                if (_soloPutOrderTrio != null && _referenceActivePut.InstrumentToken == _activePut.InstrumentToken)
                                {
                                    _straddlePutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, 2 * _tradeQty, false);
                                    //_stranglePutReferencePrice = _referenceActivePut.LastPrice;
                                    _soloPutOrderTrio = null;
                                    //_initialValueOfPut = _straddlePutOrderTrio.Order.AveragePrice;
                                    _higherProfit = false;
                                }
                                else if (_soloPutOrderTrio != null)
                                {
                                    TradeEntry(_referenceActivePut, e.CloseTime, _referenceActivePut.LastPrice, _tradeQty, false);
                                    _soloPutOrderTrio = null;
                                }
                                else
                                {
                                    _straddlePutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, false);
                                    //_stranglePutReferencePrice = _referenceActivePut.LastPrice;
                                   // _initialValueOfPut = _straddlePutOrderTrio.Order.AveragePrice;
                                    _higherProfit = false;
                                }
                            }
                            else if (_soloPutOrderTrio != null)
                            {
                                TradeEntry(_referenceActivePut, e.CloseTime, _referenceActivePut.LastPrice, _tradeQty, false);
                                _soloPutOrderTrio = null;
                            }

                            //if (_buyMode)
                            //{
                            //    _buyMode = false;
                            //    //close the buy options
                            //    if (_soloCallOrderTrio != null)
                            //    {
                            //        TradeEntry(_referenceActiveCall, e.CloseTime, _referenceActiveCall.LastPrice, _tradeQty, false);
                            //        _soloCallOrderTrio = null;
                            //    }
                            //    if (_soloPutOrderTrio != null)
                            //    {
                            //        TradeEntry(_referenceActivePut, e.CloseTime, _referenceActivePut.LastPrice, _tradeQty, false);
                            //        _soloPutOrderTrio = null;
                            //    }

                            //}

                            //if (!_sellMode)
                            //{
                            //    _sellMode = true;
                            //    if (_straddleCallOrderTrio == null)
                            //    {
                            //        if (_soloCallOrderTrio != null && _referenceActiveCall.InstrumentToken == _activeCall.InstrumentToken)
                            //        {
                            //            _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, 2 * _tradeQty, false);
                            //            _strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            //            _soloCallOrderTrio = null;
                            //        }
                            //        else if (_soloCallOrderTrio != null)
                            //        {
                            //            TradeEntry(_referenceActiveCall, e.CloseTime, _referenceActiveCall.LastPrice, _tradeQty, false);
                            //            _soloCallOrderTrio = null;
                            //        }
                            //        else
                            //        {
                            //            _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, false);
                            //            _strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            //        }
                            //    }
                            //    if (_straddlePutOrderTrio == null)
                            //    {
                            //        _straddlePutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, false);
                            //        _stranglePutReferencePrice = _referenceActivePut.LastPrice;
                            //    }
                            //}
                        }
                        decimal thresholdRatio = 1.67m;
                        decimal stopLossRatio = 1.67m;

                        if(_expiryDate.GetValueOrDefault(DateTime.Now).Date == e.CloseTime.Date)
                        {
                            thresholdRatio = 2.5m;
                            stopLossRatio = 1.67m;
                        }

                        if (_straddleShift
                            && _straddleCallOrderTrio != null && _straddlePutOrderTrio != null
                            && (_activeCall.LastPrice > _activePut.LastPrice * thresholdRatio || _activePut.LastPrice > _activeCall.LastPrice * thresholdRatio
                            || ((_activePut.LastPrice > _activeCall.LastPrice * stopLossRatio && (_straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice) < 10) && _higherProfit)
                            || ((_activeCall.LastPrice > _activePut.LastPrice * stopLossRatio && (_straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice) < 10) && _higherProfit))
                            )
                        {
                         
                            TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, true);
                            //_strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            
                            TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, true);
                            //_stranglePutReferencePrice = _referenceActivePut.LastPrice;

                            decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
                            var activeCE = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                            var activePE = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                            _activeCall = activeCE;
                            _activePut = activePE;

                            _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, false);
                            _straddlePutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, false);
                            _higherProfit = false;
                        }

                        if(!_higherProfit && _straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice > 20)
                        {
                            _higherProfit = true;
                        }

                            //if (_straddleShift && (_activeCall.LastPrice > _activePut.LastPrice * 2.4m || _activePut.LastPrice > _activeCall.LastPrice * 2.4m))
                            //{
                            //    if (_straddleCallOrderTrio != null)
                            //    {
                            //        TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, true);
                            //        _strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            //    }
                            //    if (_straddlePutOrderTrio != null)
                            //    {
                            //        TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, true);
                            //        _stranglePutReferencePrice = _referenceActivePut.LastPrice;
                            //    }

                            //    decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
                            //    var activeCE = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                            //    var activePE = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                            //    _activeCall = activeCE;
                            //    _activePut = activePE;

                            //    _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, false);
                            //    _straddlePutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, false);
                            //}
                            //else if (!_sellMode)
                            //{
                            //    _sellMode = true;
                            //    _buyMode = false;
                            //    //sell 2 * qty the bought option
                            //    //sell 1*qty the other option

                            //    if (_straddleCallOrderTrio == null)
                            //    {
                            //        _straddleCallOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, _tradeQty, false);
                            //        _strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            //    }
                            //    //else if (_straddleCallOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                            //    //{
                            //    //    _callOrderTrio = TradeEntry(_activeCall, e.CloseTime, _activeCall.LastPrice, 2 * _tradeQty, false);
                            //    //    _strangleCallReferencePrice = _referenceActiveCall.LastPrice;
                            //    //}

                            //    if (_straddlePutOrderTrio == null)
                            //    {
                            //        _straddlePutOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, _tradeQty, false);
                            //        _stranglePutReferencePrice = _referenceActivePut.LastPrice;
                            //    }
                            //    //else if (_putOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                            //    //{
                            //    //    _putOrderTrio = TradeEntry(_activePut, e.CloseTime, _activePut.LastPrice, 2 * _tradeQty, false);
                            //    //    _stranglePutReferencePrice = _referenceActivePut.LastPrice;
                            //    //}
                            //}
                       // }
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
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty/_tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder?"Bought":"Sold"), "TradeEntry");

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
                var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
                var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

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


                    if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Option != null && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_straddleCallOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.CE].Add(_straddleCallOrderTrio.Option.Strike, _straddleCallOrderTrio.Option);
                    }
                    if (_straddlePutOrderTrio != null && _straddlePutOrderTrio.Option != null && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_straddlePutOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.PE].Add(_straddlePutOrderTrio.Option.Strike, _straddlePutOrderTrio.Option);
                    }
                    if (_soloCallOrderTrio != null && _soloCallOrderTrio.Option != null && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_soloCallOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.CE].Add(_soloCallOrderTrio.Option.Strike, _soloCallOrderTrio.Option);
                    }
                    if (_soloPutOrderTrio != null && _soloPutOrderTrio.Option != null && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_soloPutOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.PE].Add(_soloPutOrderTrio.Option.Strike, _soloPutOrderTrio.Option);
                    }

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

                decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
                var activeCE = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                var activePE = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                if(_activeCall == null)
                {
                    _activeCall = activeCE;
                }
                if (_activePut == null)
                {
                    _activePut = activePE;
                }

                //First time.
                //if (ActiveOptions.Count == 0)
                //{
                //    ActiveOptions.Add(activeCE.Value);
                //    ActiveOptions.Add(activePE.Value);
                //}
                ////Already loaded from last run
                //else if (ActiveOptions.Count == 1)
                //{
                //    ActiveOptions.Add(ActiveOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value);
                //}
                //else if (ActiveOptions[0] == null)
                //{
                //    ActiveOptions[0] = ActiveOptions[1].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value;
                //}
                //else if (ActiveOptions[1] == null)
                //{
                //    ActiveOptions[1] = ActiveOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value;
                //}
                //else
                //{
                //    for (int i = 0; i < ActiveOptions.Count; i++)
                //    {
                //        Instrument option = ActiveOptions[i];
                //        bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                //        if (isOptionCall && _callOrderTrio == null)
                //        {
                //            ActiveOptions[i] = activeCE.Value;
                //        }
                //        if (!isOptionCall && _putOrderTrio == null)
                //        {
                //            ActiveOptions[i] = activePE.Value;
                //        }
                //    }
                //}
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
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, ticks[0].Timestamp.GetValueOrDefault(DateTime.UtcNow), String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                // Environment.Exit(0);
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
