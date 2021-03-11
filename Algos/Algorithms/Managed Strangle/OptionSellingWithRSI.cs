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
    public class OptionSellingWithRSI : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionSellingWithRSI source);
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
        //public Dictionary<uint, OrderLevels> tokenTradeLevels;

        public readonly decimal _rsiBandForExit;
        public readonly double _timeBandForExit;

        //public StrangleOrderLinkedList sorderList;
        public OrderTrio _callOrderTrio;
        public OrderTrio _putOrderTrio;

        public List<Order> _pastOrders;
        private bool _stopTrade;
        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;

        Dictionary<uint, IIndicatorValue> tokenEMAIndicator;
        Dictionary<uint, IIndicatorValue> tokenRSIIndicator;

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
        private RelativeStrengthIndex _bRSI;
        private bool _bRSILoaded = false, _bRSILoadedFromDB = false;
        private bool _bRSILoading = false;


        public const int CANDLE_COUNT = 30;
        private const int BASE_RSI_UPPER_THRESHOLD = 65;
        private const int BASE_RSI_LOWER_THRESHOLD = 35;

        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public readonly int _emaLength;
        private readonly decimal _rsiUpperLimitForEntry = 55;
        private readonly decimal _rsiLowerLimitForEntry = 40;
        private readonly decimal _rsiMarginForExit = 10;
        private readonly decimal _rsiLimitForExit = 60;
        private bool _nwCESet = false;
        private bool _nwPESet = false;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;

        //public const int SHORT_EMA = 5;
        //public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        //public const int RSI_THRESHOLD = 60;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.StrangleWithRSI;
        //TimeSpan candletimeframe;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        
        public OptionSellingWithRSI(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, decimal minDistanceFromBInstrument, 
            decimal maxDistanceFromBInstrument, int emaLength, int algoInstance = 0,
            bool positionSizing = false, decimal maxLossPerTrade = 0, 
            decimal rsiUpperLimitForEntry = 55, decimal rsiLowerLimitForEntry = 40m,
            decimal rsiMarginForExit = 10, decimal rsiLimitForExit = 60m)
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _minDistanceFromBInstrument = minDistanceFromBInstrument;
            _maxDistanceFromBInstrument = maxDistanceFromBInstrument;
            _rsiUpperLimitForEntry = rsiUpperLimitForEntry;
            _rsiLowerLimitForEntry = rsiLowerLimitForEntry;
            _rsiMarginForExit = rsiMarginForExit;
            _rsiLimitForExit = rsiLimitForExit;
            _emaLength = emaLength;
            _rsiBandForExit = 3;
            _timeBandForExit = 3;

            _stopTrade = true;

            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            //tokenTradeLevels = new Dictionary<uint, OrderLevels>();
            _pastOrders = new List<Order>();
            //sorderList = new StrangleOrderLinkedList();
            //_callOrderTrio = new OrderTrio();
            //_putOrderTrio = new OrderTrio();

            SubscriptionTokens = new List<uint>();

            ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            //EMAs
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();
            tokenEMAIndicator = new Dictionary<uint, IIndicatorValue>();
            tokenRSIIndicator = new Dictionary<uint, IIndicatorValue>();

            _EMALoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            CandleSeries candleSeries = new CandleSeries();

            _bRSI = new RelativeStrengthIndex();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            ExponentialMovingAverage sema = null, lema = null;
            RelativeStrengthIndex rsi = null;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;


            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, 0, 0, _rsiUpperLimitForEntry, 0,
                _rsiLowerLimitForEntry, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, _emaLength, _rsiMarginForExit, _rsiLimitForExit,
                _maxDistanceFromBInstrument, _minDistanceFromBInstrument, 
                positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();
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

                    //if (token != _baseInstrumentToken && tick.LastTradeTime.HasValue)
                    //{
                    MonitorCandles(tick, currentTime);

                    if (!_EMALoaded.Contains(token))
                    {
                        //this method can now be slow doenst matter. 
                        //make it async
                        LoadHistoricalEMAs(currentTime);
                    }
                    else
                    {
                        if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                        {
                            return;
                        }

                        IIndicatorValue rsi = tokenRSI[token].Process(tick.LastPrice, isFinal: false);
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
                        if (rsi.IsEmpty || ema.IsEmpty || !rsi.IsFormed || !ema.IsFormed)
                        {
                            return;
                        }

                        //if (ActiveOptions.Any(x => x.InstrumentToken == token) && tokenRSIIndicator.ContainsKey(_baseInstrumentToken))
                           // if (((ActiveOptions[0] != null && ActiveOptions[0].InstrumentToken == token)  || (ActiveOptions[1] != null && ActiveOptions[1].InstrumentToken == token)) && tokenRSIIndicator.ContainsKey(_baseInstrumentToken))
                            if (ActiveOptions.All(x => x != null)  && ActiveOptions.Any(x => x.InstrumentToken == token) && tokenRSIIndicator.ContainsKey(_baseInstrumentToken))
                            {
                            Instrument option = ActiveOptions.Find(x => x.InstrumentToken == token);

                            bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                            OrderTrio orderTrio = isOptionCall ? _callOrderTrio : _putOrderTrio;

                            IIndicatorValue brsi = tokenRSIIndicator[_baseInstrumentToken];
                            if (orderTrio == null)
                            {
                                orderTrio = TradeEntry(token, currentTime, tick.LastPrice, isOptionCall, rsi, ema, brsi);
                                //if(orderTrio == null)
                                //{
                                //    _stopTrade = true;
                                //    Logger.LogWrite("Trading Stopped as algo encountered an error");
                                //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                                //        String.Format(@"Error occurred! Trading has stopped. \r\n {0}", "Order did not execute properly"), "ActiveTradeIntraday");
                                //    Thread.Sleep(100);
                                //}
                            }
                            else
                            {
                                bool orderExited = TradeExit(token, currentTime, tick.LastPrice, orderTrio, rsi, ema);

                                if (orderExited)
                                {
                                    orderTrio = null;
                                }
                                else
                                {
                                  //  orderTrio = TrailMarket(token, isOptionCall, currentTime, tick.LastPrice, orderTrio);
                                }
                            }

                            if (isOptionCall)
                            {
                                _callOrderTrio = orderTrio;
                            }
                            else
                            {
                                _putOrderTrio = orderTrio;
                            }

                            //Put a hedge at 3:15 PM
                            // TriggerEODPositionClose(tick.LastTradeTime);
                            ConvertOrderToNRML(currentTime);
                        }
                    }
                    //}
                }
                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        private void ConvertOrderToNRML(DateTime? currentTime)
        {
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 05, 00))
            {
                if (_callOrderTrio != null && !_nwCESet)
                {
                    _nwCESet = true;

                    Instrument nwOption;

                    //Change Order type to Normal for overnight retention
                    Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, _callOrderTrio.Order, product: Constants.PRODUCT_NRML, currentTime.Value).Result;

                    if (order.Status == Constants.ORDER_STATUS_REJECTED)
                    {
                        _stopTrade = true;
                        return;
                    }

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       "Changed order type from MIS to Normal", "ConvertOrderToNRML");

                    OnTradeEntry(order);
                }
                if (_putOrderTrio != null && !_nwPESet)
                {
                    _nwPESet = true;

                    Instrument nwOption;

                    //Change Order type to Normal for overnight retention
                    Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, _putOrderTrio.Order, product: Constants.PRODUCT_NRML, currentTime.Value).Result;
                    if (order.Status == Constants.ORDER_STATUS_REJECTED)
                    {
                        _stopTrade = true;
                        return;
                    }
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       "Changed order type from MIS to Normal", "ConvertOrderToNRML");

                    //Save order ID to DB
                    OnTradeEntry(order);
                }
            }
        }

        //private void TriggerEODPositionClose(DateTime? currentTime)
        //{
        //    if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 15, 00))
        //    {
        //        if (_callOrderTrio != null && !_nwCESet)
        //        {
        //            _nwCESet = true;

        //            Instrument nwOption;

        //            if(!OptionUniverse[(int)InstrumentType.CE].TryGetValue(_callOrderTrio.Option.Strike + 500, out nwOption))
        //            {
        //                DataLogic dl = new DataLogic();
        //                nwOption = dl.GetInstrument(_callOrderTrio.Option.Expiry.Value, _baseInstrumentToken, 
        //                    _callOrderTrio.Option.Strike + 500, _callOrderTrio.Option.InstrumentType.Trim(' '));
        //            }
        //            //ENTRY ORDER - Buy orders at 500 points discuss ALERT
        //            Order order = MarketOrders.PlaceOrder(_algoInstance, nwOption.TradingSymbol, nwOption.InstrumentType, 0,
        //                nwOption.InstrumentToken, true, _tradeQty * Convert.ToInt32(nwOption.LotSize),
        //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, "NW");

        //            if (order.Status == Constants.ORDER_STATUS_REJECTED)
        //            {
        //                _stopTrade = true;
        //                return;
        //            }

        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
        //               string.Format("TRADE!! Placed night watchman. {0} lots of {1} @ {2}", _tradeQty,
        //               nwOption.TradingSymbol, order.Tradingsymbol, order.AveragePrice), "TradeEODEntry");

        //            //Save order ID to DB
        //            OnTradeEntry(order);
        //        }
        //        if (_putOrderTrio != null && !_nwPESet)
        //        {
        //            _nwPESet = true;


        //            Instrument nwOption;

        //            if (!OptionUniverse[(int)InstrumentType.PE].TryGetValue(_putOrderTrio.Option.Strike -500, out nwOption))
        //            {
        //                DataLogic dl = new DataLogic();
        //                nwOption = dl.GetInstrument(_putOrderTrio.Option.Expiry.Value, _baseInstrumentToken,
        //                    _putOrderTrio.Option.Strike - 500, _putOrderTrio.Option.InstrumentType.Trim(' '));
        //            }

        //            //ENTRY ORDER - Buy orders at 500 points discuss ALERT
        //            Order order = MarketOrders.PlaceOrder(_algoInstance, nwOption.TradingSymbol, nwOption.InstrumentType, 0,
        //                nwOption.InstrumentToken, true, _tradeQty * Convert.ToInt32(nwOption.LotSize),
        //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, "NW");

        //            if (order.Status == Constants.ORDER_STATUS_REJECTED)
        //            {
        //                _stopTrade = true;
        //                return;
        //            }
        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
        //               string.Format("TRADE!! Placed night watchman. {0} lots of {1} @ {2}", _tradeQty,
        //               nwOption.TradingSymbol, order.Tradingsymbol, order.AveragePrice), "TradeEntry");

        //            //Save order ID to DB
        //            OnTradeEntry(order);
        //        }
        //    }
        //}
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

        private void LoadHistoricalEMAs(DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

            try
            {
                //var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_EMALoaded.Contains(x));
                var tokens = SubscriptionTokens.Where(x => !_EMALoaded.Contains(x));

                StringBuilder sb = new StringBuilder();

                lock (lTokenEMA)
                {
                    foreach (uint t in tokens)     {
               
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

                //if (!_firstCandleOpenPriceNeeded.ContainsKey(token))
                //{
                //    _firstCandleOpenPriceNeeded.Add(token, candleStartTime != lastCandleEndTime);
                //}

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!lTokenEMA.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //    _SQLLoading.Add(token);

                if (tokenList != string.Empty)
                {
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, _emaLength + RSI_LENGTH, lastCandleEndTime));
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    //    uint tkn = Convert.ToUInt32(tk);

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
                    //}
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalEMAs");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (_EMALoaded.Contains(e.InstrumentToken) && !_stopTrade)
                {
                    if (!tokenRSI.ContainsKey(e.InstrumentToken) || !lTokenEMA.ContainsKey(e.InstrumentToken))
                    {
                        return;
                    }
                    
                    tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                    lTokenEMA[e.InstrumentToken].Process(tokenRSI[e.InstrumentToken].GetValue<decimal>(0), isFinal: true);

                    if (ActiveOptions.Any(x => x.InstrumentToken == e.InstrumentToken))
                    {
                        Instrument option = ActiveOptions.Find(x => x.InstrumentToken == e.InstrumentToken);

                        decimal rsi = tokenRSI[e.InstrumentToken].GetValue<decimal>(0);
                        decimal ema = lTokenEMA[e.InstrumentToken].GetValue<decimal>(0);


                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                            String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
                            , option.TradingSymbol, Decimal.Round(ema, 2), Decimal.Round(rsi, 2)), "CandleManger_TimeCandleFinished");

                        Thread.Sleep(100);
                        //bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                        //OrderTrio orderTrio = isOptionCall ? _callOrderTrio : _putOrderTrio;

                        //if (orderTrio == null)
                        //{
                        //    orderTrio = TradeEntry(e.InstrumentToken, e.CloseTime, e.ClosePrice);
                        //}
                        //if (isOptionCall)
                        //{
                        //    _callOrderTrio = orderTrio;
                        //}
                        //else
                        //{
                        //    _putOrderTrio = orderTrio;
                        //}

                        //if (orderTrio != null)
                        //{
                        //    bool orderExited = TradeExit(e.InstrumentToken, e.CloseTime, e.ClosePrice, orderTrio);

                        //    if (orderExited)
                        //    {
                        //        orderTrio = null;
                        //    }
                        //    else
                        //    {
                        //        orderTrio = TrailMarket(e.InstrumentToken, isOptionCall, e.CloseTime, e.ClosePrice, orderTrio);
                        //    }
                        //}
                        //else
                        //{
                        //    orderTrio = TradeEntry(e.InstrumentToken, e.CloseTime, e.ClosePrice);
                        //}
                        //if (isOptionCall)
                        //{
                        //    _callOrderTrio = orderTrio;
                        //}
                        //else
                        //{
                        //    _putOrderTrio = orderTrio;
                        //}
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
        private OrderTrio TrailMarket(uint token, bool isOptionCall, DateTime currentTime, 
            decimal lastPrice, OrderTrio orderTrio)
        {
            if (orderTrio != null && orderTrio.Option != null)
            {
                Instrument option = orderTrio.Option;

                var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
                var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);
                OrderTrio newOrderTrio;

                IIndicatorValue rsi, ema;

                decimal entryRSI;
                if (isOptionCall && option.Strike > _baseInstrumentPrice + _maxDistanceFromBInstrument
                    && tokenRSIIndicator.TryGetValue(activeCE.Value.InstrumentToken, out rsi)
                    && tokenEMAIndicator.TryGetValue(activeCE.Value.InstrumentToken, out ema)
                    && TimeCandles.ContainsKey(activeCE.Value.InstrumentToken)
                    && CheckEMATrail(activeCE.Value.InstrumentToken, currentTime, rsi, ema, out entryRSI))
                {
                    newOrderTrio = PlaceTrailingOrder(option, activeCE.Value, lastPrice, currentTime, orderTrio);
                    newOrderTrio.EntryRSI = entryRSI;
                    newOrderTrio.EntryTradeTime = currentTime;
                    
                    if(newOrderTrio.Option.InstrumentToken != activeCE.Value.InstrumentToken)
                    {
                        _stopTrade = true;
                        Logger.LogWrite("Trading Stopped as order did not execute properly");
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                            String.Format(@"Error occurred! Trading has stopped. \r\n {0}","Order did not execute properly"), "TrailMarket");
                        Thread.Sleep(100);
                    }

                    ActiveOptions.Remove(option);
                    ActiveOptions.Add(activeCE.Value);
                    return newOrderTrio;
                }
                else if (!isOptionCall && option.Strike < _baseInstrumentPrice - _maxDistanceFromBInstrument
                     && tokenRSIIndicator.TryGetValue(activePE.Value.InstrumentToken, out rsi)
                    && tokenEMAIndicator.TryGetValue(activePE.Value.InstrumentToken, out ema)
                    && TimeCandles.ContainsKey(activePE.Value.InstrumentToken)
                    && CheckEMATrail(activePE.Value.InstrumentToken, currentTime, rsi, ema, out entryRSI))
                {
                    newOrderTrio = PlaceTrailingOrder(option, activePE.Value, lastPrice, currentTime, orderTrio);
                    newOrderTrio.EntryRSI = entryRSI;
                    newOrderTrio.EntryTradeTime = currentTime;

                    if (newOrderTrio.Option.InstrumentToken != activePE.Value.InstrumentToken)
                    {
                        _stopTrade = true;
                        Logger.LogWrite("Trading Stopped as order did not execute properly");
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", "Order did not execute properly"), "TrailMarket");
                        Thread.Sleep(100);
                    }

                    ActiveOptions.Remove(option);
                    ActiveOptions.Add(activePE.Value);
                    return newOrderTrio;
                }
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
                    lastPrice, option.InstrumentToken, true,
                    _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                if(order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Closed Option {0}. Bought {1} lots @ {2}.", option.TradingSymbol,
                        _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                OnTradeExit(order);

                //Order slorder = orderTrio.SLOrder;
                //if (slorder != null)
                //{
                //    slorder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slorder, currentTime).Result;
                //    if (slorder.Status == Constants.ORDER_STATUS_REJECTED)
                //    {
                //        _stopTrade = true;
                //        return orderTrio;
                //    }

                //    OnTradeExit(slorder);
                //}

                lastPrice = TimeCandles[newInstrument.InstrumentToken].Last().ClosePrice;
                order = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, lastPrice, //newInstrument.LastPrice,
                    newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       string.Format("Traded Option {0}. Sold {1} lots @ {2}.", newInstrument.TradingSymbol,
                       _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                //slorder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, Math.Round(order.AveragePrice * 3 * 20) / 20,
                //   newInstrument.InstrumentToken, true, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                //   algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                //if (slorder.Status == Constants.ORDER_STATUS_REJECTED)
                //{
                //    _stopTrade = true;
                //    return orderTrio;
                //}

                OnTradeEntry(order);
                //OnTradeEntry(slorder);

                orderTrio.Option = newInstrument;
                orderTrio.Order = order;
                //orderTrio.SLOrder = slorder;
                
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
        private OrderTrio TradeEntry(uint token, DateTime currentTime, decimal lastPrice, bool isOptionCall,
            IIndicatorValue rsi, IIndicatorValue ema, IIndicatorValue brsi)
        {
            OrderTrio orderTrio = null;
            try
            {
                Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);

                decimal entryRSI = 0;
                ///TODO: Both previouscandle and currentcandle would be same now, as trade is getting generated at 
                ///candle close only. _pastorders check below is not needed anymore.
                    if (CheckEMA(token, currentTime, rsi, ema, brsi, isOptionCall, out entryRSI))
                    {
                    //ENTRY ORDER - Sell ALERT
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                        token, false, _tradeQty * Convert.ToInt32(option.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                    if(order.Status == Constants.ORDER_STATUS_REJECTED)
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
                       string.Format("TRADE!! Sold {0} lots of {1} @ {2}", _tradeQty,
                       option.TradingSymbol, order.AveragePrice), "TradeEntry");

                    orderTrio = new OrderTrio();
                    orderTrio.Order = order;
                    //orderTrio.SLOrder = slOrder;
                    orderTrio.Option = option;
                    orderTrio.EntryRSI = entryRSI;
                    orderTrio.EntryTradeTime = currentTime;

                    OnTradeEntry(order);
                    //OnTradeEntry(slOrder);

                    #region PositionSizing
                    //int order2Qty = 0;
                    //Order order2 = null;
                    //Order slOrder2 = null;
                    //if (_positionSizing)
                    //{
                    //    //Another ENTRY ORDER @ 30% below the top of last candle
                    //    decimal order2Price = 0;
                    //    GetOrder2PriceQty(tradeQty, order.AveragePrice - cl.StopLossPrice, previousCandle, option.LotSize, out order2Qty, out order2Price);

                    //    if (order2Qty > 0 && order2Price > cl.StopLossPrice)
                    //    {
                    //        order2 = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, order2Price,
                    //            token, true, order2Qty * Convert.ToInt32(option.LotSize),
                    //            algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                    //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order2.OrderTimestamp.Value, string.Format("TRADE!! Placed Limit Order for {0} lots of {1} @ {2}", order2Qty, option.TradingSymbol, order2.AveragePrice), "TradeEntry");


                    //        //SL for second orders
                    //        slOrder2 = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, cl.StopLossPrice,
                    //            token, false, order2Qty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                    //        //tokenTradeLevels.Add(token, new OrderLevels { FirstLegOrder = order, SLOrder = slOrder, Levels = cl });
                    //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder2.OrderTimestamp.Value, string.Format("TRADE!! Placed Stop Loss for {0} lots of {1} @ {2}", order2Qty, option.TradingSymbol, slOrder2.AveragePrice), "TradeEntry");


                    //        OrderLinkedListNode orderNode2 = new OrderLinkedListNode();
                    //        orderNode2.Order = order2;
                    //        orderNode2.SLOrder = slOrder2;
                    //        orderNode2.FirstLegCompleted = false;
                    //        orderNode2.PrevOrderNode = orderNode;
                    //        orderNode.NextOrderNode = orderNode2;

                    //        OnTradeEntry(order2);
                    //        OnTradeEntry(slOrder2);
                    //    }
                    //}
                    #endregion

                    _pastOrders.Add(order);
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
            return orderTrio;
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
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        
        //private void TradeEntry(Tick[] ticks)
        //{
        //    uint token = ticks[0].InstrumentToken;
        //    Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);

        //    Candle previousCandle = TimeCandles[token].LastOrDefault(x => x.State == CandleStates.Finished);
        //    Candle currentCandle = TimeCandles[token].Last();

        //    if (previousCandle != null
        //        && option != null
        //        //&& !tokenTradeLevels.ContainsKey(token)
        //        //&& !tokenTradeLevels.Any(x=>x.Value.Trade.InstrumentType == option.InstrumentType)
        //        && !(tokenTradeLevels.Count() > 0)
        //        && ticks[0].LastPrice > previousCandle.OpenPrice
        //        && previousCandle.ClosePrice - previousCandle.OpenPrice > 0.002m * previousCandle.OpenPrice
        //        && previousCandle.OpenTime.Date == ticks[0].Timestamp.Value.Date
        //        //&& !_pastTrades.Any(x=>x.InstrumentToken == token && x.TradeTime >= currentCandle.OpenTime)
        //        && !_pastTrades.Any(x=>x.TradeTime >= currentCandle.OpenTime)
        //        )
        //    {
        //        //ENTRY ORDER - BUY ALERT
        //        ShortTrade shortTrade = PlaceOrder(option.TradingSymbol, option.InstrumentType, ticks[0].LastPrice,
        //            token, true, TRADE_QTY * Convert.ToInt32(option.LotSize), ticks[0].Timestamp);
        //        tokenTradeLevels.Add(token, new TradeLevels { Trade = shortTrade, Levels = GetCriticalLevels(shortTrade, token, previousCandle) }); // Dictionary<ShortTrade, CriticalLevels>  shortTrade, GetCriticalLevels(shortTrade, timeCandles));
        //        _pastTrades.Add(shortTrade);
        //    }
        //}
        private async void LoadOptionsToTrade(DateTime currentTime)
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
                //       ((ce.Strike >= _baseInstrumentPrice + _minDistanceFromBInstrument && ce.Strike <= _baseInstrumentPrice +_maxDistanceFromBInstrument)
                //           || (_callOrderTrio != null && _callOrderTrio.Option != null && _callOrderTrio.Option.InstrumentToken == ce.InstrumentToken))
                //       && ((pe.Strike <= _baseInstrumentPrice - _minDistanceFromBInstrument && pe.Strike >= _baseInstrumentPrice -_maxDistanceFromBInstrument)
                //           || (_putOrderTrio != null && _putOrderTrio.Option != null && _putOrderTrio.Option.InstrumentToken == pe.InstrumentToken))
                //       )
                //    {
                //        return;
                //    }

                //}
                

                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice +_maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice -_minDistanceFromBInstrument 
                   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument);

                    if (_callOrderTrio != null && _callOrderTrio.Option != null && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_callOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.CE].Add(_callOrderTrio.Option.Strike, _callOrderTrio.Option);
                    }
                    if (_putOrderTrio != null && _putOrderTrio.Option != null && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_putOrderTrio.Option.Strike))
                    {
                        OptionUniverse[(int)InstrumentType.PE].Add(_putOrderTrio.Option.Strike, _putOrderTrio.Option);
                    }

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

                var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
                var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);


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
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }


        private bool TradeExit(uint token, DateTime currentTime, decimal lastPrice, 
            OrderTrio orderTrio, IIndicatorValue rsi, IIndicatorValue ema)
        {
            try
            {
                if (orderTrio.Option != null && orderTrio.Option.InstrumentToken == token)
                {
                    Instrument option = orderTrio.Option;
                    Order border;
                    if (CheckForExit(token, currentTime, orderTrio.EntryRSI, rsi, ema, orderTrio))
                    {
                        //Order slOrder = orderTrio.SLOrder;
                        Order order = orderTrio.Order;
                        int tradeQty = order.Quantity;

                        //#if market
                        //Place a new buy order and cancel the existing stoploss order.
                        border = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                            token, true, tradeQty, algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                        if(border.Status == Constants.ORDER_STATUS_REJECTED)
                        {
                            _stopTrade = true;
                            return false;
                        }

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, border.OrderTimestamp.Value,
                            string.Format("Bought back {0} lots of {1} at {2}", tradeQty, option.TradingSymbol, border.AveragePrice), "TradeExit");
                       
                        OnTradeExit(border);

                        //if (slOrder != null)
                        //{
                        //    //Cancel the existing SL order.
                        //    slOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slOrder, currentTime).Result;
                        //    OnTradeExit(slOrder);
                        //}
                        return true;
                    }
                }
            }
            catch (Exception exp)
            {
                _stopTrade = true;
                Logger.LogWrite(exp.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
            return false;
        }

        //        private async Task<bool> TradeExit(uint token, DateTime currentTime, decimal lastPrice, OrderLinkedList orderList)
        //        {
        //            try
        //            {
        //                //OrderLinkedList orderList = sorderList.CallOrderLinkedList;
        //                if (!_stopTrade && orderList.Option != null && orderList.Option.InstrumentToken == token)
        //                {
        //                    Instrument option = orderList.Option;

        //                    OrderLinkedListNode orderNode = orderList.FirstOrderNode;
        //                    Order border;
        //                    while (orderNode != null)
        //                    {
        //                        if (orderNode.FirstLegCompleted && orderNode.SLOrder != null && CheckForExit(token, currentTime))
        //                        {
        //                            Order order = orderNode.SLOrder;
        //                            int tradeQty = order.Quantity;

        ////#if market
        //                            //Place a new buy order and cancel the existing stoploss order.
        //                            border = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
        //                                token, true, tradeQty,
        //                                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

        //                            //Cancel the existing SL order.
        //                            order = MarketOrders.CancelOrder(_algoInstance, algoIndex, order, currentTime).Result;

        //                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, border.OrderTimestamp.Value,
        //                                string.Format("TRADE!! Bought back {0} lots of {1} at {2}", tradeQty, option.TradingSymbol, border.AveragePrice), "TradeExit");

        ////#elif local
        ////                            border = new Order();
        ////                            border.AveragePrice = lastPrice;
        ////                            border.Price = lastPrice;
        ////                            border.OrderType = Constants.ORDER_TYPE_MARKET;
        ////                            border.ExchangeTimestamp = currentTime;
        ////                            border.OrderTimestamp = currentTime;
        ////                            border.Tag = "Test";
        ////                            border.Status = Constants.ORDER_STATUS_COMPLETE;

        ////                            order.ExchangeTimestamp = currentTime;
        ////                            order.OrderTimestamp = currentTime;
        ////                            order.Tag = "Test";
        ////                            order.Status = Constants.ORDER_STATUS_CANCELLED;
        ////#endif
        ////                            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Exited the trade @ {0}", order.AveragePrice), "TradeExit");

        //                            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);
        //                            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, border);

        //                            OnTradeExit(order);
        //                            OnTradeExit(border);

        //                            orderNode.SLOrder = null;
        //                            //RemoveSLOrder(orderNode.SLOrder);
        //                        }
        //                        orderNode = orderNode.NextOrderNode;
        //                    }
        //                    orderNode = orderList.FirstOrderNode;
        //                    bool slOrderExists = false;
        //                    while (orderNode != null)
        //                    {
        //                        if (orderNode.FirstLegCompleted && orderNode.SLOrder != null)
        //                        {
        //                            slOrderExists = true;
        //                            break;
        //                        }
        //                        orderNode = orderNode.NextOrderNode;
        //                    }
        //                    if (!slOrderExists)
        //                    {
        //                        orderNode = orderList.FirstOrderNode;

        //                        ////Cancel all non executed orders
        //                        while (orderNode != null)
        //                        {
        //                            if (!orderNode.FirstLegCompleted)
        //                            {
        //#if market
        //                                MarketOrders.CancelOrder(AlgoInstance, algoIndex, orderNode.Order, currentTime);
        //                                MarketOrders.CancelOrder(AlgoInstance, algoIndex, orderNode.SLOrder, currentTime);
        //#endif
        //                            }
        //                            orderNode = orderNode.NextOrderNode;
        //                        }

        //                        orderList.FirstOrderNode = null;
        //                        orderList.Option = null;
        //                        orderList = null;
        //                        return true;
        //                    }
        //                }
        //            }
        //            catch (Exception exp)
        //            {
        //                _stopTrade = true;
        //                Logger.LogWrite(exp.StackTrace);
        //                Logger.LogWrite("Closing Application");
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
        //                Thread.Sleep(100);
        //                //Environment.Exit(0);
        //            }
        //            return false;
        //        }

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
        private bool CheckEMA(uint token, DateTime currentTime, IIndicatorValue rsi, IIndicatorValue ema, IIndicatorValue brsi, bool isOptionCall, out decimal entryRsi)
        {
            entryRsi = 0;
            try
            {
                if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                {
                    return false;
                }

                decimal emaValue = ema.GetValue<decimal>();
                decimal rsiValue = rsi.GetValue<decimal>();
                decimal brsiValue = brsi.GetValue<decimal>();

                if (rsi.IsFormed && ema.IsFormed && rsiValue < emaValue && rsiValue > emaValue - 5
                    &&( (rsiValue <= _rsiUpperLimitForEntry && rsiValue >= _rsiLowerLimitForEntry)
                    ||(brsi.IsFormed && brsiValue > BASE_RSI_UPPER_THRESHOLD  && !isOptionCall)
                    || (brsi.IsFormed && brsiValue < BASE_RSI_LOWER_THRESHOLD && isOptionCall)
                    ))
                {
                    entryRsi = rsiValue;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
                Thread.Sleep(100);
                //Environment.Exit(0);
                return false;
            }
        }
        private bool CheckEMATrail(uint token, DateTime currentTime, IIndicatorValue rsi, IIndicatorValue ema, out decimal entryRsi)
        {
            entryRsi = 0;
            try
            {
                if (!tokenRSI.ContainsKey(token) || !lTokenEMA.ContainsKey(token))
                {
                    return false;
                }

                decimal emaValue = ema.GetValue<decimal>();
                decimal rsiValue = rsi.GetValue<decimal>();

                if (rsi.IsFormed && ema.IsFormed && rsiValue < emaValue)
                    //&& rsiValue <= _rsiUpperLimitForEntry && rsiValue >= _rsiLowerLimitForEntry)
                {
                    entryRsi = rsiValue;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(ex.Message + ex.StackTrace);
                Logger.LogWrite("Trading Stopped");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
                Thread.Sleep(100);
                //Environment.Exit(0);
                return false;
            }
        }

        private bool CheckForExit(uint token, DateTime currentTime, decimal orderRSI, IIndicatorValue rsi, IIndicatorValue ema, OrderTrio orderTrio)
        {
            try
            {
                decimal emaValue = ema.GetValue<decimal>();
                decimal rsiValue = rsi.GetValue<decimal>();

                if (rsi.IsFormed && ema.IsFormed
                   && rsiValue > emaValue + _rsiMarginForExit || rsiValue >= _rsiLimitForExit
                   //&& (rsiValue >= _rsiLimitForExit || rsiValue >= orderRSI + _rsiMarginForExit)
                   //  && (
                   //((currentTime - orderTrio.EntryTradeTime).TotalMinutes < _timeBandForExit && rsiValue > emaValue + _rsiBandForExit) ||
                   //((currentTime - orderTrio.EntryTradeTime).TotalMinutes > _timeBandForExit && rsiValue > emaValue))
                   )
                {
                    return true;
                }
                else
                {
                    return false;
                }

                //if (rsi.IsFormed && ema.IsFormed 
                //    && rsiValue > emaValue + _rsiBandForExit
                //    && (rsiValue >= _rsiLimitForExit || rsiValue >= orderRSI + _rsiMarginForExit)
                //      && (
                //    ((currentTime - orderTrio.EntryTradeTime).TotalMinutes < _timeBandForExit && rsiValue > emaValue + _rsiBandForExit) ||
                //    ((currentTime - orderTrio.EntryTradeTime).TotalMinutes > _timeBandForExit && rsiValue > emaValue))
                //    )
                //{
                //    return true;
                //}
                //else
                //{
                //    return false;
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
                //Environment.Exit(0);
                return false;
            }
        }

        //private void RemoveSLOrder(Order slOrder)
        //{
        //    OrderLinkedListNode orderNode = orderList.FirstOrderNode;
        //    string orderId = slOrder.OrderId;
        //    while (orderNode != null)
        //    {
        //        if (orderNode.SLOrder != null && orderNode.SLOrder.OrderId == orderId)
        //        {
        //            orderNode.SLOrder = null;
        //        }
        //        orderNode = orderNode.NextOrderNode;
        //    }
        //}

        private CriticalLevels GetCriticalLevels(Candle previousCandle, decimal stopLossCandleFraction = -1)
        {
            CriticalLevels cl = new CriticalLevels();
            cl.PreviousCandle = previousCandle;

            decimal slPrice = previousCandle.ClosePrice * 3;

            cl.StopLossPrice = Math.Round(slPrice * 20) / 20;

            return cl;
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
                    if(!SubscriptionTokens.Contains(_baseInstrumentToken))
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

#region Historical Candle 
        private void LoadHistoricalCandles(string tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (lTokenEMA)
                {
                    DataLogic dl = new DataLogic();

                    //The below is from ticks
                    //List<decimal> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, token.ToString(), _candleTimeSpan);
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, tokenList, _candleTimeSpan);

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
                        for (int i = _emaLength - 1; i >= 0; i--)
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
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
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

        /// <summary>
        /// Modify existing order. This is used to change the SL of existing order
        /// </summary>
        /// <param name="instrument_tradingsymbol"></param>
        /// <param name="instrumenttype"></param>
        /// <param name="instrument_currentPrice"></param>
        /// <param name="instrument_Token"></param>
        /// <param name="buyOrder"></param>
        /// <param name="quantity"></param>
        /// <param name="tickTime"></param>
        /// <returns></returns>
        //private async Task<Order> ModifyOrder(Order slOrder, decimal sl, DateTime currentTime)
        //{
        //    try
        //    {
        //        // uint instrumentToken = tokenOrderLevel.Key;
        //        //Order slOrder = tokenOrderLevel.Value.SLOrder;
        //        //CriticalLevels updatedCLsForSecondLeg = tokenOrderLevel.Value.Levels;

        //        //string tradingSymbol = slOrder.Tradingsymbol;

        //        //decimal sl = updatedCLsForSecondLeg.StopLossPrice;

        //        Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, sl, slOrder, currentTime).Result;

        //        OnTradeEntry(order);
        //        return order;

        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ModifyOrder");
        //        Thread.Sleep(100);
        //        return null;
        //    }

        //}

        private void PublishLog(object sender, ElapsedEventArgs e)
        {
            Instrument[] localOptions = new Instrument[ActiveOptions.Count];
            ActiveOptions.CopyTo(localOptions);
            foreach (Instrument option in localOptions)
            {
                IIndicatorValue rsi, ema;

                if (option != null && tokenRSIIndicator.TryGetValue(option.InstrumentToken, out rsi) && tokenEMAIndicator.TryGetValue(option.InstrumentToken, out ema))
                {
                    decimal optionrsi = rsi.GetValue<decimal>();
                    decimal optionema = ema.GetValue<decimal>();

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
                    String.Format("Active Option: {0}, RSI({3}):{1}, EMA on RSI({4}): {2}", option.TradingSymbol,
                    Decimal.Round(optionrsi, 2), Decimal.Round(optionema, 2), rsi.IsFormed, ema.IsFormed), "Log_Timer_Elapsed");

                    Thread.Sleep(100);
                }
            }
        }


    }
}
