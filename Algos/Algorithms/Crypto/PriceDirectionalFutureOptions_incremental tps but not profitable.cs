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
//using KafkaFacade;
using System.Timers;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using System.Net.Http.Headers;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
using Algos.Utilities.Views;
//using Twilio.Jwt.AccessToken;
using static Algorithms.Indicators.DirectionalIndex;
using Google.Protobuf.WellKnownTypes;
using Google.Apis.Upload;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;
using static Google.Protobuf.Compiler.CodeGeneratorResponse.Types;
using Algos.Utilities;
using Grpc.Core;

namespace Algorithms.Algorithms
{
    public class PriceDirectionalFutureOptions : ICZMQ
    {
        private int _algoInstance;
        
        private IDisposable unsubscriber;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(PriceDirectionalFutureOptions source);
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

        private User _user;
        private int _futureQty;
        private bool _futureIsSet = false;
        private bool _stopTrade = true;

        private int _longFutureCounter = 0;
        private int _shortFutureCounter = 0;

        private DateTime _nextExpiry;


        private readonly TimeSpan ENTRY_WINDOW_START = new TimeSpan(02, 0, 0);
        private readonly TimeSpan ENTRY_WINDOW_END = new TimeSpan(17, 00, 0);
        
        //11 AM to 1Pm suspension
        private readonly int STOP_LOSS_POINTS = 1130;//50;//30; //40 points max loss on an average 
        private readonly int STOP_LOSS_POINTS_GAMMA = 1130;//50;//30;
        private const int DAY_STOP_LOSS_PERCENT = 1; //40 points max loss on an average 

        private decimal _nextTriggerPoint;
        private decimal _ceSL = 0;
        private decimal _peSL = 0;

        uint _lotSize;
        private enum MarketScenario
        {
            Bullish = 1,
            Neutral = 0,
            Bearish = -1
        }
        public const AlgoIndex algoIndex = AlgoIndex.Crypto_PriceDirectionWithOption;
        private decimal _targetProfit;
        private decimal _stopLoss;
        CandleManger candleManger;
        Dictionary<uint, List<Candle>> TimeCandles;
        private IHttpClientFactory _httpClientFactory;
        public List<uint> SubscriptionTokens { get; set; }
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();

        List<CryptoPosition> _cryptoPositions;

        //private CryptoPosition _futurePosition;
        private TradedCryptoFuture _future;
        private TradedCryptoOption _hedgeOption;
        private TradedCryptoOption _targetOption;
        
        private decimal _pnl = 0;
        private decimal _entryPremium = 0;
        private DateTime _expiryDate;
        private DateTime _entryDate;
        private bool _targetOptionAvailable = false;
        private bool _hedgeOptionAvailable = false;

        //Incase of double side, there will be no future,and hedge, but two target options
        private TradedCryptoOption _targetOption2;
        private bool _targetOption2Available = false;
        private TradedCryptoOption _hedgeOption2;
        private bool _hedgeOption2Available = false;
        private TradedCryptoFuture _future2;


        private bool _doubleSide = false;
        private bool _futureLong = true;
        private decimal _futurePrice;

        private readonly int MINIMUM_PROFIT_HEDGE_SIDE;
        private const int TARGET_MINIMUM_DISTANCE_FROM_FUTURE = 2000;
        private const int TARGET_OPTION_MAX_PREMIUM = 210;
        private const decimal TARGET_OPTION_FUTURE_MULTIPLIER = 15;
        private readonly int FUTURE_STEP_QUANTITY;

        //strike, premium
        //Dictionary<decimal, decimal> _callOptions = new Dictionary<decimal, decimal>();
        //Dictionary<decimal, decimal> _putOptions = new Dictionary<decimal, decimal>();

        Dictionary<decimal, TradedCryptoOption> _callOptions = new Dictionary<decimal, TradedCryptoOption>();
        Dictionary<decimal, TradedCryptoOption> _putOptions = new Dictionary<decimal, TradedCryptoOption>();


        public PriceDirectionalFutureOptions(
            DateTime expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            decimal pnl, bool futureLong, bool doubleSide, int algoInstance = 0,  IHttpClientFactory httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory;

#if !BACKTEST
            DEConnect.Login();
            _user = DEConnect.GetUser(userId: uid);
#endif
            _cryptoPositions = new List<CryptoPosition>();
            _targetProfit = targetProfit;
            _stopLoss = stopLoss;
            _futureQty = quantity;
            FUTURE_STEP_QUANTITY = Convert.ToInt32(_futureQty * (TARGET_OPTION_FUTURE_MULTIPLIER - 1) / 5);
            MINIMUM_PROFIT_HEDGE_SIDE = 8 * quantity;
            SetUpInitialData(expiry, algoInstance);
            _pnl = pnl;
            _futureLong = futureLong;
            _doubleSide = doubleSide;
            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();
        }
        private void SetUpInitialData(DateTime expiry, int algoInstance = 0)
        {
            _expiryDate = expiry;
            _nextExpiry = expiry;//DateTime.Today.AddDays(1);
            //_futurePosition = new CryptoPosition();
            _future = new TradedCryptoFuture();
            _future2 = new TradedCryptoFuture();
            _hedgeOption = new TradedCryptoOption();
            _targetOption = new TradedCryptoOption();
            _targetOption2 = new TradedCryptoOption();
            _hedgeOption2 = new TradedCryptoOption();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();

            //Arg 3 : _futureLong, arg4: doubleside
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, 27, DateTime.Now,
                expiry, _futureQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                0, CandleType.Time, 0, _targetProfit, _stopLoss, _futureLong ? 1 : 0, _doubleSide ? 1 : 0,
                0, 0, Arg9: _user == null ? "" : _user.UserId, positionSizing: false, maxLossPerTrade: 0);

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

        }

        //public void LoadActiveOrders(List<OrderTrio> activeOrderTrios)
        //{
        //    if (activeOrderTrios != null)
        //    {
        //        DataLogic dl = new DataLogic();
        //        foreach (OrderTrio orderTrio in activeOrderTrios)
        //        {
        //            Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
        //            orderTrio.Option = option;
        //            if (option.InstrumentType.ToLower() == "ce")
        //            {
        //                _callorderTrios.Add(orderTrio);
        //            }
        //            else if (option.InstrumentType.ToLower() == "pe")
        //            {
        //                _putorderTrios.Add(orderTrio);
        //            }
        //        }
        //    }
        //}


        private void ActiveTradeIntraday(string channel, string data)
        {
            L1Orderbook orderbook = null;

            try
            {
                orderbook = L1Orderbook.FromJson(data);

                //if (channel == "l1_orderbook")
                //{
                //    orderbook = L1Orderbook.FromJson(data);
                //}
                //else if (channel == "mark_price")
                //{
                //    markPrice = MarkPrice.FromJson(data);
                //}

                //Check the price of future and call and put
                //take call sell position with 0.5 qty, take optional call buy position but quite fall
                //take future buy position with 0.1qty and take hedge call buy position
                //keep checking pnl, if pnl has read threshold, close all positions


                //When doubleside is on..call is target, and put is target2, else it is based on futurelong


                UpdateOptionChain(orderbook);


                if (_targetOption.Size == 0)
                {
                    if (_doubleSide)
                    {
                        //call target
                        _targetOptionAvailable = _targetOptionAvailable || CheckTargetOptionAvailability(true, _futurePrice);
                        //put target
                        _targetOption2Available = _targetOption2Available || CheckTargetOptionAvailability(false, _futurePrice);

                        //no need for hedge at the begining, untill future is traded.
                    }
                    else
                    {
                        _targetOptionAvailable = _targetOptionAvailable || CheckTargetOptionAvailability(_futureLong, _futurePrice);
                        _hedgeOptionAvailable = _hedgeOptionAvailable || CheckHedgeOptionAvailability(_futureLong, _futurePrice);
                    }
                }

                if (_targetOptionAvailable
                    && _doubleSide ? (_targetOption2Available && _targetOption2.Size == 0) : (_hedgeOptionAvailable && _hedgeOption.Size == 0)
                    //&& DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp/1000).LocalDateTime.TimeOfDay > ENTRY_WINDOW_START
                    // && DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime.TimeOfDay < ENTRY_WINDOW_END
                    )
                {
                    #region Set Values
                    //_future.Symbol = "BTCUSD";
                    //_future.CurrentPrice = 84998;
                    //_future.EntryPrice = 84198m;
                    //_future.Size = 43;
                    //_future.Side = "buy";
                    //_future.PaidCommission = 0.97m;

                    //_targetOption.Symbol = "C-BTC-86400-250325";
                    //_targetOption.CurrentPremium = 167;
                    //_targetOption.EntryPremium = 210m;
                    //_targetOption.Size = 250;
                    //_targetOption.Side = "sell";
                    //_targetOption.Strike = 86400;
                    //_targetOption.Expiry = _nextExpiry;
                    //_targetOption.PaidCommission = 6.195m;

                    //_hedgeOption.Symbol = "P-BTC-82400-250325";
                    //_hedgeOption.CurrentPremium = 715;
                    //_hedgeOption.EntryPremium = 365;
                    //_hedgeOption.Size = 16;
                    //_hedgeOption.Side = "buy";
                    //_hedgeOption.Strike = 82400;
                    //_hedgeOption.Expiry = _nextExpiry;
                    //_hedgeOption.PaidCommission = 0.476m;


                    //_future2.Symbol = "BTCUSD";
                    //_future2.CurrentPrice = 85000;
                    //_future2.EntryPrice = 84026m;
                    //_future2.Size = 26;
                    //_future2.Side = "sell";
                    //_future2.PaidCommission = 0.767m;

                    //_targetOption2.Symbol = "P-BTC-81600-250325";
                    //_targetOption2.CurrentPremium = 108;
                    //_targetOption2.EntryPremium = 207m;
                    //_targetOption2.Size = 250;
                    //_targetOption2.Side = "sell";
                    //_targetOption2.Strike = 81600;
                    //_targetOption2.Expiry = _nextExpiry;
                    //_targetOption2.PaidCommission = 6.1065m;

                    //_hedgeOption2.Symbol = "C-BTC-85600-250325";
                    //_hedgeOption2.CurrentPremium = 400;
                    //_hedgeOption2.EntryPremium = 382;
                    //_hedgeOption2.Size = 16;
                    //_hedgeOption2.Side = "buy";
                    //_hedgeOption2.Strike = 85600;
                    //_hedgeOption2.Expiry = _nextExpiry;
                    //_hedgeOption2.PaidCommission = 0.476m;

                    //_future.Symbol = "BTCUSD";
                    //_future.CurrentPrice = 84926;
                    //_future.EntryPrice = 84326m;
                    //_future.Size = 97;
                    //_future.Side = "buy";
                    //_future.PaidCommission = 4.674m;

                    //_targetOption.Symbol = "C-BTC-86400-250325";
                    //_targetOption.CurrentPremium = 181;
                    //_targetOption.EntryPremium = 181m;
                    //_targetOption.Size = 500;
                    //_targetOption.Side = "sell";
                    //_targetOption.Strike = 86400;
                    //_targetOption.Expiry = _nextExpiry;
                    //_targetOption.PaidCommission = 10.679m;

                    //_hedgeOption.Symbol = "P-BTC-82400-250325";
                    //_hedgeOption.CurrentPremium = 715;
                    //_hedgeOption.EntryPremium = 368;
                    //_hedgeOption.Size = 25;
                    //_hedgeOption.Side = "buy";
                    //_hedgeOption.Strike = 82400;
                    //_hedgeOption.Expiry = _nextExpiry;
                    //_hedgeOption.PaidCommission = 0.743m;


                    //_future2.Symbol = "BTCUSD";
                    //_future2.CurrentPrice = 85000;
                    //_future2.EntryPrice = 83955;
                    //_future2.Size = 52;
                    //_future2.Side = "sell";
                    //_future2.PaidCommission = 1.187m;

                    //_targetOption2.Symbol = "P-BTC-81600-250325";
                    //_targetOption2.CurrentPremium = 110;
                    //_targetOption2.EntryPremium = 209m;
                    //_targetOption2.Size = 500;
                    //_targetOption2.Side = "sell";
                    //_targetOption2.Strike = 81600;
                    //_targetOption2.Expiry = _nextExpiry;
                    //_targetOption2.PaidCommission = 12.331m;

                    //_hedgeOption2.Symbol = "C-BTC-85600-250325";
                    //_hedgeOption2.CurrentPremium = 400;
                    //_hedgeOption2.EntryPremium = 337;
                    //_hedgeOption2.Size = 32;
                    //_hedgeOption2.Side = "buy";
                    //_hedgeOption2.Strike = 85600;
                    //_hedgeOption2.Expiry = _nextExpiry;
                    //_hedgeOption2.PaidCommission = 0.951m;


                    //_entryDate = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime.Date;

                    #endregion

                    TakeTrade(orderbook, _futureLong, _doubleSide);
                }
                else if (_targetOption != null && _targetOption.Size != 0)  //(_hedgeOption != null && _hedgeOption.Size != 0)
                {
                    UpdateOptionPremium(_doubleSide, _futureLong);

                    decimal pnl = _doubleSide ?
                        (_targetOption.EntryPremium - _targetOption.CurrentPremium) * (_targetOption.Size / 1000)
                        + (_targetOption2.EntryPremium - _targetOption2.CurrentPremium) * (_targetOption2.Size / 1000)
                        + (_hedgeOption.CurrentPremium - _hedgeOption.EntryPremium) * (_hedgeOption.Size / 1000)
                        + (_hedgeOption2.CurrentPremium - _hedgeOption2.EntryPremium) * (_hedgeOption2.Size / 1000)
                        - 2 * (_targetOption.PaidCommission + _targetOption2.PaidCommission + _hedgeOption.PaidCommission + _hedgeOption2.PaidCommission + _future.PaidCommission + _future2.PaidCommission)
                        + (_future.CurrentPrice - _future.EntryPrice) * (_future.Size / 1000) 
                        + (_future2.EntryPrice - _future2.CurrentPrice) * (_future2.Size / 1000)
                        :
                        (_future.CurrentPrice - _future.EntryPrice) * (_future.Size / 1000) * (_futureLong ? 1 : -1)
                        + (_hedgeOption.Size / 1000) * (_hedgeOption.CurrentPremium - _hedgeOption.EntryPremium)
                        + (_targetOption.EntryPremium - _targetOption.CurrentPremium) * (_targetOption.Size / 1000)
                        - 2 * (_future.PaidCommission + _targetOption.PaidCommission + _hedgeOption.PaidCommission);

                    //INR conversion
                    //_pnl = 85*_pnl;
                    pnl += _pnl;

#if BACKTEST
                    DateTime currentime1 = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
                    Console.WriteLine($"{pnl}: {currentime1}");
#endif
                    DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
                    //Change expiries after exit to next expiries
                    if (pnl > _targetProfit || pnl < _stopLoss * -1
                        ||
                        (currentTime.TimeOfDay > ENTRY_WINDOW_END.Subtract(TimeSpan.FromMinutes(10))
                        && _nextExpiry.Date == currentTime.Date) //DateTime.Now.Date)
                        )
                    {
                        DateTime currentime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
                        _pnl = ExitTrade(currentime);
                        
                        _stopTrade = true;

                        CryptoDataLogic dl = new CryptoDataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, pnl, currentime);

                        _future = new TradedCryptoFuture();
                        _future2 = new TradedCryptoFuture();
                        _hedgeOption = new TradedCryptoOption();
                        _targetOption = new TradedCryptoOption();
                        _hedgeOption2 = new TradedCryptoOption();
                        _targetOption2 = new TradedCryptoOption();

                        //_nextExpiry = _nextExpiry;
                        _pnl = 0;
                        _callOptions.Clear();
                        _putOptions.Clear();
                        _targetOptionAvailable = false;
                        _hedgeOptionAvailable = false;
                        _targetOption2Available = false;
                        _hedgeOption2Available = false;
                    }
                    else
                    {
                        bool entryTicket = false;

                        if(_doubleSide)
                        {
                            entryTicket = GetNextTriggerPoint(_targetOption, true, _future);

                            //long future case
                            if (entryTicket)
                            {
                                int additionlQty = GetAdditionalFutureQuantity(_targetOption, true, _future, _future.Size);
                                //additionlQty = additionlQty == 0 ? FUTURE_STEP_QUANTITY : additionlQty;

                                if (additionlQty > 0)
                                {
                                    _longFutureCounter++;
                                    AddFuture(orderbook, true, additionlQty, _hedgeOption, _future, _targetOption);
                                }
                                //_nextTriggerPoint = GetNextTriggerPoint(_future, _targetOption);
                            }


                            entryTicket = GetNextTriggerPoint(_targetOption2, false, _future2);

                            //long future case
                            if (entryTicket)
                            {
                                int additionlQty = GetAdditionalFutureQuantity(_targetOption2, false, _future2, _future2.Size);
                                //additionlQty = additionlQty == 0 ? FUTURE_STEP_QUANTITY : additionlQty;

                                if (additionlQty > 0)
                                {
                                    _shortFutureCounter++;
                                    AddFuture(orderbook, false, additionlQty, _hedgeOption2, _future2, _targetOption2);
                                }
                                //_nextTriggerPoint = GetNextTriggerPoint(_future, _targetOption);
                            }

                            //cut future if it reaches it average entry price. Reset it to correct size

                            if(_future.Size > _futureQty && _future.EntryPrice > _future.CurrentPrice && _longFutureCounter >= 3)
                            {
                                //reset to correct size
                                int correctSize = GetCorrectFutureQuantity(_targetOption, true, _future, _futureQty);

                                int additionlQty = GetAdditionalFutureQuantity(_targetOption, true, _future, correctSize);

                                if (additionlQty == 0 && correctSize >= _futureQty && correctSize < _future.Size)
                                {
                                    ReduceFuture(orderbook, true, Convert.ToInt32(_future.Size - correctSize), _hedgeOption, _future, _targetOption);
                                    _longFutureCounter = 0;
                                }
                            }
                            if (_future2.Size > _futureQty && _future2.EntryPrice < _future2.CurrentPrice && _shortFutureCounter >= 3)
                            {
                                //reset to correct size
                                int correctSize = GetCorrectFutureQuantity(_targetOption2, false, _future2, _futureQty);

                                int additionlQty = GetAdditionalFutureQuantity(_targetOption2, false, _future2, correctSize);
                                if (additionlQty == 0 && correctSize >= _futureQty && correctSize < _future2.Size)
                                {
                                    ReduceFuture(orderbook, false, Convert.ToInt32(_future2.Size - correctSize), _hedgeOption, _future2, _targetOption);
                                    _shortFutureCounter = 0;
                                }
                            }


                        }
                        else
                        {
                            entryTicket = GetNextTriggerPoint(_targetOption, _futureLong, _future);

                            //long future case
                            if (entryTicket)
                            {
                                int additionlQty = GetAdditionalFutureQuantity(_targetOption, _futureLong, _future, _future.Size);
                                additionlQty = additionlQty == 0 ? FUTURE_STEP_QUANTITY : additionlQty;


                                AddFuture(orderbook, _futureLong, additionlQty, _hedgeOption, _future, _targetOption);
                                //_nextTriggerPoint = GetNextTriggerPoint(_future, _targetOption);
                            }
                        }

                        
                    }
                }

                //CryptoOrders.PlaceDEOrder(
                //GET Postion by symbol, and store in_future postion

                //Get option 6000 points far and sell call or put
                //get option such as permium should be 1% profit while going down.

                Interlocked.Increment(ref _healthCounter);
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //throw new Exception("Trading Stopped as algo encountered an error. Check log file for details");
                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime,
                //    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "ActiveTradeIntraday");
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Check the premium 2000 points down, if it is positive, increase future qty to make it 0, 
        /// and then after adding if the new mid point changes by 200 points atleast, then fine else increase qty
        /// </summary>
        /// <param name="targetOption"></param>
        /// <param name="futureLong"></param>
        /// <returns></returns>
        private int GetAdditionalFutureQuantity(TradedCryptoOption targetOption, bool futureLong, TradedCryptoFuture future, decimal futureSize)
        {
            int additionlQty = 0;
            if (futureLong)
            {
                decimal distanceFromLowerThreshold = 2000;
                while (true)
                {
                    decimal pnlAtThreshold = (targetOption.EntryPremium * targetOption.Size/1000) - (distanceFromLowerThreshold) * futureSize/1000;

                    if (pnlAtThreshold > 15)
                    {
                        //int incremental = Convert.ToInt32(pnlAtThreshold * 1000 / (future.EntryPrice + distanceFromLowerThreshold - future.CurrentPrice));
                        int incremental = Convert.ToInt32(pnlAtThreshold * 1000 / (distanceFromLowerThreshold));
                        int newFutureQty = Convert.ToInt32(incremental) + Convert.ToInt32(futureSize);

                        if(newFutureQty >= targetOption.Size)
                        {
                            additionlQty = Convert.ToInt32(targetOption.Size) - Convert.ToInt32(futureSize);

                            if(additionlQty <=0)
                            {
                                additionlQty = 0;
                            }

                            break;
                        }

                        decimal newFutureEntryPrice = (future.CurrentPrice * incremental + futureSize * future.EntryPrice) / newFutureQty;
                        decimal newLowerThreshold = newFutureEntryPrice - (targetOption.EntryPremium * targetOption.Size) / newFutureQty;
                        decimal newUpperThreshold = targetOption.Strike
                            + (newFutureQty * (targetOption.Strike - newFutureEntryPrice)
                            + (targetOption.EntryPremium * targetOption.Size)) / (targetOption.Size - newFutureQty);

                        decimal newTriggerPoint = (newUpperThreshold + newLowerThreshold) / 2;

                        if (newTriggerPoint > _nextTriggerPoint + 200)
                        {
                            additionlQty = incremental;
                            break;
                        }
                    }
                    else if (distanceFromLowerThreshold == 0)
                    {
                        break;
                    }
                    distanceFromLowerThreshold -= 200;

                    if (distanceFromLowerThreshold == 0)
                    {
                        break;
                    }
                }
            }
            //return additionlQty;
            else
            {
                decimal distanceFromUpperThreshold = 2000;
                while (true)
                {
                    decimal pnlAtThreshold = (targetOption.EntryPremium * targetOption.Size/1000) - ((distanceFromUpperThreshold) * futureSize / 1000);

                    if (pnlAtThreshold > 15)
                    {

                        //int incremental = Convert.ToInt32(pnlAtThreshold * 1000 /(future.EntryPrice + distanceFromUpperThreshold - future.CurrentPrice));
                        int incremental = Convert.ToInt32(pnlAtThreshold * 1000 / ( distanceFromUpperThreshold));
                        int newFutureQty = Convert.ToInt32(incremental) + Convert.ToInt32(futureSize);

                        if (newFutureQty >= targetOption.Size)
                        {
                            additionlQty = Convert.ToInt32(targetOption.Size) - Convert.ToInt32(futureSize);

                            if (additionlQty <= 0)
                            {
                                additionlQty = 0;
                            }

                            break;
                        }
                        decimal newFutureEntryPrice = (future.CurrentPrice * incremental + futureSize * future.EntryPrice) / (newFutureQty);

                        decimal newLowerThreshold = targetOption.Strike
                            - (newFutureQty * (newFutureEntryPrice - targetOption.Strike)
                            - (targetOption.EntryPremium * targetOption.Size)) / (targetOption.Size - newFutureQty);
                        decimal newUpperThreshold = newFutureEntryPrice + (targetOption.EntryPremium * targetOption.Size) / newFutureQty;

                        decimal newTriggerPoint = (newUpperThreshold + newLowerThreshold) / 2;

                        if (newTriggerPoint < _nextTriggerPoint - 200)
                        {
                            additionlQty = incremental;
                            break;
                        }
                    }
                    else if (distanceFromUpperThreshold == 0)
                    {
                        break;
                    }
                    distanceFromUpperThreshold -= 200;

                    if (distanceFromUpperThreshold == 0)
                    {
                        break;
                    }
                }
            }

            return additionlQty;
        }
        private int GetCorrectFutureQuantity(TradedCryptoOption targetOption, bool futureLong, TradedCryptoFuture future, decimal futureSize)
        {
            int additionlQty = 0;
            if (futureLong)
            {
                decimal distanceFromLowerThreshold = 2000;
                while (true)
                {
                    decimal pnlAtThreshold = (targetOption.EntryPremium * targetOption.Size / 1000) - (distanceFromLowerThreshold) * futureSize / 1000;

                    if (pnlAtThreshold > 15)
                    {
                        //int incremental = Convert.ToInt32(pnlAtThreshold * 1000 / (future.EntryPrice + distanceFromLowerThreshold - future.CurrentPrice));
                        int incremental = Convert.ToInt32(pnlAtThreshold * 1000 / (distanceFromLowerThreshold));
                        int newFutureQty = Convert.ToInt32(incremental) + Convert.ToInt32(futureSize);

                        if (newFutureQty >= targetOption.Size)
                        {
                            additionlQty = Convert.ToInt32(targetOption.Size) - Convert.ToInt32(futureSize);

                            if (additionlQty <= 0)
                            {
                                additionlQty = 0;
                            }

                            break;
                        }

                        decimal newFutureEntryPrice = (future.CurrentPrice * incremental + futureSize * future.EntryPrice) / newFutureQty;
                        decimal newLowerThreshold = newFutureEntryPrice - (targetOption.EntryPremium * targetOption.Size) / newFutureQty;
                        decimal newUpperThreshold = targetOption.Strike
                            + (newFutureQty * (targetOption.Strike - newFutureEntryPrice)
                            + (targetOption.EntryPremium * targetOption.Size)) / (targetOption.Size - newFutureQty);

                        decimal newTriggerPoint = (newUpperThreshold + newLowerThreshold) / 2;

                        if (newTriggerPoint < _nextTriggerPoint + 200)
                        {
                            additionlQty = incremental;
                            break;
                        }
                    }
                    else if (distanceFromLowerThreshold == 0)
                    {
                        break;
                    }
                    distanceFromLowerThreshold -= 200;

                    if (distanceFromLowerThreshold == 0)
                    {
                        break;
                    }
                }
            }
            //return additionlQty;
            else
            {
                decimal distanceFromUpperThreshold = 2000;
                while (true)
                {
                    decimal pnlAtThreshold = (targetOption.EntryPremium * targetOption.Size / 1000) - ((distanceFromUpperThreshold) * futureSize / 1000);

                    if (pnlAtThreshold > 15)
                    {

                        //int incremental = Convert.ToInt32(pnlAtThreshold * 1000 /(future.EntryPrice + distanceFromUpperThreshold - future.CurrentPrice));
                        int incremental = Convert.ToInt32(pnlAtThreshold * 1000 / (distanceFromUpperThreshold));

                        int newFutureQty = Convert.ToInt32(incremental) + Convert.ToInt32(futureSize);

                        decimal newFutureEntryPrice = (future.CurrentPrice * incremental + futureSize * future.EntryPrice) / (newFutureQty);

                        decimal newLowerThreshold = targetOption.Strike
                            - (newFutureQty * (newFutureEntryPrice - targetOption.Strike)
                            - (targetOption.EntryPremium * targetOption.Size)) / (targetOption.Size - newFutureQty);
                        decimal newUpperThreshold = newFutureEntryPrice + (targetOption.EntryPremium * targetOption.Size) / newFutureQty;

                        decimal newTriggerPoint = (newUpperThreshold + newLowerThreshold) / 2;

                        if (newTriggerPoint > _nextTriggerPoint - 200)
                        {
                            additionlQty = incremental;
                            break;
                        }
                    }
                    else if (distanceFromUpperThreshold == 0)
                    {
                        break;
                    }
                    distanceFromUpperThreshold -= 200;

                    if (distanceFromUpperThreshold == 0)
                    {
                        break;
                    }
                }
            }

            return additionlQty;
        }
        private bool GetNextTriggerPoint(TradedCryptoOption targetOption, bool futureLong, TradedCryptoFuture future)
        {
            decimal upperThreshold;
            decimal lowerThreshold;

            if (targetOption.Size == future.Size)
            {
                return false;
            }

            if (futureLong)
            {
                upperThreshold = targetOption.Strike + (future.Size * (targetOption.Strike - future.EntryPrice) + (targetOption.EntryPremium * targetOption.Size)) / (targetOption.Size - future.Size);
                lowerThreshold = future.EntryPrice - (targetOption.EntryPremium * targetOption.Size) / future.Size;
            }
            else
            {
                lowerThreshold = targetOption.Strike - (future.Size * (future.EntryPrice - targetOption.Strike) + (targetOption.EntryPremium * targetOption.Size)) / (targetOption.Size - future.Size);
                upperThreshold = future.EntryPrice + (targetOption.EntryPremium * targetOption.Size) / future.Size;
            }

            //Check if next trigger point has reached.
            _nextTriggerPoint = (upperThreshold + lowerThreshold) / 2;

             return (futureLong ? future.CurrentPrice > _nextTriggerPoint : future.CurrentPrice < _nextTriggerPoint) && future.Size < targetOption.Size;
        }

        private void UpdateOptionPremium(bool doubleSide, bool futureLong)
        {
            if (doubleSide)
            {
                if (_callOptions.ContainsKey(_targetOption.Strike))
                {
                    _targetOption.CurrentPremium = _callOptions[_targetOption.Strike].Ask;
                }
                if (_putOptions.ContainsKey(_targetOption2.Strike))
                {
                    _targetOption2.CurrentPremium = _putOptions[_targetOption2.Strike].Ask;
                }

                //if hedges have been taken
                if (_hedgeOption.Strike != 0 && _putOptions.ContainsKey(_hedgeOption.Strike))
                {
                    _hedgeOption.CurrentPremium = _putOptions[_hedgeOption.Strike].Bid;
                }
                if (_hedgeOption2.Strike != 0 && _callOptions.ContainsKey(_hedgeOption2.Strike))
                {
                    _hedgeOption2.CurrentPremium = _callOptions[_hedgeOption2.Strike].Bid;
                }
            }
            else if (_futureLong)
            {
                //target option will be call option

                if (_callOptions.ContainsKey(_targetOption.Strike))
                {
                    _targetOption.CurrentPremium = _callOptions[_targetOption.Strike].Ask;
                }
                if (_putOptions.ContainsKey(_hedgeOption.Strike))
                {
                    _hedgeOption.CurrentPremium = _putOptions[_hedgeOption.Strike].Bid;
                }
            }
            else
            {
                if (_putOptions.ContainsKey(_targetOption.Strike))
                {
                    _targetOption.CurrentPremium = _putOptions[_targetOption.Strike].Ask;
                }
                if (_callOptions.ContainsKey(_hedgeOption.Strike))
                {
                    _hedgeOption.CurrentPremium = _callOptions[_hedgeOption.Strike].Bid;
                }
            }
        }

        private void AddTargetOption(L1Orderbook orderbook, bool _futureLong, int additionalQty,
            TradedCryptoOption hedgeOption, TradedCryptoFuture future, TradedCryptoOption targetOption)
        {
            // Add to target option

            if (targetOption.CurrentPremium > targetOption.EntryPremium && targetOption.Size < 500)
            {
                int size = additionalQty + Convert.ToInt32(targetOption.Size) <= 500 ? additionalQty : 500 - Convert.ToInt32(targetOption.Size);
                //Close earlier hedgeoption
                order = CryptoOrders.PlaceDEOrder(_algoInstance, targetOption.Symbol, Convert.ToInt32(size), productId: 0, false,
                    algoIndex, _user, limitPrice: targetOption.CurrentPremium,
                    timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


                targetOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
                targetOption.EntryPremium = (targetOption.EntryPremium * targetOption.Size + Convert.ToDecimal(order.AverageFilledPrice) * order.Size) / (order.Size + targetOption.Size);
                targetOption.Size += order.Size;
                targetOption.PaidCommission += Convert.ToDecimal(order.PaidCommission);
            }

        }
        private void AddFuture(L1Orderbook orderbook, bool _futureLong, int additionalFutureQty, 
            TradedCryptoOption hedgeOption, TradedCryptoFuture future, TradedCryptoOption targetOption)
        {
            DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
            //place order for future
            //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());
            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, additionalFutureQty, productId: future.ProductId, _futureLong,
                algoIndex, _user, limitPrice: Convert.ToDecimal(future.CurrentPrice), timeStamp: currentTime,
                httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


            //var futurePosition = futurePositionTask.Result;
            future.Symbol = order.ProductSymbol;
            future.CurrentPrice = Convert.ToDecimal(order.AverageFilledPrice);

            future.EntryPrice = (future.EntryPrice * future.Size + Convert.ToDecimal(order.AverageFilledPrice) * order.Size) / (order.Size + future.Size);
            
            future.Size = order.Size + future.Size;
            future.Side = order.Side;
            future.PaidCommission += Convert.ToDecimal(order.PaidCommission);
            //_futureQty = Convert.ToInt32(future.Size);


          
            /* Hedge Option Logic Commented

            if (hedgeOption.Size != 0)
            {
                //Close earlier hedgeoption
                order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOption.Symbol, Convert.ToInt32(hedgeOption.Size), productId: 0, false,
                    algoIndex, _user, limitPrice: hedgeOption.CurrentPremium,
                    timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                //hedgeOption = new TradedCryptoOption();
                //hedgeOption.PaidCommission += Convert.ToDecimal(order.PaidCommission);
                _pnl += ((hedgeOption.CurrentPremium - hedgeOption.EntryPremium) * (hedgeOption.Size/1000) - Convert.ToDecimal(order.PaidCommission)) - Convert.ToDecimal(hedgeOption.PaidCommission);

            }
            //place order for hedge option
            var hedgeOptionStrikePremium = GetHedgeOption(future, targetOption, _futureLong);
            string hedgeOptionsymbolType = _futureLong ? "P" : "C";

            string hedgeOptionSymbol = $"{hedgeOptionsymbolType}-BTC-{hedgeOptionStrikePremium.Value.Key}-{_nextExpiry.ToString("ddMMyy")}";

            //var hedgeOption = ZObjects.deltaExchange.GetProductBySymbolAsync(hedgeOptionSymbol, _httpClientFactory.CreateClient());
            //Here the quantity should be for delta future above original size
            order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOptionSymbol, Convert.ToInt32(future.Size) - _futureQty, productId: 0, true,
                algoIndex, _user, limitPrice: hedgeOptionStrikePremium.Value.Value.Ask,
                timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);

            //var hedgeOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(hedgeOption.Id, null, client: _httpClientFactory.CreateClient());
            //hedgeOptionPositionTask.Wait();
            //var hedgeOptionPosition = hedgeOptionPositionTask.Result;


            //hedgeOption.EntryPremium = Convert.ToDecimal(hedgeOptionPosition.EntryPrice);

            hedgeOption.Symbol = order.ProductSymbol;
            hedgeOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
            hedgeOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
            hedgeOption.Size = order.Size;
            hedgeOption.Side = order.Side;
            hedgeOption.Strike = hedgeOptionStrikePremium.Value.Key;
            hedgeOption.Expiry = _nextExpiry;
            hedgeOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);
   
            */
        }
        private void ReduceFuture(L1Orderbook orderbook, bool _futureLong, int additionalFutureQty,
            TradedCryptoOption hedgeOption, TradedCryptoFuture future, TradedCryptoOption targetOption)
        {
            DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
            //place order for future
            //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());
            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, additionalFutureQty, productId: future.ProductId, !_futureLong,
                algoIndex, _user, limitPrice: Convert.ToDecimal(future.CurrentPrice), timeStamp: currentTime,
                httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


            //var futurePosition = futurePositionTask.Result;
            future.Symbol = order.ProductSymbol;
            future.CurrentPrice = Convert.ToDecimal(order.AverageFilledPrice);

            future.EntryPrice = (future.EntryPrice * future.Size - Convert.ToDecimal(order.AverageFilledPrice) * order.Size) / (future.Size - order.Size);

            _pnl += (Convert.ToDecimal(order.AverageFilledPrice) - future.EntryPrice) * (order.Size/1000) * (_futureLong ? 1 : -1);

            future.Size = future.Size -order.Size;
            //future.Side = order.Side;
            future.PaidCommission += Convert.ToDecimal(order.PaidCommission);
            //_futureQty = Convert.ToInt32(future.Size);

            /* Hedge Option Logic Commented

            if (hedgeOption.Size != 0)
            {
                //Close earlier hedgeoption
                order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOption.Symbol, Convert.ToInt32(hedgeOption.Size), productId: 0, false,
                    algoIndex, _user, limitPrice: hedgeOption.CurrentPremium,
                    timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                //hedgeOption = new TradedCryptoOption();
                //hedgeOption.PaidCommission += Convert.ToDecimal(order.PaidCommission);
                _pnl += ((hedgeOption.CurrentPremium - hedgeOption.EntryPremium) * (hedgeOption.Size/1000) - Convert.ToDecimal(order.PaidCommission)) - Convert.ToDecimal(hedgeOption.PaidCommission);

            }
            //place order for hedge option
            var hedgeOptionStrikePremium = GetHedgeOption(future, targetOption, _futureLong);
            string hedgeOptionsymbolType = _futureLong ? "P" : "C";

            string hedgeOptionSymbol = $"{hedgeOptionsymbolType}-BTC-{hedgeOptionStrikePremium.Value.Key}-{_nextExpiry.ToString("ddMMyy")}";

            //var hedgeOption = ZObjects.deltaExchange.GetProductBySymbolAsync(hedgeOptionSymbol, _httpClientFactory.CreateClient());
            //Here the quantity should be for delta future above original size
            order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOptionSymbol, Convert.ToInt32(future.Size) - _futureQty, productId: 0, true,
                algoIndex, _user, limitPrice: hedgeOptionStrikePremium.Value.Value.Ask,
                timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);

            //var hedgeOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(hedgeOption.Id, null, client: _httpClientFactory.CreateClient());
            //hedgeOptionPositionTask.Wait();
            //var hedgeOptionPosition = hedgeOptionPositionTask.Result;


            //hedgeOption.EntryPremium = Convert.ToDecimal(hedgeOptionPosition.EntryPrice);

            hedgeOption.Symbol = order.ProductSymbol;
            hedgeOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
            hedgeOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
            hedgeOption.Size = order.Size;
            hedgeOption.Side = order.Side;
            hedgeOption.Strike = hedgeOptionStrikePremium.Value.Key;
            hedgeOption.Expiry = _nextExpiry;
            hedgeOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);
   
            */
        }
        //private decimal GetNextTriggerPoint(TradedCryptoFuture future, TradedCryptoOption targetOption)
        //{
        //    return 1;
        //}

        private void UpdateOptionChain(L1Orderbook orderbook)
        {
            string[] optionSymbolArray = orderbook.Symbol.Split("-");

            if (optionSymbolArray.Count() < 4)
            {
                //this is not an option.this may be future , check for future BTC symbol

                if (orderbook.Symbol == "BTCUSD" && _future != null)
                {
                    _futurePrice = _future.CurrentPrice = _future2.CurrentPrice = _futureLong ? Convert.ToDecimal(orderbook.BestAsk) : Convert.ToDecimal(orderbook.BestBid);
                }
            }
            else
            {

                if (optionSymbolArray[3] == _expiryDate.ToString("ddMMyy"))
                {
                    if (optionSymbolArray[0].ToLower() == "c")
                    {
                        decimal strikePrice = Convert.ToDecimal(optionSymbolArray[2]);
                        if (_callOptions.ContainsKey(strikePrice))
                        {
                            _callOptions[strikePrice].Ask = Convert.ToDecimal(orderbook.BestAsk);
                            _callOptions[strikePrice].Bid = Convert.ToDecimal(orderbook.BestBid);
                        }
                        else
                        {
                            _callOptions.Add(strikePrice, new TradedCryptoOption()
                            {
                                Ask = Convert.ToDecimal(orderbook.BestAsk),
                                Bid = Convert.ToDecimal(orderbook.BestBid),
                                Strike = strikePrice,
                                Size = 0,
                                Expiry = _expiryDate,
                                Symbol = orderbook.Symbol
                            });
                        }
                    }
                    else if (optionSymbolArray[0].ToLower() == "p")
                    {
                        decimal strikePrice = Convert.ToDecimal(optionSymbolArray[2]);
                        if (_putOptions.ContainsKey(strikePrice))
                        {
                            _putOptions[strikePrice].Ask = Convert.ToDecimal(orderbook.BestAsk);
                            _putOptions[strikePrice].Bid = Convert.ToDecimal(orderbook.BestBid);
                        }
                        else
                        {
                            _putOptions.Add(strikePrice, new TradedCryptoOption()
                            {
                                Ask = Convert.ToDecimal(orderbook.BestAsk),
                                Bid = Convert.ToDecimal(orderbook.BestBid),
                                Strike = strikePrice,
                                Size = 0,
                                Expiry = _expiryDate,
                                Symbol = orderbook.Symbol
                            });
                        }
                    }
                }
            }
        }
        //private void UpdateOptionChain(L1Orderbook orderbook, bool _futureLong)
        //{
        //    string[] optionSymbolArray = orderbook.Symbol.Split("-");

        //    if (optionSymbolArray.Count() < 4)
        //    {
        //        //this is not an option.this may be future , check for future BTC symbol

        //        if (orderbook.Symbol == "BTCUSD" && _future != null)
        //        {
        //            _futurePrice = _future.CurrentPrice = _future2.CurrentPrice = _futureLong ? Convert.ToDecimal(orderbook.BestAsk) : Convert.ToDecimal(orderbook.BestBid);
                    
        //        }
        //    }
        //    else
        //    {

        //        if (optionSymbolArray[3] == _nextExpiry.ToString("ddMMyy"))
        //        {
        //            if (optionSymbolArray[0].ToLower() == "c")
        //            {
        //                decimal strikePrice = Convert.ToDecimal(optionSymbolArray[2]);
        //                if (_callOptions.ContainsKey(strikePrice))
        //                {
        //                    _callOptions[strikePrice] = Convert.ToDecimal(orderbook.BestAsk);
        //                }
        //                else
        //                {
        //                    _callOptions.Add(strikePrice, Convert.ToDecimal(orderbook.BestAsk));
        //                }
        //            }
        //            else if (optionSymbolArray[0].ToLower() == "p")
        //            {
        //                decimal strikePrice = Convert.ToDecimal(optionSymbolArray[2]);
        //                if (_putOptions.ContainsKey(strikePrice))
        //                {
        //                    _putOptions[strikePrice] = Convert.ToDecimal(orderbook.BestAsk);
        //                }
        //                else
        //                {
        //                    _putOptions.Add(strikePrice, Convert.ToDecimal(orderbook.BestAsk));
        //                }
        //            }
        //        }
        //    }
        //}
        private decimal ExitTrade(DateTime currentTime)
        {
            //exit here

            //place order for future
            //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());

            //no need to exit the future if direction is correct. For now testing with long only
            //CryptoOrder order = await CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, _futureQty, productId: orderbook.ProductId, _futureLong,
            //    algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk), timeStamp: DateTimeOffset.FromUnixTimeSeconds(orderbook.Timestamp).DateTime, httpClient: _httpClientFactory.CreateClient());

            CryptoDataLogic cdl = new CryptoDataLogic();

            decimal pnl = 0;

            pnl += ExitOption(_targetOption, currentTime);
            pnl += ExitOption(_targetOption2, currentTime);
            pnl += ExitOption(_hedgeOption, currentTime);
            pnl += ExitOption(_hedgeOption2, currentTime);

            decimal netFuture = Math.Abs(_future.Size - _future2.Size);
            bool bought = _future.Size > _future2.Size;

            _future.EntryPrice = (_future.EntryPrice * _future.Size + _future2.EntryPrice * _future2.Size) / (_future.Size + _future2.Size);

            ExitFuture(_future, currentTime, !bought, _future.PaidCommission+_future2.PaidCommission);

            //CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, _hedgeOption.Symbol, _futureQty, productId: _hedgeOption.ProductId, false,
            //   algoIndex, _user, limitPrice: _hedgeOption.CurrentPremium, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            ////cdl.UpdateOrder(_algoInstance, order);
            //commisionPaid = Convert.ToDecimal(order.PaidCommission);

            //order = CryptoOrders.PlaceDEOrder(_algoInstance, _targetOption.Symbol, _futureQty, productId: _targetOption.ProductId, true,
            //    algoIndex, _user, limitPrice: _targetOption.CurrentPremium, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            ////cdl.UpdateOrder(_algoInstance, order);
            //commisionPaid += Convert.ToDecimal(order.PaidCommission);

            _futureIsSet = false;
            return pnl;
        }

        private decimal ExitOption(TradedCryptoOption option, DateTime currentTime)
        {
            decimal pnl = 0;
            if (option.Size > 0)
            {
                bool boughtOption = option.Side == "buy" ? true : false;
                CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, option.Symbol, Convert.ToInt32(option.Size), productId: option.ProductId, !boughtOption,
              algoIndex, _user, limitPrice: option.CurrentPremium, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, timeStamp: currentTime, 
              httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                //cdl.UpdateOrder(_algoInstance, order);
                pnl = (Convert.ToDecimal(order.AverageFilledPrice) - option.EntryPremium)* (option.Size/1000) * (boughtOption ? 1 : -1) 
                    - Convert.ToDecimal(order.PaidCommission)
                    - Convert.ToDecimal(option.PaidCommission);
            }
            return pnl;
        }

        private decimal ExitFuture(TradedCryptoFuture future, DateTime currentTime, bool buyOrder, decimal paidCommision)
        {
            decimal pnl = 0;
            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, future.Symbol, Convert.ToInt32(future.Size), productId: future.ProductId, buyOrder,
          algoIndex, _user, limitPrice: future.CurrentPrice, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, timeStamp: currentTime,
          httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);
            pnl = (Convert.ToDecimal(order.AverageFilledPrice) - future.EntryPrice) * (future.Size / 1000) * (buyOrder ? -1 : 1)
                - Convert.ToDecimal(order.PaidCommission)
                - paidCommision;

            return pnl;
        }

        private void TakeTrade(L1Orderbook orderbook, bool _futureLong, bool _doubleSide)
        {
            //1/25 times the size of target option
            if (orderbook.Symbol == "BTCUSD")
            {
                DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
                if (_future.Size == 0)
                {
                    if (!_doubleSide)
                    {
                        //place order for future
                        //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());
                        CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, _futureQty, productId: orderbook.ProductId, _futureLong,
                            algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk), timeStamp: currentTime,
                            httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                        //var futurePositionTask = ZObjects.deltaExchange.GetCurrentPosition(orderbook.ProductId, null, client: _httpClientFactory.CreateClient());
                        //futurePositionTask.Wait();

                        //var futurePosition = futurePositionTask.Result;
                        _future.Symbol = order.ProductSymbol;
                        _future.CurrentPrice = Convert.ToDecimal(order.AverageFilledPrice);
                        _future.EntryPrice = Convert.ToDecimal(order.AverageFilledPrice);
                        _future.Size = order.Size;
                        _future.Side = order.Side;
                        _future.PaidCommission = Convert.ToDecimal(order.PaidCommission);
                        //CryptoDataLogic cdl = new CryptoDataLogic();
                        //cdl.UpdateOrder(_algoInstance, order);
                    }
                    else
                    {
                        _future.Symbol = "BTCUSD";
                        _future.EntryPrice = _future.CurrentPrice;
                        _future.Size = _futureQty;
                        _future.Side = "buy";
                        _future.PaidCommission = 0.05M / 100 * _futurePrice * _future.Size/1000;

                        _future2.Symbol = "BTCUSD";
                        _future2.EntryPrice = _future2.CurrentPrice;
                        _future2.Size = _futureQty;
                        _future2.Side = "sell";
                        _future2.PaidCommission = 0.05M / 100 * _futurePrice * _future2.Size/1000;
                    }

                }
                else
                {
                    if (_doubleSide)
                    {
                        if (_future.Size + _future2.Size > 2 * _futureQty)
                        {
                            if (_future.Size > _future2.Size)
                            {
                                decimal netFutureQty = _future.Size + _future2.Size - 2 * _futureQty;
                                ResetFuture(_future, orderbook, true, Convert.ToInt32(netFutureQty));
                            }
                            else
                            {
                                decimal netFutureQty = _future.Size + _future2.Size - 2 * _futureQty;
                                ResetFuture(_future2, orderbook, false, Convert.ToInt32(netFutureQty));
                            }
                            _future.EntryPrice = _future.CurrentPrice;
                            _future.Size = _futureQty;
                            _future.Side = "buy";
                            _future2.EntryPrice = _future.CurrentPrice;
                            _future2.Size = _futureQty;
                            _future2.Side = "sell";
                        }
                    }
                    else
                    {
                        ResetFuture(_future, orderbook, _futureLong, Convert.ToInt32(_future.Size) - _futureQty);
                    }
                }
                _futureIsSet = true;
            }
            if (_futureIsSet)
            {
                // This should be atleast $150, and 3500 hours far
                //Qty should be 0.5
                DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;

                if(_doubleSide)
                {
                    _targetOption = GetTargetOption(true, currentTime);
                    _targetOption2 = GetTargetOption(false, currentTime);
                }
                else
                {
                    _targetOption = GetTargetOption(_futureLong, currentTime);
                    _hedgeOption = GetHedgeOption(_futureLong, currentTime, _targetOption);
                }

                _entryDate = currentTime.Date;
            }
        }
        private void ResetFuture(TradedCryptoFuture future, L1Orderbook orderbook, bool futureLong, int futureQty)
        {
            CryptoOrder order;
            if (future.Size > _futureQty)
            {
                int qty = futureQty;
                order = CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, qty, productId: orderbook.ProductId, !futureLong,
                algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk),
                timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime,
                httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


                //CryptoDataLogic cdl = new CryptoDataLogic();
                //cdl.UpdateOrder(_algoInstance, order);

                future.CurrentPrice = Convert.ToDecimal(order.AverageFilledPrice);
                future.EntryPrice = Convert.ToDecimal(order.AverageFilledPrice);
                future.Size = _futureQty;
                future.Side = futureLong ? "buy" : "sell";// order.Side;
                future.PaidCommission = Convert.ToDecimal(order.PaidCommission);
            }

            //var futurePositionTask = ZObjects.deltaExchange.GetCurrentPosition(orderbook.ProductId, null, client: _httpClientFactory.CreateClient());
            //futurePositionTask.Wait();
            //var futurePosition = futurePositionTask.Result;

            future.EntryPrice = future.CurrentPrice;
            future.Size = _futureQty;
            future.Side = futureLong ? "buy" : "sell";// order.Side;
        }
        private TradedCryptoOption GetTargetOption(bool futureLong, DateTime currentTime)
        {
            var targetOptionStrikePremium = GetTargetOption(_future, futureLong);
            if (targetOptionStrikePremium == null)
            {
                return null;
            }
            string targetOptionsymbolType = futureLong ? "C" : "P";
            //place order for hedge option
            //_nextExpiry = DateTime.Today.AddDays(1);

            string targetOptionSymbol = $"{targetOptionsymbolType}-BTC-{targetOptionStrikePremium?.Key}-{_nextExpiry.ToString("ddMMyy")}";

            //_targetOption.Size = TARGET_OPTION_FUTURE_MULTIPLIER * _futureQty;
            //_targetOption.CurrentPremium = targetOptionStrikePremium.Value.Value;

            //Product targetOption = ZObjects.deltaExchange.GetProductBySymbolAsync(targetOptionSymbol, _httpClientFactory.CreateClient());
            //DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;

            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, targetOptionSymbol, Convert.ToInt32(TARGET_OPTION_FUTURE_MULTIPLIER * _futureQty), 0, false,
                algoIndex, _user, limitPrice: targetOptionStrikePremium.Value.Value.Bid, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, currentTime,
                httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //CryptoDataLogic cdl = new CryptoDataLogic();
            //cdl.UpdateOrder(_algoInstance, order);

            //var targetOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(targetOption.Id, null, client: _httpClientFactory.CreateClient());
            //targetOptionPositionTask.Wait();
            //var targetOptionPosition = targetOptionPositionTask.Result;

            //_targetOption.EntryPremium = Convert.ToDecimal(targetOptionPosition.EntryPrice);

            //_targetOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);

            var targetOption = new TradedCryptoOption();
            targetOption.Symbol = order.ProductSymbol;
            targetOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
            targetOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
            targetOption.Size = order.Size;
            targetOption.Side = order.Side;
            targetOption.Strike = targetOptionStrikePremium.Value.Key;
            targetOption.Expiry = _nextExpiry;
            targetOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);

            return targetOption;
        }

        private TradedCryptoOption GetHedgeOption(bool futureLong, DateTime currentTime, TradedCryptoOption targetOption)
        {
            //place order for hedge option
            var hedgeOptionStrikePremium = GetHedgeOption(_future, targetOption, futureLong);
            string hedgeOptionsymbolType = futureLong ? "P" : "C";

            string hedgeOptionSymbol = $"{hedgeOptionsymbolType}-BTC-{hedgeOptionStrikePremium.Value.Key}-{_nextExpiry.ToString("ddMMyy")}";

            //_hedgeOption.CurrentPremium = hedgeOptionStrikePremium.Value.Value;
            //var hedgeOption = ZObjects.deltaExchange.GetProductBySymbolAsync(hedgeOptionSymbol, _httpClientFactory.CreateClient());

            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOptionSymbol, _futureQty, productId: 0, true,
                algoIndex, _user, limitPrice: hedgeOptionStrikePremium.Value.Value.Ask,
                timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);

            //var hedgeOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(hedgeOption.Id, null, client: _httpClientFactory.CreateClient());
            //hedgeOptionPositionTask.Wait();
            //var hedgeOptionPosition = hedgeOptionPositionTask.Result;


            //_hedgeOption.EntryPremium = Convert.ToDecimal(hedgeOptionPosition.EntryPrice);
            var hedgeOption = new TradedCryptoOption();
            hedgeOption.Symbol = order.ProductSymbol;
            hedgeOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
            hedgeOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
            hedgeOption.Size = _futureQty;
            hedgeOption.Side = order.Side;
            hedgeOption.Strike = hedgeOptionStrikePremium.Value.Key;
            hedgeOption.Expiry = _nextExpiry;
            hedgeOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);

            return hedgeOption;
        }

        private bool CheckTargetOptionAvailability(bool _futureLong, decimal futurePrice)
        {
            if (_futureLong)
            {
                var higherCalls = _callOptions.Where(x => x.Key >= futurePrice + TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value.Bid <= TARGET_OPTION_MAX_PREMIUM);

                return higherCalls.Count() >= 2;

            }
            else
            {
                //target option will be put
                //As far as possible with minimum profitability

                var lowerputs = _putOptions.Where(x => x.Key <= futurePrice - TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value.Bid <= TARGET_OPTION_MAX_PREMIUM);

                return lowerputs.Count() >= 2;
            }
        }
        private bool CheckHedgeOptionAvailability(bool _futureLong, decimal futurePrice)
        {
            if (_futureLong)
            {
                var lowerPuts = _putOptions.Where(x => x.Key < futurePrice);
                if (lowerPuts.Count() < 5)
                {
                    return false;
                }
            }
            else
            {
                var higherCalls = _callOptions.Where(x => x.Key > futurePrice);
                if (higherCalls.Count() < 5)
                {
                    return false;
                }
            }
            return true;
        }

        private KeyValuePair<decimal, TradedCryptoOption>? GetTargetOption(TradedCryptoFuture future, bool _futureLong)
        {

            if (_futureLong)
            {
                //target option will be call
                //As far as possible with minimum profitability

                if(_callOptions.Any(x => x.Key >= future.CurrentPrice + TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value.Bid <= TARGET_OPTION_MAX_PREMIUM))
                {
                    var higherCalls = _callOptions.Where(x => x.Key >= future.CurrentPrice + TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value.Bid <= TARGET_OPTION_MAX_PREMIUM);
                    return higherCalls.OrderBy(x => x.Key).First();
                }
            }
            else
            {
                //target option will be put
                //As far as possible with minimum profitability

                if (_putOptions.Any(x => x.Key <= future.CurrentPrice - TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value.Bid <= TARGET_OPTION_MAX_PREMIUM))
                {
                    var lowerputs = _putOptions.Where(x => x.Key <= future.CurrentPrice - TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value.Bid <= TARGET_OPTION_MAX_PREMIUM);
                    return lowerputs.OrderByDescending(x => x.Key).First();
                }
            }
            return null;
        }


        private KeyValuePair<decimal,TradedCryptoOption>? GetHedgeOption(TradedCryptoFuture future, TradedCryptoOption tradedOption, bool _futureLong)
        {
            decimal premiumReceivedFromTargetOption = tradedOption.Size * tradedOption.CurrentPremium;// EntryPremium;

            if (_futureLong)
            {
                //hedge will be put options
                //As far as possible with minimum profitability

                var lowerPuts = _putOptions.Where(x=>x.Key <= future.CurrentPrice);
                if(lowerPuts.Count() == 0)
                {
                    return null;
                }
                KeyValuePair<decimal, TradedCryptoOption> highestStrikeOption = lowerPuts.OrderByDescending(x => x.Key).First();
                foreach(var option in lowerPuts)
                {
                    if(((premiumReceivedFromTargetOption - option.Value.Ask * future.Size) - (future.EntryPrice - option.Key) * future.Size) * 85 > MINIMUM_PROFIT_HEDGE_SIDE)
                    {
                        highestStrikeOption = highestStrikeOption.Key > option.Value.Strike ? option : highestStrikeOption;
                    }
                }
                return highestStrikeOption;
            }
            else
            {
                //hedge will be call options
                //As far as possible with minimum profitability

                var higherCalls = _callOptions.Where(x => x.Key > future.CurrentPrice);

                if (higherCalls.Count() == 0)
                {
                    return null;
                }

                KeyValuePair<decimal, TradedCryptoOption> lowestStrikeOption = higherCalls.OrderBy(x => x.Key).First(); 

                foreach (var option in higherCalls)
                {
                    if (((premiumReceivedFromTargetOption - option.Value.Ask * future.Size) - (option.Key - future.EntryPrice) * future.Size) * 85 > MINIMUM_PROFIT_HEDGE_SIDE)
                    {
                        lowestStrikeOption = lowestStrikeOption.Key < option.Value.Strike ? option : lowestStrikeOption;
                    }
                }
                return lowestStrikeOption;
            }
        }


        public virtual void OnNext(string channel, string data)
        {
            try
            {
                if (_stopTrade)
                {
                    return;

                }

#if local
                //if (tick.Timestamp.HasValue && _currentDate.HasValue && tick.Timestamp.Value.Date != _currentDate)
                //{
                //    ResetAlgo(tick.Timestamp.Value.Date);
                //}
#endif
                ActiveTradeIntraday(channel, data);
                //return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, tick.Timestamp.GetValueOrDefault(DateTime.UtcNow),
                //    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                //   return;
            }
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

        public void StopTrade(bool stopTrade)
        {
            _stopTrade = stopTrade;
        }

        public virtual void OnCompleted()
        {
            Console.WriteLine("Additional Ticks data will not be transmitted.");
        }

        public int AlgoInstance
        {
            get
            {
                return _algoInstance;
            }
            set
            {
                _algoInstance = value;
            }
        }
        public virtual void OnError(Exception error)
        {
            // Do nothing.
        }
    }
}
