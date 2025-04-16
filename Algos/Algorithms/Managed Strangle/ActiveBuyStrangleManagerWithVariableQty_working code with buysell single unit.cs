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

        private InstrumentLinkedList _ceInstrumentLinkedList, _peInstrumentLinkedList;

        private DateTime? _expiryDate;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;

        private bool _stopTrade;
        public List<uint> SubscriptionTokens;
        private int _initialQty;
        private int _stepQty;
        private int _maxQty;

        private double _entryDelta;
        private double _minDelta;
        private double _maxDelta;
        private int _algoInstance;
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        private int _healthCounter = 0;
        private Object tradeLock = new Object();
        private int _lotSize = 25;
        private StrangleNode _activeStrangle;
        //Dictionary<int, StrangleNode> ActiveStrangles = new Dictionary<int, StrangleNode>();

        public ActiveBuyStrangleManagerWithVariableQty()
        {
            //LoadActiveData();
        }
        public ActiveBuyStrangleManagerWithVariableQty(uint baseInstrumentToken, DateTime? expiry, int initialQty, int stepQty, int maxQty, decimal targetProfit, decimal stopLoss, decimal entryDelta,
            decimal maxDelta, decimal minDelta, AlgoIndex algoIndex, int algoInstance = 0)
        {
            //LoadActiveStrangles();
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            SubscriptionTokens = new List<uint>();

            _initialQty = initialQty * _lotSize;
            _stepQty = stepQty * _lotSize;
            _maxQty = maxQty * _lotSize;
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

            ZConnect.Login();
            KoConnect.Login();

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
            try
            {

                uint token = tick.InstrumentToken;
                lock (tradeLock)
                {
                    if (!GetBaseInstrumentPrice(tick))
                    {
                        return;
                    }


                    if (_activeStrangle == null)
                    {
                        Instrument[] instruments = GetNewStrikes(_baseInstrumentToken, _baseInstrumentPrice, _expiryDate);
                        _activeStrangle = new StrangleNode(instruments[0], instruments[1]);
                        _activeStrangle.ID = _algoInstance;
                        _activeStrangle.BaseInstrumentToken = _baseInstrumentToken;
                        _activeStrangle.BaseInstrumentPrice = _baseInstrumentPrice;
                        _activeStrangle.CurrentPosition = PositionStatus.Open;
                        _activeStrangle.InitialQty = _initialQty;
                        _activeStrangle.StepQty = _stepQty;
                        _activeStrangle.MaxQty = _maxQty;
                        UpdateInstrumentSubscription(currentTime, _activeStrangle.Call, _activeStrangle.Put);
                    }

                    Instrument callOption = _activeStrangle.Call;
                    Instrument putOption = _activeStrangle.Put;

                    if (tick.InstrumentToken == callOption.InstrumentToken)
                    {
                        callOption.LastPrice = tick.LastPrice;
                        callOption.Bids = tick.Bids;
                        callOption.Offers = tick.Offers;
                        _activeStrangle.Call = callOption;
                    }
                    else if (tick.InstrumentToken == putOption.InstrumentToken)
                    {
                        putOption.LastPrice = tick.LastPrice;
                        putOption.Bids = tick.Bids;
                        putOption.Offers = tick.Offers;
                        _activeStrangle.Put = putOption;
                    }
                    else if (tick.InstrumentToken == _activeStrangle.BaseInstrumentToken)
                    {
                        _activeStrangle.BaseInstrumentPrice = tick.LastPrice;
                    }
                    if (_activeStrangle.BaseInstrumentPrice * callOption.LastPrice * putOption.LastPrice == 0)
                    {
                        return;
                    }


                    //Continue trading if position is open
                    if (_activeStrangle.CurrentPosition == PositionStatus.Open)
                    {
                        if (_activeStrangle.CallOrders.Count == 0)
                        {
                            _activeStrangle.CallTradedQty =  TradeEntry(callOption, _activeStrangle.CallOrders, _activeStrangle.InitialQty, currentTime);
                            _profitPoints = Math.Abs((_activeStrangle.CallOrders[0].AveragePrice) * 0.1m);
                        }
                        if (_activeStrangle.PutOrders.Count == 0)
                        {
                            _activeStrangle.PutTradedQty = TradeEntry(putOption, _activeStrangle.PutOrders, _activeStrangle.InitialQty, currentTime);
                            _profitPoints += Math.Abs((_activeStrangle.PutOrders[0].AveragePrice) * 0.1m);
                            _profitPoints = Math.Max(10, _profitPoints);
                        }

                        List<Order> callOrders = _activeStrangle.CallOrders;
                        List<Order> putOrders = _activeStrangle.PutOrders;


                        //decimal pnl = _activeStrangle.NetPnL + _activeStrangle.Put.LastPrice*_activeStrangle.PutTradedQty + _activeStrangle.Call.LastPrice * _activeStrangle.CallTradedQty;
                        //sell traded qty
#if market
                        decimal pnl = _activeStrangle.NetPnL - _activeStrangle.Put.Bids[1].Price * _activeStrangle.PutTradedQty - _activeStrangle.Call.Bids[1].Price * _activeStrangle.CallTradedQty;
#elif local
                        decimal pnl = _activeStrangle.NetPnL + _activeStrangle.Put.LastPrice * _activeStrangle.PutTradedQty + _activeStrangle.Call.LastPrice * _activeStrangle.CallTradedQty;
#endif
                        DataLogic dl = new DataLogic();

                        //decimal profitPoints = Math.Max(Math.Abs((callOrders[0].AveragePrice + putOrders[0].AveragePrice) * 0.1m), 10);

                        Console.WriteLine(pnl);
                        if (pnl > _activeStrangle.InitialQty * (_profitPoints*0+5) || pnl < _activeStrangle.InitialQty * (_profitPoints * 0 - 20))
                        {
                            //ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, _activeStrangle.Call.Bids[1].Price, _activeStrangle.CallTradedQty);
                            //_activeStrangle.NetPnL = dl.UpdateTrade(_activeStrangle.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
                            
                            Order order = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol, "ce", _activeStrangle.Call.LastPrice, 
                                callOption.InstrumentToken, false, _activeStrangle.CallTradedQty, algoIndex, currentTime);
                            //_activeStrangle.NetPnL = dl.UpdateOrder(_algoInstance, order);
                            _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.CallTradedQty;

                            OnTradeExit(order);
                            //trade = PlaceOrder(putOption.TradingSymbol, false, _activeStrangle.Put.Bids[1].Price, _activeStrangle.PutTradedQty);
                            //_activeStrangle.NetPnL = dl.UpdateTrade(_activeStrangle.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

                            order = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol, "pe", _activeStrangle.Put.LastPrice,
                                putOption.InstrumentToken, false, _activeStrangle.PutTradedQty, algoIndex, currentTime);
                            OnTradeExit(order);
                            //_activeStrangle.NetPnL = dl.UpdateOrder(_algoInstance, order);

                            _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.PutTradedQty;

                            _activeStrangle.CurrentPosition = PositionStatus.Closed;

                            _algoInstance = Utility.GenerateAlgoInstance(algoIndex, _baseInstrumentToken, _expiryDate.GetValueOrDefault(DateTime.Now),
                                _expiryDate.GetValueOrDefault(DateTime.Now), _initialQty, _maxQty, _stepQty, (decimal)_maxDelta, 0, (decimal)_minDelta, 0, 0,
                                0, 0, 0, 0, _targetProfit, _stopLoss, 0, positionSizing: false, maxLossPerTrade: _stopLoss);

                            _activeStrangle = null;
                            //Instrument[] instruments = GetNewStrikes(_activeStrangle.BaseInstrumentToken, _activeStrangle.BaseInstrumentPrice, _activeStrangle.Call.Expiry);
                            //callOption = instruments[0];
                            //putOption = instruments[1];

                            //UpdateInstrumentSubscription(currentTime, callOption, putOption);

                            ///// Start a new trade
                            //if (tick.InstrumentToken == callOption.InstrumentToken)
                            //{
                            //    callOption.LastPrice = tick.LastPrice;
                            //    callOption.Bids = tick.Bids;
                            //    callOption.Offers = tick.Offers;
                            //    _activeStrangle.Call = callOption;
                            //}
                            //else if (tick.InstrumentToken == putOption.InstrumentToken)
                            //{
                            //    putOption.LastPrice = tick.LastPrice;
                            //    putOption.Bids = tick.Bids;
                            //    putOption.Offers = tick.Offers;
                            //    _activeStrangle.Put = putOption;
                            //}

                            //decimal pePrice = putOption.LastPrice;
                            //decimal cePrice = callOption.LastPrice;
                            //int initialQty = _activeStrangle.InitialQty;

                            //Order callOrder = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol.TrimEnd(), "ce", cePrice, callOption.InstrumentToken, true, initialQty, algoIndex, currentTime);
                            //Order putOrder = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol.TrimEnd(), "pe", pePrice, putOption.InstrumentToken, true, initialQty, algoIndex, currentTime);

                            //////Update Database
                            ////int strangleId = dl.StoreStrangleData(ceToken: callOption.InstrumentToken, peToken: putOption.InstrumentToken, cePrice: callTrade.AveragePrice,
                            ////   pePrice: putTrade.AveragePrice, bInstPrice: 0, algoIndex: AlgoIndex.ActiveTradeWithVariableQty, initialQty: initialQty,
                            ////   maxQty: _activeStrangle.MaxQty, stepQty: _activeStrangle.StepQty, ceOrderId: callTrade.OrderId, peOrderId: putTrade.OrderId, transactionType: "Buy");

                            //StrangleNode tempStrangleNode = new StrangleNode(callOption, putOption);
                            //tempStrangleNode.BaseInstrumentToken = _activeStrangle.BaseInstrumentToken;
                            //tempStrangleNode.BaseInstrumentPrice = _activeStrangle.BaseInstrumentPrice;
                            //tempStrangleNode.InitialQty = tempStrangleNode.PutTradedQty = tempStrangleNode.CallTradedQty = _activeStrangle.InitialQty;
                            //tempStrangleNode.NetPnL = callOrder.AveragePrice * Math.Abs(callOrder.Quantity) + putOrder.AveragePrice * Math.Abs(putOrder.Quantity);
                            //tempStrangleNode.StepQty = _activeStrangle.StepQty;
                            //tempStrangleNode.MaxQty = _activeStrangle.MaxQty;
                            //tempStrangleNode.ID = _algoInstance;

                            //tempStrangleNode.CallOrders.Add(callOrder);
                            //tempStrangleNode.PutOrders.Add(putOrder);

                            ////ActiveStrangles.Add(strangleId, tempStrangleNode);
                            //_activeStrangle = tempStrangleNode;
                        }
                        else
                        {

#if market
                            decimal putBid = putOption.Bids.Count() >= 3 && putOption.Bids[1].Price != 0 ? putOption.Bids[1].Price : putOption.Bids.Count() >= 2 && putOption.Bids[1].Price != 0 ? putOption.Bids[1].Price : putOption.LastPrice;
                            decimal putOffer = putOption.Offers.Count() >= 3 && putOption.Offers[1].Price != 0 ? putOption.Offers[1].Price : putOption.Offers.Count() >= 2 && putOption.Offers[1].Price != 0 ? putOption.Offers[1].Price : putOption.LastPrice;
                            decimal callBid = callOption.Bids.Count() >= 3 && callOption.Bids[1].Price != 0 ? callOption.Bids[1].Price : callOption.Bids.Count() >= 2 && callOption.Bids[1].Price != 0 ? callOption.Bids[1].Price : callOption.LastPrice;
                            decimal callOffer = callOption.Offers.Count() >= 3 && callOption.Offers[1].Price != 0 ? callOption.Offers[1].Price : callOption.Offers.Count() >= 2 && callOption.Offers[1].Price != 0 ? callOption.Offers[1].Price : callOption.LastPrice;
#elif local
                            decimal putBid = putOption.LastPrice;
                            decimal putOffer = putOption.LastPrice;
                            decimal callBid = callOption.LastPrice;
                            decimal callOffer = callOption.LastPrice;
#endif
                            //int PutSellCallBuyQty = Convert.ToInt32((callOption.Offers[2].Price / putOption.Bids[2].Price) * _activeStrangle.CallTradedQty - _activeStrangle.PutTradedQty);
                            //int PutBuyCallSellQty = Convert.ToInt32((callOption.Bids[2].Price / putOption.Offers[2].Price) * _activeStrangle.CallTradedQty - _activeStrangle.PutTradedQty);

                            int PutSellCallBuyQty = Convert.ToInt32((callOffer / putBid) * _activeStrangle.CallTradedQty - _activeStrangle.PutTradedQty);
                            int PutBuyCallSellQty = Convert.ToInt32((callBid / putOffer) * _activeStrangle.CallTradedQty - _activeStrangle.PutTradedQty);

                            if (Math.Abs(PutBuyCallSellQty) >= _activeStrangle.StepQty) //need more puts or less calls
                            {
                                if (PutBuyCallSellQty > 0)
                                {
                                    if (_activeStrangle.CallTradedQty > _activeStrangle.InitialQty)
                                    {
                                        //place order to sell calls
                                        //ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, callOption.Bids[1].Price, _activeStrangle.StepQty);
                                        Order order = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol, "ce", callOption.LastPrice, callOption.InstrumentToken, false, _activeStrangle.StepQty, algoIndex, currentTime);
                                        OnTradeExit(order);
                                        //increase trade record and quantity
                                        callOrders.Add(order);
                                        _activeStrangle.CallTradedQty = callOrders.Sum(x => (x.TransactionType.ToLower() == "buy" ? 1 : -1) * x.Quantity);

                                        //_activeStrangle.CallOrders = callOrders; //This may not be required. Just check

                                        _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.StepQty;  //dl.UpdateOrder(_activeStrangle.ID, order);
                                    }
                                    else if (_activeStrangle.PutTradedQty < _activeStrangle.MaxQty)
                                    {
                                        //place order to buy Puts
                                        Order order = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol, "pe", putOption.LastPrice, putOption.InstrumentToken, true, _activeStrangle.StepQty, algoIndex, currentTime);
                                        OnTradeEntry(order);
                                        //increase trade record and quantity
                                        putOrders.Add(order);
                                        _activeStrangle.PutTradedQty = putOrders.Sum(x => (x.TransactionType.ToLower() == "buy" ? 1 : -1) * x.Quantity);

                                       // _activeStrangle.PutOrders = putOrders; //This may not be required. Just check

                                        //_activeStrangle.NetPnL = dl.UpdateOrder(_activeStrangle.ID, order);
                                        _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.StepQty * -1;  //dl.UpdateOrder(_activeStrangle.ID, order);

                                    }
                                    else
                                    {

                                    }
                                }
                            }
                            if (Math.Abs(PutSellCallBuyQty) >= _activeStrangle.StepQty) //need more calls or less puts
                            {
                                if (PutSellCallBuyQty < 0)
                                {
                                    if (_activeStrangle.PutTradedQty > _activeStrangle.InitialQty)
                                    {
                                        ////place order to sell puts
                                        //ShortTrade trade = PlaceOrder(putOption.TradingSymbol, false, putOption.Bids[1].Price, _activeStrangle.StepQty);

                                        ////increase trade record and quantity
                                        //putTrades.Add(trade);
                                        //_activeStrangle.PutTradedQty = putTrades.Sum(x => x.Quantity);

                                        //_activeStrangle.PutTrades = putTrades; //This may not be required. Just check

                                        //_activeStrangle.NetPnL = dl.UpdateTrade(_activeStrangle.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);


                                        //place order to buy Puts
                                        Order order = MarketOrders.PlaceOrder(_algoInstance, putOption.TradingSymbol, "pe", putOption.LastPrice, putOption.InstrumentToken, false, _activeStrangle.StepQty, algoIndex, currentTime);
                                        OnTradeExit(order);
                                        //increase trade record and quantity
                                        putOrders.Add(order);
                                        _activeStrangle.PutTradedQty = putOrders.Sum(x => (x.TransactionType.ToLower() =="buy"? 1: -1) *  x.Quantity);

                                        //_activeStrangle.PutOrders = putOrders; //This may not be required. Just check

                                        //_activeStrangle.NetPnL = dl.UpdateOrder(_activeStrangle.ID, order);
                                        _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.StepQty;  //dl.UpdateOrder(_activeStrangle.ID, order);



                                    }
                                    else if (_activeStrangle.CallTradedQty < _activeStrangle.MaxQty)
                                    {
                                        ////place order to buy Call
                                        //ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.Offers[1].Price, _activeStrangle.StepQty);

                                        ////increase trade record and quantity
                                        //callTrades.Add(trade);
                                        //_activeStrangle.CallTradedQty = callTrades.Sum(x => x.Quantity);

                                        //_activeStrangle.CallTrades = callTrades; //This may not be required. Just check

                                        //_activeStrangle.NetPnL = dl.UpdateTrade(_activeStrangle.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

                                        //place order to sell calls
                                        //ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, callOption.Bids[1].Price, _activeStrangle.StepQty);
                                        Order order = MarketOrders.PlaceOrder(_algoInstance, callOption.TradingSymbol, "ce", callOption.LastPrice, callOption.InstrumentToken, true, _activeStrangle.StepQty, algoIndex, currentTime);
                                        OnTradeEntry(order);
                                        //increase trade record and quantity
                                        callOrders.Add(order);
                                        _activeStrangle.CallTradedQty = callOrders.Sum(x => (x.TransactionType.ToLower() == "buy" ? 1 : -1) * x.Quantity);

                                        //_activeStrangle.CallOrders = callOrders; //This may not be required. Just check

                                        //_activeStrangle.NetPnL = dl.UpdateOrder(_activeStrangle.ID, order);
                                        _activeStrangle.NetPnL += order.AveragePrice * _activeStrangle.StepQty * -1;  //dl.UpdateOrder(_activeStrangle.ID, order);
                                    }
                                    else
                                    {

                                    }
                                }
                            }

#region OLD CODE WITHOUT BID/ASK
                            //if (Math.Abs(putBuyQtyNeeded) >= strangleNode.StepQty)
                            // {
                            //     if (putQtyNeeded > 0) //need more puts or less calls
                            //     {
                            //         if (strangleNode.CallTradedQty > strangleNode.InitialQty)
                            //         {
                            //             //place order to sell calls
                            //             ShortTrade trade = PlaceOrder(callOption.TradingSymbol, false, callOption.LastPrice, strangleNode.StepQty);

                            //             //increase trade record and quantity
                            //             callTrades.Add(trade);
                            //             strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);

                            //             strangleNode.CallTrades = callTrades; //This may not be required. Just check

                            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);

                            //         }
                            //         else if (strangleNode.PutTradedQty < strangleNode.MaxQty)
                            //         {
                            //             //place order to buy Puts
                            //             ShortTrade trade = PlaceOrder(putOption.TradingSymbol, true, putOption.LastPrice, strangleNode.StepQty);

                            //             //increase trade record and quantity
                            //             putTrades.Add(trade);
                            //             strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);

                            //             strangleNode.PutTrades = putTrades; //This may not be required. Just check

                            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
                            //         }
                            //         else
                            //         {

                            //         }
                            //     }
                            //     else //need more calls or less puts
                            //     {
                            //         if (strangleNode.PutTradedQty > strangleNode.InitialQty)
                            //         {
                            //             //place order to sell puts
                            //             ShortTrade trade = PlaceOrder(putOption.TradingSymbol, false, putOption.LastPrice, strangleNode.StepQty);

                            //             //increase trade record and quantity
                            //             putTrades.Add(trade);
                            //             strangleNode.PutTradedQty = putTrades.Sum(x => x.Quantity);

                            //             strangleNode.PutTrades = putTrades; //This may not be required. Just check

                            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, putOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
                            //         }
                            //         else if (strangleNode.CallTradedQty < strangleNode.MaxQty)
                            //         {
                            //             //place order to buy Puts
                            //             ShortTrade trade = PlaceOrder(callOption.TradingSymbol, true, callOption.LastPrice, strangleNode.StepQty);

                            //             //increase trade record and quantity
                            //             callTrades.Add(trade);
                            //             strangleNode.CallTradedQty = callTrades.Sum(x => x.Quantity);

                            //             strangleNode.CallTrades = callTrades; //This may not be required. Just check

                            //             strangleNode.NetPnL = dl.UpdateTrade(strangleNode.ID, callOption.InstrumentToken, trade, AlgoIndex.ActiveTradeWithVariableQty);
                            //         }
                            //         else
                            //         {

                            //         }
                            //     }
                            // }
                            // }
#endregion
                        }
                    }
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

        private int TradeEntry(Instrument option, List<Order> orders, int quantity, DateTime? currentTime)
        {
            //place order to buy Puts
            Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, option.LastPrice, option.InstrumentToken, true, quantity, algoIndex, currentTime);
            OnTradeEntry(order);
            //increase trade record and quantity
            orders.Add(order);

            // _activeStrangle.PutOrders = orders; //This may not be required. Just check
            DataLogic dl = new DataLogic();
            _activeStrangle.NetPnL += order.AveragePrice * quantity * -1;  //dl.UpdateOrder(_activeStrangle.ID, order);
            
            return orders.Sum(x => x.Quantity);
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

        public Instrument[] GetNewStrikes(uint baseInstrumentToken, decimal baseInstrumentPrice, DateTime? expiry)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument>[] nodeData = dl.RetrieveNextStrangleNodes(baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now), baseInstrumentPrice, baseInstrumentPrice, 0);

            SortedList<Decimal, Instrument> calls = nodeData[0];
            SortedList<Decimal, Instrument> puts = nodeData[1];

            //IEnumerable<KeyValuePair<decimal, Instrument>> callkeyvalue = calls.Where(x => x.Key < baseInstrumentPrice);
            //IEnumerable<KeyValuePair<decimal, Instrument>> putkeyvalue = puts.Where(x => x.Key > baseInstrumentPrice);

            //var call = callkeyvalue.ElementAt(callkeyvalue.Count() - 1).Value;
            //var put = putkeyvalue.ElementAt(0).Value;
            //return new Instrument[] { call, put };

            IEnumerable<KeyValuePair<decimal, Instrument>> callkeyvalue = calls.Where(x => x.Key > baseInstrumentPrice);
            IEnumerable<KeyValuePair<decimal, Instrument>> putkeyvalue = puts.Where(x => x.Key < baseInstrumentPrice);

            var call = callkeyvalue.ElementAt(0).Value;
            var put = putkeyvalue.ElementAt(putkeyvalue.Count()-1).Value;

            //call = calls[32600];
            //put = puts[32600];
            return new Instrument[] { call, put };
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

        public Order PlaceSuperMultipleOrder ()
        {
            uint token = 26544;
            string tradingSymbol = "BANKNIFTY21O1439500CE";
            decimal lastPrice = 100;
            Order callSellOrder = MarketOrders.PlaceOrder(_algoInstance, tradingSymbol, "ce", lastPrice,
                                  token, false, 25,
                                   algoIndex, DateTime.Now, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_SM, Tag: "SM Test", triggerPrice: lastPrice * 3, broker: Constants.KOTAK);

            callSellOrder.OrderTimestamp = DateTime.Now;
            callSellOrder.ExchangeTimestamp = DateTime.Now;

            OnTradeEntry(callSellOrder);
            return callSellOrder;
        }
        public Order CancelSuperMultipleOrder(Order order)
        {
            //Square off order
            uint token = 26446;
            string tradingSymbol = "BANKNIFTY21O1438500CE";
            decimal lastPrice = 100;
            Order callBuyOrder = MarketOrders.PlaceOrder(_algoInstance, tradingSymbol, "ce", lastPrice,
                                  token, true, 25,
                                   algoIndex, DateTime.Now, Constants.ORDER_TYPE_MARKET, product: Constants.PRODUCT_SM, Tag: "SM Square Off Test", triggerPrice: lastPrice, broker: Constants.KOTAK);

            //cancel original

            Order cancelOrder = MarketOrders.CancelKotakOrder(_algoInstance, algoIndex, order, DateTime.Now, product: Constants.PRODUCT_SM);
                OnTradeEntry(cancelOrder);


            cancelOrder.OrderTimestamp = DateTime.Now;
            cancelOrder.ExchangeTimestamp = DateTime.Now;

            OnTradeEntry(cancelOrder);
            return cancelOrder;
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

        private OrderTrio TradeEntry(Instrument option, DateTime currentTime, decimal lastPrice, int tradeQty, bool buyOrder)
        {
            OrderTrio orderTrio = null;
            try
            {
                decimal entryRSI = 0;
                //ENTRY ORDER - Sell ALERT
                Order order = MarketOrders.PlaceOrder(_algoInstance, option.TradingSymbol, option.InstrumentType, lastPrice,
                    option.InstrumentToken, buyOrder, tradeQty * Convert.ToInt32(option.LotSize),
                    algoIndex, currentTime, Constants.ORDER_TYPE_MARKET);

                if (order.Status == Constants.ORDER_STATUS_REJECTED)
                {
                    _stopTrade = true;
                    return orderTrio;
                }

                //LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Info, order.OrderTimestamp.Value,
                //   string.Format("TRADE!! {3} {0} lots of {1} @ {2}", tradeQty / _tradeQty,
                //   option.TradingSymbol, order.AveragePrice, buyOrder ? "Bought" : "Sold"), "TradeEntry");

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
        //            TradingSymbol = (string)strangleTokenRow["CallSymbol"]
        //        };
        //        put = new Instrument()
        //        {
        //            BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
        //            InstrumentToken = Convert.ToUInt32(strangleTokenRow["PutToken"]),
        //            InstrumentType = "PE",
        //            Strike = (Decimal)strangleTokenRow["PutStrike"],
        //            TradingSymbol = (string)strangleTokenRow["PutSymbol"]
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
        //                strangleNode.CallTrades.Add(trade);
        //            }
        //            else if (Convert.ToUInt32(strangleTradeRow["InstrumentToken"]) == put.InstrumentToken)
        //            {
        //                strangleNode.PutTrades.Add(trade);
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
                    ShortTrade callTrade = PlaceOrder(currentCE.TradingSymbol, true, cePrice, initialQty);
                    ShortTrade putTrade = PlaceOrder(currentPE.TradingSymbol, true, pePrice, initialQty);

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
