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

namespace Algos.TLogics
{
    public class ManageStrangleValue : IZMQ //, IObserver<Tick[]>
    {
        //LinkedList<decimal> lowerPuts;
        //LinkedList<decimal> upperCalls;

        public ManageStrangleValue()
        {
            LoadActiveStrangles();
        }
            Dictionary<int, InstrumentLinkedList[]> ActiveStrangles = new Dictionary<int, InstrumentLinkedList[]>();

        //InstrumentLinkedList Puts;
        //InstrumentLinkedList Calls;
        // Consumer consumer;
        List<UInt32> assignedTokens;

        //public List<UInt32> ITokensToSubscribe { get; set; }
        public IDisposable UnsubscriptionToken;


        //Variable set based on actual data

        Instrument _bInst;
        
        

        //decimal _peLowerValue, _peUpperValue, _ceLowerValue, _ceUpperValue;
        //double _pelowerDelta, _peUpperDelta, _celowerDelta, _ceUpperDelta;
        //decimal _stopLossPoints;


        //public virtual void Subscribe(Publisher publisher)
        //{
        //    UnsubscriptionToken = publisher.Subscribe(this);
        //}
        //public virtual void Subscribe(TickDataStreamer publisher)
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

        private void ReviewStrangle(InstrumentLinkedList optionList, Tick tick)
        {
            Tick[] ticks = new Tick[] { tick };
            try
            {
                    InstrumentListNode optionNode = optionList.Current;
                    Instrument currentOption = optionNode.Instrument;
                try
                {

                    /// ToDO: Remove this on the live environment as this will come with the tick data
                    if (currentOption.LastPrice == 0)
                    {
                        DataLogic dl = new DataLogic();
                        currentOption.LastPrice = dl.RetrieveLastPrice(currentOption.InstrumentToken, Convert.ToDateTime("2019-07-22 09:00:00"));

                        optionList.Current.Instrument = currentOption;
                    }

                    _bInst.InstrumentToken = currentOption.BaseInstrumentToken;

                    Tick baseInstrumentTick = ticks.FirstOrDefault(x => x.InstrumentToken == _bInst.InstrumentToken);
                    if (baseInstrumentTick.LastPrice != 0)
                    {
                        _bInst.LastPrice = baseInstrumentTick.LastPrice;
                    }
                }
                catch
                {

                }

                try
                {

                    Tick optionTick = ticks.FirstOrDefault(x => x.InstrumentToken == currentOption.InstrumentToken);
                    if (optionTick.LastPrice != 0)
                    {
                        currentOption.LastPrice = optionTick.LastPrice;
                        optionList.Current.Instrument = currentOption;
                    }
                }
                catch
                {

                }

                try
                {
                    //Pulling next nodes from the begining and keeping it up to date from the ticks. This way next nodes will be available when needed, as Tick table takes time.
                    //Doing it on Async way
                    if (optionNode.NextNode == null || optionNode.PrevNode == null)
                        AssignNextNodes(optionNode, _bInst.InstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);

                }
                catch
                {

                }
                try
                {
                    UpdateLastPriceOnAllNodes(ref optionNode, ref ticks);
                }
                catch
                {

                }

                try
                {
                    if (MoveNodes(optionNode, optionList, currentOption))
                    {
                        //put P&L impact on node level on the linked list
                        //decimal previousNodeAvgPrice = PlaceOrder(currentOption.TradingSymbol, buyOrder: true);
                        optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: true, optionList, ticks) * -1); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                        decimal previousNodeBuyPrice = optionNode.Prices.Last();  //optionNode.Prices.Skip(optionNode.Prices.Count - 2).Sum();

                        uint previousInstrumentToken = currentOption.InstrumentToken;
                        int previousInstrumentIndex = optionNode.Index;
                        //Logic to determine the next node and move there
                        optionNode = MoveToSubsequentNode(optionNode, currentOption.LastPrice, optionList, ticks);

                        currentOption = optionNode.Instrument;
                        optionList.Current = optionNode;

                        //if (optionNode.Prices.Count > 0 && optionNode.Prices.First() == 0)
                        //{
                        //    optionNode.Prices.RemoveAt(0);
                        //}
                        optionNode.Prices.Add(PlaceOrder(currentOption.TradingSymbol, buyOrder: false, optionList, ticks));
                        decimal currentNodeSellPrice = optionNode.Prices.Last(); //buy prices are negative and sell are positive. as sign denotes cashflow direction

                        //Do we need current instrument index , when current instrument is already present in the linked list?
                        //It can see the index of current instrument using list.current.index
                        optionList.CurrentInstrumentIndex = optionNode.Index;



                        //make next symbol as next to current node. Also update PL data in linklist main
                        //also update currnetindex in the linklist main
                        DataLogic dl = new DataLogic();
                        dl.UpdateListData(optionList.listID, currentOption.InstrumentToken, optionNode.Index, previousInstrumentIndex, currentOption.InstrumentType,
                           previousInstrumentToken, currentNodeSellPrice, previousNodeBuyPrice, AlgoIndex.PriceStrangle);
                    }
                }
                catch
                {

                }
            }
            catch (Exception exp)
            {
                //throw exp;
            }
        }
        private bool MoveNodes(InstrumentListNode optionNode, InstrumentLinkedList optionList, Instrument currentOption)
        {
            try
            {
                if (optionNode.Prices.Count > 0)
                {
                    if (optionList.MaxProfitPoints > 0 && optionList.MaxLossPoints > 0)
                    {
                        if ((optionNode.Prices.Last() > optionList.MaxProfitPoints) &&
                                (currentOption.LastPrice < optionNode.Prices.Last() - optionList.MaxProfitPoints || currentOption.LastPrice > optionNode.Prices.Last() + optionList.MaxLossPoints))
                        {
                            return true;
                        }
                    }
                    else if (optionList.MaxProfitPercent > 0 && optionList.MaxLossPercent > 0)
                    {
                        if (currentOption.LastPrice < optionNode.Prices.Last() * optionList.MaxProfitPercent || currentOption.LastPrice > optionNode.Prices.Last() * optionList.MaxLossPercent)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {

            }
            return false;
        }
        private InstrumentListNode MoveToSubsequentNode(InstrumentListNode optionNode, decimal optionPrice, InstrumentLinkedList optionList, Tick[] ticks)
        {
            try
            {
                bool nodeFound = false;
                Instrument currentOption = optionNode.Instrument;
                decimal initialSellingPrice = optionNode.Prices.ElementAtOrDefault(optionNode.Prices.Count - 2);
                ///TODO: Change this to Case statement.
                if (currentOption.InstrumentType.Trim(' ') == "CE")
                {
                    if (optionPrice > (optionList.MaxLossPoints == 0 ? initialSellingPrice * optionList.MaxLossPercent : initialSellingPrice + optionList.MaxLossPoints))
                    {
                        while (optionNode.NextNode != null)
                        {
                            optionNode = optionNode.NextNode;

                            //No condition modelled to determine the next node
                            //if (optionNode.Instrument.LastPrice <= upperPrice * 0.8m) //90% of upper delta
                            //{
                            nodeFound = true;
                            break;
                            //}
                        }

                    }
                    else
                    {
                        while (optionNode.PrevNode != null)
                        {
                            optionNode = optionNode.PrevNode;
                            //No condition modelled to determine the next node
                            //if (optionNode.Instrument.LastPrice >= lowerPrice * 1.2m) //110% of lower delta
                            //{
                            nodeFound = true;
                            break;
                            //}
                        }

                    }
                }
                else
                {
                    if (optionPrice > (optionList.MaxLossPoints == 0 ? initialSellingPrice * optionList.MaxLossPercent : initialSellingPrice + optionList.MaxLossPoints))
                    {
                        while (optionNode.PrevNode != null)
                        {
                            optionNode = optionNode.PrevNode;
                            //No condition modelled to determine the next node
                            //if (optionNode.Instrument.LastPrice <= upperPrice * 0.8m) //90% of upper delta
                            //{
                            nodeFound = true;
                            break;
                            //}
                        }
                    }
                    else
                    {
                        while (optionNode.NextNode != null)
                        {
                            optionNode = optionNode.NextNode;
                            //No condition modelled to determine the next node
                            //if (optionNode.Instrument.LastPrice >= lowerPrice * 1.2m) //110% of lower delta
                            //{
                            nodeFound = true;
                            break;
                            //}
                        }

                    }
                }

                if (!nodeFound)
                {
                    currentOption = optionNode.Instrument;
                    AssignNextNodes(optionNode, currentOption.BaseInstrumentToken, currentOption.InstrumentType, currentOption.Strike, currentOption.Expiry.Value);
                    optionNode = MoveToSubsequentNode(optionNode, optionPrice, optionList, ticks);
                }

            }
            catch
            {

            }
            return optionNode;

        }


        //make sure ref is working with struct . else make it class
        public virtual void OnNext(Tick tick)
        {
            //for (int i = 0; i < ActiveStrangles.Count; i++)
            //{
            //    await ReviewStrangle(ActiveStrangles[i][0], ticks);
            //    await ReviewStrangle(ActiveStrangles[i][1], ticks);
            //}
            foreach(KeyValuePair<int, InstrumentLinkedList[]> keyValuePair in ActiveStrangles)
            {
                 ReviewStrangle(keyValuePair.Value[0], tick);
                 ReviewStrangle(keyValuePair.Value[1], tick);
                //await ReviewStrangle(ActiveStrangles[i][1], ticks);
            }
            return;
        }

        private void LoadActiveStrangles()
        {
            //InstrumentLinkedList[] strangleList = new InstrumentLinkedList[2] {
            //    new InstrumentLinkedList(),
            //    new InstrumentLinkedList()
            //};
            
            DataLogic dl = new DataLogic();
            DataSet activeStrangles = dl.RetrieveActiveData(AlgoIndex.PriceStrangle);
            DataRelation strangle_Token_Relation = activeStrangles.Relations.Add("Strangle_Token",new DataColumn[] {activeStrangles.Tables[0].Columns["Id"], activeStrangles.Tables[0].Columns["OptionType"] },
                new DataColumn[] { activeStrangles.Tables[1].Columns["StrategyId"], activeStrangles.Tables[1].Columns["Type"] });

            

            

            byte strangleCount = 0;
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
                    if((decimal)strangleTokenRow["LastSellingPrice"] != 0)
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
                    MaxLossPercent = (decimal)strangleRow["MaxLossPercent"],
                    MaxProfitPercent = (decimal)strangleRow["MaxProfitPercent"],
                    StopLossPoints = (double)strangleRow["StopLossPoints"],
                    NetPrice = (decimal)strangleRow["NetPrice"],
                    listID = strategyId
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

                //strangle[strangleCount++] = new InstrumentLinkedList(
                //        strangleTokenNode.GetNodebyIndex((Int16)strangleRow["CurrentIndex"]))
                //    {
                //        CurrentInstrumentIndex = (Int16)strangleRow["CurrentIndex"],
                //        MaxLossPoints = (decimal)strangleRow["MaxLossPoints"],
                //        MaxProfitPoints = (decimal)strangleRow["MaxProfitPoints"],
                //        StopLossPoints = (decimal)strangleRow["StopLossPoints"]
                //    };
                //// int strangleID = (int)strangleTokenRow["StrangleId"];
                ////strangleNodeList.Add(strangleID, new SortedList<decimal, InstrumentListNode>(strangleTokenNode.Index, strangleTokenNode));

                //ActiveStrangles.Add(strangle);
            }
            
        }

        private void UpdateLastPriceOnAllNodes(ref InstrumentListNode currentNode, ref Tick[] ticks)
        {
            Instrument option;
            Tick optionTick;

            int currentNodeIndex = currentNode.Index;

            //go to the first node:
            while (currentNode.PrevNode != null)
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
            while (currentNode.PrevNode != null)
            {
                if (currentNode.Index == currentNodeIndex)
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
        /// Manages the value of put and call in the strangle
        /// </summary>
        /// <param name="bInst"></param>
        /// <param name="currentPE"></param>
        /// <param name="currentCE"></param>
        /// <param name="peLowerValue"></param>
        /// <param name="peUpperValue"></param>
        /// <param name="ceLowerValue"></param>
        /// <param name="ceUpperValue"></param>
        /// <param name="stopLossPoints"></param>
        /// <param name="strangleId"></param>
        public void ManageStrangle(Instrument bInst, Instrument currentPE, Instrument currentCE,
            decimal peMaxProfitPoints = 0, decimal peMaxLossPoints = 0, decimal ceMaxProfitPoints = 0, 
            decimal ceMaxLossPoints = 0, double stopLossPoints = 0, int strangleId = 0, decimal peMaxLossPercent=0, 
            decimal peMaxProfitPercent=0, decimal ceMaxLossPercent=0, decimal ceMaxProfitPercent=0)
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
                    //TEMP -> First price
                    put.Prices.Add(currentPE.LastPrice); //put.SellPrice = 100;
                    call.Prices.Add(currentCE.LastPrice);  // call.SellPrice = 100;

                    ///Uncomment below for real time orders
                    //put.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));
                    //call.Prices.Add(PlaceOrder(currentPE.TradingSymbol, false));

                    //Update Database
                    DataLogic dl = new DataLogic();
                    strangleId = dl.StoreStrangleData(currentCE.InstrumentToken, currentPE.InstrumentToken, call.Prices.Sum(),
                        put.Prices.Sum(), AlgoIndex.PriceStrangle, ceMaxProfitPoints, ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints, 
                        ceMaxProfitPercent, ceMaxLossPercent, peMaxProfitPercent, peMaxLossPercent, stopLossPoints = 0
                        
                        );
                }

                //Calls = new InstrumentLinkedList(call);
                //Puts = new InstrumentLinkedList(put);

                //Calls.MaxProfitPoints = ceMaxProfitPoints;
                //Calls.MaxLossPoints = ceMaxLossPoints;

                //Puts.MaxProfitPoints = peMaxProfitPoints;
                //Puts.MaxLossPoints = peMaxLossPoints;

                //Calls.listID = Puts.listID = strangleId;

                //_bInst = bInst;
            }
        }
        public void ManageStrangle(uint peToken, uint ceToken, string peSymbol, string ceSymbol, decimal peMaxProfitPoints = 0, decimal peMaxLossPoints = 0, decimal ceMaxProfitPoints = 0,
          decimal ceMaxLossPoints = 0, double stopLossPoints = 0, int strangleId = 0, decimal peMaxLossPercent = 0,
          decimal peMaxProfitPercent = 0, decimal ceMaxLossPercent = 0, decimal ceMaxProfitPercent = 0)
        {
            ///TODO: placeOrder lowerPutValue and upperCallValue
            /// Get Executed values on to lowerPutValue and upperCallValue

            //If new strangle, place the order and update the data base. If old strangle monitor it.
            //TEMP -> First price
            decimal pePrice = 100;
            decimal cePrice = 100;

            ///Uncomment below for real time orders
            pePrice = PlaceOrder(peSymbol, false);
            cePrice = PlaceOrder(ceSymbol, false);

            //Update Database
            DataLogic dl = new DataLogic();
            strangleId = dl.StoreStrangleData(ceToken, peToken, cePrice,
               pePrice, AlgoIndex.PriceStrangle, ceMaxProfitPoints, ceMaxLossPoints, peMaxProfitPoints, peMaxLossPoints,
                ceMaxProfitPercent, ceMaxLossPercent, peMaxProfitPercent, peMaxLossPercent, stopLossPoints = 0

                );

        }
        bool AssignNextNodesWithDelta(InstrumentListNode currentNode, UInt32 baseInstrumentToken, string instrumentType,
            decimal currentStrikePrice, DateTime expiry, DateTime? tickTimeStamp)
        {
            DataLogic dl = new DataLogic();
            SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
                currentStrikePrice, expiry, currentNode.Index);

            Instrument currentInstrument = currentNode.Instrument;

            NodeData.Add(currentInstrument.Strike, currentInstrument);

            //InstrumentListNode tempNode = currentNode;

            int currentIndex = currentNode.Index;
            int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);



            InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
            baseNode = firstOption;
            //byte index = baseNode.Index = Convert.ToByte(Math.Floor(Convert.ToDouble(NodeData.Values.Count / 2)) * -1);

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
                        currentNode.PrevNode = baseNode.PrevNode;
                        currentNode.NextNode = baseNode.NextNode;
                        break;
                    }
                    baseNode = baseNode.PrevNode;
                }
            }

            return true;
        }
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
            try
            {
                DataLogic dl = new DataLogic();
                SortedList<Decimal, Instrument> NodeData = dl.RetrieveNextNodes(baseInstrumentToken, instrumentType,
                    currentStrikePrice, expiry, currentNode.Index);

                Instrument currentInstrument = currentNode.Instrument;

                NodeData.Add(currentInstrument.Strike, currentInstrument);

                //InstrumentListNode tempNode = currentNode;

                int currentIndex = currentNode.Index;
                int currentNodeIndex = NodeData.IndexOfKey(currentInstrument.Strike);



                InstrumentListNode baseNode, firstOption = new InstrumentListNode(NodeData.Values[0]);
                baseNode = firstOption;
                //byte index = baseNode.Index = Convert.ToByte(Math.Floor(Convert.ToDouble(NodeData.Values.Count / 2)) * -1);

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
            }
            catch
            {

            }
            return true;
            
        }

        private decimal PlaceOrder(string Symbol, bool buyOrder, InstrumentLinkedList optionList, Tick[] ticks)
        {
            //temp
            if (optionList.Current.Instrument.LastPrice == 0)
            {
                if (optionList.Current.Prices.Count == 0)
                {
                   
                        DataLogic dl = new DataLogic();
                        return  dl.RetrieveLastPrice(optionList.Current.Instrument.InstrumentToken, ticks[0].LastTradeTime);

                      
                }

                return optionList.Current.Prices.Last();
            }
            else
            {
                return optionList.Current.Instrument.LastPrice;
            }

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
            Dictionary<string, dynamic> orderStatus;

            orderStatus = ZObjects.kite.PlaceOrder(Constants.EXCHANGE_NFO, Symbol,
                                      buyOrder ? Constants.TRANSACTION_TYPE_BUY : Constants.TRANSACTION_TYPE_BUY, 75, Product: Constants.PRODUCT_MIS,
                                      OrderType: Constants.ORDER_TYPE_MARKET, Validity: Constants.VALIDITY_DAY);

            string orderId = orderStatus["data"]["order_id"];
            List<Order> orderInfo = ZObjects.kite.GetOrderHistory(orderId);
            return orderInfo[orderInfo.Count - 1].AveragePrice;
        }
    }
}
