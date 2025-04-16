using Algorithms.Candles;
using Algorithms.Indicators;
using Algorithms.Utilities;
using Algorithms.Utils;
using BrokerConnectWrapper;
using GlobalCore;
using GlobalLayer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ZMQFacade;

namespace Algorithms.Algorithms
{
    public class DirectionSellOnHistoricalAvg : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public Dictionary<uint, Instrument> OptionUniverse { get; set; }
        public Dictionary<uint, uint> MappedTokens { get; set; }

        public SortedList<decimal, Instrument[]> StraddleUniverse { get; set; }
        public Dictionary<decimal, FixedSizedQueue<decimal>> HistoricalStraddleAverage { get; set; }
        public Dictionary<decimal, List<decimal>> ReferenceStraddleValue { get; set; }

        public Dictionary<decimal, List<OrderTrio>> _callOrderTrioList, _putOrderTrioList;
        public Dictionary<uint, FixedSizedQueue<decimal>> HistoricalOptionPrice { get; set; }
        public Dictionary<decimal, List<decimal>> TradedStraddleValue { get; set; }
        public Dictionary<uint, List<decimal>> OptionTradedPrices { get; set; }
        //Count and straddle value for each strike price. This is used to determine the average and limit the trade per strike price.
        public Dictionary<ReferenceTradeStrikeKey, List<decimal>> ReferenceTradedStraddleValue { get; set; }
        List<ReferenceTradeStrikeKey> referenceTradeStrikeKeys;
        //Stores traded qty and up or downtrade bool = 1 means uptrade

        //All order ids for a particular trade strike
        private Dictionary<Order, decimal> CallOrdersTradeStrike { get; set; }
        private Dictionary<Order, decimal> PutOrdersTradeStrike { get; set; }

        public Dictionary<decimal, decimal[]> TradedQuantity { get; set; }
        public Dictionary<uint, int> OptionTradedQuantity { get; set; }
        public Dictionary<decimal, List<decimal>> TradeReferenceStrikes { get; set; }

        
        private int _thresholdQtyForBuy = 14;
        private int _buyQty = 4, _boughtQty;
        private Instrument marginPutBuyInstrument;
        private Instrument marginCallBuyInstrument;
        //Strike and time order
        private Dictionary<decimal, DateTime?> LastTradeTime { get; set; }

        public Dictionary<decimal, decimal[]> CurrentSellStraddleValue { get; set; }
        public Dictionary<decimal, decimal[]> CurrentBuyStraddleValue { get; set; }
        public Dictionary<decimal, decimal> LastStraddleValue { get; set; }
        public SortedList<decimal, BS[]> SpreadBSUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(DirectionSellOnHistoricalAvg source);
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

        public List<Order> _pastOrders;
        private bool _stopTrade;
        private int _tradedQty = 0;
        
        private int _stepQty = 0;
        private int _maxQty = 0;
        private int _maxLotsOnStrike = 3;
        private decimal _marginforNextTrade = 4;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        public decimal _strikePriceRange = 300;
        private DateTime? _expiry;

        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        public const int CANDLE_COUNT = 30;
        public const int RSI_MID_POINT = 55;

        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;

        public readonly int _tradeQty;
        private readonly decimal _targetProfit;
        private decimal _stopLoss;
        private decimal _openSpread;
        private decimal _closeSpread;
        private decimal _targetPoints;
        private bool _upTrade;
        public const AlgoIndex algoIndex = AlgoIndex.SellOnHistoricalAverage;
        Dictionary<uint, List<Candle>> TimeCandles;
        private object lockObject = new object();
        public List<uint> SubscriptionTokens { get; set; }
        HttpClient _httpClient;
        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public DirectionSellOnHistoricalAvg(DateTime endTime, uint baseInstrumentToken,
            DateTime? expiry, int quantity, int maxQty, int stepQty, decimal targetProfit, 
            decimal openSpread, decimal closeSpread, decimal stopLoss,
            int algoInstance = 0, bool positionSizing = false, decimal maxLossPerTrade = 0, 
            double timeBandForExit = 0, HttpClient httpClient = null)
        {
            _httpClient = httpClient;
            _expiry = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _targetProfit = targetProfit;
            _targetPoints = targetProfit;
            _stopLoss = stopLoss;
            _openSpread = openSpread;
            _closeSpread = closeSpread;
            _stopTrade = true;
            _maxQty = maxQty;
            _stepQty = stepQty;
            SubscriptionTokens = new List<uint>();
            HistoricalStraddleAverage = new Dictionary<decimal, FixedSizedQueue<decimal>>();
            HistoricalOptionPrice = new Dictionary<uint, FixedSizedQueue<decimal>>();
            CurrentSellStraddleValue = new Dictionary<decimal, decimal[]>();
            CurrentBuyStraddleValue = new Dictionary<decimal, decimal[]>();
            LastStraddleValue = new Dictionary<decimal, decimal>();
            TradedStraddleValue = new Dictionary<decimal, List<decimal>>();
            ReferenceTradedStraddleValue = new Dictionary<ReferenceTradeStrikeKey, List<decimal>>();
            TradedQuantity = new Dictionary<decimal, decimal[]>();
            OptionTradedQuantity = new Dictionary<uint, int>();
            OptionTradedPrices = new Dictionary<uint, List<decimal>>();
            TradeReferenceStrikes = new Dictionary<decimal, List<decimal>>();
            LastTradeTime = new Dictionary<decimal, DateTime?>();
            ActiveOptions = new List<Instrument>();
            OptionUniverse = new Dictionary<uint, Instrument>();
            _tradeQty = quantity;
            referenceTradeStrikeKeys = new List<ReferenceTradeStrikeKey>();
            CallOrdersTradeStrike = new Dictionary<Order, decimal>();
            PutOrdersTradeStrike = new Dictionary<Order, decimal>();
            ReferenceStraddleValue = new Dictionary<decimal, List<decimal>>();
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                _expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins: 0, Arg1: _openSpread, Arg2: _closeSpread,
                Arg3: _targetProfit, Arg4: _stopLoss);

            _callOrderTrioList = new Dictionary<decimal, List<OrderTrio>>();
            _putOrderTrioList = new Dictionary<decimal, List<OrderTrio>>();

            ZConnect.Login();
            KoConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            DataLogic dl = new DataLogic();
            marginPutBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 37000, "pe");
            marginCallBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 41000, "ce");
            _buyQty = Math.Max(_stepQty, _buyQty);
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;

            //DataLogic dl = new DataLogic();
            //marginPutBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 37000, "pe");
            //marginCallBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 41000, "ce");

            //DataLogic dl = new DataLogic();
            //marginCallBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 35500, "pe");
            try
            {
                uint token = tick.InstrumentToken;
                lock (lockObject)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);

                    if (tick.LastTradeTime.HasValue)
                    {
                        Instrument option = OptionUniverse[token];

                        //int optionType = option.InstrumentType.Trim(' ').ToLower() == "ce" ? (int)InstrumentType.CE : (int)InstrumentType.PE;
                        decimal optionStrike = option.Strike;
                        UpdateStraddleValue(tick, option);


                        decimal tradeStrike = GetTradeStrike(optionStrike);
                        bool currentUpTrade = tradeStrike > _baseInstrumentPrice;

                        BuySideLogic(option, optionStrike, currentUpTrade, currentTime);
                        SellSideLogic(optionStrike, tradeStrike, currentUpTrade, currentTime);


                        //Close all Positions at 3:15
                        TriggerEODPositionClose(currentTime);
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

        private void BuySideLogic(Instrument option, decimal optionStrike, bool currentUpTrade, DateTime currentTime)
        {
            if ((CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] * CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0))
            {
                decimal currentBuyStraddleValue = CurrentBuyStraddleValue[optionStrike].Sum();
                decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
                bool tpHit = false;
                if (isExitTrigerred(option, out tpHit))
                {
                    //here you have to check if the trigger option prices have stabilzied and not the trade strike price as it is check below

                    //here you have to check if the trigger option prices have stabilzied and not the trade strike price as it is check below
                    //List<decimal> triggerStrikes = QuantityToBeTraded(optionStrike);
                    //int qtytobeExited = Convert.ToInt32(TradedQuantity[optionStrike][0]) - triggerStrikes.Count;
                    //if (qtytobeExited > 0)
                    //{

                    if (option.InstrumentType.ToLower() == "ce")
                    {
                        PlaceCloseOrder(_callOrderTrioList[optionStrike], tpHit, currentTime);
                        _callOrderTrioList[optionStrike] = new List<OrderTrio>();
                    }
                    else
                    {
                        PlaceCloseOrder(_putOrderTrioList[optionStrike], tpHit, currentTime);
                        _putOrderTrioList[optionStrike] = new List<OrderTrio>();
                    }

                    //PlaceCloseOrder(option, OptionTradedQuantity[option.InstrumentToken], currentTime);
                    OptionTradedPrices[option.InstrumentToken] = new List<decimal>();
                    OptionTradedQuantity[option.InstrumentToken] = 0;

                    decimal[] strikes = ReferenceStraddleValue.Keys.ToArray<decimal>();
                    foreach (decimal strike in strikes)
                    {
                        if (TradeReferenceStrikes[optionStrike].Contains(strike))
                        {
                            ReferenceStraddleValue[strike] = new List<decimal>();
                        }
                    }
                    TradeReferenceStrikes[optionStrike] = new List<decimal>();

                    //if ((_tradedQty <= _thresholdQtyForBuy + _boughtQty - _buyQty) && _boughtQty > 0)
                    //{
                    //    TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //    TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //    _boughtQty -= _buyQty;
                    //}
                    //}
                    //else
                    //{
                    //    TradedStraddleValue[optionStrike] = new List<decimal>();
                    //    TradedStraddleValue[optionStrike].Add(currentSellStraddleValue);
                    //    LastTradeTime[optionStrike] = currentTime;
                    //}

                    //foreach (ReferenceTradeStrikeKey referenceTradeStrike in referenceTradeStrikeKeys)
                    //{
                    //    if (referenceTradeStrike.TradeStrike == optionStrike && !triggerStrikes.Contains(referenceTradeStrike.ReferenceStrike))
                    //    {
                    //        ReferenceTradedStraddleValue.Remove(referenceTradeStrike);
                    //    }
                    //}
                    //referenceTradeStrikeKeys.RemoveAll(x => x.TradeStrike == optionStrike && !triggerStrikes.Contains(x.ReferenceStrike));
                    //TradeReferenceStrikes[optionStrike].RemoveAll(x => !triggerStrikes.Contains(x));

                    //This loop will help to add additional triggers for the trade strike
                    //foreach (decimal triggerStrike in triggerStrikes)
                    //{
                    //    ReferenceTradeStrikeKey referenceTradeStrikeKey = referenceTradeStrikeKeys.FirstOrDefault(x => x.ReferenceStrike == triggerStrike && x.TradeStrike == optionStrike);

                    //    if (referenceTradeStrikeKey == null)
                    //    {
                    //        referenceTradeStrikeKey = new ReferenceTradeStrikeKey(triggerStrike, optionStrike);
                    //        referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //    }

                    //    if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                    //    {
                    //        ReferenceTradedStraddleValue[referenceTradeStrikeKey][ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count - 1] = LastStraddleValue[optionStrike];
                    //    }
                    //    else
                    //    {
                    //        ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { LastStraddleValue[optionStrike] });
                    //    }

                    //    //ReferenceTradeStrikeKey referenceTradeStrikeKey = new ReferenceTradeStrikeKey(triggerStrike, optionStrike);
                    //    //referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //    //ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { LastStraddleValue[optionStrike] });

                    //    if (!TradeReferenceStrikes[optionStrike].Contains(triggerStrike))
                    //    {
                    //        TradeReferenceStrikes[optionStrike].Add(triggerStrike);
                    //    }
                    //    LastTradeTime[optionStrike] = currentTime;
                    //}
                }
                CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] = 0;
                CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] = 0;
            }
        }

        private void SellSideLogic(decimal optionStrike, decimal tradeStrike, bool currentUpTrade, DateTime currentTime)
        {
            if (CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] * CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
            {
                decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
                //decimal currentSellStraddleValueForTradeStrike = LastStraddleValue[tradeStrike];

                //ReferenceTradeStrikeKey referenceTradeStrikeKey = referenceTradeStrikeKeys.FirstOrDefault(x => x.ReferenceStrike == optionStrike && x.TradeStrike == tradeStrike);

                if (isEntryTriggered(optionStrike, tradeStrike, currentTime))
                {
                    Instrument option;
                    Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
                    Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");


                    Order optionSellOrder = null, optionTPOrder = null, optionSLOrder = null;
                    _marginforNextTrade = 0;
                    _maxLotsOnStrike = 4;
                    if (call.LastPrice < HistoricalOptionPrice[call.InstrumentToken].Value 
                       // && (OptionTradedPrices[call.InstrumentToken].Count == 0 || call.LastPrice > OptionTradedPrices[call.InstrumentToken].Last() + _marginforNextTrade)
                        && OptionTradedQuantity[call.InstrumentToken] <= _maxLotsOnStrike
                        && call.LastPrice < put.LastPrice
                        )
                    {
                        tradeStrike = tradeStrike + 100;
                        call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");

                        optionSellOrder =  TradeEntry(call, currentTime, _stepQty, tradeStrike, false, Constants.ORDER_TYPE_MARKET, call.LastPrice);
                        option = call;

                        optionTPOrder = TradeEntry(call, currentTime, _stepQty, tradeStrike, 
                            true, Constants.ORDER_TYPE_LIMIT, limitPrice: optionSellOrder.AveragePrice - _targetPoints);

                        optionSLOrder = TradeEntry(call, currentTime, _stepQty, tradeStrike,
                            true, Constants.ORDER_TYPE_SL, limitPrice: optionSellOrder.AveragePrice + _stopLoss, triggerPrice: optionSellOrder.AveragePrice + _stopLoss - 5);

                        OrderTrio callOrderTrio = new OrderTrio();
                        callOrderTrio.Order = optionSellOrder;
                        callOrderTrio.TPOrder = optionTPOrder;
                        callOrderTrio.SLOrder = optionSLOrder;

                        _callOrderTrioList[tradeStrike].Add(callOrderTrio);
                        
                    }
                    else if (put.LastPrice < HistoricalOptionPrice[put.InstrumentToken].Value 
                        //&& (OptionTradedPrices[put.InstrumentToken].Count == 0 || put.LastPrice > OptionTradedPrices[put.InstrumentToken].Last() + _marginforNextTrade)
                        && OptionTradedQuantity[put.InstrumentToken] <= _maxLotsOnStrike
                        && put.LastPrice < call.LastPrice
                        )
                    {
                        tradeStrike = tradeStrike - 100;
                        put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

                        optionSellOrder = TradeEntry(put, currentTime, _stepQty, tradeStrike, false, Constants.ORDER_TYPE_MARKET, limitPrice: put.LastPrice);
                        option = put;

                        optionTPOrder = TradeEntry(put, currentTime, _stepQty, tradeStrike,
                            true, Constants.ORDER_TYPE_LIMIT, optionSellOrder.AveragePrice - _targetPoints);

                        optionSLOrder = TradeEntry(put, currentTime, _stepQty, tradeStrike,
                           true, Constants.ORDER_TYPE_SL, optionSellOrder.AveragePrice + _stopLoss, optionSellOrder.AveragePrice + _stopLoss - 5);

                        OrderTrio putOrderTrio = new OrderTrio();
                        putOrderTrio.Order = optionSellOrder;
                        putOrderTrio.TPOrder = optionTPOrder;
                        putOrderTrio.SLOrder = optionSLOrder;

                        _putOrderTrioList[tradeStrike].Add(putOrderTrio);
                    }
                    else
                    {
                        return;
                    }
                    _tradedQty = _tradedQty + _stepQty;

                    ////Far OTM buy to reduce margin
                    //if (_tradedQty > _thresholdQtyForBuy + _boughtQty)
                    //{
                    //    putBuyOrder = TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //    callBuyOrder = TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //    _boughtQty += _buyQty;
                    //}


                    //CallOrdersTradeStrike.TryAdd(callStopLossOrder, tradeStrike);
                    //PutOrdersTradeStrike.TryAdd(putStopLossOrder, tradeStrike);

                    OnTradeEntry(optionSellOrder);
                    OnTradeEntry(optionTPOrder);
                    OnTradeEntry(optionSLOrder);

                    //straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;

                    //if (OptionTradedPrice.ContainsKey(option.InstrumentToken))
                    //{
                    OptionTradedPrices[option.InstrumentToken].Add(optionSellOrder.AveragePrice);
                    HistoricalOptionPrice[option.InstrumentToken].Enqueue(optionSellOrder.AveragePrice);
                    //}
                    //else
                    //{
                    //    OptionTradedPrice.Add(option.InstrumentToken, new List<decimal>() { optionSellOrder.AveragePrice });
                    //}

                    //TradedStraddleValue[tradeStrike].Add(straddleValue);
                    //TradedQuantity[tradeStrike][0] += _stepQty;

                    OptionTradedQuantity[option.InstrumentToken] += _stepQty;

                    //TradedQuantity[tradeStrike][1] = _upTrade ? 1 : 0;
                    //TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                    ReferenceStraddleValue[optionStrike].Add(currentSellStraddleValue);
                    //if (referenceTradeStrikeKey == null)
                    //{
                    //    referenceTradeStrikeKey = new ReferenceTradeStrikeKey(optionStrike, tradeStrike);
                    //    referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //}

                    //if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                    //{
                    //    ReferenceTradedStraddleValue[referenceTradeStrikeKey].Add(straddleValue);
                    //}
                    //else
                    //{
                    //    ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { straddleValue });
                    //}


                    TradeReferenceStrikes[tradeStrike].Add(optionStrike);
                    //LastTradeTime[tradeStrike] = currentTime;

                    //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
                    //    Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");
                }

                //HistoricalStraddleAverage[option.Strike] = HistoricalStraddleAverage[option.Strike].Value == 0 ? currentSellStraddleValue : (HistoricalStraddleAverage[option.Strike] * 1800 + currentSellStraddleValue) / 1801;
                HistoricalStraddleAverage[optionStrike].Enqueue(currentSellStraddleValue);
                
                CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] = 0;
                CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] = 0;
            }
        }
        private List<decimal> QuantityToBeTraded(decimal optionStrike)
        {
            List<decimal> triggerStrikes = new List<decimal>();
            //determine all trigger option strikes for this traded strike
            for (decimal strike = _baseInstrumentPrice - _strikePriceRange; strike <= (_baseInstrumentPrice + _strikePriceRange); strike = strike + 100)
            {
                strike = Math.Round(strike / 100, 0) * 100;
                if (optionStrike == GetTradeStrike(strike))
                {
                    if ((LastStraddleValue[strike] - HistoricalStraddleAverage[strike].Value) > _openSpread)
                    {
                        triggerStrikes.Add(strike);
                    }

                }
            }
            return triggerStrikes;
        }
        private bool isEntryTriggered(decimal optionStrike, decimal tradeStrike, DateTime currentTime)
        {
            decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
            //decimal currentSellStraddleValueForTradeStrike = LastStraddleValue[tradeStrike];
            ReferenceTradeStrikeKey referenceTradeStrikeKey = referenceTradeStrikeKeys.FirstOrDefault(x => x.ReferenceStrike == optionStrike && x.TradeStrike == tradeStrike);

            return ((optionStrike <= _baseInstrumentPrice + _strikePriceRange && optionStrike >= _baseInstrumentPrice - _strikePriceRange)
                    && (tradeStrike <= _baseInstrumentPrice + _strikePriceRange && tradeStrike >= _baseInstrumentPrice - _strikePriceRange)
                    && TradedQuantity[tradeStrike][0] < _stepQty * _maxLotsOnStrike
                    && (HistoricalStraddleAverage[optionStrike].Value != 0)
                    && (_tradedQty < _maxQty - _stepQty)
                    && (currentTime.TimeOfDay <= new TimeSpan(15, 00, 00))
                    //&& TradedStraddleValue[option.Strike].Count > 0
                    &&

                    (currentSellStraddleValue - (ReferenceStraddleValue[optionStrike].Count == 0 ? HistoricalStraddleAverage[optionStrike].Value :
                                (ReferenceStraddleValue[optionStrike].Last() + 10))) >= _openSpread);
                    //(currentSellStraddleValue - HistoricalStraddleAverage[optionStrike].Value >= _openSpread));

            //            &&


            //(((referenceTradeStrikeKey == null) && ((currentSellStraddleValue - HistoricalStraddleAverage[optionStrike].Value) >= _openSpread))
            //|| ((referenceTradeStrikeKey != null) && (ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() < _maxLotsOnStrike)
            //&& (currentSellStraddleValueForTradeStrike - ReferenceTradedStraddleValue[referenceTradeStrikeKey].Last() >= 15)))
            //);
        }

        /// <summary>
        /// TODO: check if momentum is high then sell far. Movementum may be from volume
        /// </summary>
        /// <param name="optionStrike"></param>
        /// <returns></returns>
        private decimal GetTradeStrike(decimal optionStrike)
        {
            decimal delta = 0;
            
            //if ((_baseInstrumentPrice - optionStrike) > 300)
            //{
            //    delta = (_baseInstrumentPrice - optionStrike) - 200;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) > 200)
            //{
            //    delta = (_baseInstrumentPrice - optionStrike) - 150;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) > 100)
            //{
            //    delta = (_baseInstrumentPrice - optionStrike) - 100;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < -300)
            //{
            //    delta = (_baseInstrumentPrice - optionStrike) + 200;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < -200)
            //{
            //    delta = (_baseInstrumentPrice - optionStrike) + 150;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < -100)
            //{
            //    delta = (_baseInstrumentPrice - optionStrike) + 100;
            //}
            //else
            //{
            //    delta = (_baseInstrumentPrice - optionStrike);
            //}

            //decimal delta = 0;
            //if ((_baseInstrumentPrice - optionStrike) > Math.Min(300, _strikePriceRange))
            //{
            //    delta = 100; //0;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) > 200)
            //{
            //    delta = 170; //0;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) > 90)
            //{
            //    delta = 230; //130
            //}
            //else if ((_baseInstrumentPrice - optionStrike) > 0)
            //{
            //    delta = 250;// _baseInstrumentPrice - optionStrike; //150;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < -1 * Math.Min(300, _strikePriceRange))
            //{
            //    delta = 100; //0
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < -200)
            //{
            //    delta = -170; // -70;
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < -90)
            //{
            //    delta = -230; //-130
            //}
            //else if ((_baseInstrumentPrice - optionStrike) < 0)
            //{
            //    delta = -250; //-150// (_baseInstrumentPrice - optionStrike);
            //}
            //else
            //{
            //    delta = 0;
            //}

            //sell at the money
            decimal tradeStrike = _baseInstrumentPrice + delta;

            tradeStrike = Math.Round(tradeStrike / 100, 0) * 100;
            return tradeStrike;
        }
        private void UpdateStraddleValue(Tick tick, Instrument option)
        {
            int optionType = option.InstrumentType.Trim(' ').ToLower() == "ce" ? (int)InstrumentType.CE : (int)InstrumentType.PE;
            decimal optionStrike = option.Strike;

            HistoricalOptionPrice[option.InstrumentToken].Enqueue(tick.LastPrice);
#if market
                CurrentSellStraddleValue [optionStrike][optionType] = tick.Bids[0].Price;
                CurrentBuyStraddleValue [optionStrike][optionType] = tick.Offers[0].Price;
#elif local
            CurrentSellStraddleValue[optionStrike][optionType] = tick.LastPrice;
            CurrentBuyStraddleValue[optionStrike][optionType] = tick.LastPrice;
#endif
            if (CurrentSellStraddleValue[optionStrike][0] * CurrentSellStraddleValue[optionStrike][1] * CurrentBuyStraddleValue[optionStrike][0] * CurrentBuyStraddleValue[optionStrike][1] != 0)
            {
                LastStraddleValue[optionStrike] = CurrentSellStraddleValue[optionStrike][0] + CurrentSellStraddleValue[optionStrike][1];
            }
            option.LastPrice = tick.LastPrice;
        }

        private bool isExitTrigerred(Instrument option, out bool tpHit)
        {
            decimal optionStrike = option.Strike;
            decimal currentBuyStraddleValue = CurrentBuyStraddleValue[optionStrike].Sum();
            // decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
            bool exit = false;
            tpHit = false;
            //    &&
            //    (
            //    (
            //    TradedQuantity[optionStrike][0] > 0
            //    && TradedStraddleValue[optionStrike].Count > 0
            //    &&

            //    ((currentBuyStraddleValue - TradedStraddleValue[optionStrike].Average() > _stopLoss) ||

            //    ((HistoricalStraddleAverage[optionStrike].Value > currentBuyStraddleValue + 2)
            //    && (TradedStraddleValue[optionStrike].Average() > currentBuyStraddleValue + _closeSpread)))
            //    )
            //    )
            //    )
            //{
            //    exit = true;
            //}
            if (OptionTradedQuantity[option.InstrumentToken] > 0)
            {

                if (option.LastPrice > OptionTradedPrices[option.InstrumentToken].Average() + _stopLoss)
                {
                    exit = true;
                }
                else if ((option.LastPrice < OptionTradedPrices[option.InstrumentToken].Average() - _targetPoints) && (

                true
                //((HistoricalStraddleAverage[optionStrike].Value > currentBuyStraddleValue + 2)
                //&& (TradedStraddleValue[optionStrike].Average() > currentBuyStraddleValue + _closeSpread))

                //(currentBuyStraddleValue >=  HistoricalStraddleAverage[optionStrike].Value - 2)

                ))
                {
                    tpHit = true;
                    exit = true;
                }
            }
            return exit;
        }

        //public void PlaceTempOrderForTesting()
        //{
        //    //BANKNIFTY2181836000PE
        //   Order order =  KotakMarketOrders.PlaceOrder(_algoInstance, "BANKNIFTY21AUG37500CE", "CE", 12.3m, 43735, true, 25, AlgoIndex.StrangleShiftToOneSide
        //        , orderType: "Market", product: "MIS");
        //}
        void PlaceCloseOrder(List<OrderTrio> orderTrios, bool tpHit, DateTime currentTime)
        {
            
            if(tpHit)
            {
                //cancel the SL order
                foreach (OrderTrio orderTrio in orderTrios)
                {
                    Order cancelledOrder = TradeCancel(orderTrio.SLOrder, currentTime);
                    //OnTradeExit(cancelledOrder);
                    _tradedQty = _tradedQty - Convert.ToInt32(cancelledOrder.Quantity/25);
                }
            }
            else
            {
                //cancel the tp order

                foreach (OrderTrio orderTrio in orderTrios)
                {
                    Order cancelledOrder = TradeCancel(orderTrio.TPOrder, currentTime);
                    //OnTradeExit(cancelledOrder);
                    _tradedQty = _tradedQty - Convert.ToInt32(cancelledOrder.Quantity/25);
                }
                
            }
            orderTrios = new List<OrderTrio>();
            //Order optionBuyOrder = TradeEntry(option, currentTime, qtyTobeExited, option.Strike, true);
            // //int qty = Convert.ToInt32(TradedQuantity[tradeStrike][0]);
            // Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
            // Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

            // Order callBuyOrder = null, putBuyOrder = null;

            // Task<Order> puttask = new Task<Order>(() => TradeEntry(put, currentTime, qtyTobeExited, tradeStrike, true));
            // Task<Order> calltask = new Task<Order>(() => TradeEntry(call, currentTime, qtyTobeExited, tradeStrike, true));

            // if (currentUpTrade)
            // {
            //     //callBuyOrder = TradeEntry(call, currentTime, qty, tradeStrike, true);
            //     //putBuyOrder = TradeEntry(put, currentTime, qty, tradeStrike, true);

            //     //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);
            //     //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);

            //     calltask.Start();
            //     puttask.Start();

            // }
            // else
            // {
            //     //putBuyOrder = TradeEntry(put, currentTime, qty, tradeStrike, true);
            //     //callBuyOrder = TradeEntry(call, currentTime, qty, tradeStrike, true);

            //     puttask.Start();
            //     calltask.Start();

            // }
            // Task.WaitAll(puttask, calltask);

            // if (puttask.IsCompleted && calltask.IsCompleted)
            // {
            //     putBuyOrder = puttask.Result;
            //     callBuyOrder = calltask.Result;
            // }

            // //if (qty > 0)
            // //{

            // //Order callBuyOrder = MarketOrders.PlaceOrder(_algoInstance, call.TradingSymbol, call.InstrumentType, call.LastPrice,
            // //     MappedTokens[call.InstrumentToken], true, Convert.ToInt32(qty * call.LotSize),
            // //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

            // ////Order callBuyOrder = MarketOrders.PlaceOrder(_algoInstance, call.TradingSymbol, call.InstrumentType, call.LastPrice,
            // ////     call.InstrumentToken, true, Convert.ToInt32(qty * call.LotSize),
            // ////    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade, broker: Constants.ZERODHA);

            // //Order putBuyOrder = MarketOrders.PlaceOrder(_algoInstance, put.TradingSymbol, put.InstrumentType, put.LastPrice,
            // //    MappedTokens[put.InstrumentToken], true, Convert.ToInt32(qty * put.LotSize),
            // //   algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

            // //Order putBuyOrder = MarketOrders.PlaceOrder(_algoInstance, put.TradingSymbol, put.InstrumentType, put.LastPrice,
            // //    put.InstrumentToken, true, Convert.ToInt32(qty * put.LotSize),
            // //   algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade, broker: Constants.ZERODHA);

            // OnTradeExit(callBuyOrder);
            //     OnTradeExit(putBuyOrder);

            //     //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Closed Straddle: CE: {0}, PE: {1}, Total: {2}",
            //     //Math.Round(callBuyOrder.AveragePrice, 1), Math.Round(putBuyOrder.AveragePrice, 1), Math.Round(callBuyOrder.AveragePrice + putBuyOrder.AveragePrice, 1)), "Trade Option");
            //// }

            //TradedStraddleValue[tradeStrike] = new List<decimal>();
            //TradedQuantity[tradeStrike][0] -= qtyTobeExited;
            ////TradedQuantity[tradeStrike][1] = -1;
            //LastTradeTime[tradeStrike] = null;
            ////CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] = callBuyOrder.AveragePrice;
            //CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] = putBuyOrder.AveragePrice;



            //foreach (ReferenceTradeStrikeKey referenceTradeStrike in referenceTradeStrikeKeys)
            //{
            //    if(referenceTradeStrike.TradeStrike == tradeStrike &&  !triggerStrikes.Contains(referenceTradeStrike.ReferenceStrike))
            //    {
            //        ReferenceTradedStraddleValue.Remove(referenceTradeStrike);
            //    }
            //}
            //referenceTradeStrikeKeys.RemoveAll(x => x.TradeStrike == tradeStrike && !triggerStrikes.Contains(x.ReferenceStrike));

            //TradeReferenceStrikes[tradeStrike] = new List<decimal>();
            //}
        }


        private Order TradeCancel(Order order, DateTime currentTime)
        {
            Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS, httpClient: _httpClient);
            //CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, string orderId, DateTime currentTime, string tradingSymbol)
            OnTradeExit(cancelOrder);
            return cancelOrder;
        }
        private void CancelSLOrders (decimal tradeStrike, DateTime currentTime)
        {
            //Cancel all original super multiple orders
            List<Order> orders = CallOrdersTradeStrike.Where(x => x.Value == tradeStrike).Select(x => x.Key).ToList();

            //for(int i=0;i < orders.Count;)
            //{
            //    Order order = orders[i];
            //    Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, product: Constants.PRODUCT_SM, currentTime);
            //    //CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, string orderId, DateTime currentTime, string tradingSymbol)
            //    orders.Remove(order);
            //    OnTradeEntry(cancelOrder);
            //}


            foreach (Order order in orders)
            {
                Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS, _httpClient);
                //CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, string orderId, DateTime currentTime, string tradingSymbol)
                CallOrdersTradeStrike.Remove(order);
                OnTradeEntry(cancelOrder);
            }


            //Cancel all original super multiple orders
            orders = PutOrdersTradeStrike.Where(x => x.Value == tradeStrike).Select(x => x.Key).ToList();
            foreach (Order order in orders)
            {
                Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS, _httpClient);
                PutOrdersTradeStrike.Remove(order);
                OnTradeEntry(cancelOrder);
            }
        }
        private Order TradeEntry (Instrument option, DateTime currentTime, int qtyInlots, decimal triggerOptionStrike, 
            bool buyOrder, string orderType = Constants.ORDER_TYPE_MARKET, decimal limitPrice = 0, decimal triggerPrice = 0)
        {
            Order optionOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, limitPrice,
               MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
                algoIndex, currentTime, orderType, Tag: triggerOptionStrike.ToString(), triggerPrice:triggerPrice, broker: Constants.KOTAK, httpClient: _httpClient);

            //Order optionSellOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
            //   option.InstrumentToken, false, _stepQty * Convert.ToInt32(option.LotSize),
            //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.ZERODHA);

            optionOrder.OrderTimestamp = currentTime;
            optionOrder.ExchangeTimestamp = currentTime;

            return optionOrder;
        }
        private Order TradeSLEntry(Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, bool buyOrder, decimal triggerPrice)
        {
            //Order optionOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
            //   MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
            //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

            ////Order optionSellOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
            ////   option.InstrumentToken, false, _stepQty * Convert.ToInt32(option.LotSize),
            ////    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.ZERODHA);

            //optionOrder.OrderTimestamp = DateTime.Now;
            //optionOrder.ExchangeTimestamp = DateTime.Now;

                Order stopLossOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, triggerPrice, MappedTokens[option.InstrumentToken], buyOrder,
                    qtyInlots * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT, Tag: currentUpTrade.ToString(), triggerPrice:triggerPrice, broker: Constants.KOTAK);

                //Order optionSellOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                //   option.InstrumentToken, false, _stepQty * Convert.ToInt32(option.LotSize),
                //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.ZERODHA);

                stopLossOrder.OrderTimestamp = DateTime.Now;
                stopLossOrder.ExchangeTimestamp = DateTime.Now;
            //else
            //{
            //    stopLossOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, stopLossOrder, DateTime.Now, product: Constants.PRODUCT_MIS);
            //    //OnTradeEntry(stopLossOrder);
            //}

            return stopLossOrder;
        }
        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 12, 00))
            {
                if (_tradedQty > 0)
                {
                    var localTradedOptions = new Dictionary<uint, int>(OptionTradedQuantity);
                    foreach (var element in localTradedOptions.Where(x => x.Value > 0))
                    {
                        List<OrderTrio> orderTrios = OptionUniverse[element.Key].InstrumentType == "ce" 
                            ? _callOrderTrioList[OptionUniverse[element.Key].Strike] : _putOrderTrioList[OptionUniverse[element.Key].Strike];
                        PlaceCloseOrder(orderTrios, true, currentTime);
                        PlaceCloseOrder(orderTrios, false, currentTime);
                    }
                }

                _stopTrade = true;
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                        "Completed", "TriggerEODPositionClose");
            }
        }

        public void StopTrade()
        {
            _stopTrade = true;
        }

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (StraddleUniverse == null || StraddleUniverse.Keys.Max() < _baseInstrumentPrice + 400 || StraddleUniverse.Keys.Min() > _baseInstrumentPrice - 400)
                {
                    DataLogic dl = new DataLogic();
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, Instrument> allOptions;
                    Dictionary<uint, uint> mTokens;

                    var tempStraddleUniverse = dl.LoadCloseByStraddleOptions(_expiry, _baseInstrumentToken, _baseInstrumentPrice, 
                        maxDistanceFromBInstrument:700, out allOptions, out mTokens);

                    if(StraddleUniverse == null)
                    {
                        StraddleUniverse = tempStraddleUniverse;
                        OptionUniverse = allOptions;
                        MappedTokens = mTokens;
                        MappedTokens.TryAdd(marginCallBuyInstrument.InstrumentToken, marginCallBuyInstrument.KToken);
                        MappedTokens.TryAdd(marginPutBuyInstrument.InstrumentToken, marginPutBuyInstrument.KToken);
                        
                        foreach (var item in allOptions)
                        {
                            OptionTradedPrices.TryAdd(item.Key, new List<decimal>());
                            OptionTradedQuantity.TryAdd(item.Key, 0);
                            HistoricalOptionPrice.TryAdd(item.Key, new FixedSizedQueue<decimal>());
                        }
                    }
                    else
                    {
                        foreach(var item in tempStraddleUniverse)
                        {
                            StraddleUniverse.TryAdd(item.Key, item.Value);
                        }
                        foreach (var item in allOptions)
                        {
                            OptionUniverse.TryAdd(item.Key, item.Value);
                            OptionTradedPrices.TryAdd(item.Key, new List<decimal>());
                            OptionTradedQuantity.TryAdd(item.Key, 0);
                            HistoricalOptionPrice.TryAdd(item.Key, new FixedSizedQueue<decimal>());
                        }
                        foreach (var item in mTokens)
                        {
                            MappedTokens.TryAdd(item.Key, item.Value);
                        }
                    }

                    foreach (decimal strike in StraddleUniverse.Keys)
                    {
                        CurrentSellStraddleValue.TryAdd(strike, new decimal[] { 0, 0 });
                        CurrentBuyStraddleValue.TryAdd(strike, new decimal[] { 0, 0 });
                        TradedStraddleValue.TryAdd(strike, new List<decimal>());
                        ReferenceStraddleValue.TryAdd(strike, new List<decimal>());
                        TradeReferenceStrikes.TryAdd(strike, new List<decimal>());
                        TradedQuantity.TryAdd(strike, new decimal[] { 0, -1 });
                        HistoricalStraddleAverage.TryAdd(strike, new FixedSizedQueue<decimal>());
                        LastStraddleValue.TryAdd(strike, 0);
                        LastTradeTime.TryAdd(strike, null);
                        _callOrderTrioList.TryAdd(strike, new List<OrderTrio>());
                        _putOrderTrioList.TryAdd(strike, new List<OrderTrio>());
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
                if (StraddleUniverse != null)
                {
                    foreach(var option in OptionUniverse)
                    {
                        if (!SubscriptionTokens.Contains(option.Key))
                        {
                            SubscriptionTokens.Add(option.Key);
                            dataUpdated = true;
                        }
                    }
                    //foreach (var spreads in SpreadUniverse)
                    //{
                    //    foreach (var option in spreads.Value)
                    //    {
                    //        if (!SubscriptionTokens.Contains(option[0].InstrumentToken))
                    //        {
                    //            SubscriptionTokens.Add(option[0].InstrumentToken);
                    //            dataUpdated = true;
                    //        }
                    //        if (!SubscriptionTokens.Contains(option[1].InstrumentToken))
                    //        {
                    //            SubscriptionTokens.Add(option[1].InstrumentToken);
                    //            dataUpdated = true;
                    //        }
                    //    }
                    //}
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

        public Task<bool> OnNext(Tick tick)
        {
            try
            {
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    return Task.FromResult(false);
                }
                ActiveTradeIntraday(tick);
                return Task.FromResult(true);
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

    public class ReferenceTradeStrikeKey
    {
        public ReferenceTradeStrikeKey(decimal referenceStrike, decimal tradeStrike)
        {
            ReferenceStrike = referenceStrike;
            TradeStrike = tradeStrike;
        }

        public decimal ReferenceStrike { get; }
        public decimal TradeStrike { get; }

        public override bool Equals(object obj)
        {
            ReferenceTradeStrikeKey rts = obj as ReferenceTradeStrikeKey;

            if (rts == null)
            {
                return false;
            }

            return (rts.ReferenceStrike == ReferenceStrike && rts.TradeStrike == TradeStrike);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
