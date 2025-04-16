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
using ZMQFacade;

namespace Algorithms.Algorithms
{
    public class ManagedStrangleDelta : IZMQ //, IObserver<Tick[]>
    {
        Dictionary<int, InstrumentLinkedList[]> ActiveStrangles = new Dictionary<int, InstrumentLinkedList[]>();
        InstrumentLinkedList[] ActiveStrangle = new InstrumentLinkedList[2];
        private List<Instrument> _optionUniverse;
        [field: NonSerialized]
        public delegate void OnOptionUniverseChangeHandler(ManagedStrangleDelta source);
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
        int _algoInstance;
        private uint _baseInstrumentToken;
        private decimal _baseInstrumentPrice;
        public List<uint> SubscriptionTokens { get; set; }
        private System.Timers.Timer _healthCheckTimer;
        private System.Timers.Timer _logTimer;
        int _quantity;
        decimal _targetProfit;
        decimal _stopLoss;
        double _entryDelta;
        double _maxDelta;
        double _minDelta;
        decimal _maxLossPerTrade;
        decimal lowerNoTradeZone1;
        decimal lowerNoTradeZone2;
        decimal upperNoTradeZone1;
        decimal upperNoTradeZone2;
        AlgoIndex _algoIndex = AlgoIndex.DeltaStrangle;
        DateTime? _expiryDate;
        bool _stopTrade;
        int _initialQty;
        int _maxQty;
        int _stepQty;
        private Object tradeLock = new Object();
        //public DirectionalWithStraddleShift(DateTime endTime, TimeSpan candleTimeSpan,
        //   uint baseInstrumentToken, DateTime? expiry, int quantity, int emaLength,
        //   decimal targetProfit, decimal stopLoss, bool straddleShift, decimal thresholdRatio = 1.67m,
        //   int algoInstance = 0, bool positionSizing = false,
        //   decimal maxLossPerTrade = 0)
        public ManagedStrangleDelta(uint baseInstrumentToken, DateTime? expiry, int quantity, decimal targetProfit, decimal stopLoss, decimal entryDelta, decimal maxDelta,
            decimal minDelta, decimal lowerNoTradeZone1, decimal lowerNoTradeZone2, decimal upperNoTradeZone1, decimal upperNoTradeZone2, AlgoIndex algoIndex, int algoInstance = 0)

        {
            //LoadActiveStrangles();
            _expiryDate = expiry;
            _baseInstrumentToken = baseInstrumentToken;
            _stopTrade = true;
            _stopLoss = stopLoss;
            _targetProfit = targetProfit;
            SubscriptionTokens = new List<uint>();
            _quantity = quantity;
            _maxLossPerTrade = stopLoss;

            _algoInstance = algoInstance != 0 ? algoInstance :
                Utility.GenerateAlgoInstance(algoIndex, baseInstrumentToken, expiry.GetValueOrDefault(DateTime.Now),
                expiry.GetValueOrDefault(DateTime.Now), _initialQty, _maxQty, _stepQty, _maxDelta, 0, _minDelta, 0, 0,
                0, 0, 0, 0, lowerNoTradeZone1, lowerNoTradeZone2, upperNoTradeZone1, upperNoTradeZone2, _targetProfit, _stopLoss, 
                0, 0, positionSizing: false, maxLossPerTrade: _maxLossPerTrade);

            _optionUniverse = new List<uint>();

            ZConnect.Login();

            //health check after 1 mins
            _healthCheckTimer = new System.Timers.Timer(interval: 1 * 60 * 1000);
            _healthCheckTimer.Elapsed += CheckHealth;
            _healthCheckTimer.Start();

            _logTimer = new System.Timers.Timer(interval: 5 * 60 * 1000);
            _logTimer.Elapsed += PublishLog;
            _logTimer.Start();

            
            
            
           
        }
        public IDisposable UnsubscriptionToken;

        //Instrument _bInst;

        private void ReviewStrangle(InstrumentLinkedList optionList, Tick tick)
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
                    LoadOptionsToTrade(currentTime);
                    UpdateInstrumentSubscription(currentTime);

                    InstrumentListNode optionNode = optionList.Current;
                    Instrument currentOption = optionNode.Instrument;

                    Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == currentOption.InstrumentToken);
                    if (optionTick.LastPrice != 0)
                    {
                        currentOption.LastPrice = optionTick.LastPrice;
                        optionList.Current.Instrument = currentOption;
                    }

                    // _bInst.InstrumentToken = currentOption.BaseInstrumentToken;
                    Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == optionList.BaseInstrumentToken);
                    if (baseInstrumentTick.LastPrice != 0)
                    {
                        optionList.BaseInstrumentPrice = baseInstrumentTick.LastPrice;
                    }

                    //Pulling next nodes from the begining and keeping it up to date from the ticks. This way next nodes will be available when needed, as Tick table takes time.
                    //Doing it on Async way
                    if ((optionNode.NextNode == null && !optionNode.LastNode) || (optionNode.PrevNode == null && !optionNode.FirstNode))
                        AssignNextNodes(optionNode, optionList.BaseInstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);

                    if (optionList.BaseInstrumentPrice == 0 || currentOption.LastPrice == 0)
                    {
                        optionList.Current = optionNode;
                        return;
                    }

                    ///Relatively slower task kept after asnyc method call
                    //TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
                    double optionDelta = currentOption.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(optionList.BaseInstrumentPrice));
                    //double optionDelta = currentOption.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, DateTime.Now, Convert.ToDouble(optionList.BaseInstrumentPrice));
                    if (double.IsNaN(optionDelta))
                    {
                        return;
                    }
                    optionDelta = Math.Abs(optionDelta);
                    UpdateLastPriceOnAllNodes(ref optionNode, ref ticks);

                    if (optionDelta > Convert.ToDouble(optionList.MaxLossPoints) || optionDelta < Convert.ToDouble(optionList.MaxProfitPoints))
                    {
                        uint previousInstrumentToken = currentOption.InstrumentToken;
                        int previousInstrumentIndex = optionNode.Index;

                        InstrumentListNode subsequentNode = MoveToSubsequentNode(optionNode, optionDelta, Convert.ToDouble(optionList.MaxProfitPoints),
                            Convert.ToDouble(optionList.MaxLossPoints), ticks, optionList.BaseInstrumentPrice);
                        //Logic to determine the next node and move there

                        if (subsequentNode == null || subsequentNode.Index == previousInstrumentIndex)
                        {
                            return;
                        }

                        //put P&L impact on node level on the linked list
                        optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: true, optionList) * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction
                        decimal previousNodeAvgPrice = optionNode.Prices.Last();

                        currentOption = subsequentNode.Instrument;
                        optionList.Current = subsequentNode;
                        subsequentNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: false, optionList));
                        decimal currentNodeAvgPrice = subsequentNode.Prices.Last(); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                        //Do we need current instrument index , when current instrument is already present in the linked list?
                        //It can see the index of current instrument using list.current.index
                        optionList.CurrentInstrumentIndex = subsequentNode.Index;

                        //make next symbol as next to current node. Also update PL data in linklist main
                        //also update currnetindex in the linklist main
                        DataLogic dl = new DataLogic();
                        dl.UpdateListData(optionList.listID, currentOption.InstrumentToken, subsequentNode.Index, previousInstrumentIndex, currentOption.InstrumentType,
                            previousInstrumentToken, currentNodeAvgPrice, previousNodeAvgPrice, AlgoIndex.DeltaStrangle);
                    }
                }
            }
        }
        private InstrumentListNode MoveToSubsequentNode(InstrumentListNode optionNode, double optionDelta, double lowerDelta, double upperDelta, Tick[] ticks, decimal bInstPrice)
        {
            bool nodeFound = false;
            Instrument currentOption = optionNode.Instrument;
            ///TODO: Change this to Case statement.
            if (currentOption.InstrumentType.Trim(' ') == "CE")
            {
                if (optionDelta > upperDelta)
                {
                    while (optionNode.NextNode != null)
                    {
                        optionNode = optionNode.NextNode;
                        currentOption = optionNode.Instrument;
                        if (currentOption.LastPrice == 0)
                        {
                            return null;
                        }
                        ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
                        double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(bInstPrice));
                        delta = Math.Abs(delta);

                        if (double.IsNaN(delta))
                        {
                            return null;
                        }
                        if (delta <= upperDelta * 0.75) //90% of upper delta
                        {
                            nodeFound = true;
                            break;
                        }
                    }
                }
                else
                {
                    while (optionNode.PrevNode != null)
                    {
                        optionNode = optionNode.PrevNode;
                        currentOption = optionNode.Instrument;
                        if (currentOption.LastPrice == 0)
                        {
                            return null;
                        }
                        ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
                        double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(bInstPrice));
                        delta = Math.Abs(delta);
                        if (delta >= upperDelta * 0.75)  // Do not move as the next delta is breaching the upper limit of delta
                        {
                            optionNode = optionNode.NextNode;
                             nodeFound = true;
                            break;
                        }
                        if (delta >= lowerDelta *1.43) //110% of lower delta
                        {
                            nodeFound = true;
                            break;
                        }
                    }

                }
            }
            else
            {
                if (optionDelta > upperDelta)
                {
                    while (optionNode.PrevNode != null)
                    {
                        optionNode = optionNode.PrevNode;
                        currentOption = optionNode.Instrument;
                        if (currentOption.LastPrice == 0)
                        {
                            return null;
                        }
                        ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
                        double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(bInstPrice));
                        delta = Math.Abs(delta);
                        if (double.IsNaN(delta))
                        {
                            return null;
                        }
                        else if (delta <= upperDelta * 0.75) //90% of upper delta
                        {
                            nodeFound = true;
                            break;
                        }
                    }
                }
                else
                {
                    while (optionNode.NextNode != null)
                    {
                        optionNode = optionNode.NextNode;
                        currentOption = optionNode.Instrument;
                        if (currentOption.LastPrice == 0)
                        {
                            return null;
                        }
                        ///TODO: REMOVE DATE TIME PARAMETER FROM THE UPDATE DELTA METHOD IN CASE OF LIVE RUNNING. THIS WAS DONE TO BACKTEST
                        double delta = optionNode.Instrument.UpdateDelta(Convert.ToDouble(currentOption.LastPrice), 0.1, ticks[0].LastTradeTime, Convert.ToDouble(bInstPrice));
                        delta = Math.Abs(delta);
                        if(delta >= upperDelta * 0.75)  // Do not move as the next delta is breaching the upper limit of delta
                        {
                            optionNode = optionNode.PrevNode;
                            nodeFound = true;
                            break;
                        }
                        if (delta >= lowerDelta * 1.43) //110% of lower delta
                        {
                            nodeFound = true;
                            break;
                        }
                    }
                }
            }
            if (!nodeFound)
            {
                currentOption = optionNode.Instrument;
                nodeFound = AssignNextNodes(optionNode, currentOption.BaseInstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);
                if (nodeFound)
                    optionNode = MoveToSubsequentNode(optionNode, optionDelta, lowerDelta, upperDelta, ticks, bInstPrice);
                else
                    if (optionNode.Index > 0)
                    optionNode.LastNode = true;
                else
                    optionNode.FirstNode = true;
            }
            return optionNode;
        }

        private void LoadOptionsToTrade(DateTime currentTime)
        {
            try
            {
                if (_optionUniverse == null)
                {
                    LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, " Loading Tokens from database...", "LoadOptionsToTrade");
                    //Load options asynchronously
                    DataLogic dl = new DataLogic();
                    SortedList<decimal, Instrument> ceSortedList, peSortedList;
                    _optionUniverse = dl.LoadCloseByOptions(_expiryDate, _baseInstrumentToken, _baseInstrumentPrice, 1000, out ceSortedList, out peSortedList);

                    

                    InstrumentLinkedList ceList, peList;

                    ceList = new InstrumentLinkedList(null)
                    {
                        CurrentInstrumentIndex = 0,
                        UpperDelta = _maxDelta,
                        LowerDelta = _minDelta,
                        StopLossPoints = Convert.ToDouble(_stopLoss),
                        NetPrice = 0,
                        listID = _algoInstance,
                        BaseInstrumentToken = _baseInstrumentToken
                    };
                    ceList = new InstrumentLinkedList(null)
                    {
                        CurrentInstrumentIndex = 0,
                        UpperDelta = _maxDelta,
                        LowerDelta = _minDelta,
                        StopLossPoints = Convert.ToDouble(_stopLoss),
                        NetPrice = 0,
                        listID = _algoInstance,
                        BaseInstrumentToken = _baseInstrumentToken
                    };

                    InstrumentListNode strangleTokenNode = null;

                    foreach (var item in ceSortedList)
                    {
                        if (strangleTokenNode == null)
                        {
                            strangleTokenNode = new InstrumentListNode(item.Value)
                            {
                                Index = 0
                            };
                            strangleTokenNode.Prices = null;
                        }
                        else
                        {
                            InstrumentListNode newNode = new InstrumentListNode(item.Value)
                            {
                                Index = 0
                            };
                            newNode.Prices = null;
                            strangleTokenNode.AttachNode(newNode);
                        }
                    }

                    ceList.Current = strangleTokenNode;

                    foreach (Instrument instrument in _optionUniverse)
                    {
                        ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                        ///Other wise it will create a problem with below logic, as new average gets calculated using
                        ///last 2 prices, and retrival below is the average price.

                        
                    }

                    //int strategyId = (int)strangleRow["Id"];
                    InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;


                    Instrument option = new Instrument()
                    {
                        BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Type"],
                        Strike = (Decimal)strangleTokenRow["StrikePrice"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
                    };
                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                        option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

                    ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                    ///Other wise it will create a problem with below logic, as new average gets calculated using
                    ///last 2 prices, and retrival below is the average price.
                    List<Decimal> prices = new List<decimal>();
                    if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
                        prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

                    if (strangleTokenNode == null)
                    {
                        strangleTokenNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        strangleTokenNode.Prices = prices;
                    }
                    else
                    {
                        InstrumentListNode newNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        newNode.Prices = prices;
                        strangleTokenNode.AttachNode(newNode);
                    }




                    InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                            strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                    {
                        CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                        MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                        MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                        StopLossPoints = (double)strangleRow["StopLossPoints"],
                        NetPrice = (decimal)strangleRow["NetPrice"],
                        listID = strategyId,
                        BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
                    };

                    if (ActiveStrangles.ContainsKey(strategyId))
                    {
                        ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
                    }
                    else
                    {
                        InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
                        strangle[(int)instrumentType] = instrumentLinkedList;

                        ActiveStrangles.Add(strategyId, strangle);
                    }


                    ActiveStrangle = new InstrumentLinkedList[2];

                    InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                                strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                    {
                        CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                        MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                        MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                        StopLossPoints = (double)strangleRow["StopLossPoints"],
                        NetPrice = (decimal)strangleRow["NetPrice"],
                        listID = strategyId,
                        BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
                    };



                    //foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
                    //{
                    InstrumentListNode strangleTokenNode = null;
                        foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
                        {
                            Instrument option = new Instrument()
                            {
                                BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                                InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                                InstrumentType = (string)strangleTokenRow["Type"],
                                Strike = (Decimal)strangleTokenRow["StrikePrice"],
                                TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
                            };
                            if (strangleTokenRow["Expiry"] != DBNull.Value)
                                option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

                            ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                            ///Other wise it will create a problem with below logic, as new average gets calculated using
                            ///last 2 prices, and retrival below is the average price.
                            List<Decimal> prices = new List<decimal>();
                            if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
                                prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

                            if (strangleTokenNode == null)
                            {
                                strangleTokenNode = new InstrumentListNode(option)
                                {
                                    Index = (int)strangleTokenRow["InstrumentIndex"]
                                };
                                strangleTokenNode.Prices = prices;
                            }
                            else
                            {
                                InstrumentListNode newNode = new InstrumentListNode(option)
                                {
                                    Index = (int)strangleTokenRow["InstrumentIndex"]
                                };
                                newNode.Prices = prices;
                                strangleTokenNode.AttachNode(newNode);
                            }
                        //}
                        int strategyId = (int)strangleRow["Id"];
                        InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

                        InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                                strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                        {
                            CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                            MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                            MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                            StopLossPoints = (double)strangleRow["StopLossPoints"],
                            NetPrice = (decimal)strangleRow["NetPrice"],
                            listID = strategyId,
                            BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
                        };

                        if (ActiveStrangles.ContainsKey(strategyId))
                        {
                            ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
                        }
                        else
                        {
                            InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
                            strangle[(int)instrumentType] = instrumentLinkedList;

                            ActiveStrangles.Add(strategyId, strangle);
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

        public void LoadOptionsToTrade(DateTime currentTime)
        {
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveData(AlgoIndex.DeltaStrangle);
            DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                InstrumentListNode strangleTokenNode = null;
                foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
                {
                    Instrument option = new Instrument()
                    {
                        BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Type"],
                        Strike = (Decimal)strangleTokenRow["StrikePrice"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
                    };
                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                        option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

                    ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                    ///Other wise it will create a problem with below logic, as new average gets calculated using
                    ///last 2 prices, and retrival below is the average price.
                    List<Decimal> prices = new List<decimal>();
                    if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
                        prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

                    if (strangleTokenNode == null)
                    {
                        strangleTokenNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        strangleTokenNode.Prices = prices;
                    }
                    else
                    {
                        InstrumentListNode newNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        newNode.Prices = prices;
                        strangleTokenNode.AttachNode(newNode);
                    }
                }
                int strategyId = (int)strangleRow["Id"];
                InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

                InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                {
                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                    MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                    MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId,
                    BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
                };

                if (ActiveStrangles.ContainsKey(strategyId))
                {
                    ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
                }
                else
                {
                    InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
                    strangle[(int)instrumentType] = instrumentLinkedList;

                    ActiveStrangles.Add(strategyId, strangle);
                }
            }
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
                    if (dataUpdated)
                    {
                        LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Info, currentTime, "Subscribing to new tokens", "UpdateInstrumentSubscription");
                        Task task = Task.Run(() => OnOptionUniverseChange(this));
                    }
                }
            }
            catch (Exception ex)
            {
                _stopTrade = true;
                Logger.LogWrite(String.Format("{0}, {1}", ex.Message, ex.StackTrace));
                Logger.LogWrite("Closing Application");
                LoggerCore.PublishLog(_algoInstance, _algoIndex, LogLevel.Error, currentTime, String.Format(@"Error occurred! Trading has stopped. \r\n {0}", ex.Message), "UpdateInstrumentSubscription");
                Thread.Sleep(100);
            }
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
        //make sure ref is working with struct . else make it class
        public virtual async Task<bool> OnNext(Tick[] ticks)
        {
            try
            {
                foreach (KeyValuePair<int, InstrumentLinkedList[]> keyValuePair in ActiveStrangles)
                {
                    ReviewStrangle(keyValuePair.Value[0], ticks);
                    ReviewStrangle(keyValuePair.Value[1], ticks);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return true;
        }
        private void UpdateLastPriceOnAllNodes(ref InstrumentListNode currentNode, ref Tick[] ticks)
        {
            Instrument option;
            Tick optionTick;
           
            int currentNodeIndex = currentNode.Index;

            //go to the first node:
            while(currentNode.PrevNode != null)
            {
                currentNode = currentNode.PrevNode;
            }

            //Update all the price all the way till the end
            while (currentNode.NextNode != null)
            {
                option = currentNode.Instrument;
                
                optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == option.InstrumentToken);
                if (optionTick.LastPrice != 0)
                {
                    option.LastPrice = optionTick.LastPrice;
                    currentNode.Instrument = option;

                }
                currentNode = currentNode.NextNode;
            }

            //Come back to the current node
            while(currentNode.PrevNode != null)
            {
                if(currentNode.Index == currentNodeIndex)
                {
                    break;
                }
                currentNode = currentNode.PrevNode;
            }
        }

        public virtual void OnError(Exception ex)
        {
            ///TODO: Log the error. Also handle the error.
        }

        /// <summary>
        /// Currently Implemented only for delta range.
        /// </summary>
        /// <param name="bToken"></param>
        /// <param name="bValue"></param>
        /// <param name="peToken"></param>
        /// <param name="pevalue"></param>
        /// <param name="ceToken"></param>
        /// <param name="ceValue"></param>
        /// <param name="peLowerValue"></param>
        /// <param name="pelowerDelta"></param>
        /// <param name="peUpperValue"></param>
        /// <param name="peUpperDelta"></param>
        /// <param name="ceLowerValue"></param>
        /// <param name="celowerDelta"></param>
        /// <param name="ceUpperValue"></param>
        /// <param name="ceUpperDelta"></param>
        /// <param name="stopLossPoints"></param>
        public void ManageStrangleDelta(Instrument bInst, Instrument currentPE, Instrument currentCE,
            double pelowerDelta = 0,  double peUpperDelta = 0, double celowerDelta = 0, 
            double ceUpperDelta = 0, double stopLossPoints = 0, int strangleId = 0)
        {
            if (currentPE.LastPrice * currentCE.LastPrice != 0)
            {
                ///TODO: placeOrder lowerPutValue and upperCallValue
                /// Get Executed values on to lowerPutValue and upperCallValue

                //Two seperate linked list are maintained with their incides on the linked list.
                InstrumentListNode put = new InstrumentListNode(currentPE);
                InstrumentListNode call = new InstrumentListNode(currentCE);

                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                    //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                    //                                            Convert.ToString(currentPE.InstrumentToken) });

                    //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                    //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;

                    //TODO -> First price
                    decimal pePrice = currentPE.LastPrice;
                    decimal cePrice = currentCE.LastPrice;

                    put.Prices.Add(pePrice); //put.SellPrice = 100;
                    call.Prices.Add(cePrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, call.Prices.Sum(), 
                        put.Prices.Sum(), AlgoIndex.DeltaStrangle, celowerDelta, ceUpperDelta, pelowerDelta, peUpperDelta, 
                        stopLossPoints = 0);
                }
            }
        }
        public void ManageStrangleDelta(uint peToken, uint ceToken,string peSymbol, string ceSymbol,
           double pelowerDelta = 0, double peUpperDelta = 0, double celowerDelta = 0,
           double ceUpperDelta = 0, double stopLossPoints = 0, int strangleId = 0)
        {
                //If new strangle, place the order and update the data base. If old strangle monitor it.
                if (strangleId == 0)
                {
                //Dictionary<string, Quote> keyValuePairs = ZObjects.kite.GetQuote(new string[] { Convert.ToString(currentCE.InstrumentToken),
                //                                            Convert.ToString(currentPE.InstrumentToken) });

                //decimal cePrice = keyValuePairs[Convert.ToString(currentCE.InstrumentToken)].Bids[0].Price;
                //decimal pePrice = keyValuePairs[Convert.ToString(currentPE.InstrumentToken)].Bids[0].Price;

                ////TODO -> First price
                //decimal pePrice = currentPE.LastPrice;
                //decimal cePrice = currentCE.LastPrice;

                //put.Prices.Add(pePrice); //put.SellPrice = 100;
                //call.Prices.Add(cePrice);  // call.SellPrice = 100;

                //Uncomment below for real time orders
                decimal pePrice = PlaceOrder(peSymbol, false);
                decimal cePrice = PlaceOrder(ceSymbol, false);
                //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

                //Update Database
                DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(ceToken, peToken, cePrice,
                        pePrice, AlgoIndex.DeltaStrangle, celowerDelta, ceUpperDelta, pelowerDelta, peUpperDelta,
                        stopLossPoints = 0);
                }
        }

        //bool AssignNextNodesWithDelta(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
        //    decimal currentStrikePrice, DateTime expiry, DateTime? tickTimeStamp)
        //{
        //    DataLogic dl = new DataLogic();
        //    SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
        //        currentStrikePrice, expiry, currentNode.Index);

        //    if(NodeData.Count == 0)
        //    {
        //        return false;
        //    }
        //    Instrument currentInstrument = currentNode.Instrument;

        //    NodeData.Add(currentInstrument.Strike, currentInstrument);

        //    int currentIndex = currentNode.Index;
        //    int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);

        //    InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
        //    baseNode = firstOption;
        //    int index = currentIndex - currentNodeIndex;


        //    for (byte i = 1; i < NodeData.Values.Count; i++)
        //    {
        //        InstrumentListNode option = new InstrumentListNode(NodeData.Values[i]);

        //        baseNode.NextNode = option;
        //        baseNode.Index = index;
        //        option.PrevNode = baseNode;
        //        baseNode = option;
        //        index++;
        //    }
        //    baseNode.Index = index; //to assign index to the last node

        //    if (currentNodeIndex == 0)
        //    {
        //        firstOption.NextNode.PrevNode = currentNode;
        //        currentNode.NextNode = firstOption.NextNode;
        //    }
        //    else if (currentNodeIndex == NodeData.Values.Count - 1)
        //    {
        //        baseNode.PrevNode.NextNode = currentNode;
        //        currentNode.PrevNode = baseNode.PrevNode;
        //    }
        //    else
        //    {
        //        while (baseNode.PrevNode != null)
        //        {
        //            if (baseNode.Index == currentIndex)
        //            {
        //                currentNode.PrevNode = baseNode.PrevNode;
        //                currentNode.NextNode = baseNode.NextNode;
        //                break;
        //            }
        //            baseNode = baseNode.PrevNode;
        //        }
        //    }

        //    return true;
        //}
        /// <summary>
        /// Pulls nodes data from database on both sides
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="baseInstrumentToken"></param>
        /// <param name="instrumentType"></param>
        /// <param name="currentStrikePrice"></param>
        /// <param name="expiry"></param>
        /// <param name="updownboth"></param>
        /// <returns></returns>
        bool AssignNextNodes(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
        decimal currentStrikePrice, DateTime expiry)
        {
            DataLogic dl = new DataLogic();

            int searchIndex = 0;
            if(currentNode.NextNode == null)
            {
                searchIndex++;
            }
            if (currentNode.PrevNode == null)
            {
                searchIndex--;
            }

            SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
                currentStrikePrice, expiry, searchIndex);

            if(NodeData.Count == 0)
            {
                if (currentNode.Index > 0)
                    currentNode.LastNode = true;
                else
                    currentNode.FirstNode = true;

                return false;
            }

            Instrument currentInstrument = currentNode.Instrument;

            NodeData.Add(currentInstrument.Strike, currentInstrument);

            int currentIndex = currentNode.Index;
            int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);

            InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
            baseNode = firstOption;

            int index = currentIndex - currentNodeIndex;

            for (byte i = 1; i < NodeData.Values.Count; i++)
            {
                InstrumentListNode option = new InstrumentListNode(NodeData.Values[i]);

                baseNode.NextNode = option;
                baseNode.Index = index;
                option.PrevNode = baseNode;
                baseNode = option;
                index++;
            }
            baseNode.Index = index; //to assign index to the last node

            if (currentNodeIndex == 0)
            {
                firstOption.NextNode.PrevNode = currentNode;
                currentNode.NextNode = firstOption.NextNode;
            }
            else if (currentNodeIndex == NodeData.Values.Count - 1)
            {
                baseNode.PrevNode.NextNode = currentNode;
                currentNode.PrevNode = baseNode.PrevNode;
            }
            else
            {
                while (baseNode.PrevNode != null)
                {
                    if (baseNode.Index == currentIndex)
                    {
                        baseNode.Prices = currentNode.Prices;
                        currentNode.PrevNode = baseNode.PrevNode;
                        currentNode.NextNode = baseNode.NextNode;
                        break;
                    }
                    baseNode = baseNode.PrevNode;
                }
            }

            return true;
        }

        public void LoadActiveStrangles()
        {
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveData(AlgoIndex.DeltaStrangle);
            DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token", new DataColumn[] { activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

            foreach (DataRow strangleRow in activeStrangles.Tables[0].Rows)
            {
                InstrumentListNode strangleTokenNode = null;
                foreach (DataRow strangleTokenRow in strangleRow.GetChildRows(strangle_Token_Relation))
                {
                    Instrument option = new Instrument()
                    {
                        BaseInstrumentToken = Convert.ToUInt32(strangleTokenRow["BInstrumentToken"]),
                        InstrumentToken = Convert.ToUInt32(strangleTokenRow["InstrumentToken"]),
                        InstrumentType = (string)strangleTokenRow["Type"],
                        Strike = (Decimal)strangleTokenRow["StrikePrice"],
                        TradingSymbol = (string)strangleTokenRow["TradingSymbol"]
                    };
                    if (strangleTokenRow["Expiry"] != DBNull.Value)
                        option.Expiry = Convert.ToDateTime(strangleTokenRow["Expiry"]);

                    ///TODO: Each trade should be stored seperately so that prices can be stored sepertely. 
                    ///Other wise it will create a problem with below logic, as new average gets calculated using
                    ///last 2 prices, and retrival below is the average price.
                    List<Decimal> prices = new List<decimal>();
                    if ((decimal)strangleTokenRow["LastSellingPrice"] != 0)
                        prices.Add((decimal)strangleTokenRow["LastSellingPrice"]);

                    if (strangleTokenNode == null)
                    {
                        strangleTokenNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        strangleTokenNode.Prices = prices;
                    }
                    else
                    {
                        InstrumentListNode newNode = new InstrumentListNode(option)
                        {
                            Index = (int)strangleTokenRow["InstrumentIndex"]
                        };
                        newNode.Prices = prices;
                        strangleTokenNode.AttachNode(newNode);
                    }
                }
                int strategyId = (int)strangleRow["Id"];
                InstrumentType instrumentType = (string)strangleRow["OptionType"] == "CE" ? InstrumentType.CE : InstrumentType.PE;

                InstrumentLinkedList instrumentLinkedList = new InstrumentLinkedList(
                        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                {
                    CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                    MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                    MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId,
                    BaseInstrumentToken = strangleTokenNode.Instrument.BaseInstrumentToken
                };

                if (ActiveStrangles.ContainsKey(strategyId))
                {
                    ActiveStrangles[strategyId][(int)instrumentType] = instrumentLinkedList;
                }
                else
                {
                    InstrumentLinkedList[] strangle = new InstrumentLinkedList[2];
                    strangle[(int)instrumentType] = instrumentLinkedList;

                    ActiveStrangles.Add(strategyId, strangle);
                }
            }

        }

        private decimal PlaceOrder(string Symbol, bool buyOrder, InstrumentLinkedList optionList)
        {
            //temp
            decimal price = 0;
            if (optionList.Current.Instrument.LastPrice == 0)
            {
                price = optionList.Current.Prices.Last();
            }
            else
            {
                price = optionList.Current.Instrument.LastPrice;
            }
            System.Net.Mail.SmtpClient email = new System.Net.Mail.SmtpClient("smtp.gmail.com");
            email.SendAsync("prashantholahal@gmail.com", "prashant.malviya@ge.com", "Delta trigerred", string.Format("ICAM Support Automated Message@ {0}: {1} - Buy: {2} @ {3}", DateTime.Now.ToString(),  Symbol, buyOrder.ToString(), price.ToString()), null);

            return price;
            //Dictionary<string, dynamic> orderStatus;

            //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = orderStatus["data"]["order_id"];
            //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //return orderInfo[orderInfo.Count - 1].AveragePrice;
        }
        private decimal PlaceOrder(string Symbol, bool buyOrder)
        {
            decimal price = 0;
            System.Net.Mail.SmtpClient email = new System.Net.Mail.SmtpClient("smtp.gmail.com");
            email.SendAsync("prashantholahal@gmail.com", "prashant.malviya@ge.com", "Delta trigerred", string.Format("ICAM Support Automated Message@ {0}: {1} - Buy: {2} @ {3}", DateTime.Now.ToString(), Symbol, buyOrder.ToString(), price.ToString()), null);

            return 0;
            //Dictionary<string, dynamic> orderStatus;

            //orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
            //                          buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
            //                          OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            //string orderId = orderStatus["data"]["order_id"];
            //List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            //return orderInfo[orderInfo.Count - 1].AveragePrice;
        }

        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(TickDataStreamer publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(Ticker publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}

        public virtual void Unsubscribe()
        {
            UnsubscriptionToken.Dispose();
        }

        public virtual void OnCompleted()
        {
        }
    }
}
