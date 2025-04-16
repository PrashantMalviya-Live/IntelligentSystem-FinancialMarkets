using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Algorithms.Utilities;
using GlobalLayer;
using KiteConnect;
using BrokerConnectWrapper;
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
using Algos.Utilities;
using System.Threading.Channels;
using Grpc.Core;

namespace Algorithms.Algorithms
{
    public class ActiveBuyStrangleWithVariableQty : ICZMQ
    {
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ActiveBuyStrangleWithVariableQty source, string channel);
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

        public const AlgoIndex algoIndex = AlgoIndex.Crypto_ActiveStrangleBuy;
        private decimal _targetProfit;
        private decimal _stopLoss;
        private DateTime _expiryDate;
        private bool _stopTrade;
        private int _initialQty;
        private int _stepQty;
        private int _maxQty;
        private User _user;
        private int _minQty = 50;
        private decimal _pnl = 0;
        private decimal _minPnl = 0;
        private int _algoInstance;
        private bool _higherProfit = false;
        private System.Timers.Timer _healthCheckTimer;
        private int _healthCounter = 0;
        IHttpClientFactory _httpClientFactory;
        string _userId;
        private TradedCryptoOption _callOption, _putOption;
        private TradedCryptoFuture _future = new TradedCryptoFuture();
        private const decimal TARGET_OPTION_FUTURE_MULTIPLIER = 25;
        private const decimal MAXIMUM_MULTIPLIER = 1.3M;
        private const decimal INITIAL_PREMIUM = 100;
        //strike, premium
        Dictionary<decimal, TradedCryptoOption> _callOptions = new Dictionary<decimal, TradedCryptoOption>();
        Dictionary<decimal, TradedCryptoOption> _putOptions = new Dictionary<decimal, TradedCryptoOption>();

        //Dictionary<int, StrangleNode> ActiveStrangles = new Dictionary<int, StrangleNode>();

        public ActiveBuyStrangleWithVariableQty(uint baseInstrumentToken, DateTime expiry, int initialQty, int stepQty, int maxQty, decimal targetProfit, decimal stopLoss, decimal entryDelta,
            decimal maxDelta, decimal minDelta, AlgoIndex algoIndex, IHttpClientFactory httpClientFactory = null, int algoInstance = 0, string userid = "")
        {
            //LoadActiveStrangles();
            _userId = userid;
            _httpClientFactory = httpClientFactory;
            _expiryDate = expiry;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            _initialQty = initialQty;
            _minQty = _initialQty;
            _stepQty = stepQty;
            _maxQty = maxQty;
            
            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, expiry,
                expiry, _initialQty, _maxQty, _stepQty, 0, 0, 0, 0, 0,
                0, 0, 0, 0, _targetProfit, _stopLoss, Arg9: _userId, positionSizing: false, maxLossPerTrade: _stopLoss);


            DEConnect.Login();
            _user = DEConnect.GetUser(userId: _userId);

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            //_logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            //_logTimer.Elapsed += PublishLog;
            //_logTimer.Start();

        }

        private void ActiveTrade(string channel, string data)
        {
            L1Orderbook orderbook = L1Orderbook.FromJson(data);
            DateTime currentTime = DateTimeOffset.FromUnixTimeMilliseconds(orderbook.Timestamp / 1000).LocalDateTime;

            try
            {
                UpdateOptionChain(orderbook);
                //SetTargetOptions();
                SetActiveTargetOptions();

                if (_callOption != null && _putOption != null)
                {
                    //take initial position and contnuously monitor them
                    if (_callOption.Ask > _putOption.Ask)
                    {
                        if (_putOption.Size == 0 && _callOption.Size == 0)
                        {
                            int callQty = _initialQty;
                            int putQty = Convert.ToInt32(Math.Round((_callOption.Ask * _initialQty) / _putOption.Ask, 0));

                            TakeTrade(_callOption, currentTime, callQty, true, _callOption.Ask);
                            TakeTrade(_putOption, currentTime, putQty, true, _putOption.Ask);
                        }
                        else
                        {
                            if (_callOption.Ask * _callOption.Size > _putOption.Ask * _putOption.Size * MAXIMUM_MULTIPLIER * 2/1.3M)
                            {
                                if (_putOption.Size < _maxQty)
                                {
                                    TakeTrade(_putOption, currentTime, _stepQty, true, _putOption.Ask);
                                }
                                else
                                {
                                    TakeTrade(_callOption, currentTime, _callOption.Size - _putOption.Size, false, _callOption.Ask);
                                }
                            }
                            else if (_putOption.Ask * _putOption.Size > _callOption.Ask * _callOption.Size * MAXIMUM_MULTIPLIER * 2 / 1.3M)
                            {
                                if (_callOption.Size < _maxQty)
                                {
                                    TakeTrade(_callOption, currentTime, _stepQty, true, _callOption.Ask);
                                }
                                else
                                {
                                    TakeTrade(_putOption, currentTime, _putOption.Size - _callOption.Size, false, _putOption.Ask);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (_putOption.Size == 0 && _callOption.Size == 0)
                        {
                            int putQty = _initialQty;
                            int callQty = Convert.ToInt32(Math.Round((_putOption.Ask * _initialQty) / _callOption.Ask, 0));
                            TakeTrade(_callOption, currentTime, callQty, true, _callOption.Ask);
                            TakeTrade(_putOption, currentTime, putQty, true, _putOption.Ask);
                        }
                        if (_putOption.Ask * _putOption.Size > _callOption.Ask * _callOption.Size * MAXIMUM_MULTIPLIER * 2 / 1.3M)
                        {
                            if (_callOption.Size < _maxQty)
                            {
                                TakeTrade(_callOption, currentTime, _stepQty, true, _callOption.Ask);
                            }
                            else if (_putOption.Size > _minQty)
                            {
                                TakeTrade(_putOption, currentTime, _stepQty, false, _putOption.Ask);
                            }
                            else
                            {
                                //close this put and take fresh put at initial premium
                                //TakeTrade(_putOption, currentTime, _putOption.Size, false);
                                //_putOption = null;

                                //TakeTrade(_putOption, currentTime, _putOption.Size - _callOption.Size, false);
                            }
                        }
                        else if (_callOption.Ask * _callOption.Size > _putOption.Ask * _putOption.Size * MAXIMUM_MULTIPLIER * 2 / 1.3M)
                        {
                            if (_putOption.Size < _maxQty)
                            {
                                TakeTrade(_putOption, currentTime, _stepQty, true, _putOption.Ask);
                            }
                            else if (_callOption.Size > _minQty)
                            {
                                TakeTrade(_callOption, currentTime, _stepQty, false, _callOption.Ask);
                            }
                            else
                            {
                                //TakeTrade(_callOption, currentTime, _callOption.Size - _putOption.Size, false);
                            }
                        }
                    }


                    decimal pnl = _callOption.Size/1000 * (_callOption.Bid - _callOption.EntryPremium) + _putOption.Size/1000 * (_putOption.Bid - _putOption.EntryPremium) 
                        - 2*(_putOption.PaidCommission +_callOption.PaidCommission);

                    CryptoDataLogic dl = new CryptoDataLogic();
                    
                    Console.WriteLine($"{pnl}: {currentTime}");
                    
                    if (pnl > 50 || _pnl > 59)
                    {
                        TakeTrade(_callOption, currentTime, _callOption.Size, false, _callOption.Ask);
                        TakeTrade(_putOption, currentTime, _putOption.Size, false, _putOption.Ask);
                        //_stopTrade = true;
                        _pnl += pnl;
                        _callOption = null;
                        _putOption = null;
                        Interlocked.Increment(ref _healthCounter);
                    }
                }
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

        private void SetTargetOptions()
        {
            _callOption = _callOption ?? _callOptions.Values.FirstOrDefault(x => x.Ask > INITIAL_PREMIUM - 50 && x.Ask < INITIAL_PREMIUM + 50, null);
            _putOption = _putOption ?? _putOptions.Values.FirstOrDefault(x => x.Ask > INITIAL_PREMIUM - 50 && x.Ask < INITIAL_PREMIUM + 50, null);
        }
        private void SetActiveTargetOptions()
        {
            if (_future.CurrentPrice != 0)
            {
                _callOption = _callOption ?? _callOptions.Values.FirstOrDefault(x => x.Strike >= _future.CurrentPrice + 100 && x.Strike <= _future.CurrentPrice + 400);
                _putOption = _putOption ?? _putOptions.Values.FirstOrDefault(x => x.Strike <= _future.CurrentPrice - 100 && x.Strike >= _future.CurrentPrice - 400);
            }
        }
        private void TakeTrade(TradedCryptoOption option, DateTime currentTime, decimal qty, bool longTrade, decimal lastPrice)
        {
            CryptoOrder order = CryptoOrders.PlaceDEOrder(_algoInstance, option.Symbol, Convert.ToInt32(qty), 0, longTrade,
                algoIndex, _user, orderType: Constants.DELTAEXCHANGE_ORDER_TYPE_MARKET, limitPrice: lastPrice, timeStamp: currentTime, httpClient: _httpClientFactory == null ? null : _httpClientFactory.CreateClient());

            decimal orderSize = option.Size + order.Size * (longTrade ? 1 : -1);
            if (orderSize > 0)
            {
                option.Symbol = order.ProductSymbol;
                option.CurrentPremium = Convert.ToDecimal(order.AverageFilledPrice);
                option.EntryPremium = (option.EntryPremium * option.Size + Convert.ToDecimal(order.AverageFilledPrice) * order.Size * (longTrade ? 1 : -1)) / (orderSize);
                option.Size += order.Size * (longTrade ? 1 : -1);
                option.Side = order.Side;
                option.PaidCommission += Convert.ToDecimal(order.PaidCommission);
            }
        }
        private void UpdateOptionChain(L1Orderbook orderbook)
        {
            string[] optionSymbolArray = orderbook.Symbol.Split("-");

            if (optionSymbolArray.Count() < 4)
            {
                //this is not an option.this may be future , check for future BTC symbol

                if (orderbook.Symbol == "BTCUSD" && _future != null)
                {
                    _future.CurrentPrice = Convert.ToDecimal(orderbook.BestAsk);
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

        
        
        public void OnNext(string channel, string data)
        {
            try
            {
                if (_stopTrade)
                {
                    return;
                }
                ActiveTrade(channel, data);
                return;
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Trading Stopped as algo encountered an error");
                LoggerCore.PublishLog(_algoInstance, algoIndex, LogLevel.Error, DateTime.Now,
                    String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "OnNext");
                Thread.Sleep(100);
                return;
            }
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
