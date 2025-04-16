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
using static GlobalLayer.L2OrderBook;
//using static GlobalLayer.L2OrderBook;

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
        private decimal _futurePrice;
        private decimal _hedgeOptionPrice;
        private decimal _targetOptionPrice;

        private readonly int MINIMUM_PROFIT_HEDGE_SIDE;
        private const int TARGET_MINIMUM_DISTANCE_FROM_FUTURE = 4000;
        private const int TARGET_OPTION_MAX_PREMIUM = 210;
        private const decimal TARGET_OPTION_FUTURE_MULTIPLIER = 25;
        private readonly int FUTURE_STEP_QUANTITY;

        //strike, premium
        Dictionary<decimal, decimal> callOptions = new Dictionary<decimal, decimal>();
        Dictionary<decimal, decimal> putOptions = new Dictionary<decimal, decimal>();
        
        public PriceDirectionalFutureOptions(TimeSpan candleTimeSpan, uint baseInstrumentToken,
            DateTime expiry, int quantity, string uid, decimal targetProfit, decimal stopLoss,
            decimal pnl, int algoInstance = 0,  IHttpClientFactory httpClientFactory = null)
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
            _hedgeOption = new TradedCryptoOption();
            _targetOption = new TradedCryptoOption();

            SubscriptionTokens = new List<uint>();
            CandleSeries candleSeries = new CandleSeries();
            
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, 27, DateTime.Now,
                expiry, _futureQty, 0, 0, 0, 0,
                0, 0, 0, 0, candleTimeFrameInMins:
                0, CandleType.Time, 0, _targetProfit, _stopLoss, 0,
                0, 0, Arg9: _user==null? "": _user.UserId, positionSizing: false, maxLossPerTrade: 0);

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
            bool futureLong = false;
            bool doubleSide = true;
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


                UpdateOptionChain(orderbook, futureLong);


                _targetOptionAvailable = _targetOptionAvailable || CheckTargetOptionAvailability(futureLong, _futurePrice);
                _hedgeOptionAvailable = _hedgeOptionAvailable || CheckHedgeOptionAvailability(futureLong, _futurePrice);

                if (_targetOptionAvailable && _hedgeOptionAvailable && _hedgeOption.Size == 0 
                    //&& _future.CurrentPrice > 86400
                    //&& DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp/1000).LocalDateTime.TimeOfDay > ENTRY_WINDOW_START
                   // && DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime.TimeOfDay < ENTRY_WINDOW_END
                    )
                {
                    //_future.Symbol = "BSTUSD";
                    //_future.CurrentPrice = 82254;
                    //_future.EntryPrice = 82226.5M;
                    //_future.Size = 20;
                    //_future.Side = "buy";
                    //_future.PaidCommission = 0.97M;

                    //_targetOption.Symbol = "C-BTC-86600-110325";
                    //_targetOption.CurrentPremium = 167;
                    //_targetOption.EntryPremium = 176.1M;
                    //_targetOption.Size = 500;
                    //_targetOption.Side = "Sell";
                    //_targetOption.Strike = 86600;
                    //_targetOption.Expiry = _nextExpiry;
                    //_targetOption.PaidCommission = 10.38M;

                    //_hedgeOption.Symbol = "P-BTC-81000-110325";
                    //_hedgeOption.CurrentPremium = 715;
                    //_hedgeOption.EntryPremium = 733;
                    //_hedgeOption.Size = 20;
                    //_hedgeOption.Side = "Buy";
                    //_hedgeOption.Strike = 81000;
                    //_hedgeOption.Expiry = _nextExpiry;
                    //_hedgeOption.PaidCommission = 0.582m;
                    //_entryDate = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime.Date;
                    TakeTrade(orderbook, futureLong, doubleSide);
                }
                else if (_hedgeOption != null && _hedgeOption.Size != 0)
                {
                    if (futureLong)
                    {
                        //target option will be call option

                        if (callOptions.ContainsKey(_targetOption.Strike))
                        {
                            _targetOption.CurrentPremium = callOptions[_targetOption.Strike];
                        }
                        if (putOptions.ContainsKey(_hedgeOption.Strike))
                        {
                            _hedgeOption.CurrentPremium = putOptions[_hedgeOption.Strike];
                        }
                    }
                    else
                    {
                        if (putOptions.ContainsKey(_targetOption.Strike))
                        {
                            _targetOption.CurrentPremium = putOptions[_targetOption.Strike];
                        }
                        if (callOptions.ContainsKey(_hedgeOption.Strike))
                        {
                            _hedgeOption.CurrentPremium = callOptions[_hedgeOption.Strike];
                        }
                        
                    }
                    _pnl = (_future.CurrentPrice - _future.EntryPrice) * (_future.Size/1000) * (futureLong ? 1 : -1)
                                   + (_hedgeOption.Size/1000) * (_hedgeOption.CurrentPremium - _hedgeOption.EntryPremium)
                                   + (_targetOption.EntryPremium - _targetOption.CurrentPremium) * (_targetOption.Size/1000) 
                                   - 2* (_future.PaidCommission + _targetOption.PaidCommission + _hedgeOption.PaidCommission);
                    
                    //INR conversion
                    _pnl = 85*_pnl;

#if BACKTEST
                    DateTime currentime1 = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
                    Console.WriteLine($"{_pnl}: {currentime1}");
#endif

                    //Change expiries after exit to next expiries
                    if (_pnl > _targetProfit //|| _pnl < _stopLoss * -1
                        ||
                        (DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime.TimeOfDay > ENTRY_WINDOW_END.Subtract(TimeSpan.FromMinutes(10))
                        && _entryDate.Date != DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).Date)//DateTime.Now.Date)
                        )
                    {
                        DateTime currentime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;
                        _pnl -= ExitTrade(futureLong, currentime);
                        _stopTrade = true;

                        CryptoDataLogic dl = new CryptoDataLogic();
                        dl.UpdateAlgoPnl(_algoInstance, _pnl, currentime);

                        _future = new TradedCryptoFuture();
                        _hedgeOption = new TradedCryptoOption();
                        _targetOption = new TradedCryptoOption();
                        //_nextExpiry = _nextExpiry;
                        _pnl = 0;
                        callOptions.Clear();
                        putOptions.Clear();
                        _targetOptionAvailable = false;
                        _hedgeOptionAvailable = false;
                    }
                    else
                    {
                        decimal upperThreshold = 0;
                        decimal lowerThreshold = 0;
                        if (futureLong)
                        {
                            upperThreshold = _targetOption.Strike + (_future.Size * (_targetOption.Strike - _future.EntryPrice) + (_targetOption.EntryPremium * _targetOption.Size)) / (_targetOption.Size - _future.Size);
                            lowerThreshold = _future.EntryPrice - (_targetOption.EntryPremium * _targetOption.Size) / _future.Size;
                        }
                        else
                        {
                            lowerThreshold = _targetOption.Strike - (_future.Size * (_future.EntryPrice - _targetOption.Strike) - (_targetOption.EntryPremium * _targetOption.Size)) / (_targetOption.Size - _future.Size);
                            upperThreshold = _future.EntryPrice + (_targetOption.EntryPremium * _targetOption.Size) / _future.Size;
                        }

                        //Check if next trigger point has reached.
                        _nextTriggerPoint = (upperThreshold + lowerThreshold) / 2;


                        //upper and lower threshold
                        bool entryTicket = futureLong ? _future.CurrentPrice > _nextTriggerPoint : _future.CurrentPrice < _nextTriggerPoint;

                        //long future case
                        if (entryTicket && _future.Size < _targetOption.Size)
                        {

                            int additionlQty = FUTURE_STEP_QUANTITY;
                            if (futureLong)
                            {
                                //Check the premium 2000 points down, if it is positive, increase future qty to make it 0, and then after adding if the new mid point changes by 200 points atleast, then fine else increase qty

                                decimal distanceFromLowerThreshold = 2000;
                                while (true) { 

                                    decimal pnlAtThreshold = (_targetOption.EntryPremium * _targetOption.Size) - (_future.EntryPrice - distanceFromLowerThreshold) * _future.Size;

                                    if (pnlAtThreshold > 15)
                                    {
                                        int newFutureQty = Convert.ToInt32(pnlAtThreshold);

                                        decimal newFutureEntryPrice = (_future.CurrentPrice * additionlQty + _future.Size * _future.EntryPrice) / newFutureQty;
                                        decimal newLowerThreshold = newFutureEntryPrice - (_targetOption.EntryPremium * _targetOption.Size) / newFutureQty;
                                        decimal newUpperThreshold = _targetOption.Strike
                                            + (newFutureQty * (_targetOption.Strike - newFutureEntryPrice)
                                            + (_targetOption.EntryPremium * _targetOption.Size)) / (_targetOption.Size - newFutureQty);

                                        decimal newTriggerPoint = (newUpperThreshold + newLowerThreshold) / 2;

                                        if (newTriggerPoint > _nextTriggerPoint + 200)
                                        {
                                            additionlQty = newFutureQty;
                                            break;
                                        }
                                    }
                                    else if ( distanceFromLowerThreshold == 0 )
                                    {
                                        break;
                                    }
                                    distanceFromLowerThreshold -= 200;
                                }
                            }
                            else
                            {
                                //Check the premium 2000 points down, if it is positive, increase future qty to make it 0, and then after adding if the new mid point changes by 200 points atleast, then fine else increase qty

                                decimal distanceFromUpperThreshold = 2000;
                                while (true)
                                {
                                    decimal pnlAtThreshold = (_targetOption.EntryPremium * _targetOption.Size) - (_future.EntryPrice - distanceFromUpperThreshold) * _future.Size;

                                    if (pnlAtThreshold > 15)
                                    {
                                        int newFutureQty = Convert.ToInt32(pnlAtThreshold);

                                        decimal newFutureEntryPrice = (_future.CurrentPrice * additionlQty + _future.Size * _future.EntryPrice) / newFutureQty;

                                        decimal newLowerThreshold = _targetOption.Strike
                                            - (newFutureQty * (newFutureEntryPrice - _targetOption.Strike)
                                            - (_targetOption.EntryPremium * _targetOption.Size)) / (_targetOption.Size - newFutureQty);
                                        decimal newUpperThreshold = newFutureEntryPrice + (_targetOption.EntryPremium * _targetOption.Size) / newFutureQty;

                                        decimal newTriggerPoint = (newUpperThreshold + newLowerThreshold) / 2;

                                        if (newTriggerPoint < _nextTriggerPoint - 200)
                                        {
                                            additionlQty = newFutureQty;
                                            break;
                                        }
                                    }
                                    else if (distanceFromUpperThreshold == 0)
                                    {
                                        break;
                                    }
                                    distanceFromUpperThreshold -= 200;
                                }

                            }
                            AddFuture(orderbook, futureLong, additionlQty);
                            //_nextTriggerPoint = GetNextTriggerPoint(_future, _targetOption);
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
        private void AddFuture(L1Orderbook orderbook, bool futureLong, int additionalFutureQty)
        {
            //place order for future
            //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());
            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, additionalFutureQty, productId: orderbook.ProductId, futureLong,
                algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk), timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime,
                httpClient: _httpClientFactory== null?null: _httpClientFactory.CreateClient());


            //var futurePosition = futurePositionTask.Result;
            _future.Symbol = order.ProductSymbol;
            _future.CurrentPrice = Convert.ToDecimal(order.AverageFilledPrice);
            
            _future.EntryPrice = (_future.EntryPrice *_future.Size +  Convert.ToDecimal(order.AverageFilledPrice)*order.Size)/(order.Size+_future.Size);
            
            _future.Size = order.Size + _future.Size;
            _future.Side = order.Side;
            _future.PaidCommission += Convert.ToDecimal(order.PaidCommission);
            _futureQty = Convert.ToInt32(_future.Size);



            //Close earlier hedgeoption
            order = CryptoOrders.PlaceDEOrder(_algoInstance, _hedgeOption.Symbol, Convert.ToInt32(_hedgeOption.Size), productId: 0, false,
                algoIndex, _user, limitPrice: _hedgeOption.CurrentPremium,
                timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            _hedgeOption = new TradedCryptoOption();
            _hedgeOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);

            //place order for hedge option
            var hedgeOptionStrikePremium = GetHedgeOption(_future, _targetOption, futureLong);
            string hedgeOptionsymbolType = futureLong ? "P" : "C";

            string hedgeOptionSymbol = $"{hedgeOptionsymbolType}-BTC-{hedgeOptionStrikePremium.Value.Key}-{_nextExpiry.ToString("ddMMyy")}";

            //var hedgeOption = ZObjects.deltaExchange.GetProductBySymbolAsync(hedgeOptionSymbol, _httpClientFactory.CreateClient());

            order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOptionSymbol, _futureQty, productId: 0, true,
                algoIndex, _user, limitPrice: hedgeOptionStrikePremium.Value.Value,
                timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);

            //var hedgeOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(hedgeOption.Id, null, client: _httpClientFactory.CreateClient());
            //hedgeOptionPositionTask.Wait();
            //var hedgeOptionPosition = hedgeOptionPositionTask.Result;


            //_hedgeOption.EntryPremium = Convert.ToDecimal(hedgeOptionPosition.EntryPrice);

            _hedgeOption.Symbol = order.ProductSymbol;
            _hedgeOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
            _hedgeOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
            _hedgeOption.Size = _futureQty;
            _hedgeOption.Side = order.Side;
            _hedgeOption.Strike = hedgeOptionStrikePremium.Value.Key;
            _hedgeOption.Expiry = _nextExpiry;
            _hedgeOption.PaidCommission += Convert.ToDecimal(order.PaidCommission);
   

        }
        //private decimal GetNextTriggerPoint(TradedCryptoFuture future, TradedCryptoOption targetOption)
        //{
        //    return 1;
        //}

        private void UpdateOptionChain(L1Orderbook orderbook, bool futureLong)
        {
            string[] optionSymbolArray = orderbook.Symbol.Split("-");

            if (optionSymbolArray.Count() < 4)
            {
                //this is not an option.this may be future , check for future BTC symbol

                if (orderbook.Symbol == "BTCUSD" && _future != null)
                {
                    _futurePrice = _future.CurrentPrice = futureLong ? Convert.ToDecimal(orderbook.BestAsk) : Convert.ToDecimal(orderbook.BestBid);
                    
                }
            }
            else
            {

                if (optionSymbolArray[3] == _nextExpiry.ToString("ddMMyy"))
                {
                    if (optionSymbolArray[0].ToLower() == "c")
                    {
                        decimal strikePrice = Convert.ToDecimal(optionSymbolArray[2]);
                        if (callOptions.ContainsKey(strikePrice))
                        {
                            callOptions[strikePrice] = Convert.ToDecimal(orderbook.BestAsk);
                        }
                        else
                        {
                            callOptions.Add(strikePrice, Convert.ToDecimal(orderbook.BestAsk));
                        }
                    }
                    else if (optionSymbolArray[0].ToLower() == "p")
                    {
                        decimal strikePrice = Convert.ToDecimal(optionSymbolArray[2]);
                        if (putOptions.ContainsKey(strikePrice))
                        {
                            putOptions[strikePrice] = Convert.ToDecimal(orderbook.BestAsk);
                        }
                        else
                        {
                            putOptions.Add(strikePrice, Convert.ToDecimal(orderbook.BestAsk));
                        }
                    }
                }
            }
        }
        private decimal ExitTrade(bool futureLong, DateTime currentTime)
        {
            //exit here

            //place order for future
            //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());

            //no need to exit the future if direction is correct. For now testing with long only
            //CryptoOrder order = await CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, _futureQty, productId: orderbook.ProductId, futureLong,
            //    algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk), timeStamp: DateTimeOffset.FromUnixTimeSeconds(orderbook.Timestamp).DateTime, httpClient: _httpClientFactory.CreateClient());

            CryptoDataLogic cdl = new CryptoDataLogic();

            decimal commisionPaid = 0;

            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, _hedgeOption.Symbol, _futureQty, productId: _hedgeOption.ProductId, false,
               algoIndex, _user, limitPrice: _hedgeOption.CurrentPremium, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);
            commisionPaid = Convert.ToDecimal(order.PaidCommission);

            order = CryptoOrders.PlaceDEOrder(_algoInstance, _targetOption.Symbol, _futureQty, productId: _targetOption.ProductId, true,
                algoIndex, _user, limitPrice: _targetOption.CurrentPremium, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            //cdl.UpdateOrder(_algoInstance, order);
            commisionPaid += Convert.ToDecimal(order.PaidCommission);

            _futureIsSet = false;
            return commisionPaid;
        }

        private void TakeTrade(L1Orderbook orderbook, bool futureLong, bool doubleSide)
        {
            //1/25 times the size of target option
            if (orderbook.Symbol == "BTCUSD")
            {
                if (_future.Size == 0)
                {
                    if (!doubleSide)
                    {
                        //place order for future
                        //Product btcFuture = await ZObjects.deltaExchange.GetProductBySymbolAsync("BTCUSD", _httpClientFactory.CreateClient());
                        CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, _futureQty, productId: orderbook.ProductId, futureLong,
                            algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk), timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime,
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
                        _future.Side = futureLong ? "buy" : "sell";
                        _future.PaidCommission = 0.05M / 100 * _futurePrice * _future.Size;
                    }

                }
                else
                {
                    CryptoOrder order;
                    if (_future.Size > _futureQty)
                    {
                        int qty = Convert.ToInt32(_future.Size) - _futureQty;
                        order = CryptoOrders.PlaceDEOrder(_algoInstance, orderbook.Symbol, qty, productId: orderbook.ProductId, !futureLong,
                        algoIndex, _user, limitPrice: Convert.ToDecimal(orderbook.BestAsk),
                        timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime,
                        httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());


                        //CryptoDataLogic cdl = new CryptoDataLogic();
                        //cdl.UpdateOrder(_algoInstance, order);

                        _future.CurrentPrice = Convert.ToDecimal(order.AverageFilledPrice);
                        _future.EntryPrice = Convert.ToDecimal(order.AverageFilledPrice);
                        _future.Size = _futureQty;
                        _future.Side = futureLong ? "buy" : "sell";// order.Side;
                        _future.PaidCommission = Convert.ToDecimal(order.PaidCommission);
                    }

                    //var futurePositionTask = ZObjects.deltaExchange.GetCurrentPosition(orderbook.ProductId, null, client: _httpClientFactory.CreateClient());
                    //futurePositionTask.Wait();
                    //var futurePosition = futurePositionTask.Result;

                    _future.EntryPrice = _future.CurrentPrice;
                    _future.Size = _futureQty;
                    _future.Side = futureLong ? "buy" : "sell";// order.Side;
                }
                _futureIsSet = true;
            }
            if (_futureIsSet)
            {
                // This should be atleast $150, and 3500 hours far
                //Qty should be 0.5

                var targetOptionStrikePremium = GetTargetOption(_future, futureLong);
                if(targetOptionStrikePremium == null)
                {
                    return;
                }
                string targetOptionsymbolType = futureLong ? "C" : "P";
                //place order for hedge option
                //_nextExpiry = DateTime.Today.AddDays(1);

                string targetOptionSymbol = $"{targetOptionsymbolType}-BTC-{targetOptionStrikePremium?.Key}-{_nextExpiry.ToString("ddMMyy")}";

                _targetOption.Size = TARGET_OPTION_FUTURE_MULTIPLIER * _futureQty;
                _targetOption.CurrentPremium = targetOptionStrikePremium.Value.Value;

                //Product targetOption = ZObjects.deltaExchange.GetProductBySymbolAsync(targetOptionSymbol, _httpClientFactory.CreateClient());

                CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, targetOptionSymbol, Convert.ToInt32(_targetOption.Size), 0, false,
                    algoIndex, _user, limitPrice: _targetOption.CurrentPremium, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime,
                    httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                //CryptoDataLogic cdl = new CryptoDataLogic();
                //cdl.UpdateOrder(_algoInstance, order);

                //var targetOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(targetOption.Id, null, client: _httpClientFactory.CreateClient());
                //targetOptionPositionTask.Wait();
                //var targetOptionPosition = targetOptionPositionTask.Result;

                //_targetOption.EntryPremium = Convert.ToDecimal(targetOptionPosition.EntryPrice);

                //_targetOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);

                _targetOption.Symbol = order.ProductSymbol;
                _targetOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
                _targetOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
                _targetOption.Size = order.Size;
                _targetOption.Side = order.Side;
                _targetOption.Strike = targetOptionStrikePremium.Value.Key;
                _targetOption.Expiry = _nextExpiry;
                _targetOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);

                //place order for hedge option
                var hedgeOptionStrikePremium = GetHedgeOption(_future, _targetOption, futureLong);
                string hedgeOptionsymbolType = futureLong ? "P" : "C";

                string hedgeOptionSymbol = $"{hedgeOptionsymbolType}-BTC-{hedgeOptionStrikePremium.Value.Key}-{_nextExpiry.ToString("ddMMyy")}";

                _hedgeOption.CurrentPremium = hedgeOptionStrikePremium.Value.Value;
                //var hedgeOption = ZObjects.deltaExchange.GetProductBySymbolAsync(hedgeOptionSymbol, _httpClientFactory.CreateClient());

                order = CryptoOrders.PlaceDEOrder(_algoInstance, hedgeOptionSymbol, _futureQty, productId: 0, true,
                    algoIndex, _user, limitPrice: hedgeOptionStrikePremium.Value.Value, 
                    timeStamp: DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                //cdl.UpdateOrder(_algoInstance, order);

                //var hedgeOptionPositionTask = ZObjects.deltaExchange.GetCurrentPosition(hedgeOption.Id, null, client: _httpClientFactory.CreateClient());
                //hedgeOptionPositionTask.Wait();
                //var hedgeOptionPosition = hedgeOptionPositionTask.Result;


                //_hedgeOption.EntryPremium = Convert.ToDecimal(hedgeOptionPosition.EntryPrice);

                _hedgeOption.Symbol = order.ProductSymbol;
                _hedgeOption.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
                _hedgeOption.EntryPremium = Convert.ToDecimal(order.AverageFilledPrice);
                _hedgeOption.Size = _futureQty;
                _hedgeOption.Side = order.Side;
                _hedgeOption.Strike = hedgeOptionStrikePremium.Value.Key;
                _hedgeOption.Expiry = _nextExpiry;
                _hedgeOption.PaidCommission = Convert.ToDecimal(order.PaidCommission);

                _entryDate = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime.Date;
            }
        }

        private bool CheckTargetOptionAvailability(bool futureLong, decimal futurePrice)
        {
            if (futureLong)
            {
                var higherCalls = callOptions.Where(x => x.Key >= futurePrice + TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value <= TARGET_OPTION_MAX_PREMIUM);

                return higherCalls.Count() > 3;

            }
            else
            {
                //target option will be put
                //As far as possible with minimum profitability

                var lowerputs = putOptions.Where(x => x.Key <= futurePrice - TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value <= TARGET_OPTION_MAX_PREMIUM);

                return lowerputs.Count() > 3;
            }
        }
        private bool CheckHedgeOptionAvailability(bool futureLong, decimal futurePrice)
        {
            if (futureLong)
            {
                var lowerPuts = putOptions.Where(x => x.Key < futurePrice);
                if (lowerPuts.Count() < 5)
                {
                    return false;
                }
            }
            else
            {
                var higherCalls = callOptions.Where(x => x.Key > futurePrice);
                if (higherCalls.Count() < 5)
                {
                    return false;
                }
            }
            return true;
        }

        private KeyValuePair<decimal, decimal>? GetTargetOption(TradedCryptoFuture future, bool futureLong)
        {

            if (futureLong)
            {
                //target option will be call
                //As far as possible with minimum profitability

                if(callOptions.Any(x => x.Key >= future.CurrentPrice + TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value <= TARGET_OPTION_MAX_PREMIUM))
                {
                    var higherCalls = callOptions.Where(x => x.Key >= future.CurrentPrice + TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value <= TARGET_OPTION_MAX_PREMIUM);
                    return higherCalls.OrderBy(x => x.Key).First();
                }
            }
            else
            {
                //target option will be put
                //As far as possible with minimum profitability

                if (putOptions.Any(x => x.Key <= future.CurrentPrice - TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value <= TARGET_OPTION_MAX_PREMIUM))
                {
                    var lowerputs = putOptions.Where(x => x.Key <= future.CurrentPrice - TARGET_MINIMUM_DISTANCE_FROM_FUTURE && x.Value <= TARGET_OPTION_MAX_PREMIUM);
                    return lowerputs.OrderByDescending(x => x.Key).First();
                }
            }
            return null;
        }


        private KeyValuePair<decimal,decimal>? GetHedgeOption(TradedCryptoFuture future, TradedCryptoOption tradedOption, bool futureLong)
        {
            decimal premiumReceivedFromTargetOption = tradedOption.Size * tradedOption.CurrentPremium;// EntryPremium;

            if (futureLong)
            {
                //hedge will be put options
                //As far as possible with minimum profitability

                var lowerPuts = putOptions.Where(x=>x.Key <= future.CurrentPrice);
                if(lowerPuts.Count() == 0)
                {
                    return null;
                }
                KeyValuePair<decimal, decimal> highestStrikeOption = lowerPuts.OrderByDescending(x => x.Key).First();
                foreach(var option in lowerPuts)
                {
                    if(((premiumReceivedFromTargetOption - option.Value * future.Size) - (future.EntryPrice - option.Key) * future.Size) * 85 > MINIMUM_PROFIT_HEDGE_SIDE)
                    {
                        highestStrikeOption = highestStrikeOption.Key > option.Value ? option : highestStrikeOption;
                    }
                }
                return highestStrikeOption;
            }
            else
            {
                //hedge will be call options
                //As far as possible with minimum profitability

                var higherCalls = callOptions.Where(x => x.Key > future.CurrentPrice);

                if (higherCalls.Count() == 0)
                {
                    return null;
                }

                KeyValuePair<decimal, decimal> lowestStrikeOption = higherCalls.OrderBy(x => x.Key).First(); 

                foreach (var option in higherCalls)
                {
                    if (((premiumReceivedFromTargetOption - option.Value * future.Size) - (option.Key - future.EntryPrice) * future.Size) * 85 > MINIMUM_PROFIT_HEDGE_SIDE)
                    {
                        lowestStrikeOption = lowestStrikeOption.Key < option.Value ? option : lowestStrikeOption;
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
