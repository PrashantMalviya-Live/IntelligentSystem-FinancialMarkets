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
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ZMQFacade;

namespace Algorithms.Algorithms
{
    public class StraddleSpreadValueScalping : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public Dictionary<uint, Instrument> OptionUniverse { get; set; }
        public Dictionary<uint, uint> MappedTokens { get; set; }

        public SortedList<decimal, Instrument[]> StraddleUniverse { get; set; }
        public Dictionary<decimal, FixedSizedQueue<decimal>> HistoricalStraddleAverage { get; set; }
        public Dictionary<decimal, List<decimal>> TradedStraddleValue { get; set; }
        //Count and straddle value for each strike price. This is used to determine the average and limit the trade per strike price.
        public Dictionary<ReferenceTradeStrikeKey, List<decimal>> ReferenceTradedStraddleValue { get; set; }
        List<ReferenceTradeStrikeKey> referenceTradeStrikeKeys;
        //Stores traded qty and up or downtrade bool = 1 means uptrade

        //All order ids for a particular trade strike
        private Dictionary<Order, decimal> CallOrdersTradeStrike { get; set; }
        private Dictionary<Order, decimal> PutOrdersTradeStrike { get; set; }

        private Dictionary<decimal, bool> _straddleRefreshed;
        public Dictionary<decimal, decimal[]> TradedQuantity { get; set; }
        public Dictionary<decimal, List<decimal>> TradeReferenceStrikes { get; set; }

        private int _thresholdQtyForBuy = 21;
        private int _buyQty = 5, _boughtQty;
        private Instrument marginPutBuyInstrument;
        private Instrument marginCallBuyInstrument;
        //Strike and time order
        private Dictionary<decimal, DateTime?> LastTradeTime { get; set; }

        public Dictionary<decimal, decimal[]> CurrentSellStraddleValue { get; set; }
        public Dictionary<decimal, decimal[]> CurrentBuyStraddleValue { get; set; }
        
        public SortedList<decimal, BS[]> SpreadBSUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(StraddleSpreadValueScalping source);
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
        private bool _upTrade;
        public const AlgoIndex algoIndex = AlgoIndex.ValueSpreadTrade;
        Dictionary<uint, List<Candle>> TimeCandles;
        private object lockObject = new object();
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public StraddleSpreadValueScalping(DateTime endTime, uint baseInstrumentToken,
            DateTime? expiry, int quantity, int maxQty, int stepQty, decimal targetProfit, 
            decimal openSpread, decimal closeSpread, decimal stopLoss,
            int algoInstance = 0, bool positionSizing = false, decimal maxLossPerTrade = 0, double timeBandForExit = 0)
        {
            _expiry = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _targetProfit = targetProfit;
            _stopLoss = stopLoss;
            _openSpread = openSpread;
            _closeSpread = closeSpread;
            _stopTrade = true;
            _maxQty = maxQty;
            _stepQty = stepQty;
            SubscriptionTokens = new List<uint>();
            HistoricalStraddleAverage = new Dictionary<decimal, FixedSizedQueue<decimal>>();  
            CurrentSellStraddleValue = new Dictionary<decimal, decimal[]>();
            CurrentBuyStraddleValue = new Dictionary<decimal, decimal[]>();
            TradedStraddleValue = new Dictionary<decimal, List<decimal>>();
            ReferenceTradedStraddleValue = new Dictionary<ReferenceTradeStrikeKey, List<decimal>>();
            TradedQuantity = new Dictionary<decimal, decimal[]>();
            TradeReferenceStrikes = new Dictionary<decimal, List<decimal>>();
            LastTradeTime = new Dictionary<decimal, DateTime?>();
            ActiveOptions = new List<Instrument>();
            OptionUniverse = new Dictionary<uint, Instrument>();
            _tradeQty = quantity;
            referenceTradeStrikeKeys = new List<ReferenceTradeStrikeKey>();
            CallOrdersTradeStrike = new Dictionary<Order, decimal>();
            PutOrdersTradeStrike = new Dictionary<Order, decimal>();
            _straddleRefreshed = new Dictionary<decimal, bool>();
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                _expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins: 0, Arg1: _openSpread, Arg2: _closeSpread,
                Arg3: _targetProfit, Arg4: _stopLoss);

            ZConnect.Login();
            KoConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            DataLogic dl = new DataLogic();
            marginPutBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 42000, "ce");
            marginCallBuyInstrument = dl.GetInstrument(_expiry.Value, _baseInstrumentToken, 37500, "pe");
            _buyQty = Math.Max(_stepQty, _buyQty);
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
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


                    ///Step1: Take the tick and update straddle value - put a loaded flag, so that you know it is updated.
                    ///Step2: For each tick, check which all strikes are breaking out and then accordingly sell the straddle
                    ///for each further sell, check if there is incremental values, then only sell it.
                    ///for each closure, check if it is going to reopen, then dont close it, but update the number of traded quantity so that it can be based on latest breakouts.

                    if (tick.LastTradeTime.HasValue)
                    {
                        Instrument option = OptionUniverse[tick.InstrumentToken];
                        //Update straddle value
                        decimal optionStrike = option.Strike;
                        UpdateStraddleValue(tick, option);

                        bool currentUpTrade = optionStrike < _baseInstrumentPrice;
                        decimal currentBuyStraddleValue = CurrentBuyStraddleValue[optionStrike].Sum();
                        decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();

                        if (isExitTrigerred(optionStrike))
                        {
                            //here you have to check if the trigger option prices have stabilzied and not the trade strike price as it is check below
                            int qtytobeExited = Convert.ToInt32(TradedQuantity[optionStrike][0]) - QuantityToBeTraded(optionStrike);
                            if (qtytobeExited > 0)
                            {
                                PlaceCloseOrder(optionStrike, qtytobeExited, currentTime, ((currentBuyStraddleValue - TradedStraddleValue[optionStrike].Average() > _stopLoss) ? currentUpTrade : !currentUpTrade));

                                if ((_tradedQty <= _thresholdQtyForBuy + _boughtQty - _buyQty) && _boughtQty > 0)
                                {
                                    TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, false);
                                    TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, false);
                                    _boughtQty -= _buyQty;
                                }
                            }
                            else
                            {
                                TradedStraddleValue[optionStrike] = new List<decimal>();
                                TradedStraddleValue[optionStrike].Add(currentSellStraddleValue);
                                LastTradeTime[optionStrike] = currentTime;

                                //CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] = callBuyOrder.AveragePrice;
                                //CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] = putBuyOrder.AveragePrice;
                            }

                            //Let current buy straddle fill again after every check.
                            //_straddleRefreshed[optionStrike] = false;
                            //CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] = 0;
                            //CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] = 0;
                        }

                        //bool straddlesRefreshed = AreStraddlesUpdated(true);
                        //if (straddlesRefreshed)
                        //{
                        //    decimal tradeStrike = GetTradeStrike(optionStrike);
                        //    InitiateEntryLogic(tradeStrike, optionStrike, currentTime);
                        //    ClearCurrentStraddleValues(true);
                        //}
                        // straddlesRefreshed = AreStraddlesUpdated(false);
                        //if (straddlesRefreshed)
                        //{
                        //    decimal tradeStrike = GetTradeStrike(optionStrike);
                        //    InitiateEntryLogic(tradeStrike, optionStrike, currentTime);
                        //    ClearCurrentStraddleValues(false);
                        //}
                        bool straddlesRefreshed = AreStraddlesUpdated();
                        if (straddlesRefreshed)
                        {
                            
                            InitiateEntryLogic(optionStrike, currentTime);
                            ClearCurrentStraddleValues();
                        }
                    }


                    #region Commented Code
                    //                        // if ((CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                        //&& CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                        //// && (CurrentBuyStraddleValue.ContainsKey(tradeStrike) && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] != 0
                    //                        //// && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] != 0)
                    //                        //// && (CurrentBuyStraddleValue.ContainsKey(optionStrike) && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                        ////&& CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                        //)
                    //                        // {
                    //                        // decimal currentSellStraddleValue = CurrentSellStraddleValue[option.Strike].Sum();
                    //                        ReferenceTradeStrikeKey referenceTradeStrikeKey = referenceTradeStrikeKeys.FirstOrDefault(x => x.ReferenceStrike == optionStrike && x.TradeStrike == tradeStrike);

                    //                                if (
                    //                                    (option.Strike <= _baseInstrumentPrice + _strikePriceRange && option.Strike >= _baseInstrumentPrice - _strikePriceRange)
                    //                                    && (tradeStrike <= _baseInstrumentPrice + _strikePriceRange && tradeStrike >= _baseInstrumentPrice - _strikePriceRange)
                    //                                    && TradedQuantity[tradeStrike][0] < _stepQty * 5
                    //                                    && (HistoricalStraddleAverage[option.Strike].Value != 0)
                    //                                    && (_tradedQty < _maxQty - _stepQty)
                    //                                    && (currentTime.TimeOfDay <= new TimeSpan(14, 40, 00))
                    //                                    //&& TradedStraddleValue[option.Strike].Count > 0
                    //                                    &&

                    //                                    (((referenceTradeStrikeKey == null) && ((currentSellStraddleValue - HistoricalStraddleAverage[option.Strike].Value) >= _openSpread))
                    //                                    || ((referenceTradeStrikeKey != null) && (ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() < 5)))
                    //                                    //The below line is not working becuase we have to check trade strike straddle value with current trade strike value
                    //                                    && ((TradedStraddleValue[option.Strike].Count == 0) || ((currentSellStraddleValue - (TradedStraddleValue[option.Strike].Last() + 10)) >= _openSpread))

                    //                                    //((currentSellStraddleValue - (referenceTradeStrikeKey == null ? HistoricalStraddleAverage[option.Strike].Value :
                    //                                    //(ReferenceTradedStraddleValue[referenceTradeStrikeKey].Last() + 10))) >= _openSpread)
                    //                                    //&& (referenceTradeStrikeKey == null || ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() <= 3)

                    //                                    //&&(optionType == 0 ? option.Strike <= _baseInstrumentPrice + 100 && option.Strike >= _baseInstrumentPrice - 100m 
                    //                                    //: option.Strike >= _baseInstrumentPrice - 100 && option.Strike <= _baseInstrumentPrice + 100m)

                    //                                    //) //|| _upTrade != currentUpTrade)
                    //                                    //&& (_activeOrders == null || _activeOrders[(int)InstrumentType.CE].InstrumentToken == call.InstrumentToken
                    //                                    //|| _activeOrders[(int)InstrumentType.PE].InstrumentToken == put.InstrumentToken)
                    //                                    )
                    //                                {
                    //                                    Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
                    //                                    Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

                    //                                    Order callSellOrder = null, putSellOrder = null, callStopLossOrder, putStopLossOrder, callBuyOrder, putBuyOrder;

                    //                                    Task<Order> puttask = new Task<Order>(() => TradeEntry(put, currentTime, _stepQty, optionStrike, false));
                    //                                    Task<Order> calltask = new Task<Order>(() => TradeEntry(call, currentTime, _stepQty, optionStrike, false));

                    //                                    if (currentUpTrade)
                    //                                    {
                    //                                        //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);
                    //                                        //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);

                    //                                        puttask.Start();
                    //                                        calltask.Start();
                    //                                        //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                    //                                        //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                    //                                    }
                    //                                    else
                    //                                    {
                    //                                        //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);
                    //                                        //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);

                    //                                        calltask.Start();
                    //                                        puttask.Start();

                    //                                        //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                    //                                        //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                    //                                    }

                    //                                    Task.WaitAll(puttask, calltask);
                    //                                    if (puttask.IsCompleted && calltask.IsCompleted)
                    //                                    {
                    //                                        putSellOrder = puttask.Result;
                    //                                        callSellOrder = calltask.Result;
                    //                                    }

                    //                                    _tradedQty = _tradedQty + _stepQty;

                    //                                    //Far OTM buy to reduce margin
                    //                                    if (_tradedQty > _thresholdQtyForBuy + _boughtQty)
                    //                                    {
                    //                                        putBuyOrder = TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //                                        callBuyOrder = TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //                                        _boughtQty += _buyQty;
                    //                                    }


                    //                                    //CallOrdersTradeStrike.TryAdd(callStopLossOrder, tradeStrike);
                    //                                    //PutOrdersTradeStrike.TryAdd(putStopLossOrder, tradeStrike);

                    //                                    OnTradeEntry(callSellOrder);
                    //                                    OnTradeEntry(putSellOrder);

                    //                                    straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;

                    //                                    TradedStraddleValue[tradeStrike].Add(straddleValue);
                    //                                    TradedQuantity[tradeStrike][0] += _stepQty;
                    //                                    //TradedQuantity[tradeStrike][1] = _upTrade ? 1 : 0;
                    //                                    TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                    //                                    //ReferenceStraddleValue[option.Strike].Add(currentSellStraddleValue);
                    //                                    if (referenceTradeStrikeKey == null)
                    //                                    {
                    //                                        referenceTradeStrikeKey = new ReferenceTradeStrikeKey(optionStrike, tradeStrike);
                    //                                        referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //                                    }

                    //                                    if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                    //                                    {
                    //                                        ReferenceTradedStraddleValue[referenceTradeStrikeKey].Add(currentSellStraddleValue);
                    //                                    }
                    //                                    else
                    //                                    {
                    //                                        ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { currentSellStraddleValue });
                    //                                    }


                    //                                    TradeReferenceStrikes[tradeStrike].Add(option.Strike);
                    //                                    LastTradeTime[tradeStrike] = currentTime;
                    //                                    //callSellOrder.UpOrder = _upTrade;
                    //                                    //putSellOrder.UpOrder = _upTrade;
                    //                                    //_activeOrders.Add(callSellOrder);
                    //                                    //_activeOrders.Add(putSellOrder);

                    //                                    //Close all uptrades if down trade is trigerred and vice versa
                    //                                    //CloseAllTrades(_upTrade, currentTime);


                    //                                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
                    //                                        Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");
                    //                                }

                    //                                //HistoricalStraddleAverage[option.Strike] = HistoricalStraddleAverage[option.Strike].Value == 0 ? currentSellStraddleValue : (HistoricalStraddleAverage[option.Strike] * 1800 + currentSellStraddleValue) / 1801;
                    //                                HistoricalStraddleAverage[option.Strike].Enqueue(currentSellStraddleValue);
                    //                                CurrentSellStraddleValue[option.Strike][(int)InstrumentType.CE] = 0;
                    //                                CurrentSellStraddleValue[option.Strike][(int)InstrumentType.PE] = 0;
                    //                           // }




                    //                        }


                    //                        //bool currentUpTrade;
                    //                        //if (tradeStrike > _baseInstrumentPrice)
                    //                        //{
                    //                        //    currentUpTrade = true;
                    //                        //}
                    //                        //else if (tradeStrike < _baseInstrumentPrice)
                    //                        //{
                    //                        //    currentUpTrade = false;
                    //                        //}





                    //                        //bool straddleRefreshed = true;


                    //                        //todo: code a way to update both pe and ce before using the data.This may help some slippages in live environment.
                    //                        //if (CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] != 0 && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                        //if(straddleRefreshed)
                    //                        //{

















                    //                            // }

                    //                            //decimal currentBuyStraddleValue = CurrentBuyStraddleValue[tradeStrike].Sum();

                    //                            //if (_tradedQty > 0
                    //                            //    &&
                    //                            //    (
                    //                            //    (

                    //                            //    //&& ReferenceStraddleValue[option.Strike].Average() > currentBuyStraddleValue + 2
                    //                            //    TradedQuantity.ContainsKey(tradeStrike) && TradedQuantity[tradeStrike][0] > 0
                    //                            //    && TradedStraddleValue.ContainsKey(tradeStrike) && TradedStraddleValue[tradeStrike].Count > 0
                    //                            //    && ((currentBuyStraddleValue - TradedStraddleValue[tradeStrike].Average() > _stopLoss) ||

                    //                            //    ((HistoricalStraddleAverage.ContainsKey(tradeStrike) && HistoricalStraddleAverage[tradeStrike].Value > currentBuyStraddleValue + 2)
                    //                            //    && TradedStraddleValue[tradeStrike].Average() > currentBuyStraddleValue + _closeSpread))

                    //                            //    )

                    //                            //    //|| (LastTradeTime[tradeStrike].HasValue && currentTime.TimeOfDay >= new TimeSpan(16, 00, 00)
                    //                            //    //&& currentTime.Subtract(LastTradeTime[tradeStrike].Value).TotalMinutes > _timeBandForExit)
                    //                            //    )
                    //                            //    )
                    //                            //{
                    //                            //    PlaceCloseOrder(tradeStrike, currentTime, currentUpTrade);

                    //                            //    if ((_tradedQty <= _thresholdQtyForBuy + _boughtQty - _buyQty) && _boughtQty > 0)
                    //                            //    {
                    //                            //        TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //                            //        TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //                            //        _boughtQty -= _buyQty;
                    //                            //    }
                    //                            //}
                    //                        //}

                    //                        if ((CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                            && CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                            // && (CurrentBuyStraddleValue.ContainsKey(tradeStrike) && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] != 0
                    //                            // && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] != 0)
                    //                            // && (CurrentBuyStraddleValue.ContainsKey(optionStrike) && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                            //&& CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                            )
                    //                        {
                    //                            decimal currentSellStraddleValue = CurrentSellStraddleValue[option.Strike].Sum();
                    //                            ReferenceTradeStrikeKey referenceTradeStrikeKey = referenceTradeStrikeKeys.FirstOrDefault(x => x.ReferenceStrike == optionStrike && x.TradeStrike == tradeStrike);

                    //                            if (
                    //                                (option.Strike <= _baseInstrumentPrice + _strikePriceRange && option.Strike >= _baseInstrumentPrice - _strikePriceRange)
                    //                                && (tradeStrike <= _baseInstrumentPrice + _strikePriceRange && tradeStrike >= _baseInstrumentPrice - _strikePriceRange)
                    //                                && TradedQuantity[tradeStrike][0] < _stepQty * 5
                    //                                && (HistoricalStraddleAverage[option.Strike].Value != 0)
                    //                                && (_tradedQty < _maxQty - _stepQty)
                    //                                && (currentTime.TimeOfDay <= new TimeSpan(14, 40, 00))
                    //                                //&& TradedStraddleValue[option.Strike].Count > 0
                    //                                &&

                    //                                (((referenceTradeStrikeKey == null) && ((currentSellStraddleValue - HistoricalStraddleAverage[option.Strike].Value) >= _openSpread))
                    //                                || ((referenceTradeStrikeKey != null) && (ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() < 5)))
                    //                                //The below line is not working becuase we have to check trade strike straddle value with current trade strike value
                    //                                && ((TradedStraddleValue[option.Strike].Count == 0) || ((currentSellStraddleValue - (TradedStraddleValue[option.Strike].Last() + 10)) >= _openSpread))

                    //                                //((currentSellStraddleValue - (referenceTradeStrikeKey == null ? HistoricalStraddleAverage[option.Strike].Value :
                    //                                //(ReferenceTradedStraddleValue[referenceTradeStrikeKey].Last() + 10))) >= _openSpread)
                    //                                //&& (referenceTradeStrikeKey == null || ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() <= 3)

                    //                                //&&(optionType == 0 ? option.Strike <= _baseInstrumentPrice + 100 && option.Strike >= _baseInstrumentPrice - 100m 
                    //                                //: option.Strike >= _baseInstrumentPrice - 100 && option.Strike <= _baseInstrumentPrice + 100m)

                    //                                //) //|| _upTrade != currentUpTrade)
                    //                                //&& (_activeOrders == null || _activeOrders[(int)InstrumentType.CE].InstrumentToken == call.InstrumentToken
                    //                                //|| _activeOrders[(int)InstrumentType.PE].InstrumentToken == put.InstrumentToken)
                    //                                )
                    //                            {
                    //                                Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
                    //                                Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

                    //                                Order callSellOrder = null, putSellOrder = null, callStopLossOrder, putStopLossOrder, callBuyOrder, putBuyOrder;

                    //                                Task<Order> puttask = new Task<Order>(() => TradeEntry(put, currentTime, _stepQty, optionStrike, false));
                    //                                Task<Order> calltask = new Task<Order>(() => TradeEntry(call, currentTime, _stepQty, optionStrike, false));

                    //                                if (currentUpTrade)
                    //                                {
                    //                                    //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);
                    //                                    //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);

                    //                                    puttask.Start();
                    //                                    calltask.Start();
                    //                                    //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                    //                                    //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                    //                                }
                    //                                else
                    //                                {
                    //                                    //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);
                    //                                    //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);

                    //                                    calltask.Start();
                    //                                    puttask.Start();

                    //                                    //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                    //                                    //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                    //                                }

                    //                                Task.WaitAll(puttask, calltask);
                    //                                if (puttask.IsCompleted && calltask.IsCompleted)
                    //                                {
                    //                                    putSellOrder = puttask.Result;
                    //                                    callSellOrder = calltask.Result;
                    //                                }

                    //                                _tradedQty = _tradedQty + _stepQty;

                    //                                //Far OTM buy to reduce margin
                    //                                if (_tradedQty > _thresholdQtyForBuy + _boughtQty)
                    //                                {
                    //                                    putBuyOrder = TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //                                    callBuyOrder = TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //                                    _boughtQty += _buyQty;
                    //                                }


                    //                                //CallOrdersTradeStrike.TryAdd(callStopLossOrder, tradeStrike);
                    //                                //PutOrdersTradeStrike.TryAdd(putStopLossOrder, tradeStrike);

                    //                                OnTradeEntry(callSellOrder);
                    //                                OnTradeEntry(putSellOrder);

                    //                                straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;

                    //                                TradedStraddleValue[tradeStrike].Add(straddleValue);
                    //                                TradedQuantity[tradeStrike][0] += _stepQty;
                    //                                //TradedQuantity[tradeStrike][1] = _upTrade ? 1 : 0;
                    //                                TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                    //                                //ReferenceStraddleValue[option.Strike].Add(currentSellStraddleValue);
                    //                                if (referenceTradeStrikeKey == null)
                    //                                {
                    //                                    referenceTradeStrikeKey = new ReferenceTradeStrikeKey(optionStrike, tradeStrike);
                    //                                    referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //                                }

                    //                                if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                    //                                {
                    //                                    ReferenceTradedStraddleValue[referenceTradeStrikeKey].Add(currentSellStraddleValue);
                    //                                }
                    //                                else
                    //                                {
                    //                                    ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { currentSellStraddleValue });
                    //                                }


                    //                                TradeReferenceStrikes[tradeStrike].Add(option.Strike);
                    //                                LastTradeTime[tradeStrike] = currentTime;
                    //                                //callSellOrder.UpOrder = _upTrade;
                    //                                //putSellOrder.UpOrder = _upTrade;
                    //                                //_activeOrders.Add(callSellOrder);
                    //                                //_activeOrders.Add(putSellOrder);

                    //                                //Close all uptrades if down trade is trigerred and vice versa
                    //                                //CloseAllTrades(_upTrade, currentTime);


                    //                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
                    //                                    Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");
                    //                            }

                    //                            //HistoricalStraddleAverage[option.Strike] = HistoricalStraddleAverage[option.Strike].Value == 0 ? currentSellStraddleValue : (HistoricalStraddleAverage[option.Strike] * 1800 + currentSellStraddleValue) / 1801;
                    //                            HistoricalStraddleAverage[option.Strike].Enqueue(currentSellStraddleValue);
                    //                            CurrentSellStraddleValue[option.Strike][(int)InstrumentType.CE] = 0;
                    //                            CurrentSellStraddleValue[option.Strike][(int)InstrumentType.PE] = 0;
                    //                        }
                    //                    }



                    //                        if (tick.LastTradeTime.HasValue)
                    //                    {
                    ////                        Instrument option = OptionUniverse[token];

                    ////                        int optionType = option.InstrumentType.Trim(' ').ToLower() == "ce" ? (int)InstrumentType.CE : (int)InstrumentType.PE;

                    ////                        decimal optionStrike = option.Strike;
                    ////#if market
                    ////                        CurrentSellStraddleValue [optionStrike][optionType] = tick.Bids[0].Price;
                    ////                        CurrentBuyStraddleValue [optionStrike][optionType] = tick.Offers[0].Price;
                    ////#elif local
                    ////                        CurrentSellStraddleValue[option.Strike][optionType] = tick.LastPrice;
                    ////                        CurrentBuyStraddleValue[option.Strike][optionType] = tick.LastPrice;
                    ////#endif
                    ////                        option.LastPrice = tick.LastPrice;
                    ////                        decimal straddleValue = 0;


                    //                        //decimal tradeStrike = _baseInstrumentPrice + ((_baseInstrumentPrice - optionStrike) > 300 ? ((_baseInstrumentPrice - optionStrike) - 200) : 
                    //                        //    (_baseInstrumentPrice - optionStrike) > 100 ? ((_baseInstrumentPrice - optionStrike) - 100) : (((_baseInstrumentPrice - optionStrike) < - 300) ? : ((_baseInstrumentPrice - optionStrike) + 200) :
                    //                        //    ((_baseInstrumentPrice - optionStrike) + 100))));



                    //                        ///TODO: Delta could be used to determine the range later.

                    //                        //decimal delta = 0;

                    //                        //if ((_baseInstrumentPrice - optionStrike) > Math.Min(300, _strikePriceRange))
                    //                        //{
                    //                        //    delta = 0;//50
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) > 200)
                    //                        //{
                    //                        //    delta = 70;
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) > 90)
                    //                        //{
                    //                        //    delta = 130;
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) > 0)
                    //                        //{
                    //                        //    delta = 150;// _baseInstrumentPrice - optionStrike;
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) < -1 * Math.Min(300, _strikePriceRange))
                    //                        //{
                    //                        //    delta = 0; //-50
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) < -200)
                    //                        //{
                    //                        //    delta = -70;
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) < -90)
                    //                        //{
                    //                        //    delta = -130;
                    //                        //}
                    //                        //else if ((_baseInstrumentPrice - optionStrike) < 0)
                    //                        //{
                    //                        //    delta = -150;// (_baseInstrumentPrice - optionStrike);
                    //                        //}
                    //                        //else
                    //                        //{
                    //                        //    delta = 0;
                    //                        //}


                    //                        //decimal tradeStrike = _baseInstrumentPrice + delta;

                    //                        //tradeStrike = Math.Round(tradeStrike / 100, 0) * 100;

                    //                        bool currentUpTrade = _upTrade;
                    //                        if (tradeStrike > _baseInstrumentPrice)
                    //                        {
                    //                            //_upTrade = true;
                    //                            currentUpTrade = true;
                    //                            //Limit trade to within 200 points from base instrument
                    //                            //tradeStrike = tradeStrike - _baseInstrumentPrice > 200 ? _baseInstrumentPrice + 200 : tradeStrike;
                    //                        }
                    //                        else if (tradeStrike < _baseInstrumentPrice)
                    //                        {
                    //                            //_upTrade = false;
                    //                            currentUpTrade = false;
                    //                            //Limit trade to within 200 points from base instrument
                    //                            //tradeStrike = _baseInstrumentPrice - tradeStrike > 200 ? _baseInstrumentPrice - 200 : tradeStrike;
                    //                        }

                    //                        if (
                    //                        //&& (CurrentBuyStraddleValue.ContainsKey(tradeStrike) && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] != 0
                    //                        //&& CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] != 0)
                    //                        (CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                       && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)

                    //                       )
                    //                        {
                    //                            decimal currentBuyStraddleValue = CurrentBuyStraddleValue[option.Strike].Sum();
                    //                            decimal currentSellStraddleValue = CurrentSellStraddleValue[option.Strike].Sum();
                    //                            //bool slHit = currentBuyStraddleValue - TradedStraddleValue[option.Strike].Average() > _stopLoss;
                    //                            //bool targetHit = TradedStraddleValue[option.Strike].Average() > currentBuyStraddleValue + _closeSpread;

                    //                            if (_tradedQty > 0
                    //                                &&
                    //                                (
                    //                                (

                    //                                //&& ReferenceStraddleValue[option.Strike].Average() > currentBuyStraddleValue + 2
                    //                                TradedQuantity[option.Strike][0] > 0
                    //                                && TradedStraddleValue[option.Strike].Count > 0
                    //                                && 

                    //                                ((currentBuyStraddleValue - TradedStraddleValue[option.Strike].Average() > _stopLoss) ||

                    //                                ((HistoricalStraddleAverage[option.Strike].Value > currentBuyStraddleValue + 2)
                    //                                && (TradedStraddleValue[option.Strike].Average() > currentBuyStraddleValue + _closeSpread)))
                    //                                )

                    //                                //|| (LastTradeTime[tradeStrike].HasValue && currentTime.TimeOfDay >= new TimeSpan(16, 00, 00)
                    //                                //&& currentTime.Subtract(LastTradeTime[tradeStrike].Value).TotalMinutes > _timeBandForExit)
                    //                                )
                    //                                )
                    //                            {
                    //                                //here you have to check if the trigger option prices have stabilzied and not the trade strike price as it is check below
                    //                                if ((currentSellStraddleValue - HistoricalStraddleAverage[option.Strike].Value) < _openSpread)
                    //                                {
                    //                                    PlaceCloseOrder(option.Strike, currentTime, ((currentBuyStraddleValue - TradedStraddleValue[option.Strike].Average() > _stopLoss) ? currentUpTrade : !currentUpTrade));

                    //                                    if ((_tradedQty <= _thresholdQtyForBuy + _boughtQty - _buyQty) && _boughtQty > 0)
                    //                                    {
                    //                                        TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //                                        TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //                                        _boughtQty -= _buyQty;
                    //                                    }
                    //                                }
                    //                                else
                    //                                {

                    //                                    TradedStraddleValue[option.Strike] = new List<decimal>();
                    //                                    TradedStraddleValue[option.Strike].Add(currentSellStraddleValue);

                    //                                    //TradedQuantity[tradeStrike][0] += _stepQty;
                    //                                    //TradedQuantity[tradeStrike][1] = _upTrade ? 1 : 0;
                    //                                    //TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                    //                                    //ReferenceStraddleValue[option.Strike].Add(currentSellStraddleValue);
                    //                                    //if (referenceTradeStrikeKey == null)
                    //                                    //{
                    //                                    //    referenceTradeStrikeKey = new ReferenceTradeStrikeKey(optionStrike, tradeStrike);
                    //                                    //    referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //                                    //}

                    //                                    //if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                    //                                    //{
                    //                                    //    ReferenceTradedStraddleValue[referenceTradeStrikeKey].Add(currentSellStraddleValue);
                    //                                    //}
                    //                                    //else
                    //                                    //{
                    //                                    //    ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { currentSellStraddleValue });
                    //                                    //}


                    //                                    //TradeReferenceStrikes[tradeStrike].Add(option.Strike);
                    //                                    LastTradeTime[option.Strike] = currentTime;
                    //                                }


                    //                            }
                    //                            // }

                    //                            //decimal currentBuyStraddleValue = CurrentBuyStraddleValue[tradeStrike].Sum();

                    //                            //if (_tradedQty > 0
                    //                            //    &&
                    //                            //    (
                    //                            //    (

                    //                            //    //&& ReferenceStraddleValue[option.Strike].Average() > currentBuyStraddleValue + 2
                    //                            //    TradedQuantity.ContainsKey(tradeStrike) && TradedQuantity[tradeStrike][0] > 0
                    //                            //    && TradedStraddleValue.ContainsKey(tradeStrike) && TradedStraddleValue[tradeStrike].Count > 0
                    //                            //    && ((currentBuyStraddleValue - TradedStraddleValue[tradeStrike].Average() > _stopLoss) ||

                    //                            //    ((HistoricalStraddleAverage.ContainsKey(tradeStrike) && HistoricalStraddleAverage[tradeStrike].Value > currentBuyStraddleValue + 2)
                    //                            //    && TradedStraddleValue[tradeStrike].Average() > currentBuyStraddleValue + _closeSpread))

                    //                            //    )

                    //                            //    //|| (LastTradeTime[tradeStrike].HasValue && currentTime.TimeOfDay >= new TimeSpan(16, 00, 00)
                    //                            //    //&& currentTime.Subtract(LastTradeTime[tradeStrike].Value).TotalMinutes > _timeBandForExit)
                    //                            //    )
                    //                            //    )
                    //                            //{
                    //                            //    PlaceCloseOrder(tradeStrike, currentTime, currentUpTrade);

                    //                            //    if ((_tradedQty <= _thresholdQtyForBuy + _boughtQty - _buyQty) && _boughtQty > 0)
                    //                            //    {
                    //                            //        TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //                            //        TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, false);
                    //                            //        _boughtQty -= _buyQty;
                    //                            //    }
                    //                            //}
                    //                        }

                    //                        if ((CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                            && CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                            // && (CurrentBuyStraddleValue.ContainsKey(tradeStrike) && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] != 0
                    //                            // && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] != 0)
                    //                            // && (CurrentBuyStraddleValue.ContainsKey(optionStrike) && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                    //                            //&& CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                    //                            )
                    //                        {
                    //                            decimal currentSellStraddleValue = CurrentSellStraddleValue[option.Strike].Sum();
                    //                            ReferenceTradeStrikeKey referenceTradeStrikeKey = referenceTradeStrikeKeys.FirstOrDefault(x => x.ReferenceStrike == optionStrike && x.TradeStrike == tradeStrike);

                    //                            if (
                    //                                (option.Strike <= _baseInstrumentPrice + _strikePriceRange && option.Strike >= _baseInstrumentPrice - _strikePriceRange)
                    //                                && (tradeStrike <= _baseInstrumentPrice + _strikePriceRange && tradeStrike >= _baseInstrumentPrice - _strikePriceRange)
                    //                                && TradedQuantity[tradeStrike][0] < _stepQty * 5
                    //                                && (HistoricalStraddleAverage[option.Strike].Value != 0)
                    //                                && (_tradedQty < _maxQty - _stepQty)
                    //                                && (currentTime.TimeOfDay <= new TimeSpan(14, 40, 00))
                    //                                //&& TradedStraddleValue[option.Strike].Count > 0
                    //                                &&

                    //                                (((referenceTradeStrikeKey == null) && ((currentSellStraddleValue - HistoricalStraddleAverage[option.Strike].Value) >= _openSpread))
                    //                                || ((referenceTradeStrikeKey != null) && (ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() < 5)))
                    //                                //The below line is not working becuase we have to check trade strike straddle value with current trade strike value
                    //                                && ((TradedStraddleValue[option.Strike].Count == 0) || ((currentSellStraddleValue - (TradedStraddleValue[option.Strike].Last() + 10)) >= _openSpread))

                    //                                //((currentSellStraddleValue - (referenceTradeStrikeKey == null ? HistoricalStraddleAverage[option.Strike].Value :
                    //                                //(ReferenceTradedStraddleValue[referenceTradeStrikeKey].Last() + 10))) >= _openSpread)
                    //                                //&& (referenceTradeStrikeKey == null || ReferenceTradedStraddleValue[referenceTradeStrikeKey].Count() <= 3)

                    //                                //&&(optionType == 0 ? option.Strike <= _baseInstrumentPrice + 100 && option.Strike >= _baseInstrumentPrice - 100m 
                    //                                //: option.Strike >= _baseInstrumentPrice - 100 && option.Strike <= _baseInstrumentPrice + 100m)

                    //                                //) //|| _upTrade != currentUpTrade)
                    //                                //&& (_activeOrders == null || _activeOrders[(int)InstrumentType.CE].InstrumentToken == call.InstrumentToken
                    //                                //|| _activeOrders[(int)InstrumentType.PE].InstrumentToken == put.InstrumentToken)
                    //                                )
                    //                            {
                    //                                Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
                    //                                Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

                    //                                Order callSellOrder = null, putSellOrder = null, callStopLossOrder, putStopLossOrder, callBuyOrder, putBuyOrder;

                    //                                Task<Order> puttask = new Task<Order>(() => TradeEntry(put, currentTime, _stepQty, optionStrike, false));
                    //                                Task<Order> calltask = new Task<Order>(() => TradeEntry(call, currentTime, _stepQty, optionStrike, false));

                    //                                if (currentUpTrade)
                    //                                {
                    //                                    //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);
                    //                                    //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);

                    //                                    puttask.Start();
                    //                                    calltask.Start();
                    //                                    //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                    //                                    //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                    //                                }
                    //                                else
                    //                                {
                    //                                    //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);
                    //                                    //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);

                    //                                    calltask.Start();
                    //                                    puttask.Start();

                    //                                    //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                    //                                    //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                    //                                }

                    //                                Task.WaitAll(puttask, calltask);
                    //                                if (puttask.IsCompleted && calltask.IsCompleted)
                    //                                {
                    //                                    putSellOrder = puttask.Result;
                    //                                    callSellOrder = calltask.Result;
                    //                                }

                    //                                _tradedQty = _tradedQty + _stepQty;

                    //                                //Far OTM buy to reduce margin
                    //                                if (_tradedQty > _thresholdQtyForBuy + _boughtQty)
                    //                                {
                    //                                    putBuyOrder = TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //                                    callBuyOrder = TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    //                                    _boughtQty += _buyQty;
                    //                                }


                    //                                //CallOrdersTradeStrike.TryAdd(callStopLossOrder, tradeStrike);
                    //                                //PutOrdersTradeStrike.TryAdd(putStopLossOrder, tradeStrike);

                    //                                OnTradeEntry(callSellOrder);
                    //                                OnTradeEntry(putSellOrder);

                    //                                straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;

                    //                                TradedStraddleValue[tradeStrike].Add(straddleValue);
                    //                                TradedQuantity[tradeStrike][0] += _stepQty;
                    //                                //TradedQuantity[tradeStrike][1] = _upTrade ? 1 : 0;
                    //                                TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                    //                                //ReferenceStraddleValue[option.Strike].Add(currentSellStraddleValue);
                    //                                if (referenceTradeStrikeKey == null)
                    //                                {
                    //                                    referenceTradeStrikeKey = new ReferenceTradeStrikeKey(optionStrike, tradeStrike);
                    //                                    referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                    //                                }

                    //                                if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                    //                                {
                    //                                    ReferenceTradedStraddleValue[referenceTradeStrikeKey].Add(currentSellStraddleValue);
                    //                                }
                    //                                else
                    //                                {
                    //                                    ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { currentSellStraddleValue });
                    //                                }


                    //                                TradeReferenceStrikes[tradeStrike].Add(option.Strike);
                    //                                LastTradeTime[tradeStrike] = currentTime;
                    //                                //callSellOrder.UpOrder = _upTrade;
                    //                                //putSellOrder.UpOrder = _upTrade;
                    //                                //_activeOrders.Add(callSellOrder);
                    //                                //_activeOrders.Add(putSellOrder);

                    //                                //Close all uptrades if down trade is trigerred and vice versa
                    //                                //CloseAllTrades(_upTrade, currentTime);


                    //                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
                    //                                    Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");
                    //                            }

                    //                            //HistoricalStraddleAverage[option.Strike] = HistoricalStraddleAverage[option.Strike].Value == 0 ? currentSellStraddleValue : (HistoricalStraddleAverage[option.Strike] * 1800 + currentSellStraddleValue) / 1801;
                    //                            HistoricalStraddleAverage[option.Strike].Enqueue(currentSellStraddleValue);
                    //                            CurrentSellStraddleValue[option.Strike][(int)InstrumentType.CE] = 0;
                    //                            CurrentSellStraddleValue[option.Strike][(int)InstrumentType.PE] = 0;
                    #endregion

                    //Close all Positions at 3:15
                    TriggerEODPositionClose(currentTime);
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

        private void InitiateEntryLogic( decimal optionStrike, DateTime currentTime)
        {
            decimal tradeStrike = GetTradeStrike(optionStrike);
            decimal currentSellStraddleValueForOptionStrike = CurrentSellStraddleValue[optionStrike].Sum();
            decimal currentSellStraddleValueForTradeStrike = CurrentSellStraddleValue[tradeStrike].Sum();

            bool currentUpTrade = tradeStrike > _baseInstrumentPrice;

            if (TradedQuantity[tradeStrike][0] < _stepQty * 5
                && HistoricalStraddleAverage[optionStrike].Value != 0
                 && (_tradedQty < _maxQty - _stepQty)
                 &&
                 (
                 (TradedQuantity[tradeStrike][0] == 0 && ((currentSellStraddleValueForOptionStrike - HistoricalStraddleAverage[optionStrike].Value) >= _openSpread))
                 || ((TradedQuantity[tradeStrike][0] != 0) && ((currentSellStraddleValueForTradeStrike - (TradedStraddleValue[tradeStrike].Last() + 10)) >= _openSpread))
                 )
                 && (currentTime.TimeOfDay <= new TimeSpan(14, 40, 00))
                )
            {
                Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
                Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

                Order callSellOrder = null, putSellOrder = null, callBuyOrder, putBuyOrder;

                Task<Order> puttask = new Task<Order>(() => TradeEntry(put, currentTime, _stepQty, optionStrike, false));
                Task<Order> calltask = new Task<Order>(() => TradeEntry(call, currentTime, _stepQty, optionStrike, false));

                if (currentUpTrade)
                {
                    puttask.Start();
                    calltask.Start();
                }
                else
                {
                    calltask.Start();
                    puttask.Start();
                }

                Task.WaitAll(puttask, calltask);
                if (puttask.IsCompleted && calltask.IsCompleted)
                {
                    putSellOrder = puttask.Result;
                    callSellOrder = calltask.Result;
                }

                _tradedQty = _tradedQty + _stepQty;

                //Far OTM buy to reduce margin
                if (_tradedQty > _thresholdQtyForBuy + _boughtQty)
                {
                    putBuyOrder = TradeEntry(marginPutBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    callBuyOrder = TradeEntry(marginCallBuyInstrument, currentTime, _buyQty, optionStrike, true);
                    _boughtQty += _buyQty;
                }

                OnTradeEntry(callSellOrder);
                OnTradeEntry(putSellOrder);

                decimal straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;

                TradedStraddleValue[tradeStrike].Add(straddleValue);
                TradedQuantity[tradeStrike][0] += _stepQty;
                TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                //if (referenceTradeStrikeKey == null)
                //{
                //    referenceTradeStrikeKey = new ReferenceTradeStrikeKey(optionStrike, tradeStrike);
                //    referenceTradeStrikeKeys.Add(referenceTradeStrikeKey);
                //}

                //if (ReferenceTradedStraddleValue.ContainsKey(referenceTradeStrikeKey))
                //{
                //    ReferenceTradedStraddleValue[referenceTradeStrikeKey].Add(currentSellStraddleValue);
                //}
                //else
                //{
                //    ReferenceTradedStraddleValue.TryAdd(referenceTradeStrikeKey, new List<decimal>() { currentSellStraddleValue });
                //}


                //TradeReferenceStrikes[tradeStrike].Add(option.Strike);
                LastTradeTime[tradeStrike] = currentTime;
                //callSellOrder.UpOrder = _upTrade;
                //putSellOrder.UpOrder = _upTrade;
                //_activeOrders.Add(callSellOrder);
                //_activeOrders.Add(putSellOrder);

                //Close all uptrades if down trade is trigerred and vice versa
                //CloseAllTrades(_upTrade, currentTime);


                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
                //    Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");

                //HistoricalStraddleAverage[option.Strike] = HistoricalStraddleAverage[option.Strike].Value == 0 ? currentSellStraddleValue : (HistoricalStraddleAverage[option.Strike] * 1800 + currentSellStraddleValue) / 1801;
                //   HistoricalStraddleAverage[option.Strike].Enqueue(currentSellStraddleValue);
                //ClearCurrentStraddleValues(true);
            }
        }
        private void UpdateStraddleValue(Tick tick, Instrument option)
        {
            //if (tick.LastTradeTime.HasValue)
            //{
                //Instrument option = OptionUniverse[tick.InstrumentToken];

                int optionType = option.InstrumentType.Trim(' ').ToLower() == "ce" ? (int)InstrumentType.CE : (int)InstrumentType.PE;

                decimal optionStrike = option.Strike;
#if market
                CurrentSellStraddleValue [optionStrike][optionType] = tick.Bids[0].Price;
                CurrentBuyStraddleValue [optionStrike][optionType] = tick.Offers[0].Price;
#elif local
                CurrentSellStraddleValue[optionStrike][optionType] = tick.LastPrice;
                CurrentBuyStraddleValue[optionStrike][optionType] = tick.LastPrice;
#endif
            if(CurrentSellStraddleValue[optionStrike][0] * CurrentSellStraddleValue[optionStrike][1]* CurrentBuyStraddleValue[optionStrike][0] * CurrentBuyStraddleValue[optionStrike][1] != 0)
            {
                _straddleRefreshed[optionStrike] = true;
                HistoricalStraddleAverage[optionStrike].Enqueue(CurrentSellStraddleValue[optionStrike][1] + CurrentSellStraddleValue[optionStrike][0]);
            }
            option.LastPrice = tick.LastPrice;
            //return optionStrike;
            //}
        }
        private bool AreStraddlesUpdated(bool lowerStrikes)
        {
            bool straddlesRefreshed = true;

            if (lowerStrikes)
            {
                for (decimal strike = _baseInstrumentPrice - _strikePriceRange; strike <= _baseInstrumentPrice; strike = strike + 100)
                {
                    strike = Math.Round(strike / 100, 0) * 100;
                    straddlesRefreshed = (straddlesRefreshed && _straddleRefreshed[strike]);
                    if(!straddlesRefreshed)
                    {
                        break;
                    }
                }
            }
            else
            {
                for (decimal strike = _baseInstrumentPrice; strike <= _baseInstrumentPrice + _strikePriceRange; strike = strike + 100)
                {
                    strike = Math.Round(strike / 100, 0) * 100;
                    straddlesRefreshed = (straddlesRefreshed && _straddleRefreshed[strike]);
                    if (!straddlesRefreshed)
                    {
                        break;
                    }
                }
            }

            return straddlesRefreshed;
        }
        private bool AreStraddlesUpdated()
        {
            bool straddlesRefreshed = true;

            for (decimal strike = _baseInstrumentPrice - _strikePriceRange; strike <= _baseInstrumentPrice + _strikePriceRange; strike = strike + 100)
            {
                strike = Math.Round(strike / 100, 0) * 100;
                straddlesRefreshed = (straddlesRefreshed && _straddleRefreshed[strike]);
                if (!straddlesRefreshed)
                {
                    break;
                }
            }

            return straddlesRefreshed;
        }
        private void ClearCurrentStraddleValues(bool lowerStrikes)
        {
            bool straddlesRefreshed = true;

            if (lowerStrikes)
            {
                for (decimal strike = _baseInstrumentPrice - _strikePriceRange; strike <= _baseInstrumentPrice; strike = strike + 100)
                {
                    strike = Math.Round(strike / 100, 0) * 100;
                    CurrentSellStraddleValue[strike][(int)InstrumentType.CE] = 0;
                    CurrentSellStraddleValue[strike][(int)InstrumentType.PE] = 0;

                    CurrentBuyStraddleValue[strike][(int)InstrumentType.CE] = 0;
                    CurrentBuyStraddleValue[strike][(int)InstrumentType.PE] = 0;

                }
            }
            else
            {
                for (decimal strike = _baseInstrumentPrice; strike <= _baseInstrumentPrice + _strikePriceRange; strike = strike + 100)
                {
                    strike = Math.Round(strike / 100, 0) * 100;
                    CurrentSellStraddleValue[strike][(int)InstrumentType.CE] = 0;
                    CurrentSellStraddleValue[strike][(int)InstrumentType.PE] = 0;

                    CurrentBuyStraddleValue[strike][(int)InstrumentType.CE] = 0;
                    CurrentBuyStraddleValue[strike][(int)InstrumentType.PE] = 0;
                }
            }
        }
        private void ClearCurrentStraddleValues()
        {
            for (decimal strike = _baseInstrumentPrice - _strikePriceRange; strike <= _baseInstrumentPrice + _strikePriceRange; strike = strike + 100)
            {
                strike = Math.Round(strike / 100, 0) * 100;
                CurrentSellStraddleValue[strike][(int)InstrumentType.CE] = 0;
                CurrentSellStraddleValue[strike][(int)InstrumentType.PE] = 0;
                _straddleRefreshed[strike] = false;
            }
        }

        private bool isExitTrigerred(decimal optionStrike)
        {
            decimal currentBuyStraddleValue = CurrentBuyStraddleValue[optionStrike].Sum();
            //decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
            bool exit = false;
            if (_tradedQty > 0
                &&
                CurrentBuyStraddleValue[optionStrike][0] * CurrentBuyStraddleValue[optionStrike][1] != 0
                &&
                (
                (
                TradedQuantity[optionStrike][0] > 0
                && TradedStraddleValue[optionStrike].Count > 0
                &&

                ((currentBuyStraddleValue - TradedStraddleValue[optionStrike].Average() > _stopLoss) ||

                ((HistoricalStraddleAverage[optionStrike].Value > currentBuyStraddleValue + 2)
                && (TradedStraddleValue[optionStrike].Average() > currentBuyStraddleValue + _closeSpread)))
                )
                )
                )
            {
                exit = true;
            }
            return exit;
        }
        private int QuantityToBeTraded(decimal optionStrike)
        {
            int quantityTobeTraded = 0;
            //determine all trigger option strikes for this traded strike
            for(decimal strike  = _baseInstrumentPrice - _strikePriceRange; strike< _baseInstrumentPrice; strike = strike + 100)
            {
                strike = Math.Round(strike / 100, 0) * 100;
                if (optionStrike ==  GetTradeStrike(strike))
                {
                    decimal currentSellStraddleValue = CurrentSellStraddleValue[strike].Sum();
                    if ((currentSellStraddleValue - HistoricalStraddleAverage[strike].Value) > _openSpread)
                    {
                        quantityTobeTraded++;
                    }

                }
            }
            return quantityTobeTraded;
        }

        //private decimal GetTradeStrike(decimal optionStrike)
        //{
        //    decimal delta = 0;
        //    if ((_baseInstrumentPrice - optionStrike) > Math.Min(300, _strikePriceRange))
        //    {
        //        delta = 0;//50
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) > 200)
        //    {
        //        delta = 70;
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) > 90)
        //    {
        //        delta = 130;
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) > 0)
        //    {
        //        delta = 150;// _baseInstrumentPrice - optionStrike;
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) < -1 * Math.Min(300, _strikePriceRange))
        //    {
        //        delta = 0; //-50
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) < -200)
        //    {
        //        delta = -70;
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) < -90)
        //    {
        //        delta = -130;
        //    }
        //    else if ((_baseInstrumentPrice - optionStrike) < 0)
        //    {
        //        delta = -150;// (_baseInstrumentPrice - optionStrike);
        //    }
        //    else
        //    {
        //        delta = 0;
        //    }


        //    decimal tradeStrike = _baseInstrumentPrice + delta;
        //    tradeStrike = Math.Round(tradeStrike / 100, 0) * 100;
        //    return tradeStrike;
        //}
        private decimal GetTradeStrike(decimal optionStrike)
        {
            decimal delta = 0;
            if ((_baseInstrumentPrice - optionStrike) > Math.Min(300, _strikePriceRange))
            {
                delta = 0;//50
            }
            else if ((_baseInstrumentPrice - optionStrike) > 200)
            {
                delta = 70;
            }
            else if ((_baseInstrumentPrice - optionStrike) > 90)
            {
                delta = 130;
            }
            else if ((_baseInstrumentPrice - optionStrike) > 0)
            {
                delta = 150;// _baseInstrumentPrice - optionStrike;
            }
            else if ((_baseInstrumentPrice - optionStrike) < -1 * Math.Min(300, _strikePriceRange))
            {
                delta = 0; //-50
            }
            else if ((_baseInstrumentPrice - optionStrike) < -200)
            {
                delta = -70;
            }
            else if ((_baseInstrumentPrice - optionStrike) < -90)
            {
                delta = -130;
            }
            else if ((_baseInstrumentPrice - optionStrike) < 0)
            {
                delta = -150;// (_baseInstrumentPrice - optionStrike);
            }
            else
            {
                delta = 0;
            }


            decimal tradeStrike = _baseInstrumentPrice + delta;
            tradeStrike = Math.Round(tradeStrike / 100, 0) * 100;
            return tradeStrike;
        }

        //public void PlaceTempOrderForTesting()
        //{
        //    //BANKNIFTY2181836000PE
        //   Order order =  KotakMarketOrders.PlaceOrder(_algoInstance, "BANKNIFTY21AUG37500CE", "CE", 12.3m, 43735, true, 25, AlgoIndex.StrangleShiftToOneSide
        //        , orderType: "Market", product: "MIS");
        //}
        void PlaceCloseOrder(decimal tradeStrike,int qtyTobeExited, DateTime currentTime, bool currentUpTrade)
        {
            int qty = qtyTobeExited; 
            Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
            Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

            Order callBuyOrder = null, putBuyOrder = null;

            Task<Order> puttask = new Task<Order>(() => TradeEntry(put, currentTime, qty, tradeStrike, true));
            Task<Order> calltask = new Task<Order>(() => TradeEntry(call, currentTime, qty, tradeStrike, true));

            if (currentUpTrade)
            {
                //callBuyOrder = TradeEntry(call, currentTime, qty, tradeStrike, true);
                //putBuyOrder = TradeEntry(put, currentTime, qty, tradeStrike, true);

                //putSellOrder = TradeEntry(put, currentTime, _stepQty, optionStrike, false);
                //callSellOrder = TradeEntry(call, currentTime, _stepQty, optionStrike, false);
                
                calltask.Start();
                puttask.Start();
                
            }
            else
            {
                //putBuyOrder = TradeEntry(put, currentTime, qty, tradeStrike, true);
                //callBuyOrder = TradeEntry(call, currentTime, qty, tradeStrike, true);
                
                puttask.Start();
                calltask.Start();

            }
            Task.WaitAll(puttask, calltask);

            if (puttask.IsCompleted && calltask.IsCompleted)
            {
                putBuyOrder = puttask.Result;
                callBuyOrder = calltask.Result;
            }

            //if (qty > 0)
            //{

            //Order callBuyOrder = MarketOrders.PlaceOrder(_algoInstance, call.TradingSymbol, call.InstrumentType, call.LastPrice,
            //     MappedTokens[call.InstrumentToken], true, Convert.ToInt32(qty * call.LotSize),
            //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

            ////Order callBuyOrder = MarketOrders.PlaceOrder(_algoInstance, call.TradingSymbol, call.InstrumentType, call.LastPrice,
            ////     call.InstrumentToken, true, Convert.ToInt32(qty * call.LotSize),
            ////    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade, broker: Constants.ZERODHA);

            //Order putBuyOrder = MarketOrders.PlaceOrder(_algoInstance, put.TradingSymbol, put.InstrumentType, put.LastPrice,
            //    MappedTokens[put.InstrumentToken], true, Convert.ToInt32(qty * put.LotSize),
            //   algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

            //Order putBuyOrder = MarketOrders.PlaceOrder(_algoInstance, put.TradingSymbol, put.InstrumentType, put.LastPrice,
            //    put.InstrumentToken, true, Convert.ToInt32(qty * put.LotSize),
            //   algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade, broker: Constants.ZERODHA);

            OnTradeExit(callBuyOrder);
                OnTradeExit(putBuyOrder);

                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Closed Straddle: CE: {0}, PE: {1}, Total: {2}",
                //Math.Round(callBuyOrder.AveragePrice, 1), Math.Round(putBuyOrder.AveragePrice, 1), Math.Round(callBuyOrder.AveragePrice + putBuyOrder.AveragePrice, 1)), "Trade Option");
           // }

            _tradedQty = _tradedQty - Convert.ToInt32(qty);
            TradedStraddleValue[tradeStrike] = new List<decimal>();
            TradedQuantity[tradeStrike][0] -= qty;
            //TradedQuantity[tradeStrike][1] = -1;
            LastTradeTime[tradeStrike] = null;
            CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] = callBuyOrder.AveragePrice;
            CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] = putBuyOrder.AveragePrice;


            //CancelSLOrders(tradeStrike, currentTime);
            //if (triggerStrike != 0)
            //{
            //    ReferenceStraddleValue[triggerStrike] = new List<decimal>();
            //}
            //else if (_tradedQty == 0)
            //{
            //decimal[] strikes = ReferenceStraddleValue.Keys.ToArray<decimal>();
            //foreach (decimal strike in strikes)
            //{
            //    if (TradeReferenceStrikes[tradeStrike].Contains(strike))
            //    {
            //        ReferenceStraddleValue[strike] = new List<decimal>();
            //    }
            //}


            //foreach (ReferenceTradeStrikeKey referenceTradeStrike in referenceTradeStrikeKeys)
            //{
            //    if(referenceTradeStrike.TradeStrike == tradeStrike)
            //    {
            //        ReferenceTradedStraddleValue.Remove(referenceTradeStrike);
            //    }
            //}
            //referenceTradeStrikeKeys.RemoveAll(x => x.TradeStrike == tradeStrike);

            //TradeReferenceStrikes[tradeStrike] = new List<decimal>();
            //}
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
                Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS);
                //CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, string orderId, DateTime currentTime, string tradingSymbol)
                CallOrdersTradeStrike.Remove(order);
                OnTradeEntry(cancelOrder);
            }


            //Cancel all original super multiple orders
            orders = PutOrdersTradeStrike.Where(x => x.Value == tradeStrike).Select(x => x.Key).ToList();
            foreach (Order order in orders)
            {
                Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS);
                PutOrdersTradeStrike.Remove(order);
                OnTradeEntry(cancelOrder);
            }
        }
        private Order TradeEntry (Instrument option, DateTime currentTime, int qtyInlots, decimal triggerOptionStrike, bool buyOrder)
        {
            Order optionOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
               MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: triggerOptionStrike.ToString(), broker: Constants.KOTAK);

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
                    var localQty = new Dictionary<decimal, decimal[]>(TradedQuantity);
                    foreach (var element in localQty.Where(x => x.Value[0] > 0))
                    {
                        PlaceCloseOrder(element.Key, Convert.ToInt32(element.Value[0]), currentTime, element.Key > _baseInstrumentPrice);
                    }
                }

                _stopTrade = true;

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                        "Completed", "TriggerEODPositionClose");

                //Environment.Exit(0);
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
                if (StraddleUniverse == null || StraddleUniverse.Keys.Max() < _baseInstrumentPrice + 300 || StraddleUniverse.Keys.Min() > _baseInstrumentPrice - 300)
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
                        //ReferenceStraddleValue.TryAdd(strike, new List<decimal>());
                        TradeReferenceStrikes.TryAdd(strike, new List<decimal>());
                        TradedQuantity.TryAdd(strike, new decimal[] { 0, -1 });
                        HistoricalStraddleAverage.TryAdd(strike, new FixedSizedQueue<decimal>());
                        LastTradeTime.TryAdd(strike, null);
                        _straddleRefreshed.TryAdd(strike, false);
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

            if(rts == null)
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
