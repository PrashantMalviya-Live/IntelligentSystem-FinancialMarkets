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
    public class OptionVolumeRateEMAThreshold : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[]  OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionVolumeRateEMAThreshold source);
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

        public OrderLinkedList orderList;

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
        
        public const decimal PRICE_PERCENT_INCREASE = 0.005m;
        public const decimal CANDLE_BULLISH_BODY_FRACTION = 0.55m;
        public const decimal CANDLE_BULLISH_LOWERWICK_FRACTION = 0.5m;
        public const decimal CANDLE_BULLISH_BODY_PRICE_FRACTION = 0.04m;
        public const decimal TRIGGER_EMA_ENTRY = 0.6m;

        public const decimal VOLUME_PERCENT_INCREASE = 1.0m;
        public const long VOLUME_INCREASE_RATE = 2; //1/10 of the candle time
        public const decimal INDEX_PERCENT_CHANGE = 0.001m;
        public const decimal CPR_DISTANCE = 0.00005m;
        public const decimal TARGET_PROFIT = 0.005m;
        public const decimal STOPLOSS_PRICE = 0.0005m;
        public const decimal STOPLOSS_PRICE_OPTION = 0.20m;
        public const decimal STOPLOSS_PRICE_FRACTION = 0.75m;

        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int TRADE_QTY = 4;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        
        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        public const int RSI_THRESHOLD = 60;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.MomentumTrade_Option;
        //TimeSpan candletimeframe;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public OptionVolumeRateEMAThreshold(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, int algoInstance = 0,
            bool positionSizing = false, decimal maxLossPerTrade = 0)
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            
            _stopTrade = false;

            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            //tokenTradeLevels = new Dictionary<uint, OrderLevels>();
            _pastOrders = new List<Order>();
            orderList = new OrderLinkedList();

            SubscriptionTokens = new List<uint>();

            ActiveOptions = new List<Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();

            //EMAs
            lTokenEMA = new Dictionary<uint, ExponentialMovingAverage>();
            tokenRSI = new Dictionary<uint, RelativeStrengthIndex>();

            _EMALoaded = new List<uint>();
            _SQLLoading = new List<uint>();
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            CandleSeries candleSeries = new CandleSeries();

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            ExponentialMovingAverage sema = null, lema = null;
            RelativeStrengthIndex rsi = null;

            TRADE_QTY = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;


            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

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


        public void LoadActiveOrders (Order activeOrder)
        {
            OrderLinkedListNode orderNode = new OrderLinkedListNode();
            orderNode.SLOrder = activeOrder;
            orderNode.FirstLegCompleted = true;
            orderList.FirstOrderNode = orderNode;

            DataLogic dl = new DataLogic();
            orderList.Option = dl.GetInstrument(activeOrder.InstrumentToken);

            ActiveOptions.Add(orderList.Option);
        }
        private async void ActiveTradeIntraday(Tick tick)
        {
            ///Steps:
            /// 1. Look at the first candle on both CE and PE. Which ever is positive, then take next trade on that side
            /// SL: Wait for 75% value loss compared to previous candle
            /// SL for subsequent candles will be previous candle body low/high
            /// 2.0 Try to fit in RSI to determine when to enter
            /// 3.0 try the trade with money candle and volume candle

            /// New changes 11 September 2020:
            ///  1) No trade entry at the middle of a candle
            ///  2) Reenter after stoploss if candle closes positive

            ///Next set of changes
            ///Introduce Volume, EMA, RSI to increase probability
            ///Check with trade quantity change to increase profitablity based on high probability
            ///Check for level to trade contra, if levels of last 1 hr is not breached then trade side ways

            ///Changed on 14th Sept 2020
            ///13 EMA

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
                        MonitorCandles(tick);

                        if (!_EMALoaded.Contains(token))
                        {
                            //this method can now be slow doenst matter. 
                            //make it async
                            LoadHistoricalEMAs(tick.LastTradeTime.Value);
                        }
                        
                        if (ActiveOptions.Any(x => x.InstrumentToken == token))
                        {
                            //TradeExit(tick);
                            CheckEntry(tick);
                            CheckExit(tick);
                        }

                        //Closes all postions at 3:29 PM
                        TriggerEODPositionClose(tick.LastTradeTime);
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
        private void TriggerEODPositionClose(DateTime? currentTime)
        {
            if (currentTime.GetValueOrDefault(DateTime.Now).TimeOfDay >= new TimeSpan(15, 29, 00))
            {
                //foreach (uint token in tokenTradeLevels.Keys)
                //{
                //    Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);

                //    decimal lastPrice = TimeCandles[token].Last().ClosePrice;
                //    //exit trade
                //    Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, 
                //        option.InstrumentType, lastPrice, token, false,
                //        TRADE_QTY * Convert.ToInt32(option.LotSize), algoIndex, currentTime);
                //    //shortTrade.TradingStatus = TradeStatus.Closed;
                //}
                //tokenTradeLevels.Clear();

                OrderLinkedListNode orderNode = orderList.FirstOrderNode;
                
                if(orderNode != null)
                    while(orderNode != null)
                    {
                        Order slOrder = orderNode.SLOrder;
                        if (slOrder != null)
                        {
                            MarketOrders.ModifyOrder(_algoInstance, algoIndex, 0, slOrder, currentTime.Value);
                            slOrder = null;
                        }

                        orderNode = orderNode.NextOrderNode;
                    }

                Environment.Exit(0);
            }
        }
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
                Environment.Exit(0);
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
                    Task task = Task.Run(() => LoadHistoricalCandles(tokenList, Math.Max(LONG_EMA, RSI_LENGTH), lastCandleEndTime));
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
                            lTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            tokenRSI[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                lTokenEMA[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
                                tokenRSI[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
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
            catch(Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalEMAs");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
        }

        private async void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            try
            {
                if (_EMALoaded.Contains(e.InstrumentToken) && !_stopTrade)
                {
                    if(!lTokenEMA.ContainsKey(e.InstrumentToken))
                    {
                        return;
                    }
                    lTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
                    tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);

                    if (ActiveOptions.Any(x => x.InstrumentToken == e.InstrumentToken))
                    {
                        decimal ema = lTokenEMA[e.InstrumentToken].GetCurrentValue<decimal>();
                        decimal rsi = tokenRSI[e.InstrumentToken].GetCurrentValue<decimal>();

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime, 
                            String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. EMA:{5}. RSI:{6}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
                            , ActiveOptions.Find(x=>x.InstrumentToken == e.InstrumentToken).TradingSymbol, Decimal.Round(ema, 2), Decimal.Round(rsi, 2)), "CandleManger_TimeCandleFinished");
                        
                        //StringBuilder sb = new StringBuilder();
                        //int c = TimeCandles[e.InstrumentToken].Count;
                        //for (int j = c - 1; j < c - 14; j--)
                        //{
                        //    sb.Append(TimeCandles[e.InstrumentToken][j].ClosePrice);
                        //    sb.Append(",");
                        //}
                        //sb.Remove(sb.Length - 1, 1);
                        //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                        //   String.Format("Closing prices of last 13 Candles: {0}", sb.ToString()), "CandleManger_TimeCandleFinished");

                        //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                        //    String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. EMA:{5}", e.OpenPrice, e.HighPrice, e.HighPrice, 
                        //e.ClosePrice, e.InstrumentToken, Decimal.Round(ema, 2)), "CandleManger_TimeCandleFinished");

                        if (GetCandleFormation(e) == CandleFormation.Bullish)
                        {
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime, 
                                String.Format("Bullish Candle: {0}", ActiveOptions.Find(x => x.InstrumentToken == e.InstrumentToken).TradingSymbol), "CandleManger_TimeCandleFinished");

                            //foreach (var st in tokenTradeLevels)
                            //{
                            //    if (st.Key == e.InstrumentToken)
                            //    {
                            //        st.Value.Levels = GetCriticalLevels(e, STOPLOSS_PRICE_FRACTION);
                            //        ModifyOrder(st, e.CloseTime);
                            //    }
                            //}
                            OrderLinkedListNode orderNode = orderList.FirstOrderNode;
                            if(orderNode != null)
                            {
                                while(orderNode != null)
                                {
                                    if(orderNode.FirstLegCompleted && orderNode.SLOrder != null)
                                    {
                                        CriticalLevels cl = GetCriticalLevels(e, STOPLOSS_PRICE_FRACTION);
                                        ModifyOrder(orderNode.SLOrder, cl.StopLossPrice, e.CloseTime);
                                    }
                                    orderNode = orderNode.NextOrderNode;
                                }

                            }

                            TradeEntry(e.InstrumentToken, e.CloseTime, e.ClosePrice);
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
                Environment.Exit(0);
            }
        }
        private async void TradeEntry(uint token, DateTime currentTime, decimal lastPrice)
        {
            try
            {
                Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);

                Candle previousCandle = TimeCandles[token].LastOrDefault(x => x.State == CandleStates.Finished);
                Candle currentCandle = TimeCandles[token].Last();
                ///TODO: Both previouscandle and currentcandle would be same now, as trade is getting generated at candle close only. _pastorders check below is not needed anymore.
                if (previousCandle != null
                    && option != null
                    //&& !(tokenTradeLevels.Count() > 0)
                    && orderList.FirstOrderNode == null
                    && lastPrice > previousCandle.OpenPrice
                    && previousCandle.OpenTime.Date == currentTime.Date
                    && !_pastOrders.Any(x => x.OrderTimestamp >= currentCandle.OpenTime)
                    && (CheckEMA(token, previousCandle).Result)
                    && !_stopTrade
                    )
                {
                    CriticalLevels cl = GetCriticalLevels(previousCandle);

                    int tradeQty = GetTradeQty(lastPrice - cl.StopLossPrice, option.LotSize);
                    if (tradeQty > 0)
                    {
                        //ENTRY ORDER - BUY ALERT
                        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                            token, true, tradeQty * Convert.ToInt32(option.LotSize),
                            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value, string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, option.TradingSymbol, order.AveragePrice), "TradeEntry");

                       

                        //SL for first orders
                        Order slOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, cl.StopLossPrice,
                            token, false, tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                        //tokenTradeLevels.Add(token, new OrderLevels { FirstLegOrder = order, SLOrder = slOrder, Levels = cl });
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder.OrderTimestamp.Value, string.Format("TRADE!! Placed Stop Loss for {0} lots of {1} @ {2}", tradeQty, option.TradingSymbol, slOrder.AveragePrice), "TradeEntry");

                        OrderLinkedListNode orderNode = new OrderLinkedListNode();
                        orderNode.Order = order;
                        orderNode.SLOrder = slOrder;
                        orderNode.FirstLegCompleted = true;

                        OnTradeEntry(order);
                        OnTradeEntry(slOrder);

                        int order2Qty = 0;
                        Order order2 = null;
                        Order slOrder2 = null;
                        if (_positionSizing)
                        {
                            //Another ENTRY ORDER @ 30% below the top of last candle
                            decimal order2Price = 0;
                            GetOrder2PriceQty(tradeQty, order.AveragePrice - cl.StopLossPrice, previousCandle, option.LotSize, out order2Qty, out order2Price);

                            if (order2Qty > 0 && order2Price > cl.StopLossPrice)
                            {
                                order2 = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, order2Price,
                                    token, true, order2Qty * Convert.ToInt32(option.LotSize),
                                    algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order2.OrderTimestamp.Value, string.Format("TRADE!! Placed Limit Order for {0} lots of {1} @ {2}", order2Qty, option.TradingSymbol, order2.AveragePrice), "TradeEntry");


                                //SL for second orders
                                slOrder2 = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, cl.StopLossPrice,
                                    token, false, order2Qty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

                                //tokenTradeLevels.Add(token, new OrderLevels { FirstLegOrder = order, SLOrder = slOrder, Levels = cl });
                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, slOrder2.OrderTimestamp.Value, string.Format("TRADE!! Placed Stop Loss for {0} lots of {1} @ {2}", order2Qty, option.TradingSymbol, slOrder2.AveragePrice), "TradeEntry");


                                OrderLinkedListNode orderNode2 = new OrderLinkedListNode();
                                orderNode2.Order = order2;
                                orderNode2.SLOrder = slOrder2;
                                orderNode2.FirstLegCompleted = false;
                                orderNode2.PrevOrderNode = orderNode;
                                orderNode.NextOrderNode = orderNode2;

                                OnTradeEntry(order2);
                                OnTradeEntry(slOrder2);
                            }
                        }

                        //orderList = new OrderLinkedList();
                        orderList.Option = option;
                        orderList.FirstOrderNode = orderNode;
                        _pastOrders.Add(order);
                    }
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
                Environment.Exit(0);
            }
        }
        private int GetTradeQty(decimal maxlossInPoints, uint lotSize)
        {
            decimal tradeqty = _maxLossPerTrade / maxlossInPoints * 1.3m/2m;
            return _positionSizing ?  Convert.ToInt32(Math.Ceiling(tradeqty / lotSize)): TRADE_QTY;
        }
        private void GetOrder2PriceQty (int firstLegQty, decimal firstMaxLoss, Candle previousCandle, uint lotSize,  out int qty, out decimal price)
        {
            decimal buffer = _maxLossPerTrade - firstLegQty * lotSize * firstMaxLoss;

            decimal candleSize = previousCandle.ClosePrice - previousCandle.OpenPrice;

            price = previousCandle.ClosePrice - (candleSize * 0.2m);
            price = Math.Round(price * 20) / 20;

            qty = Convert.ToInt32(Math.Ceiling((buffer / price)/lotSize));
        }
        public void StopTrade()
        {
            _stopTrade = true;
        }
        private async Task<bool> CheckEMA(uint token, Candle previousCandle)
        {
            try
            {
                decimal ema = lTokenEMA[token].GetCurrentValue<decimal>();
                decimal rsi = tokenRSI[token].GetCurrentValue<decimal>();
                if ((lTokenEMA[token].IsFormed && ema < (previousCandle.ClosePrice - (previousCandle.ClosePrice - previousCandle.OpenPrice) * TRIGGER_EMA_ENTRY))
                    && (rsi > RSI_THRESHOLD && tokenRSI[token].IsFormed))
                {
                    return true;
                }
                else
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, previousCandle.CloseTime, String.Format("13 EMA Formed: {0}.\r\n Candle Body {1}% above 13 EMA at {2}, RSI @ {3}", lTokenEMA[token].IsFormed,
                     string.Format("{0:N2}", (previousCandle.ClosePrice - ema) * 100 / (previousCandle.ClosePrice - previousCandle.OpenPrice)), Decimal.Round(ema, 2), Decimal.Round(rsi, 2)), "TradeEntry");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, previousCandle.CloseTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
                Thread.Sleep(100);
                Environment.Exit(0);
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
                int _strikePriceIncrement = 100;
                var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
                var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

                if (ActiveOptions.Count > 1)
                {
                    Instrument ce = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "ce");
                    Instrument pe = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

                    if (
                       ((ce.Strike <= _baseInstrumentPrice && ce.Strike >= _baseInstrumentPrice - _strikePriceIncrement * 2)
                           || (orderList.Option != null && orderList.Option.InstrumentToken == ce.InstrumentToken))
                       && ((pe.Strike >= _baseInstrumentPrice && pe.Strike <= _baseInstrumentPrice + _strikePriceIncrement * 2)
                           || (orderList.Option != null && orderList.Option.InstrumentToken == pe.InstrumentToken))
                       )
                    {
                        return;
                    }

                }
                DataLogic dl = new DataLogic();

                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - _strikePriceIncrement * 2 || OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice - _strikePriceIncrement * 0
                   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice + _strikePriceIncrement * 0 || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice + _strikePriceIncrement * 2))
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _strikePriceIncrement * 2);

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

                //TODO: Check for exception if less than 2 items are available.
                //var activeCEs = OptionUniverse[(int)InstrumentType.CE].Where(x => x.Key > _baseInstrumentPrice).Take(1);
                //var activePEs = OptionUniverse[(int)InstrumentType.PE].Where(x => x.Key < _baseInstrumentPrice).Reverse().Take(1);

                var activeCE = OptionUniverse[(int)InstrumentType.CE].LastOrDefault(x => x.Key <= _baseInstrumentPrice);
                var activePE = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice);


                //TODO: Check for exception if less than 2 items are available.
                //var candleCEs = OptionUniverse[(int)InstrumentType.CE].Where(x => x.Key <= _baseInstrumentPrice).Take(2);
                //var candlePEs = OptionUniverse[(int)InstrumentType.PE].Where(x => x.Key >= _baseInstrumentPrice).Reverse().Take(2);
                //CandleOptions.Clear();
                //CandleOptions.AddRange(candleCEs.Select(x => x.Value));
                //CandleOptions.AddRange(candlePEs.Select(x => x.Value));

                //foreach (var tt in tokenTradeLevels)
                //{
                //    if (!CandleOptions.Any(x => x.InstrumentToken == tt.Key))
                //    {
                //        Instrument i;
                //        if (OptionUniverse[(int)InstrumentType.CE].Any(x => x.Value.InstrumentToken == tt.Key))
                //        {
                //            i = OptionUniverse[(int)InstrumentType.CE].FirstOrDefault(x => x.Value.InstrumentToken == tt.Key).Value;
                //        }
                //        else
                //        {
                //            i = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Value.InstrumentToken == tt.Key).Value;
                //        }

                //        CandleOptions.Add(i);
                //    }
                //}




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
                    for (int i = 0; i < ActiveOptions.Count; i++)
                    {
                        Instrument option = ActiveOptions[i];
                        if (orderList.Option != null && orderList.Option.InstrumentToken != option.InstrumentToken)
                        {
                            ActiveOptions.Remove(option);
                            option = option.InstrumentType.Trim(' ').ToLower() == "ce" ? activeCE.Value : activePE.Value;
                            ActiveOptions.Insert(i, option);

                            ///TODO: after updating this value, check if ActiveOptions gets update or not.
                        }
                    }
                }


                //ActiveOptions.Clear();
                //foreach(TradeLevels st in tokenTradeLevels.Values)
                //{
                //    //if(!ActiveOptions.Any(x=>x.InstrumentToken == st.Trade.InstrumentToken))
                //    //{
                //        if(st.Trade.InstrumentType.Trim(' ').ToLower() == "ce")
                //        {
                //            ActiveOptions.Add(OptionUniverse[(int)InstrumentType.CE].Values.First(x=>x.InstrumentToken == st.Trade.InstrumentToken));
                //        }
                //        else
                //        {
                //            ActiveOptions.Add(OptionUniverse[(int)InstrumentType.PE].Values.First(x => x.InstrumentToken == st.Trade.InstrumentToken));
                //        }

                //    //}
                //}

                //if(!ActiveOptions.Any(x=>x.InstrumentType.Trim(' ').ToLower() == "ce"))
                //ActiveOptions.AddRange(activeCEs.Select(x => x.Value));
                //if (!ActiveOptions.Any(x => x.InstrumentType.Trim(' ').ToLower() == "pe"))
                //    ActiveOptions.AddRange(activePEs.Select(x => x.Value));
                //}
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
        }
        //        private async void TradeExit(Tick tick)
        //        {
        //            try
        //            {
        //                uint token = 0;
        //                token = tick.InstrumentToken;
        //                if (/*tokenTradeLevels.ContainsKey(token) &&*/ !_stopTrade && orderList.Option != null && orderList.Option.InstrumentToken == token)
        //                {
        //                    Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);

        //                    var tt = tokenTradeLevels[token];

        //                    if (tick.LastPrice < tt.Levels.StopLossPrice)
        //                    {
        //#if market
        //                        //DONOT PUT NEWEXIT ORDER . CHECK FOR SL HIT IN ZERODHA
        //                        //Check if sl order to executed
        //                        Order order = MarketOrders.GetOrder(tt.SLOrder.OrderId, _algoInstance, algoIndex).Result;
        //#elif local

        //                        Order order = tt.SLOrder;
        //                        order.AveragePrice = tick.LastPrice;
        //                        order.Price = tick.LastPrice;
        //                        order.OrderType = Constants.ORDER_TYPE_MARKET;
        //                        order.ExchangeTimestamp = tick.Timestamp;
        //                        order.OrderTimestamp = tick.LastTradeTime;
        //                        order.Tag = "Test";
        //                        order.Status = "Complete";
        //#endif
        //                        tokenTradeLevels.Remove(token);
        //                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tick.LastTradeTime.Value, string.Format("Exited the trade @ {0}", order.AveragePrice), "TradeExit");

        //                        MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);

        //                        OnTradeExit(order);
        //                    }
        //                    //else if (tt.Trade.TransactionType.ToLower() == "sell" && tick.LastPrice > tt.Levels.StopLossPrice)
        //                    //{
        //                    //    //exit trade
        //                    //    ShortTrade shortTrade = PlaceOrder(option.TradingSymbol, option.InstrumentType, tick.LastPrice, token, true,
        //                    //        TRADE_QTY * Convert.ToInt32(option.LotSize), tick.Timestamp);
        //                    //    shortTrade.TradingStatus = TradeStatus.Closed;

        //                    //    tokenTradeLevels.Remove(token);

        //                    //    OnTradeExit(shortTrade);
        //                    //}
        //                }
        //            }
        //            catch (Exception exp)
        //            {
        //                _stopTrade = true;
        //                Logger.LogWrite(exp.StackTrace);
        //                Logger.LogWrite("Closing Application");
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.Value, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
        //                Thread.Sleep(100);
        //                Environment.Exit(0);
        //            }
        //        }

        private async void CheckEntry(Tick tick)
        {
            try
            {
                uint token = 0;
                token = tick.InstrumentToken;
                if (!_stopTrade && orderList.Option != null && orderList.Option.InstrumentToken == token)
                {
                    Instrument option = orderList.Option;

                    OrderLinkedListNode orderNode = orderList.FirstOrderNode;
                    while (orderNode != null)
                    {
                        if (!orderNode.FirstLegCompleted && tick.LastPrice < orderNode.Order.AveragePrice)
                        {
                            Order order = orderNode.Order;
#if market
                            
                            //DONOT PUT NEWEXIT ORDER . CHECK FOR LIMIT HIT IN ZERODHA
                            //Check if sl order to executed
                            order = MarketOrders.GetOrder(order.OrderId, _algoInstance, algoIndex, Constants.ORDER_STATUS_COMPLETE).Result;
#elif local

                            order.AveragePrice = tick.LastPrice;
                            order.Price = tick.LastPrice;
                            order.OrderType = Constants.ORDER_TYPE_LIMIT;
                            order.ExchangeTimestamp = tick.Timestamp;
                            order.OrderTimestamp = tick.LastTradeTime;
                            order.Tag = "Test";
                            order.Status = Constants.ORDER_STATUS_COMPLETE;
#endif
                            if (order.Status == Constants.ORDER_STATUS_COMPLETE)
                            {
                                orderNode.FirstLegCompleted = true;
                                orderNode.Order = order;
                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tick.LastTradeTime.Value,
                                    string.Format("Trade!! Bought {0} lots of {1} @ {2}.", order.Quantity, option.TradingSymbol, order.AveragePrice), "CheckEntry");

                                MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);

                                OnTradeEntry(order);
                            }
                        }
                        orderNode = orderNode.NextOrderNode;
                    }
                }
            }
            catch (Exception exp)
            {
                _stopTrade = true;
                Logger.LogWrite(exp.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.Value, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }


        private async void CheckExit(Tick tick)
        {
            try
            {
                uint token = 0;
                token = tick.InstrumentToken;
                if (!_stopTrade && orderList.Option != null && orderList.Option.InstrumentToken == token)
                {
                    Instrument option = orderList.Option;

                    OrderLinkedListNode orderNode = orderList.FirstOrderNode;
                    while (orderNode != null)
                    {
                        if (orderNode.FirstLegCompleted && orderNode.SLOrder != null && tick.LastPrice < orderNode.SLOrder.TriggerPrice)
                        {
                            Order order = orderNode.SLOrder;
#if market
                            
                            //DONOT PUT NEWEXIT ORDER . CHECK FOR SL HIT IN ZERODHA
                            //Check if sl order to executed
                            order = MarketOrders.GetOrder(order.OrderId, _algoInstance, algoIndex, Constants.ORDER_STATUS_COMPLETE).Result;
#elif local

                            order.AveragePrice = tick.LastPrice;
                            order.Price = tick.LastPrice;
                            order.OrderType = Constants.ORDER_TYPE_MARKET;
                            order.ExchangeTimestamp = tick.Timestamp;
                            order.OrderTimestamp = tick.LastTradeTime;
                            order.Tag = "Test";
                            order.Status = Constants.ORDER_STATUS_COMPLETE;
#endif
                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tick.LastTradeTime.Value, string.Format("Exited the trade @ {0}", order.AveragePrice), "TradeExit");

                            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);

                            OnTradeExit(order);
                            orderNode.SLOrder = null;
                            //RemoveSLOrder(orderNode.SLOrder);
                        }
                        orderNode = orderNode.NextOrderNode;
                    }
                    orderNode = orderList.FirstOrderNode;
                    bool slOrderExists = false;
                    while (orderNode != null)
                    {
                        if(orderNode.FirstLegCompleted && orderNode.SLOrder != null)
                        {
                            slOrderExists = true;
                            break;
                        }
                        orderNode = orderNode.NextOrderNode;
                    }
                    if(!slOrderExists)
                    {
                        orderNode = orderList.FirstOrderNode;

                        //Cancel all non executed orders
                        while (orderNode != null)
                        {
                            if (!orderNode.FirstLegCompleted)
                            {
                                MarketOrders.CancelOrder(AlgoInstance, algoIndex, orderNode.Order, tick.LastTradeTime.Value);
                                MarketOrders.CancelOrder(AlgoInstance, algoIndex, orderNode.SLOrder, tick.LastTradeTime.Value);
                            }
                            orderNode = orderNode.NextOrderNode;
                        }

                        orderList.FirstOrderNode = null;
                        orderList.Option = null;
                    }
                }
            }
            catch (Exception exp)
            {
                _stopTrade = true;
                Logger.LogWrite(exp.StackTrace);
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.Value, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", exp.Message), "TradeExit");
                Thread.Sleep(100);
                Environment.Exit(0);
            }
        }
        private void RemoveSLOrder(Order slOrder)
        {
            OrderLinkedListNode orderNode = orderList.FirstOrderNode;
            string orderId = slOrder.OrderId;
            while (orderNode != null)
            {
                if( orderNode.SLOrder != null && orderNode.SLOrder.OrderId == orderId)
                {
                    orderNode.SLOrder = null;
                }
                orderNode = orderNode.NextOrderNode;
            }
        }

        private CriticalLevels GetCriticalLevels(Candle previousCandle, decimal stopLossCandleFraction = -1)
        {
            CriticalLevels cl = new CriticalLevels();
            cl.PreviousCandle = previousCandle;
            
            decimal slPrice = stopLossCandleFraction == -1 ? previousCandle.LowPrice 
                : previousCandle.ClosePrice - (previousCandle.ClosePrice - previousCandle.OpenPrice) * stopLossCandleFraction;

            cl.StopLossPrice = Math.Round(slPrice * 20) / 20;

            return cl;
        }

        private CandleFormation GetCandleFormation(Candle candle)
        {
            CandleFormation cf = CandleFormation.Indecisive;

            decimal candleBody = candle.ClosePrice - candle.OpenPrice;
            decimal candleLowerWick = candle.OpenPrice - candle.LowPrice;
            decimal candleUpperWick = candle.HighPrice - candle.ClosePrice;
            decimal candleSize = candle.HighPrice - candle.LowPrice;

            if (candleBody > 0 && (candleBody >= CANDLE_BULLISH_BODY_FRACTION * candleSize ||
                candleLowerWick >= CANDLE_BULLISH_LOWERWICK_FRACTION * candleSize) 
                && candleBody > CANDLE_BULLISH_BODY_PRICE_FRACTION * candle.ClosePrice
                )
            {
                cf = CandleFormation.Bullish;
            }
            else if (candleBody < 0 && candleBody >= CANDLE_BULLISH_BODY_FRACTION * candleSize)
            {
                cf = CandleFormation.Bearish;
            }

            return cf;
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
                        lema = new ExponentialMovingAverage(LONG_EMA);
                        foreach (var price in historicalCandlePrices[t])
                        {
                            lema.Process(price, isFinal: true);
                        }
                        lTokenEMA.Add(t, lema);

                        rsi = new RelativeStrengthIndex();
                        foreach (var price in historicalCandlePrices[t])
                        {
                            rsi.Process(price, isFinal: true);
                        }
                        tokenRSI.Add(t, rsi);

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
                Environment.Exit(0);
            }

        }
        #endregion

        public int AlgoInstance { get
            { return _algoInstance; }}
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, ticks[0].Timestamp.GetValueOrDefault(DateTime.UtcNow) , String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                Environment.Exit(0);
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

                Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, sl, slOrder, currentTime).Result;

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
