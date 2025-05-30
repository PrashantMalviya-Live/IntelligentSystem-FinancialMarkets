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
using KafkaFacade;
using System.Timers;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using Algos.Utilities.Views;
using FirebaseAdmin.Messaging;

namespace Algorithms.Algorithms
{
    public class TJ3 : IZMQ, IObserver<Tick>, IKConsumer
    {
        private int _algoInstance;
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(TJ3 source);
        [field: NonSerialized]
        public event OnOptionUniverseChangeHandler OnOptionUniverseChange;

        [field: NonSerialized]
        public delegate void OnCriticalEventsHandler(string title, string body);
        [field: NonSerialized]
        public event OnCriticalEventsHandler OnCriticalEvents;


        [field: NonSerialized]
        public delegate void OnTradeEntryHandler(Order st);
        [field: NonSerialized]
        public event OnTradeEntryHandler OnTradeEntry;

        [field: NonSerialized]
        public delegate void OnTradeExitHandler(Order st);
        [field: NonSerialized]
        public event OnTradeExitHandler OnTradeExit;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(10);

        private List<OrderTrio> _orderTrios;
        private List<OrderTrio> _orderTriosFromEMATrades;
        
        List<uint> _RSILoaded, _ADXLoaded, _schLoaded, _cprLoaded;
        List<uint> _SQLLoadingRSI, _SQLLoadingAdx, _SQLLoadingSch;

        public Dictionary<uint, Instrument> AllStocks { get; set; }
        Dictionary<uint, HalfTrend> _stockTokenHalfTrend;
        Dictionary<uint, AverageDirectionalIndex> _stockTokenADX;
        TimeSpan _candleTimeSpanShort, _candleTimeSpanLong;

        decimal _totalPnL;
        decimal _trailingStopLoss;
        decimal _algoStopLoss;
        private bool _stopTrade;
        private bool _stopLossHit = false;
        private CentralPivotRange _cpr;
        private CentralPivotRange _weeklycpr;
        private CentralPivotRange _rcpr;
        private CentralPivotRange _rweeklycpr;
        public Queue<uint> TimeCandleWaitingQueue;
        public List<uint> tokenExits;
        DateTime _endDateTime;
        DateTime? _expiryDate;
        TimeSpan _candleTimeSpan;
        public decimal _strikePriceRange;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        private bool _ibLoaded = false;
        private Instrument _activeFuture, _referenceFuture;
        public const int CANDLE_COUNT = 30;
        public readonly TimeSpan MARKET_START_TIME = new TimeSpan(9, 15, 0);
        public readonly TimeSpan MARKET_CLOSE_TIME = new TimeSpan(15, 30, 0);
        public readonly TimeSpan EXTREME_END_TIME = new TimeSpan(12, 00, 0);
        public readonly TimeSpan INITIAL_BALANCE_END_TIME = new TimeSpan(11, 00, 0);

        private bool _referenceCandleLoaded = false;
        private bool _baseCandleLoaded = false;
        private Candle _bCandle;
        public Candle _rCandle;
        public readonly TimeSpan FIRST_CANDLE_CLOSE_TIME = new TimeSpan(9, 20, 0);
        public int _tradeQty;
        private bool _positionSizing = false;
        private decimal _maxLossPerTrade = 0;
        private decimal _thresholdRatio;
        private decimal _stopLossRatio;
        private decimal _ibLowestPrice = 1000000, _ibHighestPrice;
        private decimal _ibrLowestPrice = 1000000, _ibrHighestPrice;
        private decimal _swingLowPrice = 1000000, _swingHighPrice;
        private decimal _rswingLowPrice = 1000000, _rswingHighPrice;

        private const decimal CANDLE_WICK_SIZE = 25;//45;//10000;//45;//35
        private const decimal CANDLE_BODY_MIN = 5;
        private const decimal CANDLE_BODY = 20;//40; //25
        private const decimal CANDLE_BODY_BIG = 25;//35;
        private const decimal CANDLE_BODY_EXTREME = 45;//95;
        private const decimal EMA_ENTRY_RANGE = 35;
        private const decimal RISK_REWARD = 2.3M;
        private OHLC _previousDayOHLC;
        private OHLC _previousWeekOHLC;
        private OHLC _rpreviousDayOHLC;
        private OHLC _rpreviousWeekOHLC;
        private decimal _pnl = 0;
        private Candle _prCandle;
        int _swingCounter = 60;
        int _rswingCounter = 60;
        private enum MarketScenario
        {
            Bullish = 1,
            Neutral = 0,
            Bearish = -1
        }
        private MarketScenario _extremeScenario = MarketScenario.Neutral;
        private DateTime? _currentDate;
        private List<DateTime> _dateLoaded;
        private Candle _pCandle;
        private SortedList<decimal, int> _criticalLevels;
        private SortedList<decimal, int> _criticalLevelsWeekly;
        private SortedList<decimal, int> _rcriticalLevels;
        private SortedList<decimal, int> _rcriticalLevelsWeekly;

        private bool _criticalValuesLoaded = false;
        Dictionary<uint, bool> _firstCandleOpenPriceNeeded;
        public const AlgoIndex algoIndex = AlgoIndex.TJ3;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private bool? _firstCandleOutsideRange;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        private IHttpClientFactory _httpClientFactory;
        public List<uint> SubscriptionTokens { get; set; }
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        FirebaseMessaging _firebaseMessaging;
        ExponentialMovingAverage _indexEMA;
        ExponentialMovingAverage _rindexEMA;
        bool _indexEMALoaded = false;
        bool _rindexEMALoaded = false;
        bool _indexEMALoading = false;
        bool _indexEMALoadedFromDB = false;
        private IIndicatorValue _indexEMAValue;
        private IIndicatorValue _rindexEMAValue;
        private uint _referenceBToken = 0;

        public TJ3(TimeSpan candleTimeSpan, uint baseInstrumentToken, uint referenceBaseToken,
            DateTime? expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            //OHLC previousDayOHLC, OHLC previousWeekOHLC, decimal previousDayBodyHigh, 
            //decimal previousDayBodyLow, decimal previousSwingHigh, decimal previousSwingLow,
            //decimal previousWeekLow, decimal previousWeekHigh, 
            int algoInstance = 0, bool positionSizing = false,
            decimal maxLossPerTrade = 0, IHttpClientFactory httpClientFactory = null)
        {
            ZConnect.Login();
            KoConnect.Login(userId: uid);

            _httpClientFactory = httpClientFactory;
            //_firebaseMessaging = firebaseMessaging;
            _candleTimeSpan = candleTimeSpan;

            _candleTimeSpanLong = new TimeSpan(0, 15, 0);
            _candleTimeSpanShort = new TimeSpan(0, 5, 0);

            _baseInstrumentToken = baseInstrumentToken;
            _referenceBToken = referenceBaseToken;
            //_referenceBToken = Convert.ToUInt32(Constants.BANK_NIFTY_TOKEN);

            _stopTrade = true;
            _trailingStopLoss = _stopLoss = stopLoss;
            _targetProfit = targetProfit;

            _tradeQty = quantity;
            _positionSizing = positionSizing;
            _maxLossPerTrade = maxLossPerTrade;

            SetUpInitialData(expiry, algoInstance);


            //#if local
            //            _dateLoaded = new List<DateTime>();
            //            LoadPAInputsForTest();
            //#endif

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }
        private void SetUpInitialData(DateTime? expiry, int algoInstance = 0)
        {
            _expiryDate = expiry;
            _orderTrios = new List<OrderTrio>();
            _orderTriosFromEMATrades = new List<OrderTrio>();
            _criticalLevels = new SortedList<decimal, int>();
            _criticalLevelsWeekly = new SortedList<decimal, int>();
            _rcriticalLevels = new SortedList<decimal, int>();
            _rcriticalLevelsWeekly = new SortedList<decimal, int>();

            _indexEMA = new ExponentialMovingAverage(length: 20);

            _stockTokenADX = new Dictionary<uint, AverageDirectionalIndex>(13);
            _stockTokenHalfTrend = new Dictionary<uint, HalfTrend>();

            _ADXLoaded = new List<uint>();

            _SQLLoadingAdx = new List<uint>();


            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            //DateTime ydayEndTime = _endDateTime.AddDays(-1).Date + new TimeSpan(15, 30, 00);

            _firstCandleOpenPriceNeeded = new Dictionary<uint, bool>();
            TimeCandles = new Dictionary<uint, List<Candle>>();
            candleManger = new CandleManger(TimeCandles, CandleType.Time);
            candleManger.TimeCandleFinished += CandleManger_TimeCandleFinished;
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, DateTime.Now,
                expiry.GetValueOrDefault(DateTime.Now), _tradeQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                (float)_candleTimeSpan.TotalMinutes, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, positionSizing: _positionSizing, maxLossPerTrade: _maxLossPerTrade);

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();
        }



        private void ActiveTradeIntraday(Tick tick)
        {
            DateTime currentTime = (tick.InstrumentToken == _baseInstrumentToken || tick.InstrumentToken == _referenceBToken) ?
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

                    LoadFutureToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);

                    // ADX with 15 min time frame and candle with 5 mins time frame
                    if (!_ADXLoaded.Contains(token))// && token != _baseInstrumentToken)
                    {
                        LoadHistoricalADX(currentTime);
                    }
#if local

                    // SetParameters(currentTime);
#endif
                    UpdateFuturePrice(tick.LastPrice, token);
                    MonitorCandles(tick, currentTime);
                }

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
                _baseCandleLoaded = true;
                _bCandle = e;
            }
            else if (e.InstrumentToken == _referenceBToken)
            {
                _referenceCandleLoaded = true;
                _rCandle = e;
            }

            if (_baseCandleLoaded && _referenceCandleLoaded)
            {
                #region Halftrend (5 mins) and ADX (15 mins, Length 13). Candle time frame 5 mins.
                
                if (_stockTokenADX.ContainsKey(_bCandle.InstrumentToken))
                {
                    _stockTokenHalfTrend[_bCandle.InstrumentToken].Process(_bCandle);
                    if (_bCandle.CloseTime.TimeOfDay.Minutes % 15 == 0)
                    {
                        Candle c = GetLastCandle(_bCandle.InstrumentToken, 3);
                        if (c != null)
                        {
                            _stockTokenADX[_bCandle.InstrumentToken].Process(c);
                        }
                    }
                    //else if (_bCandle.CloseTime.TimeOfDay.Minutes % 10 == 0)
                    //{
                    //    Candle c = GetLastCandle(_bCandle.InstrumentToken, 2);

                    //    if (c != null)
                    //    {
                    //        c.State = CandleStates.Inprogress;
                    //        c.Final = false;
                    //        _stockTokenADX[_bCandle.InstrumentToken].Process(c);
                    //    }
                    //}
                    //else
                    //{
                    //    _bCandle.State = CandleStates.Inprogress;
                    //    _bCandle.Final = false;
                    //    _stockTokenADX[_bCandle.InstrumentToken].Process(_bCandle);
                    //    _bCandle.State = CandleStates.Finished;
                    //    _bCandle.Final = true;
                    //}

                    decimal adxValue = _stockTokenADX[_bCandle.InstrumentToken].MovingAverage.GetCurrentValue<decimal>();
                    decimal dplus = _stockTokenADX[_bCandle.InstrumentToken].Dx.Plus.GetCurrentValue<decimal>();
                    decimal dminus = _stockTokenADX[_bCandle.InstrumentToken].Dx.Minus.GetCurrentValue<decimal>();
                    decimal htred = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetTrend(); // 0 is up trend

                    decimal htvalue = _stockTokenHalfTrend[_bCandle.InstrumentToken].GetCurrentValue<decimal>(); // 0 is up trend

                    if (adxValue > 23)
                    {
                        if (htred == 0 && dplus > dminus)
                        {
                            int qty = _orderTrios.Count == 0 ? _tradeQty : _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? 2 * _tradeQty : 0;

                            if (qty > 0)
                            {
                                OrderTrio orderTrio = new OrderTrio();
                                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                                   _activeFuture.KToken, true, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                                   Tag: String.Format("ADX {0}, DPlux {1}, DMinus {2} , HtUP {3}", adxValue, dplus, dminus, htred),
                                   product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                                OnTradeEntry(orderTrio.Order);
                                orderTrio.Option = _activeFuture;
                                _orderTrios.Clear();
                                _orderTrios.Add(orderTrio);
                                orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_activeFuture.LotSize);
                                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * -1;
                            }
                        }
                        else if (htred == 1 && dplus < dminus)  //down 
                        {
                            int qty = _orderTrios.Count == 0 ? _tradeQty : _orderTrios[0].Order.TransactionType.ToLower().Trim() == "buy" ? 2 * _tradeQty : 0;
                            if (qty > 0)
                            {
                                OrderTrio orderTrio = new OrderTrio();
                                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                                   _activeFuture.KToken, false, qty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                                   Tag: String.Format("ADX {0}, DPlux {1}, DMinus {2} , HtUP {3}", adxValue, dplus, dminus, htred),
                                   product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                                OnTradeEntry(orderTrio.Order);
                                orderTrio.Option = _activeFuture;
                                //orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_activeFuture.LotSize);
                                _orderTrios.Clear();
                                _orderTrios.Add(orderTrio);
                                orderTrio.Order.Quantity = _tradeQty * Convert.ToInt32(_activeFuture.LotSize);
                                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice;
                            }
                        }
                        if (_orderTrios.Count > 0 && ((htred == 1 && dplus > dminus) || (htred == 0 && dplus < dminus) 
                            || (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" && _orderTrios[0].Order.AveragePrice < _activeFuture.LastPrice)
                            || (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "buy" && _orderTrios[0].Order.AveragePrice > _activeFuture.LastPrice)) )
                        {
                            OrderTrio orderTrio = new OrderTrio();
                            orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                               _activeFuture.KToken, _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell", _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                               Tag: String.Format("ADX {0}, DPlux {1}, DMinus {2} , HtUP {3}", adxValue, dplus, dminus, htred),
                               product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            OnTradeEntry(orderTrio.Order);
                            orderTrio.Option = _activeFuture;
                            _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? -1 : 1);
                            
                            _orderTrios.Clear();
                            //_orderTrios.Add(orderTrio);
                        }
                    }
                }
                #endregion

                TriggerEODPositionClose(_bCandle.CloseTime, _bCandle.ClosePrice);
                _baseCandleLoaded = false;
                _referenceCandleLoaded = false;
            }
        }
        void UpdateFuturePrice(decimal lastPrice, uint token)
        {
            if (_activeFuture != null && _activeFuture.InstrumentToken == token)
            {
                _activeFuture.LastPrice = lastPrice;
            }
            else if (_referenceFuture != null && _referenceFuture.InstrumentToken == token)
            {
                _referenceFuture.LastPrice = lastPrice;
            }
        }

        void SetTargetHitFlag(uint token, decimal lastPrice)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];
                    if (orderTrio.Option.InstrumentToken == token)
                    {
                        if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (_baseInstrumentPrice > orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                        }
                        else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (_baseInstrumentPrice < orderTrio.TargetProfit))
                        {
                            orderTrio.TPFlag = true;
                        }
                    }
                }
            }
        }


        private void LoadCriticalLevels(uint token, uint rToken, DateTime currentTime)
        {
            DataLogic dl = new DataLogic();
            DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
            DateTime previousWeeklyExpiry = dl.GetPreviousWeeklyExpiry(currentTime, 2);
            List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
            List<Historical> rpdOHLCList = ZObjects.kite.GetHistoricalData(rToken.ToString(), previousTradingDate, currentTime.Date, "hour");
            List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");
            List<Historical> rpdOHLCDay = ZObjects.kite.GetHistoricalData(rToken.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

            //DateTime fromDate = currentTime.AddMonths(-1);
            //DateTime toDate = currentTime.AddMonths(-1).Date;
            //List<Historical> pdOHLCMonth = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

            //_criticalLevels.TryAdd(pdOHLCMonth.Max(x=>x.High), 50);
            //_criticalLevels.TryAdd(pdOHLCMonth.Min(x => x.Low), 51);
            //_criticalLevels.TryAdd(pdOHLCMonth.Last().Close, 52);

            //toDate = currentTime.AddDays(-(int)currentTime.DayOfWeek - 6);

            //List<Historical> pdOHLCWeek = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

            //_criticalLevels.TryAdd(pdOHLCWeek.Max(x => x.High), 40);
            //_criticalLevels.TryAdd(pdOHLCWeek.Min(x => x.Low), 41);
            //_criticalLevels.TryAdd(pdOHLCWeek.Last().Close, 42);

            List<Historical> historicalOHLC = ZObjects.kite.GetHistoricalData(token.ToString(), previousWeeklyExpiry, previousTradingDate + MARKET_CLOSE_TIME, "day");
            //List<string> dates = dl.GetActiveTradingDays(fromDate, currentTime);

            int i = 30;
            foreach (var ohlc in historicalOHLC)
            {
                _criticalLevels.TryAdd(ohlc.High, i++);
                _criticalLevels.TryAdd(ohlc.Low, i++);
                //_criticalLevels.TryAdd(ohlc.Close, i++);
            }

            #region Reference Critical Values
            List<Historical> rhistoricalOHLC = ZObjects.kite.GetHistoricalData(rToken.ToString(), previousWeeklyExpiry, previousTradingDate + MARKET_CLOSE_TIME, "day");
            //List<string> dates = dl.GetActiveTradingDays(fromDate, currentTime);

            int r = 30;
            foreach (var ohlc in rhistoricalOHLC)
            {
                _rcriticalLevels.TryAdd(ohlc.High, r++);
                _rcriticalLevels.TryAdd(ohlc.Low, r++);
                // _rcriticalLevels.TryAdd(ohlc.Close, r++);

                OHLC rpdOHLC = new OHLC() { Close = rpdOHLCDay.Last().Close, Open = rpdOHLCDay.First().Open, High = rpdOHLCDay.Max(x => x.High), Low = rpdOHLCDay.Min(x => x.Low), InstrumentToken = rToken };
                _cpr = new CentralPivotRange(rpdOHLC);
                _rpreviousDayOHLC = rpdOHLC;
                _rpreviousDayOHLC.Close = rpdOHLCList.Last().Close;

                List<Historical> rpwOHLCList = ZObjects.kite.GetHistoricalData(rToken.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
                _rpreviousWeekOHLC = new OHLC(rpwOHLCList.First(), rToken);
                _rweeklycpr = new CentralPivotRange(_rpreviousWeekOHLC);


                //load Previous Day and previous week data
                _rcriticalLevels.TryAdd(_rpreviousDayOHLC.Close, 0);
                _rcriticalLevels.TryAdd(_rpreviousDayOHLC.High, 1);
                _rcriticalLevels.TryAdd(_rpreviousDayOHLC.Low, 2);
                // _criticalLevels.TryAdd(_previousDayBodyHigh, 0);
                //_criticalLevels.TryAdd(_previousDayBodyLow, 0);
                _rcriticalLevels.Remove(0);

                //Clustering of _critical levels
                _rcriticalLevelsWeekly.TryAdd(_rpreviousWeekOHLC.Close, 0);
                _rcriticalLevelsWeekly.TryAdd(_rpreviousWeekOHLC.High, 1);
                _rcriticalLevelsWeekly.TryAdd(_rpreviousWeekOHLC.Low, 2);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.CPR], 3);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.R1], 6);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.R2], 7);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.R3], 8);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.S3], 9);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.S2], 10);
                _rcriticalLevelsWeekly.TryAdd(_rweeklycpr.Prices[(int)PivotLevel.S1], 11);
                _rcriticalLevelsWeekly.TryAdd(_rpreviousDayOHLC.High, 12);
                _rcriticalLevelsWeekly.TryAdd(_rpreviousDayOHLC.Low, 13);
                _rcriticalLevelsWeekly.Remove(0);
            }
            #endregion


            //OHLC pdOHLC = new OHLC() { Close = pdOHLCList.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };

            OHLC pdOHLC = new OHLC() { Close = pdOHLCDay.Last().Close, Open = pdOHLCDay.First().Open, High = pdOHLCDay.Max(x => x.High), Low = pdOHLCDay.Min(x => x.Low), InstrumentToken = token };
            _cpr = new CentralPivotRange(pdOHLC);
            _previousDayOHLC = pdOHLC;
            _previousDayOHLC.Close = pdOHLCList.Last().Close;

            List<Historical> pwOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), currentTime.Date.AddDays(-10), previousTradingDate, "week");
            _previousWeekOHLC = new OHLC(pwOHLCList.First(), token);
            _weeklycpr = new CentralPivotRange(_previousWeekOHLC);

            //load Previous Day and previous week data
            //_criticalLevels = new SortedList<decimal, int>();
            _criticalLevels.TryAdd(_previousDayOHLC.Close, 0);
            _criticalLevels.TryAdd(_previousDayOHLC.High, 1);
            _criticalLevels.TryAdd(_previousDayOHLC.Low, 2);
            // _criticalLevels.TryAdd(_previousDayBodyHigh, 0);
            //_criticalLevels.TryAdd(_previousDayBodyLow, 0);



            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.CPR], 7);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.UCPR], 8);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.LCPR], 9);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 10);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 11);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 12);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 13);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 14);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 15);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R4], 16);
            //_criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S4], 17);
            _criticalLevels.Remove(0);


            //Clustering of _critical levels
            decimal _thresholdDistance = 5;




            _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.Close, 0);
            _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.High, 1);
            _criticalLevelsWeekly.TryAdd(_previousWeekOHLC.Low, 2);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.CPR], 3);
            //_criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.UCPR], 4);
            //_criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.LCPR], 5);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R1], 6);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R2], 7);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.R3], 8);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S3], 9);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S2], 10);
            _criticalLevelsWeekly.TryAdd(_weeklycpr.Prices[(int)PivotLevel.S1], 11);
            _criticalLevelsWeekly.TryAdd(_previousDayOHLC.High, 12);
            _criticalLevelsWeekly.TryAdd(_previousDayOHLC.Low, 13);
            _criticalLevelsWeekly.Remove(0);
        }

        private void LoadHistoricalADX(DateTime currentTime)
        {
            try
            {
                DateTime lastCandleEndTime;
                DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);

                var tokens = SubscriptionTokens.Where(x => /*x != _baseInstrumentToken &&*/ !_ADXLoaded.Contains(x));

                StringBuilder sb = new StringBuilder();
                List<uint> newTokens = new List<uint>();
                foreach (uint t in tokens)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(t))
                    {
                        _firstCandleOpenPriceNeeded.Add(t, candleStartTime != lastCandleEndTime);
                    }
                    if (!_stockTokenADX.ContainsKey(t) && !_SQLLoadingAdx.Contains(t))
                    {
                        newTokens.Add(t);
                        sb.AppendFormat("{0},", t);
                        _SQLLoadingAdx.Add(t);
                    }
                }
                string tokenList = sb.ToString().TrimEnd(',');

                int firstCandleFormed = 0; //historicalPricesLoaded = 0;
                                           //if (!tokenRSI.ContainsKey(token) && !_SQLLoading.Contains(token))
                                           //{
                                           //_SQLLoading.Add(token);
                //if (tokenList != string.Empty)
                if (newTokens.Count > 0)
                {
                    //Task task = Task.Run(() => LoadHistoricalCandlesForADX(tokenList, 28, lastCandleEndTime));
                    LoadHistoricalCandlesForADX(newTokens, 28, lastCandleEndTime);
                }

                //LoadHistoricalCandles(token, LONG_EMA, lastCandleEndTime);
                //historicalPricesLoaded = 1;
                //}
                foreach (uint tkn in tokens)
                {
                    //if (tk != string.Empty)
                    //{
                    // uint tkn = Convert.ToUInt32(tk);


                    if (TimeCandles.ContainsKey(tkn) && _stockTokenADX.ContainsKey(tkn))
                    {
                        if (_firstCandleOpenPriceNeeded[tkn])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _stockTokenADX[tkn].Process(TimeCandles[tkn].First());
                            _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            firstCandleFormed = 1;

                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[tkn].Count > 1)
                        {
                            foreach (var price in TimeCandles[tkn])
                            {
                                _stockTokenADX[tkn].Process(TimeCandles[tkn].First());
                                _stockTokenHalfTrend[tkn].Process(TimeCandles[tkn].First());
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[tkn]) && _stockTokenADX.ContainsKey(tkn))
                    {
                        _ADXLoaded.Add(tkn);
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, String.Format("ADX loaded from DB for {0}", tkn), "MonitorCandles");
                    }
                    //}
                    // }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "LoadHistoricalRSIs");
                Thread.Sleep(100);
                //Environment.Exit(0);
            }
        }

        private void LoadHistoricalCandlesForADX(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime)
        {
            LoadHistoricalCandlesForHT(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(0, 5, 0));
            LoadHistoricalCandlesForADX(tokenList, candlesCount, lastCandleEndTime, new TimeSpan(0, 15, 0));
        }
        private void LoadHistoricalCandlesForADX(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            AverageDirectionalIndex adx;
            //CandleSeries cs = new CandleSeries();
            //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            lock (_stockTokenADX)
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 3);

                foreach (uint token in tokenList)
                {
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpanLong.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);
                    foreach (var price in historicals)
                    {
                        dl.InsertHistoricals(price);
                    }
                    foreach (var price in historicals)
                    {
                        TimeFrameCandle tc = new TimeFrameCandle();
                        tc.TimeFrame = timeSpan;
                        tc.ClosePrice = price.Close;
                        tc.OpenPrice = price.Open;
                        tc.HighPrice = price.High;
                        tc.LowPrice = price.Low;
                        tc.TotalVolume = price.Volume;
                        tc.InstrumentToken = token;
                        tc.Final = true;
                        tc.State = CandleStates.Finished;
                        if (_stockTokenADX.ContainsKey(token))
                        {
                            _stockTokenADX[tc.InstrumentToken].Process(tc);
                        }
                        else
                        {
                            adx = new AverageDirectionalIndex(13);
                            adx.Process(tc);
                            _stockTokenADX.TryAdd(tc.InstrumentToken, adx);
                        }
                    }
                }
            }
        }
        private void LoadHistoricalCandlesForHT(List<uint> tokenList, int candlesCount, DateTime lastCandleEndTime, TimeSpan timeSpan)
        {
            //CandleSeries cs = new CandleSeries();
            //List<Candle> historicalCandles = cs.LoadCandles(candlesCount, CandleType.Time, lastCandleEndTime, tokenList, timeSpan);

            HalfTrend ht;
            lock (_stockTokenHalfTrend)
            {
                DataLogic dl = new DataLogic();
                DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime, 1);

                foreach (uint token in tokenList)
                {
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpanShort.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicals)
                    {
                        TimeFrameCandle tc = new TimeFrameCandle();
                        tc.TimeFrame = timeSpan;
                        tc.ClosePrice = price.Close;
                        tc.OpenPrice = price.Open;
                        tc.HighPrice = price.High;
                        tc.LowPrice = price.Low;
                        tc.TotalVolume = price.Volume;
                        tc.OpenTime = price.TimeStamp;
                        tc.InstrumentToken = token;
                        tc.Final = true;
                        tc.State = CandleStates.Finished;
                        if (_stockTokenHalfTrend.ContainsKey(tc.InstrumentToken))
                        {
                            _stockTokenHalfTrend[tc.InstrumentToken].Process(tc);
                        }
                        else
                        {
                            ht = new HalfTrend(2, 2);
                            ht.Process(tc);
                            _stockTokenHalfTrend.TryAdd(tc.InstrumentToken, ht);
                        }
                    }
                }
            }
        }

        private void LastSwingHigh(DateTime currentTime, decimal token, decimal lastPrice, bool high, out decimal swingHigh, out decimal swingLow)
        {
            //DataLogic dl = new DataLogic();
            //DateTime previousTradingDate = dl.GetPreviousTradingDate(currentTime);
            //DateTime previousWeeklyExpiry = dl.GetPreviousWeeklyExpiry(currentTime, 2);
            //List<Historical> pdOHLCList = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, currentTime.Date, "hour");
            ////List<Historical> rpdOHLCList = ZObjects.kite.GetHistoricalData(_referenceBToken.ToString(), previousTradingDate, currentTime.Date, "hour");
            //List<Historical> pdOHLCDay = ZObjects.kite.GetHistoricalData(token.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");
            ////List<Historical> rpdOHLCDay = ZObjects.kite.GetHistoricalData(_referenceBToken.ToString(), previousTradingDate, previousTradingDate + MARKET_CLOSE_TIME, "day");

            DateTime fromDate = currentTime.AddMonths(-1);
            DateTime toDate = currentTime.AddDays(-1).Date;
            List<Historical> pdOHLCMonth = ZObjects.kite.GetHistoricalData(token.ToString(), fromDate, toDate, "day");

            swingHigh = 0;
            swingLow = 1000000;

            if (high)
            {
                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.High < swingHigh)
                    {
                        break;
                    }
                    //else if (ohlc.Low > lastPrice)
                    //{
                    //    swingHigh = ohlc.Low;
                    //    continue;
                    //}
                    else if (ohlc.High > lastPrice)
                    {
                        swingHigh = ohlc.High;
                        continue;
                    }
                }


                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.Low > swingLow)
                    {
                        break;
                    }
                    //else if (ohlc.High < lastPrice)
                    //{
                    //    swingLow = ohlc.High;
                    //    continue;
                    //}
                    else if (ohlc.Low > lastPrice)
                    {
                        swingLow = ohlc.Low;
                        continue;
                    }
                }
            }
            else
            {
                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.High < swingHigh)
                    {
                        break;
                    }
                    //else if (ohlc.Low > lastPrice)
                    //{
                    //    swingHigh = ohlc.Low;
                    //    continue;
                    //}
                    else if (ohlc.High < lastPrice)
                    {
                        swingHigh = ohlc.High;
                        continue;
                    }
                }


                foreach (var ohlc in pdOHLCMonth.OrderByDescending(x => x.TimeStamp))
                {
                    if (ohlc.Low > swingLow)
                    {
                        break;
                    }
                    //else if (ohlc.High < lastPrice)
                    //{
                    //    swingLow = ohlc.High;
                    //    continue;
                    //}
                    else if (ohlc.Low < lastPrice)
                    {
                        swingLow = ohlc.Low;
                        continue;
                    }
                }
            }
            return;
        }
        private void CheckTPSL(DateTime currentTime, decimal lastPrice, bool closeAll = false)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (orderTrio.TPFlag))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());
                        _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (orderTrio.TPFlag))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(order);

                        _pnl += (orderTrio.Order.AveragePrice - order.AveragePrice) * _tradeQty * _activeFuture.LotSize;


#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }

        private void CheckSL(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        {
            if (_orderTrios.Count > 0)
            {
                for (int i = 0; i < _orderTrios.Count; i++)
                {
                    var orderTrio = _orderTrios[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && ((lastPrice < orderTrio.StopLoss || closeAll) || (pCandle != null && (lastPrice < pCandle.LowPrice) && orderTrio.TPFlag)))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        _pnl += (order.AveragePrice - orderTrio.Order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());


                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && ((lastPrice > orderTrio.StopLoss || closeAll) || (pCandle != null && (lastPrice > pCandle.HighPrice) && orderTrio.TPFlag)))
                    {
                        Order order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo1", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _pnl += (orderTrio.Order.AveragePrice - order.AveragePrice) * _tradeQty * _activeFuture.LotSize;

                        _orderTrios.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }
        private void CheckSLForEMABasedOrders(DateTime currentTime, decimal lastPrice, Candle pCandle, bool closeAll = false)
        {
            if (_orderTriosFromEMATrades.Count > 0)
            {
                for (int i = 0; i < _orderTriosFromEMATrades.Count; i++)
                {
                    var orderTrio = _orderTriosFromEMATrades[i];

                    if ((orderTrio.Order.TransactionType.ToLower() == "buy") && (lastPrice < _indexEMAValue.GetValue<decimal>() || closeAll))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                            _activeFuture.KToken, false, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
                            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Sold @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif

                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());


                        _orderTriosFromEMATrades.Remove(orderTrio);
                        i--;
                    }
                    else if (orderTrio.Order.TransactionType.ToLower() == "sell" && (lastPrice > _indexEMAValue.GetValue<decimal>() || closeAll))
                    {
                        orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                       _activeFuture.KToken, true, _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, currentTime, Tag: "Algo2", product: Constants.KPRODUCT_NRML,
                       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        OnTradeEntry(orderTrio.Order);

#if !BACKTEST
                        OnCriticalEvents(currentTime.ToShortTimeString(), String.Format("Bought @ {0}", Math.Round(orderTrio.Order.AveragePrice, 2)));
#endif
                        ////Cancel Target profit Order
                        //MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, orderTrio.TPOrder, currentTime,
                        //httpClient: _httpClientFactory.CreateClient());

                        _orderTriosFromEMATrades.Remove(orderTrio);
                        i--;
                    }
                }
            }
        }

        private bool ValidateRiskReward(Candle c, out decimal targetLevel, out decimal stoploss, out bool s2r2level)
        {
            targetLevel = 0;
            stoploss = 0;
            s2r2level = false;
            bool _favourableRiskReward = false;
            if (c.ClosePrice > _cpr.Prices[(int)PivotLevel.LCPR] && c.ClosePrice < _cpr.Prices[(int)PivotLevel.UCPR])
            {
                // Do not take trade within CPR
            }
            else //if (ValidateCandleSize(c))
            {
                //if (c.ClosePrice > _criticalLevels.Keys.Max() || c.ClosePrice < _criticalLevels.Keys.Min())
                if (c.ClosePrice > _previousDayOHLC.High || c.ClosePrice < _previousDayOHLC.Low)
                {
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R1], 17);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R2], 18);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R3], 19);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S3], 20);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S2], 21);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S1], 22);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.R4], 23);
                    _criticalLevels.TryAdd(_cpr.Prices[(int)PivotLevel.S4], 24);
                }
                else
                {
                }

                //bullish scenario
                if (c.ClosePrice > c.OpenPrice)
                {
                    decimal plevel = 0;
                    foreach (var level in _criticalLevels)
                    {
                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0 && level.Key - plevel > 10)
                        {
                            if ((level.Key - c.ClosePrice) / (c.ClosePrice - c.LowPrice /*plevel*/) > 2.0M)
                            {
                                targetLevel = level.Key;
                                stoploss = c.LowPrice;
                                _favourableRiskReward = true;
                                if (plevel == _cpr.Prices[(int)PivotLevel.S2]
                                    //|| plevel == _cpr.Prices[(int)PivotLevel.R2]
                                    || plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.R2]
                                    )
                                {
                                    s2r2level = true;
                                }
                            }
                            break;
                        }
                        plevel = level.Key;
                    }
                }
                else
                {
                    decimal plevel = 0;
                    foreach (var level in _criticalLevels)
                    {
                        if (c.ClosePrice > plevel & c.ClosePrice < level.Key & plevel != 0 && level.Key - plevel > 10)
                        {
                            if (Math.Abs((c.ClosePrice - c.HighPrice /*plevel*/) / (level.Key - c.ClosePrice)) > 2.0M)
                            {
                                targetLevel = plevel;
                                stoploss = c.HighPrice;
                                _favourableRiskReward = true;

                                if (//plevel == _cpr.Prices[(int)PivotLevel.S2] ||
                                    level.Key == _cpr.Prices[(int)PivotLevel.R2]
                                    //|| plevel == _weeklycpr.Prices[(int)PivotLevel.S2]
                                    || level.Key == _weeklycpr.Prices[(int)PivotLevel.R2])
                                {
                                    s2r2level = true;
                                }
                                break;
                            }
                        }
                        plevel = level.Key;
                    }
                }
            }
            return _favourableRiskReward;
        }
       

        private Candle GetLastCandle(uint instrumentToken, int length)
        {
            if (TimeCandles[instrumentToken].Count < length)
            {
                return null;
            }
            var lastCandles = TimeCandles[instrumentToken].TakeLast(length);
            TimeFrameCandle tC = new TimeFrameCandle();
            tC.OpenPrice = lastCandles.ElementAt(0).OpenPrice;
            tC.OpenTime = lastCandles.ElementAt(0).OpenTime;
            tC.ClosePrice = lastCandles.ElementAt(length - 1).ClosePrice;
            tC.CloseTime = lastCandles.ElementAt(length - 1).CloseTime;
            tC.HighPrice = Math.Max(Math.Max(lastCandles.ElementAt(0).HighPrice, lastCandles.ElementAt(length - 2).HighPrice), lastCandles.ElementAt(length - 1).HighPrice);
            tC.LowPrice = Math.Min(Math.Min(lastCandles.ElementAt(0).LowPrice, lastCandles.ElementAt(length - 2).LowPrice), lastCandles.ElementAt(length - 1).LowPrice);
            tC.State = CandleStates.Finished;
            tC.Final = true;

            return tC;
        }
       
        private void TriggerEODPositionClose(DateTime currentTime, decimal lastPrice)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00) && _orderTrios.Count > 0)
            {
                OrderTrio orderTrio = new OrderTrio();
                orderTrio.Order = MarketOrders.PlaceOrder(_algoInstance, _activeFuture.TradingSymbol, "fut", _activeFuture.LastPrice,
                   _activeFuture.KToken, _orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell", _tradeQty * Convert.ToInt32(_activeFuture.LotSize), algoIndex, _bCandle.CloseTime,
                   Tag: String.Format("ADX {0}, DPlux {1}, DMinus {2} , HtUP {3}", 0, 0, 0, 0),
                   product: Constants.PRODUCT_MIS, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                OnTradeEntry(orderTrio.Order);
                orderTrio.Option = _activeFuture;
                _orderTrios.Add(orderTrio);

                _pnl += orderTrio.Order.Quantity * orderTrio.Order.AveragePrice * (_orderTrios[0].Order.TransactionType.ToLower().Trim() == "sell" ? -1 : 1);

                DataLogic dl = new DataLogic();
                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                _pnl = 0;
                _stopTrade = true;
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
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("Starting first Candle now for token: {0}", tick.InstrumentToken), "MonitorCandles");
#endif
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


        private void LoadBInstrumentEMA(uint bToken, int candleCount, DateTime currentTime)
        {
            DateTime lastCandleEndTime;
            DateTime? candleStartTime = CheckCandleStartTime(currentTime, out lastCandleEndTime);
            try
            {
                lock (_indexEMA)
                {
                    if (!_firstCandleOpenPriceNeeded.ContainsKey(bToken))
                    {
                        _firstCandleOpenPriceNeeded.Add(bToken, candleStartTime != lastCandleEndTime);
                    }
                    int firstCandleFormed = 0;
                    if (!_indexEMALoading)
                    {
                        _indexEMALoading = true;
                        LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime);
                        //Task task = Task.Run(() => LoadBaseInstrumentEMA(bToken, candleCount, lastCandleEndTime));
                    }


                    if (TimeCandles.ContainsKey(bToken) && _indexEMALoadedFromDB)
                    {
                        if (_firstCandleOpenPriceNeeded[bToken])
                        {
                            //The below EMA token input is from the candle that just started, All historical prices are already fed in.
                            _indexEMA.Process(TimeCandles[bToken].First().OpenPrice, isFinal: true);

                            firstCandleFormed = 1;
                        }
                        //In case SQL loading took longer then candle time frame, this will be used to catch up
                        if (TimeCandles[bToken].Count > 1)
                        {
                            foreach (var price in TimeCandles[bToken])
                            {
                                _indexEMA.Process(TimeCandles[bToken].First().ClosePrice, isFinal: true);
                            }
                        }
                    }

                    if ((firstCandleFormed == 1 || !_firstCandleOpenPriceNeeded[bToken]) && _indexEMALoadedFromDB)
                    {
                        _indexEMALoaded = true;
#if !BACKTEST
                        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime,
                            String.Format("{0} EMA loaded from DB for Base Instrument", 20), "LoadBInstrumentEMA");
#endif
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
        private void LoadBaseInstrumentEMA(uint bToken, int candlesCount, DateTime lastCandleEndTime)
        {
            try
            {
                lock (_indexEMA)
                {
                    DataLogic dl = new DataLogic();
                    DateTime previousTradingDate = dl.GetPreviousTradingDate(lastCandleEndTime);
                    List<Historical> historicals = ZObjects.kite.GetHistoricalData(bToken.ToString(), previousTradingDate, lastCandleEndTime.AddSeconds(-10), string.Format("{0}minute", _candleTimeSpan.Minutes));
                    //Dictionary<uint, List<decimal>> historicalCandlePrices = dl.GetHistoricalCandlePrices(candlesCount, lastCandleEndTime, bToken.ToString(), _candleTimeSpan, false);

                    foreach (var price in historicals)
                    {
                        _indexEMA.Process(price.Close, isFinal: true);
                    }
                    _indexEMALoadedFromDB = true;
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

        private void LoadFutureToTrade(DateTime currentTime)
        {
            try
            {
                if (_activeFuture == null)
                {
#if BACKTEST
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(null, _baseInstrumentToken, 0, "EQ");
                    
                    _activeFuture.LotSize = (uint) (_activeFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50: 25);
                    
                    _referenceFuture = dl.GetInstrument(null, _referenceBToken, 0, "EQ");
                    _referenceFuture.LotSize = (uint)(_referenceFuture.InstrumentToken == Convert.ToInt32(Constants.NIFTY_TOKEN) ? 50 : 25);

#else
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    _activeFuture = dl.GetInstrument(_expiryDate.Value, _baseInstrumentToken, 0, "Fut");
                    _referenceFuture = dl.GetInstrument(_expiryDate.Value, _referenceBToken, 0, "Fut");

                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, " Tokens Loaded", "LoadFutureToTrade");
                    OnCriticalEvents(currentTime.ToShortTimeString(), "Trade Started. Future Loaded.");
#endif
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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                if (_activeFuture != null && !SubscriptionTokens.Contains(_activeFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_activeFuture.InstrumentToken);
                    dataUpdated = true;
                }
                if (_referenceFuture != null && !SubscriptionTokens.Contains(_referenceFuture.InstrumentToken))
                {
                    SubscriptionTokens.Add(_referenceFuture.InstrumentToken);
                    dataUpdated = true;
                }
#if !BACKTEST
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to Future", "UpdateInstrumentSubscription");
#endif
                if (dataUpdated)
                {
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
                Thread.Sleep(100);
            }
        }

        public int AlgoInstance
        {
            get { return _algoInstance; }
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

        public virtual void OnNext(Tick tick)
        {
            try
            {
                if (_stopTrade || !tick.Timestamp.HasValue)
                {
                    return;

                }

#if local
                if (tick.Timestamp.HasValue && _currentDate.HasValue && tick.Timestamp.Value.Date != _currentDate)
                {
                    ResetAlgo(tick.Timestamp.Value.Date);
                }
#endif
                ActiveTradeIntraday(tick);
                //return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                //   return;
            }
        }
        private void ResetAlgo(DateTime tradeDate)
        {
            _currentDate = tradeDate;
            _stopTrade = false;
            DataLogic dl = new DataLogic();
            DateTime? nextExpiry = dl.GetCurrentMonthlyExpiry(_currentDate.Value);
            SetUpInitialData(nextExpiry);
        }
        private void CheckHealth(object sender, ElapsedEventArgs e)
        {
#if !BACKTEST
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
#endif
        }

        //private void PublishLog(object sender, ElapsedEventArgs e)
        //{
        //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
        //    String.Format("Current ADX: {0}", Decimal.Round(0), "Log_Timer_Elapsed"));
        //    Thread.Sleep(100);
        //}

        public virtual void Subscribe(IObservable<Tick> provider)
        {
            unsubscriber = provider.Subscribe(this);
        }

        public virtual void Unsubscribe()
        {
            unsubscriber.Dispose();
        }

        public virtual void OnCompleted()
        {
            Console.WriteLine("Additional Ticks data will not be transmitted.");
        }

        public virtual void OnError(Exception error)
        {
            // Do nothing.
        }
    }
}
