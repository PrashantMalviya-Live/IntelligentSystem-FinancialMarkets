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
using System.Net.Http;

namespace Algorithms.Algorithms
{
    public class StraddleWithCutOff : IZMQ
    {
        private readonly int _algoInstance;
        public List<Instrument> ActiveOptions { get; set; }
        public SortedList<decimal, Instrument>[] OptionUniverse { get; set; }

        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(StraddleWithCutOff source);
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

        private int _stMultiplier = 1;
        private int _stLength = 10;
        public Dictionary<uint, uint> MappedTokens { get; set; }
        private Candle _previousBNFCandle;
        //public StrangleOrderLinkedList sorderList;
        public Dictionary<decimal, OrderTrio> _straddleCallOrderTrio;
        public Dictionary<decimal, OrderTrio> _straddlePutOrderTrio;
        public OrderTrio _hedgeCallOrderTrio;
        public OrderTrio _hedgePutOrderTrio;
        public Dictionary<Decimal, SuperTrend> InstrumentSuperTrend { get; set; }
        public OrderTrio _soloCallOrderTrio;
        public OrderTrio _soloPutOrderTrio;
        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private decimal _referenceStraddleValue;
        private decimal _referenceValueForStraddleShift;
        private IHttpClientFactory _httpClientFactory;
        public List<Order> _pastOrders;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        //All active tokens that are passing all checks. This is kept seperate from tokenvolume as tokens may get in and out of the activeToken list.
        public List<uint> activeTokens;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private decimal _vixRSIThrehold;
        private RelativeStrengthIndex _vixRSI;

        private uint _baseInstrumentToken;
        private const uint VIX_TOKEN = 264969;
        private decimal _baseInstrumentPrice;
        private decimal _bInstrumentPreviousPrice;
        public const int CANDLE_COUNT = 30;
        public readonly decimal _minDistanceFromBInstrument;
        public readonly decimal _maxDistanceFromBInstrument;
        public readonly int _emaLength;
        private const int BASE_ADX_LENGTH = 30;
        private const int BASE_ST_LENGTH = 32;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private AverageDirectionalIndex _bADX;
        private bool _bADXLoaded = false, _bADXLoadedFromDB = false;
        private bool _bADXLoading = false;
        private bool _bSTLoaded = false, _bSTLoadedFromDB = false;
        private bool _bSTLoading = false;

        private bool _vixRSILoaded = false, _vixRSILoadedFromDB = false;
        private bool _vixRSILoading = false;

        private decimal _bookedPnl = 0;
        private decimal _unbookedPnl = 0;
        private Dictionary<decimal, decimal> _premiumStrikeSold = new Dictionary<decimal, decimal>();
        int _initialTrade = 0;
        int _tradedStraddleQty = 0;
        int _maxStraddleQty = 3;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        private bool _adxPeaked = false;
        private Instrument _activeCall;
        private Instrument _activePut;
        public const AlgoIndex algoIndex = AlgoIndex.MomentumBuyWithStraddle;
        //TimeSpan candletimeframe;
        private bool _straddleShift;
        bool callLoaded = false;
        bool putLoaded = false;
        bool referenceCallLoaded = false;
        bool referencePutLoaded = false;
        private decimal _targetProfit;
        private decimal _stopLoss;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;

        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        public StraddleWithCutOff(DateTime endTime, TimeSpan candleTimeSpan,
            uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength,
            decimal targetProfit, decimal stopLoss, bool straddleShift, decimal thresholdRatio = 1.67m,
            int algoInstance = 0, bool positionSizing = false, string uid = null,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;
            _endDateTime = endTime;
            _candleTimeSpan = candleTimeSpan;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _emaLength = emaLength;
            _stopTrade = true;
            _trailingStopLoss = _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            _straddleShift = straddleShift;
            _maxDistanceFromBInstrument = 800;
            _minDistanceFromBInstrument = 0;
            _thresholdRatio = thresholdRatio;
            _stopLossRatio = 1.3m;
            SubscriptionTokens = new List<uint>();
            ActiveOptions = new List<Instrument>();
            _bADX = new AverageDirectionalIndex();
            _vixRSI = new RelativeStrengthIndex();
            CandleSeries candleSeries = new CandleSeries();
            DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);
            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;
            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _vixRSIThrehold = 55;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, endTime,
                expiry.GetValueOrDefault(DateTime.Now), quantity, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            _straddleCallOrderTrio = new Dictionary<decimal, OrderTrio>();
            _straddlePutOrderTrio = new Dictionary<decimal, OrderTrio>();
            InstrumentSuperTrend = new Dictionary<decimal, SuperTrend>();
            _premiumStrikeSold = new Dictionary<decimal, decimal>();

            ZConnect.Login();
            KoConnect.Login(userId:uid);
            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();
            _httpClientFactory = httpClientFactory;
        }

        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken || tick.InstrumentToken == VIX_TOKEN) ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;
            try
            {
                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }
                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);
                    MonitorCandles(tick, currentTime);


                    if (token == _baseInstrumentToken && !_bSTLoaded)
                    {
                        LoadBInstrumentST(token, BASE_ST_LENGTH, currentTime);
                    }
                    //if (token == VIX_TOKEN && !_vixRSILoaded)
                    //{
                    //    LoadVixRSI(VIX_TOKEN, currentTime);
                    //}
                    //Update last price on options
                    UpdateOptionPrice(tick);


                    //Take trade after 9:20 AM only
                    //if (currentTime.TimeOfDay <= new TimeSpan(09, 20, 00))
                    //{
                    //    return;
                    //}
                    decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
                    _activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                    _activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                    if (_tradedStraddleQty < 1 && _straddleCallOrderTrio[atmStrike] == null && _straddlePutOrderTrio[atmStrike] == null)// && _bADX.IsFormed)
                    {
                        if (currentTime.TimeOfDay >= new TimeSpan(09, 17, 00) && _activePut.LastPrice * _activeCall.LastPrice != 0
                            && currentTime.TimeOfDay <= new TimeSpan(14, 40, 00))
                        {

                            TakeInitialTrades(currentTime, atmStrike);
                            _tradedStraddleQty++;
                        }
                    }



                    //if (_straddleCallOrderTrio[atmStrike] == null && _straddlePutOrderTrio[atmStrike] == null)// && _bADX.IsFormed)
                    //{
                    //    if (currentTime.TimeOfDay >= new TimeSpan(09, 20, 00) && _activePut.LastPrice * _activeCall.LastPrice != 0
                    //        && currentTime.TimeOfDay <= new TimeSpan(14, 40, 00)
                    //            // && _bADX.IsFormed && _bADX.MovingAverage.GetValue<decimal>(0) < 40
                    //            //&& /*_vixRSI.IsFormed &&*/ _vixRSI.GetValue<decimal>(0) < _vixRSIThrehold
                    //            && !_stopLossHit)
                    //    {
                    //        _activeCall.UpdateDelta(Convert.ToDouble(_activeCall.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));
                    //        _activePut.UpdateDelta(Convert.ToDouble(_activePut.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));

                    //        //Take trade after 9:15:15 AM only
                    //        if ((Math.Abs(Math.Abs(_activeCall.Delta) - Math.Abs(_activePut.Delta)) < 0.15))
                    //        {

                    //            atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
                    //            _activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                    //            _activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                    //            _straddleCallOrderTrio[atmStrike] = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                    //            _straddlePutOrderTrio[atmStrike] = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                    //            _higherProfit = false;

                    //            _premiumStrikeSold ??= new Dictionary<decimal, decimal>();

                    //            if (_premiumStrikeSold.ContainsKey(atmStrike))
                    //            {
                    //                _premiumStrikeSold[atmStrike] = _straddleCallOrderTrio[atmStrike].Order.AveragePrice + _straddlePutOrderTrio[atmStrike].Order.AveragePrice;
                    //            }
                    //            else
                    //            {
                    //                _premiumStrikeSold.Add(atmStrike, _straddleCallOrderTrio[atmStrike].Order.AveragePrice + _straddlePutOrderTrio[atmStrike].Order.AveragePrice);
                    //            }

                    //            LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                    //                    String.Format("Taking strike: {0}. BNF: {1}", _activeCall.Strike, _baseInstrumentPrice), "CandleManger_TimeCandleFinished");
                    //        }
                    //        //else if (_stopLossHit)// && _adxPeaked)
                    //        //{
                    //        //    _stopLossHit = false;
                    //        //    //_adxPeaked = false;
                    //        //    Candle candle = TimeCandles[_baseInstrumentToken].Skip(1).LastOrDefault();

                    //        //    atmStrike = candle.ClosePrice < _baseInstrumentPrice ? Math.Floor(_baseInstrumentPrice / 100) * 100 : Math.Ceiling(_baseInstrumentPrice / 100) * 100;
                    //        //    _activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                    //        //    _activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                    //        //    _straddleCallOrderTrio = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                    //        //    _straddlePutOrderTrio = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                    //        //    _higherProfit = false;
                    //        //}
                    //    }

                    //}
                    //else
                    //{
                    //    CheckSL(currentTime);
                    //}
                    CheckSL(currentTime);


                }

                //Closes all postions at 3:20 PM
                TriggerEODPositionClose(currentTime);
                //Convert to Iron fly at 3:20 PM
                // HedgeStraddle(currentTime);

                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
            }
        }
        private void CandleManger_TimeCandleFinished(object sender, Candle e)
        {
            if (e.InstrumentToken == _baseInstrumentToken)
            {
                //check if BNF crossed super trend.
                //yes then close current straddle and sell at the money straddle.

                if (_previousBNFCandle != null && InstrumentSuperTrend[_baseInstrumentToken].isFormed &&
                    ((_previousBNFCandle.ClosePrice < InstrumentSuperTrend[_baseInstrumentToken].GetValue() && _baseInstrumentPrice >= InstrumentSuperTrend[_baseInstrumentToken].GetValue())
                    || (_previousBNFCandle.ClosePrice > InstrumentSuperTrend[_baseInstrumentToken].GetValue() && _baseInstrumentPrice <= InstrumentSuperTrend[_baseInstrumentToken].GetValue()))
                    )
                {
                    CloseAndTakeNewStradde(e.CloseTime);

                    //this flag ensures that 3 atm strike straddles are sold.
                    _initialTrade = 0;
                }
                InstrumentSuperTrend ??= new Dictionary<decimal, SuperTrend>();
                if (!InstrumentSuperTrend.ContainsKey(_baseInstrumentToken))
                {
                    SuperTrend st = new SuperTrend(_stMultiplier, _stLength);
                    st.Process(e);
                    InstrumentSuperTrend.Add(_baseInstrumentToken, st);
                }
                else
                {
                    InstrumentSuperTrend[_baseInstrumentToken].Process(e);
                }
                _previousBNFCandle = e;
            }
        }

        private void TakeInitialTrades(DateTime currentTime, decimal atmStrike)
        {
            //}
            //decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
            //_activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
            //_activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];

            //_activeCall.UpdateDelta(Convert.ToDouble(_activeCall.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));
            //_activePut.UpdateDelta(Convert.ToDouble(_activePut.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));

            //Take trade after 9:15:15 AM only
            //if ((Math.Abs(Math.Abs(_activeCall.Delta) - Math.Abs(_activePut.Delta)) < 0.15))
            //{

                atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;
                _activeCall = OptionUniverse[(int)InstrumentType.CE][atmStrike];
                _activePut = OptionUniverse[(int)InstrumentType.PE][atmStrike];

                _straddleCallOrderTrio[atmStrike] = TradeEntry(_activeCall, currentTime, _activeCall.LastPrice, _tradeQty, false);
                _straddlePutOrderTrio[atmStrike] = TradeEntry(_activePut, currentTime, _activePut.LastPrice, _tradeQty, false);
                _higherProfit = false;

                _premiumStrikeSold ??= new Dictionary<decimal, decimal>();

                if (_premiumStrikeSold.ContainsKey(atmStrike))
                {
                    _premiumStrikeSold[atmStrike] = _straddleCallOrderTrio[atmStrike].Order.AveragePrice + _straddlePutOrderTrio[atmStrike].Order.AveragePrice;
                }
                else
                {
                    _premiumStrikeSold.Add(atmStrike, _straddleCallOrderTrio[atmStrike].Order.AveragePrice + _straddlePutOrderTrio[atmStrike].Order.AveragePrice);
                }

                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                        String.Format("Taking strike: {0}. BNF: {1}", _activeCall.Strike, _baseInstrumentPrice), "CandleManger_TimeCandleFinished");
            //}
        }

        /// <summary>
        /// Set SL on each option equal to 5% sum and then move the SL and just check SL hit in this method.
        /// </summary>
        /// <param name="currentTime"></param>
        private void CheckSL(DateTime currentTime)
        {
            for(int i = 0; i< _premiumStrikeSold.Count;i++)
            {
                var strikePremium = _premiumStrikeSold.ElementAt(i);
            //}
            //foreach (var strikePremium in _premiumStrikeSold)
            //{
                if (strikePremium.Value != 0)
                {
                    if (_straddleCallOrderTrio[strikePremium.Key] == null && _straddlePutOrderTrio[strikePremium.Key] == null)
                    {
                        _premiumStrikeSold[strikePremium.Key] = 0;
                    }
                    else if (_straddleCallOrderTrio[strikePremium.Key] != null && _straddlePutOrderTrio[strikePremium.Key] != null &&
                        OptionUniverse[0][strikePremium.Key].LastPrice + OptionUniverse[1][strikePremium.Key].LastPrice > 1.05m * strikePremium.Value)
                    {
                        if (OptionUniverse[(int)InstrumentType.CE][strikePremium.Key].LastPrice > OptionUniverse[(int)InstrumentType.PE][strikePremium.Key].LastPrice)
                        {
                            CloseOption(_straddleCallOrderTrio[strikePremium.Key], currentTime);
                            _straddleCallOrderTrio[strikePremium.Key] = null;
                            //Move SL for other leg to cost
                            SetSL(_straddlePutOrderTrio[strikePremium.Key], _straddlePutOrderTrio[strikePremium.Key].Order.AveragePrice, currentTime);
                        }
                        else
                        {
                            CloseOption(_straddlePutOrderTrio[strikePremium.Key], currentTime);
                            _straddlePutOrderTrio[strikePremium.Key] = null;
                            //Move SL for other leg to cost
                            SetSL(_straddleCallOrderTrio[strikePremium.Key], _straddleCallOrderTrio[strikePremium.Key].Order.AveragePrice, currentTime);
                        }
                    }
                    //Check SL for each leg
                    else
                    {
                        if (_straddleCallOrderTrio[strikePremium.Key] != null && _straddleCallOrderTrio[strikePremium.Key].SLOrder != null)
                        {
                            if (OptionUniverse[(int)InstrumentType.CE][strikePremium.Key].LastPrice > _straddleCallOrderTrio[strikePremium.Key].StopLoss)
                            {
                                //Cancel stop loss order
#if market
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddleCallOrderTrio[strikePremium.Key].SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddleCallOrderTrio[strikePremium.Key].SLOrder, currentTime);
#endif
                                OnTradeExit(slorder);

                                //CloseOption(_straddleCallOrderTrio[strikePremium.Key], currentTime);
                                _straddleCallOrderTrio[strikePremium.Key] = null;
                                _tradedStraddleQty--;
                            }
                        }
                        else if (_straddlePutOrderTrio[strikePremium.Key] != null && _straddlePutOrderTrio[strikePremium.Key].SLOrder != null)
                        {
                            if (OptionUniverse[(int)InstrumentType.PE][strikePremium.Key].LastPrice > _straddlePutOrderTrio[strikePremium.Key].StopLoss)
                            {
                                //Cancel stop loss order
#if market
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddlePutOrderTrio[strikePremium.Key].SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddlePutOrderTrio[strikePremium.Key].SLOrder, currentTime);
#endif
                                OnTradeExit(slorder);

                                //CloseOption(_straddlePutOrderTrio[strikePremium.Key], currentTime);
                                _straddlePutOrderTrio[strikePremium.Key] = null;
                                _tradedStraddleQty--;
                            }
                        }
                    }
                }
            }
            
        }
        private void CloseOption(OrderTrio orderTrio, DateTime currentTime)
        {
            if (orderTrio != null)
            {
                Instrument option = orderTrio.Option;

                //exit trade
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                    option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK);
                OnTradeEntry(order);

                if (orderTrio.SLOrder != null)
                {
#if market
                    Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                    Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.SLOrder, currentTime);
#endif
                    OnTradeEntry(slorder);
                }

                orderTrio = null;
            }
        }
        private void SetSL(OrderTrio orderTrio, decimal newStoploss, DateTime currentTime)
        {
            Instrument option = orderTrio.Option;

            //MarketOrders.ModifyOrder(_algoInstance, algoIndex, newStoploss, orderTrio.SLOrder, currentTime, orderTrio.Option.LastPrice);

#if market
            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                   option.InstrumentType, newStoploss, MappedTokens[option.InstrumentToken],
                   true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, orderType:"SLM", triggerPrice: newStoploss, 
                   broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());
#elif local
            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
               option.InstrumentType, newStoploss, MappedTokens[option.InstrumentToken],
               true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, orderType: "SLM", triggerPrice: newStoploss,
               broker: Constants.KOTAK);
#endif

            OnTradeEntry(order);
            orderTrio.SLOrder = order;
            orderTrio.StopLoss = order.AveragePrice;
            //OnTradeEntry(order);
        }
        private void UpdateOptionPrice(Tick tick)
        {
            bool optionFound = false;
            for (int i = 0; i < 2; i++)
            {
                foreach (var optionVar in OptionUniverse[i])
                {
                    Instrument option = optionVar.Value;
                    if (option != null && option.InstrumentToken == tick.InstrumentToken)
                    {
                        option.LastPrice = tick.LastPrice;
                        optionFound = true;
                        break;
                    }
                }
                if (optionFound)
                {
                    break;
                }
            }
        }
        private void CloseAndTakeNewStradde(DateTime currentTime)
        {
            decimal atmStrike = Math.Round(_baseInstrumentPrice / 100) * 100;

            for (int i = 0; i < _premiumStrikeSold.Count; i++)
            {
                var strikePremium = _premiumStrikeSold.ElementAt(i);
                //}
                //foreach (var strikePremium in _premiumStrikeSold)
                //{
                if (strikePremium.Value != 0)
                {
                    if (_straddlePutOrderTrio[strikePremium.Key] == null && _straddleCallOrderTrio[strikePremium.Key] == null)
                    {
                        _premiumStrikeSold[strikePremium.Key] = 0;
                    }
                    else
                    {
                        if (_straddlePutOrderTrio[strikePremium.Key] != null && _straddleCallOrderTrio[strikePremium.Key] != null)
                        {
                            if (atmStrike != strikePremium.Key)
                            {
                                //Instrument call = OptionUniverse[(int)InstrumentType.CE][strikePremium.Key];
                                //Instrument put = OptionUniverse[(int)InstrumentType.PE][strikePremium.Key];

                                //call.UpdateDelta(Convert.ToDouble(call.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));
                                //put.UpdateDelta(Convert.ToDouble(put.LastPrice), 0.1, currentTime, Convert.ToDouble(_baseInstrumentPrice));

                                //if ((Math.Abs(Math.Abs(call.Delta) - Math.Abs(put.Delta)) > 0.15))
                                //{
                                CloseOption(_straddleCallOrderTrio[strikePremium.Key], currentTime);
                                CloseOption(_straddlePutOrderTrio[strikePremium.Key], currentTime);
                                _straddleCallOrderTrio[strikePremium.Key] = null;
                                _straddlePutOrderTrio[strikePremium.Key] = null;

                                _tradedStraddleQty--;
                                //}
                            }
                        }
                        else
                        {
                            if (_straddleCallOrderTrio[strikePremium.Key] != null)
                            {
                                Instrument option = _straddleCallOrderTrio[strikePremium.Key].Option;

#if market
                            //enter
                            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                                option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                                true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());
#elif local
                                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                                    option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK);
#endif
                                OnTradeEntry(order);

                                if (_straddleCallOrderTrio[strikePremium.Key].SLOrder != null)
                                {
#if market
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddleCallOrderTrio[strikePremium.Key].SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                                    Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddleCallOrderTrio[strikePremium.Key].SLOrder, currentTime);
#endif
                                    OnTradeEntry(slorder);
                                    _tradedStraddleQty--;
                                }
                                _straddleCallOrderTrio[strikePremium.Key] = null;

                            }
                            else if (_straddlePutOrderTrio[strikePremium.Key] != null)
                            {
                                Instrument option = _straddlePutOrderTrio[strikePremium.Key].Option;

#if market
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                    option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());
#elif local
                                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                                    option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK);
#endif
                                OnTradeEntry(order);

                                if (_straddlePutOrderTrio[strikePremium.Key].SLOrder != null)
                                {
#if market
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddlePutOrderTrio[strikePremium.Key].SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                                    Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddlePutOrderTrio[strikePremium.Key].SLOrder, currentTime);
#endif
                                    OnTradeEntry(slorder);
                                    _tradedStraddleQty--;
                                }
                                _straddlePutOrderTrio[strikePremium.Key] = null;
                            }
                        }

                    }
                }
            }
        }
        
        private void CloseStraddle(DateTime currentTime)
        {
            for (int i = 0; i < _premiumStrikeSold.Count; i++)
            {
                var strikePremium = _premiumStrikeSold.ElementAt(i);
                //}
                //foreach (var strikePremium in _premiumStrikeSold)
                //{
                if (strikePremium.Value != 0)
                {
                    if (_straddlePutOrderTrio[strikePremium.Key] == null && _straddleCallOrderTrio[strikePremium.Key] == null)
                    {
                        _premiumStrikeSold[strikePremium.Key] = 0;
                    }
                    else
                    {
                        if (_straddleCallOrderTrio[strikePremium.Key] != null)
                        {
                            Instrument option = _straddleCallOrderTrio[strikePremium.Key].Option;

#if market
                //enter
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                    option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());
#elif local
                            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                                option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                                true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK);
#endif
                            OnTradeEntry(order);

                            if (_straddleCallOrderTrio[strikePremium.Key].SLOrder != null)
                            {
#if market
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddleCallOrderTrio[strikePremium.Key].SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddleCallOrderTrio[strikePremium.Key].SLOrder, currentTime);
#endif
                                OnTradeEntry(slorder);
                            }
                            _straddleCallOrderTrio[strikePremium.Key] = null;
                        }
                        if (_straddlePutOrderTrio[strikePremium.Key] != null)
                        {
                            Instrument option = _straddlePutOrderTrio[strikePremium.Key].Option;

#if market
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                    option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                    true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());
#elif local
                            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol,
                                option.InstrumentType, option.LastPrice, MappedTokens[option.InstrumentToken],
                                true, _tradeQty * Convert.ToInt32(option.LotSize), algoIndex, currentTime, broker: Constants.KOTAK);
#endif
                            OnTradeEntry(order);

                            if (_straddlePutOrderTrio[strikePremium.Key].SLOrder != null)
                            {
#if market
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddlePutOrderTrio[strikePremium.Key].SLOrder, currentTime, httpClient: _httpClientFactory.CreateClient());
#elif local
                                Order slorder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, _straddlePutOrderTrio[strikePremium.Key].SLOrder, currentTime);
#endif
                                OnTradeEntry(slorder);
                            }
                            _straddlePutOrderTrio[strikePremium.Key] = null;
                        }
                    }
                    
                }
            }
        }
        private void TriggerEODPositionClose(DateTime currentTime)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 20, 00))// && _referenceStraddleValue != 0)
            {
                CloseStraddle(currentTime);
            }
        }

        private void LoadBInstrumentST(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_firstCandleOpenPriceNeeded)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_bSTLoading)
                    {
                        _bSTLoading = true;
                        Task task = Task.Run(() => LoadBaseInstrumentST(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _bSTLoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            //_bADX.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);
                            //_bADX.Process(TimeCandles[bToken].First());
                            
                            InstrumentSuperTrend ??= new Dictionary<decimal, SuperTrend>();

                            if(!InstrumentSuperTrend.ContainsKey(_baseInstrumentToken))
                            {
                                SuperTrend st = new SuperTrend(_stMultiplier, _stLength);
                                st.Process(TimeCandles[bToken].First());

                                InstrumentSuperTrend.Add(bToken, st);
                            }
                            else
                            {
                                InstrumentSuperTrend[bToken].Process(TimeCandles[bToken].First());
                            }

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var candle in TimeCandles[bToken].Skip(1))
                            {
                                //_bADX.Process(candle);
                                InstrumentSuperTrend[bToken].Process(candle);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _bSTLoadedFromDB)
                    {
                        _bSTLoaded = true;
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} ST loaded from DB for Base Instrument", BASE_ST_LENGTH), "LoadBInstrumentST");
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
        
        private void MonitorCandles(Tick tick, DateTime currentTime)
        {
            try
            {
                uint token = tick.InstrumentToken;

                //Check the below statement, this should not keep on adding to 
                //TimeCandles with everycall, as the list doesnt return new candles unless built

                if (TimeCandles.ContainsKey(token))
                {
                    candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true); // TODO: USING LOCAL VERSION RIGHT NOW
                }
                else
                {
                    DateTime lastCandleEndTime;
                    DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                    if (candleStartTime.HasValue)
                    {
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
                        //candle starts from there
                        candleManger.StreamingTimeFrameCandle(tick, token, _candleTimeSpan, true, candleStartTime); // TODO: USING LOCAL VERSION

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
        
        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                //ENTRY ORDER - Sell ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    MappedTokens[option.InstrumentToken], buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, broker: Constants.KOTAK, httpClient: _httpClientFactory.CreateClient());

                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }
                order.OrderTimestamp = DateTime.Now;
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");

                orderTrio = new OrderTrio();
                orderTrio.Order = order;
                //orderTrio.SLOrder = slOrder;
                orderTrio.Option = option;
                orderTrio.EntryTradeTime = currentTime;
                OnTradeEntry(order);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
                Thread.Sleep(100);
            }
            return orderTrio;
        }
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        private DateTime? CheckCandleStartTime(DateTime currentTime, out DateTime lastEndTime)
        {
            try
            {
                DateTime? candleStartTime = null;

                if (currentTime.TimeOfDay < MARKET_START_TIME)
                {
                    candleStartTime = DateTime.Now.Date + MARKET_START_TIME;
                    lastEndTime = candleStartTime.Value;
                }
                else
                {

                    double mselapsed = (currentTime.TimeOfDay - MARKET_START_TIME).TotalMilliseconds % _candleTimeSpan.TotalMilliseconds;

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


        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (OptionUniverse == null ||
                (OptionUniverse[(int)InstrumentType.CE].Keys.Last() <= _baseInstrumentPrice + _minDistanceFromBInstrument
                || OptionUniverse[(int)InstrumentType.CE].Keys.First() >= _baseInstrumentPrice + _maxDistanceFromBInstrument)
                   || (OptionUniverse[(int)InstrumentType.PE].Keys.First() >= _baseInstrumentPrice - _minDistanceFromBInstrument
                   || OptionUniverse[(int)InstrumentType.PE].Keys.Last() <= _baseInstrumentPrice - _maxDistanceFromBInstrument)
                    )
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously

                    Dictionary<uint, uint> mTokens;
                    DataLogic dl = new DataLogic();
                     var allOptions = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, _maxDistanceFromBInstrument, out mTokens);

                    foreach (var item in allOptions[0])
                    {
                        InstrumentSuperTrend.TryAdd(item.Key, null);
                        _premiumStrikeSold.TryAdd(item.Key, 0);
                        _straddleCallOrderTrio.TryAdd(item.Key, null);
                        _straddlePutOrderTrio.TryAdd(item.Key, null);
                    }
                    foreach (var item in allOptions[1])
                    {
                        InstrumentSuperTrend.TryAdd(item.Key, null);
                        _premiumStrikeSold.TryAdd(item.Key, 0);
                        _straddleCallOrderTrio.TryAdd(item.Key, null);
                        _straddlePutOrderTrio.TryAdd(item.Key, null);
                    }

                    if (OptionUniverse == null)
                    {
                        OptionUniverse = allOptions;
                        MappedTokens = mTokens;
                    }
                    else
                    {
                        foreach (var item in allOptions[0])
                        {
                            OptionUniverse[0].TryAdd(item.Key, item.Value);
                            //InstrumentSuperTrend.TryAdd(item.Key, null);
                            //_premiumStrikeSold.TryAdd(item.Key, 0);
                            //_straddleCallOrderTrio.TryAdd(item.Key, null);
                            //_straddlePutOrderTrio.TryAdd(item.Key, null);
                        }
                        foreach (var item in allOptions[1])
                        {
                            OptionUniverse[1].TryAdd(item.Key, item.Value);
                            //InstrumentSuperTrend.TryAdd(item.Key, null);
                            //_premiumStrikeSold.TryAdd(item.Key, 0);
                            //_straddleCallOrderTrio.TryAdd(item.Key, null);
                            //_straddlePutOrderTrio.TryAdd(item.Key, null);
                        }
                        foreach (var item in mTokens)
                        {
                            MappedTokens.TryAdd(item.Key, item.Value);
                        }
                    }

                    //if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Option != null
                    //    && !OptionUniverse[(int)InstrumentType.CE].ContainsKey(_straddleCallOrderTrio.Option.Strike))
                    //{
                    //    OptionUniverse[(int)InstrumentType.CE].Add(_straddleCallOrderTrio.Option.Strike, _straddleCallOrderTrio.Option);
                    //}
                    //if (_straddlePutOrderTrio != null && _straddlePutOrderTrio.Option != null
                    //    && !OptionUniverse[(int)InstrumentType.PE].ContainsKey(_straddlePutOrderTrio.Option.Strike))
                    //{
                    //    OptionUniverse[(int)InstrumentType.PE].Add(_straddlePutOrderTrio.Option.Strike, _straddlePutOrderTrio.Option);
                    //}

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadOptionsToTrade");
                }

            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadOptionsToTrade");
                Thread.Sleep(100);
            }
        }

        private uint GetKotakToken(uint kiteToken)
        {
            return MappedTokens[kiteToken];
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
                    if (!SubscriptionTokens.Contains(_baseInstrumentToken))
                    {
                        SubscriptionTokens.Add(_baseInstrumentToken);
                    }
                    if (!SubscriptionTokens.Contains(VIX_TOKEN))
                    {
                        SubscriptionTokens.Add(VIX_TOKEN);
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
            if (baseInstrumentTick != null && baseInstrumentTick.LastPrice != 0)
            {
                _baseInstrumentPrice = baseInstrumentTick.LastPrice;
            }
            if (_baseInstrumentPrice == 0)
            {
                return false;
            }
            return true;
        }
        private void LoadBaseInstrumentST(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
               // lock (_bADX)
               // {
                    CandleSeries cs = new CandleSeries();

                    //DataLogic dl = new DataLogic();

                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, 
                    //    lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    List<Candle> historicalCandles = cs.LoadCandles(candlesCount,
                      CandleType.Time, lastCandleEndTime, bToken.ToString(), _candleTimeSpan);

                    InstrumentSuperTrend ??= new Dictionary<decimal, SuperTrend>();
                    if(!InstrumentSuperTrend.ContainsKey(bToken))
                    {
                        InstrumentSuperTrend.TryAdd(bToken, new SuperTrend(_stMultiplier, _stLength));
                    }
                    foreach (var candle in historicalCandles)
                    {
                        InstrumentSuperTrend[bToken].Process(candle);
                        //_bADX.Process(candle);
                    }
                    _bSTLoadedFromDB = true;
                //}
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

        private void LoadVIXRSIFromDB(uint token, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_vixRSI)
                {
                    CandleSeries cs = new CandleSeries();

                    //DataLogic dl = new DataLogic();

                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, 
                    //    lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    List<Candle> historicalCandles = cs.LoadCandles(16,
                      CandleType.Time, lastCandleEndTime, token.ToString(), _candleTimeSpan);

                    foreach (var candle in historicalCandles)
                    {
                        _vixRSI.Process(candle.ClosePrice);
                    }
                    _vixRSILoadedFromDB = true;
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
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
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
                Thread.Sleep(100);
            }
            else
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Health, e.SignalTime, "0", "CheckHealth");
                Thread.Sleep(100);
            }
        }

        private void PublishLog(object sender, ElapsedEventArgs e)
        {
            if (InstrumentSuperTrend != null)
            {
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
                String.Format("BNF Supertrend BNF: {0}", Decimal.Round(InstrumentSuperTrend[_baseInstrumentToken].GetValue(), 2)),
                "Log_Timer_Elapsed");

                for(int i = 0; i< _premiumStrikeSold.Count; i++)
                {
                    var strikePremium = _premiumStrikeSold.ElementAt(1);
                    if (strikePremium.Value != 0)
                    {
                        if (_straddleCallOrderTrio[strikePremium.Key] != null && _straddleCallOrderTrio[strikePremium.Key].Order != null
                    && _straddlePutOrderTrio[strikePremium.Key] != null && _straddlePutOrderTrio[strikePremium.Key].Order != null)
                        {
                            if (_activeCall.LastPrice * _activePut.LastPrice != 0)
                            {
                                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
                                String.Format("Call: {0}, Put: {1}. Straddle Profit: {2}. BNF: {3}, Call Delta : {4}, Put Delta {5}", _activeCall.LastPrice, _activePut.LastPrice,
                                _straddleCallOrderTrio[strikePremium.Key].Order.AveragePrice + _straddlePutOrderTrio[strikePremium.Key].Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice,
                                 _baseInstrumentPrice, Math.Round(_activeCall.Delta, 2), Math.Round(_activePut.Delta, 2)),
                                "Log_Timer_Elapsed");
                            }

                            Thread.Sleep(100);
                        }
                    }
                }
            }
        }

    }
}
