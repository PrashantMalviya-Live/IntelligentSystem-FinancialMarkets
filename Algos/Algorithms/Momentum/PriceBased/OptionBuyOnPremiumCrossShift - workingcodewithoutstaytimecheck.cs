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
    public class OptionBuyOnPremiumCrossShift : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public StraddleLinkedList _straddleList;
        public StraddleNode _lowerNode;
        public StraddleNode _upperNode;
        public DateTime _crossingTime;
        public bool _firstDateDone = false;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(OptionBuyOnPremiumCrossShift source);
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
        private readonly int _emaLength;


        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        public const int RSI_THRESHOLD = 60;
        private const int RSI_BAND = 1;
        private const int LOSSPERTRADE = 1000;

        private Dictionary<uint, bool> _lowerPremium;

        private Dictionary<uint, bool> _belowEMA;
        private Dictionary<uint, bool> _betweenEMAandUpperBand;
        private Dictionary<uint, bool> _aboveUpperBand;

        public const AlgoIndex algoIndex = AlgoIndex.PremiumCross;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public readonly decimal _rsiBandForEntry;
        public readonly decimal _rsiBandForExit;
        public readonly double _timeBandForExit;
        private readonly decimal _lowerLimitForCEBuy;
        private readonly decimal _upperLimitForPEBuy;
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public OptionBuyOnPremiumCrossShift(DateTime endTime, uint baseInstrumentToken, 
            DateTime? expiry, int quantity, decimal targetProfit, int algoInstance = 0)
        {
            _endDateTime = endTime;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _targetProfit = targetProfit;
            _stopTrade = true;

            SubscriptionTokens = new List<uint>();
            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _lowerPremium = new Dictionary<uint, bool>();
            _tradeQty = quantity;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins:
                0, Arg5: 0, Arg4: 0, Arg1:0, Arg2:_targetProfit, upperLimit:0, 
                Arg3: 0, Arg6: 0);

            ZConnect.Login();

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
                Instrument option = dl.GetInstrument(activeCallOrder.InstrumentToken);
                ActiveOptions.Add(option);
            }

            if (activePutOrder != null && activePutOrder.OrderId != "")
            {
                _putOrderTrio = new OrderTrio();
                _putOrderTrio.Order = activePutOrder;

                DataLogic dl = new DataLogic();
                Instrument option = dl.GetInstrument(activePutOrder.InstrumentToken);
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

                if (!GetBaseInstrumentPrice(tick))
                {
                    return;
                }

                LoadOptionsToTrade(currentTime);
                UpdateInstrumentSubscription(currentTime);
                //MonitorCandles(tick, currentTime);

                //check for premium cross
                UpdateTradedPrice(tick.InstrumentToken, tick.LastPrice);


                ///Step 1: Check BNF value and check for cross on both side straddle
                ///Step 2: Change position depending on cross
                ///Step3: Wait for 1 min on the cross for position to revert. 1 min it should stay on one side, then call it cross.
                ///Note: Set current after taking order on any straddle
                ///IMPORTANT!!! check if _upperNode and _lowerNode are not null and intact, then no need to find immediate node
                ///as it is taking 14 ms each
                
                if(_lowerNode== null || _upperNode == null || _lowerNode.Strike > _baseInstrumentPrice || _upperNode.Strike < _baseInstrumentPrice)
                {
                    _lowerNode = _straddleList.FindImmediateNode(_baseInstrumentPrice, true);
                    _upperNode = _straddleList.FindImmediateNode(_baseInstrumentPrice, false);
                }

                if (_lowerNode.Call.LastPrice * _lowerNode.Put.LastPrice != 0)
                {
                    if (!_lowerNode.Crossed && _lowerNode.isCallLower == 1 && _lowerNode.Call.LastPrice > _lowerNode.Put.LastPrice)
                    {
                        _lowerNode.Crossed = true;
                    }
                    if (!_lowerNode.Crossed && _lowerNode.isCallLower == -1 && _lowerNode.Call.LastPrice < _lowerNode.Put.LastPrice)
                    {
                        _lowerNode.Crossed = true;
                    }
                    if (_lowerNode.Crossed)
                    {
                        _lowerNode.Crossed = false;
                        _lowerNode.isCallLower = 0;
                        //take trade on lower node
                        if (_lowerNode.Call.LastPrice > _lowerNode.Put.LastPrice)
                        {
                            //buy call
                            //sell put is optional
                            if (_putOrderTrio == null || (_putOrderTrio.Order.OrderTimestamp.HasValue && (currentTime - _putOrderTrio.Order.OrderTimestamp.Value).TotalMinutes > 5) || (_putOrderTrio.Order.AveragePrice - _lowerNode.Put.LastPrice > 30 ))
                            {
                                if (_callOrderTrio == null || _callOrderTrio.Option.InstrumentToken != _lowerNode.Call.InstrumentToken)
                                {
                                    if (_callOrderTrio != null && _callOrderTrio.Option.InstrumentToken != _lowerNode.Call.InstrumentToken)
                                    {
                                        TradeEntry(_callOrderTrio.Option, currentTime, _targetProfit, false);
                                        _callOrderTrio = null;
                                    }
                                    _callOrderTrio = TradeEntry(_lowerNode.Call, currentTime, _targetProfit, true);
                                }

                                if (_putOrderTrio != null && _putOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                                {
                                    TradeEntry(_putOrderTrio.Option, currentTime, _targetProfit, false);
                                    _putOrderTrio = null;
                                }

                                //_putOrderTrio = TradeEntry(_lowerNode.Put, currentTime, _targetProfit, false);
                            }
                        }
                        else
                        {
                            //buy put
                            //sell call is optional
                            if (_callOrderTrio == null || (_callOrderTrio.Order.OrderTimestamp.HasValue && (currentTime - _callOrderTrio.Order.OrderTimestamp.Value).TotalMinutes > 5) || (_callOrderTrio.Order.AveragePrice - _lowerNode.Call.LastPrice > 30))
                            {
                                if (_putOrderTrio == null || _putOrderTrio.Option.InstrumentToken != _lowerNode.Put.InstrumentToken)
                                {
                                    if (_putOrderTrio != null && _putOrderTrio.Option.InstrumentToken != _lowerNode.Put.InstrumentToken)
                                    {
                                        TradeEntry(_putOrderTrio.Option, currentTime, _targetProfit, false);
                                        _putOrderTrio = null;
                                    }

                                    _putOrderTrio = TradeEntry(_lowerNode.Put, currentTime, _targetProfit, true);
                                }
                                if (_callOrderTrio != null && _callOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                                {
                                    TradeEntry(_callOrderTrio.Option, currentTime, _targetProfit, false);
                                    _callOrderTrio = null;
                                }
                                //_callOrderTrio = TradeEntry(_lowerNode.Put, currentTime, _targetProfit, true);
                            }
                        }
                        //close trade on the upper node if taken already
                    }
                }
                //StraddleNode _upperNode = _straddleList.FindImmediateNode(_baseInstrumentPrice, false);
                if (_upperNode.Call.LastPrice * _upperNode.Put.LastPrice != 0)
                {
                    if (!_upperNode.Crossed && _upperNode.isCallLower == 1 && _upperNode.Call.LastPrice > _upperNode.Put.LastPrice)
                    {
                        _upperNode.Crossed = true;
                    }
                    if (!_upperNode.Crossed && _upperNode.isCallLower == - 1 && _upperNode.Call.LastPrice < _upperNode.Put.LastPrice)
                    {
                        _upperNode.Crossed = true;
                    }
                    if (_upperNode.Crossed)
                    {
                        _upperNode.Crossed = false;
                        _upperNode.isCallLower = 0;
                        //take trade on upper node
                        if (_upperNode.Call.LastPrice > _upperNode.Put.LastPrice)
                        {
                            //buy call
                            //sell put is optional
                            //_callOrderTrio = TradeEntry(_upperNode.Call, currentTime, _targetProfit, true);
                            //_putOrderTrio = TradeEntry(_upperNode.Put, currentTime, _targetProfit, false);
                            if (_putOrderTrio == null || (_putOrderTrio.Order.OrderTimestamp.HasValue && (currentTime - _putOrderTrio.Order.OrderTimestamp.Value).TotalMinutes > 5) || (_putOrderTrio.Order.AveragePrice - _upperNode.Put.LastPrice > 30))
                            {
                                if (_callOrderTrio == null || _callOrderTrio.Option.InstrumentToken != _upperNode.Call.InstrumentToken)
                                {
                                    if (_callOrderTrio != null && _callOrderTrio.Option.InstrumentToken != _upperNode.Call.InstrumentToken)
                                    {
                                        TradeEntry(_callOrderTrio.Option, currentTime, _targetProfit, false);
                                        _callOrderTrio = null;
                                    }
                                    _callOrderTrio = TradeEntry(_upperNode.Call, currentTime, _targetProfit, true);
                                }

                                if (_putOrderTrio != null && _putOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                                {
                                    TradeEntry(_putOrderTrio.Option, currentTime, _targetProfit, false);
                                    _putOrderTrio = null;
                                }
                            }


                        }
                        else
                        {
                            //buy put
                            //sell call is optional
                            if (_callOrderTrio == null || (_callOrderTrio.Order.OrderTimestamp.HasValue && (currentTime - _callOrderTrio.Order.OrderTimestamp.Value).TotalMinutes > 5) || (_callOrderTrio.Order.AveragePrice - _upperNode.Call.LastPrice > 30))
                            {
                                if (_putOrderTrio == null || _putOrderTrio.Option.InstrumentToken != _upperNode.Put.InstrumentToken)
                                {
                                    if (_putOrderTrio != null && _putOrderTrio.Option.InstrumentToken != _upperNode.Put.InstrumentToken)
                                    {
                                        TradeEntry(_putOrderTrio.Option, currentTime, _targetProfit, false);
                                        _putOrderTrio = null;
                                    }

                                    _putOrderTrio = TradeEntry(_upperNode.Put, currentTime, _targetProfit, true);
                                }
                                if (_callOrderTrio != null && _callOrderTrio.Order.TransactionType.Trim(' ').ToLower() == "buy")
                                {
                                    TradeEntry(_callOrderTrio.Option, currentTime, _targetProfit, false);
                                    _callOrderTrio = null;
                                }

                                //_callOrderTrio = TradeEntry(_upperNode.Put, currentTime, _targetProfit, true);
                            }
                        }
                        //close trade on the lower node if taken already
                    }
                }

                //Put a hedge at 3:15 PM
                // TriggerEODPositionClose(tick.LastTradeTime);
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

        private void UpdateTradedPrice(uint token, decimal lastPrice)
        {
            StraddleNode straddleNode = _straddleList.First;

            while(straddleNode != null)
            {
                if(straddleNode.Call.InstrumentToken == token)
                {
                    straddleNode.Call.LastPrice = lastPrice;
                    break;
                }
                else if (straddleNode.Put.InstrumentToken == token)
                {
                    straddleNode.Put.LastPrice = lastPrice;
                    break;
                }
                if (!straddleNode.Crossed && straddleNode.Call.LastPrice * straddleNode.Put.LastPrice != 0 && straddleNode.isCallLower == 0)
                {
                    straddleNode.isCallLower = straddleNode.Call.LastPrice < straddleNode.Put.LastPrice ? 1 : -1;
                }
                straddleNode = straddleNode.NextNode;
            }
           
        }


        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            //try
            //{
            //    if (e.InstrumentToken == _baseInstrumentToken)
            //    {
            //        if (_bEMALoaded)
            //        {
            //            _bEMA.Process(e.ClosePrice, isFinal: true);
            //        }
            //    }
            //    else if (_EMALoaded.Contains(e.InstrumentToken))
            //    {
            //        if (!lTokenEMA.ContainsKey(e.InstrumentToken))
            //        {
            //            return;
            //        }
                    
            //        tokenRSI[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
            //        lTokenEMA[e.InstrumentToken].Process(tokenRSI[e.InstrumentToken].GetValue<decimal>(0), isFinal: true);

            //        if (ActiveOptions.Any(x => x.InstrumentToken == e.InstrumentToken))
            //        {
            //            Instrument option = ActiveOptions.Find(x => x.InstrumentToken == e.InstrumentToken);

            //            decimal rsi = tokenRSI[e.InstrumentToken].GetValue<decimal>(0);
            //            decimal ema = lTokenEMA[e.InstrumentToken].GetValue<decimal>(0);

            //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
            //                String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. RSI:{6}. EMA on RSI:{5}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
            //                , option.TradingSymbol, Decimal.Round(ema, 2), Decimal.Round(rsi, 2)), "CandleManger_TimeCandleFinished");
            //        }
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _stopTrade = true;
            //    Logger.LogWrite(ex.Message + ex.StackTrace);
            //    Logger.LogWrite("Closing Application");
            //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
            //        String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CandleManger_TimeCandleFinished");
            //    Thread.Sleep(100);
            //}
        }
        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal targetProfit, bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                
                int tradeQty = GetTradeQty();

                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                    option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                    string.Format("TRADE!! {3} {0} lots of {1} @ {2}.", tradeQty, option.TradingSymbol, order.AveragePrice, buyOrder?"Bought":"Sold"), "TradeEntry");


                //Order tpOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, order.AveragePrice + targetProfit,
                //    option.InstrumentToken, !buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                //    algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT, product: Constants.PRODUCT_NRML);

                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                //    string.Format("Placed target profit at {0}.", targetProfit), "TradeEntry");

                //if (option.InstrumentType.Trim(' ').ToLower() == "ce" && _callOrderTrio != null)
                //{

                //    Order clorder = MarketOrders.PlaceOrder(_algoInstance, _callOrderTrio.Option.TradingSymbol, _callOrderTrio.Option.InstrumentType, _callOrderTrio.Option.LastPrice,
                //   _callOrderTrio.Option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(_callOrderTrio.Option.LotSize),
                //   algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                //        string.Format("{0} {1} @ {2} profit.", !buyOrder ? "Bought back" : "Sold", option.TradingSymbol, clorder.AveragePrice - _callOrderTrio.Order.AveragePrice), "TradeEntry");
                //}
                //if (option.InstrumentType.Trim(' ').ToLower() == "pe" && _putOrderTrio != null)
                //{

                //    Order clorder = MarketOrders.PlaceOrder(_algoInstance, _putOrderTrio.Option.TradingSymbol, _putOrderTrio.Option.InstrumentType, _putOrderTrio.Option.LastPrice,
                //   _putOrderTrio.Option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(_putOrderTrio.Option.LotSize),
                //   algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_NRML);

                //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                //        string.Format("{0} {1} @ {2} profit.", !buyOrder ? "Bought back" : "Sold", option.TradingSymbol, clorder.AveragePrice - _putOrderTrio.Order.AveragePrice), "TradeEntry");
                //}

                orderTrio = new OrderTrio();
                orderTrio.Option = option;
                orderTrio.Order = order;
                //orderTrio.SLOrder = slOrder;
                //orderTrio.TPOrder = tpOrder;
                //orderTrio.StopLoss = stopLoss;
                //orderTrio.TargetProfit = order.AveragePrice + targetProfit;
                //orderTrio.EntryRSI = entryRSI;
                orderTrio.EntryTradeTime = currentTime;

                OnTradeEntry(order);
                //OnTradeEntry(slOrder);
                //OnTradeEntry(tpOrder);
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
        private int GetTradeQty()
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

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if(_straddleList == null || (_straddleList.Current != null && (_straddleList.Current.PrevNode == null || _straddleList.Current.NextNode == null)))
                {
                    DataLogic dl = new DataLogic();

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    var closeOptions = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, 
                        Math.Max(_maxDistanceFromBInstrument, 300)); //loading at least 300 tokens each side

                    decimal minStrike = closeOptions[0].First().Key;
                    decimal maxStrike = closeOptions[0].Last().Key;

                    for(decimal i = minStrike; i <= maxStrike; i += 100)
                    {
                        StraddleNode straddleNode = new StraddleNode();
                        straddleNode.Strike = i;
                        straddleNode.Call = closeOptions[(int)InstrumentType.CE][i];
                        straddleNode.Put = closeOptions[(int)InstrumentType.PE][i];

                        if(_straddleList != null)
                        {
                            _straddleList.AppendNode(straddleNode);
                        }
                        else
                        {
                            _straddleList = new StraddleLinkedList(straddleNode);
                        }
                    }
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (_straddleList != null)
                {
                    StraddleNode straddleNode = _straddleList.First;
                    while (straddleNode !=null)
                    {
                        if (!SubscriptionTokens.Contains(straddleNode.Call.InstrumentToken))
                        {
                            SubscriptionTokens.Add(straddleNode.Call.InstrumentToken);
                            dataUpdated = true;
                        }
                        if (!SubscriptionTokens.Contains(straddleNode.Put.InstrumentToken))
                        {
                            SubscriptionTokens.Add(straddleNode.Put.InstrumentToken);
                            dataUpdated = true;
                        }
                        straddleNode = straddleNode.NextNode;
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, 
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
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
        }
    }
}
