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
    public class OptionBuyWithRSICandle : IZMQ
    {
        private readonly int _algoInstance;
        //public List<Instrument> ActiveOptions { get; set; }
        public Instrument _activeCall;
        public Instrument _activePut;
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionBuyWithRSICandle source);
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

        public List<Order> _pastOrders;
        private bool _stopTrade;
        
        private const int BASE_EMA_LENGTH = 200;
        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;
        //Dictionary<uint, AverageDirectionalIndex> tokenADX;

        Dictionary<uint, IIndicatorValue> tokenEMAIndicator;
        Dictionary<uint, IIndicatorValue> tokenRSIIndicator;
        //Dictionary<uint, IIndicatorValue> tokenADXIndicator;

        private decimal _baseEMAValue = 27950;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        TimeSpan _rsiTimeSpan;
        public decimal _strikePriceRange;
        List<uint> _EMALoaded;
        List<uint> _SQLLoading;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;

        Candle _bCandle;
        Candle _ceCandle;
        Candle _peCandle;
        bool _ceLoaded = false;
        bool _peLoaded = false;
        
        private bool _breakKillerFlag = false;
        private bool _orbChecked = false;
        private OHLC _previousDayOHLC, _currentDayRangeOHLC;
        private bool _previousDayOHLCLoading = false, _previousDayOHLCLoaded = false;
        private bool _currentDayRangeLoaded = false;
        private decimal _currentDayRangeLow;
        private decimal _currentDayRangeHigh;


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
        //private readonly decimal _adxLowerLimit;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public const decimal CANDLE_BULLISH_LOWERWICK_FRACTION = 0.5m;
        private const decimal CANDLE_BULLISH_UPPERWICK_FRACTION = 0.2m;
        public const decimal CANDLE_BEARISH_UPPERWICK_FRACTION = 0.5m;
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
        private decimal _maxProfit;

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
        CandleManger _tradeCandleManger, _rsiCandleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        Dictionary<uint, List<Candle>> RSICandles;

        public readonly decimal _rsiBandForEntry;
        public readonly decimal _rsiBandForExit;
        public readonly double _timeBandForExit;
        private readonly decimal _lowerLimitForCEBuy;
        private readonly decimal _upperLimitForPEBuy;
        private decimal _exitDecisiveCandleHighLow;
        private decimal _entryDecisiveCandleHighLow;
        private bool crossed = false;
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public OptionBuyWithRSICandle(DateTime endTime, TimeSpan candleTimeSpan, uint baseInstrumentToken, 
            DateTime? expiry, int quantity, decimal minDistanceFromBInstrument, decimal maxDistanceFromBInstrument,
            decimal rsiLowerLimitForEntry, decimal rsiUpperLimitForExit, decimal ceLevel, decimal peLevel, decimal targetProfit, decimal stopLoss,
            int emaLength, decimal entryDecisiveCandleHighLow, decimal exitDecisiveCandleHighLow, int algoInstance = 0, bool entryatCross = false, 
            bool positionSizing = false, decimal maxLossPerTrade = 0)
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
            _stopLoss = stopLoss;
            _rsiBandForExit = 3;
            _timeBandForExit = 5;
            _rsiBandForEntry = 0.0m;
            _stopTrade = true;
            _lowerLimitForCEBuy = ceLevel;
            _upperLimitForPEBuy = peLevel;
            _entryAtCross = entryatCross;
            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            _pastOrders = new List<Order>();
            _entryDecisiveCandleHighLow = entryDecisiveCandleHighLow;
            _exitDecisiveCandleHighLow = exitDecisiveCandleHighLow;

            SubscriptionTokens = new List<uint>();

            //ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            RSICandles = new Dictionary<uint, List<Candle>>();

            tokenRSIIndicator = new Dictionary<uint, IIndicatorValue>();
            tokenEMAIndicator = new Dictionary<uint, IIndicatorValue>();
            //tokenADXIndicator = new Dictionary<uint, IIndicatorValue>();

            //EMAs
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();
            //tokenADX = new Dictionary<uint, AverageDirectionalIndex>();
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

            _rsiCandleManger = new CandleManger(RSICandles, CandleType.Time);
            _rsiCandleManger.TimeCandleFinished += RSICandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, Arg5: _minDistanceFromBInstrument, 
                Arg4: _maxDistanceFromBInstrument, lowerLimit: _rsiLowerLimit, 
                Arg1:_emaLength, Arg2:_targetProfit, upperLimit:_rsiUpperLimit, 
                Arg3: _lowerLimitForCEBuy, Arg6: _upperLimitForPEBuy, Arg7: _entryDecisiveCandleHighLow,
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
                DataLogic dl = new DataLogic();
                Instrument option = dl.GetInstrument(activeOrder.Tradingsymbol);

                if(option.InstrumentType.Trim(' ').ToLower() =="ce")
                {
                    _activeCall = option;
                }
                else
                {
                    _activePut = option;
                }

                _orderTrio = new OrderTrio();
                _orderTrio.Order = activeOrder;
                _orderTrio.Option = option;
                decimal stopLoss, targetProfit;
                GetCriticalLevels(_orderTrio.Order.AveragePrice, out stopLoss, out targetProfit);
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

        //        _callOrderTrio.TargetProfit = _callOrderTrio.Order.AveragePrice + _targetProfit;

        //        decimal stopLoss, targetProfit;
        //        GetCriticalLevels(_callOrderTrio.Order.AveragePrice, out stopLoss, out targetProfit);
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

        //        decimal stopLoss, targetProfit;
        //        GetCriticalLevels(_putOrderTrio.Order.AveragePrice, out stopLoss, out targetProfit);
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

                    LoadOptionsToTrade(currentTime);
                    UpdateOptionPrices(token, tick.LastPrice);
                    UpdateInstrumentSubscription(currentTime);

                    if (token == _baseInstrumentToken)
                    {
                        if (!_previousDayOHLCLoading)
                        {
                            _previousDayOHLCLoading = true;
                            _previousDayOHLC = PreviousDayRange(token, currentTime);
                            _previousDayOHLCLoaded = true;
                        }
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
                        //}


                        MonitorRSICandles(tick, currentTime);
                        MonitorTradeCandles(tick, currentTime);


                        if (_lowerLimitForCEBuy <= 0 && _upperLimitForPEBuy <= 0)
                        {
                            if (!_bEMALoaded)
                            {
                                LoadBInstrumentEMA(token, BASE_EMA_LENGTH, currentTime);
                            }
                        }
                        //}
                        //else if (tick.LastTradeTime.HasValue)
                        //{
                        if (!_EMALoaded.Contains(token))
                        {
                            LoadHistoricalEMAs(token, currentTime);
                        }
                        if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                        {
                            return;
                        }
                    }
                }
                    ///Stop loss on option price. 32% retracement
                    if (_orderTrio != null && _orderTrio.Option.InstrumentToken == token)
                    {
                        _orderTrio.Option.LastPrice = tick.LastPrice;
                        decimal currentProfit = tick.LastPrice - _orderTrio.Order.AveragePrice;

                        _maxProfit = Math.Max(currentProfit, _maxProfit);

                        if(_maxProfit > 30 && currentProfit <= _maxProfit * 0.815m)
                        {
                            //decimal rsi = tokenRSI[token].GetValue<decimal>(0);
                            //decimal ema = lTokenEMA[token].GetValue<decimal>(0);

                            //bool isOptionCall = _orderTrio.Option.InstrumentType.Trim(' ').ToLower() == "";

                           _orderTrio.StopLoss = tick.LastPrice;
                        //Stop loss and target profit live
                        if (TradeExit(currentTime, _orderTrio, rsiValue: 0, emaValue: 0, isOptionCall: true, 0))
                        {
                            _orderTrio = null;
                            _maxProfit = 0;
                        }
                        }
                    }



                    ////Stop loss and profit only for decisive candles. Other SL on RSI is on candle end.
                    //if (_orderTrio != null && tokenRSI.ContainsKey(token) && lTokenEMA.ContainsKey(token))
                    //{
                    //    IIndicatorValue rsi = tokenRSI[token].Process(tick.LastPrice, isFinal: false);
                    //    IIndicatorValue ema = lTokenEMA[token].Process(rsi.GetValue<decimal>(), isFinal: false);

                    //    //Stop loss and target profit live
                    //    if (TradeExit(token, tick.LastPrice, currentTime, rsi, ema, _bEMAValue, false))
                    //    {
                    //        _orderTrio = null;
                    //    }
                    //}

                    //code emergency exit
                    //    bool orderExited = TradeExit(token, currentTime, e.ClosePrice, orderTrio, rsi, ema);

                    //    if (orderExited)
                    //    {
                    //        orderTrio = null;
                    //    }
                    //    else
                    //    {
                    //        orderTrio = TrailMarket(token, isOptionCall, currentTime, e.ClosePrice, _bEMAValue, orderTrio);
                    //    }
                    //}

                    //if (isOptionCall)
                    //{
                    //    _callOrderTrio = orderTrio;
                    //}
                    //else
                    //{
                    //    _putOrderTrio = orderTrio;
                    //}
                //}
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

        private OHLC PreviousDayRange(uint token, DateTime currentDateTime)
        {
            DataLogic dl = new DataLogic();
            return dl.GetPreviousDayRange(token, currentDateTime);
        }
        private void MonitorRSICandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (RSICandles.ContainsKey(token))
                {
                    _rsiCandleManger.StreamingShortTimeFrameCandle(tick, token, _rsiTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
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
                        _rsiCandleManger.StreamingShortTimeFrameCandle(tick, token, _rsiTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

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

                    if (RSICandles.ContainsKey(bToken) && _bEMALoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _bEMA.Process(RSICandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (RSICandles[bToken].Count > 1)
                        {
                            foreach (var price in RSICandles[bToken])
                            {
                                _bEMA.Process(RSICandles[bToken].First().ClosePrice, isFinal: true);
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
        private void LoadHistoricalEMAs(uint token, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, _rsiTimeSpan, out lastCandleEndTime);

            try
            {
                //var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_EMALoaded.Contains(x));
                StringBuilder sb = new StringBuilder();

                lock (lTokenEMA)
                {
                    //foreach (uint t in tokens)
                    //{
                        if (!_firstCandleOpenPriceNeeded.ContainsKey(token))
                        {
                            _firstCandleOpenPriceNeeded.Add(token, candleStartTime != lastCandleEndTime);
                        }

                        if (!lTokenEMA.ContainsKey(token) && !_SQLLoading.Contains(token))
                        {
                            sb.AppendFormat("{0},", token);
                            _SQLLoading.Add(token);
                        }
                    //}
                }
                string tokenList = sb.ToString().TrimEnd(',');
                int firstCandleFormed = 0;

                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, _emaLength * 2 + RSI_LENGTH, lastCandleEndTime));
                }
               // foreach (uint tkn in tokens)
               // {
                    if (RSICandles.ContainsKey(token) && lTokenEMA.ContainsKey(token))
                    {
                        if (_firstCandleOpenPriceNeeded[token])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            tokenRSI[token].Process(RSICandles[token].First().OpenPrice, isFinal: true);

                            lTokenEMA[token].Process(tokenRSI[token].GetValue<decimal>(0), isFinal: true);
                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (RSICandles[token].Count > 1)
                        {
                            foreach (var price in RSICandles[token])
                            {
                                tokenRSI[token].Process(RSICandles[token].First().ClosePrice, isFinal: true);
                                lTokenEMA[token].Process(tokenRSI[token].GetValue<decimal>(0), isFinal: true);
                            }
                        }
                    }
                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[token]) && lTokenEMA.ContainsKey(token))
                    {
                        _EMALoaded.Add(token);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("EMA & RSI loaded from DB for {0}", token), "MonitorCandles");
                    }
                //}
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
                uint token = e.InstrumentToken;
                DateTime currentTime = e.CloseTime;
                if (token == _baseInstrumentToken)
                {
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
                            if(_currentDayRangeHigh * _currentDayRangeLow == 0)
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

                    _bCandle = e;

                    if (_lowerLimitForCEBuy <= 0 && _upperLimitForPEBuy <= 0)
                    {
                        if (_bEMALoaded)
                        {
                            _bEMAValue = _bEMA.Process(e.ClosePrice, isFinal: false);
                        }
                    }
                    if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                    {
                        return;
                    }

                    IIndicatorValue rsi = tokenRSI[token].Process(e.ClosePrice, isFinal: false);
                    IIndicatorValue ema = lTokenEMA[token].Process(rsi.GetValue<decimal>(), isFinal: false);

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


                    if (rsi.IsEmpty || ema.IsEmpty || !rsi.IsFormed || !ema.IsFormed)// || _bEMAValue == null || _bEMAValue.IsEmpty)
                    {
                        return;
                    }
                    decimal rsiValue = rsi.GetValue<decimal>();
                    decimal emaValue = ema.GetValue<decimal>();

                    if (!_belowEMA.ContainsKey(token))
                    {
                        _belowEMA.Add(token, rsiValue < emaValue);
                    }
                    else if ((!_belowEMA[token] && rsiValue < emaValue) || ((_belowEMA[token] && rsiValue > emaValue)))
                    {
                        _belowEMA[token] =  rsiValue < emaValue;
                        crossed = true;
                    }

                    if (_orderTrio != null)
                    {
                        bool isOptionCall = _orderTrio.Option.InstrumentType.Trim(' ').ToLower() == "ce";
                        if (_exitDecisiveCandleHighLow > 0)
                        {
                            CandleFormation xcf = GetCandleFormation(_bCandle, _exitDecisiveCandleHighLow);

                            if ((xcf == CandleFormation.Bullish && isOptionCall)
                                || (xcf == CandleFormation.Bearish && !isOptionCall))
                            {
                                _orderTrio.BaseInstrumentStopLoss = _bCandle.OpenPrice;
                            }
                        }

                        bool orderExited = TradeExit(currentTime, _orderTrio, rsiValue, emaValue, isOptionCall, _bCandle.ClosePrice);

                        if (orderExited)
                        {
                            _orderTrio = null;
                        }
                        else
                        {
                            _orderTrio = TrailMarket(currentTime, _bEMAValue, _orderTrio);
                        }
                    }

                    else
                    //if (_orderTrio == null)
                    {
                        CandleFormation ecf = CandleFormation.Indecisive;
                        if (_entryDecisiveCandleHighLow > 0)
                        {
                            ecf = GetCandleFormation(_bCandle, _entryDecisiveCandleHighLow);
                        }

                        if (_breakKillerFlag)
                        {
                            //if (e.CloseTime.TimeOfDay < new TimeSpan(12, 30, 0) || (_currentDayRangeHigh < e.ClosePrice && e.ClosePrice < _currentDayRangeLow))
                            //{
                                if ((_entryDecisiveCandleHighLow > 0 ? ecf == CandleFormation.Bullish : true)
                               && (rsiValue > emaValue)
                               && (rsiValue >= _rsiLowerLimit && emaValue >= _rsiLowerLimit)
                               && ((_entryAtCross ? (crossed && !_belowEMA[token]) : true) || (_entryDecisiveCandleHighLow > 0))
                               && (_baseInstrumentPrice >= (_lowerLimitForCEBuy <= 0 ? _bEMAValue.GetValue<decimal>() : _lowerLimitForCEBuy)))
                                {
                                    _activeCall = OptionUniverse[(int)InstrumentType.CE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument).Value;
                                    _orderTrio = TradeEntry(currentTime, _activeCall, _bCandle.OpenPrice);
                                    crossed = false;
                                }
                                else if ((_entryDecisiveCandleHighLow > 0 ? ecf == CandleFormation.Bearish : true)
                                    && (rsiValue < emaValue)
                                    && (rsiValue <= _rsiUpperLimit && emaValue <= _rsiUpperLimit)
                                    && ((_entryAtCross ? crossed && _belowEMA[token] : true) || (_entryDecisiveCandleHighLow > 0))
                                    && (_baseInstrumentPrice <= (_upperLimitForPEBuy <= 0 ? _bEMAValue.GetValue<decimal>() : _upperLimitForPEBuy)))
                                {
                                    _activePut = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument).Value;

                                    _orderTrio = TradeEntry(currentTime, _activePut, _bCandle.OpenPrice);
                                    crossed = false;
                                    //TradeEntry(fToken, lastPrice, _fCandle.OpenPrice, currentTime, rsi, ema, _bEMAValue, TradeZone.Short);
                                }
                            //}
                        }
                    }

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                           String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", _bCandle.OpenPrice, _bCandle.HighPrice, _bCandle.LowPrice, _bCandle.ClosePrice
                           , "Bank Nifty", Decimal.Round(ema.GetValue<decimal>(), 2), Decimal.Round(rsi.GetValue<decimal>(), 2)), "CandleManger_TimeCandleFinished");

                        _bCandle = null;
                        //_fCandle = null;
                    //}
                    //if (_bCandle != null)// && _ceLoaded && _peLoaded)
                    //{
                    //    //_ceLoaded = false;
                    //    //_peLoaded = false;

                    //    if (GetCandleFormation(_bCandle) == CandleFormation.Bullish)
                    //    {
                    //        Trade(true, currentTime, rsi, ema);
                    //    }
                    //    else if (GetCandleFormation(_bCandle) == CandleFormation.Bearish)
                    //    {
                    //        Trade(false, currentTime, rsi, ema);
                    //    }

                    //    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, _ceCandle.CloseTime, 
                    //    //    String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", _ceCandle.OpenPrice, _ceCandle.HighPrice, _ceCandle.LowPrice, _ceCandle.ClosePrice
                    //    //    , ActiveOptions.Find(x => x.InstrumentToken == _ceCandle.InstrumentToken).TradingSymbol, Decimal.Round(ema.GetValue<decimal>(), 2), Decimal.Round(rsi.GetValue<decimal>(), 2)), "CandleManger_TimeCandleFinished");

                    //    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, _peCandle.CloseTime, 
                    //    //    String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", _peCandle.OpenPrice, _peCandle.HighPrice, _peCandle.LowPrice, _peCandle.ClosePrice
                    //    //    , ActiveOptions.Find(x => x.InstrumentToken == _peCandle.InstrumentToken).TradingSymbol, Decimal.Round(ema.GetValue<decimal>(), 2), Decimal.Round(rsi.GetValue<decimal>(), 2)), "CandleManger_TimeCandleFinished");

                    //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                    //       String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", _bCandle.OpenPrice, _bCandle.HighPrice, _bCandle.LowPrice, _bCandle.ClosePrice
                    //       , "Bank Nifty", Decimal.Round(ema.GetValue<decimal>(), 2), Decimal.Round(rsi.GetValue<decimal>(), 2)), "CandleManger_TimeCandleFinished");

                    //    _bCandle = null;
                    //    _ceCandle = null;
                    //    _peCandle = null;
                    //    //}
                    //}
                }
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
        private void RSICandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (e.InstrumentToken == _baseInstrumentToken)
                {
                    if (_bEMALoaded)
                    {
                        _bEMA.Process(e.ClosePrice, isFinal: true);
                    }

                    if (_EMALoaded.Contains(e.InstrumentToken))
                    {
                        if (lTokenEMA.ContainsKey(e.InstrumentToken))
                        {
                            tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                            lTokenEMA[e.InstrumentToken].Process(tokenRSI[e.InstrumentToken].GetValue<decimal>(0), isFinal: true);
                        }

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

        //private void Trade(bool isOptionCall, DateTime currentTime, IIndicatorValue rsi, IIndicatorValue ema)
        //{
        //    OrderTrio orderTrio = isOptionCall ? _callOrderTrio : _putOrderTrio;
        //    //orderTrio.Option = option;
        //    if (orderTrio == null)
        //    {
        //        orderTrio = TradeEntry(currentTime, rsi, ema, _bEMAValue, isOptionCall);
        //    }
        //    else
        //    {
        //        bool orderExited = TradeExit(currentTime, orderTrio, rsi, ema);

        //        if (orderExited)
        //        {
        //            orderTrio = null;
        //        }
        //        else
        //        {
        //            orderTrio = TrailMarket(isOptionCall, currentTime, _bEMAValue, orderTrio);
        //        }
        //    }

        //    if (isOptionCall)
        //    {
        //        _callOrderTrio = orderTrio;
        //    }
        //    else
        //    {
        //        _putOrderTrio = orderTrio;
        //    }
        //}

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
                    && (candleBody >= CANDLE_DECISIVE_BODY_FRACTION * candleSize ||
                    (candleLowerWick >= CANDLE_BULLISH_LOWERWICK_FRACTION * candleSize && candleUpperWick <= CANDLE_BULLISH_UPPERWICK_FRACTION * candleSize)
                    )
                    //&& candleBody > CANDLE_BULLISH_BODY_PRICE_FRACTION * candle.ClosePrice
                    )
                {
                    cf = CandleFormation.Bullish;
                }
                else if (candleBody < 0 && (Math.Abs(candleBody) >= CANDLE_DECISIVE_BODY_FRACTION * candleSize ||
                    (candleUpperWick >= CANDLE_BEARISH_UPPERWICK_FRACTION * candleSize && candleLowerWick <= CANDLE_BEARISH_LOWERWICK_FRACTION * candleSize)
                    )
                    )
                {
                    cf = CandleFormation.Bearish;
                }
            }

            return cf;
        }
        private OrderTrio TrailMarket(DateTime currentTime, IIndicatorValue bema,  OrderTrio orderTrio)
        {
            Instrument option = orderTrio.Option;
            decimal lastPrice = option.LastPrice;
            bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
            OrderTrio newOrderTrio;

            if (isOptionCall && option.Strike < _baseInstrumentPrice - _maxDistanceFromBInstrument)
            {
                var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);

                newOrderTrio = PlaceTrailingOrder(option, activeCE.Value, lastPrice, currentTime, orderTrio);
                _activeCall = activeCE.Value;
                return newOrderTrio;
            }
            else if (!isOptionCall && option.Strike > _baseInstrumentPrice + _maxDistanceFromBInstrument)
            {
                var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);
                newOrderTrio = PlaceTrailingOrder(option, activePE.Value, lastPrice, currentTime, orderTrio);
                _activePut = activePE.Value;
                return newOrderTrio;
            }
            return orderTrio;
        }
        private OrderTrio PlaceTrailingOrder(Instrument option, Instrument newInstrument, decimal lastPrice,
            DateTime currentTime, OrderTrio orderTrio)
        {
            try
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                        "Trailing market...", "PlaceTrailingOrder");

                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType,
                    lastPrice, option.InstrumentToken, false,
                    _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Closed Option {0}. Bought {1} lots @ {2}.", option.TradingSymbol,
                        _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                OnTradeExit(order);

                decimal profitMade = order.AveragePrice - orderTrio.Order.AveragePrice;

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
                lastPrice = newInstrument.LastPrice;

                GetCriticalLevels(lastPrice, out stopLoss, out targetProfit, profitMade);
                order = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, lastPrice, //newInstrument.LastPrice,
                    newInstrument.InstrumentToken, true, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

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
                orderTrio.BaseInstrumentStopLoss = _bCandle.OpenPrice;
                //orderTrio.EntryRSI = entryRSI;
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
        private OrderTrio TradeEntry(DateTime currentTime, Instrument option, decimal bCandleOpenPrice)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal stopLoss = 0, targetProfit = 0;
                GetCriticalLevels(option.LastPrice, out stopLoss, out targetProfit);

                int tradeQty = GetTradeQty(option.LastPrice - stopLoss, option.LotSize);
                //ENTRY ORDER - BUY ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                    option.InstrumentToken, true, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

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
                orderTrio.StopLoss = stopLoss;
                orderTrio.BaseInstrumentStopLoss = bCandleOpenPrice;
                orderTrio.TargetProfit = targetProfit;
                //orderTrio.EntryRSI = entryRSI;
                orderTrio.EntryTradeTime = currentTime;

                OnTradeEntry(order);
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

        //private OrderTrio TradeEntry(DateTime currentTime, 
        //    IIndicatorValue rsi, IIndicatorValue ema, IIndicatorValue bema, 
        //    bool isOptionCall)
        //{
        //    OrderTrio orderTrio = null;
        //    try
        //    {
        //        Instrument option = isOptionCall ? ActiveOptions.FirstOrDefault(x => x.InstrumentType.ToLower() == "ce") : ActiveOptions.FirstOrDefault(x => x.InstrumentType.ToLower() == "pe");
        //        decimal entryRSI = 0;
        //        if (option != null && CheckEntryCriteria(_baseInstrumentToken, currentTime, rsi, ema, bema, isOptionCall, out entryRSI))
        //        {
        //            decimal stopLoss = 0, targetProfit = 0;
        //            GetCriticalLevels(option.LastPrice, out stopLoss, out targetProfit);

        //            int tradeQty = GetTradeQty(option.LastPrice - stopLoss, option.LotSize);
        //            //ENTRY ORDER - BUY ALERT
        //            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //                option.InstrumentToken, true, tradeQty * Convert.ToInt32(option.LotSize),
        //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product:Constants.PRODUCT_NRML);

        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
        //                string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, option.TradingSymbol, order.AveragePrice), "TradeEntry");

        //            ////SL for first orders
        //            //Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, stopLoss,
        //            //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

        //            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
        //            //    string.Format("Placed Stop Loss for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, slOrder.AveragePrice), "TradeEntry");

        //            ////target profit
        //            //Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, targetProfit,
        //            //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

        //            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
        //            //    string.Format("Placed Target Profit for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, tpOrder.AveragePrice), "TradeEntry");

        //            orderTrio = new OrderTrio();
        //            orderTrio.Option = option;
        //            orderTrio.Order = order;
        //            //orderTrio.SLOrder = slOrder;
        //            //orderTrio.TPOrder = tpOrder;
        //            orderTrio.StopLoss = stopLoss;
        //            orderTrio.TargetProfit = targetProfit;
        //            orderTrio.EntryRSI = entryRSI;
        //            orderTrio.EntryTradeTime = currentTime;
                    
        //            OnTradeEntry(order);
        //            //OnTradeEntry(slOrder);
        //            //OnTradeEntry(tpOrder);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Trading stopped");
        //        //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }
        //    return orderTrio;
        //}
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
        private bool CheckEntryCriteria(uint token, DateTime currentTime, IIndicatorValue rsi, IIndicatorValue ema, 
            IIndicatorValue bema, bool isOptionCall, out decimal entryRSI)
        {
            entryRSI = 0;
            try
            {
                if (!tokenRSI.ContainsKey(token) || !_belowEMA.ContainsKey(token))
                {
                    return false;
                }

                decimal emaValue = ema.GetValue<decimal>();
                decimal rsiValue = rsi.GetValue<decimal>();
                decimal bemaValue = _baseEMAValue;// bema.GetValue<decimal>();

                entryRSI = rsiValue;



                /// Entry Criteria:
                /// RSI Cross EMA from below, below 40 and both crosses above 40
                /// RSI Cross EMA from below between 40 and 50

                //if (rsi.IsFormed && ema.IsFormed && rsiValue < emaValue)
                //{
                //    _belowEMA[token] = true;
                //}

                //if (!crossed)
                //{
                //    if (!_belowEMA.ContainsKey(token))
                //    {
                //        _belowEMA.Add(token, rsiValue < emaValue);
                //    }
                //    else
                //    {
                //        _belowEMA[token] = rsiValue < emaValue;
                //    }
                //}

                if (rsi.IsFormed && ema.IsFormed
                    && (isOptionCall ? rsiValue > emaValue : rsiValue < emaValue) /*+ _rsiBandForEntry */ && rsiValue >= _rsiLowerLimit && emaValue >= _rsiLowerLimit
                    && (_entryAtCross ? crossed && (isOptionCall ? _belowEMA[token] : !_belowEMA[token]) : true) // && _betweenEMAandUpperBand)
                    && ((isOptionCall && _baseInstrumentPrice >= (_lowerLimitForCEBuy <= 0 ? bemaValue : _lowerLimitForCEBuy))
                    || (!isOptionCall && _baseInstrumentPrice <= (_upperLimitForPEBuy <= 0 ? bemaValue : _upperLimitForPEBuy)))
                    )
                {
                    _belowEMA[token] = rsiValue < emaValue;
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
        private bool CheckExitCriteria(uint token, decimal bCandleClosePrice, decimal lastPrice, DateTime currentTime, 
            OrderTrio orderTrio, decimal rsiValue, decimal emaValue, bool isOptionCall, out bool tpHit)
        {
            /// Exit Criteria:
            /// RSI cross EMA from above and both crosses below 60, and both comes below 60
            /// RSI cross EMA from above between 50 and 60.
            /// Target profit hit
            tpHit = false;
            try
            {
                if(lastPrice > orderTrio.TargetProfit)
                {
                    tpHit = true;
                    return true;
                }


                if ( tokenRSI[token].IsFormed && lTokenEMA[token].IsFormed 
                    && (((isOptionCall && ((rsiValue <= _rsiUpperLimit && emaValue <= _rsiUpperLimit && rsiValue < emaValue) || (_exitDecisiveCandleHighLow > 0 && bCandleClosePrice > 0 && bCandleClosePrice < orderTrio.BaseInstrumentStopLoss))) || 
                            (!isOptionCall && ((rsiValue >= _rsiLowerLimit && emaValue >= _rsiLowerLimit && rsiValue > emaValue)||(_exitDecisiveCandleHighLow > 0 && bCandleClosePrice > 0 && bCandleClosePrice > orderTrio.BaseInstrumentStopLoss))))
                    || (lastPrice <= orderTrio.StopLoss ))
                    )
                {
                    //_belowEMA[token] = false;
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
                //var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
                //var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

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

                //if (OptionUniverse == null ||
                //(OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                //|| OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice +_maxDistanceFromBInstrument)
                //   || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice -_minDistanceFromBInstrument 
                //   || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                //    )
                if (OptionUniverse == null ||
               (OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice
               || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice + 1000)
                  || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice
                  || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - 1000)
                   )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    //var closeOptions = dl.LoadCloseByExpiryOptions(_baseInstrumentToken, _baseInstrumentPrice, Math.Max(_maxDistanceFromBInstrument, 600)); //loading at least 600 tokens each side
                    var closeOptions = dl.LoadCloseByExpiryOptions(_baseInstrumentToken, _baseInstrumentPrice, 1000, currentTime); //loading at least 600 tokens each side

                    UpsertOptionUniverse(closeOptions);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

                //if (_orderTrio == null)
                //{
                //    _activePut = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument).Value;
                //    _activeCall = OptionUniverse[(int)InstrumentType.CE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument).Value;
                //}

                //var activePE = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
                //var activeCE = OptionUniverse[(int)InstrumentType.CE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);


                ////First time.
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
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                    String.Format(@"Error occurred! Trading has stopped. {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }
        private void UpdateOptionPrices(uint token, decimal lastPrice)
        {
            if (OptionUniverse != null)
            {
                if (OptionUniverse[(int)InstrumentType.PE].Any(x => x.Value.InstrumentToken == token))
                {
                    Instrument option = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Value.InstrumentToken == token).Value;
                    option.LastPrice = lastPrice;
                    OptionUniverse[(int)InstrumentType.PE][option.Strike] = option;

                    //if (_activePut.InstrumentToken == token)
                    //{
                    //    _activePut.LastPrice = lastPrice;
                    //}
                }
                else if (OptionUniverse[(int)InstrumentType.CE].Any(x => x.Value.InstrumentToken == token))
                {
                    Instrument option = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Value.InstrumentToken == token).Value;
                    option.LastPrice = lastPrice;
                    OptionUniverse[(int)InstrumentType.CE][option.Strike] = option;
                    

                    //if (_activeCall.InstrumentToken == token)
                    //{
                    //    _activeCall.LastPrice = lastPrice;
                    //}
                }
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
        
        private bool TradeExit(DateTime currentTime, OrderTrio orderTrio, decimal rsiValue, decimal emaValue, bool isOptionCall, decimal bCandleClosePrice)
        {
            try
            {
                    Instrument option = _orderTrio.Option;
                    //Instrument option = orderTrio.Option;
                    bool tpHit = false;

                    if(CheckExitCriteria(_baseInstrumentToken, bCandleClosePrice, option.LastPrice, currentTime, orderTrio, rsiValue, emaValue, isOptionCall, out tpHit))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                               option.InstrumentToken, false, _tradeQty * Convert.ToInt32(option.LotSize),
                               algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

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
                        //orderTrio.Option = null;

                        //ActiveOptions.Remove(option);

                        return true;
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
        
        private void GetCriticalLevels(decimal lastPrice, out decimal stopLoss, out decimal targetProfit, decimal profitMade = 0)
        {
            stopLoss = Math.Round((lastPrice > _targetProfit ? lastPrice - _targetProfit : 2.0m) * 20) / 20;
            targetProfit = Math.Round((lastPrice + Math.Abs(_targetProfit - profitMade)) * 20) / 20;
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
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
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
        private void LoadBaseInstrumentEMA(uint  bToken, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_bEMA)
                {
                    DataLogic dl = new DataLogic();
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _rsiTimeSpan, false);

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
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _rsiTimeSpan, false);

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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, 
                    ticks[0].Timestamp.GetValueOrDefault(DateTime.UtcNow), String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
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
            if (_lowerLimitForCEBuy <= 0 && _upperLimitForPEBuy <= 0 && !_bEMALoaded)
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
                Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, 0, tpOrder, currentTime, lastPrice).Result;

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
