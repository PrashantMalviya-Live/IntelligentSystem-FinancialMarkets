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
    public class OptionActiveBuyingWithRSI : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionActiveBuyingWithRSI source);
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

        public List<OrderTrio> orderTrios;

        public List<Order> _pastOrders;
        private bool _stopTrade;
        public decimal _baseInstrumentPrice;
        Dictionary<uint, ExponentialMovingAverage> lTokenEMA;
        Dictionary<uint, RelativeStrengthIndex> tokenRSI;
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
        uint _baseInstrumentToken;
        public const int CANDLE_COUNT = 30;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;

        private readonly decimal _rsiUpperLimit = 0.65m;
        private readonly decimal _rsiLowerLimit = 0.55m;
        ExponentialMovingAverage _ema;
        RelativeStrengthIndex _rsi;

        //public const decimal PRICE_PERCENT_INCREASE = 0.005m;
        //public const decimal CANDLE_BULLISH_BODY_FRACTION = 0.55m;
        //public const decimal CANDLE_BULLISH_LOWERWICK_FRACTION = 0.5m;
        //public const decimal CANDLE_BULLISH_BODY_PRICE_FRACTION = 0.04m;
        //public const decimal TRIGGER_EMA_ENTRY = 0.6m;

        //public const decimal VOLUME_PERCENT_INCREASE = 1.0m;
        //public const long VOLUME_INCREASE_RATE = 2; //1/10 of the candle time
        //public const decimal INDEX_PERCENT_CHANGE = 0.001m;
        //public const decimal CPR_DISTANCE = 0.00005m;
        //public const decimal TARGET_PROFIT = 0.005m;
        //public const decimal STOPLOSS_PRICE = 0.0005m;
        //public const decimal STOPLOSS_PRICE_OPTION = 0.20m;
        //public const decimal STOPLOSS_PRICE_FRACTION = 0.75m;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly int _tradeQty;
        private readonly bool _positionSizing = false;
        private readonly decimal _maxLossPerTrade = 0;
        private readonly decimal _targetProfit;
        private readonly int _rsiLength;
        private readonly int _emaLength;



        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        
        public const int RSI_THRESHOLD = 60;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.MomentumBuyWithRSI;
        //TimeSpan candletimeframe;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public OptionActiveBuyingWithRSI(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, decimal minDistanceFromBInstrument, decimal maxDistanceFromBInstrument,
            decimal targetProfit, decimal stopLoss, int emaLength, int rsiLength, int algoInstance = 0, bool positionSizing = false, decimal maxLossPerTrade = 0 ) //, decimal rsi=15)
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _minDistanceFromBInstrument = minDistanceFromBInstrument;
            _maxDistanceFromBInstrument = maxDistanceFromBInstrument;
            _rsiLength = rsiLength;
            _emaLength = emaLength;
            _targetProfit = targetProfit;

            _stopTrade = false;

            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            //tokenTradeLevels = new Dictionary<uint, OrderLevels>();
            _pastOrders = new List<Order>();
            orderTrios = new List<OrderTrio>();
            //sorderList = new StrangleOrderLinkedList();

            SubscriptionTokens = new List<uint>();

            ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            //EMAs
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();
            _ema = new ExponentialMovingAverage(emaLength);
            _rsi = new RelativeStrengthIndex();
            _rsi.Length = _rsiLength;

            _EMALoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            CandleSeries candleSeries = new CandleSeries();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            //ExponentialMovingAverage sema = null, lema = null;
            //RelativeStrengthIndex rsi = null;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;


            //candleManger = new CandleManger(TimeCandles, CandleType.Time);
            //candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes);

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
        }


        public void LoadActiveOrders(Order activeCallOrder, Order activePutOrder)
        {
            //OrderLinkedListNode orderNode = new OrderLinkedListNode();
            //orderNode.SLOrder = activeOrder;
            //orderNode.FirstLegCompleted = true;
            //orderList.FirstOrderNode = orderNode;

            //DataLogic dl = new DataLogic();
            //orderList.Option = dl.GetInstrument(activeOrder.InstrumentToken);

            //ActiveOptions.Add(orderList.Option);
        }
        private async void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = tick.Timestamp.Value;
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

                    if (token != _baseInstrumentToken && tick.LastTradeTime.HasValue)
                    {
                        if(tokenRSI.ContainsKey(token))
                        {
                            tokenRSI[token].Process(tick.LastPrice, isFinal: true);
                            lTokenEMA[token].Process(tokenRSI[token].GetCurrentValue<decimal>(), isFinal: true);
                        }
                        else
                        {
                            _rsi = new RelativeStrengthIndex();
                            _rsi.Length = _rsiLength;
                            _rsi.Process(tick.LastPrice, isFinal: true);
                            _ema = new ExponentialMovingAverage(_emaLength);
                            _ema.Process(_rsi.GetCurrentValue<decimal>(), isFinal: true);

                            tokenRSI.Add(token, _rsi);
                            lTokenEMA.Add(token, _ema);
                        }

                        if (ActiveOptions.Any(x => x.InstrumentToken == token))
                        {
                            if (_ema.IsFormed && _rsi.IsFormed)
                            {
                                if (orderTrios.Count == 0)
                                {
                                    TradeEntry(token, tick.LastPrice, currentTime);
                                }
                                else if (orderTrios.Any(x => x.Option.InstrumentToken == token))
                                {
                                    TradeEMAExit(token, currentTime, tick.LastPrice);
                                }
                            }
                        }
                        //Put a hedge at 3:15 PM
                       // TriggerEODPositionClose(tick.LastTradeTime);
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
        }

        //private async void Trade(uint token, DateTime currentTime, decimal lastPrice)
        //{
        //    try
        //    {
        //            TradeEntry(token, lastPrice, currentTime);
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CandleManger_TimeCandleFinished");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }
        //}

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
        private async void MonitorCandles(Tick tick)
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
                    DateTime? candleStartTime = CheckCandleStartTime(tick.LastTradeTime.Value, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tick.Timestamp.Value, String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
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
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.Value, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        private async void LoadHistoricalEMAs(DateTime currentTime)
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
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, LONG_EMA + RSI_LENGTH, lastCandleEndTime));
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

                            lTokenEMA[tkn].Process(tokenRSI[tkn].GetCurrentValue<decimal>(), isFinal: true);
                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                tokenRSI[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                                lTokenEMA[tkn].Process(tokenRSI[tkn].GetCurrentValue<decimal>(), isFinal: true);
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


        //private async void TrailMarket(uint token, DateTime currentTime, decimal lastPrice, OrderLinkedList orderList, Candle e)
        //{

        //    if (orderList != null && orderList.Option != null && orderList.FirstOrderNode != null)
        //    {
        //        Instrument option = orderList.Option;

        //        var activeCE = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
        //        var activePE = OptionUniverse[(int)InstrumentType.PE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);

        //        //RelativeStrengthIndex ce_rsi, pe_rsi;

        //        if (option.InstrumentType.Trim(' ').ToLower() == "ce" && option.Strike > _baseInstrumentPrice + _maxDistanceFromBInstrument
        //            && CheckEMA(activeCE.Value.InstrumentToken, currentTime).Result)
        //        {
        //            PlaceTrailingOrder(option, activeCE.Value, lastPrice, currentTime, InstrumentType.CE, orderList);

        //            ActiveOptions.Remove(option);
        //            ActiveOptions.Add(activeCE.Value);
        //        }
        //        else if (option.InstrumentType.Trim(' ').ToLower() == "pe" && option.Strike < _baseInstrumentPrice - _maxDistanceFromBInstrument
        //            && CheckEMA(activePE.Value.InstrumentToken, e).Result)
        //        {
        //            PlaceTrailingOrder(option, activePE.Value, lastPrice, currentTime, InstrumentType.PE, orderList);
                    
        //            ActiveOptions.Remove(option);
        //            ActiveOptions.Add(activePE.Value);
        //        }
        //    }
        //}
        private void PlaceTrailingOrder(Instrument option, Instrument newInstrument, decimal lastPrice, 
            DateTime currentTime, InstrumentType instrumentType, OrderLinkedList orderList)
        {
            try
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                        "Trailing market...", "PlaceTrailingOrder");

                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType,
                    lastPrice, option.InstrumentToken, true,
                    _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Closed Option {0}. Bought {1} lots @ {2}.", option.TradingSymbol,
                        _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                Order slorder = orderList.FirstOrderNode.SLOrder;
                MarketOrders.CancelOrder(_algoInstance, algoIndex, slorder, currentTime);

                OnTradeExit(order);
                OnTradeExit(slorder);

                lastPrice = TimeCandles[newInstrument.InstrumentToken].Last().ClosePrice;
                order = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, lastPrice, //newInstrument.LastPrice,
                    newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                       string.Format("Traded Option {0}. Sold {1} lots @ {2}.", newInstrument.TradingSymbol,
                       _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

                CriticalLevels cl = new CriticalLevels();
                
                slorder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, order.AveragePrice * 5,
                   newInstrument.InstrumentToken, true, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
                   algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                OnTradeEntry(order);
                OnTradeEntry(slorder);

                orderList.Option = newInstrument;
                OrderLinkedListNode orderNode = new OrderLinkedListNode();
                orderNode.FirstLegCompleted = true;
                orderNode.Order = order;
                orderNode.SLOrder = slorder;
                orderList.FirstOrderNode = orderNode;
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

        }
        private async void TradeEntry(uint token, decimal lastPrice, DateTime currentTime)
        {
            try
            {
                Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);
                if (CheckEMA(token, currentTime, true).Result)
                {
                    decimal stopLoss = 0, targetProfit = 0;
                    GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);

                    int tradeQty = GetTradeQty(lastPrice - stopLoss, option.LotSize);
                    //ENTRY ORDER - BUY ALERT
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                        token, true, tradeQty * Convert.ToInt32(option.LotSize),
                        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    //    string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, option.TradingSymbol, order.AveragePrice), "TradeEntry");
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Entry {0} : {1}", option.TradingSymbol, order.AveragePrice), "TradeEntry");

                    //SL for first orders
                    //Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, stopLoss,
                    //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                    ////LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
                    ////    string.Format("Placed Stop Loss for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, slOrder.AveragePrice), "TradeEntry");

                    ////target profit
                    //Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, targetProfit,
                    //    token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value,
                    //    string.Format("Placed Target Profit for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, slOrder.AveragePrice), "TradeEntry");

                    OrderTrio orderTrio = new OrderTrio();
                    orderTrio.Option = option;
                    orderTrio.Order = order;
                    //orderTrio.SLOrder = slOrder;
                    //orderTrio.TPOrder = tpOrder;
                    //orderTrio.StopLoss = stopLoss;
                    //orderTrio.TargetProfit = targetProfit;

                    orderTrios.Add(orderTrio);

                    OnTradeEntry(order);
                    //OnTradeEntry(slOrder);
                    //OnTradeEntry(tpOrder);
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
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
        private async Task<bool> CheckEMA(uint token, DateTime currentTime, bool checkEntry)
        {
            try
            {
                decimal ema = lTokenEMA[token].GetCurrentValue<decimal>();
                decimal rsi = tokenRSI[token].GetCurrentValue<decimal>();

                decimal previousEma = lTokenEMA[token].GetValue<decimal>(1);
                decimal previousRsi = tokenRSI[token].GetValue<decimal>(1);

                if (checkEntry && rsi > ema && previousRsi <= previousEma)
                {
                        return true;
                }
                else if (!checkEntry && rsi < ema && previousRsi >= previousEma)
                {
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
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
                Thread.Sleep(100);
                //Environment.Exit(0);
                return false;
            }
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

                if (ActiveOptions.Count > 1)
                {
                    Instrument ce = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "ce");
                    Instrument pe = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

                    if (
                       ((pe.Strike >= _baseInstrumentPrice + _minDistanceFromBInstrument && pe.Strike <= _baseInstrumentPrice +_maxDistanceFromBInstrument)
                           || (orderTrios.Any( o=> o.Option.InstrumentToken == pe.InstrumentToken )))
                       && ((ce.Strike <= _baseInstrumentPrice - _minDistanceFromBInstrument && ce.Strike >= _baseInstrumentPrice -_maxDistanceFromBInstrument)
                       || (orderTrios.Any(o => o.Option.InstrumentToken == ce.InstrumentToken)))
                       )
                    {
                        return;
                    }

                }
                DataLogic dl = new DataLogic();
                Dictionary<uint, uint> mappedTokens;
                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice +_maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice -_minDistanceFromBInstrument 
                   || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out mappedTokens);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
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
                else
                {
                    if (orderTrios.Count > 0)
                    {
                        for (int i = 0; i < ActiveOptions.Count; i++)
                        {
                            Instrument option = ActiveOptions[i];
                            if (option.InstrumentType.Trim(' ').ToLower() == "ce" && orderTrios.Any(x => x.Option.InstrumentToken != option.InstrumentToken))
                            {
                                ActiveOptions[i] = activeCE.Value;
                            }
                            if (option.InstrumentType.Trim(' ').ToLower() == "pe" && orderTrios.Any(x => x.Option.InstrumentToken != option.InstrumentToken))
                            {
                                ActiveOptions[i] = activePE.Value;
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }


        private async void TradeEMAExit(uint token, DateTime currentTime, decimal lastPrice)
        {
            for (int j = 0; j < orderTrios.Count; j++)
            {
                OrderTrio orderTrio = orderTrios[j];
                Instrument option = orderTrio.Option;

                if (CheckEMA(token, currentTime, false).Result)
                {
                    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                       token, false, _tradeQty * Convert.ToInt32(option.LotSize),
                       algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                        string.Format("Exit {0} : {1}", option.TradingSymbol, order.AveragePrice), "TradeEntry");

                    OnTradeExit(order);

                    orderTrio.TPOrder = null;
                    orderTrio.SLOrder = null;
                    orderTrio.Option = null;
                    orderTrios.Remove(orderTrio);
                    j--;

                    ActiveOptions.Remove(option);
                }
            }
        }

        private async Task<bool> TradeExit(uint token, DateTime currentTime, decimal lastPrice)
        {
            try
            {
                for (int j = 0; j < orderTrios.Count; j++)
                {
                    OrderTrio orderTrio = orderTrios[j];
                    Instrument option = orderTrio.Option;

                    if (lastPrice <= orderTrio.StopLoss)
                    {
                        UpdateOrder(orderTrio.SLOrder, orderTrio.TPOrder, lastPrice, currentTime);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            string.Format("Stop Loss Triggered. Exited the trade @ {0}", orderTrio.SLOrder.AveragePrice), "TradeExit");

                        MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, orderTrio.SLOrder);

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Cancelled Target profit order", "TradeExit");

                        OnTradeExit(orderTrio.SLOrder);
                        OnTradeExit(orderTrio.TPOrder);
                        orderTrio.TPOrder = null;
                        orderTrio.SLOrder = null;
                        orderTrio.Option = null;
                        orderTrios.Remove(orderTrio);
                        j--;

                        ActiveOptions.Remove(option);
                    }
                    else if (lastPrice >= orderTrio.TargetProfit)
                    {
                        UpdateOrder(orderTrio.TPOrder, orderTrio.SLOrder, lastPrice, currentTime);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            string.Format("Target profit Triggered. Exited the trade @ {0}", orderTrio.SLOrder.AveragePrice), "TradeExit");

                        MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, orderTrio.TPOrder);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Cancelled Stop Loss order", "TradeExit");

                        OnTradeExit(orderTrio.SLOrder);
                        OnTradeExit(orderTrio.TPOrder);
                        orderTrio.TPOrder = null;
                        orderTrio.SLOrder = null;
                        orderTrio.Option = null;
                        orderTrios.Remove(orderTrio);
                        j--;

                        ActiveOptions.Remove(option);
                    }
                }
            }
            catch (Exception exp)
            {
                _stopTrade = true;
                Logger.LogWrite(exp.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
            return false;
        }
        private void UpdateOrder(Order completedOrder, Order orderTobeCancelled, decimal lastPrice, DateTime currentTime)
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
            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, 
            //    string.Format("Stop Loss Triggered. Exited the trade @ {0}", order.AveragePrice), "TradeExit");

            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, completedOrder);

            //OnTradeExit(order);
            //orderTrio.SLOrder = null;

            //cancel target profit order
            //order = orderTrio.TPOrder;
#if market
                            
            //Cancel the target profit limit order
            MarketOrders.CancelOrder(_algoInstance, algoIndex, orderTobeCancelled, currentTime);
#elif local
            orderTobeCancelled.AveragePrice = lastPrice;
            orderTobeCancelled.Price = lastPrice;
            orderTobeCancelled.ExchangeTimestamp = currentTime;
            orderTobeCancelled.OrderTimestamp = currentTime;
            orderTobeCancelled.Tag = "Test";
            orderTobeCancelled.Status = Constants.ORDER_STATUS_CANCELLED;
#endif
            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Cancelled Target profit order", "TradeExit");

            //MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);

            //OnTradeExit(order);
            //orderTrio.TPOrder = null;
        }
        private bool CheckForExit(uint token, DateTime currentTime)
        {
            try
            {
                decimal ema = lTokenEMA[token].GetCurrentValue<decimal>();
                decimal rsi = tokenRSI[token].GetCurrentValue<decimal>();

                if (tokenRSI[token].IsFormed && lTokenEMA[token].IsFormed && rsi > ema && rsi >= _rsiUpperLimit)
                {
                    return true;
                }
                else
                {
                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                    //    String.Format("RSI({0}): {0}. EMA({2}) on RSI @ {3}", tokenRSI[token].IsFormed, Decimal.Round(rsi, 2), lTokenEMA[token].IsFormed, Decimal.Round(ema, 2)), "TradeExit");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
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

        private void GetCriticalLevels(decimal lastPrice, out decimal stopLoss, out decimal targetProfit)
        {
            stopLoss = lastPrice - _targetProfit;
            targetProfit = lastPrice + _targetProfit;
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
                Environment.Exit(0);
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

                        lema = new ExponentialMovingAverage(LONG_EMA);
                        for (int i = LONG_EMA - 1; i >= 0; i--)
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
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
            }
        }
        public void StopTrade(bool stop)
        {
            //_stopTrade = stop;
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
        private async Task<Order> ModifyOrder(Order slOrder, decimal sl, DateTime currentTime)
        {
            try
            {
                // uint instrumentToken = tokenOrderLevel.Key;
                //Order slOrder = tokenOrderLevel.Value.SLOrder;
                //CriticalLevels updatedCLsForSecondLeg = tokenOrderLevel.Value.Levels;

                //string tradingSymbol = slOrder.Tradingsymbol;

                //decimal sl = updatedCLsForSecondLeg.StopLossPrice;

                Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, sl, slOrder, currentTime);

                OnTradeEntry(order);
                return order;

            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ModifyOrder");
                Thread.Sleep(100);
                Environment.Exit(0);
                return null;
            }

        }

    }
}
