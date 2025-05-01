using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using BrokerConnectWrapper;
//using Pub_Sub;
//using MarketDataTest;
using System.Data;
using System.Diagnostics;
using ZMQFacade;
using System.Timers;
using GlobalCore;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using static System.Net.Mime.MediaTypeNames;
using System.IO;
//using InfluxDB.Client.Api.Domain;
using System.Security.Cryptography;
//using static Google.Protobuf.Collections.MapField<TKey, TValue>;

namespace Algorithms.Algorithms
{
    public class ActiveBuyStrangleManagerWithVariableQty : IZMQ// IObserver<Tick[]>
    {
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ActiveBuyStrangleManagerWithVariableQty source);
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

        private Instrument _activeCall;
        private Instrument _activePut;
        public const AlgoIndex algoIndex = AlgoIndex.ActiveTradeWithVariableQty;
        private bool _straddleShift;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private decimal _profitPoints;
        private int multiplier = 1;
        private InstrumentLinkedList _ceInstrumentLinkedList, _peInstrumentLinkedList;
        SortedList<Decimal, Instrument> _calls, _puts;
        Dictionary<uint, Instrument> _callRepository, _putRepository;
        private DateTime? _expiryDate;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        //public Dictionary<uint, uint> MappedTokens { get; set; }
        private bool _stopTrade;
        public List<uint> SubscriptionTokens;
        private int _initialQty;
        private int _stepQty;
        private int _maxQty;
        private User _user;
        private int _minQty = 50;
        private decimal _pnl = 0;
        private decimal _minPnl = 0;
        private bool nextRound = false;
        private double _entryDelta;
        private double _minDelta;
        private double _maxDelta;
        private int _algoInstance;
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        private int _lotSize = 15;
        private StrangleNode _activeStrangle;
        IHttpClientFactory _httpClientFactory;
        string _userId;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(10);
        private readonly SemaphoreSlim linerSemaphone = new SemaphoreSlim(1);
        private int _individualLegMaxTrades = 0;
        //private int individualLegTradeCounter = 0;
        private int callLegTradeCounter = 0;
        private int putLegTradeCounter = 0;
        //int ccs = 1, pcs = 1, ccb = 1, pcb = 1;
        int cc = 1, pc = 1;
        decimal _referenceStrike;
        decimal _referenceCEPrice = 0;
        decimal _referencePEPrice = 0;

        //Dictionary<int, StrangleNode> ActiveStrangles = new Dictionary<int, StrangleNode>();

        public ActiveBuyStrangleManagerWithVariableQty()
        {
//            LoadActiveData();
        }
        public ActiveBuyStrangleManagerWithVariableQty(uint baseInstrumentToken, DateTime? expiry, int initialQty, int stepQty, int maxQty, decimal targetProfit, decimal stopLoss, decimal entryDelta,
            decimal maxDelta, decimal minDelta, AlgoIndex algoIndex, IHttpClientFactory httpClientFactory = null, int algoInstance = 0, string userid = "")
        {
            //LoadActiveStrangles();
            _userId = userid;
            _httpClientFactory = httpClientFactory;
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            SubscriptionTokens = new List<uint>();

            _initialQty = initialQty * _lotSize;
            _minQty = _initialQty;
            _stepQty = stepQty * _lotSize;
            _maxQty = maxQty * _lotSize;
            _individualLegMaxTrades = 3;
            _maxDelta = (double)maxDelta;
            _entryDelta = (double)entryDelta;
            _minDelta = (double)minDelta;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now),
                expiry.GetValueOrDefault(DateTime.Now), _initialQty, _maxQty, _stepQty, (decimal)_maxDelta, 0, (decimal)_minDelta, 0, 0,
                0, 0, 0, 0, _targetProfit, _stopLoss, 0, positionSizing: false, maxLossPerTrade: _stopLoss);

            //_optionUniverse = new List<Instrument>();
            _ceInstrumentLinkedList = new InstrumentLinkedList(null)
            {
                InstrumentType = "ce",
                UpperDelta = _maxDelta,
                LowerDelta = _minDelta,
                BaseInstrumentToken = _baseInstrumentToken,
                Current = null,
                CurrentInstrumentIndex = 0,
                StopLossPoints = (double)_stopLoss
            };
            _peInstrumentLinkedList = new InstrumentLinkedList(null)
            {
                InstrumentType = "pe",
                UpperDelta = _maxDelta,
                LowerDelta = _minDelta,
                BaseInstrumentToken = _baseInstrumentToken,
                Current = null,
                CurrentInstrumentIndex = 0,
                StopLossPoints = (double)_stopLoss
            };

            ////ZConnect.Login();
            //_user = KoConnect.GetUser(userId: "PM27031981");

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();

        }

        private void ReviewStrangle(Tick tick)
        {
            DateTime currentTime = tick.InstrumentToken == _baseInstrumentToken ?
                tick.Timestamp.Value : tick.LastTradeTime.Value;

            //if(currentTime.TimeOfDay <= new TimeSpan(9, 19, 0))
            //{
            //    return;
            //}
            try
            {

                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }

                    //if (_activeStrangle == null)
                    //{
                    //    _activeStrangle = GetNewStrikes(_baseInstrumentToken, _baseInstrumentPrice, _expiryDate);
                    //    UpdateInstrumentSubscription(currentTime, _activeStrangle.Call, _activeStrangle.Put);
                    //}
                    LoadOptions();
                    UpdateInstrumentSubscription(currentTime);
                    UpdateOptionPrice(tick);


                    StrangleNode atmStrangle = GetActiveStrangle();
                    if(_activeStrangle == null)
                    {
                        _activeStrangle = atmStrangle;
                        return;
                    }
                    if (tick.InstrumentToken == _activeStrangle.BaseInstrumentToken)
                    {
                        _activeStrangle.BaseInstrumentPrice = tick.LastPrice;
                    }

                    Instrument callOption = _activeStrangle.Call;
                    Instrument putOption = _activeStrangle.Put;

                    if (_activeStrangle.CurrentPosition == PositionStatus.NotTraded && ((_activeStrangle.CallTradedQty == 0 && _activeStrangle.PutTradedQty == 0) || nextRound))
                    {
                        _activeStrangle.CurrentPosition = PositionStatus.NotTraded;
                        if (_activeStrangle.CallTradedQty == 0 && _activeStrangle.PutTradedQty == 0)
                        {
                            if ((callOption.LastPrice > putOption.LastPrice && callOption.LastPrice / putOption.LastPrice > 1.2m)
                                || (putOption.LastPrice > callOption.LastPrice && putOption.LastPrice / callOption.LastPrice > 1.2m))
                            {
                                _activeStrangle.CurrentPosition = PositionStatus.Open;
                                //return;
                            }
                        }

                        nextRound = false;
                    }

                        //Continue trading if position is open
                        if (_activeStrangle.CurrentPosition == PositionStatus.Open)
                    {
                        //if(atmStrangle != null && atmStrangle.Call.Strike != callOption.Strike)
                        //{
                        //    //Close activestrangle and start a new atm
                        //    TriggerEODPositionClose(currentTime, true, false);
                        //    return;

                        //}
                        //if ((_activeStrangle.CallOrders.Count == 0 && _activeStrangle.PutOrders.Count == 0) || nextRound)
                        if ((_activeStrangle.CallTradedQty == 0 && _activeStrangle.PutTradedQty == 0) || nextRound)
                        {
                            //_activeStrangle.CurrentPosition = PositionStatus.NotTraded;
                            //if (_activeStrangle.CallTradedQty == 0 && _activeStrangle.PutTradedQty == 0)
                            //{
                            //    if ((callOption.LastPrice > putOption.LastPrice && callOption.LastPrice / putOption.LastPrice > 1.2m)
                            //        || (putOption.LastPrice > callOption.LastPrice && putOption.LastPrice / callOption.LastPrice > 1.2m))
                            //    {
                            //        _activeStrangle.CurrentPosition = PositionStatus.Open;
                            //        return;
                            //    }
                            //}

                            nextRound = false;
                            //_activeStrangle.NetPnL = 0;
                            if (callOption.LastPrice > putOption.LastPrice)
                            {
                                int qty = Convert.ToInt32(Math.Round((callOption.LastPrice * _activeStrangle.InitialQty) / putOption.LastPrice / putOption.LotSize, 0) * putOption.LotSize);

                                _referenceStrike = _baseInstrumentPrice;
                                TradeEntry(callOption, _activeStrangle.CallOrders, _activeStrangle.InitialQty, currentTime);
                                _activeStrangle.CallTradedQty += _activeStrangle.InitialQty;

                                //PostOrderInKotak(callOption, currentTime, callOption.Strike, _activeStrangle.InitialQty, true, true);

                                TradeEntry(putOption, _activeStrangle.PutOrders, qty, currentTime);
                                _activeStrangle.PutTradedQty += qty;
                                //PostOrderInKotak(putOption, currentTime, putOption.Strike, qty, true, true);
                                _referenceCEPrice = callOption.LastPrice;
                                _referencePEPrice = putOption.LastPrice;

                            }
                            else
                            {
                                TradeEntry(putOption, _activeStrangle.PutOrders, _activeStrangle.InitialQty, currentTime);
                                //PostOrderInKotak(putOption, currentTime, putOption.Strike, _activeStrangle.InitialQty, true, true);
                                _activeStrangle.PutTradedQty += _activeStrangle.InitialQty;

                                int qty = Convert.ToInt32(Math.Round((putOption.LastPrice * _activeStrangle.InitialQty) / callOption.LastPrice / putOption.LotSize, 0) * putOption.LotSize);

                                TradeEntry(callOption, _activeStrangle.CallOrders, qty, currentTime);
                                _activeStrangle.CallTradedQty += qty;

                                _referenceStrike = _baseInstrumentPrice;
                                _referenceCEPrice = callOption.LastPrice;
                                _referencePEPrice = putOption.LastPrice;
                                //PostOrderInKotak(callOption, currentTime, callOption.Strike, qty, true, true);
                            }
                            //if (_activeStrangle.CallOrders.Count == 0)
                            //{
                            //    _activeStrangle.CallTradedQty = TradeEntry(callOption, _activeStrangle.CallOrders, _activeStrangle.InitialQty, currentTime);
                            //    _profitPoints = Math.Abs((_activeStrangle.CallOrders[0].AveragePrice) * 0.1m);
                            //}
                            //if (_activeStrangle.PutOrders.Count == 0)
                            //{
                            //    _activeStrangle.PutTradedQty = TradeEntry(putOption, _activeStrangle.PutOrders, _activeStrangle.InitialQty, currentTime);
                            //    _profitPoints += Math.Abs((_activeStrangle.PutOrders[0].AveragePrice) * 0.1m);
                            //    _profitPoints = Math.Max(10, _profitPoints);
                            //}

                            _activeStrangle.CurrentPosition = PositionStatus.Open;
                        }
                        else if (_activeStrangle.CallTradedQty != 0 && _activeStrangle.PutTradedQty != 0)
                        {
                            if (_referenceCEPrice > _activeStrangle.Call.LastPrice + 15 && _activeStrangle.CallTradedQty <= _maxQty)// && _activeStrangle.PutTradedQty > _minQty)
                            {
                                _referenceCEPrice = _activeStrangle.Call.LastPrice;

                                Order corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "ce", _activeStrangle.Call.LastPrice,
                                    callOption.KToken, true, _stepQty, algoIndex, currentTime, product: Constants.KPRODUCT_NRML,
                                    broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);




                                OnTradeExit(corder);

                                _activeStrangle.NetPnL -= corder.AveragePrice * _stepQty;

                                _activeStrangle.CallTradedQty += _stepQty;

                                _referenceCEPrice = corder.AveragePrice;
                                _activeStrangle.CallOrders.Add(corder);
                            }
                            else
                            {
                                //for (int i = 0; i < _activeStrangle.CallOrders.Count; i++)
                                //{
                                //    Order order = _activeStrangle.CallOrders[i];

                                //    if (order.TransactionType == "buy" && _activeStrangle.Call.LastPrice > order.AveragePrice + 10 && _activeStrangle.Call.LastPrice > _referenceCEPrice + 10)// && _activeStrangle.CallTradedQty >= _minQty)
                                //    {
                                //        Order corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "ce", _activeStrangle.Call.LastPrice,
                                //           callOption.KToken, false, _stepQty, algoIndex, currentTime, product: Constants.KPRODUCT_NRML,
                                //           broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                //        OnTradeExit(corder);
                                //        _referenceCEPrice = corder.AveragePrice;

                                //        _activeStrangle.NetPnL += corder.AveragePrice * _stepQty;
                                //        _activeStrangle.CallTradedQty -= _stepQty;

                                //        if (order.Quantity > _stepQty)
                                //        {
                                //            order.Quantity -= _stepQty;
                                //        }
                                //        else
                                //        {
                                //            _activeStrangle.CallOrders.RemoveAt(i);
                                //        }
                                //        //_activeStrangle.PutOrders.RemoveAt(i);
                                //        break;
                                //    }
                                //}
                            }
                            if (_referencePEPrice > _activeStrangle.Put.LastPrice + 15 && _activeStrangle.PutTradedQty <= _maxQty)// && _activeStrangle.CallTradedQty > _minQty)
                            {
                                _referencePEPrice = _activeStrangle.Put.LastPrice;

                                Order porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                                    putOption.KToken, true, _stepQty, algoIndex, currentTime, product: Constants.KPRODUCT_NRML,
                                    broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                                OnTradeExit(porder);

                                _activeStrangle.NetPnL -= porder.AveragePrice * _stepQty;

                                _activeStrangle.PutTradedQty += _stepQty;
                                _referencePEPrice = porder.AveragePrice;
                                _activeStrangle.PutOrders.Add(porder);
                            }
                            else
                            {

                                //for (int i = 0; i < _activeStrangle.PutOrders.Count; i++)
                                //{
                                //    Order order = _activeStrangle.PutOrders[i];

                                //    if (order.TransactionType == "buy" && _activeStrangle.Put.LastPrice > order.AveragePrice + 10 && _activeStrangle.Put.LastPrice > _referencePEPrice + 10)// && _activeStrangle.PutTradedQty > _minQty)
                                //    {
                                //        Order porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                                //            putOption.KToken, false, _stepQty, algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                                //            HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                                //        OnTradeExit(porder);
                                //        _referencePEPrice = porder.AveragePrice;
                                //        _activeStrangle.NetPnL += porder.AveragePrice * _stepQty;
                                //        _activeStrangle.PutTradedQty -= _stepQty;

                                //        if (order.Quantity > _stepQty)
                                //        {
                                //            order.Quantity -= _stepQty;
                                //        }
                                //        else
                                //        {
                                //            _activeStrangle.PutOrders.RemoveAt(i);
                                //        }

                                //        break;
                                //    }
                                //}
                            }


                            //if (_baseInstrumentPrice - _referenceStrike > 50)
                            //{
                            //    _referenceStrike = _baseInstrumentPrice;

                            //    if (_activeStrangle.CallOrders.Count > 1 && _activeStrangle.CallOrders.Last().AveragePrice + 10*cc < _activeStrangle.Call.LastPrice)
                            //    {
                            //        cc++;
                            //        Order corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "ce", _activeStrangle.Call.LastPrice,
                            //           callOption.KToken, false, _stepQty, algoIndex, currentTime,
                            //           broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //        OnTradeExit(corder);

                            //        _activeStrangle.NetPnL += corder.AveragePrice * _stepQty;
                            //        _activeStrangle.CallTradedQty -= _stepQty;

                            //        _activeStrangle.CallOrders.RemoveAt(_activeStrangle.CallOrders.Count - 1);
                            //    }
                            //    else if (_activeStrangle.Put.LastPrice < _activeStrangle.PutOrders.Sum(x => x.AveragePrice * x.Quantity) / _activeStrangle.PutOrders.Sum(x => x.Quantity) - 10*cc
                            //        && _activeStrangle.PutOrders.Sum(x => x.Quantity) < _maxQty)
                            //    {
                            //        cc++;
                            //        Order porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                            //            putOption.KToken, true, _stepQty, algoIndex, currentTime,
                            //            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //        OnTradeExit(porder);

                            //        _activeStrangle.NetPnL -= porder.AveragePrice * _stepQty;
                            //        _activeStrangle.PutTradedQty += _stepQty;

                            //        _activeStrangle.PutOrders.Add(porder);
                            //    }

                            //    //putOption = _puts[putOption.Strike + 100];
                            //    //_activeStrangle.Put = _putRepository[putOption.InstrumentToken];

                            //    //porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                            //    //    putOption.KToken, true, _activeStrangle.PutTradedQty, algoIndex, currentTime, Tag: _activeStrangle.Call.LastPrice.ToString(),
                            //    //    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //    //OnTradeExit(porder);

                            //    //_activeStrangle.NetPnL -= porder.AveragePrice * _activeStrangle.PutTradedQty;


                            //    //Order corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Call.LastPrice,
                            //    //    callOption.KToken, false, _activeStrangle.CallTradedQty, algoIndex, currentTime,
                            //    //    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //    //OnTradeExit(corder);

                            //    //_activeStrangle.NetPnL += corder.AveragePrice * _activeStrangle.CallTradedQty;

                            //    //callOption = _calls[callOption.Strike + 100];
                            //    //_activeStrangle.Call = _callRepository[callOption.InstrumentToken];

                            //    //corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Call.LastPrice,
                            //    //    callOption.KToken, true, _activeStrangle.CallTradedQty, algoIndex, currentTime, Tag: _activeStrangle.Put.LastPrice.ToString(),
                            //    //    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //    //OnTradeExit(corder);

                            //    //_activeStrangle.NetPnL -= corder.AveragePrice * _activeStrangle.CallTradedQty;

                            //}
                            //else if (_referenceStrike - _baseInstrumentPrice > 50 
                            //    )
                            //{
                            //    _referenceStrike = _baseInstrumentPrice;

                            //    if (_activeStrangle.PutOrders.Count > 1 && _activeStrangle.PutOrders.Last().AveragePrice + 10*pc < _activeStrangle.Put.LastPrice)
                            //    {
                            //        pc++;
                            //        Order porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                            //       putOption.KToken, false, _stepQty, algoIndex, currentTime,
                            //       broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //        OnTradeExit(porder);

                            //        _activeStrangle.NetPnL += porder.AveragePrice * _stepQty;
                            //        _activeStrangle.PutTradedQty -= _stepQty;

                            //        _activeStrangle.PutOrders.RemoveAt(_activeStrangle.PutOrders.Count - 1);
                            //    }
                            //    else if (_activeStrangle.Call.LastPrice < _activeStrangle.CallOrders.Sum(x => x.AveragePrice * x.Quantity) / _activeStrangle.CallOrders.Sum(x => x.Quantity) - 10*pc
                            //        && _activeStrangle.CallOrders.Sum(x => x.Quantity) < _maxQty)
                            //    {
                            //        pc++;
                            //        Order corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "ce", _activeStrangle.Call.LastPrice,
                            //            callOption.KToken, true, _stepQty, algoIndex, currentTime,
                            //            broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //        OnTradeExit(corder);

                            //        _activeStrangle.NetPnL -= corder.AveragePrice * _stepQty;

                            //        _activeStrangle.CallTradedQty += _stepQty;

                            //        _activeStrangle.CallOrders.Add(corder);
                            //    }
                                
                            //    //callOption = _calls[callOption.Strike - 100];
                            //    //_activeStrangle.Call = _callRepository[callOption.InstrumentToken];

                            //    //corder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Call.LastPrice,
                            //    //    callOption.KToken, true, _activeStrangle.CallTradedQty, algoIndex, currentTime, Tag: _activeStrangle.Put.LastPrice.ToString(),
                            //    //    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //    //OnTradeExit(corder);

                            //    //_activeStrangle.NetPnL -= corder.AveragePrice * _activeStrangle.CallTradedQty;


                            //    //Order porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                            //    //   putOption.KToken, false, _activeStrangle.PutTradedQty, algoIndex, currentTime,
                            //    //   broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //    //OnTradeExit(porder);

                            //    //_activeStrangle.NetPnL += porder.AveragePrice * _activeStrangle.PutTradedQty;

                            //    //putOption = _puts[putOption.Strike - 100];
                            //    //_activeStrangle.Put = _putRepository[putOption.InstrumentToken];

                            //    //porder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                            //    //    putOption.KToken, true, _activeStrangle.PutTradedQty, algoIndex, currentTime, Tag: _activeStrangle.Call.LastPrice.ToString(),
                            //    //    broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

                            //    //OnTradeExit(porder);

                            //    //_activeStrangle.NetPnL -= porder.AveragePrice * _activeStrangle.PutTradedQty;

                            //}
                        }

                        List<Order> callOrders = _activeStrangle.CallOrders;
                        List<Order> putOrders = _activeStrangle.PutOrders;


                        //decimal pnl = _activeStrangle.NetPnL + _activeStrangle.Put.LastPrice*_activeStrangle.PutTradedQty + _activeStrangle.Call.LastPrice * _activeStrangle.CallTradedQty;
                        //sell traded qty
#if market
                        decimal pnl = _activeStrangle.NetPnL + _activeStrangle.Put.Bids[1].Price * _activeStrangle.PutTradedQty + _activeStrangle.Call.Bids[1].Price * _activeStrangle.CallTradedQty;
#elif local
                        decimal pnl = _activeStrangle.NetPnL + _activeStrangle.Put.LastPrice * _activeStrangle.PutTradedQty + _activeStrangle.Call.LastPrice * _activeStrangle.CallTradedQty;
#endif
                        DataLogic dl = new DataLogic();

                        //decimal profitPoints = Math.Max(Math.Abs((callOrders[0].AveragePrice + putOrders[0].AveragePrice) * 0.1m), 10);
                        _minPnl = Math.Min(_minPnl, pnl);
                        if (pnl > 90000 || pnl < -2000)
                        {
                            Order order = MarketOrders.PlaceOrder(_algoInstance, _activeStrangle.Call.TradingSymbol.Trim(' '), "ce", _activeStrangle.Call.LastPrice,
                   _activeStrangle.Call.KToken, false, _activeStrangle.CallTradedQty, algoIndex, currentTime, Tag: "", product: Constants.KPRODUCT_NRML, 
                   broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);
                            //_activeStrangle.NetPnL = dl.UpdateOrder(_algoInstance, order);
                            //_activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.CallTradedQty;

                            //PostOrderInKotak(callOption, currentTime, callOption.Strike, _activeStrangle.CallTradedQty, true, false);

                            OnTradeExit(order);
                            //trade = PlaceOrder(putOption.TradingSymbol.Trim(' '), false, _activeStrangle.Put.Bids[1].Price, _activeStrangle.PutTradedQty);
                            //_activeStrangle.NetPnL = dl.UpdateTrade(_activeStrangle.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

                            order = MarketOrders.PlaceOrder(_algoInstance, _activeStrangle.Put.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                                _activeStrangle.Put.KToken, false, _activeStrangle.PutTradedQty, algoIndex, currentTime, Tag: "", product: Constants.KPRODUCT_NRML, 
                                broker: Constants.KOTAK, HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                            OnTradeExit(order);
                            //_activeStrangle.NetPnL = dl.UpdateOrder(_algoInstance, order);

                            //_activeStrangle.NetPnL = 0;

                            //_activeStrangle.CallTradedQty = 0;
                            //_activeStrangle.PutTradedQty = 0;
                            //_activeStrangle.CallOrders.Clear();
                            //_activeStrangle.PutOrders.Clear();
                            _activeStrangle = null;
                            cc = 1;
                            pc = 1;
                            cc = 1;
                            pc = 1;
                            _pnl += pnl;

                            //if (_pnl > 15000 || _pnl < -4000)
                            //{
                                _stopTrade = true;
                                dl = new DataLogic();
                                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                                dl.DeActivateAlgo(_algoInstance);
                           // }
                        }

                        //Console.WriteLine(_algoInstance + "  "  +  pnl + "           " + _pnl);
                       
                    }

                    //Closes all postions at 3:20 PM
                    TriggerEODPositionClose(currentTime);

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

        private void UpdateOptionPrice(Tick tick)
        {
            if (_callRepository.ContainsKey(tick.InstrumentToken))
            {
                _callRepository[tick.InstrumentToken].LastPrice = tick.LastPrice;
                _callRepository[tick.InstrumentToken].Bids = tick.Bids;
                _callRepository[tick.InstrumentToken].Offers = tick.Offers;
            }
            else if (_putRepository.ContainsKey(tick.InstrumentToken))
            {
                _putRepository[tick.InstrumentToken].LastPrice = tick.LastPrice;
                _putRepository[tick.InstrumentToken].Bids = tick.Bids;
                _putRepository[tick.InstrumentToken].Offers = tick.Offers;
            }
        }
        private void TriggerEODPositionClose(DateTime currentTime, bool closeAll = false, bool stopTrade = true)
        {
            if (currentTime.TimeOfDay >= new TimeSpan(15, 10, 00) || closeAll)
            {
                _stopTrade = stopTrade;

                Order order = MarketOrders.PlaceOrder(_algoInstance, _activeStrangle.Call.TradingSymbol.Trim(' '), "ce", _activeStrangle.Call.LastPrice,
                    _activeStrangle.Call.KToken, false, _activeStrangle.CallTradedQty, algoIndex, currentTime, Tag: _minPnl.ToString(), product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                    HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);


                //_activeStrangle.NetPnL = dl.UpdateOrder(_algoInstance, order);
                _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.CallTradedQty;

                //PostOrderInKotak(callOption, currentTime, callOption.Strike, _activeStrangle.CallTradedQty, true, false);

                OnTradeExit(order);
                //trade = PlaceOrder(putOption.TradingSymbol.Trim(' '), false, _activeStrangle.Put.Bids[1].Price, _activeStrangle.PutTradedQty);
                //_activeStrangle.NetPnL = dl.UpdateTrade(_activeStrangle.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

                order = MarketOrders.PlaceOrder(_algoInstance, _activeStrangle.Put.TradingSymbol.Trim(' '), "pe", _activeStrangle.Put.LastPrice,
                    _activeStrangle.Put.KToken, false, _activeStrangle.PutTradedQty, algoIndex, currentTime, Tag: _minPnl.ToString(), product: Constants.KPRODUCT_NRML, broker: Constants.KOTAK,
                    HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);

                _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.PutTradedQty;
                
                OnTradeExit(order);
                //_activeStrangle.NetPnL = dl.UpdateOrder(_algoInstance, order);

                //PostOrderInKotak(putOption, currentTime, putOption.Strike, _activeStrangle.PutTradedQty, true, false);

                _activeStrangle.CurrentPosition = PositionStatus.Closed;

                _pnl += _activeStrangle.NetPnL;

                DataLogic dl = new DataLogic();

                dl.UpdateAlgoPnl(_algoInstance, _pnl);
                dl.DeActivateAlgo(_algoInstance);
                _minPnl = 0;
                _activeStrangle = null;
                multiplier = 1;
                nextRound = false;

            }
        }

        private int TradeEntry(Instrument option, List<Order> orders, int quantity, DateTime? currentTime)
        {
            //place order to buy Puts
            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol.Trim(' ').Trim(' '), option.InstrumentType, option.LastPrice, 
                option.KToken, true, quantity, algoIndex, currentTime, product: Constants.KPRODUCT_NRML, broker:Constants.KOTAK,
                HsServerId: _user.HsServerId, httpClient: _httpClientFactory == null ? null : KoConnect.ConfigureHttpClient(_user, _httpClientFactory.CreateClient()), user: _user);




            OnTradeEntry(order);
            //increase trade record and quantity
            orders.Add(order);

            // _activeStrangle.PutOrders = orders; //This may not be required. Just check
            DataLogic dl = new DataLogic();
            _activeStrangle.NetPnL += order.AveragePrice * quantity * -1;  //dl.UpdateOrder(_activeStrangle.ID, order);
            
            //return orders.Sum(x => x.Quantity);
            return orders.Sum(x => (x.TransactionType.ToLower() == "buy" ? 1 : -1) * x.Quantity);
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

        private StrangleNode GetActiveStrangle()
        {
            foreach (var strike in _calls.Keys)
            {
                if (strike < _baseInstrumentPrice + 200 && strike > _baseInstrumentPrice - 200)
                {
                    Instrument callOption = _calls[strike];
                    Instrument putOption = _puts[strike];
                    if ((callOption.LastPrice * putOption.LastPrice != 0) && ((callOption.LastPrice > putOption.LastPrice && callOption.LastPrice / putOption.LastPrice < 1.2m)
                                                     || (putOption.LastPrice > callOption.LastPrice && putOption.LastPrice / callOption.LastPrice < 1.2m)))
                    {
                        StrangleNode activeStrangle = new StrangleNode(callOption, putOption);
                        activeStrangle.ID = _algoInstance;
                        activeStrangle.BaseInstrumentToken = _baseInstrumentToken;
                        activeStrangle.BaseInstrumentPrice = _baseInstrumentPrice;
                        activeStrangle.CurrentPosition = PositionStatus.Open;
                        activeStrangle.InitialQty = _initialQty;
                        activeStrangle.StepQty = _stepQty;
                        activeStrangle.MaxQty = _maxQty;
                        activeStrangle.CurrentPosition = PositionStatus.NotTraded;
                        return activeStrangle;
                    }
                }
            }
            return null;
        }
        private void LoadOptions()
        {
            DataLogic dl = new DataLogic();

            if (_calls == null || _puts == null || Math.Abs(_referenceStrike - _calls.Keys.Max()) < 400 || Math.Abs(_referenceStrike - _puts.Keys.Max()) < 400)
            {
                SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(_baseInstrumentToken, _expiryDate.GetValueOrDefault(DateTime.Now),
                    _baseInstrumentPrice, _baseInstrumentPrice, 0);

                _calls = nodeData[0];
                _puts = nodeData[1];

                _callRepository = new Dictionary<uint, Instrument>();
                foreach (var option in _calls.Values)
                {
                    option.KToken = option.InstrumentToken;
                    _callRepository.Add(option.InstrumentToken, option);
                }
                _putRepository = new Dictionary<uint, Instrument>();
                foreach (var option in _puts.Values)
                {
                    option.KToken = option.InstrumentToken;
                    _putRepository.Add(option.InstrumentToken, option);
                }
            }

            //var call = _calls.Where(x => x.Key > _baseInstrumentPrice).FirstOrDefault().Value;
            //var put = _puts.Where(x => x.Key < _baseInstrumentPrice).LastOrDefault().Value;

            //if (call == null || put == null)
            //{
            //    SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now),
            //       baseInstrumentPrice, baseInstrumentPrice, 0);

            //    _calls = nodeData[0];
            //    _puts = nodeData[1];

            //    _callRepository = new Dictionary<uint, Instrument>();
            //    foreach (var option in _calls.Values)
            //    {
            //        option.KToken = option.InstrumentToken;
            //        _callRepository.Add(option.InstrumentToken, option);
            //    }
            //    _putRepository = new Dictionary<uint, Instrument>();
            //    foreach (var option in _puts.Values)
            //    {
            //        option.KToken = option.InstrumentToken;
            //        _putRepository.Add(option.InstrumentToken, option);
            //    }

            //    call = _calls.Where(x => x.Key > baseInstrumentPrice).FirstOrDefault().Value;
            //    put = _puts.Where(x => x.Key < baseInstrumentPrice).LastOrDefault().Value;
            //}

            //call.LastPrice = 0;
            //put.LastPrice = 0;
            //StrangleNode activeStrangle = new StrangleNode(call, put);
            //activeStrangle.ID = _algoInstance;
            //activeStrangle.BaseInstrumentToken = _baseInstrumentToken;
            //activeStrangle.BaseInstrumentPrice = _baseInstrumentPrice;
            //activeStrangle.CurrentPosition = PositionStatus.Open;
            //activeStrangle.InitialQty = _initialQty;
            //activeStrangle.StepQty = _stepQty;
            //activeStrangle.MaxQty = _maxQty;
            //return activeStrangle;
        }
        public StrangleNode GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry)
        {
            DataLogic dl = new DataLogic();

            if (_calls == null || _puts == null)
            {
                SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now),
                    baseInstrumentPrice, baseInstrumentPrice, 0);

                _calls = nodeData[0];
                _puts = nodeData[1];

                _callRepository = new Dictionary<uint, Instrument>();
                foreach (var option in _calls.Values)
                {
                    option.KToken = option.InstrumentToken;
                    _callRepository.Add(option.InstrumentToken, option);
                }
                _putRepository = new Dictionary<uint, Instrument>();
                foreach (var option in _puts.Values)
                {
                    option.KToken = option.InstrumentToken;
                    _putRepository.Add(option.InstrumentToken, option);
                }
            }

            var call = _calls.Where(x => x.Key > baseInstrumentPrice).FirstOrDefault().Value;
            var put = _puts.Where(x => x.Key < baseInstrumentPrice).LastOrDefault().Value;

            if(call == null || put == null)
            {
                SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now),
                   baseInstrumentPrice, baseInstrumentPrice, 0);

                _calls = nodeData[0];
                _puts = nodeData[1];

                _callRepository = new Dictionary<uint, Instrument>();
                foreach (var option in _calls.Values)
                {
                    option.KToken = option.InstrumentToken;
                    _callRepository.Add(option.InstrumentToken, option);
                }
                _putRepository = new Dictionary<uint, Instrument>();
                foreach (var option in _puts.Values)
                {
                    option.KToken = option.InstrumentToken;
                    _putRepository.Add(option.InstrumentToken, option);
                }

                call = _calls.Where(x => x.Key > baseInstrumentPrice).FirstOrDefault().Value;
                put = _puts.Where(x => x.Key < baseInstrumentPrice).LastOrDefault().Value;
            }

            call.LastPrice = 0;
            put.LastPrice = 0;
            StrangleNode activeStrangle = new StrangleNode(call, put);
            activeStrangle.ID = _algoInstance;
            activeStrangle.BaseInstrumentToken = _baseInstrumentToken;
            activeStrangle.BaseInstrumentPrice = _baseInstrumentPrice;
            activeStrangle.CurrentPosition = PositionStatus.Open;
            activeStrangle.InitialQty = _initialQty;
            activeStrangle.StepQty = _stepQty;
            activeStrangle.MaxQty = _maxQty;
            return activeStrangle;
        }

        private void UpdateInstrumentSubscription(DateTime currentTime, Instrument call, Instrument put)
        {
            try
            {
                bool dataUpdated = false;
                if (!SubscriptionTokens.Contains(call.InstrumentToken))
                {
                    SubscriptionTokens.Add(call.InstrumentToken);
                    dataUpdated = true;
                }
                if (!SubscriptionTokens.Contains(put.InstrumentToken))
                {
                    SubscriptionTokens.Add(put.InstrumentToken);
                    dataUpdated = true;
                }
                if (dataUpdated)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
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

        private void UpdateInstrumentSubscription(DateTime currentTime)
        {
            try
            {
                bool dataUpdated = false;
                foreach(var optionToken in _callRepository.Keys)
                {
                    if (!SubscriptionTokens.Contains(optionToken))
                    {
                        SubscriptionTokens.Add(optionToken);
                        dataUpdated = true;
                    }
                }
                foreach (var optionToken in _putRepository.Keys)
                {
                    if (!SubscriptionTokens.Contains(optionToken))
                    {
                        SubscriptionTokens.Add(optionToken);
                        dataUpdated = true;
                    }
                }
                if (dataUpdated)
                {
                    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                    Task task = Task.Run(() => OnOptionUniverseChange(this));
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
            httpContentBuilder.Append(option.KToken);
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
                                              orderTask = GetKotakOrder(Convert.ToString(data["orderId"]), _algoInstance, algoIndex, Constants.KORDER_STATUS_TRADED, instrument.TradingSymbol.Trim(' '));
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
                                                  _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.CallTradedQty;
                                              }
                                              else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                                              {
                                                  _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.PutTradedQty;
                                              }
                                          }
                                          else
                                          {
                                              if (instrument.InstrumentType.Trim(' ').ToLower() == "ce")
                                              {
                                                  //increase trade record and quantity
                                                  _activeStrangle.CallOrders.Add(order);
                                                  //_activeStrangle.CallTradedQty = _activeStrangle.CallOrders.Sum(x => (x.TransactionType.ToLower() == "buy" ? 1 : -1) * x.Quantity);
                                                  _activeStrangle.CallTradedQty -= order.Quantity;

                                                  DataLogic dl = new DataLogic();
                                                  _activeStrangle.NetPnL += order.AveragePrice * order.Quantity * -1;  //dl.UpdateOrder(_activeStrangle.ID, order);

                                                  //return _activeStrangle.CallOrders.Sum(x => x.Quantity);
                                              
                                              }
                                              else if (instrument.InstrumentType.Trim(' ').ToLower() == "pe")
                                              {
                                                  //increase trade record and quantity
                                                  _activeStrangle.PutOrders.Add(order);
                                                  //_activeStrangle.PutTradedQty = _activeStrangle.PutOrders.Sum(x => (x.TransactionType.ToLower() == "buy" ? 1 : -1) * x.Quantity);
                                                  _activeStrangle.PutTradedQty -= order.Quantity;

                                                  DataLogic dl = new DataLogic();
                                                  _activeStrangle.NetPnL += order.AveragePrice * order.Quantity * -1;  //dl.UpdateOrder(_activeStrangle.ID, order);

                                                 // return _activeStrangle.PutOrders.Sum(x => x.Quantity);
                                              }
                                          }

                                          MarketOrders.UpdateOrderDetails(_algoInstance, algoIndex, order);
                                          //_optionStrikeCovered[optionStrike] = false;

                                      }
                                      else if (orderStatus != null && orderStatus.ContainsKey("fault") && orderStatus["fault"] != null)
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
        
        
        public void OnNext(Tick tick)
        {
            try
            {
                lock (tradeLock)
                {
                    if (_stopTrade || !tick.Timestamp.HasValue)
                    {
                        return;
                    }
                    ReviewStrangle(tick);
                    //ActiveTradeIntraday(ticks[0]);
                }
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

            //lock (ActiveStrangles)
            //{
            //    for (int i = 0; i < ActiveStrangles.Count; i++)
            //    {
            //        ReviewStrangle(ActiveStrangles.ElementAt(i).Value, ticks[0]);

            //        //   foreach (KeyValuePair<int, StrangleNode> keyValuePair in ActiveStrangles)
            //        // {
            //        //  ReviewStrangle(keyValuePair.Value, ticks);
            //        // }
            //    }
            //}
            //return true;
        }

        //private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        //{
        //    OrderTrio orderTrio = null;
        //    try
        //    {
        //        decimal entryRSI = 0;
        //        //ENTRY ORDER - Sell ALERT
        //        Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol.Trim(' '), option.InstrumentType, lastPrice,
        //            option.KToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
        //            algoIndex, currentTime, Constants.ORDER_TYPE_MARKET, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //        if (order.Status == Constants.ORDER_STATUS_REJECTED)
        //        {
        //            _stopTrade = true;
        //            return orderTrio;
        //        }

        //        //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
        //        //   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
        //        //   option.TradingSymbol.Trim(' '), order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");

        //        orderTrio = new OrderTrio();
        //        orderTrio.Order = order;
        //        //orderTrio.SLOrder = slOrder;
        //        orderTrio.Option = option;
        //        orderTrio.EntryTradeTime = currentTime;
        //        OnTradeEntry(order);
        //    }
        //    catch (Exception ex)
        //    {
        //        _stopTrade = true;
        //        Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
        //        Logger.LogWrite("Closing Application");
        //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "TradeEntry");
        //        Thread.Sleep(100);
        //    }
        //    return orderTrio;
        //}

        private ShortTrade PlaceOrder(string tradingSymbol, bool buyOrder, decimal currentPrice, int quantity, DateTime? tickTime=null, uint token=0)
        {
            //Dictionary<string, dynamic> orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, tradingSymbol.TrimEnd(),
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_SELL, quantity, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            ///TEMP, REMOVE Later
            if (currentPrice == 0)
            {
                DataLogic dl = new DataLogic();
                currentPrice = dl.RetrieveLastPrice(token, tickTime, buyOrder);
            }

            string orderId = "0";
            decimal averagePrice = 0;
            //if (orderStatus["data"]["order_id"] != null)
            //{
            //    orderId = orderStatus["data"]["order_id"];
            //}
            if (orderId != "0")
            {
                System.Threading.Thread.Sleep(200);
                List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
                averagePrice = orderInfo[orderInfo.Count - 1].AveragePrice;
            }
            if (averagePrice == 0)
                averagePrice = buyOrder ? currentPrice + 1 : currentPrice - 1;
            averagePrice = buyOrder ? averagePrice * -1 : averagePrice;

            ShortTrade trade = new ShortTrade();
            trade.AveragePrice = averagePrice;
            trade.ExchangeTimestamp = DateTime.Now;
            trade.Quantity = buyOrder ? quantity : quantity*-1;
            trade.OrderId = orderId;
            trade.TransactionType = buyOrder ? "Buy":"Sell";

            return trade;
        }


        public void LoadActiveOrders(List<OrderTrio> activeOrderTrios)
        {
            if (activeOrderTrios != null)
            {
                Instrument call = null , put = null;
                List<Order> callOrders = new List<Order>();
                List<Order> putOrders = new List<Order>();
                DataLogic dl = new DataLogic();
                foreach (OrderTrio orderTrio in activeOrderTrios)
                {
                    Instrument option = dl.GetInstrument(orderTrio.Order.Tradingsymbol);
                    orderTrio.Option = option;
                    if (option.InstrumentType.ToLower() == "ce")
                    {
                        //_callorderTrios.Add(orderTrio);
                        call = option;
                        callOrders.Add(orderTrio.Order);
                    }
                    else if (option.InstrumentType.ToLower() == "pe")
                    {
                        //_putorderTrios.Add(orderTrio);
                        put = option;
                        putOrders.Add(orderTrio.Order);
                    }
                }
                
                StrangleNode strangleNode = new StrangleNode(call, put);
                strangleNode.InitialQty = _initialQty;
                strangleNode.BaseInstrumentToken = call.BaseInstrumentToken;
                strangleNode.PutTradedQty = strangleNode.PutOrders.Sum(x => x.Quantity * (x.TransactionType == "buy" ? 1 : -1));
                strangleNode.CallTradedQty = strangleNode.CallOrders.Sum(x => x.Quantity * (x.TransactionType == "buy" ? 1 : -1));
                strangleNode.CurrentPosition = PositionStatus.Open;
                strangleNode.MaxQty = _maxQty;
                strangleNode.StepQty = _stepQty;
                strangleNode.NetPnL = _pnl;
                strangleNode.ID = _algoInstance;

                _activeStrangle = strangleNode;
            }
        }
        //private void LoadActiveData()
        //{
        //    AlgoIndex algoIndex = AlgoIndex.ActiveTradeWithVariableQty;
        //    DataLogic dl = new DataLogic();
        //    DataSet activeStrangles = dl.RetrieveActiveStrangleData(algoIndex);
        //    DataRelation strategy_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
        //        new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"] });

        //    DataRelation strategy_Trades_Relation = activeStrangles.Relations.Add("Strangle_Trades", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"] },
        //        new DataColumn[] { activeStrangles.Tables[2].Columns["StrategyId"] });

        //    Instrument call, put;

        //    foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
        //    {
        //        DataRow strangleTokenRow = strangleRow.GetChildRows(strategy_Token_Relation)[0];

        //        call = new Instrument()
        //        {
        //            BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //            InstrumentToken = Convert.ToUInt32(strangleTokenRow["CallToken"]),
        //            InstrumentType = "CE",
        //            Strike = (Decimal)strangleTokenRow["CallStrike"],
        //            TradingSymbol.Trim(' ') = (string)strangleTokenRow["CallSymbol"]
        //        };
        //        put = new Instrument()
        //        {
        //            BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //            InstrumentToken = Convert.ToUInt32(strangleTokenRow["PutToken"]),
        //            InstrumentType = "PE",
        //            Strike = (Decimal)strangleTokenRow["PutStrike"],
        //            TradingSymbol.Trim(' ') = (string)strangleTokenRow["PutSymbol"]
        //        };

        //        if (strangleTokenRow["Expiry"] != DBNull.Value)
        //        {
        //            call.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
        //            put.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);
        //        }

        //        StrangleNode strangleNode = new StrangleNode(call, put);

        //        strangleNode.InitialQty = (int)strangleTokenRow["CallInitialQty"];

        //        ShortTrade trade;
        //        decimal netPnL = 0;
        //        foreach (DataRow strangleTradeRow in strangleRow.GetChildRows(strategy_Trades_Relation))
        //        {
        //            trade = new ShortTrade();
        //            trade.AveragePrice = (Decimal)strangleTradeRow["Price"];
        //            trade.ExchangeTimestamp = (DateTime?)strangleTradeRow["TimeStamp"];
        //            trade.OrderId = (string)strangleTradeRow["OrderId"];
        //            trade.TransactionType = (string)strangleTradeRow["TransactionType"];
        //            trade.Quantity = (int)strangleTradeRow["Quantity"];

        //            if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == call.InstrumentToken)
        //            {
        //                strangleNode.CallOrders.Add();
        //            }
        //            else if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == put.InstrumentToken)
        //            {
        //                strangleNode.PutOrders.Add();
        //                //strangleNode.PutTrades.Add(trade);
        //            }
        //            netPnL += trade.AveragePrice * Math.Abs(trade.Quantity);
        //        }
        //        strangleNode.BaseInstrumentToken = call.BaseInstrumentToken;
        //        strangleNode.PutTradedQty = strangleNode.PutTrades.Sum(x => x.Quantity);
        //        strangleNode.CallTradedQty = strangleNode.CallTrades.Sum(x => x.Quantity);
        //        strangleNode.CurrentPosition = PositionStatus.Open;
        //        strangleNode.MaxQty = (int)strangleRow["MaxQty"];
        //        strangleNode.StepQty = Convert.ToInt32(strangleRow["MaxProfitPoints"]);
        //        strangleNode.NetPnL = netPnL;
        //        strangleNode.ID = (int)strangleRow["Id"];

        //        ActiveStrangles.Add(strangleNode.ID, strangleNode);
        //    }
        //}

        public void StoreActiveBuyStrangeTrade(Instrument bInst, Instrument currentPE, Instrument currentCE,
            int initialQty, int maxQty, int stepQty, decimal safetyWidth = 10, int strangleId = 0, 
            DateTime timeOfOrder = default(DateTime))
        {
            if (currentPE.LastPrice * currentCE.LastPrice != 0)
            {
                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                    //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                    //                                            Convert.ToString(currentPE.InstrumentToken) });

                    //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                    //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;
                    //decimal bPrice = keyValuePairs[Convert.ToString(bInst.InstrumentToken)].Bids[0].Price;
                    //timeOfOrder = DateTime.Now;

                    //TEMP -> First price
                    decimal pePrice = currentPE.LastPrice;
                    decimal cePrice = currentCE.LastPrice;
                    //decimal bPrice = bInst.LastPrice;
                    //////put.Prices.Add(pePrice); //put.SellPrice = 100;
                    //call.Prices.Add(cePrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    ShortTrade callTrade = PlaceOrder(currentCE.TradingSymbol.Trim(' '), true, cePrice, initialQty);
                    ShortTrade putTrade = PlaceOrder(currentPE.TradingSymbol.Trim(' '), true, pePrice, initialQty);

                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(ceToken: currentCE.InstrumentToken, peToken: currentPE.InstrumentToken, cePrice: callTrade.AveragePrice,
                       pePrice: putTrade.AveragePrice, bInstPrice: 0, algoIndex: AlgoIndex.ActiveTradeWithVariableQty, initialQty: initialQty, 
                       maxQty: maxQty, stepQty: stepQty, ceOrderId: callTrade.OrderId, peOrderId: putTrade.OrderId, transactionType: "Buy")  ;
                }
            }
        }
        public int AlgoInstance
        {
            get
            { return _algoInstance; }
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
        public void StopTrade(bool stop)
        {
            _stopTrade = stop;
        }

        //public Order PlaceSuperMultipleOrder()
        //{
        //    uint token = 26544;
        //    string tradingSymbol = "BANKNIFTY21O1439500CE";
        //    decimal lastPrice = 100;
        //    Order callSellOrder = MarketOrders.PlaceOrder(_algoInstance, tradingSymbol, "ce", lastPrice,
        //                          token, false, 25,
        //                           algoIndex, DateTime.Now, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_SM, Tag: "SM Test",
        //                           triggerPrice: lastPrice * 3, broker: Constants.KOTAK, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

        //    callSellOrder.OrderTimestamp = DateTime.Now;
        //    callSellOrder.ExchangeTimestamp = DateTime.Now;

        //    OnTradeEntry(callSellOrder);
        //    return callSellOrder;
        //}
        //public Order CancelSuperMultipleOrder(Order order)
        //{
        //    //Square off order
        //    uint token = 26446;
        //    string tradingSymbol = "BANKNIFTY21O1438500CE";
        //    decimal lastPrice = 100;
        //    Order callBuyOrder = MarketOrders.PlaceOrder(_algoInstance, tradingSymbol, "ce", lastPrice,
        //                          token, true, 25,
        //                           algoIndex, DateTime.Now, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_SM, Tag: "SM Square Off Test", triggerPrice: lastPrice, broker: Constants.KOTAK);

        //    //cancel original

        //    Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, DateTime.Now, product: Constants.PRODUCT_SM);
        //    OnTradeEntry(cancelOrder);


        //    cancelOrder.OrderTimestamp = DateTime.Now;
        //    cancelOrder.ExchangeTimestamp = DateTime.Now;

        //    OnTradeEntry(cancelOrder);
        //    return cancelOrder;
        //}

        private void PublishLog(object sender, ElapsedEventArgs e)
        {
            //if (_bADX != null && _bADX.MovingAverage != null)
            //{
            //    LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
            //    String.Format("Current ADX: {0}", Decimal.Round(_bADX.MovingAverage.GetValue<decimal>(0), 2)),
            //    "Log_Timer_Elapsed");
            //}
            //if (_straddleCallOrderTrio != null && _straddleCallOrderTrio.Order != null
            //    && _straddlePutOrderTrio != null && _straddlePutOrderTrio.Order != null)
            //{
            //    if (_activeCall.LastPrice * _activePut.LastPrice != 0)
            //    {
            //        LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, e.SignalTime,
            //        String.Format("Call: {0}, Put: {1}. Straddle Profit: {2}. BNF: {3}, Call Delta : {4}, Put Delta {5}", _activeCall.LastPrice, _activePut.LastPrice,
            //        _straddleCallOrderTrio.Order.AveragePrice + _straddlePutOrderTrio.Order.AveragePrice - _activeCall.LastPrice - _activePut.LastPrice,
            //         _baseInstrumentPrice, Math.Round(_activeCall.Delta, 2), Math.Round(_activePut.Delta, 2)),
            //        "Log_Timer_Elapsed");
            //    }

            //    Thread.Sleep(100);
            //}
        }
    }
}
