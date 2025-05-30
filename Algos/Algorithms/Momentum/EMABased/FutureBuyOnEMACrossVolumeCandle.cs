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

namespace Algorithms.Algorithms
{
    public class FutureBuyonEMACrossVolumeCandle : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(FutureBuyonEMACrossVolumeCandle source);
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

        private OrderTrio _callOrderTrio;
        private OrderTrio _putOrderTrio;
        private OrderTrio _orderTrio;
        public List<Order> _pastOrders;
        private bool _stopTrade;
        private Instrument _activeFuture;
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
        uint _volumeThrehold;
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
        Dictionary<uint, List<Candle>> VolumeCandles;

        public readonly decimal _emaBandForExit;
        public readonly decimal _rsiBandForExit;
        public readonly double _timeBandForExit;
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        private enum Trade
        {
            Buy = 1,
            Sell = 2,
            DoNotTrade = 3
        }
        public FutureBuyonEMACrossVolumeCandle(DateTime endTime, TimeSpan candleTimeSpan, uint volumeThreshold, uint baseInstrumentToken,
            DateTime? expiry, int quantity, decimal targetProfit, decimal stopLoss, int sEMALength,
            int lEMALength, int signalEMALength = 25, int algoInstance = 0, bool positionSizing = false, decimal maxLossPerTrade = 0)
        {
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _volumeThrehold = volumeThreshold;
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
            VolumeCandles = new Dictionary<uint, List<Candle>>();

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

            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            candleManger = new CandleManger(VolumeCandles, CandleType.Volume);
            candleManger.VolumeCandleFinished += CandleManger_VolumeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, Arg1: _sEMALength, Arg2: lEMALength, 
                Arg3: _targetProfit, Arg4: _stopLoss, Arg5:_signalEMALength);

            ////ZConnect.Login();

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
                lock (VolumeCandles)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    //LoadOptionsToTrade(currentTime);
                    LoadFuturesToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    MonitorVolumeCandles(tick, currentTime);

                    if (token == _baseInstrumentToken)
                    {
                        //if (!_bEMALoaded)
                        //{
                        //    LoadBInstrumentEMA(token, BASE_EMA_LENGTH, currentTime);
                        //}
                        //else
                        //{
                        //    _bEMAValue = _bEMA.Process(tick.LastPrice, isFinal: false);
                        //}
                    }
                    else if (tick.LastTradeTime.HasValue)
                    {
                        if (!_EMALoaded.Contains(token))
                        {
                            LoadHistoricalEMAs(currentTime);
                        }
                        if (!sTokenEMA.ContainsKey(token) || !lTokenEMA.ContainsKey(token) || ! signalTokenEMA.ContainsKey(token))
                        {
                            return;
                        }

                        IIndicatorValue sema = sTokenEMA[token].Process(tick.LastPrice, isFinal: false);
                        IIndicatorValue lema = lTokenEMA[token].Process(tick.LastPrice, isFinal: false);
                        IIndicatorValue signalEMA = signalTokenEMA[token].Process(tick.LastPrice, isFinal: false);

                        if (stokenEMAIndicator.ContainsKey(token))
                        {
                            stokenEMAIndicator[token] = sema;
                        }
                        else
                        {
                            stokenEMAIndicator.Add(token, sema);
                        }
                        if (ltokenEMAIndicator.ContainsKey(token))
                        {
                            ltokenEMAIndicator[token] = lema;
                        }
                        else
                        {
                            ltokenEMAIndicator.Add(token, lema);
                        }
                        
                        if (signalEMAIndicator.ContainsKey(token))
                        {
                            signalEMAIndicator[token] = signalEMA;
                        }
                        else
                        {
                            signalEMAIndicator.Add(token, signalEMA);
                        }
                        if (!_belowEMA.ContainsKey(token))
                        {
                            _belowEMA.Add(token, false);
                        }

                        if (sema.IsEmpty || lema.IsEmpty || !sema.IsFormed || !lema.IsFormed || signalEMA.IsEmpty || !signalEMA.IsFormed)
                        {
                            return;
                        }
                        if (_activeFuture != null && _activeFuture.InstrumentToken == token)
                        {
                            TradeEntry(token, tick.LastPrice, currentTime, sema, lema, _bEMAValue, signalEMA);
                        }

                        //if (ActiveOptions.Any(x => x.InstrumentToken == token))
                        //{
                        //    Instrument option = ActiveOptions.Find(x => x.InstrumentToken == token);

                        //    bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
                        //    OrderTrio orderTrio = isOptionCall ? _callOrderTrio : _putOrderTrio;

                        //    if (orderTrio == null)
                        //    {
                        //        orderTrio = TradeEntry(token, tick.LastPrice, currentTime, sema, lema, _bEMAValue, signalEMA, isOptionCall);
                        //    }
                        //    else
                        //    {
                        //        bool orderExited = TradeExit(token, currentTime, tick.LastPrice, orderTrio, sema, lema);

                        //        if (orderExited)
                        //        {
                        //            orderTrio = null;
                        //        }
                        //        else
                        //        {
                        //           // orderTrio = TrailMarket(token, isOptionCall, currentTime, tick.LastPrice, _bEMAValue, orderTrio);
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

                        //    //Put a hedge at 3:15 PM
                        //    //TriggerEODPositionClose(tick.LastTradeTime);
                        //}

                        //Put a hedge at 3:15 PM
                        // TriggerEODPositionClose(tick.LastTradeTime);
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
        private void MonitorVolumeCandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //VolumeCandles with everycall, as the list doesnt return new candles unless built

                candleManger.StreamingVolumeCandle(tick, token, _volumeThrehold);

                //if (VolumeCandles.ContainsKey(token))
                //{
                //    candleManger.StreamingVolumeCandle(tick, token, _volumeThrehold); // TODO: USING LOCAL VERSION RIGHT NOW
                //}
                //else
                //{
                //    candleManger.StreamingVolumeCandle(tick, token, _volumeThrehold); // TODO: USING LOCAL VERSION RIGHT NOW
                //}
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


                    if (VolumeCandles.ContainsKey(bToken) && _bEMALoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _bEMA.Process(VolumeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (VolumeCandles[bToken].Count > 1)
                        {
                            foreach (var price in VolumeCandles[bToken])
                            {
                                _bEMA.Process(VolumeCandles[bToken].First().ClosePrice, isFinal: true);
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
                    if (VolumeCandles.ContainsKey(tkn) && lTokenEMA.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            sTokenEMA[tkn].Process(VolumeCandles[tkn].First().OpenPrice, isFinal: true);
                            lTokenEMA[tkn].Process(VolumeCandles[tkn].First().OpenPrice, isFinal: true);
                            signalTokenEMA[tkn].Process(VolumeCandles[tkn].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (VolumeCandles[tkn].Count > 1)
                        {
                            foreach (var price in VolumeCandles[tkn])
                            {
                                sTokenEMA[tkn].Process(VolumeCandles[tkn].First().ClosePrice, isFinal: true);
                                lTokenEMA[tkn].Process(VolumeCandles[tkn].First().ClosePrice, isFinal: true);
                                signalTokenEMA[tkn].Process(VolumeCandles[tkn].First().OpenPrice, isFinal: true);
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

        private void CandleManger_VolumeCandleFinished(object sender, Candle e)
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

                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
                            String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. sEMA:{6}. lEMA:{5}. Signal EMA: {7}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
                            , option.TradingSymbol, Decimal.Round(sema, 2), Decimal.Round(lema, 2), Decimal.Round(signalema, 2)), "CandleManger_TimeCandleFinished");
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

        private OrderTrio TradeEntry(uint token, decimal lastPrice, DateTime currentTime,
            IIndicatorValue sema, IIndicatorValue lema, IIndicatorValue bema, IIndicatorValue signalEMA)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                Trade trade = CheckEntryCriteria(token, currentTime, sema, lema, bema, signalEMA, lastPrice);
                if (trade != Trade.DoNotTrade)
                {
                    decimal stopLoss = 0, targetProfit = 0;
                    GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);

                    int tradeQty = GetTradeQty(lastPrice - stopLoss, _activeFuture.LotSize);
                    //ENTRY ORDER - BUY ALERT

                    Order order = null;
                    if (trade == Trade.Buy)
                    {
                        if (_orderTrio == null)
                        {
                            //Buy once
                            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                                token, true, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                                string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                        }
                        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                        {
                            //Do nothing
                        }
                        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "sell" && (currentTime - _orderTrio.EntryTradeTime).TotalMinutes > 3)
                        {
                            //buy double
                            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                                token, true, 2 * tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                                string.Format("TRADE!! Bought {0} lots of {1} @ {2}.", 2 * tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                        }
                    }
                    else if (trade == Trade.Sell)
                    {
                        if (_orderTrio == null)
                        {
                            //sell once
                            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                                token, false, tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                                string.Format("TRADE!! Sold {0} lots of {1} @ {2}.", tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                        }
                        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy" && (currentTime - _orderTrio.EntryTradeTime).TotalMinutes > 3)
                        {
                            //sell double
                            order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, _activeFuture.InstrumentType, lastPrice,
                                token, false, 2 * tradeQty * Convert.ToInt32(_activeFuture.LotSize),
                                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                                string.Format("TRADE!! Sold {0} lots of {1} @ {2}.", 2 * tradeQty, _activeFuture.TradingSymbol, order.AveragePrice), "TradeEntry");

                        }
                        else if (_orderTrio.Order.TransactionType.Trim(' ').ToLower() == "sell")
                        {
                            //do nothing
                        }
                    }




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
        //            && VolumeCandles.ContainsKey(activeCE.Value.InstrumentToken)
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
        //            && VolumeCandles.ContainsKey(activePE.Value.InstrumentToken)
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


                lastPrice = VolumeCandles[newInstrument.InstrumentToken].Last().ClosePrice;
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
        //private OrderTrio TradeEntry(uint token, decimal lastPrice, DateTime currentTime,
        //    IIndicatorValue sema, IIndicatorValue lema, IIndicatorValue bema, IIndicatorValue signalEMA, bool isOptionCall)
        //{
        //    OrderTrio orderTrio = null;
        //    try
        //    {
        //        Instrument option = ActiveOptions.FirstOrDefault(x => x.InstrumentToken == token);
        //        decimal entryRSI = 0;
        //        if (option != null && CheckEntryCriteria(token, currentTime, sema, lema, bema, signalEMA, isOptionCall, lastPrice, out entryRSI))
        //        {
        //            decimal stopLoss = 0, targetProfit = 0;
        //            GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);

        //            int tradeQty = GetTradeQty(lastPrice - stopLoss, option.LotSize);
        //            //ENTRY ORDER - BUY ALERT
        //            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
        //                token, true, tradeQty * Convert.ToInt32(option.LotSize),
        //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

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
        //private bool CheckEntryCriteria(uint token, DateTime currentTime, IIndicatorValue sema, 
        //    IIndicatorValue lema, IIndicatorValue bema, IIndicatorValue signalEMA, bool isOptionCall, decimal currentPrice, out decimal entryRSI)
        //{
        //    entryRSI = 0;
        //    try
        //    {
        //        if (!sTokenEMA.ContainsKey(token) || !lTokenEMA.ContainsKey(token) || !signalTokenEMA.ContainsKey(token))
        //        {
        //            return false;
        //        }

        //        decimal semaValue = sema.GetValue<decimal>();
        //        decimal lemaValue = lema.GetValue<decimal>();
        //        decimal bemaValue = bema == null ? 0 : bema.GetValue<decimal>();
        //        decimal signalemaValue = signalEMA.GetValue<decimal>();
        //        //entryRSI = rsiValue;

        //        if (semaValue < lemaValue)
        //        {
        //            _belowEMA[token] = true;
        //        }

        //        /// Entry Criteria:
        //        /// RSI Cross EMA from below, below 40 and both crosses above 40
        //        /// RSI Cross EMA from below between 40 and 50

        //        if (sema.IsFormed && lema.IsFormed
        //            && semaValue > lemaValue
        //            && _belowEMA[token]
        //            && currentPrice < semaValue + 2
        //            //&& sTokenEMA[token].GetValue<decimal>(0) < lTokenEMA[token].GetValue<decimal>(0)
        //            && ((isOptionCall && (_baseInstrumentPrice >= bemaValue || bemaValue == 0))
        //            || (!isOptionCall && (_baseInstrumentPrice <= bemaValue || bemaValue == 0)))
        //            && (lemaValue > signalemaValue)
        //            )
        //        {
        //            _belowEMA[token] = false;
        //            return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
        //        Thread.Sleep(100);
        //    }
        //    return false;
        //}

        
        private Trade CheckEntryCriteria(uint token, DateTime currentTime, IIndicatorValue sema,
            IIndicatorValue lema, IIndicatorValue bema, IIndicatorValue signalEMA, decimal currentPrice)
        {
            Trade trade = Trade.DoNotTrade;
            try
            {
                if (!sTokenEMA.ContainsKey(token) || !lTokenEMA.ContainsKey(token) || !signalTokenEMA.ContainsKey(token))
                {
                    trade = Trade.DoNotTrade;
                }
                else
                {

                    decimal semaValue = sema.GetValue<decimal>();
                    decimal lemaValue = lema.GetValue<decimal>();
                    decimal bemaValue = bema == null ? 0 : bema.GetValue<decimal>();
                    decimal signalemaValue = signalEMA.GetValue<decimal>();
                    //entryRSI = rsiValue;

                    if (semaValue < lemaValue)
                    {
                        trade = Trade.Sell;
                    }
                    else
                    {
                        trade = Trade.Buy;
                    }
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
            return trade;
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

                if ( (lastPrice <= orderTrio.StopLoss)
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
        //private void LoadOptionsToTrade(DateTime currentTime)
        //{
        //    try
        //    {
        //        var ceStrike = Math.Floor(_baseInstrumentPrice / 100m) * 100m;
        //        var peStrike = Math.Ceiling(_baseInstrumentPrice / 100m) * 100m;

        //        //if (ActiveOptions.Count > 1)
        //        //{
        //        //    Instrument ce = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "ce");
        //        //    Instrument pe = ActiveOptions.First(x => x.InstrumentType.Trim(' ').ToLower() == "pe");

        //        //    if (
        //        //       ((pe.Strike >= _baseInstrumentPrice + _minDistanceFromBInstrument && pe.Strike <= _baseInstrumentPrice +_maxDistanceFromBInstrument)
        //        //           || (_putOrderTrio != null && _putOrderTrio.Option != null && _putOrderTrio.Option.InstrumentToken == pe.InstrumentToken))
        //        //       && ((ce.Strike <= _baseInstrumentPrice - _minDistanceFromBInstrument && ce.Strike >= _baseInstrumentPrice -_maxDistanceFromBInstrument)
        //        //       || (_callOrderTrio != null && _callOrderTrio.Option != null && _callOrderTrio.Option.InstrumentToken == ce.InstrumentToken))
        //        //       )
        //        //    {
        //        //        return;
        //        //    }

        //        //}
        //        DataLogic dl = new DataLogic();

        //        if (OptionUniverse == null ||
        //        (OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
        //        || OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
        //           || (OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
        //           || OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
        //            )
        //        {
        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
        //            //Load options asynchronously
        //            OptionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument);

        //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
        //        }

        //        var activePE = OptionUniverse[(int)InstrumentType.PE].FirstOrDefault(x => x.Key >= _baseInstrumentPrice + _minDistanceFromBInstrument);
        //        var activeCE = OptionUniverse[(int)InstrumentType.CE].LastOrDefault(x => x.Key <= _baseInstrumentPrice - _minDistanceFromBInstrument);


        //        //First time.
        //        if (ActiveOptions.Count == 0)
        //        {
        //            ActiveOptions.Add(activeCE.Value);
        //            ActiveOptions.Add(activePE.Value);
        //        }
        //        //Already loaded from last run
        //        else if (ActiveOptions.Count == 1)
        //        {
        //            ActiveOptions.Add(ActiveOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value);
        //        }
        //        else if (ActiveOptions[0] == null)
        //        {
        //            ActiveOptions[0] = ActiveOptions[1].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value;
        //        }
        //        else if (ActiveOptions[1] == null)
        //        {
        //            ActiveOptions[1] = ActiveOptions[0].InstrumentType.Trim(' ').ToLower() == "ce" ? activePE.Value : activeCE.Value;
        //        }
        //        else
        //        {
        //            for (int i = 0; i < ActiveOptions.Count; i++)
        //            {
        //                Instrument option = ActiveOptions[i];
        //                bool isOptionCall = option.InstrumentType.Trim(' ').ToLower() == "ce";
        //                if (isOptionCall && _callOrderTrio == null)
        //                {
        //                    ActiveOptions[i] = activeCE.Value;
        //                }
        //                if (!isOptionCall && _putOrderTrio == null)
        //                {
        //                    ActiveOptions[i] = activePE.Value;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. {0}", ex.Message), "LoadOptionsToTrade");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }
        //}

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

        //private void UpdateInstrumentSubscription(DateTime currentTime)
        //{
        //    try
        //    {
        //        bool dataUpdated = false;
        //        if (OptionUniverse != null)
        //        {
        //            foreach (var options in OptionUniverse)
        //            {
        //                foreach (var option in options)
        //                {
        //                    if (!SubscriptionTokens.Contains(option.Value.InstrumentToken))
        //                    {
        //                        SubscriptionTokens.Add(option.Value.InstrumentToken);
        //                        dataUpdated = true;
        //                    }
        //                }
        //            }
        //            if (dataUpdated)
        //            {
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
        //                Task task = Task.Run(() => OnOptionUniverseChange(this));
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }
        //}

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
        private void LoadHistoricalCandles(string token, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (lTokenEMA)
                {
                    DataLogic dl = new DataLogic();

                    //The below is from ticks
                    //List<decimal> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, token.ToString(), _candleTimeSpan);
                    Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, token, TimeSpan.Zero, false, CandleType.Volume, _volumeThrehold);

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
                Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, 0, tpOrder, currentTime, currentmarketPrice:lastPrice);

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
