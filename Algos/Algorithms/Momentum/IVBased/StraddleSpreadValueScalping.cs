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
using static System.Net.Mime.MediaTypeNames;

namespace Algorithms.Algorithms
{
    public class StraddleSpreadValueScalping : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public Dictionary<uint, Instrument> OptionUniverse { get; set; }
        public Dictionary<uint, uint> MappedTokens { get; set; }
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(10);
        private readonly SemaphoreSlim linerSemaphone = new SemaphoreSlim(1);
        public SortedList<decimal, Instrument[]> StraddleUniverse { get; set; }
        public ConcurrentDictionary<decimal, FixedSizedQueue<decimal>> HistoricalStraddleAverage { get; set; }
        //public ConcurrentDictionary<decimal, List<decimal>> TradedStraddleValue { get; set; }
        public ConcurrentDictionary<decimal, List<decimal>> CallValueInStraddle { get; set; }
        public ConcurrentDictionary<decimal, List<decimal>> PutValueInStraddle { get; set; }
        //Count and straddle value for each strike price. This is used to determine the average and limit the trade per strike price.
        public ConcurrentDictionary<decimal, List<decimal>> ReferenceStraddleValue { get; set; }
        //Stores traded qty and up or downtrade bool = 1 means uptrade

        //All order ids for a particular trade strike
        private Dictionary<Order, decimal> CallOrdersTradeStrike { get; set; }
        private Dictionary<Order, decimal> PutOrdersTradeStrike { get; set; }

        public ConcurrentDictionary<decimal, decimal[]> TradedQuantity { get; set; }
        public ConcurrentDictionary<decimal, List<decimal>> TradeReferenceStrikes { get; set; }

        //Strike and time order
        private Dictionary<decimal, DateTime?> LastTradeTime { get; set; }
        private Dictionary<decimal, DateTime?> CallLastTradeTime { get; set; }
        private Dictionary<decimal, DateTime?> PutLastTradeTime { get; set; }

        private Dictionary<decimal, bool> _optionStrikeCovered;
        private Dictionary<decimal, bool> _optionStrikeBuyExecuting;
        public ConcurrentDictionary<decimal, decimal[]> CurrentSellStraddleValue { get; set; }
        public ConcurrentDictionary<decimal, decimal[]> CurrentBuyStraddleValue { get; set; }
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
        bool noEntry = false;
        //public Dictionary<uint, decimal> tokenLastClose; // This will come from the close in today's ticks
        //public Dictionary<uint, CentralPivotRange> tokenCPR;

        private List<Order> _activeOrders;
        private OrderTrio _callOrderTrio;
        private OrderTrio _putOrderTrio;

        public List<Order> _pastOrders;
        private bool _stopTrade;
        private int _tradedQty = 0;
        
        private int _stepQty = 0;
        private int _maxQty = 0;
        DateTime? _expiryDate;
        //TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private DateTime? _expiry;

        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        public readonly int _tradeQty;
        private readonly bool _positionSizing = false;
        private readonly decimal _maxLossPerTrade = 0;
        private readonly decimal _targetProfit;
        private readonly decimal _stopLoss;
        private decimal _openSpread;
        private decimal _closeSpread;
        private bool _upTrade;
        private const int CE = 1;
        private const int PE = 0;

        public const AlgoIndex algoIndex = AlgoIndex.ValueSpreadTrade;
        HttpClient _httpClient;
        IHttpClientFactory _httpClientFactory;
        public List<uint> SubscriptionTokens { get; set; }

        private object lockObject = new object();
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
            //_httpClient = _httpClientFactory.CreateClient();
            _expiry = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _targetProfit = targetProfit;
            _stopLoss = stopLoss;
            _openSpread = openSpread;
            _closeSpread = closeSpread;
            _stopTrade = true;
            //tokenLastClose = new Dictionary<uint, decimal>();
            _pastOrders = new List<Order>();
            _maxQty = maxQty;
            _stepQty = stepQty;
            SubscriptionTokens = new List<uint>();
            HistoricalStraddleAverage = new ConcurrentDictionary<decimal, FixedSizedQueue<decimal>>();  
            CurrentSellStraddleValue = new ConcurrentDictionary<decimal, decimal[]>();
            _optionStrikeCovered = new Dictionary<decimal, bool>();
            _optionStrikeBuyExecuting = new Dictionary<decimal, bool>();
            CurrentBuyStraddleValue = new ConcurrentDictionary<decimal, decimal[]>();
            //TradedStraddleValue = new ConcurrentDictionary<decimal, List<decimal>>();
            CallValueInStraddle = new ConcurrentDictionary<decimal, List<decimal>>();
            PutValueInStraddle = new ConcurrentDictionary<decimal, List<decimal>>();
            ReferenceStraddleValue = new ConcurrentDictionary<decimal, List<decimal>>();
            TradedQuantity = new ConcurrentDictionary<decimal, decimal[]>();
            TradeReferenceStrikes = new ConcurrentDictionary<decimal, List<decimal>>();
            LastTradeTime = new Dictionary<decimal, DateTime?>();
            CallLastTradeTime = new Dictionary<decimal, DateTime?>();
            PutLastTradeTime = new Dictionary<decimal, DateTime?>();

            ActiveOptions = new List<Instrument>();
            OptionUniverse = new Dictionary<uint, Instrument>();
            _activeOrders = new List<Order>(); 

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
                Arg3: _targetProfit, Arg4: _stopLoss, Arg5: _stopLoss);

            ZConnect.Login();
            KoConnect.Login(userId:"PM27031981");

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

        private async Task ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                uint token = tick.InstrumentToken;
                await linerSemaphone.WaitAsync();

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

                        int optionType = option.InstrumentType.Trim(' ').ToLower() == "ce" ? (int)InstrumentType.CE : (int)InstrumentType.PE;

                        decimal optionStrike = option.Strike;
#if market
                        CurrentSellStraddleValue[optionStrike][optionType] = tick.Bids[0].Price;
                        CurrentBuyStraddleValue[optionStrike][optionType] = tick.Offers[0].Price;
#elif local
                        CurrentSellStraddleValue[option.Strike][optionType] = tick.LastPrice;
                        CurrentBuyStraddleValue[option.Strike][optionType] = tick.LastPrice;
#endif
                        option.LastPrice = tick.LastPrice;

                        if (optionType == (int)InstrumentType.CE)
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
                            currentUpTrade = true;
                        }
                        else if (tradeStrike < _baseInstrumentPrice)
                        {
                            currentUpTrade = false;
                        }


                        if ((CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] * CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] != 0)
                            && (CurrentBuyStraddleValue.ContainsKey(tradeStrike) && CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.CE] * CurrentBuyStraddleValue[tradeStrike][(int)InstrumentType.PE] != 0)
                            )
                        {
                            decimal currentSellStraddleValue = CurrentSellStraddleValue[optionStrike].Sum();
                            if (
                                 (HistoricalStraddleAverage[optionStrike].Value != 0)
                                && (_tradedQty <= _maxQty - _stepQty)
                                &&
                                ((currentSellStraddleValue - (ReferenceStraddleValue[optionStrike].Count == 0 ? HistoricalStraddleAverage[optionStrike].Value :
                                (ReferenceStraddleValue[optionStrike].Last() + 2))) >= _openSpread)
                                && ReferenceStraddleValue[optionStrike].Count() < 3
                                 // && (optionStrike <= _baseInstrumentPrice + 300 && optionStrike >= _baseInstrumentPrice - 300)
                                  && (tradeStrike <= _baseInstrumentPrice + 300 && tradeStrike >= _baseInstrumentPrice - 300)
                                  && TradedQuantity[tradeStrike][0] < 3
                                && (currentTime.TimeOfDay <= new TimeSpan(14, 35, 00))
                                //&& !noEntry
                                //&& !_optionStrikeCovered[optionStrike]
                                )
                            {

                                _tradedQty = _tradedQty + _stepQty;

                                TradedQuantity[tradeStrike][0] += _stepQty;
                                TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                                LastTradeTime[tradeStrike] = currentTime;
                                TradeReferenceStrikes[tradeStrike].Add(optionStrike);

                                ReferenceStraddleValue[optionStrike].Add(currentSellStraddleValue);
                                //_optionStrikeCovered[optionStrike] = true;
                                //CallEntryTrades(optionStrike, tradeStrike, currentSellStraddleValue, currentTime, currentUpTrade);
                                ExecuteOrderEntry(optionStrike, tradeStrike, currentTime, currentUpTrade);
                                //noEntry = true;
                            }
                            HistoricalStraddleAverage[optionStrike].Enqueue(currentSellStraddleValue);
                            CurrentSellStraddleValue[optionStrike][(int)InstrumentType.CE] = 0;
                            CurrentSellStraddleValue[optionStrike][(int)InstrumentType.PE] = 0;

                            decimal currentBuyStraddleValue = CurrentBuyStraddleValue[tradeStrike].Sum();
                            decimal stoploss = 50;

                            if (_tradedQty > 0 && TradedQuantity[tradeStrike][0] > 0)
                            {
                                if ((CallValueInStraddle[tradeStrike].Count() > 0) && (CallValueInStraddle[tradeStrike].Count() == PutValueInStraddle[tradeStrike].Count()))
                                {
                                    decimal straddleSellAverage = CallValueInStraddle[tradeStrike].Average() + PutValueInStraddle[tradeStrike].Average();

                                    if (
                                        ((currentBuyStraddleValue - straddleSellAverage) > stoploss)
                                        ||
                                        ((HistoricalStraddleAverage[tradeStrike].Value > currentBuyStraddleValue + 2)
                                            && straddleSellAverage > currentBuyStraddleValue + _closeSpread)
                                    // && !_optionStrikeBuyExecuting[tradeStrike]
                                    )
                                    {
                                        //_optionStrikeBuyExecuting[tradeStrike] = true;
                                        //PlaceCloseOrder(tradeStrike, currentTime, currentUpTrade);
                                        CallOrderExit(tradeStrike, currentTime, currentUpTrade);
                                    }
                                }
                            }
                        }

                        //Close all Positions at 3:10
                        TriggerEODPositionClose(currentTime);
                    }
                }
                linerSemaphone.Release();
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
            //using (var httpClient = _httpClientFactory.CreateClient())
            //{
                ExecuteOrderEntry(optionStrike, tradeStrike, currentTime, currentUpTrade);
            //}
        }
        private void ExecuteOrderEntry(decimal optionStrike, decimal tradeStrike,
            DateTime currentTime, bool currentUpTrade)
        {
            //HttpClient httpClient = _httpClientFactory.CreateClient();

            Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
            Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

            //Order callSellOrder, putSellOrder, callStopLossOrder, putStopLossOrder;
            //Task<Order> callSellOrderTask, putSellOrderTask;
            //string callOrderId, putOrderId;
            //decimal straddleValue = 0;
            //DateTime callOrderStart, callOrderEnd, putOrderStart, putOrderEnd;

            //callSellOrder = TradeEntrySync(call, currentTime, _stepQty, CallLastTradeTime[optionStrike].Value.ToString(), false);
            //putSellOrder = TradeEntrySync(put, currentTime, _stepQty, PutLastTradeTime[optionStrike].Value.ToString(), false);



            if (currentUpTrade)
            {
                //putOrderStart = DateTime.Now;
                //putOrderId = TradeEntry(put, currentTime, _stepQty, currentUpTrade, false, httpClient);
                //callOrderStart = putOrderEnd = DateTime.Now;
                //callOrderId = TradeEntry(call, currentTime, _stepQty, currentUpTrade, false, httpClient);
                //callOrderEnd = DateTime.Now;
                //putSellOrder = GetOrderDetails(putOrderId, put, currentTime, _stepQty, currentUpTrade, false, putOrderEnd - putOrderStart, httpClient);
                //callSellOrder = GetOrderDetails(callOrderId, call, currentTime, _stepQty, currentUpTrade, false, callOrderEnd - callOrderStart, httpClient);


                //putOrderStart = DateTime.Now;
                //putOrderId = TradeEntry(put, currentTime, _stepQty, currentUpTrade, false, httpClient);
                //callOrderStart = putOrderEnd = DateTime.Now;
                //callOrderId = TradeEntry(call, currentTime, _stepQty, currentUpTrade, false, httpClient);
                //callOrderEnd = DateTime.Now;
                //putSellOrder = GetOrderDetails(putOrderId, put, currentTime, _stepQty, currentUpTrade, false, putOrderEnd - putOrderStart, httpClient);
                //callSellOrder = GetOrderDetails(callOrderId, call, currentTime, _stepQty, currentUpTrade, false, callOrderEnd - callOrderStart, httpClient);
#if market
                PostOrderInKotak(put, currentTime, optionStrike, _stepQty, currentUpTrade, false);
                PostOrderInKotak(call, currentTime, optionStrike, _stepQty, currentUpTrade, false);

#elif local
                PostOrderInLocal(put, currentTime, optionStrike, _stepQty, currentUpTrade, false);
                PostOrderInLocal(call, currentTime, optionStrike, _stepQty, currentUpTrade, false);
                
#endif

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
                //callOrderStart = DateTime.Now;
                //callOrderId = TradeEntry(call, currentTime, _stepQty, currentUpTrade, false, httpClient);
                //putOrderStart = callOrderEnd = DateTime.Now;
                //putOrderId = TradeEntry(put, currentTime, _stepQty, currentUpTrade, false, httpClient);
                //putOrderEnd = DateTime.Now;
                //callSellOrder = GetOrderDetails(callOrderId, call, currentTime, _stepQty, currentUpTrade, false, callOrderEnd - callOrderStart, httpClient);
                //putSellOrder = GetOrderDetails(putOrderId, put, currentTime, _stepQty, currentUpTrade, false, putOrderEnd - putOrderStart, httpClient);

#if market
                PostOrderInKotak(call, currentTime, optionStrike, _stepQty, currentUpTrade, false);
                PostOrderInKotak(put, currentTime, optionStrike, _stepQty, currentUpTrade, false);

#elif local
                PostOrderInLocal(call, currentTime, optionStrike, _stepQty, currentUpTrade, false);
                PostOrderInLocal(put, currentTime, optionStrike, _stepQty, currentUpTrade, false);
#endif

                //putOrderStart = DateTime.Now;
                //callSellOrderTask = TradeEntryAsync(call, currentTime, _stepQty, currentUpTrade, false);
                //putSellOrderTask = TradeEntryAsync(put, currentTime, _stepQty, currentUpTrade, false);
                //callOrderStart = putOrderEnd = DateTime.Now;

                //callOrderEnd = DateTime.Now;


                //callStopLossOrder = TradeSLEntry(call, currentTime, _stepQty, currentUpTrade, true, callSellOrder.AveragePrice * 2);
                //putStopLossOrder = TradeSLEntry(put, currentTime, _stepQty, currentUpTrade, true, putSellOrder.AveragePrice * 2);
            }




            //straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;
            //TradedStraddleValue[tradeStrike].Add(straddleValue);

            //ReferenceStraddleValue[optionStrike].Add(referenceStraddleValue);
            //_optionStrikeCovered[optionStrike] = false;

            //Close all uptrades if down trade is trigerred and vice versa
            //CloseAllTrades(_upTrade, currentTime);

            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Straddle sold: CE: {0}, PE: {1}, Total: {2}",
            //    Math.Round(callSellOrder.AveragePrice, 1), Math.Round(putSellOrder.AveragePrice, 1), Math.Round(callSellOrder.AveragePrice + putSellOrder.AveragePrice, 1)), "Trade Option");
            //}
        }


        private void CallOrderExit(decimal tradeStrike, DateTime currentTime, bool currentUpTrade)
        {
           // using (var httpClient = _httpClientFactory.CreateClient())
            //{
                PlaceCloseOrder(tradeStrike, currentTime, currentUpTrade);
            //}
        }

        void PlaceCloseOrder(decimal tradeStrike, DateTime currentTime, bool currentUpTrade)
        {
            
            Instrument call = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "ce");
            Instrument put = OptionUniverse.Values.First(x => x.Strike == tradeStrike && x.InstrumentType.Trim(' ').ToLower() == "pe");

            //Order callBuyOrder, putBuyOrder;
            //Task<Order> callBuyOrderTask, putBuyOrderTask;
            //string callOrderId, putOrderId;
            //DateTime callOrderStart, callOrderEnd, putOrderStart, putOrderEnd;

            int qty = Convert.ToInt32(TradedQuantity[tradeStrike][0]);
            _tradedQty = _tradedQty - Convert.ToInt32(qty);

            //callBuyOrderTask = TradeEntryAsync(call, currentTime, qty, CallLastTradeTime[tradeStrike].ToString(), true);
            //putBuyOrderTask = TradeEntryAsync(put, currentTime, qty, PutLastTradeTime[tradeStrike].ToString(), true);

            //callBuyOrder = TradeEntrySync(call, currentTime, qty, CallLastTradeTime[tradeStrike].ToString(), true);
            //putBuyOrder = TradeEntrySync(put, currentTime, qty, PutLastTradeTime[tradeStrike].ToString(), true);

            TradedQuantity[tradeStrike][0] = 0;
            TradedQuantity[tradeStrike][1] = -1;
            //LastTradeTime[tradeStrike] = null;
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

            if (currentUpTrade)
            {
                //callOrderStart = DateTime.Now;
                //callOrderId = TradeEntry(call, currentTime, qty, currentUpTrade, true, httpClient);
                //putOrderStart = callOrderEnd = DateTime.Now;
                //putOrderId = TradeEntry(put, currentTime, qty, currentUpTrade, true, httpClient);
                //putOrderEnd = DateTime.Now;
                //putBuyOrder = GetOrderDetails(putOrderId, put, currentTime, qty, currentUpTrade, true, putOrderEnd - putOrderStart, httpClient);
                //callBuyOrder = GetOrderDetails(callOrderId, call, currentTime, qty, currentUpTrade, true, callOrderEnd - callOrderStart, httpClient);

                //callBuyOrderTask = TradeEntryAsync(call, currentTime, qty, CallLastTradeTime[tradeStrike].ToString(), true);
                //putBuyOrderTask = TradeEntryAsync(put, currentTime, qty, PutLastTradeTime[tradeStrike].ToString(), true);

#if market
                PostOrderInKotak(call, currentTime, 0, qty, currentUpTrade, true);
                PostOrderInKotak(put, currentTime, 0, qty, currentUpTrade, true);
#elif local
                PostOrderInLocal(call, currentTime, 0, qty, currentUpTrade, true);
                PostOrderInLocal(put, currentTime, 0, qty, currentUpTrade, true);
#endif

            }
            else
            {
                //putBuyOrder = TradeEntry(put, currentTime, qty, currentUpTrade, true);
                //callBuyOrder = TradeEntry(call, currentTime, qty, currentUpTrade, true);
                //putOrderStart = DateTime.Now;
                //putOrderId = TradeEntry(put, currentTime, qty, currentUpTrade, true, httpClient);
                //callOrderStart = putOrderEnd = DateTime.Now;
                //callOrderId = TradeEntry(call, currentTime, qty, currentUpTrade, true, httpClient);
                //callOrderEnd = DateTime.Now;
                //callBuyOrder = GetOrderDetails(callOrderId, call, currentTime, qty, currentUpTrade, true, callOrderEnd - callOrderStart, httpClient);
                //putBuyOrder = GetOrderDetails(putOrderId, put, currentTime, qty, currentUpTrade, true, putOrderEnd - putOrderStart, httpClient);

#if market
                PostOrderInKotak(put, currentTime, 0, qty, currentUpTrade, true);
                PostOrderInKotak(call, currentTime, 0, qty, currentUpTrade, true);

#elif local
                PostOrderInLocal(put, currentTime, 0, qty, currentUpTrade, true);
                PostOrderInLocal(call, currentTime, 0, qty, currentUpTrade, true);
#endif
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

            //OnTradeExit(callBuyOrder);
            //OnTradeExit(putBuyOrder);

            //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, string.Format("Closed Straddle: CE: {0}, PE: {1}, Total: {2}",
            //Math.Round(callBuyOrder.AveragePrice, 1), Math.Round(putBuyOrder.AveragePrice, 1), Math.Round(callBuyOrder.AveragePrice + putBuyOrder.AveragePrice, 1)), "Trade Option");
            // }

            //TradedStraddleValue[tradeStrike] = new List<decimal>();


            //_optionStrikeBuyExecuting[tradeStrike] = false;
            //}
            // }
        }

        private string TradeEntry(Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, bool buyOrder, HttpClient httpClient)
        {
                string orderId = MarketOrders.PlaceInstantKotakOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
                   MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: currentUpTrade.ToString(), httpClient: httpClient);

            return orderId;
        }
        private void PostOrderInKotak(Instrument option, DateTime currentTime, decimal optionStrike,
            int qtyInlots, bool currentUpTrade, bool buyOrder)
        {

            HttpClient httpClient = _httpClientFactory.CreateClient();

            string url = "https://tradeapi.kotaksecurities.com/apim/orders/1.0/order/mis";
            

            StringContent dataJson = null;
            string accessToken = ZObjects.kotak.KotakAccessToken;
            string sessionToken = ZObjects.kotak.UserSessionToken;
            HttpRequestMessage httpRequest = new HttpRequestMessage();
            httpRequest.Method = new HttpMethod("POST");

            //httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            //httpClient.DefaultRequestHeaders.Add("consumerKey", ZObjects.kotak.ConsumerKey);
            //httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            //httpClient.DefaultRequestHeaders.Add("sessionToken", sessionToken);
            //httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            httpRequest.Headers.Add("accept", "application/json");
            httpRequest.Headers.Add("consumerKey", ZObjects.kotak.ConsumerKey);
            httpRequest.Headers.Add("Authorization", "Bearer " + accessToken);
            
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.RequestUri = new Uri(url);
            httpRequest.Headers.Add("sessionToken", sessionToken);
            //using (Stream webStream = httpRequest.GetRequestStream())
            //using (StreamWriter requestWriter = new StreamWriter(webStream))
            //    requestWriter.Write(requestBody);


            StringBuilder httpContentBuilder = new StringBuilder("{");
            httpContentBuilder.Append("\"instrumentToken\": ");
            httpContentBuilder.Append(MappedTokens[option.InstrumentToken]);
            httpContentBuilder.Append(", ");

            httpContentBuilder.Append("\"transactionType\": \"");
            httpContentBuilder.Append(buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL);
            httpContentBuilder.Append("\", ");

            httpContentBuilder.Append("\"quantity\": ");
            httpContentBuilder.Append(qtyInlots * Convert.ToInt32(option.LotSize));
            httpContentBuilder.Append(", ");

            httpContentBuilder.Append("\"price\": ");
            httpContentBuilder.Append(0); //Price = 0 means market order
            httpContentBuilder.Append(", ");

            //httpContentBuilder.Append("\"product\": \"");
            //httpContentBuilder.Append(Constants.PRODUCT_MIS.ToLower());
            //httpContentBuilder.Append("\", ");

            httpContentBuilder.Append("\"validity\": \"GFD\", ");
            httpContentBuilder.Append("\"disclosedQuantity\": 0");
            httpContentBuilder.Append(", ");
            httpContentBuilder.Append("\"triggerPrice\": 0");
            httpContentBuilder.Append(", ");
            httpContentBuilder.Append("\"variety\": \"True\"");
            httpContentBuilder.Append(", ");

            httpContentBuilder.Append("\"tag\": \"");
            httpContentBuilder.Append(currentUpTrade.ToString());
            httpContentBuilder.Append("\"");
            httpContentBuilder.Append("}");

            dataJson = new StringContent(httpContentBuilder.ToString(), Encoding.UTF8, Application.Json);
            httpRequest.Content = dataJson;

            //httpClient.DefaultRequestHeaders. = httpRequest.Headers;

            try
            {
               semaphore.WaitAsync();

                //Task<HttpResponseMessage> httpResponsetask = 
               //httpClient.PostAsync(url, httpRequest.Content)
                httpClient.SendAsync(httpRequest)
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
                                          Instrument instrument = (Instrument)option;
                                          Order order = null;
                                          Task<Order> orderTask;
                                          int counter = 0;
                                          while (true)
                                          {
                                             // order = GetOrderDetails(Convert.ToString(data["orderId"]), instrument, currentTime, qtyInlots, currentUpTrade, buyOrder, new TimeSpan(0, 2, 0), httpClient);
                                              orderTask = GetKotakOrder(Convert.ToString(data["orderId"]), _algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, instrument.TradingSymbol);
                                              orderTask.Wait();
                                              order = orderTask.Result;

                                              if (order.Status == Constants.KORDER_STATUS_TRADED)
                                              {
                                                  break;
                                              }
                                              else if (order.Status == Constants.KORDER_STATUS_REJECTED)
                                              {
                                                  //_stopTrade = true;
                                                  Logger.LogWrite("Order Rejected");
                                                  LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow), "Order Rejected", "GetOrder");
                                                  break;
                                                  //throw new Exception("order did not execute properly");
                                              }
                                              else if (counter > 5 && order.Status == Constants.KORDER_STATUS_OPEN)
                                              {
                                                  //_stopTrade = true;
                                                  Logger.LogWrite("order did not execute properly. Waited for 1 minutes");
                                                  LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, order.OrderTimestamp.GetValueOrDefault(DateTime.UtcNow),
                                                      "Order did not go through. Waited for 10 minutes", "GetOrder");
                                                  break;
                                                  //throw new Exception("order did not execute properly. Waited for 10 minutes");
                                              }
                                              counter++;
                                          }
                                          
                                          OnTradeEntry(order);

                                          //_optionStrikeCovered[optionStrike] = false;

                                          //_tradedQty = _tradedQty + _stepQty;

                                          //TradedQuantity[tradeStrike][0] += _stepQty;
                                          //TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                                          //LastTradeTime[tradeStrike] = currentTime;

                                          if (order.TransactionType.ToUpper() == Constants.TRANSACTION_TYPE_SELL)
                                          {
                                              if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                                              {
                                                  CallValueInStraddle[instrument.Strike].Add(order.AveragePrice);
                                              }
                                              else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                                              {
                                                  PutValueInStraddle[instrument.Strike].Add(order.AveragePrice);
                                              }
                                          }
                                          else
                                          {
                                              if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                                              {
                                                  CallValueInStraddle[instrument.Strike] = new List<decimal>();
                                              }
                                              else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                                              {
                                                  PutValueInStraddle[instrument.Strike] = new List<decimal>();
                                              }
                                          }


                                         MarketOrders. UpdateOrderDetails(_algoInstance, algoIndex, order);


                                          //TradedStraddleValue[tradeStrike].Add(straddleValue);
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
                                      else if(orderStatus != null && orderStatus.ContainsKey("fault") && orderStatus["fault"] != null)
                                      {
                                          Logger.LogWrite(Convert.ToString(orderStatus["fault"]["message"]));
                                          throw new Exception(string.Format("Error while placing order", _algoInstance));
                                      }
                                      else
                                      {
                                          throw new Exception(string.Format("Place Order status null for algo instance:{0}", _algoInstance));
                                      }
                                  }
                              }
                              );
                        //Console.WriteLine("success response:" + response.IsSuccessStatusCode);
                    }, option);

                semaphore.Release();

                //_tradedQty = _tradedQty + _stepQty;
                //TradedQuantity[tradeStrike][0] += _stepQty;
                //TradedQuantity[tradeStrike][1] = currentUpTrade ? 1 : 0;
                //LastTradeTime[tradeStrike] = currentTime;
                //TradeReferenceStrikes[tradeStrike].Add(optionStrike);

                //  OnTradeEntry(callSellOrder);
                // OnTradeEntry(putSellOrder);

                //straddleValue = callSellOrder.AveragePrice + putSellOrder.AveragePrice;
                //TradedStraddleValue[tradeStrike].Add(straddleValue);




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

            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                throw ex;
            }
            finally
            {
               // httpClient.Dispose();
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

        private void PostOrderInLocal(Instrument option, DateTime currentTime, decimal optionStrike, int qtyInLots, bool currentUpDate, bool buyOrder, string Tag = null)
        {

            ///TEMP, REMOVE Later
            decimal currentPrice = option.LastPrice;
            if (currentPrice == 0)
            {
                currentPrice = 10;
                // DataLogic dl = new DataLogic();
                // currentPrice = dl.RetrieveLastPrice(instrument_Token, tickTime, buyOrder);
                //order.AveragePrice = currentPrice;
            }
            //System.Threading.Thread.Sleep(5000);
            Order order = new Order()
            {
                AlgoInstance = _algoInstance,
                OrderId = Guid.NewGuid().ToString(),
                AveragePrice = currentPrice,
                ExchangeTimestamp = currentTime,
                OrderType = "MARKET",
                Price = currentPrice,
                Product = Constants.PRODUCT_NRML,
                CancelledQuantity = 0,
                FilledQuantity = qtyInLots,
                InstrumentToken = option.InstrumentToken,
                OrderTimestamp = currentTime,
                Quantity = qtyInLots,
                Validity = Constants.VALIDITY_DAY,
                TriggerPrice = currentPrice,
                Tradingsymbol = option.TradingSymbol,
                TransactionType = buyOrder ? "buy" : "sell",
                Status = "MARKET" == Constants.ORDER_TYPE_MARKET ? "Complete" : "Trigger Pending",
                Variety = "regular",
                //if(Tag != null)
                //{
                //   order.Tag = Tag;
                //}
                Tag = "Test",
                AlgoIndex = (int)algoIndex,
                StatusMessage = "Ordered",
            };

            if (Tag != null)
            {
                order.Tag = Tag;
            }
            
            OnTradeEntry(order);

            if (order.TransactionType.ToUpper() == Constants.TRANSACTION_TYPE_SELL)
            {
                if (option.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    CallValueInStraddle[option.Strike].Add(order.AveragePrice);
                }
                else if (option.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    PutValueInStraddle[option.Strike].Add(order.AveragePrice);
                }
            }
            else
            {
                if (option.InstrumentType.Trim(' ').ToLower() == "ce")
                {
                    CallValueInStraddle[option.Strike] = new List<decimal>();
                }
                else if (option.InstrumentType.Trim(' ').ToLower() == "pe")
                {
                    PutValueInStraddle[option.Strike] = new List<decimal>();
                }
            }

            MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);
        }
        private Order TradeEntrySync(Instrument option, DateTime currentTime, int qtyInlots, string lastTradeTime, bool buyOrder)
        {
            return MarketOrders.PlaceKotakOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice,
               MappedTokens[option.InstrumentToken], buyOrder, qtyInlots * Convert.ToInt32(option.LotSize),
                algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, Tag: lastTradeTime, httpClient: _httpClient);

        }

        public string BuildParam(string Key, dynamic Value)
        {
            if (Value is string)
            {
                return "\"" + Key + "\"" + ":" + "\"" + (string)Value + "\"";
            }
            else
            {
                return "\"" + Key + "\"" + ":" + Value;
            }
        }

        private Order GetOrderDetails (string orderId, Instrument option, DateTime currentTime, int qtyInlots, bool currentUpTrade, 
            bool buyOrder, TimeSpan orderExecutionTime, HttpClient httpClient)
        {
            //_httpClient = _httpClientFactory.CreateClient();
            return MarketOrders.GetOrderDetails(_algoInstance, option.TradingSymbol, algoIndex, orderId, currentTime, option.LastPrice,
                MappedTokens[option.InstrumentToken], buyOrder, Constants.ORDER_TYPE_MARKET, qtyInlots * Convert.ToInt32(option.LotSize), 
                Tag: orderExecutionTime.TotalMilliseconds.ToString(), httpClient: httpClient);
        }

        public Task<Order> GetKotakOrder(string orderId, int algoInstance, AlgoIndex algoIndex, string status, string tradingSymbol)
        {
            Order oh = null;
            int counter = 0;
            var httpClient = _httpClientFactory.CreateClient();

            StringBuilder url = new StringBuilder("https://tradeapi.kotaksecurities.com/apim/reports/1.0/orders/");//.Append(orderId);
            httpClient.BaseAddress = new Uri(url.ToString());
            httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("consumerKey", ZObjects.kotak.ConsumerKey);
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
            httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //HttpRequestMessage httpRequest = new HttpRequestMessage();
            //httpRequest.Method = new HttpMethod("GET");
            //httpRequest.Headers.Add("accept", "application/json");
            //httpRequest.Headers.Add("consumerKey", ZObjects.kotak.ConsumerKey);
            //httpRequest.Headers.Add("Authorization", "Bearer " + ZObjects.kotak.KotakAccessToken);
            //httpClient.DefaultRequestHeaders.Add("sessionToken", ZObjects.kotak.UserSessionToken);
            //httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //httpRequest.RequestUri = new Uri(url.ToString());

            //while (true)
            //{
            try
            {
                //return httpClient.SendAsync(httpRequest).ContinueWith((getTask) =>
                return httpClient.GetAsync(url.ToString()).ContinueWith((getTask) =>
                {

                    HttpResponseMessage response = getTask.Result;
                    response.EnsureSuccessStatusCode();

                    return response.Content.ReadAsStreamAsync().ContinueWith(
                          (readTask) =>
                          {
                              //Console.WriteLine("Web content in response:" + readTask.Result);
                              using (StreamReader responseReader = new StreamReader(readTask.Result))
                              {
                                  string response = responseReader.ReadToEnd();
                                  Dictionary<string, dynamic> orderData = GlobalLayer.Utils.JsonDeserialize(response);


                                  KotakOrder kOrder = null;
                                  List<KotakOrder> orderhistory = new List<KotakOrder>();
                                  dynamic oid;
                                  foreach (Dictionary<string, dynamic> item in orderData["success"])
                                  {
                                      if (item.TryGetValue("orderId", out oid) && Convert.ToString(oid) == orderId)
                                      {
                                          kOrder = new KotakOrder(item);
                                          break;
                                      }
                                  }

                                  kOrder.Tradingsymbol = tradingSymbol;
                                  kOrder.OrderType = "Market";
                                  oh = new Order(kOrder);
                                 
                                  oh.AlgoInstance = algoInstance;
                                  oh.AlgoIndex = Convert.ToInt32(algoIndex);
                                  return oh;
                              }
                          });
                }).Unwrap();
            }
            catch (Exception ex)
            {
                Logger.LogWrite(ex.Message);
                throw ex;
            }
            finally
            {
               // httpClient.Dispose();
            }
            //}
            //oh.AlgoInstance = algoInstance;
            //oh.AlgoIndex = Convert.ToInt32(algoIndex);
            //return oh;
            // }
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
                        PlaceCloseOrder(element.Key, currentTime, element.Key > _baseInstrumentPrice);
                    }
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                        "Completed", "TriggerEODPositionClose");

                _stopTrade = true;
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
                        //TradedStraddleValue.TryAdd(strike, new List<decimal>());
                        CallValueInStraddle.TryAdd(strike, new List<decimal>());
                        PutValueInStraddle.TryAdd(strike, new List<decimal>());
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
        private uint GetKotakToken(uint kiteToken)
        {
            return MappedTokens[kiteToken];
        }

        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

    }
}
