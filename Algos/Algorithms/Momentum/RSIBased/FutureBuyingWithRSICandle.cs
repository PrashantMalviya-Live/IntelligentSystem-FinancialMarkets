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
    public class FutureBuyWithRSICandle : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(FutureBuyWithRSICandle source);
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

        public Dictionary<uint, decimal> tokenLastClose; // This will come from the close in today's ticks
        public Dictionary<uint, CentralPivotRange> tokenCPR;

        //private OrderTrio _callOrderTrio;
        //private OrderTrio _putOrderTrio;
        private OrderTrio _orderTrio;

        private Instrument _activeFuture;
        public List<Order> _pastOrders;
        private bool _stopTrade;
        
        private const int BASE_EMA_LENGTH = 200;
        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;

        Dictionary<uint, IIndicatorValue> tokenEMAIndicator;
        Dictionary<uint, IIndicatorValue> tokenRSIIndicator;

        private decimal _baseEMAValue = 27950;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan, _rsiTimeSpan;
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

        private bool _breakKillerFlag = false;
        private bool _orbChecked = false;
        private OHLC _previousDayOHLC, _currentDayRangeOHLC;
        private bool _previousDayOHLCLoading = false, _previousDayOHLCLoaded = false;
        private bool _currentDayRangeLoaded = false;
        private decimal _currentDayRangeLow;
        private decimal _currentDayRangeHigh;

        private readonly decimal _rsiLowerLimit;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public const decimal CANDLE_BULLISH_LOWERWICK_FRACTION = 0.4m;
        private const decimal CANDLE_BULLISH_UPPERWICK_FRACTION = 0.2m;
        public const decimal CANDLE_BEARISH_UPPERWICK_FRACTION = 0.4m;
        private const decimal CANDLE_BEARISH_LOWERWICK_FRACTION = 0.2m;
        public const decimal CANDLE_DECISIVE_BODY_FRACTION = 0.55m;
        public const decimal CANDLE_BULLISH_BODY_PRICE_FRACTION = 0.04m;

        public readonly int _tradeQty;
        private readonly bool _positionSizing = false;
        private readonly decimal _maxLossPerTrade = 0;
        private readonly decimal _targetProfit;
        private readonly decimal _stopLoss;
        private readonly decimal _rsi;
        private readonly int _emaLength;
        private decimal _entryDecisiveCandleHighLow;
        private decimal _exitDecisiveCandleHighLow;

        private Candle _bCandle, _fCandle;
        private bool _fLoaded = false;
        private decimal _maxProfit = 0;
        private enum Trade
        {
            Buy = 1,
            Sell = 2,
            DoNotTrade = 3
        }

        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        public const int RSI_THRESHOLD = 60;
        private const int RSI_BAND = 1;
        private const int LOSSPERTRADE = 1000;
        private bool _entryAtCross = false;
        private Dictionary<uint, bool> _belowEMA;
        private Dictionary<uint, bool> _betweenEMAandUpperBand;
        private Dictionary<uint, bool> _aboveUpperBand;

        public const AlgoIndex algoIndex = AlgoIndex.MomentumBuyWithRSI;
        CandleManger _tradeCandleManger, _rsiCandleManager;
        Dictionary<uint, List<Candle>> TimeCandles, RSICandles;

        public readonly decimal _rsiBandForEntry;
        public readonly decimal _rsiBandForExit;
        public readonly double _timeBandForExit;
        private readonly decimal _futureBuyLevel;
        private readonly decimal _futureSellLevel;
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public FutureBuyWithRSICandle(DateTime endTime, TimeSpan candleTimeSpan, uint baseInstrumentToken, 
            DateTime? expiry, int quantity, decimal minDistanceFromBInstrument, decimal maxDistanceFromBInstrument,
            decimal rsiLowerLimitForEntry, decimal rsiUpperLimitForExit, decimal buyLevel, decimal sellLevel, decimal targetProfit, 
            int emaLength, decimal entryDecisiveCandleHighLow, decimal exitDecisiveCandleHighLow, int algoInstance = 0, bool entryatCross = false, 
            bool positionSizing = false, decimal maxLossPerTrade = 0 )
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _rsiTimeSpan = new TimeSpan(0, 30, 0);
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _minDistanceFromBInstrument = minDistanceFromBInstrument;
            _maxDistanceFromBInstrument = maxDistanceFromBInstrument;
            _rsiLowerLimit = rsiLowerLimitForEntry;
            _rsiUpperLimit = rsiUpperLimitForExit;
            _emaLength = emaLength;
            _targetProfit = targetProfit;
            _rsiBandForExit = 3;
            _timeBandForExit = 5;
            _rsiBandForEntry = 1.0m;
            _stopTrade = true;
            _futureBuyLevel = buyLevel;
            _futureSellLevel = sellLevel;
            _entryAtCross = entryatCross;
            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            _pastOrders = new List<Order>();
            _entryDecisiveCandleHighLow = entryDecisiveCandleHighLow;
            _exitDecisiveCandleHighLow = exitDecisiveCandleHighLow;
            SubscriptionTokens = new List<uint>();

            ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            RSICandles = new Dictionary<uint, List<Candle>>();


            tokenRSIIndicator = new Dictionary<uint, IIndicatorValue>();
            tokenEMAIndicator = new Dictionary<uint, IIndicatorValue>();

            //EMAs
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();
            _bEMA = new ExponentialMovingAverage(BASE_EMA_LENGTH);
            _EMALoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            CandleSeries candleSeries = new CandleSeries();
            _belowEMA = new Dictionary<uint, bool>();
            _aboveUpperBand = new Dictionary<uint, bool>();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            _tradeCandleManger = new CandleManger(TimeCandles, CandleType.Time);
            _tradeCandleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _rsiCandleManager = new CandleManger(RSICandles, CandleType.Time);
            _rsiCandleManager.TimeCandleFinished += RSICandleManager_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, Arg5: _minDistanceFromBInstrument, 
                Arg4: _maxDistanceFromBInstrument, lowerLimit: _rsiLowerLimit, 
                Arg1:_emaLength, Arg2:_targetProfit, upperLimit:_rsiUpperLimit, 
                Arg3: _futureBuyLevel, Arg6: _futureSellLevel, Arg7: _entryDecisiveCandleHighLow,
                Arg8: _exitDecisiveCandleHighLow);

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
        }

        public void LoadActiveOrders(Order activeOrder)
        {
            if (activeOrder != null && activeOrder.OrderId != "")
            {
                _orderTrio = new OrderTrio();
                _orderTrio.Order = activeOrder;

                DataLogic dl = new DataLogic();
                _activeFuture = dl.GetInstrument(activeOrder.Tradingsymbol);
                
                bool longTrade = _orderTrio.Order.TransactionType.ToLower() == "buy";

                decimal stopLoss, targetProfit;
                GetCriticalLevels(_orderTrio.Order.AveragePrice, 0, longTrade, out stopLoss, out targetProfit);
                _orderTrio.StopLoss = stopLoss;
                _orderTrio.TargetProfit = targetProfit;
            }
        }
        //public void LoadActiveOrders(Order activeCallOrder, Order activePutOrder)
        //{
        //    if (activeCallOrder != null && activeCallOrder.OrderId != "")
        //    {
        //        _callOrderTrio = new OrderTrio();
        //        _callOrderTrio.Order = activeCallOrder;

        //        DataLogic dl = new DataLogic();
        //        Instrument option = dl.GetInstrument(activeCallOrder.Tradingsymbol);
        //        ActiveOptions.Add(option);
        //        _callOrderTrio.Option = option;

        //        bool longTrade = _callOrderTrio.Order.TransactionType.ToLower() == "buy";

        //        decimal stopLoss, targetProfit;
        //        GetCriticalLevels(_callOrderTrio.Order.AveragePrice, 0, longTrade, out stopLoss, out targetProfit);
        //        _callOrderTrio.StopLoss = stopLoss;
        //        _callOrderTrio.TargetProfit = targetProfit;
        //    }

        //    if (activePutOrder != null && activePutOrder.OrderId != "")
        //    {
        //        _putOrderTrio = new OrderTrio();
        //        _putOrderTrio.Order = activePutOrder;

        //        DataLogic dl = new DataLogic();
        //        Instrument option = dl.GetInstrument(activeCallOrder.Tradingsymbol);
        //        ActiveOptions.Add(option);
        //        _putOrderTrio.Option = option;

        //        bool longTrade = _putOrderTrio.Order.TransactionType.ToLower() == "buy";
        //        decimal stopLoss, targetProfit;
        //        GetCriticalLevels(_putOrderTrio.Order.AveragePrice, 0, longTrade, out stopLoss, out targetProfit);
        //        _putOrderTrio.StopLoss = stopLoss;
        //        _putOrderTrio.TargetProfit = targetProfit;
        //    }
        //}

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
                    
                    //LoadOptionsToTrade(currentTime);
                    LoadFuturesToTrade(currentTime);

                    UpdateInstrumentSubscription(currentTime);
                    MonitorRSICandles(tick, currentTime);
                    
                    //MonitorRSICandles(tick, currentTime);
                    MonitorTradeCandles(tick, currentTime);

                    if (token == _baseInstrumentToken)
                    {
                        if (_futureSellLevel <= 0 && _futureBuyLevel <= 0)
                        {
                            if (!_bEMALoaded)
                            {
                                LoadBInstrumentEMA(token, BASE_EMA_LENGTH, currentTime);
                            }
                            //else
                            //{
                            //    _bEMAValue = _bEMA.Process(e.ClosePrice, isFinal: false);
                            //}
                        }
                    }
                    else if (tick.LastTradeTime.HasValue)
                    {
                        if (!_EMALoaded.Contains(token))
                        {
                            LoadHistoricalEMAs(currentTime);
                        }
                        if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                        {
                            return;
                        }

                        if (!_previousDayOHLCLoading)
                        {
                            _previousDayOHLCLoading = true;
                            _previousDayOHLC = PreviousDayRange(token, currentTime);
                            _previousDayOHLCLoaded = true;

                            if (!_breakKillerFlag && !_orbChecked)
                            {
                                if (currentTime.TimeOfDay >= new TimeSpan(09, 15, 0))
                                {
                                    _breakKillerFlag = tick.LastPrice > _previousDayOHLC.High || tick.LastPrice < _previousDayOHLC.Low ? true : false;

                                    _orbChecked = true;
                                }

                                if (_breakKillerFlag)
                                {
                                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                                       "Breakout Killer Flag Enabled", "CandleManger_TimeCandleFinished");
                                }
                            }
                        }
                    }

                    //Stop loss and profit only for decisive candles. Other SL on RSI is on candle end.
                    if (_orderTrio != null)
                    {
                        _orderTrio.Option.LastPrice = tick.LastPrice;
                        decimal currentProfit = tick.LastPrice - _orderTrio.Order.AveragePrice;

                        _maxProfit = Math.Max(currentProfit, _maxProfit);

                        if (_maxProfit > 80 && currentProfit <= _maxProfit * 0.615m)
                        {
                            //decimal rsi = tokenRSI[token].GetValue<decimal>(0);
                            //decimal ema = lTokenEMA[token].GetValue<decimal>(0);

                            //bool isOptionCall = _orderTrio.Option.InstrumentType.Trim(' ').ToLower() == "";

                            _orderTrio.StopLoss = tick.LastPrice;
                            //Stop loss and target profit live
                            if (TradeExit(token, tick.LastPrice, currentTime, rsiValue: 0, emaValue: 0, _bEMAValue, false))
                            {
                                _orderTrio = null;
                                _maxProfit = 0;
                            }
                        }

                        //IIndicatorValue rsi = tokenRSI[token].Process(tick.LastPrice, isFinal: false);
                        //IIndicatorValue ema = lTokenEMA[token].Process(rsi.GetValue<decimal>(), isFinal: false);

                        
                    }
                   
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
        //private void TriggerEODPositionClose(DateTime? currentTime)
        //{
        //    if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 29, 00))
        //    {
        //        OrderLinkedListNode orderNode = orderList.FirstOrderNode;

        //        if (orderNode != null)
        //            while (orderNode != null)
        //            {
        //                Order slOrder = orderNode.SLOrder;
        //                if (slOrder != null)
        //                {
        //                    MarketOrders.ModifyOrder(_algoInstance, algoIndex, 0, slOrder, currentTime.Value);
        //                    slOrder = null;
        //                }

        //                orderNode = orderNode.NextOrderNode;
        //            }

        //        Environment.Exit(0);
        //    }
        //}
        private void MonitorRSICandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (RSICandles.ContainsKey(token))
                {
                    _rsiCandleManager.StreamingShortTimeFrameCandle(tick, token, _rsiTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, _rsiTimeSpan, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
                        //candle starts from there
                        _rsiCandleManager.StreamingShortTimeFrameCandle(tick, token, _rsiTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

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

        private OHLC PreviousDayRange(uint token, DateTime currentDateTime)
        {
            DataLogic dl = new DataLogic();
            return dl.GetPreviousDayRange(token, currentDateTime);
        }

        private void MonitorTradeCandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;
                if (TimeCandles.ContainsKey(token))
                {
                    _tradeCandleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true);
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, _candleTimeSpan, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
                        //candle starts from there
                        _tradeCandleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

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

        private void LoadBInstrumentEMA(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, _rsiTimeSpan, out lastCandleEndTime);
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
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, _rsiTimeSpan, out lastCandleEndTime);

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
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, _emaLength * 2 + RSI_LENGTH, lastCandleEndTime));
                }
                foreach (uint tkn in tokens)
                {
                    if (TimeCandles.ContainsKey(tkn) && lTokenEMA.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            tokenRSI[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);

                            lTokenEMA[tkn].Process(tokenRSI[tkn].GetValue<decimal>(0), isFinal: true);
                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                tokenRSI[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                                lTokenEMA[tkn].Process(tokenRSI[tkn].GetValue<decimal>(0), isFinal: true);
                            }
                        }
                    }
                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && lTokenEMA.ContainsKey(tkn))
                    {
                        _EMALoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("EMA & RSI loaded from DB for {0}", tkn), "MonitorCandles");
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

        private void RSICandleManager_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (e.InstrumentToken == _baseInstrumentToken)
                {
                    if (_bEMALoaded)
                    {
                        _bEMA.Process(e.ClosePrice, isFinal: true);
                    }
                }
                else if (_EMALoaded.Contains(e.InstrumentToken))
                {
                    if (!lTokenEMA.ContainsKey(e.InstrumentToken))
                    {
                        return;
                    }
                    
                    tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                    lTokenEMA[e.InstrumentToken].Process(tokenRSI[e.InstrumentToken].GetValue<decimal>(0), isFinal: true);

                    //if (ActiveOptions.Any(x => x.InstrumentToken == e.InstrumentToken))
                    //{
                    //    Instrument option = ActiveOptions.Find(x => x.InstrumentToken == e.InstrumentToken);

                    //    decimal rsi = tokenRSI[e.InstrumentToken].GetValue<decimal>(0);
                    //    decimal ema = lTokenEMA[e.InstrumentToken].GetValue<decimal>(0);

                    //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                    //        String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
                    //        , option.TradingSymbol, Decimal.Round(ema, 2), Decimal.Round(rsi, 2)), "CandleManger_TimeCandleFinished");
                    //}
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

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                uint token = e.InstrumentToken;
                DateTime currentTime = e.CloseTime;
                IIndicatorValue rsi = null, ema = null;
                if (token == _baseInstrumentToken)
                {

                    if (_futureSellLevel <= 0 && _futureBuyLevel <= 0)
                    {
                        if (_bEMALoaded)
                        {
                            _bEMAValue = _bEMA.Process(e.ClosePrice, isFinal: false);
                        }
                    }
                    _bCandle = e;
                }
                else
                {
                    if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                    {
                        return;
                    }

                    rsi = tokenRSI[token].Process(e.ClosePrice, isFinal: false);
                    ema = lTokenEMA[token].Process(rsi.GetValue<decimal>(), isFinal: false);

                    if (tokenRSIIndicator.ContainsKey(token))
                    {
                        tokenRSIIndicator[token] = rsi;
                    }
                    else
                    {
                        tokenRSIIndicator.Add(token, rsi);
                    }

                    if (tokenEMAIndicator.ContainsKey(token))
                    {
                        tokenEMAIndicator[token] = ema;
                    }
                    else
                    {
                        tokenEMAIndicator.Add(token, ema);
                    }
                    if (!_aboveUpperBand.ContainsKey(token))
                    {
                        _aboveUpperBand.Add(token, false);
                    }
                    //if (!_belowEMA.ContainsKey(token))
                    //{
                    //    _belowEMA.Add(token, false);
                    //}

                    if (rsi.IsEmpty || ema.IsEmpty || !rsi.IsFormed || !ema.IsFormed)// || _bEMAValue == null || _bEMAValue.IsEmpty)
                    {
                        return;
                    }
                    //else if (rsi.GetValue<decimal>() < ema.GetValue<decimal>())
                    //{
                    //    _belowEMA[token] = true;
                    //}
                    //else if (rsi.GetValue<decimal>() > ema.GetValue<decimal>())
                    //{
                    //    _belowEMA[token] = false;
                    //}

                    if (_activeFuture != null && _activeFuture.InstrumentToken == token)
                    {
                        _fCandle = e;
                        _fLoaded = true;

                        if (!_breakKillerFlag)
                        {
                            //if (e.CloseTime.TimeOfDay >= new TimeSpan(09, 15, 0) && e.CloseTime.TimeOfDay <= new TimeSpan(10, 15, 0))
                            //{
                            //    _currentDayRangeHigh = e.HighPrice > _currentDayRangeHigh ? e.HighPrice : _currentDayRangeHigh;
                            //    _currentDayRangeLow = e.LowPrice < _currentDayRangeLow || _currentDayRangeLow == 0 ? e.LowPrice : _currentDayRangeLow;
                            //}
                            //else 
                            if (e.CloseTime.TimeOfDay > new TimeSpan(10, 15, 0))
                            {
                                if (_currentDayRangeHigh * _currentDayRangeLow == 0)
                                {
                                    //retrive range from database
                                    DataLogic dl = new DataLogic();
                                    _currentDayRangeOHLC = dl.GetPriceRange(token, e.CloseTime.Date + new TimeSpan(09, 15, 0), e.CloseTime.Date + new TimeSpan(10, 15, 0));

                                    _currentDayRangeHigh = _currentDayRangeOHLC.High;
                                    _currentDayRangeLow = _currentDayRangeOHLC.Low;
                                }
                                if (_currentDayRangeHigh * _currentDayRangeLow != 0)
                                {
                                    _breakKillerFlag = e.ClosePrice > _currentDayRangeHigh || e.ClosePrice < _currentDayRangeLow ? true : false;

                                }
                            }
                            if (!_breakKillerFlag && _previousDayOHLCLoaded)
                            {
                                _breakKillerFlag = e.ClosePrice > _previousDayOHLC.High || e.ClosePrice < _previousDayOHLC.Low ? true : false;
                            }

                            if (_breakKillerFlag)
                            {
                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                                   "Breakout Killer Flag Enabled", "CandleManger_TimeCandleFinished");
                            }
                        }

                    }
                }
                if (_bCandle != null && _fLoaded)
                {
                    _fLoaded = false;
                    uint fToken = _activeFuture.InstrumentToken;
                    rsi = tokenRSIIndicator[fToken];
                    ema = tokenEMAIndicator[fToken];

                    decimal lastPrice = _fCandle.ClosePrice;
                    if(_orderTrio != null)
                    {
                        if (_exitDecisiveCandleHighLow > 0)
                        {
                            CandleFormation xcf = GetCandleFormation(_bCandle, _exitDecisiveCandleHighLow);

                            if (xcf == CandleFormation.Bullish)
                            {
                                //Update StopLoss
                                if (_orderTrio.Order.TransactionType.ToLower() == "buy")
                                {
                                    _orderTrio.StopLoss = _fCandle.OpenPrice;
                                }
                            }
                            else if (xcf == CandleFormation.Bearish)
                            {
                                //Update StopLoss
                                if (_orderTrio.Order.TransactionType.ToLower() == "sell")
                                {
                                    _orderTrio.StopLoss = _fCandle.OpenPrice;
                                }
                            }
                        }
                        decimal emaValue = ema.GetValue<decimal>();
                        decimal rsiValue = rsi.GetValue<decimal>();

                        if (TradeExit(token, _fCandle.ClosePrice, currentTime, rsiValue, emaValue, _bEMAValue, candleEnd: true))
                        {
                            _orderTrio = null;
                        }
                    }
                    if (_orderTrio == null && _breakKillerFlag)
                    {
                        if (_entryDecisiveCandleHighLow > 0)
                        {
                            CandleFormation ecf = GetCandleFormation(_bCandle, _entryDecisiveCandleHighLow);

                            if (ecf == CandleFormation.Bullish)
                            {
                                TradeEntry(fToken, lastPrice, _fCandle.OpenPrice, currentTime, rsi, ema, _bEMAValue, TradeZone.Long);
                            }
                            else if (ecf == CandleFormation.Bearish)
                            {
                                TradeEntry(fToken, lastPrice, _fCandle.OpenPrice, currentTime, rsi, ema, _bEMAValue, TradeZone.Short);
                            }
                        }
                        else
                        {
                            TradeEntry(fToken, lastPrice, 0, currentTime, rsi, ema, _bEMAValue, TradeZone.Open);
                        }
                    }


                    //    //if (_orderTrio == null)
                    //    //{
                    //    if (cf == CandleFormation.Bullish)
                    //    {
                    //        ExecuteTrade(currentTime, rsi, ema, _fCandle, true);
                    //    }
                    //    else if (cf == CandleFormation.Bearish)
                    //    {
                    //        ExecuteTrade(currentTime, rsi, ema, _fCandle, false);
                    //    }
                    ////}
                    ////else 
                    //if (_orderTrio != null && TradeExit(token, _fCandle.ClosePrice, currentTime, rsi, ema, _bEMAValue))
                    //{
                    //    _orderTrio = null;
                    //}

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, _fCandle.CloseTime,
                        String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", _fCandle.OpenPrice, _fCandle.HighPrice, _fCandle.LowPrice, _fCandle.ClosePrice
                        , _activeFuture.TradingSymbol, Decimal.Round(ema.GetValue<decimal>(), 2), Decimal.Round(rsi.GetValue<decimal>(), 2)), "CandleManger_TimeCandleFinished");

                    _bCandle = null;
                    _fCandle = null;
                }

                //    if (_activeFuture != null && _activeFuture.InstrumentToken == token)
                //{
                //    TradeEntry(token, e.ClosePrice, currentTime, rsi, ema, _bEMAValue);

                //}
                //}
                //Put a hedge at 3:15 PM
                // TriggerEODPositionClose(tick.LastTradeTime);
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

        //private void ExecuteTrade(DateTime currentTime, IIndicatorValue rsi, IIndicatorValue ema, 
        //    Candle fCandle, bool longTrade)
        //{
        //    //orderTrio.Option = option;
        //    decimal lastPrice = fCandle.ClosePrice;
        //    uint token = fCandle.InstrumentToken;

        //    if (_orderTrio == null)
        //    {
        //        TradeEntry(token, lastPrice, fCandle.OpenPrice, currentTime, rsi, ema, _bEMAValue, longTrade);
        //    }
        //    else
        //    {
        //        //Update StopLoss
        //        if ((longTrade && _orderTrio.Order.TransactionType.ToLower() == "buy") ||(!longTrade && _orderTrio.Order.TransactionType.ToLower() == "sell"))
        //        {
        //            _orderTrio.StopLoss = fCandle.OpenPrice;
        //        }
        //    }
        //    //else
        //    //{
        //    //    //EXIT CHECKING SHOULD BE DONE ON REGULAR BASIS ON EACH CANDLE AND NOT JUST ON BEARINSH OR BULLISH
        //    //    //CANDLE
        //    //    _orderTrio = TradeExit(token, lastPrice, currentTime, rsi, ema, _bEMAValue, longTrade);
        //    //}
        //}

        private OrderTrio TradeEntry(uint token, decimal lastPrice, decimal openPrice, DateTime currentTime, 
            IIndicatorValue rsi, IIndicatorValue ema, IIndicatorValue bema, TradeZone tz)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                Trade t = CheckEntryCriteria(token, currentTime, rsi, ema, bema, tz, out entryRSI);

                if (t == Trade.Buy || t == Trade.Sell)
                {
                    bool longTrade = t == Trade.Buy;
                    decimal stopLoss = 0, targetProfit = 0;
                    GetCriticalLevels(lastPrice, openPrice, longTrade, out stopLoss, out targetProfit);

                    //This method should also check if there is any current position on opposite side, then include that quantity in current trade.
                    int tradeQty = GetTradeQty(lastPrice - stopLoss, _activeFuture.LotSize);
                    //ENTRY ORDER - BUY ALERT

                    //Buy once
                    Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                        token, longTrade, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("TRADE!! {3} {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice, longTrade?"Bought":"Sold"), "TradeEntry");

                    
                    //if (trade == Trade.Buy)
                    //{
                    //    if (_orderTrio == null)
                    //    {
                    //        //Buy once
                    //        order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //            token, true, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //            string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //    }
                    //    else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                    //    {
                    //        //Do nothing
                    //    }
                    //    else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "sell" && (currentTime - _orderTrio.EntryTradeTime).TotalMinutes > 3)
                    //    {
                    //        //buy double
                    //        order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //            token, true, 2 * tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //            string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", 2 * tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //    }
                    //}
                    //else if (trade == Trade.Sell)
                    //{
                    //    if (_orderTrio == null)
                    //    {
                    //        //sell once
                    //        order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //            token, false, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //            string.Format("TRADE!! Sold {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //    }
                    //    else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" && (currentTime - _orderTrio.EntryTradeTime).TotalMinutes > 3)
                    //    {
                    //        //sell double
                    //        order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //            token, false, 2 * tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //            string.Format("TRADE!! Sold {0} lots of {1} @ {2}.", 2 * tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //    }
                    //    else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "sell")
                    //    {
                    //        //do nothing
                    //    }
                    //}




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

                    if (order != null)
                    {
                        orderTrio = new OrderTrio();
                        orderTrio.Option = _activeFuture;
                        orderTrio.Order = order;
                        //orderTrio.SLOrder = slOrder;
                        //orderTrio.TPOrder = tpOrder;
                        orderTrio.StopLoss = stopLoss;
                        orderTrio.TargetProfit = targetProfit;
                        orderTrio.EntryRSI = entryRSI;
                        orderTrio.EntryTradeTime = currentTime;
                        _orderTrio = orderTrio;
                        OnTradeEntry(order);
                    }
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
        private bool TradeExit(uint token, decimal lastPrice, DateTime currentTime,
            decimal rsiValue, decimal emaValue, IIndicatorValue bema, bool candleEnd)
        {
            bool exit = false;
            try
            {
                
                decimal entryRSI = 0;
                bool tpHit;
                bool longTrade = _orderTrio.Order.TransactionType.ToLower() == "buy";
                //Trade trade = CheckExitCriteria(token, currentTime, rsi, ema, bema, longTrade, out entryRSI);
                if(CheckExitCriteria(token, lastPrice, currentTime, rsiValue, emaValue, longTrade, candleEnd, out tpHit))
                {
                    //This method should also check if there is any current position on opposite side, then include that quantity in current trade.
                    int tradeQty = GetTradeQty(0, _activeFuture.LotSize);
                    //ENTRY ORDER - BUY ALERT

                    //Buy once
                    Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                        token, !longTrade, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    OnTradeEntry(order);
                    exit = true;
                    //if (trade != Trade.DoNotTrade)
                    //{
                    //    decimal stopLoss = 0, targetProfit = 0;
                    //    GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);

                    //    int tradeQty = GetTradeQty(lastPrice - stopLoss, _activeFuture.LotSize);
                    //    //ENTRY ORDER - BUY ALERT

                    //    Order order = null;
                    //    if (trade == Trade.Buy)
                    //    {
                    //        if (_orderTrio == null)
                    //        {
                    //            //Buy once
                    //            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //                token, true, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //                string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //        }
                    //        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                    //        {
                    //            //Do nothing
                    //        }
                    //        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "sell" && (currentTime - _orderTrio.EntryTradeTime).TotalMinutes > 3)
                    //        {
                    //            //buy double
                    //            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //                token, true, 2 * tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //                string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", 2 * tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //        }
                    //    }
                    //    else if (trade == Trade.Sell)
                    //    {
                    //        if (_orderTrio == null)
                    //        {
                    //            //sell once
                    //            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //                token, false, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //                string.Format("TRADE!! Sold {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //        }
                    //        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" && (currentTime - _orderTrio.EntryTradeTime).TotalMinutes > 3)
                    //        {
                    //            //sell double
                    //            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                    //                token, false, 2 * tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                    //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //                string.Format("TRADE!! Sold {0} lots of {1} @ {2}.", 2 * tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //        }
                    //        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "sell")
                    //        {
                    //            //do nothing
                    //        }
                    //    }




                    //    ////SL for first orders
                    //    //Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, stopLoss,
                    //    //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                    //    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
                    //    //    string.Format("Placed Stop Loss for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, slOrder.AveragePrice), "TradeEntry");

                    //    ////target profit
                    //    //Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, targetProfit,
                    //    //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                    //    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
                    //    //    string.Format("Placed Target Profit for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, tpOrder.AveragePrice), "TradeEntry");

                    //    if (order != null)
                    //    {
                    //        orderTrio = new OrderTrio();
                    //        orderTrio.Option = _activeFuture;
                    //        orderTrio.Order = order;
                    //        //orderTrio.SLOrder = slOrder;
                    //        //orderTrio.TPOrder = tpOrder;
                    //        orderTrio.StopLoss = stopLoss;
                    //        orderTrio.TargetProfit = targetProfit;
                    //        orderTrio.EntryRSI = entryRSI;
                    //        orderTrio.EntryTradeTime = currentTime;
                    //        _orderTrio = orderTrio;
                    //        OnTradeEntry(order);
                    //    }
                    //    //OnTradeEntry(slOrder);
                    //    //OnTradeEntry(tpOrder);
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
            return exit;
        }
        private CandleFormation GetCandleFormation(Candle candle, decimal decisiveCandleHighLow)
        {
            CandleFormation cf = CandleFormation.Indecisive;

            decimal candleBody = candle.ClosePrice - candle.OpenPrice;

            decimal candleLowerWick = candleBody >= 0 ? candle.OpenPrice - candle.LowPrice : candle.ClosePrice - candle.LowPrice;
            decimal candleUpperWick = candleBody >= 0 ? candle.HighPrice - candle.ClosePrice : candle.HighPrice - candle.OpenPrice;
            decimal candleSize = candle.HighPrice - candle.LowPrice;

            if (candleSize >= decisiveCandleHighLow)
            {
                if (candleBody > 0
                    && ((candleBody >= CANDLE_DECISIVE_BODY_FRACTION * candleSize) ||
                    (candleLowerWick >= CANDLE_BULLISH_LOWERWICK_FRACTION * candleSize && candleUpperWick <= CANDLE_BULLISH_UPPERWICK_FRACTION * candleSize)
                    )
                    //&& candleBody > CANDLE_BULLISH_BODY_PRICE_FRACTION * candle.ClosePrice
                    )
                {
                    cf = CandleFormation.Bullish;
                }
                else if ((candleBody < 0 && (Math.Abs(candleBody) >= CANDLE_DECISIVE_BODY_FRACTION * candleSize) ||
                    (candleUpperWick >= CANDLE_BEARISH_UPPERWICK_FRACTION * candleSize && candleLowerWick <= CANDLE_BEARISH_LOWERWICK_FRACTION * candleSize)
                    )
                    )
                {
                    cf = CandleFormation.Bearish;
                }
            }

            return cf;
        }
        private int GetTradeQty(decimal maxlossInPoints, uint lotSize)
        {
            return _tradeQty;
        }
        private void GetOrder2PriceQty(int firstLegQty, decimal firstMaxLoss, Candle previousCandle, uint lotSize, out int qty, out decimal price)
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
        private Trade CheckEntryCriteria(uint token, DateTime currentTime, IIndicatorValue rsi, IIndicatorValue ema,
                    IIndicatorValue bema, TradeZone tz, out decimal entryRSI)
        {
            //bool trade = false;
            Trade t = Trade.DoNotTrade;
            entryRSI = 0;
            try
            {
                if (!tokenRSI.ContainsKey(token) || !rsi.IsFormed || !ema.IsFormed)
                {
                    //trade = false;
                    t = Trade.DoNotTrade;
                }
                else
                {
                    decimal emaValue = ema.GetValue<decimal>();
                    decimal rsiValue = rsi.GetValue<decimal>();
                    decimal bemaValue = _baseEMAValue;// bema.GetValue<decimal>();

                    entryRSI = rsiValue;

                    //if ((tz == TradeZone.Short && rsiValue < emaValue) || (tz == TradeZone.Long && rsiValue > emaValue))
                    //{
                    //    trade = true;
                    //}

                    //if (!_belowEMA.ContainsKey(token))
                    //{
                    //    _belowEMA.Add(token, false);
                    //}

                    if (((tz == TradeZone.Short) || (tz == TradeZone.Open && (_entryAtCross ? (!_belowEMA.ContainsKey(token) || !_belowEMA[token]) : true))) 
                        && rsiValue < emaValue
                        )
                    {
                        t = Trade.Sell;
                        _belowEMA[token] = true;
                    }

                    else if (((tz == TradeZone.Long) || (tz == TradeZone.Open && (_entryAtCross ? (!_belowEMA.ContainsKey(token) || _belowEMA[token]) : true))) 
                        && rsiValue > emaValue)
                    {
                        t = Trade.Buy;
                        _belowEMA[token] = false;
                    }
                }

                /// Entry Criteria:
                /// RSI Cross EMA from below, below 40 and both crosses above 40
                /// RSI Cross EMA from below between 40 and 50

                //if (rsi.IsFormed && ema.IsFormed
                //    && rsiValue > emaValue + _rsiBandForEntry && rsiValue >= _rsiLowerLimit && emaValue >= _rsiLowerLimit
                //    //&& rsiValue <= RSI_MID_POINT && emaValue <= RSI_MID_POINT
                //    && ((isOptionCall && _baseInstrumentPrice >= bemaValue)
                //    || (!isOptionCall && _baseInstrumentPrice <= bemaValue))
                //    )
                //{
                //    return true;
                //}

                //if (rsi.IsFormed && ema.IsFormed && rsiValue < emaValue)
                //{
                //    _belowEMA[token] = true;
                //}
                ////if (rsi.IsFormed && ema.IsFormed && rsiValue > emaValue && rsiValue < emaValue + _rsiBandForEntry)
                ////{
                ////    _belowEMA = true;
                ////}
                //if (rsi.IsFormed && ema.IsFormed
                //  //  && rsiValue > emaValue  && (_orderTrio == null || (currentTime - orderTrio.EntryTradeTime).TotalMinutes > _timeBandForExit && rsiValue < emaValue - _rsiBandForEntry)))

                //  //  && (_entryAtCross?_belowEMA[token] : true) // && _betweenEMAandUpperBand)
                //  //  && ((_baseInstrumentPrice >= (_futureBuyLevel <= 0 ? bemaValue : _futureBuyLevel))
                //  //  || (_baseInstrumentPrice <= (_futureSellLevel <= 0 ? bemaValue : _futureSellLevel)))
                //  //  )

                //  //  if (tokenRSI[token].IsFormed && lTokenEMA[token].IsFormed
                //  //&& rsiValue <= _rsiUpperLimit && emaValue <= _rsiUpperLimit
                //  //&& (
                //  //((currentTime - orderTrio.EntryTradeTime).TotalMinutes <= _timeBandForExit && rsiValue < emaValue - _rsiBandForExit) ||
                //  //((currentTime - orderTrio.EntryTradeTime).TotalMinutes > _timeBandForExit && rsiValue < emaValue - _rsiBandForEntry))
                //  )
                //    {
                //    _belowEMA[token] = false;
                //    //_betweenEMAandUpperBand = false;
                //    return true;
                //}
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
            return t;
        }
        private bool CheckExitCriteria(uint token, decimal lastPrice, DateTime currentTime, 
            decimal rsiValue, decimal emaValue, bool longTrade, bool candleEnd, out bool tpHit)
        {
            /// Exit Criteria:
            /// RSI cross EMA from above and both crosses below 60, and both comes below 60
            /// RSI cross EMA from above between 50 and 60.
            /// Target profit hit
            tpHit = false;
            bool exit = false;
            try
            {
                //decimal emaValue = ema.GetValue<decimal>();
                //decimal rsiValue = rsi.GetValue<decimal>();

                if ((!longTrade && (lastPrice < _orderTrio.TargetProfit)) || (longTrade && (lastPrice >= _orderTrio.TargetProfit)))
                {
                    tpHit = true;
                    exit = true;
                }
                else
                {
                    decimal bemaValue = _baseEMAValue;// bema.GetValue<decimal>();

                    if((!longTrade && (((lastPrice >= _orderTrio.StopLoss) && (_orderTrio.StopLoss != 0)) 
                        || ((rsiValue > emaValue && (candleEnd))))) 
                        || (longTrade && (((lastPrice <= _orderTrio.StopLoss) && (_orderTrio.StopLoss != 0)) 
                        || ((rsiValue < emaValue && (candleEnd))))))
                    {
                        tpHit = false;
                        exit = true;
                    }
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
            return exit;
        }

        private void LoadFuturesToTrade(DateTime currentTime)
        {
            try
            {
                DataLogic dl = new DataLogic();

                if (_activeFuture == null)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Future from database...", "LoadFuturesToTrade");
                    //Load options asynchronously
                   _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Future Loaded", "LoadFuturesToTrade");
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
        private void UpsertOptionUniverse(SortedList<decimal, Instrument>[] closeOptions)
        {
            OptionUniverse ??= new SortedList<decimal, Instrument>[2];
            for (int it = 0; it < 2; it++)
            {
                for (int i = 0; i < closeOptions[it].Count; i++)
                {
                    decimal strike = closeOptions[it].ElementAt(i).Key;
                    if (!(OptionUniverse[it]??= new SortedList<decimal, Instrument>()).ContainsKey(strike))
                    {
                        OptionUniverse[it].Add(strike, closeOptions[it].ElementAt(i).Value);
                    }
                }
            }
        }
        
        //private bool TradeExit(uint token, DateTime currentTime, decimal lastPrice, OrderTrio orderTrio, IIndicatorValue rsi, IIndicatorValue ema)
        //{
        //    try
        //    {
        //        if (orderTrio.Option != null && orderTrio.Option.InstrumentToken == token)
        //        {
        //            Instrument option = orderTrio.Option;
        //            bool tpHit ;

        //            if(CheckExitCriteria(token, lastPrice, currentTime, rsi, ema, out tpHit))
        //            {
        //                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
        //                       token, false, _tradeQty * Convert.ToInt32(option.LotSize),
        //                       algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

        //                if (tpHit)
        //                {
        //                    //orderTrio.TPOrder = UpdateOrder(orderTrio.TPOrder, lastPrice, currentTime);
        //                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
        //                    string.Format("Target profit Triggered. Exited the trade @ {0}", order.AveragePrice), "TradeExit");
        //                }
        //                else
        //                {
        //                    //orderTrio.TPOrder = ModifyOrder(orderTrio.TPOrder, lastPrice, currentTime);
        //                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
        //                    string.Format("Stop Loss Triggered. Exited the trade @ {0}", order.AveragePrice), "TradeExit");
        //                }

        //                //orderTrio.SLOrder = CancelSLOrder(orderTrio.SLOrder, currentTime);
        //                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Cancelled Stop Loss order", "TradeExit");

        //                //OnTradeExit(orderTrio.SLOrder);
        //                //OnTradeExit(orderTrio.TPOrder);
        //                OnTradeExit(order);
        //                //orderTrio.TPOrder = null;
        //                //orderTrio.SLOrder = null;
        //                orderTrio.Option = null;

        //                ActiveOptions.Remove(option);

        //                return true;
        //            }
        //        }
        //    }
        //    catch (Exception exp)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(exp.Message + exp.StackTrace);
        //        Logger.LogWrite("Trading Stopped");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
        //        Thread.Sleep(100);
        //    }
        //    return false;
        //}

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
        
        private void GetCriticalLevels(decimal lastPrice, decimal openPrice, bool longTrade, out decimal stopLoss, out decimal targetProfit, decimal profitMade = 0)
        {

            if(longTrade)
            {
                stopLoss = Math.Round(openPrice * 20) / 20;
                targetProfit = Math.Round((lastPrice + Math.Abs(_targetProfit - profitMade)) * 20) / 20;
            }
            else
            {
                stopLoss = Math.Round(openPrice * 20) / 20;
                targetProfit = Math.Round((lastPrice - Math.Abs(_targetProfit - profitMade)) * 20) / 20;
            }
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, TimeSpan candleTimeSpan, out DateTime lastEndTime)
        {
            try
            {
                DateTime? candleStartTime = null;

                if (currentTime.TimeOfDay < MARKET_START_TIME)
                {
                    candleStartTime = currentTime.Date + MARKET_START_TIME;
                    lastEndTime = candleStartTime.Value;
                }
                else
                {

                    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % candleTimeSpan.TotalMilliseconds;

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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (_activeFuture != null && !SubscriptionTokens.Contains(_activeFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeFuture.InstrumentToken);
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Futures", "UpdateInstrumentSubscription");
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
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
        private void LoadBaseInstrumentEMA(uint  bToken, int candlesCount, DateTime lastCandleEndTime)
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

                    ExponentialMovingAverage lema; //.Process(candle.ClosePrice, isFinal: true)
                    RelativeStrengthIndex rsi;

                    foreach (uint t in historicalCandlePrices.Keys)
                    {
                        rsi = new RelativeStrengthIndex();
                        foreach (var price in historicalCandlePrices[t])
                        {
                            rsi.Process(price, isFinal: true);
                        }
                        tokenRSI.Add(t, rsi);

                        lema = new ExponentialMovingAverage(_emaLength);
                        for (int i = _emaLength*2 - 1; i >= 0; i--)
                        {
                            lema.Process(tokenRSI[t].GetValue<decimal>(i), isFinal: true);
                        }
                        
                        lTokenEMA.Add(t, lema);

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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "1", "CheckHealth");
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
            }
        }

        public void StopTrade(bool stop)
        {
            _stopTrade = stop;

            if (!_stopTrade)
            {
                LoadHistoricalData();
            }
        }
        private void LoadHistoricalData()
        {
            if (_futureBuyLevel <= 0 && _futureSellLevel <= 0 && !_bEMALoaded)
            {
                LoadBInstrumentEMA(_baseInstrumentToken, BASE_EMA_LENGTH, DateTime.Now);
            }
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
