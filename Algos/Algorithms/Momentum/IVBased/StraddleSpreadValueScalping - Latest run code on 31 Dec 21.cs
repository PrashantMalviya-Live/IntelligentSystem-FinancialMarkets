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
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.IO;
using Newtonsoft.Json;

namespace Algorithms.Algorithms
{
    public class StraddleSpreadValueScalping : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public Dictionary<uint, Instrument> OptionUniverse { get; set; }
        public Dictionary<uint, uint> MappedTokens { get; set; }
        private SemaphoreSlim semaphore;
        public SortedList<decimal, Instrument[]> StraddleUniverse { get; set; }
        public Dictionary<decimal, FixedSizedQueue<decimal>> HistoricalStraddleAverage { get; set; }
        public Dictionary<decimal, List<decimal>> TradedStraddleValue { get; set; }
        //Count and straddle value for each strike price. This is used to determine the average and limit the trade per strike price.
        public Dictionary<decimal, List<decimal>> ReferenceStraddleValue { get; set; }
        //Stores traded qty and up or downtrade bool = 1 means uptrade

        //All order ids for a particular trade strike
        private Dictionary<Order, decimal> CallOrdersTradeStrike { get; set; }
        private Dictionary<Order, decimal> PutOrdersTradeStrike { get; set; }

        public Dictionary<decimal, decimal[]> TradedQuantity { get; set; }
        public Dictionary<decimal, List<decimal>> TradeReferenceStrikes { get; set; }

        //Strike and time order
        private Dictionary<decimal, DateTime?> LastTradeTime { get; set; }
        private Dictionary<decimal, DateTime?> CallLastTradeTime { get; set; }
        private Dictionary<decimal, DateTime?> PutLastTradeTime { get; set; }

        private Dictionary<decimal, bool> _optionStrikeCovered;
        private Dictionary<decimal, bool> _optionStrikeBuyExecuting;
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

        private List<Order> _activeOrders;
        private OrderTrio _callOrderTrio;
        private OrderTrio _putOrderTrio;
        private Option _activeNearOption;
        private Option _activeFarOption;

        public List<Order> _pastOrders;
        private bool _stopTrade;
        private int _tradedQty = 0;
        
        private int _stepQty = 0;
        private int _maxQty = 0;
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
        public decimal _strikePriceRange;
        List<uint> _EMALoaded;
        List<uint> _SQLLoading;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;

        //private DateTime? _nearExpiry;
        //private DateTime? _farExpiry;
        private DateTime? _expiry;

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
        private decimal _openSpread;
        private decimal _closeSpread;
        private bool _upTrade;
        public const int SHORT_EMA = 5;
        public const int LONG_EMA = 13;
        public const int RSI_LENGTH = 15;
        public const int RSI_THRESHOLD = 60;
        public const int NEAR_EXPIRY = 0;
        public const int FAR_EXPIRY = 1;
        private const int CE = 1;
        private const int PE = 0;

        private const int LOSSPERTRADE = 1000;
        public const AlgoIndex algoIndex = AlgoIndex.ValueSpreadTrade;
        //CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        HttpClient _httpClient;
        IHttpClientFactory _httpClientFactory;
        public readonly decimal _emaBandForExit;
        public readonly decimal _rsiBandForExit;
        public double _timeBandForExit;
        public List<uint> SubscriptionTokens { get; set; }

        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        public StraddleSpreadValueScalping(DateTime endTime, uint baseInstrumentToken,
            DateTime? expiry, int quantity, int maxQty, int stepQty, decimal targetProfit, 
            decimal openSpread, decimal closeSpread, decimal stopLoss,
            int algoInstance = 0, bool positionSizing = false, decimal maxLossPerTrade = 0, 
            double timeBandForExit = 0, IHttpClientFactory httpClientFactory = null)// HttpClient httpClient = null)
        {
            //_httpClient = httpClient;
            _httpClientFactory = httpClientFactory;
            _httpClient = _httpClientFactory.CreateClient();
            _endDateTime = endTime;
            _expiry = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _targetProfit = targetProfit;
            _stopLoss = stopLoss;
            _openSpread = openSpread;
            _closeSpread = closeSpread;
            _timeBandForExit = timeBandForExit;
            _stopTrade = true;
            tokenLastClose = new Dictionary<uint, decimal>();
            tokenCPR = new Dictionary<uint, CentralPivotRange>();
            tokenExits = new List<uint>();
            _pastOrders = new List<Order>();
            _maxQty = maxQty;
            _stepQty = stepQty;
            SubscriptionTokens = new List<uint>();
            HistoricalStraddleAverage = new Dictionary<decimal, FixedSizedQueue<decimal>>();  
            CurrentSellStraddleValue = new Dictionary<decimal, decimal[]>();
            _optionStrikeCovered = new Dictionary<decimal, bool>();
            _optionStrikeBuyExecuting = new Dictionary<decimal, bool>();
            CurrentBuyStraddleValue = new Dictionary<decimal, decimal[]>();
            TradedStraddleValue = new Dictionary<decimal, List<decimal>>();
            ReferenceStraddleValue = new Dictionary<decimal, List<decimal>>();
            TradedQuantity = new Dictionary<decimal, decimal[]>();
            TradeReferenceStrikes = new Dictionary<decimal, List<decimal>>();
            LastTradeTime = new Dictionary<decimal, DateTime?>();
            CallLastTradeTime = new Dictionary<decimal, DateTime?>();
            PutLastTradeTime = new Dictionary<decimal, DateTime?>();

            ActiveOptions = new List<Instrument>();
            OptionUniverse = new Dictionary<uint, Instrument>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            _activeOrders = new List<Order>(); 
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

            CallOrdersTradeStrike = new Dictionary<Order, decimal>();
            PutOrdersTradeStrike = new Dictionary<Order, decimal>();

            //candleManger = new CandleManger(TimeCandles, CandleType.Time);
            // candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, DateTime.Now,
                _expiry.GetValueOrDefault(DateTime.Now), quantity, candleTimeFrameInMins: 0, Arg1: _openSpread, Arg2: _closeSpread,
                Arg3: _targetProfit, Arg4: _stopLoss, Arg5: Convert.ToDecimal(_timeBandForExit));

            ZConnect.Login();
            KoConnect.Login();

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
                lock (TimeCandles)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    //MonitorCandles(tick, currentTime);

                   if (tick.LastTradeTime.HasValue)
                    {
                        Instrument option = OptionUniverse[token];

                        int optionType = option.InstrumentType.Trim(' ').ToLower() == "ce" ? (int)InstrumentType.CE : (int)InstrumentType.PE;

                        decimal optionStrike = option.Strike;
#if market
                        CurrentSellStraddleValue [optionStrike][optionType] = tick.Bids[0].Price;
                        CurrentBuyStraddleValue [optionStrike][optionType] = tick.Offers[0].Price;
#elif local
                        CurrentSellStraddleValue[option.Strike][optionType] = tick.LastPrice;
                        CurrentBuyStraddleValue[option.Strike][optionType] = tick.LastPrice;
#endif
                        option.LastPrice = tick.LastPrice;
                        
                        if(optionType == (int) InstrumentType.CE)
                        {
                            CallLastTradeTime[optionStrike] = tick.LastTradeTime;
                        }
                        else if (optionType == (int)InstrumentType.PE)
                        {
                            PutLastTradeTime[optionStrike] = tick.LastTradeTime;
                        }

                        decimal delta = 0;

                        if ((_baseInstrumentPrice - optionStrike) > 300)
                        {
                            delta = (_baseInstrumentPrice - optionStrike) - 150;
                        }
                        else if ((_baseInstrumentPrice - optionStrike) > 200)
                        {
                            delta = (_baseInstrumentPrice - optionStrike) - 100;
                        }
                        else if ((_baseInstrumentPrice - optionStrike) > 100)
                        {
                            delta = (_baseInstrumentPrice - optionStrike) - 50;
                        }
                        else if ((_baseInstrumentPrice - optionStrike) < -300)
                        {
                            delta = (_baseInstrumentPrice - optionStrike) + 120;
                        }
                        else if ((_baseInstrumentPrice - optionStrike) < -200)
                        {
                            delta = (_baseInstrumentPrice - optionStrike) + 80;
                        }
                        else if ((_baseInstrumentPrice - optionStrike) < -100)
                        {
                            delta = (_baseInstrumentPrice - optionStrike) + 30;
                        }
                        else
                        {
                            delta = (_baseInstrumentPrice - optionStrike);
                        }
                        decimal tradeStrike = _baseInstrumentPrice + delta;

                        tradeStrike = Math.Round(tradeStrike / 100, 0) * 100;

                        bool currentUpTrade = _upTrade;
                        if (tradeStrike > _baseInstrumentPrice)
                        {
                            //_upTrade = true;
                            currentUpTrade = true;
                            //Limit trade to within 200 points from base instrument
                            //tradeStrike = tradeStrike - _baseInstrumentPrice > 200 ? _baseInstrumentPrice + 200 : tradeStrike;
                        }
                        else if (tradeStrike < _baseInstrumentPrice)
                        {
                            //_upTrade = false;
                            currentUpTrade = false;
                            //Limit trade to within 200 points from base instrument
                            //tradeStrike = _baseInstrumentPrice - tradeStrike > 200 ? _baseInstrumentPrice - 200 : tradeStrike;
                        }

                        //if(CurrentBuyStraddleValue.ContainsKey(optionStrike) && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                        //    && CurrentBuyStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                        //{
                        //    decimal currentBuyStraddleValue = CurrentBuyStraddleValue[optionStrike].Sum();

                        //    if (_tradedQty > 0
                        //        &&
                        //        (
                        //        (

                        //        //&& ReferenceStraddleValue[option.Strike].Average() > currentBuyStraddleValue + 2
                        //        TradedQuantity.ContainsKey(optionStrike) && TradedQuantity[optionStrike][0] > 0
                        //        && TradedStraddleValue.ContainsKey(optionStrike) && TradedStraddleValue[optionStrike].Count > 0
                        //        && ((currentBuyStraddleValue - TradedStraddleValue[optionStrike].Average() > _stopLoss) ||

                        //        ((HistoricalStraddleAverage.ContainsKey(optionStrike) && HistoricalStraddleAverage[optionStrike].Value > currentBuyStraddleValue + 2)
                        //        && TradedStraddleValue[optionStrike].Average() > currentBuyStraddleValue + _closeSpread))

                        //        )

                        //        //|| (LastTradeTime[tradeStrike].HasValue && currentTime.TimeOfDay >= new TimeSpan(16, 00, 00)
                        //        //&& currentTime.Subtract(LastTradeTime[tradeStrike].Value).TotalMinutes > _timeBandForExit)
                        //        )
                        //        )
                        //    {
                        //        PlaceCloseOrder(optionStrike, currentTime, currentUpTrade.ToString());
                        //    }
                        //}


                        if ((CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] != 0
                            && CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                            && (CurrentBuyStraddleValue.ContainsKey(tradeStrike) && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] != 0
                            && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] != 0)
                            )
                        {
                            decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
                            //if (currentSpread > 0.85m * (TradedSpread[option.Strike] == 0 ? HistoricalSpread[option.Strike] : TradedSpread[option.Strike])
                            //    && _tradedQty < _maxQty - _stepQty
                            //    && (_activeOrders == null || _activeOrders[0].InstrumentToken == nearOption.InstrumentToken
                            //    || _activeOrders[1].InstrumentToken == farOption.InstrumentToken))
                            if (
                                ((currentSellStraddleValue - (ReferenceStraddleValue[optionStrike].Count == 0 ? HistoricalStraddleAverage[optionStrike].Value :
                                (ReferenceStraddleValue[optionStrike].Last() + 5))) >= _openSpread)
                                && ReferenceStraddleValue[optionStrike].Count() <= 4

                                  //&&(optionType == 0 ? option.Strike <= _baseInstrumentPrice + 100 && option.Strike >= _baseInstrumentPrice - 100m 
                                  //: option.Strike >= _baseInstrumentPrice - 100 && option.Strike <= _baseInstrumentPrice + 100m)
                                  //&& (optionStrike <= _baseInstrumentPrice + 300 && optionStrike >= _baseInstrumentPrice - 300)
                                  && (tradeStrike <= _baseInstrumentPrice + 300 && tradeStrike >= _baseInstrumentPrice - 300)

                                && (HistoricalStraddleAverage[optionStrike].Value != 0)
                                && (_tradedQty < _maxQty - _stepQty)
                                && (currentTime.TimeOfDay <= new TimeSpan(14, 35, 00))
                                //) //|| _upTrade != currentUpTrade)
                                //&& (_activeOrders == null || _activeOrders[(int)InstrumentType.CE].InstrumentToken == call.InstrumentToken
                                //|| _activeOrders[(int)InstrumentType.PE].InstrumentToken == put.InstrumentToken)
                                && !_optionStrikeCovered[optionStrike]
                                )
                            {
                                _optionStrikeCovered[optionStrike] = true;
                                CallEntryTrades(optionStrike, tradeStrike, currentSellStraddleValue, currentTime, currentUpTrade);
                            }

                            //HistoricalStraddleAverage[option.Strike] = HistoricalStraddleAverage[option.Strike].Value == 0 ? currentSellStraddleValue : (HistoricalStraddleAverage[option.Strike] * 1800 + currentSellStraddleValue) / 1801;
                            HistoricalStraddleAverage[optionStrike].Enqueue(currentSellStraddleValue);
                            CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] = 0;
                            CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] = 0;

                            decimal currentBuyStraddleValue = CurrentBuyStraddleValue[tradeStrike].Sum();
                            decimal stoploss = 50;
                            if (_tradedQty > 0
                                &&
                                (
                                (

                                //&& ReferenceStraddleValue[option.Strike].Average() > currentBuyStraddleValue + 2
                                TradedQuantity.ContainsKey(tradeStrike) && TradedQuantity[tradeStrike][0] > 0
                                && TradedStraddleValue.ContainsKey(tradeStrike) && TradedStraddleValue[tradeStrike].Count > 0
                                && ((currentBuyStraddleValue - TradedStraddleValue[tradeStrike].Average() > stoploss) ||

                                ((HistoricalStraddleAverage.ContainsKey(tradeStrike) && HistoricalStraddleAverage[tradeStrike].Value > currentBuyStraddleValue + 2)
                                && TradedStraddleValue[tradeStrike].Average() > currentBuyStraddleValue + _closeSpread))

                                )
                                
                                //|| (LastTradeTime[tradeStrike].HasValue && currentTime.TimeOfDay >= new TimeSpan(16, 00, 00)
                                //&& currentTime.Subtract(LastTradeTime[tradeStrike].Value).TotalMinutes > _timeBandForExit)
                                )
                               // && !_optionStrikeBuyExecuting[tradeStrike]
                                )
                            {

                                //_optionStrikeBuyExecuting[tradeStrike] = true;
                                //PlaceCloseOrder(tradeStrike, currentTime, currentUpTrade);
                                CallOrderExit(tradeStrike, currentTime, currentUpTrade);
                            }
                        }

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

        private void CallEntryTrades(decimal optionStrike, decimal tradeStrike, decimal currentSellStraddleValue, 
            DateTime currentTime, bool currentUpTrade)
        {
            using (var httpClient = _httpClientFactory.CreateClient())
            {
                ExecuteOrderEntry(optionStrike, tradeStrike, currentSellStraddleValue, currentTime, currentUpTrade, httpClient);
            }
        }
        private void ExecuteOrderEntry(decimal optionStrike, decimal tradeStrike,
            decimal currentSellStraddleValue, DateTime currentTime, bool currentUpTrade, HttpClient httpClient)
        {
            Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
            Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

            Order callSellOrder, putSellOrder, callStopLossOrder, putStopLossOrder;
            Task<Order> callSellOrderTask, putSellOrderTask;
            string callOrderId, putOrderId;
            decimal straddleValue = 0;
            DateTime callOrderStart, callOrderEnd, putOrderStart, putOrderEnd;

            //callSellOrder = TradeEntrySync(call, currentTime, _stepQty, CallLastTradeTime[optionStrike].Value.ToString(), false);
            //putSellOrder = TradeEntrySync(put, currentTime, _stepQty, PutLastTradeTime[optionStrike].Value.ToString(), false);


            if (currentUpTrade)
            {
                putOrderStart = DateTime.Now;
                putOrderId = TradeEntry(put, currentTime, _stepQty, currentUpTrade, false, httpClient);
                callOrderStart = putOrderEnd = DateTime.Now;
                callOrderId = TradeEntry(call, currentTime, _stepQty, currentUpTrade, false, httpClient);
                callOrderEnd = DateTime.Now;
                putSellOrder = GetOrderDetails(putOrderId, put, currentTime, _stepQty, currentUpTrade, false, putOrderEnd - putOrderStart, httpClient);
                callSellOrder = GetOrderDetails(callOrderId, call, currentTime, _stepQty, currentUpTrade, false, callOrderEnd - callOrderStart, httpClient);


                //putSellOrderTask = TradeEntryAsync(put, currentTime, _stepQty, currentUpTrade, false);
                //callSellOrderTask = TradeEntryAsync(call, currentTime, _stepQty, currentUpTrade, false);
                //putSellOrder = GetOrderDetails(putOrderId, put, currentTime, _stepQty, currentUpTrade, false, putOrderEnd - putOrderStart);
                //callSellOrder = GetOrderDetails(callOrderId, call, currentTime, _stepQty, currentUpTrade, false, callOrderEnd - callOrderStart);

                //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
                //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
            }
            else
            {
                //callSellOrder = TradeEntry(call, currentTime, _stepQty, currentUpTrade, false);
                //putSellOrder = TradeEntry(put, currentTime, _stepQty, currentUpTrade, false);
                callOrderStart = DateTime.Now;
                callOrderId = TradeEntry(call, currentTime, _stepQty, currentUpTrade, false, httpClient);
                putOrderStart = callOrderEnd = DateTime.Now;
                putOrderId = TradeEntry(put, currentTime, _stepQty, currentUpTrade, false, httpClient);
                putOrderEnd = DateTime.Now;
                callSellOrder = GetOrderDetails(callOrderId, call, currentTime, _stepQty, currentUpTrade, false, callOrderEnd - callOrderStart, httpClient);
                putSellOrder = GetOrderDetails(putOrderId, put, currentTime, _stepQty, currentUpTrade, false, putOrderEnd - putOrderStart, httpClient);


                //putOrderStart = DateTime.Now;
                //callSellOrderTask = TradeEntryAsync(call, currentTime, _stepQty, currentUpTrade, false);
                //putSellOrderTask = TradeEntryAsync(put, currentTime, _stepQty, currentUpTrade, false);
                //callOrderStart = putOrderEnd = DateTime.Now;

                //callOrderEnd = DateTime.Now;


                //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
            }


            _tradedQty = _tradedQty + _stepQty;
            TradedQuantity[tradeStrike][0] += _stepQty;
            TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
            LastTradeTime[tradeStrike] = currentTime;
            TradeReferenceStrikes[tradeStrike].Add(optionStrike);

            OnTradeEntry(callSellOrder);
            OnTradeEntry(putSellOrder);

            straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;
            TradedStraddleValue[tradeStrike].Add(straddleValue);

            ReferenceStraddleValue[optionStrike].Add(currentSellStraddleValue);
            _optionStrikeCovered[optionStrike] = false;

            //Close all uptrades if down trade is trigerred and vice versa
            //CloseAllTrades(_upTrade, currentTime);

            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
            //    Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");
            //}
        }


        private void CallOrderExit(decimal tradeStrike, DateTime currentTime, bool currentUpTrade)
        {
            using (var httpClient = _httpClientFactory.CreateClient())
            {
                PlaceCloseOrder(tradeStrike, currentTime, currentUpTrade, httpClient);
            }
        }

        void PlaceCloseOrder(decimal tradeStrike, DateTime currentTime, bool currentUpTrade, HttpClient httpClient)
        {
            
            Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
            Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

            Order callBuyOrder, putBuyOrder;
            Task<Order> callBuyOrderTask, putBuyOrderTask;
            string callOrderId, putOrderId;
            DateTime callOrderStart, callOrderEnd, putOrderStart, putOrderEnd;

            int qty = Convert.ToInt32(TradedQuantity[tradeStrike][0]);
            _tradedQty = _tradedQty - Convert.ToInt32(qty);

            //callBuyOrderTask = TradeEntryAsync(call, currentTime, qty, CallLastTradeTime[tradeStrike].ToString(), true);
            //putBuyOrderTask = TradeEntryAsync(put, currentTime, qty, PutLastTradeTime[tradeStrike].ToString(), true);

            //callBuyOrder = TradeEntrySync(call, currentTime, qty, CallLastTradeTime[tradeStrike].ToString(), true);
            //putBuyOrder = TradeEntrySync(put, currentTime, qty, PutLastTradeTime[tradeStrike].ToString(), true);

            if (currentUpTrade)
            {
                callOrderStart = DateTime.Now;
                callOrderId = TradeEntry(call, currentTime, qty, currentUpTrade, true, httpClient);
                putOrderStart = callOrderEnd = DateTime.Now;
                putOrderId = TradeEntry(put, currentTime, qty, currentUpTrade, true, httpClient);
                putOrderEnd = DateTime.Now;
                putBuyOrder = GetOrderDetails(putOrderId, put, currentTime, qty, currentUpTrade, true, putOrderEnd - putOrderStart, httpClient);
                callBuyOrder = GetOrderDetails(callOrderId, call, currentTime, qty, currentUpTrade, true, callOrderEnd - callOrderStart, httpClient);

                //callBuyOrderTask = TradeEntryAsync(call, currentTime, qty, CallLastTradeTime[tradeStrike].ToString(), true);
                //putBuyOrderTask = TradeEntryAsync(put, currentTime, qty, PutLastTradeTime[tradeStrike].ToString(), true);

            }
            else
            {
                //putBuyOrder = TradeEntry(put, currentTime, qty, currentUpTrade, true);
                //callBuyOrder = TradeEntry(call, currentTime, qty, currentUpTrade, true);
                putOrderStart = DateTime.Now;
                putOrderId = TradeEntry(put, currentTime, qty, currentUpTrade, true, httpClient);
                callOrderStart = putOrderEnd = DateTime.Now;
                callOrderId = TradeEntry(call, currentTime, qty, currentUpTrade, true, httpClient);
                callOrderEnd = DateTime.Now;
                callBuyOrder = GetOrderDetails(callOrderId, call, currentTime, qty, currentUpTrade, true, callOrderEnd - callOrderStart, httpClient);
                putBuyOrder = GetOrderDetails(putOrderId, put, currentTime, qty, currentUpTrade, true, putOrderEnd - putOrderStart, httpClient);

                //putBuyOrderTask = TradeEntryAsync(put, currentTime, qty, currentUpTrade, true);
                //callBuyOrderTask = TradeEntryAsync(call, currentTime, qty, currentUpTrade, true);

            }

            //Task.WaitAll(callBuyOrderTask, putBuyOrderTask);
            //if (putBuyOrderTask.IsCompletedSuccessfully && callBuyOrderTask.IsCompletedSuccessfully)
            //{

            //callBuyOrder = callBuyOrderTask.Result;
            //putBuyOrder = putBuyOrderTask.Result;
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
            
            TradedStraddleValue[tradeStrike] = new List<decimal>();
            TradedQuantity[tradeStrike][0] = 0;
            TradedQuantity[tradeStrike][1] = -1;
            LastTradeTime[tradeStrike] = null;
            CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] = 0;
            CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] = 0;

            //CancelSLOrders(tradeStrike, currentTime);
            //if (triggerStrike != 0)
            //{
            //    ReferenceStraddleValue[triggerStrike] = new List<decimal>();
            //}
            //else if (_tradedQty == 0)
            //{
            decimal[] strikes = ReferenceStraddleValue.Keys.ToArray<decimal>();
            foreach (decimal strike in strikes)
            {
                if (TradeReferenceStrikes[tradeStrike].Contains(strike))
                {
                    ReferenceStraddleValue[strike] = new List<decimal>();
                }
            }
            TradeReferenceStrikes[tradeStrike] = new List<decimal>();

            //_optionStrikeBuyExecuting[tradeStrike] = false;
            //}
            // }
        }

        //private void CancelSLOrders (decimal tradeStrike, DateTime currentTime)
        //{
        //    //Cancel all original super multiple orders
        //    List<Order> orders = CallOrdersTradeStrike.Where(x => x.Value == tradeStrike).Select(x => x.Key).ToList();

        //    //for(int i=0;i < orders.Count;)
        //    //{
        //    //    Order order = orders[i];
        //    //    Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, product: Constants.PRODUCT_SM, currentTime);
        //    //    //CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, string orderId, DateTime currentTime, string tradingSymbol)
        //    //    orders.Remove(order);
        //    //    OnTradeEntry(cancelOrder);
        //    //}


        //    foreach (Order order in orders)
        //    {
        //        Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS);
        //        //CancelKotakOrder(int algoInstance, AlgoIndex algoIndex, string orderId, DateTime currentTime, string tradingSymbol)
        //        CallOrdersTradeStrike.Remove(order);
        //        OnTradeEntry(cancelOrder);
        //    }


        //    //Cancel all original super multiple orders
        //    orders = PutOrdersTradeStrike.Where(x => x.Value == tradeStrike).Select(x => x.Key).ToList();
        //    foreach (Order order in orders)
        //    {
        //        Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, currentTime, product: Constants.PRODUCT_MIS);
        //        PutOrdersTradeStrike.Remove(order);
        //        OnTradeEntry(cancelOrder);
        //    }
        //}

        //private BS[] SetIVandGetBSModel(uint instrumentToken, DateTime currentTime, decimal lastPrice)
        //{
        //    BS[] bs = null;
        //    foreach (var bsModel in SpreadBSUniverse)
        //    {
        //        if (bsModel.Value[0].option.InstrumentToken == instrumentToken)
        //        {
        //            Option option = bsModel.Value[0].option;
        //            option.BaseInstrumentPrice = _baseInstrumentPrice;
        //            option.LastPrice = lastPrice;
        //            option.LastTradeTime = currentTime;
        //            //bsModel.Value[0].ImpliedVolatility(currentTime, lastPrice);

        //            var currentTimeToExp = bsModel.Value[0].GetExpirationTimeLine(option.LastTradeTime.Value);
        //            double iv = DerivativesHelper.BlackScholesImpliedVol(Convert.ToDouble(option.LastPrice), Convert.ToDouble(option.Strike), Convert.ToDouble(option.BaseInstrumentPrice),
        //                            currentTimeToExp.Value, Convert.ToDouble(bsModel.Value[0].RiskFree), 0d, bsModel.Value[0].OptionTypeFlag);

        //            option.IV = (decimal?)iv;
        //            bs = bsModel.Value;
        //            break;
        //        }
        //        else if (bsModel.Value[1].option.InstrumentToken == instrumentToken)
        //        {
        //            Option option = bsModel.Value[1].option;
        //            option.BaseInstrumentPrice = _baseInstrumentPrice;
        //            option.LastPrice = lastPrice;
        //            option.LastTradeTime = currentTime;

        //            //bsModel.Value[1].ImpliedVolatility(currentTime, lastPrice);

        //            var currentTimeToExp = bsModel.Value[1].GetExpirationTimeLine(option.LastTradeTime.Value);
        //            double iv = DerivativesHelper.BlackScholesImpliedVol(Convert.ToDouble(option.LastPrice), Convert.ToDouble(option.Strike), Convert.ToDouble(option.BaseInstrumentPrice),
        //                            currentTimeToExp.Value, Convert.ToDouble(bsModel.Value[1].RiskFree), 0d, bsModel.Value[1].OptionTypeFlag);

        //            option.IV = (decimal?)iv;

        //            bs = bsModel.Value;
        //            break;
        //        }
        //    }
        //    return bs;
        //}

        //private Order TradeEntry (Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, bool buyOrder)
        //{
        //    string orderId = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //       MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
        //        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK, arg:"");


        //    Order optionOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //       MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
        //        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

        //    //Order optionSellOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //    //   option.InstrumentToken, false, _stepQty * Convert.ToInt32(option.LotSize),
        //    //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.ZERODHA);

        //    optionOrder.OrderTimestamp = DateTime.Now;
        //    optionOrder.ExchangeTimestamp = DateTime.Now;

        //    return optionOrder;
        //}
        private string TradeEntry(Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, bool buyOrder, HttpClient httpClient)
        {
                string orderId = MarketOrders.PlaceInstantKotakOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                   MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), httpClient: httpClient);

            return orderId;
        }
        private void ExecuteTradeinKotak(Instrument option, DateTime currentTime, decimal optionStrike, decimal referenceStraddleValue,
            int qtyInlots, bool currentUpTrade, bool buyOrder, HttpClient httpClient)
        {

            string url = "https://tradeapi.kotaksecurities.com/apim/orders/1.0/order/mis";

            HttpResponseMessage httpResponse;
            StringContent dataJson = null;
            //HttpWebResponse webResponse = null;
            Dictionary<string, dynamic> responseDictionary;
            string accessToken = "dead6a06-113e-3ef6-bb46-897401304140";
            string sessionToken = "";
            HttpRequestMessage httpRequest = new HttpRequestMessage();
            httpRequest.Method = new HttpMethod("POST");
            httpRequest.Headers.Add("accept", "application/json");
            httpRequest.Headers.Add("consumerKey", "tpayXGqA2my6N8OXRdWYo1ynvoka");
            httpRequest.Headers.Add("Authorization", "Bearer " + accessToken);
            httpRequest.Headers.Add("sessionToken", sessionToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.RequestUri = new Uri(url);
            
            //using (Stream webStream = httpRequest.GetRequestStream())
            //using (StreamWriter requestWriter = new StreamWriter(webStream))
            //    requestWriter.Write(requestBody);

            try
            {
                //semaphore.WaitAsync();

                //Task<HttpResponseMessage> httpResponsetask = 
               httpClient.PostAsync(url, httpRequest.Content)
                    .ContinueWith((postTask, option) =>
                    {
                        HttpResponseMessage response = postTask.Result;
                        response.EnsureSuccessStatusCode();
                        
                        response.Content.ReadAsStreamAsync().ContinueWith(
                              (readTask) =>
                              {
                                  //Console.WriteLine("Web content in response:" + readTask.Result);
                                  using (StreamReader responseReader = new StreamReader(readTask.Result))
                                  {
                                      string response = responseReader.ReadToEnd();
                                      Dictionary<string, dynamic> orderStatus = GlobalLayer.Utils.JsonDeserialize(response);

                                      if (orderStatus != null && orderStatus.ContainsKey("Success") && orderStatus["Success"] != null)
                                      {
                                          Dictionary<string, dynamic> data = orderStatus["Success"]["NSE"];
                                          Order order = GetKotakOrder(Convert.ToString(data["orderId"]), _algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, ((Instrument)option).TradingSymbol);
                                          OnTradeEntry(order);
                                          _optionStrikeCovered[optionStrike] = false;
                                          _tradedQty = _tradedQty + _stepQty;

                                          TradedQuantity[tradeStrike][0] += _stepQty;
                                          TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                                          LastTradeTime[tradeStrike] = currentTime;

                                          TradedStraddleValue[tradeStrike].Add(straddleValue);
                                          //_tradedQty = _tradedQty + _stepQty;
                                          //TradedQuantity[tradeStrike][0] += _stepQty;
                                          //TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                                          //LastTradeTime[tradeStrike] = currentTime;
                                          //TradeReferenceStrikes[tradeStrike].Add(optionStrike);
                                          //straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;
                                          //TradedStraddleValue[tradeStrike].Add(straddleValue);
                                          //ReferenceStraddleValue[optionStrike].Add(currentSellStraddleValue);
                                          _optionStrikeCovered[optionStrike] = false;
                                      }
                                      else
                                      {
                                          throw new Exception(string.Format("Place Order status null for algo instance:{0}", algoInstance));
                                      }
                                  }
                              }
                              );
                        //Console.WriteLine("success response:" + response.IsSuccessStatusCode);
                    }, option);


                //_tradedQty = _tradedQty + _stepQty;
                //TradedQuantity[tradeStrike][0] += _stepQty;
                //TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                //LastTradeTime[tradeStrike] = currentTime;
                //TradeReferenceStrikes[tradeStrike].Add(optionStrike);

              //  OnTradeEntry(callSellOrder);
               // OnTradeEntry(putSellOrder);

                //straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;
                //TradedStraddleValue[tradeStrike].Add(straddleValue);

                ReferenceStraddleValue[optionStrike].Add(referenceStraddleValue);
                _optionStrikeCovered[optionStrike] = false;


                //    })

                //await httpResponsetask.ContinueWith(async (postTask) => {


                //    //Task<string> contentStreamTask = httpResponse.Content.ReadAsStringAsync();
                //    //contentStreamTask.Wait();
                //    //string contentStream = contentStreamTask.Result;
                //    //responseDictionary = Utils.JsonDeserialize(contentStream);
                //    //httpResponse.Dispose();

                //    // = JsonConvert.DeserializeObject<ClientHierarchyResult>(sResult);
                //    retunr postTask.Result.Content.ReadAsStringAsync().ContinueWith((resp) =>
                //    {
                //        var jresult = JsonConvert.DeserializeObject<JsonWrapper>(oHttpResponseMessage.Result.ToString());
                //        return jresult;
                //    });



                //});

                //    using (StreamReader responseReader = new StreamReader(httpResponsetask.))
                //    {
                //        string response = responseReader.ReadToEnd();
                //        if (_enableLogging) Console.WriteLine("DEBUG: " + (int)((HttpWebResponse)webResponse).StatusCode + " " + response + "\n");

                //        HttpStatusCode status = ((HttpWebResponse)webResponse).StatusCode;

                //        if (webResponse.ContentType == "application/json")
                //        {
                //            Dictionary<string, dynamic> responseDictionary = Utils.JsonDeserialize(response);
                //        }
                //    }



                //            httpResponsetask.Wait();
                //    httpResponse = httpResponsetask.Result;
                //    semaphore.Release();
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                throw ex;
            }

            //HttpStatusCode statusCode = httpResponse.StatusCode;

            //if (httpResponse.IsSuccessStatusCode)
            //{
            //    Task<string> contentStreamTask = httpResponse.Content.ReadAsStringAsync();
            //    contentStreamTask.Wait();
            //    string contentStream = contentStreamTask.Result;
            //    responseDictionary = str Utils.JsonDeserialize(contentStream);
            //    httpResponse.Dispose();
            //    return responseDictionary;
            //}
            //else if ((statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.ServiceUnavailable || statusCode == HttpStatusCode.InternalServerError) && (counter < 4))
            //{
            //    httpResponse.Dispose();
            //    throw new Exception(statusCode.ToString());
            //}
            //else
            //{
            //    httpResponse.Dispose();
            //    throw new Exception(statusCode.ToString());
            //}
        }
      
        //private async Task<Order> TradeEntryAsync(Instrument option, DateTime currentTime, int qtyInlots, string lastTradeTime, bool buyOrder)
        //{
        //    return await MarketOrders.PlaceKotakOrderAsync(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //       MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
        //        algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: lastTradeTime, httpClient: _httpClient);

        //}
        private Order TradeEntrySync(Instrument option, DateTime currentTime, int qtyInlots, string lastTradeTime, bool buyOrder)
        {
            return MarketOrders.PlaceKotakOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
               MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: lastTradeTime, httpClient: _httpClient);

        }

        private Order GetOrderDetails (string orderId, Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, 
            bool buyOrder, TimeSpan orderExecutionTime, HttpClient httpClient)
        {
            //_httpClient = _httpClientFactory.CreateClient();
            return MarketOrders.GetOrderDetails(_algoInstance, option.TradingSymbol, algoIndex, orderId, currentTime, option.LastPrice,
                MappedTokens[option.InstrumentToken], buyOrder, Constants.ORDER_TYPE_MARKET, qtyInlots * Convert.ToInt32(option.LotSize), 
                Tag: orderExecutionTime.TotalMilliseconds.ToString(), httpClient: httpClient);
        }

        public Order GetKotakOrder(string orderId, int algoInstance, AlgoIndex algoIndex, string status, string tradingSymbol)
        {
            Order oh = null;
            int counter = 0;
            var httpClient = _httpClientFactory.CreateClient();
                while (true)
            {
                try
                {
                    KotakOrder orderInfo = ZObjects.kotak.GetOrderHistory(orderId, httpClient);
                    orderInfo.Tradingsymbol = tradingSymbol;
                    orderInfo.OrderType = "Market";
                    oh = new Order(orderInfo);

                    if (oh.Status == status)
                    {
                        //order.AveragePrice = oh.AveragePrice;
                        break;
                    }
                    if (oh.Status == Constants.KORDER_STATUS_TRADED)
                    {
                        //order.AveragePrice = oh.AveragePrice;
                        break;
                    }
                    else if (oh.Status == Constants.KORDER_STATUS_CANCELLED)
                    {
                        Logger.LogWrite("Order Cancelled");
                        LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Cancelled", "GetOrder");
                        break;
                    }
                    //else 
                    else if (oh.Status == Constants.KORDER_STATUS_REJECTED)
                    {
                        //_stopTrade = true;
                        Logger.LogWrite("Order Rejected");
                        LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
                        break;
                        //throw new Exception("order did not execute properly");
                    }
                    if (counter > 150 && oh.Status == Constants.KORDER_STATUS_OPEN)
                    {
                        //_stopTrade = true;
                        Logger.LogWrite("order did not execute properly. Waited for 1 minutes");
                        LoggerCore.PublishLog(algoInstance, algoIndex, LogLevel.Error, oh.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order did not go through. Waited for 10 minutes", "GetOrder");
                        break;
                        //throw new Exception("order did not execute properly. Waited for 10 minutes");
                    }
                    counter++;

                    System.Threading.Thread.Sleep(400);
                }
                catch (Exception ex)
                {
                    Logger.LogWrite(ex.Message);
                    break;
                }
            }
            oh.AlgoInstance = algoInstance;
            oh.AlgoIndex = Convert.ToInt32(algoIndex);
            return oh;
        }

        //private Order TradeSLEntry(Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, bool buyOrder, decimal triggerPrice)
        //{
        //    //Order optionOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //    //   MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
        //    //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.KOTAK);

        //    ////Order optionSellOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //    ////   option.InstrumentToken, false, _stepQty * Convert.ToInt32(option.LotSize),
        //    ////    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.ZERODHA);

        //    //optionOrder.OrderTimestamp = DateTime.Now;
        //    //optionOrder.ExchangeTimestamp = DateTime.Now;

        //        Order stopLossOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, triggerPrice, MappedTokens[option.InstrumentToken], buyOrder,
        //            qtyInlots * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT, Tag: currentUpTrade.ToString(), triggerPrice:triggerPrice, broker: Constants.KOTAK, arg:"");

        //        //Order optionSellOrder = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
        //        //   option.InstrumentToken, false, _stepQty * Convert.ToInt32(option.LotSize),
        //        //    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), broker: Constants.ZERODHA);

        //        stopLossOrder.OrderTimestamp = DateTime.Now;
        //        stopLossOrder.ExchangeTimestamp = DateTime.Now;
        //    //else
        //    //{
        //    //    stopLossOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, stopLossOrder, DateTime.Now, product: Constants.PRODUCT_MIS);
        //    //    //OnTradeEntry(stopLossOrder);
        //    //}

        //    return stopLossOrder;
        //}
        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 12, 00))
            {
                using (var httpClient = _httpClientFactory.CreateClient())
                if (_tradedQty > 0)
                {
                    var localQty = new Dictionary<decimal, decimal[]>(TradedQuantity);
                    foreach (var element in localQty.Where(x => x.Value[0] > 0))
                    {
                        PlaceCloseOrder(element.Key, currentTime, element.Key > _baseInstrumentPrice, httpClient);
                    }
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                        "Completed", "TriggerEODPositionClose");

                _stopTrade = true;
                //Environment.Exit(0);
            }
        }
        //private void MonitorCandles(Tick tick, DateTime currentTime)
        //{
        //    //try
        //    //{
        //    //    uint token = tick.InstrumentToken;

        //    //    //Check the below statement, this should not keep on adding to 
        //    //    //TimeCandles with everycall, as the list doesnt return new candles unless built

        //    //    if (TimeCandles.ContainsKey(token))
        //    //    {
        //    //        candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
        //    //    }
        //    //    else
        //    //    {
        //    //        DateTime lastCandleEndTime;
        //    //        DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

        //    //        if (candleStartTime.HasValue)
        //    //        {
        //    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
        //    //                String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
        //    //            //candle starts from there
        //    //            candleManger.StreamingShortTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

        //    //        }
        //    //    }
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    _stopTrade = true;
        //    //    Logger.LogWrite(ex.Message + ex.StackTrace);
        //    //    Logger.LogWrite("Trading Stopped as algo encountered an error");
        //    //    //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
        //    //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //    //        String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
        //    //    Thread.Sleep(100);
        //    //    //Environment.Exit(0);
        //    //}
        //}

        //private void LoadBInstrumentEMA(uint bToken, int candleCount, DateTime currentTime)
        //{
        //    DateTime lastCandleEndTime;
        //    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
        //    try
        //    {
        //        lock (_bEMA)
        //        {
        //            if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
        //            {
        //                _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
        //            }
        //            int firstCandleFormed = 0;
        //            if (!_bEMALoading)
        //            {
        //                _bEMALoading = true;
        //                Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
        //            }


        //            if (TimeCandles.ContainsKey(bToken) && _bEMALoadedFromDB)
        //            {
        //                if (_firstCandleOpenPriceNeeded[bToken])
        //                {
        //                    //The below EMA token input is from the candle that just started, All historical prices are already fed in.
        //                    _bEMA.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

        //                    firstCandleFormed = 1;
        //                }
        //                //In case SQL loading took longer then candle time frame, this will be used to catch up
        //                if (TimeCandles[bToken].Count > 1)
        //                {
        //                    foreach (var price in TimeCandles[bToken])
        //                    {
        //                        _bEMA.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
        //                    }
        //                }
        //            }

        //            if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _bEMALoadedFromDB)
        //            {
        //                _bEMALoaded = true;
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
        //                    String.Format("{0} EMA loaded from DB for Base Instrument", BASE_EMA_LENGTH), "LoadBInstrumentEMA");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Trading Stopped as algo encountered an error");
        //        //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "MonitorCandles");
        //        Thread.Sleep(100);
        //    }
        //}
        //private void LoadHistoricalEMAs(DateTime currentTime)
        //{
        //    DateTime lastCandleEndTime;
        //    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

        //    try
        //    {
        //        var tokens = SubscriptionTokens.Where(x => x != _baseInstrumentToken && !_EMALoaded.Contains(x));
        //        StringBuilder sb = new StringBuilder();

        //        lock (lTokenEMA)
        //        {
        //            foreach (uint t in tokens)
        //            {
        //                if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
        //                {
        //                    _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
        //                }

        //                if (!lTokenEMA.ContainsKey(t) && !_SQLLoading.Contains(t))
        //                {
        //                    sb.AppendFormat("{0},", t);
        //                    _SQLLoading.Add(t);
        //                }
        //            }
        //        }
        //        string tokenList = sb.ToString().TrimEnd(',');
        //        int firstCandleFormed = 0;

        //        if (tokenList != string.Empty)
        //        {
        //            Task task = Task.Run(() => LoadHistoricalCandles(tokenList, _signalEMALength * 2, lastCandleEndTime));
        //        }
        //        foreach (uint tkn in tokens)
        //        {
        //            if (TimeCandles.ContainsKey(tkn) && lTokenEMA.ContainsKey(tkn))
        //            {
        //                if (_firstCandleOpenPriceNeeded[tkn])
        //                {
        //                    //The below EMA token input is from the candle that just started, All historical prices are already fed in.
        //                    sTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
        //                    lTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
        //                    signalTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);

        //                    firstCandleFormed = 1;
        //                }
        //                //In case SQL loading took longer then candle time frame, this will be used to catch up
        //                if (TimeCandles[tkn].Count > 1)
        //                {
        //                    foreach (var price in TimeCandles[tkn])
        //                    {
        //                        sTokenEMA[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
        //                        lTokenEMA[tkn].Process(TimeCandles[tkn].First().ClosePrice, isFinal: true);
        //                        signalTokenEMA[tkn].Process(TimeCandles[tkn].First().OpenPrice, isFinal: true);
        //                    }
        //                }
        //            }
        //            if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && lTokenEMA.ContainsKey(tkn))
        //            {
        //                _EMALoaded.Add(tkn);
        //                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("EMAs loaded from DB for {0}", tkn), "MonitorCandles");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Trading Stopped as algo encountered an error");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalEMAs");
        //        Thread.Sleep(100);
        //    }
        //}

        //private void CandleManger_TimeCandleFinished(object sender, Candle e)
        //{
        //    //try
        //    //{
        //    //    if (e.InstrumentToken == _baseInstrumentToken)
        //    //    {
        //    //        //if (_bEMALoaded)
        //    //        //{
        //    //        //    _bEMA.Process(e.ClosePrice, isFinal: true);
        //    //        //}
        //    //    }
        //    //    else if (_EMALoaded.Contains(e.InstrumentToken))
        //    //    {
        //    //        if (!lTokenEMA.ContainsKey(e.InstrumentToken) || !sTokenEMA.ContainsKey(e.InstrumentToken) || !signalTokenEMA.ContainsKey(e.InstrumentToken))
        //    //        {
        //    //            return;
        //    //        }

        //    //        sTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
        //    //        lTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);
        //    //        signalTokenEMA[e.InstrumentToken].Process(e.ClosePrice, isFinal: true);

        //    //        if (ActiveOptions.Any(x => x.InstrumentToken == e.InstrumentToken))
        //    //        {
        //    //            Instrument option = ActiveOptions.Find(x => x.InstrumentToken == e.InstrumentToken);

        //    //            decimal sema = sTokenEMA[e.InstrumentToken].GetValue<decimal>(0);
        //    //            decimal lema = lTokenEMA[e.InstrumentToken].GetValue<decimal>(0);
        //    //            decimal signalema = signalTokenEMA[e.InstrumentToken].GetValue<decimal>(0);

        //    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.CloseTime,
        //    //                String.Format("Candle ({4}) OHLC: {0} | {1} | {2} | {3}. sEMA:{6}. lEMA:{5}. Signal EMA: {7}", e.OpenPrice, e.HighPrice, e.LowPrice, e.ClosePrice
        //    //                , option.TradingSymbol, Decimal.Round(sema, 2), Decimal.Round(lema, 2), Decimal.Round(signalema, 2)), "CandleManger_TimeCandleFinished");
        //    //        }
        //    //    }
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    _stopTrade = true;
        //    //    Logger.LogWrite(ex.Message + ex.StackTrace);
        //    //    Logger.LogWrite("Closing Application");
        //    //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, e.CloseTime,
        //    //        String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CandleManger_TimeCandleFinished");
        //    //    Thread.Sleep(100);
        //    //}
        //}
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
        //            && TimeCandles.ContainsKey(activeCE.Value.InstrumentToken)
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
        //            && TimeCandles.ContainsKey(activePE.Value.InstrumentToken)
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
        //private OrderTrio PlaceTrailingOrder(Instrument option, Instrument newInstrument, decimal lastPrice,
        //    DateTime currentTime, OrderTrio orderTrio, decimal entryRSI)
        //{
        //    try
        //    {
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
        //                "Trailing market...", "PlaceTrailingOrder");

        //        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType,
        //            lastPrice, option.InstrumentToken, false,
        //            _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK);

        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
        //                string.Format("Closed Option {0}. Bought {1} lots @ {2}.", option.TradingSymbol,
        //                _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

        //        OnTradeExit(order);

        //        //Order slorder = orderTrio.SLOrder;
        //        //if (slorder != null)
        //        //{
        //        //    slorder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slorder, currentTime).Result;
        //        //    OnTradeExit(slorder);
        //        //}
        //        //Order tporder = orderTrio.TPOrder;
        //        //if (tporder != null)
        //        //{
        //        //    tporder = MarketOrders.CancelOrder(_algoInstance, algoIndex, tporder, currentTime).Result;
        //        //    OnTradeExit(tporder);
        //        //}

        //        decimal stopLoss = 0, targetProfit = 0;
        //        GetCriticalLevels(lastPrice, out stopLoss, out targetProfit);


        //        lastPrice = TimeCandles[newInstrument.InstrumentToken].Last().ClosePrice;
        //        order = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, lastPrice, //newInstrument.LastPrice,
        //            newInstrument.InstrumentToken, true, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
        //            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK);

        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
        //               string.Format("Traded Option {0}. Sold {1} lots @ {2}.", newInstrument.TradingSymbol,
        //               _tradeQty, order.AveragePrice), "PlaceTrailingOrder");

        //        //slorder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, stopLoss,
        //        //   newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize),
        //        //   algoIndex, currentTime, Constants.ORDER_TYPE_SLM);

        //        ////target profit
        //        //tporder = MarketOrders.PlaceOrder(_algoInstance, newInstrument.TradingSymbol, newInstrument.InstrumentType, targetProfit,
        //        //    newInstrument.InstrumentToken, false, _tradeQty * Convert.ToInt32(newInstrument.LotSize), algoIndex, currentTime, Constants.ORDER_TYPE_LIMIT);

        //        //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, tporder.OrderTimestamp.Value,
        //        //    string.Format("Placed Target Profit for {0} lots of {1} @ {2}", _tradeQty, newInstrument.TradingSymbol, tporder.AveragePrice), "TradeEntry");

        //        orderTrio = new OrderTrio();
        //        orderTrio.Option = newInstrument;
        //        orderTrio.Order = order;
        //        //orderTrio.SLOrder = slorder;
        //        //orderTrio.TPOrder = tporder;
        //        orderTrio.StopLoss = stopLoss;
        //        orderTrio.TargetProfit = targetProfit;
        //        orderTrio.EntryRSI = entryRSI;
        //        orderTrio.EntryTradeTime = currentTime;

        //        OnTradeEntry(order);
        //        //OnTradeEntry(slorder);
        //        //OnTradeEntry(tporder);

        //        return orderTrio;
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "PlaceTrailingOrder");
        //        Thread.Sleep(100);
        //        //Environment.Exit(0);
        //    }
        //    return orderTrio;

        //}
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
        //                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK);

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
        //private int GetTradeQty(decimal maxlossInPoints, uint lotSize)
        //{
        //    return _tradeQty;
        //}
        //private void GetOrder2PriceQty(int firstLegQty, decimal firstMaxLoss,
        //    Candle previousCandle, uint lotSize, out int qty, out decimal price)
        //{
        //    decimal buffer = _maxLossPerTrade - firstLegQty * lotSize * firstMaxLoss;

        //    decimal candleSize = previousCandle.ClosePrice - previousCandle.OpenPrice;

        //    price = previousCandle.ClosePrice - (candleSize * 0.2m);
        //    price = Math.Round(price * 20) / 20;

        //    qty = Convert.ToInt32(Math.Ceiling((buffer / price) / lotSize));
        //}
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
        //private bool CheckExitCriteria(uint token, decimal lastPrice, DateTime currentTime,
        //    OrderTrio orderTrio, IIndicatorValue sema, IIndicatorValue lema, out bool tpHit)
        //{
        //    /// Exit Criteria:
        //    /// RSI cross EMA from above and both crosses below 60, and both comes below 60
        //    /// RSI cross EMA from above between 50 and 60.
        //    /// Target profit hit
        //    tpHit = false;
        //    try
        //    {
        //        decimal semaValue = sema.GetValue<decimal>();
        //        decimal lemaValue = lema.GetValue<decimal>();

        //        if (lastPrice > orderTrio.TargetProfit)
        //        {
        //            tpHit = true;
        //            return true;
        //        }

        //        if ((lastPrice <= orderTrio.StopLoss)
        //            || ((currentTime - orderTrio.EntryTradeTime).TotalMinutes > _timeBandForExit && semaValue < lemaValue - _emaBandForExit)
        //            || ((currentTime - orderTrio.EntryTradeTime).TotalMinutes > 15)
        //            )
        //        {
        //            return true;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckEMA");
        //        Thread.Sleep(100);
        //    }
        //    return false;
        //}

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
                        ReferenceStraddleValue.TryAdd(strike, new List<decimal>());
                        TradeReferenceStrikes.TryAdd(strike, new List<decimal>());
                        TradedQuantity.TryAdd(strike, new decimal[] { 0, -1 });
                        HistoricalStraddleAverage.TryAdd(strike, new FixedSizedQueue<decimal>());
                        LastTradeTime.TryAdd(strike, null);
                        CallLastTradeTime.TryAdd(strike, null);
                        PutLastTradeTime.TryAdd(strike, null);
                        _optionStrikeCovered.TryAdd(strike, false);
                        _optionStrikeBuyExecuting.TryAdd(strike, false);
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

        //private bool TradeExit(uint token, DateTime currentTime, decimal lastPrice, OrderTrio orderTrio, IIndicatorValue sema, IIndicatorValue lema)
        //{
        //    try
        //    {
        //        if (orderTrio.Option != null && orderTrio.Option.InstrumentToken == token)
        //        {
        //            Instrument option = orderTrio.Option;
        //            bool tpHit = false;

        //            if (CheckExitCriteria(token, lastPrice, currentTime, orderTrio, sema, lema, out tpHit))
        //            {
        //                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
        //                       token, false, _tradeQty * Convert.ToInt32(option.LotSize),
        //                       algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

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

//        private Order CancelSLOrder(Order slOrder, DateTime currentTime)
//        {
//#if market

//            //Cancel the target profit limit order
//            slOrder = MarketOrders.CancelOrder(_algoInstance, algoIndex, slOrder, currentTime).Result;
//#elif local
//            slOrder.ExchangeTimestamp = currentTime;
//            slOrder.OrderTimestamp = currentTime;
//            slOrder.Tag = "Test";
//            slOrder.Status = Constants.ORDER_STATUS_CANCELLED;
//#endif
//            return slOrder;
//        }
//        private Order UpdateOrder(Order completedOrder, decimal lastPrice, DateTime currentTime)
//        {
//#if market
//            completedOrder = MarketOrders.GetOrder(completedOrder.OrderId, _algoInstance, algoIndex, Constants.ORDER_STATUS_COMPLETE);
//#elif local
//            completedOrder.AveragePrice = lastPrice;
//            completedOrder.Price = lastPrice;
//            completedOrder.OrderType = Constants.ORDER_TYPE_LIMIT;
//            completedOrder.ExchangeTimestamp = currentTime;
//            completedOrder.OrderTimestamp = currentTime;
//            completedOrder.Tag = "Test";
//            completedOrder.Status = Constants.ORDER_STATUS_COMPLETE;
//#endif
//            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, completedOrder);
//            return completedOrder;
//        }

//        private void GetCriticalLevels(decimal lastPrice, out decimal stopLoss, out decimal targetProfit)
//        {
//            stopLoss = 0; // Math.Round((lastPrice > _targetProfit ? lastPrice - _targetProfit : 2.0m) * 20) / 20;
//            targetProfit = Math.Round((lastPrice + _targetProfit) * 20) / 20;
//        }

//        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
//        {
//            try
//            {
//                double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;
//                DateTime? candleStartTime = null;
//                //if(mselapsed < 1000) //less than a second
//                //{
//                //    candleStartTime =  currentTime;
//                //}
//                if (mselapsed < 60 * 1000)
//                {
//                    candleStartTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
//                }
//                //else
//                //{
//                lastEndTime = currentTime.Date.Add(TimeSpan.FromMilliseconds(currentTime.TimeOfDay.TotalMilliseconds - mselapsed));
//                //}

//                return candleStartTime;
//            }
//            catch (Exception ex)
//            {
//                _stopTrade = true;
//                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
//                Logger.LogWrite("Closing Application");
//                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "CheckCandleStartTime");
//                Thread.Sleep(100);
//                Environment.Exit(0);
//                lastEndTime = DateTime.Now;
//                return null;
//            }
//        }

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

        //public SortedList<Decimal, Instrument>[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry, int strikePriceIncrement)
        //{
        //    DataLogic dl = new DataLogic();
        //    SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);
        //    return nodeData;
        //}

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
        private uint GetKotakToken(uint kiteToken)
        {
            return MappedTokens[kiteToken];
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
        //private Order ModifyOrder(Order tpOrder, decimal lastPrice, DateTime currentTime)
        //{
        //    try
        //    {
        //        Order order = MarketOrders.ModifyOrder(_algoInstance, algoIndex, 0, tpOrder, currentTime, lastPrice).Result;

        //        OnTradeEntry(order);
        //        return order;

        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(ex.Message + ex.StackTrace);
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
        //            String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ModifyOrder");
        //        Thread.Sleep(100);
        //        return null;
        //    }

        //}


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
